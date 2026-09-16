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

        /// <summary>
        /// 🗑️ **구 「두 줄이 배율 막대와 안 겹친다」는 폐기됐다** (2026-09-16 · 육안 9차 ②).
        ///
        /// 배율 버튼은 09-15 에 **핀치·휠로 대체**됐다(보드 개편 ⑦). 그리는 코드는 지웠는데
        /// **자리는 안 걷어서**, 아무도 안 그리는 사각이 기준 폭 560 을 계속 잡고 있었다.
        /// 그 탓에 팔레트에 1440 중 600 만 남았고, 「여섯을 한 줄에」와 곱해져 버튼이
        /// **화면에서 40px** 로 깎였다(사용자 실측).
        ///
        /// 📌 이제 지키는 것은 반대다 — **두 줄이 그 자리를 실제로 쓴다.**
        /// 다시 좁아지면 같은 증상이 돌아온다.
        /// </summary>
        [Test]
        public void 두_줄이_폐기된_배율_막대_자리까지_쓴다()
        {
            Rect zoom = UiLayout.ZoomBarRect(W, H);
            Rect tabs = UiLayout.CategoryTabRect(W, H);
            Rect row = UiLayout.PaletteRect(W, H);

            Assert.That(row.xMax, Is.GreaterThan(zoom.x),
                "팔레트가 폐기된 줌바 자리를 아직 비워 두고 있다");
            Assert.That(tabs.xMax, Is.EqualTo(row.xMax).Within(0.5f),
                "탭 줄과 버튼 줄의 오른변이 다르다 — 같은 폭이어야 한다");
        }

        /// <summary>
        /// 되찾은 폭으로 버튼이 **얼마나 커지는가** — 수로 남긴다.
        ///
        /// ⚠️ **216 은 아직 못 채운다.** 여섯을 한 줄에 넣는 한 폭이 모자란다
        /// (기준 1440 에서 왼쪽 원형 240 을 빼면 1176 이고, 여섯으로 나누면 182 다).
        /// 「216 이냐 여섯이냐」는 **값 판정**이라 코드가 고르지 않는다 — 수만 남긴다.
        /// </summary>
        [Test]
        public void 되찾은_폭에서_버튼_한_변을_수로_남긴다()
        {
            Rect row = UiLayout.PaletteRect(W, H);
            const int wantVisible = 6;
            float pad = 12f;                     // 기준 캔버스에서의 여백

            float byWidth = (row.width - pad * 2f - pad * (wantVisible - 1)) / wantVisible;

            TestContext.WriteLine($"[재현] 팔레트 폭 {row.width:F0} · 여섯 기준 한 변 {byWidth:F0}"
                                  + $" · 상한 {UiLayout.PaletteButtonSize:F0}");

            Assert.That(byWidth, Is.GreaterThan(150f),
                "되찾은 폭에서도 버튼이 150 미만이면 폭 말고 다른 것이 자르고 있다");
            Assert.That(byWidth, Is.LessThan(UiLayout.PaletteButtonSize),
                "216 을 넘겼다면 상한이 안 걸린 것이다 — 이 시험의 설명을 고친다");
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
