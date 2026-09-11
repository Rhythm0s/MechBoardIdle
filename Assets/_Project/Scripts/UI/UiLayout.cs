using UnityEngine;
using MBI.Core;

namespace MBI.UI
{
    /// <summary>
    /// 화면 배치의 **치수 한 곳** (2026-09-11 신설 · 플랜 §68-4 (A) · UI 문서 9-4 · UI 아트 문서 4장).
    ///
    /// **왜 신설하는가.** 지금까지 화면 요소는 저마다 **날 픽셀**을 들고 있었다 —
    /// 팔레트는 `x = Screen.width - 142`, 조립 진입은 `220×46`, 전투 HUD는 `(12,10,560,280)`.
    /// 창 크기가 달라지면 겹치고, 문서가 값을 고쳐도 **어디를 고쳐야 하는지가 안 보인다.**
    /// 09-02 이후 「겹침」으로 고친 자리가 여섯인데 전부 같은 뿌리다.
    ///
    /// 여기 담는 것은 **문서에 있는 값뿐**이다. 없는 것은 <see cref="PaletteRect"/> 하나이며
    /// **가정**으로 표기한다 — 설계가 역기입한다(`260911_W01` 3장 팔레트 자리).
    ///
    /// ⚠️ **세로로 환산한다.** 기준 캔버스는 1440×2560(비 0.5625)인데 실제 창은 비가 다르다.
    /// 가로로 맞추면 세로 띠 다섯이 화면 밖으로 밀리므로 **세로를 맞추고 가로는 창 전체**를 쓴다 —
    /// <see cref="SupplyStopRules.BandRect"/> 가 이미 쓰는 환산이며 여기가 그 출처가 된다.
    /// </summary>
    public static class UiLayout
    {
        // ────────────────────────────── 기준 캔버스 ──────────────────────────────

        /// <summary>기준 캔버스 가로 (UI 문서 9-4).</summary>
        public const float DesignWidth = 1440f;

        /// <summary>기준 캔버스 세로. <see cref="SupplyStopRules.DesignScreenHeight"/> 와 같은 값이다.</summary>
        public const float DesignHeight = 2560f;

        /// <summary>기준 캔버스 → 실제 창 배율. **세로로 맞춘다.**</summary>
        public static float Scale(float screenHeight) => screenHeight / DesignHeight;

        /// <summary>지금 창의 배율.</summary>
        public static float CurrentScale => Scale(Screen.height);

        /// <summary>기준 캔버스 길이 하나를 지금 창 픽셀로.</summary>
        public static float Px(float design) => design * CurrentScale;

        // ───────────────────────────── 레이어 2 · 띠 다섯 ─────────────────────────────
        //
        //  UI 문서 9-4 「화면 배치 (레이어 2, 1440 × 2560 기준)」
        //
        //      y      0 ┌──────────────────────────┐
        //               │  전투             768    │  ← CombatInsetView 뷰포트 상단 30%
        //           768 ├──────────────────────────┤  ← 경고 띠 96 이 여기서 시작(12-2 · 겹쳐 뜬다)
        //               │  보드            1352    │
        //          2120 ├──────────────────────────┤
        //               │  부유 띠(미니맵·모드) 200│
        //          2320 ├──────────────────────────┤
        //               │  변수 패널        112    │
        //          2432 ├──────────────────────────┤
        //               │  액션바           128    │  ← 적용 320×128 이 여기 앉는다
        //          2560 └──────────────────────────┘
        //
        //  ⚠️ **다섯을 더하면 정확히 2560 이다.** 시험이 그것을 지킨다 —
        //  하나를 고치면서 다른 하나를 안 고치면 틈이나 겹침이 생긴다.

        /// <summary>전투 자리 높이 (기준 캔버스). <see cref="CombatInsetView.HeightShare"/> 의 분자다.</summary>
        public const float CombatHeight = 768f;

        /// <summary>보드 자리 높이 (기준 캔버스).</summary>
        public const float BoardHeight = 1352f;

        /// <summary>부유 띠 높이 — 미니맵·모드 버튼이 **보드 위에 떠 있지 않고** 여기 앉는다.</summary>
        public const float FloatBandHeight = 200f;

        /// <summary>변수 패널 높이 (기준 캔버스). **하단이다** — 우상단이 아니다.</summary>
        public const float VariablePanelHeight = 112f;

        /// <summary>액션바 높이 (기준 캔버스).</summary>
        public const float ActionBarHeight = 128f;

        /// <summary>띠 다섯. 위에서 아래 차례다.</summary>
        public enum Band { Combat, Board, FloatBand, VariablePanel, ActionBar }

        /// <summary>그 띠의 윗변 (기준 캔버스).</summary>
        public static float DesignTop(Band band)
        {
            switch (band)
            {
                case Band.Combat: return 0f;
                case Band.Board: return CombatHeight;
                case Band.FloatBand: return CombatHeight + BoardHeight;
                case Band.VariablePanel: return CombatHeight + BoardHeight + FloatBandHeight;
                default: return CombatHeight + BoardHeight + FloatBandHeight + VariablePanelHeight;
            }
        }

        /// <summary>그 띠의 높이 (기준 캔버스).</summary>
        public static float DesignHeightOf(Band band)
        {
            switch (band)
            {
                case Band.Combat: return CombatHeight;
                case Band.Board: return BoardHeight;
                case Band.FloatBand: return FloatBandHeight;
                case Band.VariablePanel: return VariablePanelHeight;
                default: return ActionBarHeight;
            }
        }

        /// <summary>
        /// 그 띠가 실제 창에서 앉을 자리. **가로는 창 전체**다 — 기준 캔버스보다 넓은 창에서
        /// 좌우에 빈 띠를 남기지 않는다(<see cref="SupplyStopRules.BandRect"/> 와 같은 처리).
        /// </summary>
        public static Rect BandRect(Band band, float screenWidth, float screenHeight)
        {
            float s = Scale(screenHeight);
            return new Rect(0f, DesignTop(band) * s, screenWidth, DesignHeightOf(band) * s);
        }

        /// <summary>지금 창에서의 띠 자리.</summary>
        public static Rect BandRect(Band band) => BandRect(band, Screen.width, Screen.height);

        // ───────────────────────────── 레이어 1 · 요소 치수 ─────────────────────────────
        //
        //  UI 아트 요청 문서(20) 4장. **원형·막대는 실물 크기가 정보다** —
        //  손가락이 닿는 크기라 창이 작아지면 같은 비율로 줄어야 한다.

        /// <summary>합체·태그 **원형 버튼** 지름 (기준 캔버스). 링 게이지가 이 테두리를 돈다.</summary>
        public const float RoundButtonDiameter = 200f;

        /// <summary>조립 진입 **막대 버튼** 가로 (기준 캔버스). 항상 노출되는 하나뿐인 화면 전환이다.</summary>
        public const float BarButtonWidth = 600f;

        /// <summary>조립 진입 막대 버튼 세로 (기준 캔버스).</summary>
        public const float BarButtonHeight = 160f;

        /// <summary>액션바 「적용」 버튼 가로 (기준 캔버스).</summary>
        public const float ApplyButtonWidth = 320f;

        /// <summary>「적용」 버튼 세로. <see cref="ActionBarHeight"/> 와 **같아야 한다**(시험이 지킨다).</summary>
        public const float ApplyButtonHeight = 128f;

        /// <summary>
        /// 버튼의 **최소 변** (기준 캔버스 · UI 문서 「1440 기준 150px」).
        ///
        /// 손가락 하나가 닿는 최소다. 지금 코드의 버튼은 `130×36` **날 픽셀**이라 이 값을 한참
        /// 밑돈다 — 리허설 1차의 「눌러도 안 먹었다」가 나온 자리 중 하나다.
        /// </summary>
        public const float MinButton = 150f;

        /// <summary>이 변이 최소를 지키는가 (기준 캔버스 단위). 시험·진단용.</summary>
        public static bool MeetsMinButton(float designSide) => designSide >= MinButton - 0.001f;

        /// <summary>
        /// 기준 캔버스 사각 → 실제 창 사각. **가로는 캔버스를 창 가운데에 놓고** 잰다 —
        /// 띠와 달리 요소는 늘리면 안 되고 기준 비율을 지켜야 한다.
        /// </summary>
        public static Rect Px(Rect design, float screenWidth, float screenHeight)
        {
            float s = Scale(screenHeight);
            float xOff = (screenWidth - DesignWidth * s) * 0.5f;
            return new Rect(xOff + design.x * s, design.y * s, design.width * s, design.height * s);
        }

        /// <summary>지금 창 기준.</summary>
        public static Rect Px(Rect design) => Px(design, Screen.width, Screen.height);

        // ───────────────────────────── 자리 ─────────────────────────────

        /// <summary>조립 진입 막대 — 액션바 가운데. 「항상 노출」이라 자리가 고정이다.</summary>
        public static Rect EnterBoardRect(float screenWidth, float screenHeight)
        {
            Rect bar = BandRect(Band.ActionBar, screenWidth, screenHeight);
            float s = Scale(screenHeight);
            float w = BarButtonWidth * s, h = BarButtonHeight * s;
            return new Rect((screenWidth - w) * 0.5f, bar.y + (bar.height - h) * 0.5f, w, h);
        }

        /// <summary>
        /// 합체·태그 원형 둘 — 부유 띠 **오른쪽**에 나란히. 전투 화면에서만 그려진다.
        /// <paramref name="index"/> 0 = 태그 · 1 = 합체(오른쪽 끝).
        /// </summary>
        public static Rect RoundButtonRect(int index, float screenWidth, float screenHeight)
        {
            Rect band = BandRect(Band.FloatBand, screenWidth, screenHeight);
            float s = Scale(screenHeight);
            float d = RoundButtonDiameter * s, gap = 24f * s, margin = 24f * s;
            float right = screenWidth - margin - d;
            return new Rect(right - (1 - index) * (d + gap),
                band.y + (band.height - d) * 0.5f, d, d);
        }

        /// <summary>
        /// 노드 팔레트 자리 — ⚠️ **가정이다.** UI 문서에 팔레트 자리 절이 없다(미결 표 1번).
        ///
        /// **부유 띠 왼쪽**에 가로로 두었다. 근거는 둘이다 —
        /// ① 종전 자리(보드 위 오른쪽)는 `UiPlate` 와 튜토리얼 DIM 이 겹치는 **유일한** 자리였고
        ///    보드 밖으로 내보내면 그 겹침이 **0** 이 된다(플랜 §69 2번).
        /// ② 부유 띠는 미니맵·모드와 같은 「보드를 조작하는 것」 층이다.
        ///
        /// 값이 서면 **이 메서드 하나만** 바뀐다.
        /// </summary>
        public static Rect PaletteRect(float screenWidth, float screenHeight)
        {
            Rect band = BandRect(Band.FloatBand, screenWidth, screenHeight);
            float s = Scale(screenHeight);
            float margin = 24f * s;
            // 원형 둘이 오른쪽을 쓰므로 그 왼쪽까지만 차지한다.
            float right = RoundButtonRect(0, screenWidth, screenHeight).x - margin;
            return new Rect(margin, band.y, Mathf.Max(0f, right - margin), band.height);
        }
    }
}
