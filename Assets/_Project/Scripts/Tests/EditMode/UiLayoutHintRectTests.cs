using MBI.UI;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **가려졌다는 말은 사각 둘이 겹쳤다는 말이다** (2026-09-21 사용자 육안 ⓐ).
    ///
    /// 조립 안내 줄이 「절반 가려진다」고 왔다. 자리가 그리는 쪽에 흩어져 있어서
    /// **눈으로만** 볼 수 있었는데, 사각을 <see cref="UiLayout.BoardHintRect"/> 로 빼고
    /// 나니 겹치는지는 **셈으로 답이 난다.**
    ///
    /// 📌 여기서 지키는 것은 **안 겹친다** 하나다. 「읽기 좋은 자리인가」는 사람이 본다 —
    /// 시험이 대신 볼 수 있는 것과 없는 것을 섞지 않는다.
    /// </summary>
    public sealed class UiLayoutHintRectTests
    {
        /// <summary>실제로 쓰는 창들 — 기준비 · 넓은 창 · 좁은 창 · 낮은 창.</summary>
        private static readonly Vector2[] Windows =
        {
            new Vector2(1440f, 2560f),
            new Vector2(1920f, 1080f),
            new Vector2(1280f, 720f),
            new Vector2(615f, 1085f),
        };

        [Test]
        public void 모드_막대와_안_겹친다()
        {
            foreach (Vector2 w in Windows)
            {
                Rect hint = UiLayout.BoardHintRect(w.x, w.y);
                Rect mode = UiLayout.ModeBarRect(w.x, w.y);
                Assert.IsFalse(hint.Overlaps(mode),
                    $"{w.x}x{w.y} — 안내 줄 {hint} 가 모드 막대 {mode} 와 겹친다");
            }
        }

        [Test]
        public void 부유_띠를_안_침범한다()
        {
            foreach (Vector2 w in Windows)
            {
                Rect hint = UiLayout.BoardHintRect(w.x, w.y);
                Rect band = UiLayout.BandRect(UiLayout.Band.FloatBand, w.x, w.y);
                Assert.LessOrEqual(hint.yMax, band.y + 0.01f,
                    $"{w.x}x{w.y} — 안내 줄이 부유 띠로 내려갔다 ({hint.yMax} > {band.y})");
            }
        }

        [Test]
        public void 보드_띠_안에_있다()
        {
            foreach (Vector2 w in Windows)
            {
                Rect hint = UiLayout.BoardHintRect(w.x, w.y);
                Rect board = UiLayout.BandRect(UiLayout.Band.Board, w.x, w.y);
                Assert.GreaterOrEqual(hint.y, board.y - 0.01f,
                    $"{w.x}x{w.y} — 안내 줄이 보드 띠 위로 올라갔다(전투 인셋 자리다)");
            }
        }

        /// <summary>
        /// **글자를 올려 잡았으면 상자도 따라 커져야 한다** (2026-09-21 사용자 육안 3차 ②).
        ///
        /// `KoreanFont.Snap` 은 사다리에서 **위로** 올려 잡는데 높이는 `44 × s` 로 박혀
        /// 있었다 — 720 창에서 상자 **12.4** 에 글자 **16** 이었다. 기준 창에서만 맞아서
        /// 눈으로는 한참 못 봤다.
        /// </summary>
        [Test]
        public void 상자가_글자보다_낮지_않다()
        {
            foreach (Vector2 w in Windows)
            {
                Rect hint = UiLayout.BoardHintRect(w.x, w.y);
                int font = UiLayout.BoardHintFontSize(w.y);
                Assert.GreaterOrEqual(hint.height, font * 1.4f,
                    $"{w.x}x{w.y} — 상자 {hint.height:F1} 에 글자 {font} 다(아래가 잘린다)");
            }
        }

        /// <summary>
        /// ⚠️ **폭은 안 줄인다.** 모드 막대 왼끝까지로 좁히면 겹침은 없어지지만
        /// 문장이 잘린다 — 가려진 글자를 잘린 글자로 바꾸는 것은 고친 것이 아니다.
        /// </summary>
        [Test]
        public void 문장이_들_만큼_넓다()
        {
            Rect hint = UiLayout.BoardHintRect(1440f, 2560f);
            Assert.GreaterOrEqual(hint.width, 800f,
                $"안내 줄 폭이 {hint.width} 다 — 32px 글자로 그 문장이 안 든다");
        }
    }
}
