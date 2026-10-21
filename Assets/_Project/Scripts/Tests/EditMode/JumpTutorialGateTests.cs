using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>#254 — 바로가기 점프 저장(튜토리얼 · 앞 판 통과 기록 없음)에서도 S1 · S2 · S4 막이 선다 · 끝 조건은 그대로.</summary>
    public sealed class JumpTutorialGateTests
    {
        [Test]
        public void S4_MergeForce_StandsWithoutTutorialRecord()
        {
            Assert.IsTrue(MergeTutorialRule.IsActive(false, 4, false, false, true, true), "점프 저장 · S4 도전 · B 해금 · 게이지 준비");
            Assert.IsFalse(MergeTutorialRule.IsActive(false, 4, false, true, true, true), "S4 통과면 안 섬(끝 조건 그대로)");
            Assert.IsFalse(MergeTutorialRule.IsActive(false, 4, true, false, true, true), "파밍 층이면 안 섬");
        }

        [Test]
        public void EndConditions_Unchanged()
        {
            Assert.IsFalse(ChallengeTutorialRule.IsActive(false, true, 1, true), "S1 통과면 T3 끝");
            Assert.IsFalse(ModuleTutorialRule.IsActive(false, false, true, 2, true, false), "S2 통과면 T4 끝");
            Assert.IsFalse(ModuleTutorialRule.IsActive(false, false, false, 2, true, true), "대상 노드에 모듈이면 끝");
            Assert.IsFalse(RobotBGate.TutorialActive(false, 4, true, true, 50d, 50d), "해금했으면 끝");
            Assert.IsFalse(RobotBGate.TutorialActive(false, 4, true, false, 49d, 50d), "골드 모자라면 막 없음");
        }
    }
}
