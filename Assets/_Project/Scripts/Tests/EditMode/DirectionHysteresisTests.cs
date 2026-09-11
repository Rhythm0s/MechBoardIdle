using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 방향 접기의 떨림 (2026-09-11 신설 · 플랜 §71-16 ⑦).
    ///
    /// **무엇을 지키는가.** 대각으로 갈 때 **축이 프레임마다 뒤집히지 않는가**다.
    /// 구 규칙(`|dx| >= |dy|`)은 두 축이 거의 같을 때 미세한 차이로 승자가 갈려
    /// 동면과 북면을 번갈아 냈다 — 걷는 그림이 두 장 겹쳐 보이는 그 증상이다.
    /// </summary>
    public sealed class DirectionHysteresisTests
    {
        [Test]
        public void 첫_방향은_우세_축이_정한다()
        {
            Assert.AreEqual(UnitAnimDirection.East, DirectionHysteresis.Dominant(new Vector2(1f, 0.2f)));
            Assert.AreEqual(UnitAnimDirection.West, DirectionHysteresis.Dominant(new Vector2(-1f, 0.2f)));
            Assert.AreEqual(UnitAnimDirection.North, DirectionHysteresis.Dominant(new Vector2(0.2f, 1f)));
            Assert.AreEqual(UnitAnimDirection.South, DirectionHysteresis.Dominant(new Vector2(0.2f, -1f)));
        }

        [Test]
        public void 대각_경계에서_축이_안_뒤집힌다()
        {
            // **이것이 결함 그 자체다.** 45도 언저리에서 부호 없는 잡음이 오가는 상황을
            // 그대로 흉내 낸다 — 구 규칙이면 East ↔ North 가 번갈아 나왔다.
            var jitter = new[]
            {
                new Vector2(1.00f, 0.99f), new Vector2(0.99f, 1.00f),
                new Vector2(1.01f, 0.99f), new Vector2(0.98f, 1.02f),
                new Vector2(1.00f, 1.00f),
            };

            UnitAnimDirection dir = UnitAnimDirection.East;
            foreach (Vector2 d in jitter)
            {
                dir = DirectionHysteresis.Resolve(d, dir);
                Assert.AreEqual(UnitAnimDirection.East, dir, $"델타 {d} 에서 축이 넘어갔다");
            }
        }

        [Test]
        public void 세로로_붙들고_있어도_똑같다()
        {
            // 반대 방향도 같아야 한다 — 한쪽에만 관성이 있으면 북에서 동으로는 잘 가고
            // 동에서 북으로는 안 가는 비대칭이 생긴다.
            UnitAnimDirection dir = UnitAnimDirection.North;
            foreach (Vector2 d in new[] { new Vector2(1.00f, 0.99f), new Vector2(1.02f, 0.98f) })
            {
                dir = DirectionHysteresis.Resolve(d, dir);
                Assert.AreEqual(UnitAnimDirection.North, dir);
            }
        }

        [Test]
        public void 확실히_꺾으면_넘어간다()
        {
            // 관성이 너무 세면 **방향이 안 바뀐다** — 대각으로 꺾어도 옛 면을 붙들고
            // 미끄러진다. 1.5배를 넘기면 넘어가야 한다.
            Assert.AreEqual(UnitAnimDirection.North,
                DirectionHysteresis.Resolve(new Vector2(1f, 2f), UnitAnimDirection.East),
                "세로가 가로의 2배면 넘어간다");
            Assert.AreEqual(UnitAnimDirection.East,
                DirectionHysteresis.Resolve(new Vector2(2f, 1f), UnitAnimDirection.North));
        }

        [Test]
        public void 같은_축_안에서는_좌우가_그대로_뒤집힌다()
        {
            // 동 → 서는 **같은 축**이라 떨림이 아니다. 실제로 반대로 걷는 것이므로
            // 막으면 뒤로 걸을 때 그림이 안 바뀐다.
            Assert.AreEqual(UnitAnimDirection.West,
                DirectionHysteresis.Resolve(new Vector2(-1f, 0.1f), UnitAnimDirection.East));
            Assert.AreEqual(UnitAnimDirection.South,
                DirectionHysteresis.Resolve(new Vector2(0.1f, -1f), UnitAnimDirection.North));
        }

        [Test]
        public void 값이_1_보다_작아도_관성이_뒤집히지_않는다()
        {
            // 0.5 를 넣으면 「새 축이 절반만 돼도 이긴다」가 되어 **떨림이 더 심해진다.**
            // 1 로 눌러 최소한 구 규칙과 같게 만든다.
            Assert.AreEqual(UnitAnimDirection.East,
                DirectionHysteresis.Resolve(new Vector2(1f, 0.9f), UnitAnimDirection.East, 0.5f));
        }

        [Test]
        public void 가정값은_1_5_다()
        {
            // ⚠️ 문서에 절이 없는 **가정**이다 — 설계가 역기입하면 이 단언이 바뀐다.
            Assert.AreEqual(1.5f, DirectionHysteresis.Hysteresis, 0.0001f);
        }
    }
}
