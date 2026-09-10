using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 조립 화면 위쪽에 전투를 겹쳐 보여 주는 자리
    /// (2026-09-10 사용자 확정 · 플랜 §66-21 d · UI 문서 「연속성」).
    ///
    /// **왜 필요한가.** 설계원칙 연속성은 「조립 진입 시 전투가 멈추지 않는다」인데,
    /// 지금까지 그것을 보여 주는 것은 **HUD 글자뿐**이었다. 카메라가 하나뿐이라
    /// 조립 화면에서는 전투가 **화면 밖**에 있었고, 상단 0~768 은 검은 바탕이었다.
    ///
    /// ⚠️ **시뮬은 안 건드린다.** 전투는 원래도 계속 돌고 있었다 — 바뀌는 것은
    /// **보여 주는가**뿐이다. 카메라를 하나 더 두어 같은 장면의 다른 자리를 비춘다.
    ///
    /// ⚠️ **경고 띠와 안 겹친다.** 띠는 768 에서 시작하고(<see cref="SupplyStopRules"/>)
    /// 이 자리는 768 에서 끝난다 — **맞닿되 겹치지 않는다.** 겹치면 「생산이 멈췄습니다」가
    /// 전투 위에 얹혀 둘 다 안 읽힌다.
    /// </summary>
    public static class CombatInsetView
    {
        /// <summary>화면 세로에서 이 자리가 차지하는 몫. 문서 기준 768 ÷ 2560.</summary>
        public const float HeightShare = 0.3f;

        /// <summary>
        /// 카메라 뷰포트(0~1 · 왼아래 기준). 위쪽 30% 를 가로 전체로 쓴다.
        ///
        /// ⚠️ 유니티 뷰포트는 **아래에서 위로** 잰다 — 위쪽 30% 는 `y = 0.7` 에서 시작한다.
        /// 화면 좌표(위에서 아래로)와 뒤집혀 있어 0.3 을 그대로 쓰면 **아래쪽**이 잡힌다.
        /// </summary>
        public static Rect Viewport => new Rect(0f, 1f - HeightShare, 1f, HeightShare);

        /// <summary>이 자리의 아랫변이 화면 위에서 몇 픽셀인가. 띠가 여기서 시작해야 한다.</summary>
        public static float BottomPixels(float screenHeight) => screenHeight * HeightShare;

        /// <summary>
        /// 경고 띠와 맞닿기만 하는가. **겹치면 둘 다 안 읽힌다.**
        /// </summary>
        public static bool ClearsBand(float screenWidth, float screenHeight)
        {
            Rect band = SupplyStopRules.BandRect(screenWidth, screenHeight);
            return BottomPixels(screenHeight) <= band.y + 0.001f;
        }
    }
}
