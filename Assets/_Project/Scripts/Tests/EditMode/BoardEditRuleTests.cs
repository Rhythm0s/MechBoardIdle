using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **도전 층에서는 보드를 못 고친다** (2026-10-14 사용자 확정 ④).
    /// </summary>
    public sealed class BoardEditRuleTests
    {
        [Test]
        public void 파밍에서는_고친다()
        {
            Assert.IsTrue(BoardEditRule.CanEdit(isFarming: true, isTutorialStage: false));
        }

        [Test]
        public void 도전에서는_못_고친다()
        {
            // ⚠️ 이것이 사용자가 신고한 자리다 — 도전 중에 벨트가 깔렸다.
            Assert.IsFalse(BoardEditRule.CanEdit(isFarming: false, isTutorialStage: false));
        }

        [Test]
        public void 튜토리얼_판은_층이_없어_언제나_고친다()
        {
            // ⚠️⚠️ 여기를 막으면 **벨트 한 칸을 까는 수업이 안 돈다** — 튜토리얼이 멈춘다.
            Assert.IsTrue(BoardEditRule.CanEdit(isFarming: false, isTutorialStage: true));
            Assert.IsTrue(BoardEditRule.CanEdit(isFarming: true, isTutorialStage: true));
        }
    }
}
