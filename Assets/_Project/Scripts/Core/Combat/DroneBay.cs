using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 드론 사출대(로봇 B). 밸런스 문서 10-3 · 전투 시스템 문서.
    ///
    /// ⚠️ **사출대는 재고 층이 아니다**(260829_V03 §판정②). 재고는 세 층뿐이고
    /// (노드 출력 버퍼 · 저장 노드 · **마운트 적재**) 사출대라는 층은 없다 —
    /// 사출대는 화면에서 **어디로 나오느냐**이지 어디에 담겨 있느냐가 아니다.
    /// 그래서 대기열을 여기서 들지 않고 **마운트에서 꺼내 쏜다.**
    /// 이걸 자기 큐로 들고 있던 동안 로봇 B의 마운트는 구조적으로 영구히 비었고,
    /// 태그의 두 트리거가 **둘 다** 막혀 있었다.
    ///
    /// 실효 방출량 = **min(유입, 슬롯 × 방출률)**. 두 병목 중 낮은 쪽이 이긴다:
    ///   유입이 모자라면 슬롯이 놀고, 슬롯이 모자라면 만든 드론이 쌓인다.
    /// 이 min이 「물류가 전투력을 만든다」의 로봇 B 판이다 — 유입은 보드가 만든다.
    ///
    /// 드론은 **B의 탄약**이다(밸런스: 소모품 · 충전량 = 피해 총량 = 수명).
    /// 그래서 다 쓰면 소멸하고 회수되지 않는다 — 탄약이 되돌아오지 않는 것과 같다.
    ///
    /// 슬롯 수·방출률·기당 피해는 전부 TBD placeholder다. 여기서 값을 정하지 않는다.
    /// </summary>
    public sealed class DroneBay
    {
        private float _allowance;  // 방출률이 허용한 몫(소수 누적) — 아래 주석 참조

        // dt를 잘게 더하면 1기가 정확히 1.0이 아니라 0.9999…로 끝나 그 기체가 다음 틱으로 밀린다.
        // 잔여가 이월되므로 장기 방출률은 맞지만 초 경계에서 한 기가 늦는다 —
        // 발사 누산기(CombatSimulation.FireEpsilon)와 같은 뿌리이고 같은 방식으로 푼다.
        private const float LaunchEpsilon = 1e-4f;

        public DroneBay(int slots, float releaseRatePerSlot, float chargePerDrone)
        {
            Slots = Mathf.Max(0, slots);
            ReleaseRatePerSlot = Mathf.Max(0f, releaseRatePerSlot);
            ChargePerDrone = Mathf.Max(0f, chargePerDrone);
        }

        /// <summary>동시 출격 슬롯 수(params slot = 3, 강화 비대상 상수).</summary>
        public int Slots { get; }

        /// <summary>슬롯당 방출률(기/초/슬롯) — params r.</summary>
        public float ReleaseRatePerSlot { get; }

        /// <summary>드론 1기의 충전량 = 피해 총량 = 수명(params dB).</summary>
        public float ChargePerDrone { get; }

        /// <summary>현재 필드에 나가 있는 드론 수. 슬롯을 점유한다.</summary>
        public int Active { get; private set; }

        /// <summary>슬롯 상한에 걸린 초당 방출 능력(기/초).</summary>
        public float SlotThroughput => Slots * ReleaseRatePerSlot;

        /// <summary>실효 방출량(기/초) = min(유입, 슬롯 × 방출률). 낮은 쪽이 병목이다.</summary>
        public float EffectiveRelease(float inflowPerSec) =>
            Mathf.Min(Mathf.Max(0f, inflowPerSec), SlotThroughput);

        /// <summary>유입이 병목인가(슬롯이 놀고 있는가). 진단 표시용.</summary>
        public bool InflowLimited(float inflowPerSec) => inflowPerSec < SlotThroughput;

        /// <summary>
        /// 이번 틱 출격 수. 슬롯 여유 · 방출 허용량 · **마운트에 실린 드론** 중 가장 적은 쪽이 나간다.
        /// 방출률은 시간당 상한이므로 dt를 곱해 이번 틱 몫을 낸다.
        ///
        /// 마운트에서 빼는 것은 호출자가 한다 — 사출대가 재고를 건드리면
        /// 「재고는 마운트가 든다」가 두 곳으로 갈린다.
        /// </summary>
        public int Launch(float dt, float loadedInMount)
        {
            if (dt <= 0f || Slots <= 0) return 0;

            // 방출 허용량은 **누적한다.** 틱마다 버리면 dt가 작을 때 영영 못 나간다 —
            // 처리량 3기/초에 dt 0.02면 틱당 0.06기이고, 정수로 내리면 언제나 0이다.
            // 탄약 재고의 소수 이월과 같은 문제이고 같은 방식으로 푼다.
            _allowance += SlotThroughput * dt;

            int freeSlots = Slots - Active;
            if (freeSlots <= 0) return 0;

            float available = Mathf.Max(0f, loadedInMount);
            int count = Mathf.FloorToInt(Mathf.Min(available, Mathf.Min(freeSlots, _allowance)) + LaunchEpsilon);
            if (count <= 0) return 0;

            _allowance -= count;
            Active += count;
            return count;
        }

        /// <summary>
        /// 드론이 사라졌다(충전량 소진·전투 종료). **슬롯이 즉시 빈다** —
        /// 늦게 비우면 방출률 r이 의미를 잃고 min(유입, 슬롯 × r) 식이 성립하지 않는다.
        /// </summary>
        public void Retire(int count = 1)
        {
            Active = Mathf.Max(0, Active - Mathf.Max(0, count));
        }

        /// <summary>전투 종료 정리. 필드가 없어지므로 나가 있던 드론도 함께 사라진다.</summary>
        public void Reset()
        {
            Active = 0;
            _allowance = 0f;
        }
    }

    /// <summary>
    /// 필드에 나가 있는 드론 1기. **충전량이 곧 수명**이라 쏠수록 줄고 0이 되면 소멸한다.
    /// 표적은 본체와 같은 최근접 규칙을 쓰되 **기준점이 드론 자신**이라 본체와 다른 적을 칠 수 있다.
    /// </summary>
    public sealed class DroneUnit
    {
        public DroneUnit(Vector2 position, float charge, float damagePerHit, float attackRange)
        {
            Position = position;
            Charge = Mathf.Max(0f, charge);
            DamagePerHit = Mathf.Max(0f, damagePerHit);
            AttackRange = Mathf.Max(0f, attackRange);
        }

        public Vector2 Position { get; set; }

        /// <summary>남은 충전량 = 남은 피해 총량 = 남은 수명.</summary>
        public float Charge { get; private set; }

        public float DamagePerHit { get; }

        /// <summary>사거리는 본체와 동일하게 둔다(C-3 확정).</summary>
        public float AttackRange { get; }

        /// <summary>충전량이 남아 있는가. 0이면 소멸 대상이다.</summary>
        public bool IsAlive => Charge > 0f;

        /// <summary>
        /// 한 번 쏜다. 남은 충전량보다 큰 피해는 나가지 않는다 —
        /// 충전량이 곧 피해 총량이므로 마지막 발은 남은 만큼만이다.
        /// </summary>
        public float Fire()
        {
            if (Charge <= 0f) return 0f;

            float dealt = Mathf.Min(DamagePerHit, Charge);
            Charge -= dealt;
            return dealt;
        }
    }
}
