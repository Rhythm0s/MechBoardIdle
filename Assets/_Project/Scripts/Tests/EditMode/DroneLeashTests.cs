using MBI.Core.Combat;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **누적형 드론의 활동 범위 — 플레이어 기준 N** (2026-09-18 사용자 확정).
    ///
    /// 여기서 지키는 것은 하나다 — **잣대가 드론이 아니라 로봇에 있다.**
    /// 종전 규칙은 드론 자리에서 사거리를 재서, 적을 타고 한없이 멀어지는 길이 열려 있었다.
    /// </summary>
    public sealed class DroneLeashTests
    {
        private const float D = 0.001f;

        [Test]
        public void 값이_없으면_드론_사거리를_쓴다()
        {
            // ⚠️ 지어낸 수를 넣지 않고 이미 있는 값에서 끈다(본체 사거리 9.2 · C-3).
            Assert.AreEqual(9.2f, DroneLeashRule.Radius(0f, 9.2f), D);
            Assert.AreEqual(9.2f, DroneLeashRule.Radius(-3f, 9.2f), D, "음수도 「안 줬다」로 본다");
            Assert.AreEqual(4f, DroneLeashRule.Radius(4f, 9.2f), D, "주면 그것을 쓴다");
        }

        [Test]
        public void 범위_안팎을_로봇_기준으로_가른다()
        {
            var robot = new Vector2(10f, 10f);

            Assert.IsTrue(DroneLeashRule.Inside(robot, new Vector2(13f, 10f), 4f));
            Assert.IsTrue(DroneLeashRule.Inside(robot, new Vector2(14f, 10f), 4f), "테두리는 안이다");
            Assert.IsFalse(DroneLeashRule.Inside(robot, new Vector2(14.1f, 10f), 4f));
        }

        [Test]
        public void 범위_밖이면_테두리로_끌어당긴다()
        {
            var robot = new Vector2(0f, 0f);
            Vector2 got = DroneLeashRule.Clamp(robot, new Vector2(100f, 0f), 4f);

            Assert.AreEqual(4f, got.x, D, "테두리에 선다");
            Assert.AreEqual(0f, got.y, D, "방향은 그대로다");
        }

        [Test]
        public void 범위_안이면_그대로_둔다()
        {
            // 📌 되돌려 보내지 않는다 — 왕복하면 드론이 진자처럼 흔들린다.
            var robot = Vector2.zero;
            var p = new Vector2(2f, 1f);
            Assert.AreEqual(p, DroneLeashRule.Clamp(robot, p, 4f));
        }

        [Test]
        public void 붙잡은_적이_범위를_벗어나면_놓는다()
        {
            var robot = Vector2.zero;

            Assert.IsTrue(DroneLeashRule.KeepsTarget(robot, new Vector2(3f, 0f), 4f));
            Assert.IsFalse(DroneLeashRule.KeepsTarget(robot, new Vector2(9f, 0f), 4f),
                "적이 걸어 나가면 따라 나가지 않는다");
        }

        [Test]
        public void 범위가_0_이면_로봇_자리뿐이다()
        {
            // 사거리도 범위도 0 인 드론은 칠 것이 없다 — 「제한 없음」이 아니다.
            var robot = new Vector2(5f, 5f);
            Assert.AreEqual(robot, DroneLeashRule.Clamp(robot, new Vector2(9f, 9f), 0f));
            Assert.IsFalse(DroneLeashRule.Inside(robot, new Vector2(6f, 5f), 0f));
        }
    }
}
