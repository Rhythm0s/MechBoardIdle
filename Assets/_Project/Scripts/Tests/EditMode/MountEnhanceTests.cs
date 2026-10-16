using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **마운트 강화 최소판** (2026-10-16 사용자 확정 #150).
    /// </summary>
    public sealed class MountEnhanceTests
    {
        [Test]
        public void 계수는_표식이_정한다()
        {
            Assert.AreEqual(1f, MountEnhanceRule.MountCoef(false, 1f, 1.45f), 1e-6f);
            Assert.AreEqual(1.45f, MountEnhanceRule.MountCoef(true, 1f, 1.45f), 1e-6f);
        }

        [Test]
        public void 재료가_비용_이상이고_아직_안_켰을_때만_눌린다()
        {
            Assert.IsFalse(MountEnhanceRule.CanEnhance(false, 99d, 100d));
            Assert.IsTrue(MountEnhanceRule.CanEnhance(false, 100d, 100d));
            Assert.IsFalse(MountEnhanceRule.CanEnhance(true, 500d, 100d), "되돌리기·두 번 쓰기 없음");
            Assert.IsFalse(MountEnhanceRule.CanEnhance(false, 100d, 0d), "비용이 없으면 안 눌린다");
        }

        [Test]
        public void 문구는_가정_글자_그대로다()
        {
            Assert.AreEqual("강화 (재료 전부 투입)", MountEnhanceRule.ButtonLabel);
            Assert.AreEqual("강화 ×1.45 적용", MountEnhanceRule.AppliedLabel(1.45f));
        }

        [Test]
        public void 옛_저장과_새_저장은_강화_안_함으로_선다()
        {
            Assert.IsFalse(new SaveDataV1().mountEnhanced);
            var old = JsonUtility.FromJson<SaveDataV1>("{\"schemaVersion\":1,\"enhMaterial\":100}");
            Assert.IsFalse(old.mountEnhanced);
            var on = new SaveDataV1 { mountEnhanced = true };
            Assert.IsTrue(JsonUtility.FromJson<SaveDataV1>(JsonUtility.ToJson(on)).mountEnhanced);
        }

        [Test]
        public void 강화_요청은_한_번_가져가면_빈다()
        {
            IdleSignals.Reset();
            IdleSignals.RequestMountEnhance(100d);
            Assert.IsTrue(IdleSignals.TryDrainMountEnhance(out double cost));
            Assert.AreEqual(100d, cost);
            Assert.IsFalse(IdleSignals.TryDrainMountEnhance(out _));
            IdleSignals.Reset();
        }

        [Test]
        public void 러너와_하네스는_powerModel_로_계수를_안_정한다()
        {
            // 🗑️ 구 「S4 부터 저절로 ×1.45」 — 화면 배선을 코드가 스스로 말하게 한다(ModuleDetachWiredTests 와 같은 꼴).
            string runner = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            string harness = System.IO.File.ReadAllText("Assets/_Project/Scripts/Editor/StageClearHarness.cs");
            StringAssert.DoesNotContain("? robot.mountCoef : robot.enhancedMountCoef", runner);
            StringAssert.DoesNotContain("? robotB.mountCoef : robotB.enhancedMountCoef", runner);
            StringAssert.DoesNotContain("? robot.mountCoef : robot.enhancedMountCoef", harness);
            StringAssert.Contains("MountEnhanceRule.MountCoef(", runner);
            string panel = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/ReviewerShortcuts.cs");
            StringAssert.Contains("DrawEnhanceButton(y, x, w, h, button);", panel, "설정 판에 강화 단추가 없다");
        }
    }
}
