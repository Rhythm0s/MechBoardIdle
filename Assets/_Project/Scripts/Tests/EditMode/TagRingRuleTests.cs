using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>#251 — 태그 링 분모 = 전 슬롯 만충 기준 · 빈 슬롯이 있어도 불변.</summary>
    public sealed class TagRingRuleTests
    {
        [Test]
        public void B_DenominatorFixedAt40_AsSlotsFill()
        {
            var b = new MountLoad(MountLoad.SlotsRobotB, MountLoad.StandardStacks(10f, 5f));
            Assert.AreEqual(40f, TagRingRule.Denominator(b, true), 1e-4f, "빈 판 = 8 × 5");
            b.Load(MountItem.Drone, 3f);
            Assert.AreEqual(40f, TagRingRule.Denominator(b, true), 1e-4f, "한 슬롯 열려도 그대로");
            Assert.AreEqual(3f / 40f, TagRingRule.Ratio(b, true), 1e-4f);
            b.Load(MountItem.Drone, 4f);   // 두 번째 슬롯이 열린다
            Assert.AreEqual(40f, TagRingRule.Denominator(b, true), 1e-4f);
            Assert.AreEqual(7f / 40f, TagRingRule.Ratio(b, true), 1e-4f, "톱니 없이 오른다");
        }

        [Test]
        public void A_DenominatorFixedAt40()
        {
            var a = new MountLoad(MountLoad.SlotsRobotA, MountLoad.StandardStacks(10f));
            Assert.AreEqual(40f, TagRingRule.Denominator(a, false), 1e-4f);
            a.Load(MountItem.Standard, 12f);
            Assert.AreEqual(40f, TagRingRule.Denominator(a, false), 1e-4f);
            Assert.AreEqual(0.3f, TagRingRule.Ratio(a, false), 1e-4f);
        }

        [Test]
        public void Runner_UsesRule()
        {
            string sr = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            StringAssert.Contains("DrawLoadRing(tagRect, TagRingRule.Ratio(standby, standbyIsB),", sr);
        }
    }
}
