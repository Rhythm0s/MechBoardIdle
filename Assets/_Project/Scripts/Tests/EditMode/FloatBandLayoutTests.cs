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
            // 2026-09-15 사용자 확정 C안은 「띠 312 − 탭 96 = 216」이었다.
            //
            // 그 값은 **높이**로는 맞다. 그런데 같은 날 「한 줄에 여섯」도 확정됐고,
            // 되찾은 팔레트 폭 1176 을 여섯으로 나누면 **182** 다 — **폭이 먼저 걸린다.**
            // 09-16 에 사용자가 182 로 내렸다(§74-12 C).
            //
            // 📌 지키는 것이 바뀌었다 — 「띠와 똑같다」가 아니라 **「띠를 안 넘는다」**다.
            float roomInBand = UiLayout.FloatBandHeight - UiLayout.CategoryTabHeight;
            Assert.That(UiLayout.PaletteButtonSize, Is.LessThanOrEqualTo(roomInBand + 0.01f),
                "버튼 한 변이 띠에서 남는 높이를 넘었다 — 넘으면 액션바에 물린다");

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
            // 2026-09-16 사용자 확정 — 상한을 216 에서 **182** 로 내렸다(§74-12 C).
            // 화면에 나오는 수와 코드가 말하는 수를 같게 둔다. 216 을 남겨 두면
            // 상한이 영영 안 걸리고, 다음 사람이 「왜 216 이 안 나오나」를 다시 판다.
            Assert.That(byWidth, Is.EqualTo(UiLayout.PaletteButtonSize).Within(1f),
                "폭에서 나온 한 변과 상한이 다르다 — 둘 중 하나가 낡았다");
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
        public void 로봇_탭_둘이_띠를_세로로_나눠_쓴다()
        {
            // 🗑️ 구 자리(정사각 200×200)를 **가로로** 나누면 한 변이 97 이 되어
            //    최소 버튼 150 을 못 넘는다 — 그래서 **세로**로 나눈다(2026-09-16).
            Rect a = UiLayout.RobotTabRect(false, W, H);
            Rect b = UiLayout.RobotTabRect(true, W, H);
            Rect band = UiLayout.BandRect(UiLayout.Band.FloatBand, W, H);
            float s = UiLayout.Scale(H);

            Assert.That(a.width, Is.EqualTo(b.width).Within(0.5f), "둘의 폭이 다르면 줄이 안 선다");
            Assert.That(a.x, Is.EqualTo(b.x).Within(0.5f));

            Assert.That(a.height, Is.GreaterThanOrEqualTo(UiLayout.MinButton * s - 0.5f),
                "탭 A 높이가 최소 버튼 150 을 못 넘는다");
            Assert.That(b.height, Is.GreaterThanOrEqualTo(UiLayout.MinButton * s - 0.5f),
                "탭 B 높이가 최소 버튼 150 을 못 넘는다");
            Assert.That(a.width, Is.GreaterThanOrEqualTo(UiLayout.MinButton * s - 0.5f),
                "탭 폭이 최소 버튼 150 을 못 넘는다");

            Assert.That(a.yMax, Is.LessThanOrEqualTo(b.y + 0.5f), "둘이 세로로 겹친다");
            Assert.That(band.y, Is.LessThanOrEqualTo(a.y + 0.5f), "띠 위로 삐져나간다");
            Assert.That(b.yMax, Is.LessThanOrEqualTo(band.yMax + 0.5f), "띠 아래로 삐져나간다");
        }

        [Test]
        public void 로봇_탭이_탭_줄과_버튼_줄과_안_겹친다()
        {
            // **이것이 지키는 것** — 왼쪽 세로 칸을 셋이 다투면 09-15 에 튜토리얼 두 줄이
            // 탭 위에 겹쳐 「전」 한 글자만 보이던 그 일이 그대로 돌아온다(육안 7차 ②).
            Rect a = UiLayout.RobotTabRect(false, W, H);
            Rect tabs = UiLayout.CategoryTabRect(W, H);
            Rect row = UiLayout.PaletteRect(W, H);

            Assert.That(a.xMax, Is.LessThanOrEqualTo(tabs.x + 0.5f), "카테고리 탭과 겹친다");
            Assert.That(a.xMax, Is.LessThanOrEqualTo(row.x + 0.5f), "노드 버튼 줄과 겹친다");
        }

        [Test]
        public void 좁은_창에서도_로봇_탭이_띠를_안_넘는다()
        {
            foreach (float w in new[] { 615f, 720f, 1080f, 1440f })
            foreach (float h in new[] { 1085f, 1280f, 1920f, 2560f })
            {
                Rect band = UiLayout.BandRect(UiLayout.Band.FloatBand, w, h);
                Rect a = UiLayout.RobotTabRect(false, w, h);
                Rect b = UiLayout.RobotTabRect(true, w, h);

                Assert.That(a.y, Is.GreaterThanOrEqualTo(band.y - 0.5f), $"{w}x{h}");
                Assert.That(b.yMax, Is.LessThanOrEqualTo(band.yMax + 0.5f), $"{w}x{h}");
                Assert.That(a.height, Is.GreaterThan(0f), $"{w}x{h}");
            }
        }

        [Test]
        public void 모드_판이_보드_띠_안에_있고_다른_것과_안_겹친다()
        {
            // 2026-09-16 육안 4차 ⑤ — 「이동 모드(바꾸기) 판이 HUD 글자 블록과 겹친다」.
            // ⚠️ **레이아웃 셈으로는 재현이 안 됐다** — 조립 화면의 HUD 글자는
            //    전투 띠 안에 갇히고 이 판은 **보드 띠** 안이다.
            //    ✅ 대신 **튜토리얼 진행 두 줄과 겹치는 것**을 이 시험이 잡았다(615x1920) —
            //       사용자가 본 「HUD 글자 블록」이 그 두 줄이었을 가능성이 높다.
            //    그래서 지금 잡을 수 있는 것부터 박아 둔다: 띠를 안 넘고, 보드 띠를
            //    같이 쓰는 것들과 안 겹친다.
            foreach (float w in new[] { 615f, 720f, 1080f, 1440f })
            foreach (float h in new[] { 1085f, 1280f, 1920f, 2560f })
            {
                Rect plate = UiLayout.ModePlateRect(w, h);
                Rect boardBand = UiLayout.BandRect(UiLayout.Band.Board, w, h);
                Rect combatBand = UiLayout.BandRect(UiLayout.Band.Combat, w, h);

                Assert.That(plate.y, Is.GreaterThanOrEqualTo(boardBand.y - 0.5f), $"{w}x{h} 보드 띠 위로 나갔다");
                Assert.That(plate.yMax, Is.LessThanOrEqualTo(boardBand.yMax + 0.5f), $"{w}x{h} 보드 띠 아래로 나갔다");
                Assert.That(plate.xMax, Is.LessThanOrEqualTo(w + 0.5f), $"{w}x{h} 화면 밖으로 나갔다");

                // **전투 띠와 안 겹친다** — HUD 글자 블록이 사는 띠다.
                Assert.IsFalse(plate.Overlaps(combatBand), $"{w}x{h} 전투 띠(HUD 글자)와 겹친다");

                // 튜토리얼 진행 두 줄도 보드 띠를 쓴다 — 반대 구석이라 안 겹쳐야 한다.
                Assert.IsFalse(plate.Overlaps(UiLayout.TutorialProgressRect(w, h)),
                    $"{w}x{h} 튜토리얼 진행 두 줄과 겹친다");

                // 부유 띠(팔레트·탭)와도 안 겹친다.
                Assert.IsFalse(plate.Overlaps(UiLayout.BandRect(UiLayout.Band.FloatBand, w, h)),
                    $"{w}x{h} 부유 띠와 겹친다");
            }
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
