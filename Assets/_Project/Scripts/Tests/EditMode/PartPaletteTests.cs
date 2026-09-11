using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 파츠 종류별 색 다섯 (2026-09-11 신설 · UI 문서 3-0 · 플랜 §71-16 ⑧).
    ///
    /// **무엇을 지키는가.** 색이 **다섯인가**(좌우가 같은가)와 **저채도인가**다.
    /// 색은 여기서 바탕이고, 채도를 올리면 바탕이 앞으로 나와 품목과 노드를 가린다.
    /// </summary>
    public sealed class PartPaletteTests
    {
        [Test]
        public void 좌우는_같은_색이다()
        {
            // 가르면 색이 여덟이 되어 **색이 정보를 잃는다** — 좌우는 이름표가 이미 가른다.
            Assert.AreEqual(PartPalette.Of(RobotPart.ArmL), PartPalette.Of(RobotPart.ArmR));
            Assert.AreEqual(PartPalette.Of(RobotPart.LegL), PartPalette.Of(RobotPart.LegR));
            Assert.AreEqual(PartPalette.Of(RobotPart.ShoulderL), PartPalette.Of(RobotPart.ShoulderR));
        }

        [Test]
        public void 색이_정확히_다섯이고_서로_다르다()
        {
            Color[] five = PartPalette.Five;
            Assert.AreEqual(5, five.Length);

            for (int i = 0; i < five.Length; i++)
            for (int j = i + 1; j < five.Length; j++)
                Assert.IsTrue(Distance(five[i], five[j]) > 0.06f,
                    $"{i}번과 {j}번이 눈으로 안 갈린다");
        }

        [Test]
        public void 모두_저채도다()
        {
            // 채도 = (최대 − 최소). 바탕이라 낮아야 한다 — 지침 §1 이 코어를
            // 「물류와 그 애니메이션」으로 정했고 구역은 그 아래다.
            foreach (Color c in PartPalette.Five)
            {
                float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                Assert.Less(max - min, 0.30f, $"{c} 가 너무 진하다");
                Assert.Greater(max - min, 0.02f, $"{c} 는 회색이라 갈리지 않는다");
            }
        }

        [Test]
        public void 파츠가_아니면_미색이다()
        {
            Assert.AreEqual(PartPalette.Neutral, PartPalette.Of(RobotPart.None));
        }

        [Test]
        public void 바닥_틴트는_흰_쪽으로_당긴다()
        {
            // `SpriteRenderer.color` 는 그림에 **곱해진다** — 알파를 낮추면 투명해질 뿐
            // 색이 안 섞인다. 흰 쪽으로 당겨야 타일 무늬가 남는다.
            Color floor = PartPalette.FloorOf(RobotPart.Torso);
            Color raw = PartPalette.Of(RobotPart.Torso);

            Assert.AreEqual(1f, floor.a, 0.0001f, "바닥은 불투명하다");
            Assert.Greater(floor.r, raw.r, "흰 쪽으로 당겨졌다");
            Assert.Greater(floor.g, raw.g);
            Assert.Less(floor.g, 1f, "그래도 색이 남아 있다");
        }

        [Test]
        public void 점선은_바닥보다_옅다()
        {
            // 배치 규격 「밑의 타일을 가리지 않는다」 — 선은 알파 40%.
            Assert.AreEqual(0.40f, PartPalette.LineAlpha, 0.0001f);
            Assert.AreEqual(0.40f, PartPalette.LineOf(RobotPart.Head).a, 0.0001f);
        }

        [Test]
        public void 모든_파츠가_색을_받는다()
        {
            foreach (RobotPart p in System.Enum.GetValues(typeof(RobotPart)))
            {
                if (p == RobotPart.None) continue;
                Assert.AreNotEqual(PartPalette.Neutral, PartPalette.Of(p),
                    $"{p} 가 미색으로 떨어진다 — 색표에서 빠졌다");
            }
        }

        private static float Distance(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
    }
}
