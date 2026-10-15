using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **도전 중에는 모드 단추가 「도전 중 변경 불가」로 잠긴다 · 태그 원형 셋은 로봇 B 해금 뒤에만**
    /// (2026-10-15 사용자 확정 #143 · #144).
    /// </summary>
    public sealed class ChallengeModeLockTests
    {
        [Test]
        public void 도전_층에서는_모드를_못_바꾼다()
        {
            Assert.IsFalse(BoardEditRule.CanToggleMode(isFarming: false, isTutorialStage: false));
        }

        [Test]
        public void 파밍_층과_튜토리얼_판은_그대로_바꾼다()
        {
            Assert.IsTrue(BoardEditRule.CanToggleMode(isFarming: true, isTutorialStage: false));
            Assert.IsTrue(BoardEditRule.CanToggleMode(isFarming: false, isTutorialStage: true));
        }

        [Test]
        public void 문구는_사용자_원문_그대로다()
        {
            Assert.AreEqual("도전 중 변경 불가", BoardEditRule.ChallengeModeLockedLabel);
        }

        [Test]
        public void 태그_원형은_S4_통과_전_S4_이하에서는_안_보인다()
        {
            // 원형 셋을 그리는 문은 RobotBGate.IsUnlocked 하나다(#144 · #103).
            Assert.IsFalse(RobotBGate.Unlocked(false, 1));
            Assert.IsFalse(RobotBGate.Unlocked(false, 4));
            Assert.IsTrue(RobotBGate.Unlocked(true, 1));
            Assert.IsTrue(RobotBGate.Unlocked(false, 5), "심사자 S5·S6 점프는 해금(#103)이라 보인다");
        }
    }
}
