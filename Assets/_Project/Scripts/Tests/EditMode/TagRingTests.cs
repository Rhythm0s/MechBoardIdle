using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// #185 태그 원형 링 하나 (2026-10-19 사용자 확정) — 쿨다운 안쪽 띠 · 노란 네모 테두리 폐기 ·
    /// 적재 링 색 = 스킬 준비(노랑) / 평상(하늘색). 그리기라 소스로 지킨다.
    /// </summary>
    public sealed class TagRingTests
    {
        private static string Src() => System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");

        [Test]
        public void 링은_적재_하나고_준비면_노랑()
        {
            string sr = Src();
            StringAssert.Contains("skillReady ? TagSkillRingColor : TagLoadRingColor, loadT);", sr);
            StringAssert.DoesNotContain("if (skillReady) DrawTagSkillRing(tagRect);", sr, "노란 네모 테두리 폐기");
            StringAssert.DoesNotContain("DrawLoadRing(Shrink(tagRect, loadT), ready,", sr, "쿨다운 안쪽 띠 폐기");
            StringAssert.Contains("if (cd > 0f) return $\"쿨다운\\n{cd:F1}s\";", sr, "쿨다운은 글자로");
        }
    }
}
