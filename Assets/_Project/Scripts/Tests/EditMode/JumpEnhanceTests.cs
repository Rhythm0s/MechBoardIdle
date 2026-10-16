using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **바로가기 S4 이상 점프 = 강화 표식 켬** (사용자 확정 #162 · 2026-10-16).
    /// </summary>
    public sealed class JumpEnhanceTests
    {
        [TearDown]
        public void Clean()
        {
            IdleSignals.TryDrainJumpEnhance();
            IdleSignals.MountEnhanced = false;
        }

        [Test]
        public void S4부터_켜고_S1부터_S3은_안_켠다()
        {
            Assert.IsFalse(MountEnhanceRule.JumpTurnsOn(1));
            Assert.IsFalse(MountEnhanceRule.JumpTurnsOn(3));
            Assert.IsTrue(MountEnhanceRule.JumpTurnsOn(4));
            Assert.IsTrue(MountEnhanceRule.JumpTurnsOn(6));
            Assert.IsFalse(MountEnhanceRule.JumpTurnsOn(0), "튜토리얼(0)은 안 켠다");
        }

        [Test]
        public void 요청은_표식을_바로_세우고_한_번만_가져간다()
        {
            IdleSignals.MountEnhanced = false;
            IdleSignals.RequestJumpEnhance();
            Assert.IsTrue(IdleSignals.MountEnhanced, "판이 서기 전에 표식이 서야 계수가 바로 ×1.45");
            Assert.IsTrue(IdleSignals.TryDrainJumpEnhance());
            Assert.IsFalse(IdleSignals.TryDrainJumpEnhance());
        }

        [Test]
        public void 바로가기는_판을_세우기_전에_요청하고_런타임은_재료없이_저장한다()
        {
            string sc = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/ReviewerShortcuts.cs");
            int req = sc.IndexOf("IdleSignals.RequestJumpEnhance();");
            int load = sc.IndexOf("runner.LoadStage(s);\n            runner.FillMountsForJump();".Replace("\n", System.Environment.NewLine));
            if (load < 0) load = sc.IndexOf("runner.FillMountsForJump();");
            Assert.Greater(req, 0);
            Assert.Less(req, load, "점프 강화 요청이 판을 세운 뒤에 있다");
            string rt = System.IO.File.ReadAllText("Assets/_Project/Scripts/Idle/IdleRuntime.cs");
            StringAssert.Contains("if (IdleSignals.TryDrainJumpEnhance() && !Data.mountEnhanced)", rt);
        }
    }
}
