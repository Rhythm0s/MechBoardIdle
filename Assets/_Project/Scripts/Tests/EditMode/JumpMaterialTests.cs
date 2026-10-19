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
            Assert.AreEqual("강화 Lv 1 · ×1.00", MountEnhanceRule.LevelLine(false, 1.45f));
            Assert.AreEqual("강화 Lv 2 · ×1.45", MountEnhanceRule.LevelLine(true, 1.45f));
            Assert.AreEqual("강화 (재료 50 · 고철 300)", MountEnhanceRule.ButtonLabelWithCost(false, 50d, 300d));
            Assert.AreEqual("강화 완료", MountEnhanceRule.ButtonLabelWithCost(true, 50d, 300d));
        }

        [Test]
        public void 창_단추_자리와_요구치_0_목표_줄()
        {
            string sr = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            int cancel = sr.IndexOf("if (UiSkin.Button(new Rect(pad, y, bw, bh), ChallengeEntryNotice.Cancel, btn))");
            Assert.Greater(cancel, 0, "#200 — 왼쪽 칸 = 취소");
            string hud = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.Hud.cs");
            StringAssert.Contains(": HasRequirement && stage.req > 0f", hud);
        }
    }
}
