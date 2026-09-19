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
        /// **인셋이 쓸 `orthographicSize`** (2026-09-19 사용자 확정 · 리허설 1 ①).
        ///
        /// **왜 필요한가 — 실측.** 종전에는 주 카메라의 `orthographicSize` 를 **그대로**
        /// 썼다. 그런데 인셋은 **가로는 화면 전체, 세로는 30%** 인 뷰포트라
        /// 가로세로비가 주 카메라의 <c>1 ÷ 0.3 = 3.33 배</c>다. 값으로 재면 —
        ///
        /// · 주 카메라 (ortho 8 · 기준 캔버스 1440×2560) → 가로 반폭 **4.5 칸**
        /// · 인셋 (같은 ortho 8) → 가로 반폭 **15 칸** (전폭 30 칸)
        /// · 스폰 띠는 로봇 기준 **8~14 칸**(`CombatTuning.spawnRingMin/MaxTbd`)
        ///
        /// 즉 인셋은 **띠를 통째로 품고 있었다** — 조립 화면 위쪽에 아직 걸어오지도 않은
        /// 적이 죽 늘어서 보였다(사용자 리허설 1 ①).
        ///
        /// **규칙 — 인셋의 가로 반폭을 주 카메라와 같게 한다.**
        /// <code>
        ///   인셋비 = 주비 ÷ HeightShare
        ///   인셋ortho × 인셋비 = 주ortho × 주비
        ///   ⇒ 인셋ortho = 주ortho × HeightShare
        /// </code>
        /// 📌 **가로세로비가 식에서 지워진다** — 창 크기를 재지 않아도 되고, 어떤 화면에서도
        /// 가로로 보이는 폭이 두 화면에서 같다. **지어낸 수가 하나도 없다**(있는 것은
        /// <see cref="HeightShare"/> 하나뿐이다).
        ///
        /// ⚠️ **세로는 좁아진다** — ortho 8 → 2.4 이므로 세로 반폭도 2.4 다. 그것이
        /// 「줌인」의 내용이다. 모서리까지가 <c>√(4.5² + 2.4²) ≈ 5.1</c> 칸이라
        /// **띠 최솟값 8 보다 안쪽**이다 — 갓 스폰한 적은 안 보이고 **걸어 들어오면서**
        /// 보이기 시작한다(사용자가 정한 「스폰 띠 밖 몬스터가 안 보이게」).
        ///
        /// ⚠️ 이 값은 **판정에 안 쓴다** — 광역·태그 스킬이 보는 사각은 주 카메라가 낸다
        /// (`StageRunner.PushVisibleBounds`). 인셋은 **보여 주기만** 한다.
        /// </summary>
        public static float InsetOrthoSize(float mainOrthoSize) => mainOrthoSize * HeightShare;

        /// <summary>
        /// 바닥 그림이 덮어야 하는 가로 반폭.
        ///
        /// 🗑️ **넓히던 것 폐기**(2026-09-19). 종전 식은 <c>max(주비, 주비 ÷ 0.3)</c> 이라
        /// **주 카메라의 3.33 배**를 깔았다. 그 까닭은 인셋이 주 카메라보다 **넓게 보던**
        /// 시절의 것인데, <see cref="InsetOrthoSize"/> 가 인셋의 가로 반폭을 주 카메라와
        /// **같게** 만들었으므로 넓힐 근거가 사라졌다.
        ///
        /// 📌 **이 함수는 남긴다** — 부르는 자리가 둘이고(러너), 여기 한 곳이 「얼마나
        /// 까는가」를 쥐고 있어야 다음에 또 갈릴 때 **한 군데만 고친다**(지침 §7).
        ///
        /// ⚠️ **종전 실측은 여전히 사실이다** — 2026-09-10 에 좁게 깔았더니 조립 화면
        /// 상단 좌우에 검은 여백이 남았다. 그때는 인셋이 넓었기 때문이고, 지금은
        /// 넓지 않다. **인셋 배율을 다시 넓히면 이 함수도 같이 되돌려야 한다.**
        /// </summary>
        public static float BackgroundHalfWidth(float halfHeight, float mainAspect) =>
            halfHeight * mainAspect;

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
