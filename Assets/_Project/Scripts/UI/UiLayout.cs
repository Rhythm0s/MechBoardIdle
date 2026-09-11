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
    ///
    /// ⚠️⚠️ **레이어 둘은 서로 다른 화면이다** (2026-09-11 사용자 확정 · 정정).
    ///
    /// | | 레이어 1 — **전투 화면** | 레이어 2 — **조립 화면** |
    /// |---|---|---|
    /// | 꼴 | **절대 좌표**(기준 캔버스 위 자리 하나하나) | **띠 다섯**(세로로 쌓아 2560 을 채운다) |
    /// | 든 것 | 상단 정보줄 · 상태창 · 방치 보상 · 태그 · 합체 · 조립 진입 | 인셋 · 보드 · 부유 띠 · 변수 패널 · 액션바 |
    ///
    /// **조립 진입·합체·태그는 조립 화면 띠에 안 들어간다.** 종전 코드는 이 셋을 레이어 2 의
    /// 띠 안에 앉히려다 「막대 160 이 액션바 128 을 넘는다」는 **없는 충돌**을 만들어 냈다 —
    /// 두 수는 **다른 화면의 수**라 애초에 견줄 것이 아니었다.
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

        // ═════════════════════════ 레이어 1 · 전투 화면 (절대 좌표) ═════════════════════════
        //
        //  UI 아트 요청 문서(20) 4장 · 2026-09-11 사용자 확정.
        //  **자리는 좌표로 주어졌다** — 띠로 쪼개 앉히는 것이 아니다.
        //
        //      y    0 ┌──────────────────────────┐
        //             │ 상단 정보줄        120   │
        //         120 ├──────────────────────────┤
        //         160 │ 상태창 (x40)             │        방치 보상 → 우상단
        //             │                          │
        //        2080 │              ◯ 태그  x1280 (지름 200)
        //        2300 │              ◯ 합체  x1280 (지름 200)
        //        2460 │      ▭ 조립 진입  x720 · 600×160
        //        2560 └──────────────────────────┘
        //
        //  ⚠️ **x·y 는 가운데다.** 막대의 x720 이 화면 한가운데(1440÷2)라는 데서 따라온다 —
        //  왼윗모서리로 읽으면 막대가 오른쪽으로 300 밀린다.
        //
        //  **원형 둘은 세로로 쌓인다**(2080 → 2300 · 사이 220 > 지름 200). 가로로 나란히
        //  두면 x1280 하나로 둘을 못 앉힌다.

        /// <summary>상단 정보줄 높이 (기준 캔버스).</summary>
        public const float InfoBarHeight = 120f;

        /// <summary>상태창 왼윗모서리 (기준 캔버스). ⚠️ **크기는 문서에 없다** — 그리는 쪽이 잰다.</summary>
        public static Vector2 StatusPanelOrigin => new Vector2(40f, 160f);

        /// <summary>태그 원형의 **가운데** (기준 캔버스).</summary>
        public static Vector2 TagButtonCenter => new Vector2(1280f, 2080f);

        /// <summary>합체 원형의 **가운데** (기준 캔버스). 태그 아래 220 — 지름 200 이라 20 이 뜬다.</summary>
        public static Vector2 MergeButtonCenter => new Vector2(1280f, 2300f);

        /// <summary>조립 진입 막대의 **가운데** (기준 캔버스). x720 = 화면 한가운데.</summary>
        public static Vector2 EnterBoardCenter => new Vector2(720f, 2460f);

        // ───────────────────────────── 요소 치수 ─────────────────────────────
        //
        //  **원형·막대는 실물 크기가 정보다** — 손가락이 닿는 크기라
        //  창이 작아지면 같은 비율로 줄어야 한다.

        /// <summary>합체·태그 **원형 버튼** 지름 (기준 캔버스). 링 게이지가 이 테두리를 돈다.</summary>
        public const float RoundButtonDiameter = 200f;

        /// <summary>조립 진입 **막대 버튼** 가로 (기준 캔버스). 항상 노출되는 하나뿐인 화면 전환이다.</summary>
        public const float BarButtonWidth = 600f;

        /// <summary>조립 진입 막대 버튼 세로 (기준 캔버스).</summary>
        public const float BarButtonHeight = 160f;

        /// <summary>액션바 「적용」 버튼 가로 (기준 캔버스).</summary>
        public const float ApplyButtonWidth = 320f;

        /// <summary>
        /// 「적용」 버튼 세로. <see cref="ActionBarHeight"/> 와 **같아야 한다**(시험이 지킨다) —
        /// 둘 다 레이어 2 의 수라 견주는 것이 맞다.
        ///
        /// ⚠️ **문서 안 충돌 하나가 여기 남는다** — UI 아트 5-3 의 액션바 128 과
        /// 6-2 의 버튼 최소 150 이 서로 안 맞는다. **판정 대기**(`260911_V01` 2-4).
        /// </summary>
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

        /// <summary>
        /// 레이어 1 의 **가운데 좌표 + 크기** → 실제 창 사각.
        /// 가로는 기준 캔버스를 창 가운데에 놓고 잰다(<see cref="Px(Rect,float,float)"/> 와 같다).
        /// </summary>
        public static Rect Centered(Vector2 designCenter, float designW, float designH,
            float screenWidth, float screenHeight) =>
            Px(new Rect(designCenter.x - designW * 0.5f, designCenter.y - designH * 0.5f,
                designW, designH), screenWidth, screenHeight);

        /// <summary>
        /// 조립 진입 막대 — **레이어 1** 의 (720, 2460) · 600×160. 전투 화면에서만 그린다.
        ///
        /// ⚠️ **액션바와 무관하다**(2026-09-11 정정). 종전에는 이것을 레이어 2 의 액션바 128 안에
        /// 앉히려 해서 「160 이 128 을 위아래 16씩 넘친다」는 **없는 충돌**이 나왔다.
        /// </summary>
        public static Rect EnterBoardRect(float screenWidth, float screenHeight) =>
            Centered(EnterBoardCenter, BarButtonWidth, BarButtonHeight, screenWidth, screenHeight);

        /// <summary>
        /// 조립 화면의 「전투로」 — ⚠️ **가정이다.** 문서가 정한 액션바 내용물은 「적용 320×128」
        /// 하나이고, 돌아가는 버튼의 자리는 적혀 있지 않다.
        ///
        /// **액션바 왼쪽**에 두었다 — 적용(오른쪽)과 안 겹치고, 높이를 띠에 맞춘다.
        /// 값이 서면 이 메서드 하나만 바뀐다.
        /// </summary>
        public static Rect ExitBoardRect(float screenWidth, float screenHeight)
        {
            Rect bar = BandRect(Band.ActionBar, screenWidth, screenHeight);
            float s = Scale(screenHeight);
            float margin = 24f * s;
            return new Rect(bar.x + margin, bar.y, BarButtonWidth * s * 0.5f, bar.height);
        }

        /// <summary>액션바 「적용」 — 오른쪽 끝. 높이가 띠와 같아 띠를 꽉 채운다.</summary>
        public static Rect ApplyRect(float screenWidth, float screenHeight)
        {
            Rect bar = BandRect(Band.ActionBar, screenWidth, screenHeight);
            float s = Scale(screenHeight);
            float margin = 24f * s, w = ApplyButtonWidth * s;
            return new Rect(screenWidth - margin - w, bar.y, w, ApplyButtonHeight * s);
        }

        /// <summary>
        /// 합체·태그 원형 둘 — **레이어 1** 의 x1280 에 **세로로 쌓인다**(태그 2080 · 합체 2300).
        /// 전투 화면에서만 그려진다.
        ///
        /// ⚠️ **가로로 나란히가 아니다**(2026-09-11 정정). 좌표가 둘 다 x1280 이라
        /// 가로로 두면 한 자리에 둘을 앉히게 된다.
        /// <paramref name="index"/> 0 = 태그(위) · 1 = 합체(아래).
        /// </summary>
        public static Rect RoundButtonRect(int index, float screenWidth, float screenHeight) =>
            Centered(index == 0 ? TagButtonCenter : MergeButtonCenter,
                RoundButtonDiameter, RoundButtonDiameter, screenWidth, screenHeight);

        /// <summary>
        /// 부유 띠의 **미니맵(왼쪽)·모드 버튼(오른쪽)** 자리 (2026-09-11 사용자 확정).
        /// <paramref name="right"/> 가 참이면 모드 버튼이다.
        ///
        /// ⚠️ **레이어 2 다** — 전투 화면의 원형 둘과 자리를 다투지 않는다(다른 화면이다).
        /// </summary>
        public static Rect FloatBandSlot(bool right, float screenWidth, float screenHeight)
        {
            Rect band = BandRect(Band.FloatBand, screenWidth, screenHeight);
            float s = Scale(screenHeight);
            float d = Mathf.Min(RoundButtonDiameter * s, band.height - 24f * s);
            float margin = 24f * s;
            float x = right ? screenWidth - margin - d : margin;
            return new Rect(x, band.y + (band.height - d) * 0.5f, d, d);
        }

        /// <summary>
        /// 노드 팔레트 자리 — ⚠️ **가정이다.** UI 문서에 팔레트 자리 절이 없다(미결 표 1번).
        ///
        /// 부유 띠의 **미니맵(왼쪽)과 모드 버튼(오른쪽) 사이**에 두었다. 근거는 둘이다 —
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
            float gap = 16f * s;
            float left = FloatBandSlot(false, screenWidth, screenHeight).xMax + gap;
            float right = FloatBandSlot(true, screenWidth, screenHeight).x - gap;
            return new Rect(left, band.y, Mathf.Max(0f, right - left), band.height);
        }
    }
}
