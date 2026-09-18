using UnityEngine;

namespace MBI.Core.Combat
{
    /// <summary>
    /// **한 발을 여러 발로 쪼갠다** (2026-09-18 사용자 확정 — 「탄환 한 발로 세 발이 나가고
    /// 대미지는 1/2」).
    ///
    /// ⚠️⚠️ **왜 들어왔나.** 실측에서 벽이 **발사 간격** 하나로 좁혀졌다 —
    /// 보드를 여덟 배로 키워도 쏜 발이 240 에서 안 움직였고(공급이 아니다),
    /// 탄종을 셋으로 늘려도 **재고가 없어 못 쏜 틱이 0.0%** 였다(재고도 아니다).
    /// 남은 것은 **줄의 발사 주기**였고, 그것을 **소비 한 번에 나가는 발 수**로 연다.
    ///
    /// 📌 **소비는 그대로 한 발이다.** 창고·마운트·물류는 아무것도 안 바뀐다 —
    /// 바뀌는 것은 **그 한 발이 몇 번 꽂히는가**와 **한 번이 얼마나 아픈가** 둘뿐이다.
    ///
    /// 📌 **총량은 1.5 배다**(3 × 1/2). 「세 발」과 「절반」은 각각 사용자 값이고,
    /// 그 곱이 얼마인지는 **따라 나오는 수**다 — 여기서 정하는 것이 아니다.
    ///
    /// ⚠️ 값 둘은 **사용자 확정**이며 TBD 가 아니다. 다만 그 곱이 밸런스에 닿으므로
    /// 실측을 붙여 설계에 올린다.
    /// </summary>
    public static class ShotSplitRule
    {
        /// <summary>사용자 확정 — 소비 한 번에 나가는 발 수.</summary>
        public const int DefaultShotsPerRound = 3;

        /// <summary>사용자 확정 — 한 발의 피해 배수.</summary>
        public const float DefaultDamageFactor = 0.5f;

        /// <summary>
        /// 실제로 쓸 발 수. **1 보다 작으면 1** — 0 발을 쏘면 탄만 사라진다.
        /// </summary>
        public static int ShotsPerRound(int tbd) => tbd > 0 ? tbd : 1;

        /// <summary>
        /// 실제로 쓸 피해 배수. **0 이하면 1** — 「안 정했다」를 「피해 0」으로 읽지 않는다.
        /// </summary>
        public static float DamageFactor(float tbd) => tbd > 0f ? tbd : 1f;

        /// <summary>
        /// 한 발이 내는 **총 피해의 배수**. 화면에도 회신문에도 이 수로 적는다 —
        /// 「세 발」과 「절반」을 따로 적으면 읽는 사람이 곱셈을 해야 한다.
        /// </summary>
        public static float ThroughputRatio(int shotsTbd, float damageTbd)
            => ShotsPerRound(shotsTbd) * DamageFactor(damageTbd);
    }
}
