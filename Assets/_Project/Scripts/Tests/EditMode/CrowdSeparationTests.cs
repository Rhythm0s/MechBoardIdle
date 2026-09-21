using System.Collections.Generic;
using MBI.Core;
using MBI.Core.Combat;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **겹치면 서로 밀어 틈을 만든다** (2026-09-21 사용자 확정 · 육안 ⑤ⓐ).
    ///
    /// 09-21 에 곁눈질 예산을 고치고도 무리가 섰다 — 진짜 벽은 **끼임**이었고,
    /// 그것은 값이 아니라 규칙이었다. 사용자가 규칙을 바꿨다(09-16 「적끼리 밀지
    /// 않음」 뒤집힘).
    ///
    /// 📌 **되돌릴 수 있는지를 첫 줄에서 지킨다** — 세기 0 이 구 거동이어야
    /// 「켤지 말지」가 값 하나의 문제로 남는다.
    /// </summary>
    public sealed class CrowdSeparationTests
    {
        private const float Dt = 1f / 60f;

        private static CombatEntity At(Vector2 p, float radius = 0.334f) =>
            new CombatEntity { position = p, hp = 1f, maxHp = 1f, radius = radius };

        [Test]
        public void 세기가_0_이면_구_거동대로_안_민다()
        {
            CombatEntity a = At(Vector2.zero), b = At(new Vector2(0.1f, 0f));
            var all = new List<CombatEntity> { a, b };

            CrowdSeparation.Resolve(all, 0f, Dt);

            Assert.AreEqual(Vector2.zero, a.position);
            Assert.AreEqual(new Vector2(0.1f, 0f), b.position);
        }

        [Test]
        public void 겹친_둘이_서로_멀어진다()
        {
            CombatEntity a = At(Vector2.zero), b = At(new Vector2(0.1f, 0f));
            var all = new List<CombatEntity> { a, b };
            float before = (b.position - a.position).magnitude;

            CrowdSeparation.Resolve(all, 6f, Dt);

            Assert.Greater((b.position - a.position).magnitude, before, "안 벌어졌다");
            Assert.Less(a.position.x, 0f, "a 가 반대쪽으로 밀려야 한다");
            Assert.Greater(b.position.x, 0.1f, "b 가 반대쪽으로 밀려야 한다");
        }

        /// <summary>안 겹친 둘은 건드리지 않는다 — 미는 것이지 흩는 것이 아니다.</summary>
        [Test]
        public void 안_겹쳤으면_안_민다()
        {
            CombatEntity a = At(Vector2.zero), b = At(new Vector2(5f, 0f));
            CrowdSeparation.Resolve(new List<CombatEntity> { a, b }, 6f, Dt);

            Assert.AreEqual(Vector2.zero, a.position);
            Assert.AreEqual(new Vector2(5f, 0f), b.position);
        }

        /// <summary>
        /// 정확히 포개져도 갈라진다 — 방향이 없는 자리라 그냥 두면 영영 포갠 채다.
        /// ⚠️ 흔드는 값은 **자리에서** 만든다(`Random` 이면 판마다 그림이 달라진다).
        /// </summary>
        [Test]
        public void 정확히_포개져도_갈라진다()
        {
            CombatEntity a = At(Vector2.zero), b = At(Vector2.zero);
            CrowdSeparation.Resolve(new List<CombatEntity> { a, b }, 6f, Dt);

            Assert.Greater((b.position - a.position).magnitude, 0f, "포갠 채로 남았다");
        }

        /// <summary>한 틱에 튀지 않는다 — 빽빽한 무리가 터지듯 흩어지면 안 된다.</summary>
        [Test]
        public void 한_틱에_상한보다_더_안_민다()
        {
            CombatEntity a = At(Vector2.zero), b = At(new Vector2(0.001f, 0f));
            CrowdSeparation.Resolve(new List<CombatEntity> { a, b }, 100000f, Dt);

            Assert.LessOrEqual(a.position.magnitude, CrowdSeparation.MaxPerTick + 1e-4f);
            Assert.LessOrEqual((b.position - new Vector2(0.001f, 0f)).magnitude,
                CrowdSeparation.MaxPerTick + 1e-4f);
        }

        /// <summary>
        /// **끼인 무리가 풀린다** — ⑤ 가 화면에서 보였던 그림이다.
        ///
        /// 서로 겹치도록 촘촘히 세운 스물다섯(0.2 간격 · 반경 0.334 라 이웃마다 겹친다)이
        /// 몇 초 뒤에는 **아무도 안 겹친다.**
        ///
        /// ⚠️ **6초는 재서 넣은 수다.** 처음에 3초로 썼다가 34 쌍이 남아 걸렸는데,
        /// 재 보니 **세기 6 에서 3초 34쌍 → 6초 0쌍**이었다(12 면 3초에 3쌍, 20 이면 0).
        /// 시험이 틀렸던 것이지 규칙이 느렸던 것이 아니다 — 스물다섯이 0.2 에서
        /// 0.668 로 벌어지려면 판이 세 배 넘게 커져야 한다.
        ///
        /// 📌 **화면에서는 그동안도 무리가 다가온다** — 여기서 재는 것은 「언젠가
        /// 풀리는가」이지 「즉시 풀리는가」가 아니다.
        /// </summary>
        [Test]
        public void 끼인_무리가_풀린다()
        {
            var all = new List<CombatEntity>();
            for (int i = 0; i < 25; i++)
                all.Add(At(new Vector2(i % 5 * 0.2f, i / 5 * 0.2f)));

            for (int t = 0; t < 60 * 6; t++) CrowdSeparation.Resolve(all, 6f, Dt);

            int overlapping = 0;
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                    if ((all[j].position - all[i].position).magnitude
                        < all[i].radius + all[j].radius - 1e-3f) overlapping++;

            Assert.AreEqual(0, overlapping, $"6초 뒤에도 {overlapping} 쌍이 겹쳐 있다");
        }
    }
}
