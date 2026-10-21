using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>#189 S4 합체 강제 버튼 (2026-10-19 사용자 확정).</summary>
    public sealed class MergeTutorialRuleTests
    {
        [SetUp] public void SetUp() => TutorialSignals.Reset();
        [TearDown] public void TearDown() => TutorialSignals.Reset();

        private static bool Active(bool cleared = true, int stage = 4, bool farming = false, bool s4Cleared = false,
                                   bool unlocked = true, bool ready = true) =>
            MergeTutorialRule.IsActive(cleared, stage, farming, s4Cleared, unlocked, ready);

        [Test]
        public void S4_도전_첫판_만충일_때만()
        {
            Assert.IsTrue(Active());
            Assert.IsFalse(Active(farming: true), "파밍 층 아님");
            Assert.IsFalse(Active(s4Cleared: true), "통과한 뒤에는 안 뜸(한 번)");
            Assert.IsFalse(Active(unlocked: false));
            Assert.IsFalse(Active(ready: false), "만충 전 · 합체 중 · 사용 뒤 = 꺼짐");
            Assert.IsFalse(Active(stage: 5));
            Assert.IsTrue(Active(cleared: false), "#254 — 튜토리얼 통과 기록 없는 점프 저장에도 섬(🗑️ IsFalse)");
            Assert.AreEqual("합체 버튼을 눌러 합체해보세요", MergeTutorialRule.Prompt);
        }

        [Test]
        public void 합체_국면은_합체와_전투로만()
        {
            var p = TutorialGate.Phase.Merge;
            Assert.IsTrue(TutorialGate.Allows(p, TutorialGate.Control.Merge));
            Assert.IsTrue(TutorialGate.Allows(p, TutorialGate.Control.ExitBoard));
            Assert.IsFalse(TutorialGate.Allows(p, TutorialGate.Control.TagButton));
            Assert.IsFalse(TutorialGate.Allows(p, TutorialGate.Control.EnterBoard));
            Assert.IsFalse(TutorialGate.Allows(p, TutorialGate.Control.StageList));
            Assert.IsTrue(TutorialGate.Allows(TutorialGate.Phase.None, TutorialGate.Control.TagButton));

            TutorialSignals.HighlightMergeButton = true;
            Assert.AreEqual(TutorialGate.Phase.Merge, TutorialGate.Current);

            string sr = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            StringAssert.Contains("TutorialSignals.HighlightMergeButton = MergeTutorialRule.IsActive(", sr);
            StringAssert.Contains("ChallengeTutorialOverlay.DrawMerge(TutorialSignals.HighlightMergeButton);", sr);
            StringAssert.Contains("&& TutorialGate.Allows(TutorialGate.Control.Merge);", sr);
        }
    }
}
