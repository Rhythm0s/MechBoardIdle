using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>#193 통과한 판 도전 불가 · #194 최초 통과 보상 한 줄 (2026-10-19 사용자 확정).</summary>
    public sealed class ClearedStageRuleTests
    {
        [Test]
        public void 통과한_판은_통과함_흐림_승리면_다시_도전_숨김()
        {
            Assert.IsFalse(ClearedStageRule.CanChallenge(true));
            Assert.IsTrue(ClearedStageRule.CanChallenge(false));
            Assert.AreEqual("통과함", ClearedStageRule.ButtonLabel(true));
            Assert.AreEqual("도전", ClearedStageRule.ButtonLabel(false));
            Assert.IsFalse(ClearedStageRule.ShowsRetry(true), "승리 = [다시 도전] 숨김");
            Assert.IsTrue(ClearedStageRule.ShowsRetry(false), "실패 = 그대로");
        }

        [Test]
        public void 최초_통과_보상_한_줄()
        {
            Assert.AreEqual("강화 재료 +30", ClearedStageRule.RewardLine(true, 30f));
            Assert.IsNull(ClearedStageRule.RewardLine(false, 30f), "재통과 = 재지급 없음 · 안 씀");
            Assert.IsNull(ClearedStageRule.RewardLine(true, 0f), "0 이면 안 씀");

            string sr = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            StringAssert.Contains("if (ClearedStageRule.ShowsRetry(won)) labels.Add(\"다시 도전\");", sr);
            StringAssert.Contains("&& ClearedStageRule.CanChallenge(cleared);", sr);
            StringAssert.Contains("_clearRewardLine = ClearedStageRule.RewardLine(", sr);
        }

        [Test]
        public void S6_파밍은_기본_곡()   // #192
        {
            string sr = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            StringAssert.Contains("AudioSignals.Phase = MusicPhaseRule.Of(stage.reqType, StageLayerSignals.IsFarming);", sr);
        }
    }
}
