using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **설정 바로가기 점프 = 마운트 가득** (사용자 확정 #156 · 2026-10-16).
    /// </summary>
    public sealed class JumpFillTests
    {
        [Test]
        public void A는_표준탄_40_B는_드론_40으로_찬다()
        {
            var a = new MountLoad(MountLoad.SlotsRobotA, MountLoad.StandardStacks(10f));
            a.Load(MountItem.Standard, 0.4f);   // 남은 것이 있어도 가득까지만
            a.Load(MountItem.Standard, MountLoad.SlotsRobotA * 10f);
            Assert.AreEqual(40f, a.Total, 1e-3f);

            var b = new MountLoad(MountLoad.SlotsRobotB, MountLoad.StandardStacks(10f, 5f));
            float cap = MountLoad.SlotsRobotB * 5f;
            b.Load(MountItem.Drone, cap * 0.5f);
            b.Load(MountItem.DroneAoe, cap);
            Assert.AreEqual(40f, b.AmountOf(MountItem.Drone) + b.AmountOf(MountItem.DroneAoe), 1e-3f);
        }

        [Test]
        public void 바로가기는_판을_세운_뒤_채우고_튜토리얼_점프는_안_채운다()
        {
            string src = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/ReviewerShortcuts.cs");
            StringAssert.Contains("runner.LoadStage(s);\n            runner.FillMountsForJump();", src.Replace("\r\n", "\n"));
            int tut = src.IndexOf("if (s == tutorialStage && tutorialStage != null)", System.StringComparison.Ordinal);
            int fill = src.IndexOf("runner.FillMountsForJump();", System.StringComparison.Ordinal);
            Assert.Greater(fill, tut, "튜토리얼 갈래(먼저 return) 뒤에서만 채운다");
        }
    }
}
