using MBI.UI;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **노드 팝오버는 화면 안에 다 들어온다** (2026-10-15 사용자 육안 #139 · 8127).
    /// 왼쪽 가장자리 노드에서 「90도 돌리기」→「00도 돌리기」·「상태」→「태」로 잘리던 자리.
    /// </summary>
    public sealed class RecipePopoverFitTests
    {
        private const float Sw = 576f, Sh = 1024f, Margin = 8f;

        [Test]
        public void 왼쪽_가장자리_노드에서도_왼변에_여백이_남는다()
        {
            Rect r = UiLayout.RecipePopoverRect(new Vector2(4f, 500f), 40f, 400f, 300f, Sw, Sh, Margin);
            Assert.GreaterOrEqual(r.x, Margin);
            Assert.LessOrEqual(r.xMax, Sw - Margin + 0.001f);
        }

        [Test]
        public void 오른쪽_가장자리_노드에서도_오른변에_여백이_남는다()
        {
            Rect r = UiLayout.RecipePopoverRect(new Vector2(Sw - 4f, 500f), 40f, 400f, 300f, Sw, Sh, Margin);
            Assert.GreaterOrEqual(r.x, Margin);
            Assert.LessOrEqual(r.xMax, Sw - Margin + 0.001f);
        }

        [Test]
        public void 글자가_화면보다_넓으면_폭은_화면_안까지만이다()
        {
            // 그 안에서 그리는 쪽이 줄을 바꾼다 — 판이 화면 밖으로 나가면 글자가 잘린다.
            Rect r = UiLayout.RecipePopoverRect(new Vector2(300f, 500f), 40f, 2000f, 300f, Sw, Sh, Margin);
            Assert.AreEqual(Sw - Margin * 2f, r.width, 0.001f);
            Assert.AreEqual(Margin, r.x, 0.001f);
        }

        [Test]
        public void 아래_가장자리_노드에서도_아래로_안_나간다()
        {
            Rect r = UiLayout.RecipePopoverRect(new Vector2(300f, Sh - 2f), 40f, 300f, 200f, Sw, Sh, Margin);
            Assert.LessOrEqual(r.yMax, Sh - Margin + 0.001f);
            Assert.GreaterOrEqual(r.y, Margin);
        }

        [Test]
        public void 화면보다_좁은_요청은_그대로다()
        {
            Assert.AreEqual(300f, UiLayout.RecipePopoverFitWidth(300f, Sw, Margin), 0.001f);
        }
    }
}
