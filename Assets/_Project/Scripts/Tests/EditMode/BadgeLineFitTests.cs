using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>#224 — 배지 목표 줄 글자 사각 ⊂ 카드 줄 칸(S1~S6 파밍 · 도전 · 창 셋).</summary>
    public sealed class BadgeLineFitTests
    {
        private static Vector2 Measure(string text, int size)
        {
            string[] lines = text.Split('\n');
            int widest = 0;
            foreach (string l in lines) if (l.Length > widest) widest = l.Length;
            return new Vector2(widest * size * 0.9f, lines.Length * size * 1.3f);
        }

        private static readonly string[] Topics = { "벨트 연결", "모듈 배치", "마운트 강화", "로봇 B 해금 · 합체", "폭발탄", "자유 플레이 · 보스" };

        [TestCase(420f, 60f)]   // 576×1024 근처
        [TestCase(300f, 40f)]   // 1024×768 근처
        [TestCase(200f, 28f)]   // 작은 창
        public void EveryGoalLine_InsideCardRow(float width, float height)
        {
            foreach (string topic in Topics)
                foreach (string goal in new[] { "파밍: " + topic + "  ·  모아서 보드를 키운다", "목표: " + topic })
                {
                    BadgeLineFit.Result r = BadgeLineFit.Fit(goal, width, height, 24, Measure);
                    if (!r.fits) { Assert.AreEqual(BadgeLineFit.Floor, r.fontSize, goal); continue; }
                    Vector2 m = Measure(r.text, r.fontSize);
                    Assert.LessOrEqual(m.x, width + 0.5f, goal + " 폭");
                    Assert.LessOrEqual(m.y, height + 0.5f, goal + " 높이");
                }
        }

        [Test]
        public void FarmLine_BreaksBeforeTail() =>
            Assert.AreEqual("파밍: 로봇 B 해금 · 합체\n모아서 보드를 키운다",
                BadgeLineFit.BreakAtSeparator("파밍: 로봇 B 해금 · 합체  ·  모아서 보드를 키운다"));

        [Test]
        public void ShortLine_StaysOneLineAtStartSize()
        {
            BadgeLineFit.Result r = BadgeLineFit.Fit("목표: 폭발탄", 400f, 60f, 24, Measure);
            Assert.AreEqual("목표: 폭발탄", r.text);
            Assert.AreEqual(24, r.fontSize);
        }
    }
}
