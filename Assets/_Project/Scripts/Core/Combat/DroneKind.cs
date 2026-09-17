using UnityEngine;

namespace MBI.Core.Combat
{
    /// <summary>
    /// 드론 두 종 (2026-09-16 신설 · 사용자 확정 · 플랜 §74-21 · §74-3 #30).
    ///
    /// ⚠️⚠️ **여태 코드에 종 구분이 없었다.** 조합표에는 `StackDrone`·`AoeDrone` 둘이
    /// 있었고 아트도 둘 다 있었는데(`drone_n` · `drone_w`), 전투는 **한 종만** 냈다.
    /// 「드론 믹스로 조립」이라는 역할 설명이 화면에서 성립한 적이 없다.
    /// </summary>
    public enum DroneKind
    {
        /// <summary>누적형 — 한 적에게 **날아가 붙어** 충전량을 다 쓸 때까지 때린다.</summary>
        Stack = 0,

        /// <summary>광역형 — 플레이어 **주변을 돌며** 사거리 안 적 전부를 친다.</summary>
        Aoe = 1,
    }

    /// <summary>
    /// **다음 한 기를 어느 종으로 낼 것인가** (2026-09-16 · 사용자 확정).
    ///
    /// 📌 **종을 가르는 것은 보드다** — 복합 가공가 누적형 조합표를 돌면 누적형이,
    /// 광역형을 돌면 광역형이 난다. 그 비율이 <paramref name="aoeShare"/> 다.
    ///
    /// ⚠️⚠️ **난수를 안 쓴다.** 시뮬은 결정론이라(같은 입력 = 같은 판) 주사위를 굴리면
    /// 하네스가 재는 값이 매번 달라진다. 대신 **빚을 쌓는다** — 나갈 때마다 몫만큼
    /// 더해 두고 1 을 넘으면 그 종으로 한 기 내보낸다. 장기 비율은 몫과 같고
    /// 짧은 구간에서도 두 종이 **번갈아** 난다.
    ///
    /// ⚠️ 몫이 0 이면 전부 누적형, 1 이면 전부 광역형이다 — **가장자리를 안 지어낸다.**
    /// </summary>
    public sealed class DroneKindPicker
    {
        private float _owedAoe;

        /// <summary>쌓인 빚(시험·진단용).</summary>
        public float OwedAoe => _owedAoe;

        /// <summary>다음 한 기의 종. 부를 때마다 빚이 갱신된다.</summary>
        public DroneKind Next(float aoeShare)
        {
            float share = Mathf.Clamp01(aoeShare);
            _owedAoe += share;

            if (_owedAoe >= 1f - 1e-4f)
            {
                _owedAoe -= 1f;
                return DroneKind.Aoe;
            }
            return DroneKind.Stack;
        }

        /// <summary>전투가 새로 서면 빚도 비운다 — 앞 판의 나머지가 넘어오면 안 된다.</summary>
        public void Reset() => _owedAoe = 0f;
    }
}
