using MBI.UI;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 부유 띠 두 줄 (2026-09-15 · 하단 개편 ⑥ · 육안 「버튼이 너무 작다」).
    ///
    /// ⚠️ **노드 버튼은 UI 6-2 의 최소 150 을 지켜야 한다.** 200 짜리 띠에 두 줄을 넣으면
    /// 둘 다 그것을 밑돌았다 — 걷어 낸 변수 패널의 112 를 받아 312 로 키웠다.
    /// </summary>
    public sealed class FloatBandLayoutTests
    {
        [Test]
        public void 팔레트_버튼은_띠에서_남는_높이를_다_쓴다()
        {
            // 2026-09-15 사용자 확정 C안 — 부유 띠 312 − 카테고리 탭 줄 96 = 216.
            Assert.That(UiLayout.PaletteButtonSize,
                Is.EqualTo(UiLayout.FloatBandHeight - UiLayout.CategoryTabHeight).Within(0.01f),
                "띠에서 탭 줄을 뺀 나머지가 버튼 한 변이다 — 한쪽만 고치면 버튼이 띠를 넘거나 남긴다");

            Assert.That(UiLayout.MeetsMinButton(UiLayout.PaletteButtonSize), Is.True,
                "문서 최소 150 을 넘어야 한다");
        }

        [Test]
        public void 기준_캔버스에서_한_줄에_여섯_개가_선다()
        {
            // 1440 기준 · 좌우 여백과 버튼 사이 여백을 넉넉히 12 씩 잡아도 여섯은 든다.
            const float canvas = 1440f;
            int fit = Mathf.FloorToInt(canvas / (UiLayout.PaletteButtonSize + 12f));
            Assert.That(fit, Is.GreaterThanOrEqualTo(6),
                $"한 줄에 {fit} 개만 선다 — 여섯이 안 서면 스크롤이 곧바로 필요해진다");
        }

        private const float W = 1440f, H = 2560f;

        [Test]
        public void 띠_다섯의_합은_여전히_2560이다()
        {
            float sum = UiLayout.CombatHeight + UiLayout.BoardHeight
                        + UiLayout.FloatBandHeight + UiLayout.VariablePanelHeight
                        + UiLayout.ActionBarHeight;

            Assert.That(sum, Is.EqualTo(UiLayout.DesignHeight).Within(0.001f),
                "부유 띠를 키우면서 어딘가를 안 줄이면 띠가 화면 밖으로 밀린다");
        }

        [Test]
        public void 노드_버튼_줄이_최소_150을_넘는다()
        {
            // 기준 캔버스에서 잰다 — 실제 창은 이 값에 배율이 곱해진다.
            float rowDesign = UiLayout.FloatBandHeight - UiLayout.CategoryTabHeight;

            Assert.That(UiLayout.MeetsMinButton(rowDesign), Is.True,
                "노드 버튼은 손가락이 닿는 크기여야 한다(UI 6-2) — 지금 "
                + rowDesign + " / 최소 " + UiLayout.MinButton);
        }

        [Test]
        public void 두_줄이_띠_안에서_안_겹친다()
        {
            Rect band = UiLayout.BandRect(UiLayout.Band.FloatBand, W, H);
            Rect tabs = UiLayout.CategoryTabRect(W, H);
            Rect row = UiLayout.PaletteRect(W, H);

            Assert.That(tabs.y, Is.EqualTo(band.y).Within(0.5f), "탭은 띠 맨 위에서 시작한다");
            Assert.That(row.y, Is.EqualTo(tabs.yMax).Within(0.5f), "버튼 줄은 탭 바로 아래다");
            Assert.That(row.yMax, Is.LessThanOrEqualTo(band.yMax + 0.5f), "띠 밖으로 넘치면 안 된다");
        }

        [Test]
        public void 두_줄이_배율_막대와_안_겹친다()
        {
            Rect zoom = UiLayout.ZoomBarRect(W, H);
            Assert.That(UiLayout.CategoryTabRect(W, H).xMax, Is.LessThanOrEqualTo(zoom.x + 0.5f));
            Assert.That(UiLayout.PaletteRect(W, H).xMax, Is.LessThanOrEqualTo(zoom.x + 0.5f));
        }

        [Test]
        public void 튜토리얼_두_줄이_두_줄_왼쪽에_선다()
        {
            // 미니맵이 쓰던 정사각 자리다(개편 ⑤).
            Rect tut = UiLayout.TutorialProgressRect(W, H);
            Rect row = UiLayout.PaletteRect(W, H);

            Assert.That(tut.x, Is.LessThan(row.x), "버튼 줄보다 왼쪽이어야 자리를 안 다툰다");
        }

        [Test]
        public void 전투로_버튼이_액션바_가운데다()
        {
            Rect bar = UiLayout.BandRect(UiLayout.Band.ActionBar, W, H);
            Rect exit = UiLayout.ExitBoardRect(W, H);

            Assert.That(exit.center.x, Is.EqualTo(W * 0.5f).Within(0.5f), "가운데가 아니면 띠가 빈 판으로 남는다");
            Assert.That(exit.y, Is.EqualTo(bar.y).Within(0.5f));
        }

        [Test]
        public void 모드_판이_보드_오른쪽_아래다()
        {
            Rect board = UiLayout.BandRect(UiLayout.Band.Board, W, H);
            Rect plate = UiLayout.ModePlateRect(W, H);

            Assert.That(plate.yMax, Is.LessThanOrEqualTo(board.yMax + 0.5f), "보드 띠를 넘으면 부유 띠와 다툰다");
            Assert.That(plate.xMax, Is.LessThanOrEqualTo(W + 0.5f));
            Assert.That(plate.x, Is.GreaterThan(W * 0.5f), "오른쪽 절반에 있어야 한다");
        }
    }
}
