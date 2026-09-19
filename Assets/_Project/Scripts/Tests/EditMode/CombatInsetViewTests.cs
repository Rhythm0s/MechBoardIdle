using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **조립 화면 인셋이 주 화면보다 넓게 보면 안 된다**
    /// (2026-09-19 사용자 확정 · 리허설 1 ① — 「스폰 띠 밖 몬스터가 안 보이게」).
    ///
    /// 📌 지키는 것은 **수 하나가 아니라 관계**다 — 「인셋 ortho 가 2.4 인가」를 재면
    /// `combatSize` 가 바뀌는 날 시험이 거짓말을 한다. 재야 할 것은
    /// **두 카메라가 가로로 같은 폭을 보는가**이고, 그것은 어떤 화면비에서도 참이어야 한다.
    /// </summary>
    public sealed class CombatInsetViewTests
    {
        private const float D = 0.0001f;

        /// <summary>인셋 뷰포트의 가로세로비 — 가로는 화면 전부, 세로는 몫만큼이다.</summary>
        private static float InsetAspect(float mainAspect) =>
            mainAspect / CombatInsetView.HeightShare;

        [Test]
        public void 인셋이_가로로_주_카메라와_같은_폭을_본다()
        {
            // ⚠️ **화면비를 여럿 넣는다** — 식에서 비가 지워진다는 것이 이 규칙의 핵심이라,
            //    한 비에서만 맞는 것은 규칙이 아니라 우연이다.
            foreach (float mainAspect in new[] { 0.5625f, 0.75f, 1f, 1.7778f })
            {
                foreach (float mainOrtho in new[] { 5f, 8f, 12f })
                {
                    float mainHalfW = mainOrtho * mainAspect;
                    float insetHalfW = CombatInsetView.InsetOrthoSize(mainOrtho) * InsetAspect(mainAspect);
                    Assert.AreEqual(mainHalfW, insetHalfW, D,
                        $"비 {mainAspect} · ortho {mainOrtho} 에서 가로 폭이 다르다");
                }
            }
        }

        [Test]
        public void 갓_스폰한_적이_인셋에_안_들어온다()
        {
            // 스폰 띠는 로봇 기준 8~14 칸이다(`CombatTuning.spawnRingMin/MaxTbd` 자산 실측).
            // 인셋의 **모서리**까지가 그 최솟값보다 안쪽이어야 「스폰 띠 밖은 안 보인다」가 선다 —
            // ⚠️ 가로·세로 반폭만 재면 **모서리 쪽에서 적이 화면 안에 선다**
            //    (`SpawnRingRule.RadiusFromView` 가 대각선을 쓰는 것과 같은 까닭이다).
            const float mainOrtho = 8f, mainAspect = 0.5625f, bandMin = 8f;

            float halfH = CombatInsetView.InsetOrthoSize(mainOrtho);
            float halfW = halfH * InsetAspect(mainAspect);
            float corner = new Vector2(halfW, halfH).magnitude;

            Assert.Less(corner, bandMin,
                $"인셋 모서리 {corner:F2} 칸이 띠 최솟값 {bandMin} 안에 있으면 갓 스폰한 적이 보인다");
        }

        [Test]
        public void 종전처럼_주_카메라_값을_그대로_쓰면_띠가_통째로_보인다()
        {
            // 🗑️ **폐기된 거동을 적어 둔다** — 왜 고쳤는지가 시험에 남아야 되돌아가지 않는다.
            //    ortho 8 을 그대로 쓰면 인셋 가로 반폭이 15 칸이라 띠(8~14)가 다 든다.
            const float mainOrtho = 8f, mainAspect = 0.5625f;
            float oldHalfW = mainOrtho * InsetAspect(mainAspect);

            Assert.AreEqual(15f, oldHalfW, D, "종전 실측 — 인셋 가로 반폭 15 칸");
            Assert.Greater(oldHalfW, 14f, "띠 최댓값까지 보였다는 사실");
        }

        [Test]
        public void 바닥_그림은_주_카메라_폭만_깐다()
        {
            // 🗑️ 넓히던 것 폐기(2026-09-19) — 인셋이 더 넓게 보지 않으므로 넓힐 근거가 없다.
            const float halfH = 8f, mainAspect = 0.5625f;
            Assert.AreEqual(halfH * mainAspect,
                CombatInsetView.BackgroundHalfWidth(halfH, mainAspect), D);
        }

        [Test]
        public void 인셋은_경고_띠와_안_겹친다()
        {
            // 종전 규약을 그대로 둔다 — 배율을 바꾼 것이 자리까지 건드리지 않았음을 지킨다.
            Assert.IsTrue(CombatInsetView.ClearsBand(1440f, 2560f));
        }
    }
}
