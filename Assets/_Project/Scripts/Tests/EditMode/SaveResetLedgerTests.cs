using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **「처음부터」 뒤에는 통과 장부도 비어야 한다** (2026-10-15 · #141).
    /// 저장만 지우고 메모리 장부를 남겨 T3 가 안 뜨고 S2 가 열려 있던 결함.
    /// </summary>
    public sealed class SaveResetLedgerTests
    {
        [TearDown]
        public void Clean()
        {
            IdleSignals.PublishClearedStages(null);
            IdleSignals.TutorialCleared = false;
        }

        [Test]
        public void S1_통과_저장을_지우면_장부가_빈다()
        {
            IdleSignals.PublishClearedStages(new[] { "S1" });
            IdleSignals.TutorialCleared = true;

            IdleSignals.RepublishAfterReset(new SaveDataV1());

            Assert.AreEqual(0, IdleSignals.ClearedStageIds.Count);
            Assert.IsFalse(IdleSignals.TutorialCleared);
        }

        [Test]
        public void 저장이_없어도_장부는_빈다()
        {
            IdleSignals.PublishClearedStages(new[] { "S1", "S2" });

            IdleSignals.RepublishAfterReset(null);

            Assert.AreEqual(0, IdleSignals.ClearedStageIds.Count);
        }
    }
}
