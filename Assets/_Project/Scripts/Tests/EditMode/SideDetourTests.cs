using System.Collections.Generic;
using MBI.Core;
using MBI.Core.Combat;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **곁눈질 예산 — 우회를 더 강하게** (2026-09-19 · 리허설 1 ② 후속).
    ///
    /// ✅ **켰다 — 3칸이 출하 판이다**(2026-09-19 사용자 확정 · `enemySideDetourCellsTbd = 3`).
    ///
    /// ⚠️⚠️ **밸런스가 움직였다.** 하네스 실측으로 S2·S5 가 LoseTimeout(살아남음)에서
    ///    **LoseDead** 로 바뀌었고 S3 는 **23.0초** · S4 는 **24.3초** 빨리 죽는다.
    ///    (⚠️ 「20 초씩」이라 적었던 것은 **어림한 수**다 — 표의 수를 안 뺐다. 사용자가 **어려워짐을
    ///    감수하고** 켰다) — 되돌리려면 자산 값 하나다. 등가선·요구치 재산출의 입력이다.
    ///
    /// 📌 **0 = 종전 거동**이라는 것이 이 시험의 첫 줄이다. 그것이 참이어야
    ///    「켤지 말지」가 값 하나의 문제로 남는다.
    /// </summary>
    public sealed class SideDetourTests
    {
        private const float D = 0.0001f;
        private const float Hold = 0.4f;
        private const float Dt = 0.05f;

        private static CombatEntity At(Vector2 p, float radius = 0.3f) =>
            new CombatEntity { position = p, hp = 1f, maxHp = 1f, radius = radius };

        /// <summary>한 발(0.075칸)만큼 앞으로 가면 닿는 거리. 반경 합 0.6 바로 바깥이다.</summary>
        private const float JustAhead = 0.65f;

        /// <summary>
        /// 표적과 **같은 가로선**에 서고 바로 앞이 막힌 판 — 굳음이 가장 잘 나던 자리다.
        ///
        /// ⚠️ **막는 것을 반경 합(0.6) 안쪽에 두면 안 된다** — 그러면 처음부터 겹쳐 있어
        ///    **옆칸까지 다 막힌** 판이 되고, 시험이 「우회가 안 된다」를 거짓으로 보고한다.
        ///    0.65 에 두면 앞(0.075)은 0.575 로 막히고 옆(0.075)은 0.654 로 뚫린다.
        /// </summary>
        private static (CombatEntity self, List<CombatEntity> all, Vector2 target) Walled()
        {
            CombatEntity self = At(Vector2.zero);
            CombatEntity wall = At(new Vector2(JustAhead, 0f));
            var all = new List<CombatEntity> { self, wall };
            return (self, all, new Vector2(5f, 0f));            // 표적은 정동쪽 — 부 축 델타 0
        }

        [Test]
        public void 예산이_0_이면_종전처럼_선다()
        {
            // 종전 식은 부 축 허용량이 **표적까지 남은 양**이라, 한 축에 서면 0 이 되어
            // 곁눈질이 제자리걸음이었다. 그 거동을 그대로 지킨다.
            (CombatEntity self, List<CombatEntity> all, Vector2 target) = Walled();
            float hold = 0f, budget = 0f;
            Vector2 dir = Vector2.zero;

            Vector2? next = GridMovement.StepOrSide(self.position, target, 0.075f, self.radius,
                self, all, null, ref hold, ref dir, Hold, Dt, ref budget, budgetCells: 0f);

            Assert.IsNull(next, "예산이 없으면 그대로 선다");
        }

        [Test]
        public void 예산이_있으면_한_축에_서도_돌아간다()
        {
            (CombatEntity self, List<CombatEntity> all, Vector2 target) = Walled();
            float hold = 0f, budget = 3f;
            Vector2 dir = Vector2.zero;

            Vector2? next = GridMovement.StepOrSide(self.position, target, 0.075f, self.radius,
                self, all, null, ref hold, ref dir, Hold, Dt, ref budget, budgetCells: 3f);

            Assert.IsNotNull(next, "옆이 비었는데 안 갔다");
            Assert.Greater(Mathf.Abs(next.Value.y), D, "부 축으로 움직여야 한다");
            Assert.AreEqual(0f, next.Value.x, D, "주 축은 막혀 있다 — 대각선으로 가지 않는다");
            Assert.Less(budget, 3f, "간 만큼 예산이 깎여야 한다");
        }

        [Test]
        public void 표적_쪽이_막히면_반대쪽을_본다()
        {
            // ⚠️ **종전에는 여기서 포기했다** — 무리가 표적 쪽을 메우면 영영 못 돌아갔다.
            CombatEntity self = At(Vector2.zero);
            var all = new List<CombatEntity>
            {
                self,
                At(new Vector2(JustAhead, 0f)),   // 앞
                At(new Vector2(0f, 0.67f)),       // 표적 쪽(위) — 표적이 북동쪽이라 이쪽을 먼저 본다
            };
            var target = new Vector2(5f, 1f);

            float hold = 0f, budget = 3f;
            Vector2 dir = Vector2.zero;
            Vector2? next = GridMovement.StepOrSide(self.position, target, 0.075f, self.radius,
                self, all, null, ref hold, ref dir, Hold, Dt, ref budget, budgetCells: 3f);

            Assert.IsNotNull(next, "반대쪽이 비었는데 안 갔다");
            Assert.Less(next.Value.y, 0f, "표적 쪽이 막혔으니 반대쪽(아래)으로 간다");
        }

        [Test]
        public void 예산을_다_쓰면_선다()
        {
            (CombatEntity self, List<CombatEntity> all, Vector2 target) = Walled();
            float hold = 0f, budget = 0f;      // 다 쓴 상태
            Vector2 dir = Vector2.zero;

            Vector2? next = GridMovement.StepOrSide(self.position, target, 0.075f, self.radius,
                self, all, null, ref hold, ref dir, Hold, Dt, ref budget, budgetCells: 3f);

            Assert.IsNull(next, "예산이 다했으면 옆이 비어도 안 간다 — 하염없이 미끄러지지 않는다");
        }

        [Test]
        public void 앞이_뚫리면_예산이_다시_찬다()
        {
            CombatEntity self = At(Vector2.zero);
            var all = new List<CombatEntity> { self };          // 막는 것이 없다
            float hold = 1f, budget = 0.2f;
            Vector2 dir = Vector2.up;

            Vector2? next = GridMovement.StepOrSide(self.position, new Vector2(5f, 0f), 0.075f,
                self.radius, self, all, null, ref hold, ref dir, Hold, Dt, ref budget, budgetCells: 3f);

            Assert.IsNotNull(next);
            Assert.AreEqual(3f, budget, D, "주 축이 뚫렸으면 예산을 다시 채운다");
            Assert.AreEqual(0f, hold, D, "붙들기도 끝난다");
            Assert.AreEqual(Vector2.zero, dir, "붙들던 방향도 놓는다");
        }

        [Test]
        public void 밀어내지_않는다()
        {
            // ⚠️ **사용자 확정 불변** — 우회를 강하게 해도 막힌 칸에는 못 들어간다.
            CombatEntity self = At(Vector2.zero);
            var all = new List<CombatEntity>
            {
                self,
                At(new Vector2(JustAhead, 0f)),
                At(new Vector2(0f, 0.67f)), At(new Vector2(0f, -0.67f)),
            };

            float hold = 0f, budget = 3f;
            Vector2 dir = Vector2.zero;
            Vector2? next = GridMovement.StepOrSide(self.position, new Vector2(5f, 0.5f), 0.075f,
                self.radius, self, all, null, ref hold, ref dir, Hold, Dt, ref budget, budgetCells: 3f);

            Assert.IsNull(next, "세 방향이 다 막혔으면 선다 — 밀어내기는 없다");
        }
    }
}
