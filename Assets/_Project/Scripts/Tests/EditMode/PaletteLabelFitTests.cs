using System.Collections.Generic;
using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>#220 — 팔레트 라벨 글자 사각 ⊂ 단추 사각(창 셋 · 노드 · 모듈 · 벨트 요소 이름).</summary>
    public sealed class PaletteLabelFitTests
    {
        // 가짜 잼: 한글 한 자 = 크기 폭 · 줄 높이 = 1.3 × 크기.
        private static Vector2 Measure(string text, int size)
        {
            string[] lines = text.Split('\n');
            int widest = 0;
            foreach (string l in lines) if (l.Length > widest) widest = l.Length;
            return new Vector2(widest * size, lines.Length * size * 1.3f);
        }

        private static readonly List<string> Names = new List<string>
            { "변환기", "기초 가공소", "복합 가공소", "에너지", "저장", "부스터", "보호막", "생산량 모듈", "생산속도 모듈", "병합기", "분류기" };

        [TestCase(110f)]   // 576×1024 근처
        [TestCase(60f)]    // 1024×768 근처
        [TestCase(30f)]    // 아주 작은 창(42)
        public void EveryLabel_InsideButton(float side)
        {
            var button = new Rect(0f, 0f, side, side);
            PaletteLabelFit.Result r = PaletteLabelFit.Compute(Names, side, Measure);
            Rect band = PaletteLabelFit.BandRect(button, r.bandHeight);
            Assert.IsTrue(button.Contains(band.min) && button.Contains(band.max), "띠는 단추 안");
            if (!r.fits) Assert.AreEqual(PaletteLabelFit.FontFloor, r.fontSize, "끝까지 안 들면 바닥 + 클립");
            else
                foreach (string n in Names)
                {
                    Vector2 m = Measure(PaletteLabelFit.TextAt(n, r.fontSize, PaletteLabelFit.BandWidth(side), Measure), r.fontSize);
                    Assert.LessOrEqual(m.x, band.width + 0.5f, n + " 폭");
                    Assert.LessOrEqual(m.y, band.height + 0.5f, n + " 높이");
                }
        }

        [Test]
        public void Cap_IsNotExceeded_EvenWhenRoomy() =>
            Assert.LessOrEqual(PaletteLabelFit.Compute(new List<string> { "저장" }, 400f, Measure).fontSize, PaletteLabelFit.FontCap);

        [Test]
        public void SmallWindow_AllowsBelowLadderFloor()
        {
            PaletteLabelFit.Result r = PaletteLabelFit.Compute(Names, 60f, Measure);
            Assert.IsTrue(r.fits);
            Assert.Less(r.fontSize, 16, "팔레트 라벨 한정 — 사다리 바닥 16 아래 허용(#220)");
        }

        [Test]
        public void GrownBand_ShrinksThumbAboveIt()
        {
            var button = new Rect(0f, 0f, 60f, 60f);
            Rect thumb = PaletteLabelFit.ThumbRect(button, 38f, 0.88f);
            Rect band = PaletteLabelFit.BandRect(button, 38f);
            Assert.LessOrEqual(thumb.yMax, band.yMin + 0.5f, "띠가 반을 넘으면 그림은 띠 위");
        }
    }
}
