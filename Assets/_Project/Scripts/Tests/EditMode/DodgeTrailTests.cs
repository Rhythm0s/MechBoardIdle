using MBI.Core.Combat;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 회피 잔상·줄기의 **자리 셈** (2026-09-18 사용자 확정 · 참고 이미지).
    ///
    /// ⚠️ 재는 것은 **순수 계산**뿐이다 — 그림이 몇 초 뒤 사라지는지는 씬이 있어야 재지고,
    /// 그것은 육안이 볼 자리다. 여기서 지키는 것은 「자국이 지나온 길 위에 선다」다.
    /// </summary>
    public sealed class DodgeTrailTests
    {
        private const float D = 0.001f;

        [Test]
        public void 잔상은_시작점에서_끝점_사이를_등간격으로_선다()
        {
            var from = new Vector2(0f, 0f);
            var to = new Vector2(3f, 0f);

            Vector2[] spots = DodgeTrailRule.AfterimagePositions(from, to, 3);

            Assert.AreEqual(3, spots.Length);
            Assert.AreEqual(0f, spots[0].x, D, "첫 장은 **시작점**이다 — 어디서 왔는지가 자국의 내용물이다");
            Assert.AreEqual(1f, spots[1].x, D);
            Assert.AreEqual(2f, spots[2].x, D);
        }

        [Test]
        public void 잔상은_끝점에_안_선다()
        {
            // ⚠️ 끝점에는 로봇 본체가 서 있다 — 겹치면 「한 장 더 그린 로봇」으로 보인다.
            var from = new Vector2(0f, 0f);
            var to = new Vector2(1.25f, 0f);

            foreach (Vector2 s in DodgeTrailRule.AfterimagePositions(from, to, 3))
                Assert.Less(s.x, to.x - D, "끝점에는 잔상이 없다");
        }

        [Test]
        public void 뒤엣것일수록_옅다()
        {
            // 「사라져 가는 자국」의 방향이다 — 시작점 쪽이 가장 옅다.
            float a0 = DodgeTrailRule.AfterimageAlpha(0, 3, 0.45f);
            float a1 = DodgeTrailRule.AfterimageAlpha(1, 3, 0.45f);
            float a2 = DodgeTrailRule.AfterimageAlpha(2, 3, 0.45f);

            Assert.Less(a0, a1);
            Assert.Less(a1, a2);
            Assert.AreEqual(0.45f, a2, D, "가장 진한 것이 지정한 옅기다");
        }

        [Test]
        public void 줄기는_두_점의_한가운데에_그_길이로_눕는다()
        {
            var from = new Vector2(1f, 1f);
            var to = new Vector2(1f, 3f);   // 위로 2칸

            Assert.AreEqual(new Vector2(1f, 2f), DodgeTrailRule.StreakCenter(from, to));
            Assert.AreEqual(2f, DodgeTrailRule.StreakLength(from, to), D);
            Assert.AreEqual(90f, DodgeTrailRule.StreakDegrees(from, to), D, "위로 가면 90도");
        }

        [Test]
        public void 제자리_회피에는_그릴_자국이_없다()
        {
            // 길이가 0 이면 부르는 쪽이 아무것도 안 그린다 — 점 하나가 남으면 안 된다.
            var p = new Vector2(2f, 2f);
            Assert.AreEqual(0f, DodgeTrailRule.StreakLength(p, p), D);
            Assert.AreEqual(0f, DodgeTrailRule.StreakDegrees(p, p), D, "방향이 없으면 0 이다");
        }

        [Test]
        public void 장수가_0_이면_잔상이_없다()
        {
            Assert.AreEqual(0, DodgeTrailRule.AfterimagePositions(Vector2.zero, Vector2.one, 0).Length);
        }
    }
}
