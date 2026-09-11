using MBI.Core;
using MBI.UI;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 화면 배치 치수 (2026-09-11 신설 · 플랜 §68-4 (A) · UI 문서 9-4 · UI 아트 문서 4장).
    ///
    /// **무엇을 지키는가.** 여기 든 값은 전부 **문서에 있는 것**이라 시험이 지키는 것은
    /// 「예쁜가」가 아니라 **문서와 같은가**다. 띠 다섯이 화면을 정확히 덮는지, 문서가
    /// 다른 곳에 적어 둔 같은 값(전투 768 · 경고 띠 시작 768)과 어긋나지 않는지를 본다.
    /// </summary>
    public sealed class UiLayoutTests
    {
        private const float D = 0.001f;

        [Test]
        public void 띠_다섯이_화면을_정확히_덮는다()
        {
            // 하나를 고치면서 다른 하나를 안 고치면 틈이나 겹침이 생긴다 — 그 자리를 여기서 막는다.
            float sum = UiLayout.CombatHeight + UiLayout.BoardHeight + UiLayout.FloatBandHeight
                        + UiLayout.VariablePanelHeight + UiLayout.ActionBarHeight;
            Assert.AreEqual(UiLayout.DesignHeight, sum, D, "768 + 1330 + 200 + 112 + 150 = 2560");
        }

        [Test]
        public void 띠는_차례대로_맞닿고_겹치지_않는다()
        {
            var order = new[]
            {
                UiLayout.Band.Combat, UiLayout.Band.Board, UiLayout.Band.FloatBand,
                UiLayout.Band.VariablePanel, UiLayout.Band.ActionBar,
            };

            float cursor = 0f;
            foreach (UiLayout.Band b in order)
            {
                Assert.AreEqual(cursor, UiLayout.DesignTop(b), D, $"{b} 윗변");
                cursor += UiLayout.DesignHeightOf(b);
            }
            Assert.AreEqual(UiLayout.DesignHeight, cursor, D, "마지막 띠가 바닥에서 끝난다");
        }

        [Test]
        public void 기준_캔버스는_문서_둘과_같은_값이다()
        {
            // 같은 숫자를 두 곳에 적어 두면 한쪽만 고쳐지는 날이 온다 — 그 날을 여기서 잡는다.
            Assert.AreEqual(SupplyStopRules.DesignScreenHeight, UiLayout.DesignHeight, D,
                "경고 띠가 쓰는 기준 캔버스와 같아야 한다");
            Assert.AreEqual(1440f / 2560f, UiLayout.DesignWidth / UiLayout.DesignHeight, D,
                "기준 비 0.5625");
        }

        [Test]
        public void 전투_띠는_인셋_뷰포트와_같은_몫이다()
        {
            // 카메라 뷰포트(0.3)와 배치 값(768)이 갈리면 전투 그림과 경고 띠가 어긋난다.
            Assert.AreEqual(CombatInsetView.HeightShare,
                UiLayout.CombatHeight / UiLayout.DesignHeight, D,
                "768 ÷ 2560 = 0.3 = CombatInsetView.HeightShare");
        }

        [Test]
        public void 경고_띠는_전투_아랫변에서_시작한다()
        {
            Assert.AreEqual(UiLayout.DesignTop(UiLayout.Band.Board),
                SupplyStopRules.DesignBandTop, D,
                "맞닿되 겹치지 않는다 — 보드 띠 윗변 = 경고 띠 시작");
        }

        [Test]
        public void 문서_요소_치수가_그대로다()
        {
            Assert.AreEqual(200f, UiLayout.RoundButtonDiameter, D, "합체·태그 원형 200");
            Assert.AreEqual(600f, UiLayout.BarButtonWidth, D, "조립 진입 막대 600");
            Assert.AreEqual(160f, UiLayout.BarButtonHeight, D, "조립 진입 막대 160");
            Assert.AreEqual(320f, UiLayout.ApplyButtonWidth, D, "적용 320");
            Assert.AreEqual(150f, UiLayout.MinButton, D, "버튼 최소 변 150(1440 기준)");
        }

        [Test]
        public void 적용_버튼은_액션바를_꽉_채운다()
        {
            // 문서가 둘 다 128 로 적어 두었다 — 하나만 바뀌면 띠 안에 틈이 생긴다.
            Assert.AreEqual(UiLayout.ActionBarHeight, UiLayout.ApplyButtonHeight, D);
        }

        [Test]
        public void 문서_요소는_최소_변을_지킨다()
        {
            Assert.IsTrue(UiLayout.MeetsMinButton(UiLayout.RoundButtonDiameter), "원형 200");
            Assert.IsTrue(UiLayout.MeetsMinButton(UiLayout.BarButtonHeight), "막대 높이 160");

            // ✅ **닫혔다**(2026-09-11 설계 확정 (가)). 구 128 은 버튼 최소 150 을 밑돌아
            // UI 아트 5-3 과 6-2 가 서로 안 맞았다 — **띠를 버튼 최소에 맞추는 쪽**으로
            // 답이 왔고, 보드에서 22 를 덜어 다섯 합 2560 을 지켰다.
            // **단언이 뒤집혔다** — 판정 대기 표기를 걷는다.
            Assert.IsTrue(UiLayout.MeetsMinButton(UiLayout.ApplyButtonHeight),
                "적용 150 = 버튼 최소 150");
        }

        [Test]
        public void 환산은_세로로_맞춘다()
        {
            Assert.AreEqual(1f, UiLayout.Scale(2560f), D, "기준 창에서는 1배");
            Assert.AreEqual(0.5f, UiLayout.Scale(1280f), D);

            // 가로가 아무리 넓어도 띠 높이는 세로만 따라간다 — 가로로 맞추면 띠가 화면 밖으로 민다.
            Rect wide = UiLayout.BandRect(UiLayout.Band.ActionBar, 4000f, 2560f);
            Rect narrow = UiLayout.BandRect(UiLayout.Band.ActionBar, 800f, 2560f);
            Assert.AreEqual(narrow.height, wide.height, D);
            Assert.AreEqual(4000f, wide.width, D, "가로는 창 전체");
        }

        [Test]
        public void 조립_진입_막대는_레이어_1_좌표에_앉는다()
        {
            // ⚠️ **구 시험을 걷었다**(2026-09-11 사용자 정정). 종전에는 「막대 160 이 액션바 128 을
            // 위아래 16씩 넘친다」를 재고 있었는데, **두 수는 다른 화면의 수**다 —
            // 조립 진입은 **레이어 1**(전투 화면 · 절대 좌표 720, 2460)이고 액션바는 레이어 2 다.
            // 없는 충돌을 시험이 지키고 있었다.
            Rect bar = UiLayout.EnterBoardRect(1440f, 2560f);

            Assert.AreEqual(600f, bar.width, D);
            Assert.AreEqual(160f, bar.height, D);
            Assert.AreEqual(720f, bar.center.x, D, "x720 = 화면 한가운데");
            Assert.AreEqual(2460f, bar.center.y, D, "y2460");
            Assert.LessOrEqual(bar.yMax, UiLayout.DesignHeight, "화면 밖으로 안 나간다");
        }

        [Test]
        public void 원형_둘은_x1280_에_세로로_쌓인다()
        {
            Rect tag = UiLayout.RoundButtonRect(0, 1440f, 2560f);
            Rect merge = UiLayout.RoundButtonRect(1, 1440f, 2560f);

            Assert.AreEqual(200f, tag.width, D);
            Assert.AreEqual(tag.width, tag.height, D, "원형이라 정사각이다");
            Assert.AreEqual(1280f, tag.center.x, D, "둘 다 x1280");
            Assert.AreEqual(1280f, merge.center.x, D);
            Assert.AreEqual(2080f, tag.center.y, D, "태그가 위");
            Assert.AreEqual(2300f, merge.center.y, D, "합체가 아래");

            // 가운데 사이가 220 이고 지름이 200 이라 20 이 뜬다 — 붙으면 오조작이 난다.
            Assert.IsFalse(tag.Overlaps(merge), "태그와 합체가 겹치면 오조작이 난다");
            Assert.AreEqual(20f, merge.y - tag.yMax, D, "사이 20");
        }

        [Test]
        public void 레이어_1_셋은_조립_띠에_안_들어간다()
        {
            // 사용자 확정: 「조립 진입·합체·태그는 조립 화면 띠에 안 들어간다.」
            // 레이어 2 의 띠 자리와 섞어 재던 것이 오늘의 오진이었다 — 그 자리를 여기서 막는다.
            Assert.AreEqual(2560f, UiLayout.DesignTop(UiLayout.Band.ActionBar)
                                   + UiLayout.ActionBarHeight, D, "레이어 2 는 띠로 2560 을 채운다");

            // 레이어 1 의 셋은 **띠 소속이 아니라 좌표**다. 좌표가 어느 띠와 겹치든 그것은
            // 다른 화면의 자리라 충돌이 아니다 — 겹침을 단언하지 **않는** 것이 이 시험의 내용이다.
            Assert.AreEqual(2460f, UiLayout.EnterBoardCenter.y, D);
            Assert.AreEqual(2080f, UiLayout.TagButtonCenter.y, D);
            Assert.AreEqual(2300f, UiLayout.MergeButtonCenter.y, D);
            Assert.AreEqual(120f, UiLayout.InfoBarHeight, D, "상단 정보줄 120");
            Assert.AreEqual(new Vector2(40f, 160f), UiLayout.StatusPanelOrigin, "상태창 x40 y160");
        }

        [Test]
        public void 모드_막대는_부유_띠_바로_위_가운데다()
        {
            Rect bar = UiLayout.ModeBarRect(1440f, 2560f);
            Rect band = UiLayout.BandRect(UiLayout.Band.FloatBand, 1440f, 2560f);

            Assert.AreEqual(600f, bar.width, D);
            Assert.AreEqual(150f, bar.height, D);
            Assert.AreEqual(720f, bar.center.x, D, "화면 가운데");
            Assert.AreEqual(16f, band.y - bar.yMax, D, "띠 위 여백 16");
            Assert.IsFalse(bar.Overlaps(band), "띠 안이 아니라 띠 위다");

            // ⚠️ **보드 뷰포트 안에 든다** — 아랫변이 보드 띠의 아랫변이다.
            Rect board = UiLayout.BandRect(UiLayout.Band.Board, 1440f, 2560f);
            Assert.IsTrue(bar.yMax <= board.yMax + D, "보드 뷰포트를 안 넘는다");
        }

        [Test]
        public void 모드_막대는_좁은_창에서도_최소를_지킨다()
        {
            // ⚠️ **이것이 §71-22 ① 의 재발 방지다.** 615×1085(배율 0.42)에서 종전 원형 칸은
            // 지름 75px 로 최소 150 의 절반에도 못 미쳤다. 막대는 세로 비율 환산 그대로여도
            // **화면 가로의 절반 가까이**를 차지한다.
            Rect bar = UiLayout.ModeBarRect(615f, 1085f);
            Assert.Greater(bar.width, 615f * 0.35f, "좁은 창에서도 눈에 띈다");
            Assert.Greater(bar.height, 0f);
            Assert.AreEqual(615f * 0.5f, bar.center.x, 0.5f, "가운데는 창 폭의 절반이다");
            Assert.IsTrue(bar.xMin >= 0f && bar.xMax <= 615f, "화면 밖으로 안 나간다");
        }

        [Test]
        public void 팔레트는_띠_오른쪽_끝까지_쓴다()
        {
            // 모드 버튼이 띠에서 나가면서 그 칸이 비었다 — 팔레트가 좁을 이유가 없다.
            Rect pal = UiLayout.PaletteRect(1440f, 2560f);
            Assert.Greater(pal.xMax, 1440f - 40f, "오른쪽 여백까지 쓴다");
        }

        [Test]
        public void 부유_띠_왼쪽은_미니맵이다()
        {
            Rect map = UiLayout.FloatBandSlot(right: false, 1440f, 2560f);
            Rect band = UiLayout.BandRect(UiLayout.Band.FloatBand, 1440f, 2560f);

            Assert.IsTrue(band.y <= map.y && map.yMax <= band.yMax, "미니맵은 부유 띠 안");
            // ⚠️ **오른쪽 칸은 비었다**(2026-09-11 · §71-22 ②) — 모드 버튼이 막대로 나갔다.
            Assert.IsFalse(map.Overlaps(UiLayout.ModeBarRect(1440f, 2560f)),
                "미니맵과 모드 막대가 겹치면 안 된다");
        }

        [Test]
        public void 팔레트는_미니맵과_모드_사이다()
        {
            // 팔레트 자리는 **가정**이지만, 같은 띠를 쓰는 미니맵·모드와 겹치지 않는 것은
            // 가정이 아니라 규칙이다 — 겹치면 눌리는 쪽이 그리는 차례로 정해진다.
            Rect pal = UiLayout.PaletteRect(1440f, 2560f);
            Rect map = UiLayout.FloatBandSlot(right: false, 1440f, 2560f);

            Assert.IsFalse(pal.Overlaps(map), "팔레트와 미니맵이 겹치면 안 된다");
            Assert.IsFalse(pal.Overlaps(UiLayout.ModeBarRect(1440f, 2560f)),
                "팔레트와 모드 막대가 겹치면 안 된다");
            Assert.Greater(pal.width, 0f, "기준 창에서는 자리가 남는다");
        }

        [Test]
        public void 액션바_둘은_안_겹친다()
        {
            // 적용(오른쪽 · 문서)과 「전투로」(왼쪽 · 가정)가 같은 띠를 쓴다.
            Rect apply = UiLayout.ApplyRect(1440f, 2560f);
            Rect exit = UiLayout.ExitBoardRect(1440f, 2560f);
            Rect band = UiLayout.BandRect(UiLayout.Band.ActionBar, 1440f, 2560f);

            Assert.AreEqual(320f, apply.width, D);
            Assert.AreEqual(150f, apply.height, D);
            Assert.IsFalse(apply.Overlaps(exit));
            Assert.IsTrue(band.y <= apply.y && apply.yMax <= band.yMax + D, "액션바 안");
            Assert.IsTrue(band.y <= exit.y && exit.yMax <= band.yMax + D, "액션바 안");
        }
    }
}
