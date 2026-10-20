using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>#223 — 도전 진입 만재는 S4 · S5 · S6 만.</summary>
    public sealed class ChallengeFillRuleTests
    {
        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(4, true)]
        [TestCase(5, true)]
        [TestCase(6, true)]
        [TestCase(0, false)]
        public void FillsOnlyS4ToS6(int stage, bool expected) =>
            Assert.AreEqual(expected, ChallengeFillRule.FillsOnEntry(stage));
    }
}
