using MBI.Core;
using MBI.Core.Combat;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **합체 게이지를 판 너머로 이어 간다** (사용자 확정 #155 · 2026-10-16).
    /// </summary>
    public sealed class MergeCarryTests
    {
        [Test]
        public void 이어_받은_게이지는_만충까지로_자른다()
        {
            var m = new MergeSystem();
            m.SetCharge(45f);
            Assert.AreEqual(45f, m.ChargeSeconds, 1e-4f);
            m.SetCharge(500f);
            Assert.AreEqual(MergeSystem.GaugeFullSeconds, m.ChargeSeconds, 1e-4f);
            Assert.IsTrue(m.IsReady);
            m.SetCharge(-3f);
            Assert.AreEqual(0f, m.ChargeSeconds, 1e-4f);
        }

        [Test]
        public void 쓴_판에서는_이어_받지_않는다()
        {
            var m = new MergeSystem();
            m.SetCharge(MergeSystem.GaugeFullSeconds);
            Assert.IsTrue(m.TryActivate());
            m.SetCharge(60f);
            Assert.AreEqual(0f, m.ChargeSeconds, 1e-4f, "합체 중·사용 뒤에는 그대로");
        }

        [Test]
        public void 안내_창_게이지_줄은_내림_퍼센트다()
        {
            Assert.AreEqual("합체 게이지 50%", ChallengeEntryNotice.LineMerge(0.5f));
            Assert.AreEqual("합체 게이지 99%", ChallengeEntryNotice.LineMerge(0.999f));
            Assert.AreEqual("합체 게이지 100%", ChallengeEntryNotice.LineMerge(1.3f));
        }

        [Test]
        public void 저장_칸은_더하기만_하고_옛_저장은_0이다()
        {
            Assert.AreEqual(0f, new SaveDataV1().mergeChargeSeconds);
            var old = JsonUtility.FromJson<SaveDataV1>("{\"schemaVersion\":1}");
            Assert.AreEqual(0f, old.mergeChargeSeconds);
            var on = new SaveDataV1 { mergeChargeSeconds = 42f };
            Assert.AreEqual(42f, JsonUtility.FromJson<SaveDataV1>(JsonUtility.ToJson(on)).mergeChargeSeconds, 1e-4f);
        }

        [Test]
        public void 러너는_판이_설_때_게이지를_넣고_매_프레임_게시한다()
        {
            string src = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            StringAssert.Contains("_sim.Merge.SetCharge(IdleSignals.MergeChargeSeconds);", src);
            StringAssert.Contains("IdleSignals.MergeChargeSeconds = (m.UsedThisStage || m.IsActive) ? 0f : m.ChargeSeconds;", src);
            StringAssert.Contains("bool t3 = TutorialSignals.HighlightChallengeButton;", src, "T3 안내 창 갈래가 없다");
        }
    }
}
