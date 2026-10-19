using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>#209 옛 저장 자동 복구 — 불러온 A 판 · 튜토리얼 마침 · 운반로 칸 빔이면 채운다.</summary>
    public sealed class SaveRepairRuleTests
    {
        [Test]
        public void BrokenA_AfterTutorial_Fills() =>
            Assert.IsTrue(SaveRepairRule.ShouldFillAfterRestore(MountOwner.RobotA, true, true));

        [Test]
        public void TutorialNotCleared_LeavesGap() =>
            Assert.IsFalse(SaveRepairRule.ShouldFillAfterRestore(MountOwner.RobotA, false, true), "튜토리얼 중엔 그 칸이 할 일");

        [Test]
        public void RobotB_NeverFilled() =>
            Assert.IsFalse(SaveRepairRule.ShouldFillAfterRestore(MountOwner.RobotB, true, true));

        [Test]
        public void CellAlreadyFilled_NoOp() =>
            Assert.IsFalse(SaveRepairRule.ShouldFillAfterRestore(MountOwner.RobotA, true, false));
    }
}
