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
        //               │  보드            1330    │
        //          2098 ├──────────────────────────┤
        //               │  부유 띠           312   │  ← 탭 96 + 노드 버튼 216 (09-15 개편)
        //               │   (변수 패널 112 을 흡수)│
        //          2410 ├──────────────────────────┤
        //               │  액션바           150    │  ← 적용 320×150 이 여기 앉는다
        //          2560 └──────────────────────────┘
        //
        //  ⚠️ **다섯을 더하면 정확히 2560 이다.** 시험이 그것을 지킨다 —
        //  하나를 고치면서 다른 하나를 안 고치면 틈이나 겹침이 생긴다.

        /// <summary>전투 자리 높이 (기준 캔버스). <see cref="CombatInsetView.HeightShare"/> 의 분자다.</summary>
        public const float CombatHeight = 768f;

        /// <summary>
        /// 보드 자리 높이 (기준 캔버스). **1352 → 1330**(2026-09-11 설계 확정 (가)).
        ///
        /// 액션바가 128 에서 150 으로 커지면서 **22 를 보드에서 덜어 냈다** —
        /// 다섯을 더해 2560 이 되어야 하므로 어딘가는 줄어야 하고, **보드가 가장 넓어
        /// 줄어도 덜 아프다.** 구 1352 는 폐기 표기.
        /// </summary>
        public const float BoardHeight = 1330f;

        /// <summary>
        /// 부유 띠 높이 — 카테고리 탭 줄 + 노드 버튼 줄이 여기 앉는다.
        ///
        /// ⚠️ **200 → 312**(2026-09-15 · 하단 개편 ⑥ · 육안 「버튼이 너무 작다」).
        ///
        /// **값을 지어내지 않았다** — 같은 날 걷은 **변수 패널의 112 를 그대로 받았다**
        /// (개편 ①). 띠 다섯의 합은 2560 그대로다:
        /// 전투 768 + 보드 1330 + 부유 **312** + 변수 **0** + 액션바 150 = 2560.
        ///
        /// ⚠️ **왜 키워야 했나.** 200 안에 두 줄을 넣으면 **둘 다 최소 150 을 밑돈다**
        /// (UI 6-2). 312 면 탭 96 + 버튼 216 이라 **버튼 줄이 규격을 넘는다.**
        /// </summary>
        public const float FloatBandHeight = 312f;

        /// <summary>
        /// ⚠️ **폐기 — 변수 패널을 걷었다**(2026-09-15 사용자 확정 · 하단 개편 ①).
        ///
        /// 구 112. 그 높이는 **부유 띠가 받았다**(200 → 312). 상수를 지우지 않고 0 으로
        /// 두는 이유는 <see cref="DesignTop"/> 의 셈이 이 이름을 쓰기 때문이다 —
        /// 되살리려면 이 값만 112 로 되돌리고 부유 띠를 200 으로 내리면 된다.
        /// </summary>
        public const float VariablePanelHeight = 0f;

        /// <summary>
        /// 카테고리 탭 줄 높이 (기준 캔버스). ⚠️ **가정**(설계 역기입) — 문서에 탭 줄 절이 없다.
        ///
        /// ⚠️ **버튼 최소 150 을 밑돈다.** 탭은 **글자만** 있어 낮아도 눌리는 자리가 보이고,
        /// 남는 높이는 **자주 누르는 쪽**(노드 버튼)에 준다 — 그쪽이 216 으로 규격을 넘는다.
        /// 96 을 고르면 두 줄 합이 312 에 정확히 맞는다.
        /// </summary>
        public const float CategoryTabHeight = 96f;

        /// <summary>
        /// 액션바 높이 (기준 캔버스). **128 → 150**(2026-09-11 설계 확정 (가)).
        ///
        /// ✅ **문서 안 어긋남이 닫혔다** — 구 128 은 버튼 최소 150 을 밑돌아
        /// UI 아트 5-3 과 6-2 가 서로 안 맞았다(`260911_V01` 2-4 판정 요청).
        /// **띠를 버튼 최소에 맞추는 쪽**으로 답이 왔다. 구 128 은 폐기 표기.
        /// </summary>
        public const float ActionBarHeight = 150f;

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

        /// <summary>
        /// 태그 원형의 **가운데** (기준 캔버스).
        ///
        /// 🗑️ **구 y2080 폐기**(2026-09-18 사용자 확정 · 시안 3). 오른쪽 기둥에
        /// **자동 토글과 마일스톤 카드**가 들어오면서 넷이 세로로 서야 했다 —
        /// 위에서부터 **태그 → 자동 → 합체 → 마일스톤 → 조립 막대**(설계가 준 차례 그대로).
        /// 종전 2080/2300 사이는 **20** 뿐이라 토글이 들어갈 자리가 없었다.
        ///
        /// ⚠️ **새 y 는 가정이다**(설계 사후 역기입 자리). 정한 법은 하나다 —
        /// 아래에서부터 쌓아 올리며 **아무것도 안 겹치게** 한다(조립 막대 윗변 2380 이 바닥).
        /// </summary>
        public static Vector2 TagButtonCenter => new Vector2(1280f, 1680f);

        /// <summary>
        /// 합체 원형의 **가운데** (기준 캔버스). 🗑️ 구 y2300 폐기 — 위 주석과 같은 이유다.
        /// 태그(1680) → 토글(1800~1880) → 합체(2000) 차례다.
        /// </summary>
        public static Vector2 MergeButtonCenter => new Vector2(1280f, 2000f);

        /// <summary>
        /// **자동 교대 토글** — 태그 원형 **바로 아래**(2026-09-18 사용자 확정 · 시안 3).
        ///
        /// 🗑️ **구 자리(태그 원형 위) 폐기** — 설계가 준 차례는 「태그 원형 → 바로 아래 자동」이다.
        /// ⚠️ 크기는 가정이다. 폭은 원형 지름보다 조금 넓게, 높이는 눌리는 최소의 절반을 넘긴다.
        /// </summary>
        public static Rect TagAutoToggleRect(float screenWidth, float screenHeight)
        {
            // ⚠️ **태그 원형에 바짝 붙인다**(2026-09-19 사용자 확정 ③ — 「태그 버튼 하단에 더 붙여서」).
            //
            // 🗑️ 구 y1840(태그 아랫변에서 60) 폐기. 새 자리는 **아랫변에서 잰다** —
            // 가운데를 못 박아 두면 원형 지름이 바뀔 때 간격이 딸려 오지 않는다.
            //
            // 📌 폭도 **원형 지름을 그대로 쓴다**(300 → 200). 글자가 「Auto」 넉 자로 줄어
            // 300 짜리 판에는 한가운데 낱말 하나만 떠 있게 되고, 그러면 **원형과 한 덩이로
            // 안 읽힌다.** 새 수를 들이지 않고 이미 있는 <see cref="RoundButtonDiameter"/> 를 쓴다.
            // ⚠️ 간격 8 은 가정이다(설계 역기입 자리).
            const float h = 80f, gap = 8f;
            float top = TagButtonCenter.y + RoundButtonDiameter * 0.5f + gap;
            return Centered(new Vector2(TagButtonCenter.x, top + h * 0.5f),
                            RoundButtonDiameter, h, screenWidth, screenHeight);
        }

        /// <summary>조립 진입 막대의 **가운데** (기준 캔버스). x720 = 화면 한가운데.</summary>
        public static Vector2 EnterBoardCenter => new Vector2(720f, 2460f);

        // ── 시안 3 — 칩 줄 · 배지 · 마일스톤 · 진단 (2026-09-18 사용자 확정 · 플랜 §85-8) ──
        //
        //  ⚠️⚠️ **여기 수는 전부 가정이다**(설계 사후 역기입 자리). 확정된 것은 **무엇이
        //  어디에 오는가**이고(좌: 썸네일·닉네임·전투력 / 우: 골드·소리·설정 …),
        //  **몇 픽셀인가**는 문서에 없다. 그래서 이미 있는 수에서 끈다 —
        //  칩 줄 높이는 상단 정보줄 120 을 그대로 쓰고, 누를 것은 전부 최소 150 을 넘긴다.
        //
        //  📌 **헤더는 없다**(11f2ef9 「헤더 + 아래 블록」 폐기 · 2026-09-18 사용자 확정).
        //  판 정보는 칩 줄 아래 **배지 + 한 줄**로 내려가고, 로봇 상태는 **글자가 아니라
        //  몸에 붙은 막대**가 말한다. 좌상단 글자 블록은 통째로 사라진다.

        /// <summary>상단 칩 줄 — 높이는 상단 정보줄과 같다(기준 캔버스 · ⚠️ 가정).</summary>
        public const float ChipBarHeight = InfoBarHeight;

        /// <summary>칩 줄 좌우 여백 (기준 캔버스 · ⚠️ 가정 · 상태창 x40 과 같은 값).</summary>
        public const float ChipBarPad = 40f;

        /// <summary>상단 칩 줄 전체.</summary>
        public static Rect ChipBarRect(float screenWidth, float screenHeight)
        {
            float sc = Scale(screenHeight);
            return new Rect(ChipBarPad * sc, 8f * sc,
                            screenWidth - ChipBarPad * 2f * sc, ChipBarHeight * sc - 16f * sc);
        }

        /// <summary>칩 줄 오른쪽 아이콘 한 칸의 한 변 (기준 캔버스 · ⚠️ 가정 · 최소 150 이다).</summary>
        public const float ChipIconSide = MinButton;

        /// <summary>
        /// 칩 줄 **왼쪽 무리** — 썸네일 + 닉네임 + 전투력.
        /// ⚠️ 판을 **무리마다** 깐다 — 줄 전체를 덮으면 오른쪽 버튼들이 판 밑으로 들어간다.
        /// </summary>
        public static Rect ChipLeftRect(float screenWidth, float screenHeight)
        {
            Rect bar = ChipBarRect(screenWidth, screenHeight);
            float sc = Scale(screenHeight);
            return new Rect(bar.x, bar.y, Mathf.Min(620f * sc, bar.width * 0.5f), bar.height);
        }

        /// <summary>
        /// 칩 줄 **소리 아이콘** — ⚠️⚠️ **`AudioOptionsPanel` 이 여기에 제 버튼을 그린다.**
        ///
        /// 📌 소리 버튼을 칩 줄이 **따로 그리지 않는다.** 그리면 같은 일을 하는 자리가 둘이
        /// 되고(되풀이되는 결함 종류다), 한쪽만 고쳐지는 날이 온다 — 자리만 여기서 정하고
        /// 그리는 것은 패널 하나가 한다.
        /// </summary>
        public static Rect ChipSoundRect(float screenWidth, float screenHeight)
        {
            Rect bar = ChipBarRect(screenWidth, screenHeight);
            float sc = Scale(screenHeight);
            float side = Mathf.Min(bar.height, ChipIconSide * sc);
            return new Rect(bar.xMax - side * 2f - 8f * sc, bar.y, side, bar.height);
        }

        /// <summary>칩 줄 **설정 아이콘** — 맨 오른쪽 구석. 메인 메뉴를 다시 연다.</summary>
        public static Rect ChipSettingsRect(float screenWidth, float screenHeight)
        {
            Rect bar = ChipBarRect(screenWidth, screenHeight);
            float sc = Scale(screenHeight);
            float side = Mathf.Min(bar.height, ChipIconSide * sc);
            return new Rect(bar.xMax - side, bar.y, side, bar.height);
        }

        /// <summary>칩 줄 **골드 칩** — 소리 아이콘 왼쪽.</summary>
        public static Rect ChipGoldRect(float screenWidth, float screenHeight)
        {
            Rect bar = ChipBarRect(screenWidth, screenHeight);
            Rect sound = ChipSoundRect(screenWidth, screenHeight);
            float sc = Scale(screenHeight);
            float w = Mathf.Min(360f * sc, bar.width * 0.28f);
            return new Rect(sound.x - 12f * sc - w, bar.y, w, bar.height);
        }

        /// <summary>
        /// 스테이지 배지 + 목표 한 줄 + 「남은 몬스터 · 리젠」 — 칩 줄 **바로 아래 왼쪽**.
        /// ⚠️ 가정 — 폭은 상태창과 같은 760(화면 절반을 안 넘긴다).
        /// </summary>
        public static Rect StageBadgeRect(float screenWidth, float screenHeight)
        {
            float sc = Scale(screenHeight);
            float w = Mathf.Min(760f * sc, screenWidth - ChipBarPad * 2f * sc);
            return new Rect(ChipBarPad * sc, (ChipBarHeight + 16f) * sc, w, 210f * sc);
        }

        /// <summary>
        /// 좌측 「i」 — **개발 빌드 전용** 접이식 진단 패널의 손잡이.
        /// ⚠️ 가정 — 배지 아래, 최소 버튼 한 변.
        /// </summary>
        public static Rect DevToggleRect(float screenWidth, float screenHeight)
        {
            float sc = Scale(screenHeight);
            Rect badge = StageBadgeRect(screenWidth, screenHeight);
            return new Rect(badge.x, badge.yMax + 12f * sc, MinButton * sc * 0.6f, MinButton * sc * 0.6f);
        }

        /// <summary>펼친 진단 패널. ⚠️ 가정 — 손잡이 아래로 흐르고 화면 안에서 잘린다.</summary>
        public static Rect DevPanelRect(float screenWidth, float screenHeight)
        {
            float sc = Scale(screenHeight);
            Rect toggle = DevToggleRect(screenWidth, screenHeight);
            float top = toggle.yMax + 8f * sc;
            float w = Mathf.Min(760f * sc, screenWidth - ChipBarPad * 2f * sc);
            return new Rect(toggle.x, top, w, Mathf.Max(0f, screenHeight - top - 40f * sc));
        }

        /// <summary>
        /// 마일스톤 카드 — **우하단**. 원형 둘(x1280) 아래, 조립 막대(y2460) 위에 든다.
        /// ⚠️ 가정 — 460×300 · 오른 여백 40.
        /// </summary>
        public static Rect MilestoneCardRect(float screenWidth, float screenHeight)
        {
            // 오른쪽 기둥의 **넷째 칸**이다 — 합체 원형(아랫변 2100) 아래, 조립 막대
            // (윗변 2380) 위. ⚠️ 크기·자리 전부 가정이며 겹치지 않는 것이 정한 법이다.
            const float w = 560f, h = 240f;
            float top = 2120f, right = DesignWidth - ChipBarPad;
            return Px(new Rect(right - w, top, w, h), screenWidth, screenHeight);
        }

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
        /// ✅ **128 → 150**(2026-09-11 설계 확정). 이제 **버튼 최소 150 을 지킨다** —
        /// 판정 대기 표기는 걷었다.
        /// </summary>
        public const float ApplyButtonHeight = 150f;

        /// <summary>
        /// **모드 막대** (2026-09-11 사용자 확정 · 플랜 §71-22 ②).
        ///
        /// **왜 띠에서 나왔는가.** 부유 띠 오른쪽 칸은 **좁은 창에서 너무 작아졌다** —
        /// 615×1085 창(배율 0.42)에서 지름이 **75px** 이 되어 최소 150 의 절반에도 못 미쳤고,
        /// 소리 버튼과 화면 오른쪽 아래를 함께 다투었다.
        ///
        /// **조립 진입 막대와 같은 문법**으로 바꾼다 — 600×150 막대에 문구
        /// 「▼ 조립 모드로 / ▲ 이동 모드로」가 **상태 표시를 겸한다.** 자리는 **화면 가운데**,
        /// **부유 띠 바로 위**(= 보드 뷰포트 아랫변)에 여백 <see cref="ModeBarGap"/> 을 두고 앉는다.
        ///
        /// ⚠️ **띠 안이 아니라 띠 위다.** 보드의 아랫변에 붙어야 「이 막대가 보드를 조작한다」가
        /// 서고, 띠 안에 넣으면 미니맵·팔레트와 **같은 줄의 또 하나**가 된다.
        ///
        /// ⚠️ **항상 맨 위에 그린다** — 부유 띠 그릇이 이것을 덮은 것이 §71-22 ① 의 결함이었다.
        /// </summary>
        public const float ModeBarWidth = 600f;

        /// <summary>모드 막대 세로. 조립 진입 막대(160)와 달리 **버튼 최소 150** 에 맞춘다.</summary>
        public const float ModeBarHeight = 150f;

        /// <summary>모드 막대와 부유 띠 사이 여백 (기준 캔버스).</summary>
        public const float ModeBarGap = 16f;

        /// <summary>
        /// 버튼의 **최소 변** (기준 캔버스 · UI 문서 「1440 기준 150px」).
        ///
        /// 손가락 하나가 닿는 최소다. 지금 코드의 버튼은 `130×36` **날 픽셀**이라 이 값을 한참
        /// 밑돈다 — 리허설 1차의 「눌러도 안 먹었다」가 나온 자리 중 하나다.
        /// </summary>
        public const float MinButton = 150f;

        /// <summary>
        /// 팔레트 노드 버튼 한 변 (2026-09-15 사용자 확정 · C안 · 육안 6차 ⑥).
        ///
        /// **부유 띠 312 − 카테고리 탭 줄 96 = 216.** 띠 안에서 가질 수 있는 **최대**이고
        /// 정사각이라 그림과 글자가 둘 다 들어간다. 기준 캔버스 1440 에서 **한 줄 여섯 개**가
        /// 서고 나머지는 종전처럼 가로로 스크롤한다.
        ///
        /// ⚠️ **문서 최소 150 과 다른 값이다.** 150 은 「이보다 작으면 손이 안 닿는다」는
        /// **하한**이고, 이것은 「띠가 허락하는 최대」다 — 두 수는 같은 물음의 답이 아니다.
        /// </summary>
        /// ✅ **182 로 내렸다**(2026-09-16 사용자 확정 · 플랜 §74-12 C).
        ///
        /// 🗑️ **216 은 폐기.** 근거는 「부유 띠 312 − 탭 줄 96 = 216」이었고 **높이**로는 맞다.
        /// 그런데 사용자 확정이 **「한 줄에 여섯」**(09-15)이라 **폭**이 먼저 걸린다 —
        /// 되찾은 팔레트 폭 1176 을 여섯으로 나누면 여백을 빼고 **182** 다.
        ///
        /// ⚠️ 216 을 남겨 두면 **상한이 영영 안 걸린다** — 화면에는 늘 182 가 나오는데
        /// 코드는 216 이라고 말하는 셈이라, 다음 사람이 「왜 216 이 안 나오나」를 다시 판다.
        /// **화면에 나오는 수와 코드가 말하는 수를 같게 둔다.**
        ///
        /// 📌 폭을 굶기던 것은 폐기된 줌바였고 그것은 09-16 에 걷었다(팔레트 600 → 1176).
        public const float PaletteButtonSize = 182f;

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

            // ⚠️ **왼쪽 끝 → 가운데**(2026-09-15 사용자 확정 · 하단 개편 ③).
            //
            // 액션바에 있던 것은 이 버튼 하나뿐이었다(「적용」은 자리만 있고 아무도 안 그렸다).
            // 하나뿐인 것을 왼쪽에 붙여 두면 **오른쪽이 빈 판**으로 남는다 — 띠 전체가
            // 이 버튼의 자리라는 것이 화면에서 읽히지 않았다.
            float w = BarButtonWidth * s * 0.5f;
            return new Rect((screenWidth - w) * 0.5f, bar.y, w, bar.height);
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
        /// 모드 막대 — **화면 가운데 · 부유 띠 바로 위** (2026-09-11 사용자 확정 · §71-22 ②).
        ///
        /// 아랫변이 부유 띠 윗변에서 <see cref="ModeBarGap"/> 만큼 떨어진다 —
        /// 그 자리가 **보드 뷰포트의 아랫변**이다.
        /// </summary>
        public static Rect ModeBarRect(float screenWidth, float screenHeight)
        {
            float s = Scale(screenHeight);
            float w = ModeBarWidth * s, h = ModeBarHeight * s;
            float bandTop = DesignTop(Band.FloatBand) * s;
            return new Rect((screenWidth - w) * 0.5f, bandTop - ModeBarGap * s - h, w, h);
        }

        /// <summary>
        /// 부유 띠의 **미니맵 자리**(왼쪽) (2026-09-11 사용자 확정).
        ///
        /// ⚠️ **오른쪽은 비었다**(§71-22 ②) — 모드 버튼이 <see cref="ModeBarRect"/> 로 나갔다.
        /// <paramref name="right"/> 는 오른쪽 여백 자리를 재는 데만 남긴다.
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
        /// <summary>
        /// 부유 띠를 **두 줄로 쪼갠다** (2026-09-15 사용자 확정 · 하단 개편 ② ·
        /// 참고 = 명일방주: 엔드필드 배치 화면).
        ///
        /// 위 = **카테고리 탭 줄**, 아래 = **노드 버튼 줄**(가로 스크롤).
        ///
        /// ⚠️ **비율 0.38 은 가정이다** — 문서에 탭 줄 절이 없다(설계 역기입).
        /// 탭은 글자만 있어 낮아도 읽히고, 노드 버튼은 그림이 들어가 높아야 한다.
        ///
        /// ⚠️ **폐기 — 비율이 아니라 절대 높이로 나눈다**(2026-09-15 · 개편 ⑥).
        /// 비율로 두면 띠가 바뀔 때 **두 줄이 같이 작아진다.** 노드 버튼은 규격(150)이
        /// 있으므로 **탭을 96 으로 못 박고 나머지를 버튼에 준다** — `CategoryTabHeight`.
        /// </summary>
        public const float CategoryTabFraction = 0.38f;

        /// <summary>
        /// **로봇 탭 A·B 자리** — 부유 띠 **왼쪽 세로 칸** (2026-09-16 · 사용자 확정 · §74-16 ①).
        ///
        /// ⚠️ **왜 이 자리인가.** 미니맵이 09-15 에 폐기돼(`a37a101` · 그리는 곳 0건)
        /// 왼쪽 정사각이 통째로 비었다. 튜토리얼 진행 두 줄도 같은 날 **보드 띠로
        /// 나갔으므로**(육안 7차 ②) 다투는 것이 없다.
        ///
        /// ⚠️⚠️ **정사각 자리(200×200)를 그대로 쓰지 않는다.** 둘을 가로로 나누면
        /// 한 변이 **97** 이 되어 최소 버튼 <see cref="MinButton"/>(150) 을 못 넘는다.
        /// 띠 **전체 높이**(312)를 세로로 둘로 나누면 각 **200×150** 이라 딱 맞는다 —
        /// 그래서 자리는 정사각이 아니라 **세로 칸**이다.
        ///
        /// ⚠️ 폭 200 은 <see cref="RoundButtonDiameter"/> 를 그대로 쓴다. 카테고리 탭과
        /// 노드 버튼 줄이 **둘 다 `FloatBandSlot(false).xMax` 오른쪽**에서 시작하므로,
        /// 그 폭을 벗어나지 않는 한 겹치지 않는다(같은 상수를 보게 두는 까닭이다).
        ///
        /// ⚠️ **가정이다** — UI 문서에 로봇 탭 절이 없다(설계 역기입 자리).
        /// </summary>
        public static Rect RobotTabRect(bool second, float screenWidth, float screenHeight)
        {
            Rect band = BandRect(Band.FloatBand, screenWidth, screenHeight);
            float s = Scale(screenHeight);

            // 정사각 자리와 **같은 x·폭**을 쓴다 — 오른쪽 것들이 그 끝을 기준으로 선다.
            Rect slot = FloatBandSlot(right: false, screenWidth, screenHeight);

            float gap = 12f * s;
            float h = (band.height - gap) * 0.5f;
            float y = band.y + (second ? h + gap : 0f);
            return new Rect(slot.x, y, slot.width, h);
        }

        /// <summary>카테고리 탭 줄 — 부유 띠 위쪽. 오른쪽 끝은 배율 막대가 쓴다.</summary>
        public static Rect CategoryTabRect(float screenWidth, float screenHeight)
        {
            Rect band = BandRect(Band.FloatBand, screenWidth, screenHeight);
            float s = Scale(screenHeight);
            float gap = 16f * s;
            float left = FloatBandSlot(false, screenWidth, screenHeight).xMax + gap;

            // ⚠️⚠️ **줌바 자리를 더 이상 비워 두지 않는다** (2026-09-16 · 육안 9차 ②).
            //
            // 종전에는 `ZoomBarRect(...).x - gap` 이었다. 그런데 **배율 버튼은 09-15 에
            // 핀치·휠로 대체됐고**(보드 개편 ⑦), 그리는 코드는 지웠는데 **자리는 안 걷었다** —
            // 전수 검색으로 `ZoomBarRect` 를 그리는 곳이 **0 건**임을 확인했다(시험만 참조한다).
            //
            // 그 빈 자리가 기준 폭 **560**(+ 여백 40)이라, 팔레트에 1440 중 **600** 밖에 안
            // 남았다. 여섯을 한 줄에 넣으라는 요구와 곱해져 버튼 한 변이 **기준 100**
            // 남짓으로 깎였다 — 화면에서 **40px**(사용자 실측)이다.
            //
            // 📌 **폐기는 한쪽만 하면 안 된다.** 그리는 쪽만 지우고 자리를 남기면,
            // 남은 자리가 **아무 일도 안 하면서 다른 것을 굶긴다.**
            float right = screenWidth - 24f * s;
            return new Rect(left, band.y, Mathf.Max(0f, right - left), CategoryTabHeight * s);
        }

        /// <summary>
        /// 노드 버튼 줄 — 부유 띠 **아래쪽**.
        ///
        /// ⚠️ **왼쪽 세로 칸은 로봇 탭 A·B 가 쓴다**(2026-09-16 · `RobotTabRect`).
        /// 🗑️ 구 주석 「튜토리얼 진행 두 줄이 쓴다」는 폐기 — 그 두 줄은 **같은 09-15 에
        ///    보드 띠로 나갔다**(육안 7차 ②). 폐기를 한쪽만 해서 주석만 남아 있었다.
        /// 미니맵이 나가며 빈 자리다.
        ///
        /// ⚠️ **오른쪽 끝은 배율 막대가 쓴다**(2026-09-14 · §72-12 5).
        /// 구 「오른쪽 끝까지 쓴다」(2026-09-11 · §71-22 ②)는 폐기다.
        /// </summary>
        public static Rect PaletteRect(float screenWidth, float screenHeight)
        {
            Rect band = BandRect(Band.FloatBand, screenWidth, screenHeight);
            float s = Scale(screenHeight);
            float gap = 16f * s;
            float left = FloatBandSlot(false, screenWidth, screenHeight).xMax + gap;

            // ⚠️ **줌바 자리를 안 비운다** — 까닭은 `CategoryTabRect` 에 적었다(폐기된 위젯).
            //    탭 줄과 버튼 줄은 **같은 폭**을 써야 한다. 한쪽만 넓히면 탭이 버튼 줄보다
            //    길어져 「어느 탭이 어느 칸을 여는가」가 화면에서 어긋난다.
            float right = screenWidth - 24f * s;

            float top = band.y + CategoryTabHeight * s;
            return new Rect(left, top, Mathf.Max(0f, right - left),
                Mathf.Max(0f, band.yMax - top));
        }

        /// <summary>
        /// **지금 무슨 모드인가** 판 — 보드 **우측 하단** (2026-09-15 · 하단 개편 ④).
        ///
        /// ⚠️ **가정이다 · 사용자 미답.** 구 중앙 600×150 막대를 걷고 이 판이 그 일을
        /// 대신한다 — 판이 곧 전환 버튼이다. 되돌릴 수 있게 **한 메서드**로 둔다.
        ///
        /// ⚠️ **보드 띠 안이다** — 부유 띠로 내리면 팔레트와 자리를 다투고, 전투 띠로
        /// 올리면 인셋을 가린다. 보드 오른쪽 아래는 실루엣이 안 닿는 모서리다.
        /// </summary>
        public const float ModePlateWidth = 320f;
        public const float ModePlateHeight = 96f;

        public static Rect ModePlateRect(float screenWidth, float screenHeight)
        {
            Rect band = BandRect(Band.Board, screenWidth, screenHeight);
            float s = Scale(screenHeight);
            float margin = 24f * s;
            float w = ModePlateWidth * s, h = ModePlateHeight * s;
            return new Rect(screenWidth - margin - w, band.yMax - margin - h, w, h);
        }

        // ───────────────────────── 조립 안내 줄 (가정) ─────────────────────────

        /// <summary>안내 줄 높이 · 왼쪽 여백 (기준 캔버스). ⚠️ 가정.</summary>
        public const float BoardHintHeight = 44f;
        public const float BoardHintMargin = 24f;

        /// <summary>안내 줄 글자 크기 (기준 캔버스). ⚠️ 가정.</summary>
        public const float BoardHintFontDesign = 32f;

        /// <summary>
        /// 안내 줄이 **실제로 쓸** 글자 크기 — 사다리로 올려 잡은 뒤의 값.
        ///
        /// ⚠️⚠️ **그리는 쪽에서 따로 스냅하면 안 된다.** 상자 높이가 이 값을 따라가야
        /// 하는데, 두 곳에서 각자 스냅하면 **같은 창에서 다른 답**이 나온다(지침 §7).
        /// </summary>
        public static int BoardHintFontSize(float screenHeight) =>
            KoreanFont.Snap(Mathf.Max(9, Mathf.RoundToInt(
                BoardHintFontDesign * Scale(screenHeight))));

        /// <summary>
        /// 조립 화면 안내 줄 자리 — 보드 띠 **왼쪽 아래**, ⚠️ **모드 막대를 피해서**.
        ///
        /// ⚠️⚠️ **종전 자리는 모드 막대 속이었다**(2026-09-21 사용자 육안 ⓐ — 「안내 줄이
        /// 절반 가려진다」). `boardBand.yMax − 24 − 44` 는 세로로 **2030~2074** 인데
        /// <see cref="ModeBarRect"/> 가 **1932~2082** 를 쓴다 — 두 자리가 겹쳐 있었다.
        /// 가로도 겹쳤다: 안내 줄은 폭을 `띠 × 0.6` 으로 잡아 창이 넓을수록 오른쪽으로
        /// 자라고, 모드 막대는 **화면 한가운데**라 넓어질수록 왼쪽으로 온다.
        ///
        /// 📌 **자리를 눈으로 고르지 않는다** — 두 사각이 안 겹치는지는 셈으로 답이 난다.
        /// <c>UiLayoutHintRectTests</c> 가 창 넷에서 그것을 지킨다.
        ///
        /// **세로로 피한다 — 모드 막대 윗변 위로 올린다.**
        ///
        /// ⚠️ **가로로 좁히는 길은 안 골랐다.** 막대 왼끝까지면 폭이 380(기준 캔버스)인데
        /// 이 문장은 32px 로 **900 넘게** 쓴다 — 좁히면 안 가려지는 대신 **문장이 잘린다.**
        /// 가려진 글자를 잘린 글자로 바꾸는 것은 고친 것이 아니다.
        /// </summary>
        public static Rect BoardHintRect(float screenWidth, float screenHeight)
        {
            Rect band = BandRect(Band.Board, screenWidth, screenHeight);
            Rect mode = ModeBarRect(screenWidth, screenHeight);
            float s = Scale(screenHeight);

            // ⚠️⚠️ **높이는 글자에서 낸다**(2026-09-21 사용자 육안 3차 ② — 「안내 줄이
            //    아직도 아래가 잘린다」). 종전 `44 × s` 는 **박힌 수**였다.
            //
            // `KoreanFont.Snap` 은 사다리에서 **위로** 올려 잡는다. 1080 창(s=0.42)에서
            // 글자는 13.4 → **16** 인데 상자는 **18.5** 였다 — 16px 한 줄이 IMGUI 에서
            // 20px 가까이 쓰니 **아래가 잘린다.** 1536 창은 19.2 → **24** 에 상자 26.4 로
            // 더 심했다. 기준 창(s=1)에서만 36 대 44 로 맞아서, 내 쪽에서는 멀쩡해 보였다.
            //
            // 📌 **글자를 올려 잡았으면 상자도 따라 커져야 한다** — 1.5 는 IMGUI 한 줄
            //    여유다(⚠️ 가정). 박힌 44 는 **아래 한계**로만 남긴다.
            float h = Mathf.Max(BoardHintHeight * s, BoardHintFontSize(screenHeight) * 1.5f);
            float x = band.x + BoardHintMargin * s;

            // 아랫변 기준 종전 자리 · 모드 막대에 닿으면 그 위로 올린다.
            float y = Mathf.Min(band.yMax - BoardHintMargin * s - h,
                                mode.y - 8f * s - h);

            // 창이 좁아 막대가 화면을 다 먹으면 띠 안에는 남는다.
            y = Mathf.Max(band.y, y);

            // 폭은 **종전 그대로 0.6** — 위로 올렸으므로 가로로 겹쳐도 상관없다.
            return new Rect(x, y, band.width * 0.6f, h);
        }

        // ───────────────────────── 조합표 팝오버 (가정) ─────────────────────────
        //
        // ⚠️ **전부 가정이다** — UI 문서에 조합표 패널 절이 없다(설계 역기입).
        //
        // ⚠️ **왜 팝오버인가.** 종전 자리는 `x 12 · y 380` **날 픽셀**이었고, 고른 노드가
        // 보드 어디에 있든 패널은 늘 화면 왼쪽에 떴다 — **무엇을 고쳤는지가 눈에서 멀었다.**
        // 오늘 하단 넷이 전부 「날 픽셀 자리가 문서 좌표 위에 남아 있었다」였고 이것이 같은 종류다.

        /// <summary>
        /// 조합표 팝오버 폭 (기준 캔버스). ⚠️ 가정.
        ///
        /// ⚠️ **380 → 560**(2026-09-15 · 육안 ② 팝오버 확장). 절이 넷으로 늘면서
        /// 「입력 아이콘 n/초 + 입력 아이콘 n/초 → 출력 아이콘 n/초」 한 줄이 380 을 넘는다.
        /// 구 380 은 조합표 버튼만 있던 시절의 폭이다 — 폐기 표기로 남긴다.
        /// </summary>
        public const float RecipePopoverWidth = 560f;

        /// <summary>줄 하나의 높이 (기준 캔버스). 버튼 최소 150 을 밑돈다 — ⚠️ 가정이고 판정거리다.</summary>
        public const float RecipePopoverRow = 76f;

        /// <summary>노드 칸과 팝오버 사이 틈 · 안쪽 여백 (기준 캔버스). ⚠️ 가정.</summary>
        public const float RecipePopoverGap = 20f;

        /// <summary>
        /// 조합표 팝오버가 앉을 자리 — **고른 노드 옆**.
        ///
        /// ⚠️ **오른쪽이 먼저다.** 오른쪽으로 나가면 왼쪽에 붙인다. 둘 다 안 되면 오른쪽에
        /// 붙이고 화면 안으로 민다 — **안 보이는 것보다 겹치는 편**이 낫다.
        ///
        /// ⚠️ **띠 밖으로는 안 나간다.** 위는 전투 인셋, 아래는 부유 띠다 — 거기까지 덮으면
        /// 「무엇을 고치는 중인지」가 아니라 「화면이 가려졌다」가 된다.
        /// </summary>
        public static Rect RecipePopoverRect(Vector2 nodeScreenPos, float cellPixels,
            float height, float screenWidth, float screenHeight)
        {
            float s = Scale(screenHeight);
            float w = RecipePopoverWidth * s;
            float gap = RecipePopoverGap * s;
            float half = cellPixels * 0.5f;

            float x = nodeScreenPos.x + half + gap;                 // 오른쪽이 먼저
            if (x + w > screenWidth) x = nodeScreenPos.x - half - gap - w;   // 안 되면 왼쪽
            x = Mathf.Clamp(x, 0f, Mathf.Max(0f, screenWidth - w));

            float top = BandRect(Band.Board, screenWidth, screenHeight).y;
            float bottom = BandRect(Band.FloatBand, screenWidth, screenHeight).y;

            float y = nodeScreenPos.y - height * 0.5f;              // 칸 가운데에 맞춘다
            y = Mathf.Clamp(y, top, Mathf.Max(top, bottom - height));

            return new Rect(x, y, w, height);
        }

        /// <summary>
        /// 배율 막대가 잡아 두는 폭 (기준 캔버스). ⚠️ **가정이다** — 문서에 배율 자리 절이 없다.
        ///
        /// 버튼 둘(각 <see cref="MinButton"/>)과 「보드 배율 ×1.00」 한 줄이 들어가는 크기다.
        /// 실제 글자 폭은 그리는 쪽이 재고, 여기서는 **팔레트가 물러설 만큼**만 잡는다.
        /// </summary>
        /// 🗑️ **폐기(2026-09-16)** — 배율 버튼이 09-15 에 핀치·휠로 대체되면서 그리는 곳이
        /// 0 건이 됐다. 지우지 않고 표기로 남기는 것은 시험이 아직 이 자리를 견주기
        /// 때문이다. **`PaletteRect` 는 더 이상 이 폭을 비워 두지 않는다.**
        public const float ZoomBarWidth = 560f;

        /// <summary>
        /// **보드 배율 조작 자리** — 부유 띠의 오른쪽 끝 (2026-09-14 · §72-12 5).
        ///
        /// ⚠️ **날 픽셀 자리를 걷었다.** 종전은 <c>x 164 · y 308</c> 고정이었고, 근거로 달린
        /// 「모드 버튼(12..152)의 오른쪽」은 모드 버튼이 <see cref="ModeBarRect"/> 로 나간
        /// 2026-09-11 부터 **없는 것을 가리키는 참조**였다.
        ///
        /// 더 나쁜 것은 y 308 이다 — 경고 띠가 기준 캔버스 **y 768** 에서 시작하는데, 창이
        /// 작으면 그 768 이 실제 픽셀로 300 대까지 내려온다. 2차 스크린샷에서 「생산이
        /// 멈췄습니다」가 배율 막대 뒤에 깔려 **둘 다 안 읽혔다.** 띠로 옮기면 그 겹침은
        /// **자리로** 사라진다(부유 띠는 2098 에서 시작한다).
        ///
        /// ⚠️ **가정이다** — 문서에 배율 자리 절이 없다. 값이 서면 이 메서드 하나만 바뀐다.
        /// </summary>
        public static Rect ZoomBarRect(float screenWidth, float screenHeight)
        {
            Rect band = BandRect(Band.FloatBand, screenWidth, screenHeight);
            float s = Scale(screenHeight);
            float margin = 24f * s, pad = 12f * s;
            float h = Mathf.Max(0f, band.height - pad * 2f);
            float w = ZoomBarWidth * s;
            return new Rect(screenWidth - margin - w, band.y + pad, w, h);
        }

        /// <summary>
        /// 튜토리얼 진행 두 줄이 앉을 자리 — **부유 띠 왼쪽** (2026-09-15 · 육안 ④).
        ///
        /// ⚠️ **종전은 `Rect(12, Screen.height − 96, 420, 84)` 날 픽셀이었다.**
        /// 띠 체계 밖이라 창 높이만 따라갔고, 화면이 낮으면 **HUD 첫 줄과 겹쳤다.**
        /// 09-14 하단 넷과 **같은 병**이다 — 날 픽셀 자리가 문서 좌표 위에 남아 있었다.
        ///
        /// ⚠️ **왜 부유 띠인가.** 이 두 줄은 조립 화면에 얹히는 **상태 표시**라
        /// 보드를 가리면 안 되고, 액션바는 버튼 자리다. 부유 띠는 오른쪽을 배율 막대가
        /// 쓰고 **왼쪽이 비어 있다**(§71-22).
        ///
        /// ⚠️ **가정이다** — 문서에 튜토리얼 진행 자리 절이 없다(설계 역기입 자리).
        /// 배율 막대와 안 겹치게 폭을 그 왼쪽까지로 자른다.
        ///
        /// 🗑️ **폐기 — 부르는 곳이 0 건이다**(2026-09-18 · 시안 3). 튜토리얼 목표 두 줄은
        /// **우하단 마일스톤 카드**로 갔다(<c>UiLayout.MilestoneCardRect</c>).
        /// 자리 계산은 남긴다 — 시험이 이 치수를 잡고 있고, 카드가 안 맞으면 되돌아올 자리다.
        /// </summary>
        public static Rect TutorialProgressRect(float screenWidth, float screenHeight)
        {
            float s = Scale(screenHeight);
            float pad = 12f * s;

            // ⚠️⚠️ **부유 띠에서 보드 띠로 내보냈다**(2026-09-15 · 사용자 육안 7차 ②).
            //
            // 종전 자리는 부유 띠 **왼쪽**이었다. 그런데 그 띠는 **탭 줄 여섯 + 노드 버튼**이
            // 함께 쓰는 자리라, **창이 좁아지면 셋이 같은 폭을 다툰다** — 실측(약 730px)에서
            // 튜토리얼 세 줄이 탭·버튼 **위에 그대로 겹쳐** 탭이 「전」 한 글자만 보였다.
            //
            // 📌 **띠 안에서 자리를 나누는 것으로는 못 푼다.** 탭 여섯과 버튼 여섯은
            // 이미 띠를 꽉 쓰기로 정한 값이다(216 · C안). 겹침을 없애려면 **띠 밖**이어야 한다.
            //
            // 보드 띠 **왼쪽 아래**로 내린다 — 모드 판이 오른쪽 아래를 쓰므로 반대 구석이고,
            // 조작 안내 한 줄(`BoardController`)이 그 아래에 있으므로 **그 위에** 앉는다.
            // 목표 두 줄은 보드에서 할 일을 말하므로 보드 안이 오히려 제자리다.
            Rect boardBand = BandRect(Band.Board, screenWidth, screenHeight);

            float lineH = 40f * s;                 // 목표 두 줄 + 진단 한 줄
            float h = lineH * 3f;
            float hintH = 44f * s;                 // 조작 안내 줄(같은 파일이 아니라 보드가 그린다)
            float bottomGap = 24f * s;

            float y = boardBand.yMax - bottomGap - hintH - 8f * s - h;
            float x = boardBand.x + bottomGap;
            float w = Mathf.Min(boardBand.width * 0.62f, 880f * s);

            // ⚠️⚠️ **모드 판 왼쪽까지로 자른다**(2026-09-16 · 사용자 육안 4차 ⑤ ·
            //    시험이 615x1920 에서 잡았다).
            //
            // 둘 다 보드 띠 **아래**를 쓴다 — 이쪽은 왼쪽 구석, 모드 판은 오른쪽 구석.
            // 넓은 창에서는 가운데가 비어 안 만나는데, **창이 좁아지면 62% 가 판까지
            // 닿는다.** 09-15 에 이 두 줄을 부유 띠에서 내보낸 것과 **같은 병**이다 —
            // 「띠 안에서 자리를 나누는 것으로는 못 푼다」고 그때 적어 두고도,
            // 폭을 비율로만 잡아 다시 만났다.
            //
            // 📌 **비율이 아니라 이웃의 자리에서 자른다** — 그래야 창 크기와 무관하다.
            float plateLeft = ModePlateRect(screenWidth, screenHeight).x;
            w = Mathf.Min(w, Mathf.Max(0f, plateLeft - bottomGap - x));

            return new Rect(x, Mathf.Max(boardBand.y + pad, y), w, h);
        }
    }
}
