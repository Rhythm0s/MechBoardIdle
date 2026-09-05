using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// **마운트에 실제로 도착한 것**을 전투력 비율(초당)로 환산한다
    /// (2026-09-05 신설 · `260904_W04` 2-1 4번 「actual = 도착량」).
    ///
    /// 왜 필요한가 — 종전 `actual`은 <b>계산된 값</b>이었다.
    /// 「군수 노드 수 × 라인 스펙 × 전력배율 × 발열배율 × 벨트배율」이라,
    /// 벨트를 아무리 이상하게 깔아도 노드 수만 같으면 같은 수가 나왔다. 라인이 막혀 물건이
    /// 쌓이든, 분류기가 갈래를 잘못 나눠 한쪽이 굶든, 출력은 태연했다. 그래서
    /// 「물류 라인을 최적화하는 행위가 재미있는가」에서 **최적화의 결과가 숫자에 안 보였다.**
    ///
    /// 이제 세는 것은 <b>마운트 고정 포트를 통과한 개수</b>다. 라인이 실제로 나른 것만
    /// 세어지므로, 벨트를 잘 깔면 오르고 못 깔면 내린다.
    ///
    /// **왜 개수가 아니라 비율인가.** 도착은 이산 사건이라 한 프레임에 0개였다가 3개였다가
    /// 한다. 그대로 쓰면 출력이 초당 수십 번 튄다. 여기서는 구간마다 「몇 초 동안 얼마가
    /// 왔는가」로 모아 비율을 내고, 그 비율을 60초 롤링이 다시 평탄화한다.
    ///
    /// **값을 만들지 않는다.** 발당피해는 무기 스펙에서 그대로 읽고, 이 파일에는 밸런스
    /// 수치가 하나도 없다. 여기서 나오는 것은 관측치다.
    ///
    /// 순수 클래스 — <see cref="UnityEngine"/> 타입을 상태로 갖지 않아 EditMode에서 돈다.
    /// </summary>
    public sealed class MountDelivery
    {
        // 아직 비율로 안 바꾼 몫. 샘플 구간이 끝날 때까지 모은다.
        private float _pendingPower;
        private float _pendingSeconds;

        /// <summary>마지막으로 낸 비율(전투력/초). 아직 한 구간도 안 끝났으면 0.</summary>
        public float Rate { get; private set; }

        /// <summary>모으는 중인 구간의 길이(초). 진단용.</summary>
        public float PendingSeconds => _pendingSeconds;

        /// <summary>이번 세션에 마운트로 간 전투력 총합. 진단용 — 비율 계산에는 안 쓴다.</summary>
        public float TotalPower { get; private set; }

        /// <summary>
        /// 한 틱 관측한다. <paramref name="arrivals"/>는 이 틱에 마운트에 닿은 것들이고,
        /// <paramref name="damageOf"/>는 탄종별 발당피해를 돌려주는 함수다.
        ///
        /// ⚠️ **탄약이 아닌 도착은 안 센다.** 마운트에 드론이나 부품이 닿아도 그것은
        /// 전투력이 아니다 — 세면 부품 하나가 탄 하나로 둔갑한다.
        /// </summary>
        public void Observe(IReadOnlyList<MountArrival> arrivals, System.Func<AmmoKind, float> damageOf,
            float dt)
        {
            if (dt > 0f) _pendingSeconds += dt;
            if (arrivals == null || damageOf == null) return;

            for (int i = 0; i < arrivals.Count; i++)
            {
                if (!MountItemMap.TryAmmoKindOf(arrivals[i].kind, out AmmoKind kind)) continue;

                float damage = damageOf(kind);
                if (damage <= 0f) continue;

                _pendingPower += damage;
                TotalPower += damage;
            }
        }

        /// <summary>
        /// 모아 둔 구간을 비율로 바꾼다. 구간이 <paramref name="minSeconds"/>보다 짧으면
        /// 아직 안 바꾸고 <c>false</c>를 돌려준다 — 한 프레임(수 ms)으로 나누면 한 개만 닿아도
        /// 비율이 수백으로 튄다.
        /// </summary>
        public bool TryDrain(float minSeconds, out float rate)
        {
            rate = Rate;
            if (_pendingSeconds < minSeconds || _pendingSeconds <= 0f) return false;

            rate = _pendingPower / _pendingSeconds;
            Rate = rate;

            _pendingPower = 0f;
            _pendingSeconds = 0f;
            return true;
        }

        /// <summary>보드가 바뀌거나 코어가 없어졌을 때. 모으던 구간을 버린다.</summary>
        public void Reset()
        {
            _pendingPower = 0f;
            _pendingSeconds = 0f;
            Rate = 0f;
            TotalPower = 0f;
        }
    }
}
