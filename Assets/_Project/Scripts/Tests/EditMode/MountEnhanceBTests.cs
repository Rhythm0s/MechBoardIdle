using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>#195 로봇 B 마운트 강화 (2026-10-19 사용자 확정) · #196 비용 50.</summary>
    public sealed class MountEnhanceBTests
    {
        [SetUp] public void SetUp() { TutorialSignals.Reset(); IdleSignals.Reset(); }
        [TearDown] public void TearDown() { TutorialSignals.Reset(); IdleSignals.Reset(); }

        private static bool Active(bool cleared = true, int stage = 6, bool farming = true, bool unlocked = true,
                                   bool enhancedB = false, double mat = 50d, double scrap = 300d) =>
            MountEnhanceRule.TutorialActiveB(cleared, stage, farming, unlocked, enhancedB, mat, 50d, scrap, 300d);

        [Test]
        public void B_안내는_S6_파밍_재료_고철_둘_다()
        {
            Assert.IsTrue(Active());
            Assert.IsTrue(Active(mat: 50d), "남은 13 + S3 37 = 50 — 막이 선다");
            Assert.IsFalse(Active(mat: 49d));
            Assert.IsFalse(Active(scrap: 299d));
            Assert.IsFalse(Active(enhancedB: true));
            Assert.IsFalse(Active(unlocked: false));
            Assert.IsFalse(Active(stage: 5));
            Assert.IsFalse(Active(farming: false));
            Assert.IsFalse(MountEnhanceRule.JumpTurnsOnB(6), "#198 — S6 점프는 B 를 자동으로 안 켬(재료 지급 → B 막)");
            Assert.IsFalse(MountEnhanceRule.JumpTurnsOnB(5));
            Assert.AreEqual("로봇 B 마운트를 탭한 뒤 강화를 누르세요", MountEnhanceRule.BoardHintB);
        }

        [Test]
        public void B_표식은_따로_저장되고_따로_신호()
        {
            Assert.IsFalse(new SaveDataV1().mountEnhancedB);
            var on = new SaveDataV1 { mountEnhancedB = true };
            Assert.IsTrue(JsonUtility.FromJson<SaveDataV1>(JsonUtility.ToJson(on)).mountEnhancedB);

            IdleSignals.RequestMountEnhanceB(50d);
            Assert.IsTrue(IdleSignals.TryDrainMountEnhanceB(out double c));
            Assert.AreEqual(50d, c);
            Assert.IsFalse(IdleSignals.TryDrainMountEnhanceB(out _));
            IdleSignals.RequestJumpEnhanceB();
            Assert.IsTrue(IdleSignals.MountEnhancedB);
            Assert.IsFalse(IdleSignals.MountEnhanced, "B 표식이 A 를 켜지 않는다");
        }

        [Test]
        public void B_안내는_보드에서_B_판을_볼_때만()
        {
            TutorialSignals.HighlightEnhanceMountB = true;
            Assert.AreEqual(TutorialGate.Phase.EnhanceMount, TutorialGate.Current, "전투 화면 = ▼ 조립");
            TutorialSignals.BoardViewOpen = true;
            TutorialSignals.BoardViewIsTutorialBoard = false;   // B 판
            Assert.AreEqual(TutorialGate.Phase.EnhanceMountBoard, TutorialGate.Current);
            TutorialSignals.BoardViewIsTutorialBoard = true;    // A 판
            Assert.AreEqual(TutorialGate.Phase.None, TutorialGate.Current);

            string sr = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            StringAssert.Contains("_sim.SetDroneKindCap(1, DroneKindCapB(_mountEnhancedBApplied));", sr);   // #242 강화 = 상한 +2(🗑️ #234 처리량 +2 · B 계수 ×1.45)
            StringAssert.Contains("_sim.SetMountCoef(1, robotB.mountCoef);", sr);
            string bc = System.IO.File.ReadAllText("Assets/_Project/Scripts/Logistics/BoardController.cs");
            StringAssert.Contains("if (owner == MountOwner.RobotB) IdleSignals.RequestMountEnhanceB(cost);", bc);
        }
    }
}
