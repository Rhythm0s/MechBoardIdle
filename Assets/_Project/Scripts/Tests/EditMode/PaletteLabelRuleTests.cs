using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>#213 — 팔레트 단추 이름은 낱말 자리에서 두 줄(2026-10-20 사용자 확정).</summary>
    public sealed class PaletteLabelRuleTests
    {
        [TestCase("기초 가공소", "기초\n가공소")]
        [TestCase("생산량 모듈", "생산량\n모듈")]
        [TestCase("생산속도 모듈", "생산속도\n모듈")]
        [TestCase("복합 가공소", "복합\n가공소")]
        [TestCase("변환기", "변환기")]
        public void BreaksAtWord(string label, string expected) =>
            Assert.AreEqual(expected, PaletteLabelRule.BreakAtWord(label));

        [Test]
        public void WidestLine_PicksLonger() =>
            Assert.AreEqual("가공소", PaletteLabelRule.WidestLine("기초\n가공소"));
    }
}
