using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>#198 점프 재료 · #199 팝오버 레벨/단추 · #200 창 단추 자리 · #201 요구치 0 목표 줄 (2026-10-19).</summary>
    public sealed class JumpMaterialTests
    {
        [Test]
        public void 점프_재료는_그_값까지만()
        {
            Assert.AreEqual(63d, MountEnhanceRule.JumpMaterialTarget(63d, 50d, false, false), "S3 = 63");
            Assert.AreEqual(50d, MountEnhanceRule.JumpMaterialTarget(100d, 50d, true, false), "S4·S5·S6 = A 자동 몫 뺀 50");
            Assert.AreEqual(0d, MountEnhanceRule.JumpMaterialTarget(100d, 50d, true, true));
            Assert.AreEqual(0d, MountEnhanceRule.JumpMaterialTarget(30d, 50d, true, true), "음수 없음");
            Assert.IsFalse(MountEnhanceRule.JumpTurnsOn(3));
            Assert.IsTrue(MountEnhanceRule.JumpTurnsOn(4));

            string rs = System.IO.File.ReadAllText("Assets/_Project/Scripts/Idle/IdleRuntime.cs");
            StringAssert.Contains("if (Wallet.EnhMaterial < topMat) Wallet.AddEnhMaterial(topMat - Wallet.EnhMaterial);", rs);
        }

        [Test]
        public void 팝오버_레벨_줄과_단추()
        {
            Assert.AreEqual("Lv 1 · 피해량 ×1.00", MountEnhanceRule.LevelLine(false, 1.45f));   // #202
            Assert.AreEqual("Lv 2 · 피해량 ×1.45", MountEnhanceRule.LevelLine(true, 1.45f));
            Assert.AreEqual("Lv 1 · 출격 +0", MountEnhanceRule.LevelLineB(false, 2f));   // #234
            Assert.AreEqual("Lv 2 · 출격 +2", MountEnhanceRule.LevelLineB(true, 2f));
            Assert.AreEqual("강화 (재료 50 · 고철 300)", MountEnhanceRule.ButtonLabelWithCost(false, 50d, 300d));
            Assert.AreEqual("강화 완료", MountEnhanceRule.ButtonLabelWithCost(true, 50d, 300d));
        }

        [Test]
        public void 튜토리얼_막_동안_전투_정지()   // #203
        {
            Assert.IsTrue(TutorialPauseRule.ShouldPause(true, false, false));
            Assert.IsTrue(TutorialPauseRule.ShouldPause(false, true, false));
            Assert.IsTrue(TutorialPauseRule.ShouldPause(false, false, true));
            Assert.IsFalse(TutorialPauseRule.ShouldPause(false, false, false));
            string sr = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            StringAssert.Contains("goto TutorialSignalsUpdate;", sr);
            Assert.Less(sr.IndexOf("goto TutorialSignalsUpdate;"), sr.IndexOf("_sim.Tick(Time.deltaTime);"));
            Assert.Less(sr.IndexOf("TutorialSignalsUpdate:"), sr.IndexOf("TutorialSignals.HighlightChallengeButton = ChallengeTutorialRule.IsActive("));
        }

        [Test]
        public void 창_단추_자리와_요구치_0_목표_줄()
        {
            string sr = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            int cancel = sr.IndexOf("if (UiSkin.Button(new Rect(pad, y, bw, bh), ChallengeEntryNotice.Cancel, btn))");
            Assert.Greater(cancel, 0, "#200 — 왼쪽 칸 = 취소");
            string hud = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.Hud.cs");
            // #218 — 요구치 줄을 모든 판에서 뺐다(🗑️ #201 「요구치 0 판만」 갈래)
            StringAssert.DoesNotContain(": HasRequirement && stage.req > 0f", hud);
        }
    }
}
