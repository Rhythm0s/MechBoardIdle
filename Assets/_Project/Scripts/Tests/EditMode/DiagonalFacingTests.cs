using MBI.Data;
using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **45° 대각 이동에서 면이 떨리지 않는다** (2026-10-01 사용자 육안 ⑭ ②).
    ///
    /// ⚠️ 이 시험의 핵심은 **진짜 <see cref="GridMovement.Step"/> 을 돌린다**는 것이다.
    /// 한 축짜리 델타를 손으로 적어 넣으면 「내가 만든 입력에 내가 답하는」 시험이 되고,
    /// 걸음 규칙이 바뀌어도 이 시험은 모른 채 통과한다.
    /// </summary>
    public sealed class DiagonalFacingTests
    {
        /// <summary>격자 걸음을 그대로 돌리며 뷰가 보는 면의 열을 만든다.</summary>
        private static UnitAnimDirection[] Walk(Vector2 from, Vector2 to, float step, int ticks,
            bool stepped)
        {
            var dirs = new UnitAnimDirection[ticks];
            UnitAnimDirection last = UnitAnimDirection.East;
            Vector2 prev = Vector2.zero;
            Vector2 pos = from;
            for (int i = 0; i < ticks; i++)
            {
                Vector2 next = GridMovement.Step(pos, to, step);
                Vector2 delta = next - pos;
                pos = next;
                last = stepped
                    ? StepFacing.Resolve(delta, ref prev, last)
                    : DirectionHysteresis.Resolve(delta, last);   // 고치기 전 길
                dirs[i] = last;
            }
            return dirs;
        }

        private static int Flips(UnitAnimDirection[] dirs)
        {
            int n = 0;
            for (int i = 1; i < dirs.Length; i++) if (dirs[i] != dirs[i - 1]) n++;
            return n;
        }

        /// <summary>
        /// ⚠️ **먼저 병이 있다는 것을 못 박는다** — 고치기 전 길로 걸으면 면이 거의
        /// 매 틱 뒤집힌다. 이 단정이 깨지면 걸음 규칙이 바뀐 것이고, 아래 시험의
        /// 전제도 같이 바뀐다.
        /// </summary>
        [Test]
        public void 걸음_하나만_보면_매_틱_뒤집힌다()
        {
            UnitAnimDirection[] before =
                Walk(Vector2.zero, new Vector2(10f, 10f), 0.1f, 12, stepped: false);
            Assert.GreaterOrEqual(Flips(before), 10,
                "45° 인데 면이 안 떨린다 — 걸음이 한 축짜리가 아니게 됐다");
        }

        /// <summary>**딱 45°** — 면을 끝까지 붙든다. 열에 바뀐 자리가 하나도 없어야 한다.</summary>
        [Test]
        public void 정확한_45도에서_면을_붙든다()
        {
            UnitAnimDirection[] after =
                Walk(Vector2.zero, new Vector2(10f, 10f), 0.1f, 12, stepped: true);
            Assert.AreEqual(0, Flips(after), $"면이 떨린다: {string.Join(",", after)}");
            Assert.AreEqual(UnitAnimDirection.East, after[after.Length - 1],
                "붙들긴 했는데 엉뚱한 면을 붙들었다");
        }

        /// <summary>45° **근처**(미세하게 기울어도)에서도 붙든다 — 경계선 위만 고치면 소용없다.</summary>
        [Test]
        public void 각도가_45도_근처여도_면을_붙든다()
        {
            foreach (float ty in new[] { 9.3f, 9.7f, 10.3f, 10.9f })
            {
                UnitAnimDirection[] after =
                    Walk(Vector2.zero, new Vector2(10f, ty), 0.1f, 12, stepped: true);
                Assert.LessOrEqual(Flips(after), 1,
                    $"목표 y={ty} 에서 면이 떨린다: {string.Join(",", after)}");
            }
        }

        /// <summary>
        /// ⚠️ **붙들기가 영영 붙들면 안 된다** — 축이 확실히 바뀌면 넘어간다.
        /// 거의 세로로만 가는 길이라 걸음이 연달아 세로로 나고, 합도 세로가 된다.
        /// </summary>
        [Test]
        public void 축이_확실히_바뀌면_넘어간다()
        {
            UnitAnimDirection[] after =
                Walk(Vector2.zero, new Vector2(0.2f, 10f), 0.1f, 12, stepped: true);
            Assert.AreEqual(UnitAnimDirection.North, after[after.Length - 1],
                $"위로 가는데 면이 안 돌았다: {string.Join(",", after)}");
        }

        /// <summary>넘어가는 데 **두 걸음**을 넘기지 않는다 — 늦게 돌면 등지고 걷는 것으로 보인다.</summary>
        [Test]
        public void 두_걸음_안에_넘어간다()
        {
            UnitAnimDirection[] after =
                Walk(Vector2.zero, new Vector2(0f, 10f), 0.1f, 4, stepped: true);
            Assert.AreEqual(UnitAnimDirection.North, after[1],
                $"세로로만 가는데 두 걸음 안에 안 돌았다: {string.Join(",", after)}");
        }

        /// <summary>
        /// ⚠️ **안 움직인 프레임이 기억을 지우지 않는다** — 지우면 다음 걸음이 다시
        /// 한 축짜리가 되어 떨림이 돌아온다. 0 을 끼워 넣어도 붙들어야 한다.
        /// </summary>
        [Test]
        public void 멈춘_프레임이_기억을_안_지운다()
        {
            UnitAnimDirection last = UnitAnimDirection.East;
            Vector2 prev = new Vector2(0.1f, 0f);
            last = StepFacing.Resolve(Vector2.zero, ref prev, last);
            Assert.AreEqual(UnitAnimDirection.East, last, "멈춘 프레임이 면을 바꿨다");
            Assert.AreEqual(new Vector2(0.1f, 0f), prev, "멈춘 프레임이 기억을 지웠다");

            last = StepFacing.Resolve(new Vector2(0f, 0.1f), ref prev, last);
            Assert.AreEqual(UnitAnimDirection.East, last,
                "멈춤을 지나고 나서 면이 뒤집혔다 — 기억이 끊겼다");
        }

        /// <summary>관성 배수는 여전히 한 곳에서 나온다 — 두 곳에 적지 않는다(지침 §7).</summary>
        [Test]
        public void 관성_값을_따로_안_든다()
        {
            Vector2 prev = Vector2.zero;
            Assert.AreEqual(
                DirectionHysteresis.Resolve(new Vector2(0.1f, 0f), UnitAnimDirection.North),
                StepFacing.Resolve(new Vector2(0.1f, 0f), ref prev, UnitAnimDirection.North),
                "직전 걸음이 0 이면 종전 규칙과 답이 같아야 한다");
        }
    }
}
