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
        public void 도전_층_이동_모드_탭은_고르기이고_팝오버_편집_행은_흐리다()
        {
            // #148 — 화면 배선을 코드가 스스로 말하게 한다(ModuleDetachWiredTests 와 같은 꼴).
            string src = System.IO.File.ReadAllText("Assets/_Project/Scripts/Logistics/BoardController.cs");
            StringAssert.Contains("if (_challengePanForced && _grid != null", src, "도전 층 이동 모드 탭 → 고르기 갈래가 없다");
            StringAssert.Contains("GUI.enabled = r.IsRunnable && editOk;", src, "조합표 후보가 도전 층에서 안 흐리다");
            StringAssert.Contains("GUI.enabled = wasDel && editOk;", src, "「떼기」가 도전 층에서 안 흐리다");
            StringAssert.Contains("GUI.enabled = wasEdit && editOk;", src, "「90도 돌리기」가 도전 층에서 안 흐리다");
            StringAssert.Contains("// #148 덧 — 도전 중 「보기」로 열어 둔 팝오버는 도전과 함께 닫는다", src,
                                  "도전 층을 나올 때 팝오버를 안 닫는다(T4 안내 줄을 가렸다)");
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
