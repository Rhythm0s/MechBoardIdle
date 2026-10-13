using MBI.UI;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **결과 배너가 「다시」 단추를 안 덮는다** (2026-10-13 사용자 확정 ② · 빌드 c74a0d2).
    ///
    /// ⚠️⚠️ **왜 덮었나.** 종전 상자 높이는 **160 × 배율**로 박혀 있었고, 글자 크기는
    /// <c>Mathf.Max(16, …)</c> 으로 **아래가 막혀** 있었다. 창이 작아지면 상자만 줄고
    /// 글자는 안 줄어 한 줄이 상자를 먹는다 — 판 여백·안내 줄과 **같은 병**이다.
    ///
    /// 📌 그래서 글자 한 줄 높이를 **받아서** 상자를 낸다. 여기서 재는 줄 높이는
    /// 그리는 쪽(<c>StageRunner</c>)이 <c>big.lineHeight</c> 로 넘기는 값과 같은 자리다.
    /// </summary>
    public sealed class ResultBannerTests
    {
        /// <summary>세로로 긴 창 넷 + **넓고 낮은 창 셋**(사용자가 쓰는 쪽).</summary>
        private static readonly Vector2[] Screens =
        {
            new Vector2(1440f, 2560f), new Vector2(720f, 1280f),
            new Vector2(1080f, 1920f), new Vector2(1536f, 2730f),
            new Vector2(1600f, 690f), new Vector2(1920f, 900f),
            new Vector2(1280f, 720f),
        };

        /// <summary>
        /// 그릴 때 쓰는 글자 크기 규칙을 그대로 옮긴 것 —
        /// <c>Snap(Max(16, round(68 × 배율)))</c> 에 IMGUI 한 줄 여유를 곱한다.
        /// ⚠️ **아래가 막혀 있다**는 것이 이 결함의 뿌리이므로 그 막음을 꼭 넣는다.
        /// </summary>
        private static float LinePx(float screenHeight)
        {
            float sc = UiLayout.Scale(screenHeight);
            int px = Mathf.Max(16, Mathf.RoundToInt(68f * sc));
            return px * 1.2f;
        }

        [Test]
        public void 글자와_단추가_안_겹친다()
        {
            foreach (Vector2 s in Screens)
            {
                float line = LinePx(s.y);
                Rect text = UiLayout.ResultBannerTextRect(s.x, s.y, line);
                Rect button = UiLayout.ResultBannerButtonRect(s.x, s.y, line);

                Assert.LessOrEqual(text.yMax, button.y, $"{s} — 결과 글자가 「다시」 단추를 덮는다");
            }
        }

        /// <summary>⚠️ 틈은 <see cref="UiLayout.PlateGap"/> 에서 난다 — 따로 적지 않는다.</summary>
        [Test]
        public void 틈이_PlateGap_이다()
        {
            foreach (Vector2 s in Screens)
            {
                float line = LinePx(s.y);
                Rect text = UiLayout.ResultBannerTextRect(s.x, s.y, line);
                Rect button = UiLayout.ResultBannerButtonRect(s.x, s.y, line);

                Assert.AreEqual(UiLayout.PlateGap * UiLayout.Scale(s.y), button.y - text.yMax, 0.01f,
                    $"{s} — 틈이 PlateGap 이 아니다 · 값이 두 곳에 산다");
            }
        }

        /// <summary>**둘 다 상자 안에 든다** — 상자가 글자를 따라 커져야 참이다.</summary>
        [Test]
        public void 글자와_단추가_상자_안에_든다()
        {
            foreach (Vector2 s in Screens)
            {
                float line = LinePx(s.y);
                Rect box = UiLayout.ResultBannerRect(s.x, s.y, line);
                Rect text = UiLayout.ResultBannerTextRect(s.x, s.y, line);
                Rect button = UiLayout.ResultBannerButtonRect(s.x, s.y, line);

                Assert.GreaterOrEqual(text.y, box.y, $"{s} — 글자가 상자 위로 나갔다");
                Assert.LessOrEqual(button.yMax, box.yMax, $"{s} — 단추가 상자 아래로 나갔다");
                Assert.LessOrEqual(button.xMax, box.xMax, $"{s} — 단추가 상자 오른쪽으로 나갔다");
            }
        }

        /// <summary>상자가 화면 안에 드는가.</summary>
        [Test]
        public void 상자가_화면_안에_든다()
        {
            foreach (Vector2 s in Screens)
            {
                Rect box = UiLayout.ResultBannerRect(s.x, s.y, LinePx(s.y));
                Assert.GreaterOrEqual(box.x, 0f, $"{s} — 왼쪽으로 나갔다");
                Assert.LessOrEqual(box.xMax, s.x, $"{s} — 오른쪽으로 나갔다");
                Assert.LessOrEqual(box.yMax, s.y, $"{s} — 아래로 나갔다");
            }
        }

        /// <summary>
        /// **조립 안내 줄의 첫 글자가 띠 밖으로 안 나간다** (사용자 확정 ③).
        ///
        /// ⚠️ 왼쪽 여백이 **글자보다 좁으면** 첫 자의 왼쪽이 잘린다. 바닥은
        /// `여백 ÷ 글자`(24÷32)로 내므로 기준 캔버스에서는 종전 24 그대로다.
        /// </summary>
        [Test]
        public void 안내_줄_왼쪽_여백이_글자보다_좁지_않다()
        {
            foreach (Vector2 s in Screens)
            {
                Rect band = UiLayout.BandRect(UiLayout.Band.Board, s.x, s.y);
                Rect hint = UiLayout.BoardHintRect(s.x, s.y);
                float px = UiLayout.BoardHintFontSize(s.y);

                float gutter = hint.x - band.x;
                Assert.GreaterOrEqual(gutter, px * (UiLayout.BoardHintMargin / UiLayout.BoardHintFontDesign) - 0.01f,
                    $"{s} — 왼쪽 여백이 글자보다 좁다 · 첫 글자가 잘린다");
                Assert.GreaterOrEqual(hint.x, 0f, $"{s} — 안내 줄이 화면 왼쪽 밖이다");
            }
        }

        /// <summary>
        /// **안내 줄이 띠 안에 들고, 양쪽 여백이 같다** (2026-10-13 · 오른쪽이 잘렸다).
        ///
        /// ⚠️ 종전 폭은 `띠 × 0.6` 이라 창이 작아지면 문장이 안 들어갔다 — 글자는
        /// 아래가 막혀 안 줄고 폭만 줄기 때문이다. 왼쪽만 고치면 **가려진 글자를
        /// 잘린 글자로 바꾼** 것이 된다(이 함수 주석이 그것을 금한다).
        /// </summary>
        [Test]
        public void 안내_줄이_띠_안에_들고_양쪽_여백이_같다()
        {
            foreach (Vector2 s in Screens)
            {
                Rect band = UiLayout.BandRect(UiLayout.Band.Board, s.x, s.y);
                Rect hint = UiLayout.BoardHintRect(s.x, s.y);

                float left = hint.x - band.x;
                float right = band.xMax - hint.xMax;

                Assert.LessOrEqual(hint.xMax, band.xMax + 0.01f, $"{s} — 안내 줄이 띠 오른쪽으로 나갔다");
                Assert.AreEqual(left, right, 0.01f, $"{s} — 양쪽 여백이 다르다");
                Assert.Greater(hint.width, band.width * 0.6f, $"{s} — 폭이 종전보다 안 넓어졌다");
            }
        }

        /// <summary>
        /// ⚠️ **여백은 좁아지지 않는다** — 어느 창에서도 종전 `24 × 배율` 밑으로 안 내려간다.
        ///
        /// ⚠️⚠️ 기준 캔버스에서 **24 가 아니라 27** 이다. `KoreanFont.Snap` 이 32 를
        /// 사다리에서 **36 으로 올려** 잡기 때문이다(36 × 0.75 = 27). 처음 적은 주석은
        /// 「종전과 똑같이 24」였고 **이 시험이 그것을 틀렸다고 잡았다** — 그래서 남긴다.
        /// </summary>
        [Test]
        public void 여백이_종전보다_좁아지지_않는다()
        {
            foreach (Vector2 s in Screens)
            {
                Rect band = UiLayout.BandRect(UiLayout.Band.Board, s.x, s.y);
                Rect hint = UiLayout.BoardHintRect(s.x, s.y);
                float before = UiLayout.BoardHintMargin * UiLayout.Scale(s.y);

                Assert.GreaterOrEqual(hint.x - band.x, before - 0.01f,
                    $"{s} — 여백이 종전보다 좁아졌다");
            }

            Rect b0 = UiLayout.BandRect(UiLayout.Band.Board, 1440f, 2560f);
            Rect h0 = UiLayout.BoardHintRect(1440f, 2560f);
            Assert.AreEqual(27f, h0.x - b0.x, 0.01f,
                "기준 캔버스의 여백이 27 이 아니다 — 사다리나 비가 바뀐 것이다");
        }
    }
}
