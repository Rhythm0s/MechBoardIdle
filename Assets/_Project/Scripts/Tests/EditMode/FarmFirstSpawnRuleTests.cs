using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>#206 첫 스폰 띠 ×1.3 · #207 막 동안 스폰 보류(2026-10-19 사용자 확정).</summary>
    public sealed class FarmFirstSpawnRuleTests
    {
        [Test]
        public void FirstBand_ScalesBothEdgesBy1_3()
        {
            (float min, float max) = FarmFirstSpawnRule.FirstBand(10f, 20f);
            Assert.AreEqual(13f, min, 1e-4f);
            Assert.AreEqual(26f, max, 1e-4f);
        }

        [Test]
        public void Paused_HoldsFirstSpawn() =>
            Assert.IsFalse(FarmFirstSpawnRule.ShouldSpawnFirst(true, true, 99), "막(정지) 동안은 안 낸다");

        [Test]
        public void WaitsSettleFrames_ThenSpawnsOnce()
        {
            Assert.IsFalse(FarmFirstSpawnRule.ShouldSpawnFirst(true, false, 1), "판이 선 첫 프레임 = 막 신호가 아직 앞 프레임 값");
            Assert.IsTrue(FarmFirstSpawnRule.ShouldSpawnFirst(true, false, FarmFirstSpawnRule.SettleFrames));
            Assert.IsFalse(FarmFirstSpawnRule.ShouldSpawnFirst(false, false, 99), "이미 냈으면 다시 안 낸다(리필은 지금 띠)");
        }

        [Test]
        public void PauseRule_NoPhase_Runs() =>
            Assert.IsFalse(TutorialPauseRule.ShouldPause(false, false, false, false, false, false, false));

        // #207 확장(23:4x 규칙 통일) — 국면 하나하나가 홀로 정지를 세운다.
        [TestCase(true, false, false, false, false, false, false, TestName = "Pause_S1_T3")]
        [TestCase(false, true, false, false, false, false, false, TestName = "Pause_S2_T4")]
        [TestCase(false, false, true, false, false, false, false, TestName = "Pause_S4_Unlock")]
        [TestCase(false, false, false, true, false, false, false, TestName = "Pause_S3_Enhance")]
        [TestCase(false, false, false, false, true, false, false, TestName = "Pause_S5_Explosive")]
        [TestCase(false, false, false, false, false, true, false, TestName = "Pause_S6_EnhanceB")]
        [TestCase(false, false, false, false, false, false, true, TestName = "Pause_S4_MergePrompt_Challenge")]
        public void PauseRule_EachPhasePauses(bool t3, bool t4, bool unlock, bool enhA, bool expl, bool enhB, bool merge) =>
            Assert.IsTrue(TutorialPauseRule.ShouldPause(t3, t4, unlock, enhA, expl, enhB, merge));

        [Test]
        public void PauseRule_OldThreeArg_StillMatches()
        {
            Assert.IsTrue(TutorialPauseRule.ShouldPause(false, true, false));
            Assert.IsFalse(TutorialPauseRule.ShouldPause(false, false, false));
        }
    }
}
