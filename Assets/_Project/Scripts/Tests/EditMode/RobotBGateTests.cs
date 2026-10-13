using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **로봇 B 는 S4 를 넘겨야 열린다** (2026-10-13 사용자 확정 · §74-3 #84).
    ///
    /// ⚠️ 이 답을 묻는 곳이 셋이다 — 조립 화면 로봇 탭 · 전투 화면 태그 버튼 · 자동 교대.
    /// 셋이 각자 세면 한 곳만 고쳐지는 날이 온다(지침 §7). 여기가 그 하나를 지킨다.
    /// </summary>
    public sealed class RobotBGateTests
    {
        [TearDown]
        public void Clean() => RobotBGate.Reset();

        [Test]
        public void 처음에는_잠겨_있다()
        {
            RobotBGate.Reset();
            Assert.IsFalse(RobotBGate.IsUnlocked, "처음부터 B 가 열려 있다");
        }

        [Test]
        public void S4_를_깨면_열린다()
        {
            Assert.IsTrue(RobotBGate.Unlocked(s4Cleared: true, currentStageNumber: 0),
                "S4 를 깼는데 안 열린다");
        }

        /// <summary>⚠️ 튜토리얼 ~ S4 **동안에는** 잠겨 있다 — 깨는 중은 깬 것이 아니다.</summary>
        [Test]
        public void S4_까지는_잠겨_있다()
        {
            for (int n = 0; n <= 4; n++)
                Assert.IsFalse(RobotBGate.Unlocked(s4Cleared: false, currentStageNumber: n),
                    $"S{n} 에서 B 가 열려 있다");
        }

        /// <summary>✅ **심사자가 S5 로 뛰면 열린 것으로 본다** — 사용자 확정 2026-10-13(#103).</summary>
        [Test]
        public void S5_위로_뛰면_열린_것으로_본다()
        {
            Assert.IsTrue(RobotBGate.Unlocked(s4Cleared: false, currentStageNumber: 5),
                "S5 로 뛰었는데 로봇이 하나뿐이다");
            Assert.IsTrue(RobotBGate.Unlocked(s4Cleared: false, currentStageNumber: 6));
        }

        /// <summary>
        /// ⚠️ **여는 판을 글자 하나가 정한다** — 숫자 4 를 다른 곳에 안 적는다.
        /// 여는 판이 바뀌면 이 시험이 따라 움직여야 한다.
        /// </summary>
        [Test]
        public void 여는_판은_UnlockStageId_가_정한다()
        {
            int gate = StageMapName.NumberOf(RobotBGate.UnlockStageId);
            Assert.Greater(gate, 0, "여는 판이 여섯에 안 드는 글자다");

            Assert.IsFalse(RobotBGate.Unlocked(false, gate), "여는 판 그 자리에서 이미 열렸다");
            Assert.IsTrue(RobotBGate.Unlocked(false, gate + 1), "여는 판 다음에서 안 열린다");
        }

        /// <summary>처음부터(저장 초기화)면 다시 잠긴다.</summary>
        [Test]
        public void 초기화하면_다시_잠긴다()
        {
            RobotBGate.S4Cleared = true;
            RobotBGate.CurrentStageNumber = 6;
            Assert.IsTrue(RobotBGate.IsUnlocked);

            RobotBGate.Reset();

            Assert.IsFalse(RobotBGate.IsUnlocked, "초기화했는데 B 가 열린 채다");
        }
    }
}
