using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 드론 사출대(로봇 B). 밸런스 문서 10-3 · 전투 시스템 문서.
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
        private float _pending; // 만들어졌지만 아직 못 나간 드론(소수 누적 포함)

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

        /// <summary>대기 중(만들어졌으나 미출격) 드론 수. 소수는 다음 틱으로 이월된다.</summary>
        public float Pending => _pending;

        /// <summary>슬롯 상한에 걸린 초당 방출 능력(기/초).</summary>
        public float SlotThroughput => Slots * ReleaseRatePerSlot;

        /// <summary>실효 방출량(기/초) = min(유입, 슬롯 × 방출률). 낮은 쪽이 병목이다.</summary>
        public float EffectiveRelease(float inflowPerSec) =>
            Mathf.Min(Mathf.Max(0f, inflowPerSec), SlotThroughput);

        /// <summary>유입이 병목인가(슬롯이 놀고 있는가). 진단 표시용.</summary>
        public bool InflowLimited(float inflowPerSec) => inflowPerSec < SlotThroughput;

        /// <summary>생산 유입을 대기열에 넣는다. 유입 = 생산이므로 보드 산출이 그대로 온다.</summary>
        public void Produce(float dt, float inflowPerSec)
        {
            if (dt <= 0f || inflowPerSec <= 0f) return;
            _pending += inflowPerSec * dt;
        }

        /// <summary>
        /// 이번 틱 출격 수. 슬롯 여유와 대기열 중 **적은 쪽**만 나간다.
        /// 방출률은 시간당 상한이므로 dt를 곱해 이번 틱 몫을 낸다.
        /// </summary>
        public int Launch(float dt)
        {
            if (dt <= 0f || Slots <= 0) return 0;

            int freeSlots = Slots - Active;
            if (freeSlots <= 0) return 0;

            float allowedByRate = SlotThroughput * dt;
            int count = Mathf.FloorToInt(Mathf.Min(_pending, Mathf.Min(freeSlots, allowedByRate)));
            if (count <= 0) return 0;

            _pending -= count;
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
            _pending = 0f;
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
