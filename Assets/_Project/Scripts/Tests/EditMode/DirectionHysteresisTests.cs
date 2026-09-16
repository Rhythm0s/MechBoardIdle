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
        public void 한_방향이_붙드는_각은_120도다()
        {
            // 🗑️ **구 단언 「가정값은 1.5 다」는 폐기**(2026-09-16 사용자 확정 · 육안 10차 ①).
            // 1.5 는 문서 절이 없는 가정이었고 tan 56.3°(=112.6°) 였다. 지금은 확정 120° 다.
            Assert.AreEqual(120f, DirectionHysteresis.RetainDegrees, 0.0001f);

            // **배수는 각에서 나온다** — tan 60° = √3. 손으로 적은 수가 아니다.
            Assert.AreEqual(Mathf.Sqrt(3f), DirectionHysteresis.Hysteresis, 0.0001f,
                "배수가 tan(120/2) 가 아니다");
        }

        [Test]
        public void 육십도_안이면_유지하고_넘으면_돈다()
        {
            // **결함 그 자체의 시험이다** — 「표적이 대각선이면 얼굴이 좌/우로 매우 빠르게
            // 뒤집힌다」(사용자 육안 10차 ①). 45° 언저리는 유지되어야 하고,
            // 60° 를 확실히 넘으면 그때는 돌아야 한다.
            foreach (float deg in new[] { 0f, 30f, 45f, 55f, 59f })
            {
                var d = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
                Assert.AreEqual(UnitAnimDirection.East,
                    DirectionHysteresis.Resolve(d, UnitAnimDirection.East),
                    $"{deg}도는 ±60 안이라 동면을 유지해야 한다");
            }

            foreach (float deg in new[] { 61f, 75f, 90f, 110f })
            {
                var d = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
                Assert.AreEqual(UnitAnimDirection.North,
                    DirectionHysteresis.Resolve(d, UnitAnimDirection.East),
                    $"{deg}도는 ±60 밖이라 북면으로 넘어가야 한다");
            }
        }

        [Test]
        public void 대각선에_선_표적에서_얼굴이_안_떤다()
        {
            // 조준도 **걸음과 같은 자리**에서 넷으로 접힌다(`CombatEntityView`).
            // 45° 언저리를 오가는 표적을 흉내 낸다 — 구 규칙(관성 1)이면 매번 뒤집혔다.
            var wobble = new[]
            {
                new Vector2(1.00f, 0.98f), new Vector2(0.98f, 1.00f),
                new Vector2(1.00f, 1.02f), new Vector2(1.03f, 0.99f),
            };

            UnitAnimDirection dir = UnitAnimDirection.East;
            foreach (Vector2 d in wobble)
            {
                dir = DirectionHysteresis.Resolve(d, dir);
                Assert.AreEqual(UnitAnimDirection.East, dir, $"조준 {d} 에서 얼굴이 뒤집혔다");
            }

            // ⚠️ 구 규칙이 실제로 떨었다는 것도 같이 박아 둔다 — 안 그러면 이 시험이
            // 「원래 안 떨었다」로도 통과해 버려 무엇을 고쳤는지가 사라진다.
            Assert.AreEqual(UnitAnimDirection.North,
                DirectionHysteresis.Resolve(new Vector2(0.98f, 1.00f), UnitAnimDirection.East, 1f),
                "관성 1 에서는 45도 언저리에서 축이 넘어갔어야 한다(구 결함)");
        }
    }
}
