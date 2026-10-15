using MBI.UI;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **HUD 「도전」·「스테이지」 칸과 스테이지 목록 줄이 글자를 따라간다** (2026-10-15 사용자 확정 #142 a·c).
    /// </summary>
    public sealed class HudButtonFitTests
    {
        private const float Sw = 576f, Sh = 1024f;

        [Test]
        public void 글자가_넓으면_도전_칸이_넓어지고_오른끝_아랫변은_그대로다()
        {
            Rect design = UiLayout.ChallengeButtonRect(Sw, Sh);
            Rect grown = UiLayout.ChallengeButtonRect(Sw, Sh, design.width + 20f, design.height + 6f);
            Assert.AreEqual(design.width + 20f, grown.width, 0.001f);
            Assert.AreEqual(design.height + 6f, grown.height, 0.001f);
            Assert.AreEqual(design.xMax, grown.xMax, 0.001f);
            Assert.AreEqual(design.yMax, grown.yMax, 0.001f);
        }

        [Test]
        public void 글자가_좁으면_설계_칸_그대로다()
        {
            Rect design = UiLayout.ChallengeButtonRect(Sw, Sh);
            Rect same = UiLayout.ChallengeButtonRect(Sw, Sh, 1f, 1f);
            Assert.AreEqual(design, same);
        }

        [Test]
        public void 스테이지_단추는_도전과_같은_크기로_왼쪽에_선다()
        {
            Rect chal = UiLayout.ChallengeButtonRect(Sw, Sh, 110f, 40f);
            Rect list = UiLayout.StageListButtonRect(Sw, Sh, 110f, 40f);
            Assert.AreEqual(chal.width, list.width, 0.001f);
            Assert.AreEqual(chal.height, list.height, 0.001f);
            Assert.Less(list.xMax, chal.x + 0.001f);
        }

        [Test]
        public void 두_줄_줄이면_목록_줄_높이가_글자를_따라_커진다()
        {
            Rect one = UiLayout.StageListSlotRect(Sw, Sh, 6, 1);
            Rect two = UiLayout.StageListSlotRect(Sw, Sh, 6, 1, one.height + 10f);
            Assert.AreEqual(one.height + 10f, two.height, 0.001f);
            // 패널도 같은 값으로 커진다 — 닫기 칸이 패널 밖으로 안 나간다.
            Rect panel = UiLayout.StageListPanelRect(Sw, Sh, 6, one.height + 10f);
            Rect close = UiLayout.StageListSlotRect(Sw, Sh, 6, 7, one.height + 10f);
            Assert.LessOrEqual(close.yMax, panel.yMax + 0.001f);
        }
    }
}
