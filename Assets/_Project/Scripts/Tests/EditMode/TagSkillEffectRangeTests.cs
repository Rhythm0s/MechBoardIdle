using MBI.Combat;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **태그 스킬 연출이 화면 전체를 덮는가** (2026-09-18 사용자 확정 · 육안 뒤).
    ///
    /// 판정은 09-08 부터 「화면 안의 적 전부」였다. 연출은 그 뒤로 세 번 바뀌었고
    /// 🗑️ 앞의 둘은 폐기됐다 — 「한 바퀴 도는 부채꼴」(09-16) · 「적 무리 방향 한 번」(09-18 오전) ·
    /// 「원점에서 360도로 뻗기」(09-18 오후 · 같은 날 육안에서 **소용돌이로 보인다**고 뒤집혔다).
    ///
    /// ⚠️⚠️ **여기서 지키는 것은 하나다 — 그림에 원점이 없을 것.** 원점에서 뻗으면
    /// 줄기를 몇으로 늘리든 방사형(소용돌이·폭죽)으로 읽힌다. 「화면 전체」는 방사가 아니라
    /// **덮기**다. 그래서 자리 고르기가 **화면 사각형 안에 고르게** 퍼지는지를 잰다.
    ///
    /// 배향·두께·밝기가 화면에서 어떻게 보이는지는 여기서 못 본다 — 그것은 육안이다.
    /// </summary>
    public sealed class TagSkillEffectRangeTests
    {
        private static readonly Rect Screen = new Rect(-9f, -16f, 18f, 32f);

        [Test]
        public void 뿌린_자리가_전부_화면_안이다()
        {
            // 화면 밖에 떨어지면 **판정이 안 닿는 곳에 그림이 간다** — 09-16 에 주황 사각이
            // 화면 절반을 덮었을 때와 같은 병이다(그림이 판정보다 넓다).
            Vector2[] spots = TagSkillEffect.ScatterPositions(Screen, 30);

            Assert.AreEqual(30, spots.Length);
            foreach (Vector2 p in spots)
                Assert.IsTrue(Screen.Contains(p), $"{p} 가 화면 밖이다");
        }

        [Test]
        public void 네_귀퉁이가_다_덮인다()
        {
            // 「화면 전체」의 뜻이 여기 있다 — 가운데만 촘촘하면 원점이 있는 그림과 같아진다.
            Vector2[] spots = TagSkillEffect.ScatterPositions(Screen, 36);

            int lb = 0, rb = 0, lt = 0, rt = 0;
            foreach (Vector2 p in spots)
            {
                bool left = p.x < Screen.center.x;
                bool down = p.y < Screen.center.y;
                if (left && down) lb++;
                else if (!left && down) rb++;
                else if (left) lt++;
                else rt++;
            }

            Assert.Greater(lb, 0, "왼쪽 아래가 비었다");
            Assert.Greater(rb, 0, "오른쪽 아래가 비었다");
            Assert.Greater(lt, 0, "왼쪽 위가 비었다");
            Assert.Greater(rt, 0, "오른쪽 위가 비었다");
        }

        [Test]
        public void 자리가_안_겹친다()
        {
            // 칸마다 하나씩이라 같은 자리에 둘이 서면 흔들림이 아니라 **계산이 틀린 것**이다.
            Vector2[] spots = TagSkillEffect.ScatterPositions(Screen, 25);

            for (int i = 0; i < spots.Length; i++)
                for (int j = i + 1; j < spots.Length; j++)
                    Assert.Greater((spots[i] - spots[j]).sqrMagnitude, 1e-6f,
                        $"{i} 와 {j} 가 같은 자리다");
        }

        [Test]
        public void 같은_입력이면_같은_그림이다()
        {
            // ⚠️ `Random` 을 쓰면 판마다 다른 그림이 나와 **시험이 값을 못 잰다.**
            Vector2[] a = TagSkillEffect.ScatterPositions(Screen, 30);
            Vector2[] b = TagSkillEffect.ScatterPositions(Screen, 30);

            for (int i = 0; i < a.Length; i++) Assert.AreEqual(a[i], b[i]);
            Assert.AreEqual(TagSkillEffect.Jitter(7), TagSkillEffect.Jitter(7));
        }

        [Test]
        public void 흔들림은_0과_1_사이다()
        {
            // 범위를 벗어나면 칸 밖으로 튀어 화면 밖에 떨어진다.
            for (int i = 0; i < 200; i++)
            {
                float v = TagSkillEffect.Jitter(i);
                Assert.GreaterOrEqual(v, 0f);
                Assert.Less(v, 1f);
            }
        }
    }
}
