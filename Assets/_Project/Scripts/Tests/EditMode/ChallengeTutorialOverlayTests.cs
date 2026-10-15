using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **T3 어둠막·문구는 국면과 함께 켜지고 꺼진다** (2026-10-14 사용자 확정 #130).
    /// </summary>
    public sealed class ChallengeTutorialOverlayTests
    {
        [Test]
        public void T3_가_서면_뜬다()
        {
            Assert.IsTrue(ChallengeTutorialOverlayRule.ShouldShow(
                t3Active: true, boardViewOpen: false, settingsOpen: false, menuOpen: false));
        }

        [Test]
        public void T3_가_꺼지면_같이_사라진다()
        {
            // ⚠️ 끝 조건은 T3 그대로다 — S1 을 통과해 T3 가 꺼지면 막도 문구도 없다.
            Assert.IsFalse(ChallengeTutorialOverlayRule.ShouldShow(
                t3Active: false, boardViewOpen: false, settingsOpen: false, menuOpen: false));
        }

        [Test]
        public void 끝_조건을_실제_규칙으로_따라간다()
        {
            // 화면이 읽는 길 그대로 — T3 규칙 → 막. S1 을 깨는 순간 막이 내려가야 한다.
            bool before = ChallengeTutorialRule.IsActive(
                tutorialCleared: true, stage1Cleared: false, stageNumber: 1, isFarming: true);
            bool after = ChallengeTutorialRule.IsActive(
                tutorialCleared: true, stage1Cleared: true, stageNumber: 1, isFarming: true);

            Assert.IsTrue(ChallengeTutorialOverlayRule.ShouldShow(before, false, false, false),
                "S1 미통과 · S1 파밍 — 막이 서야 한다");
            Assert.IsFalse(ChallengeTutorialOverlayRule.ShouldShow(after, false, false, false),
                "S1 통과 뒤에도 막이 남으면 「잠금은 풀렸는데 화면은 어둡다」가 된다");
        }

        [Test]
        public void 조립_화면에서는_안_깐다()
        {
            // 「도전」 단추가 없는 화면이라 남길 구멍이 없다 — 깔면 「▲전투로」까지 묻힌다.
            Assert.IsFalse(ChallengeTutorialOverlayRule.ShouldShow(
                t3Active: true, boardViewOpen: true, settingsOpen: false, menuOpen: false));
        }

        [Test]
        public void 설정과_메뉴_위에는_안_덮는다()
        {
            Assert.IsFalse(ChallengeTutorialOverlayRule.ShouldShow(true, false, true, false),
                "설정 판이 흐려지면 못 읽는다");
            Assert.IsFalse(ChallengeTutorialOverlayRule.ShouldShow(true, false, false, true),
                "메뉴는 제 배경을 깐다 — 두 겹이 된다");
        }

        [Test]
        public void 문구는_사용자_확정_글자_그대로다()
        {
            Assert.AreEqual("도전 버튼을 눌러 도전해보세요", ChallengeTutorialOverlayRule.Prompt);
        }
    }
}
