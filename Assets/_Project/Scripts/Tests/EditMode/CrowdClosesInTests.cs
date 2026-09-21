using System.Collections.Generic;
using MBI.Core;
using MBI.Core.Combat;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **무리가 뭉쳐 서 있으면 안 된다** (2026-09-21 사용자 육안 ⑤ · S2 스크린샷).
    ///
    /// ⚠️⚠️ 이 자리를 09-19 에 한 번 봤고 **놓쳤다.** 그때 하네스가 낸 수는
    /// 「안 막혔는데 정지 = 0」이었고 나는 그것으로 「길막 규칙이지 결함이 아니다」라고
    /// 적었다. 잰 것은 **「막혔는가」**였는데 물었어야 할 것은
    /// **「막힌 놈이 돌아갈 수 있는가」**였다 — 둘은 다른 물음이다.
    ///
    /// 📌 그래서 이 시험은 **막힘**을 안 센다. **가까워지는가**를 센다.
    /// </summary>
    public sealed class CrowdClosesInTests
    {
        private const float Dt = 1f / 60f;

        /// <summary>
        /// 쏘지 않는 로봇 — 탄 라인이 비어 아무도 안 죽는다. 이 시험은 **움직임**만 본다.
        /// (적이 죽어 없어지면 「다가왔는지」가 죽음에 가려진다.)
        /// </summary>
        private static RobotSetup Robot(float hp) => new RobotSetup
        {
            hp = hp,
            mountCoef = 1f,
            moduleMult = 1f,
            attackRange = 0f,
            radius = 0.666f,
            multiShotCount = 1,
            aoeRadius = 0f,
            aoeSplashFactor = 1f,
            lines = new List<AmmoLine>(),
            ammoCapacity = 0f,
        };

        /// <summary>한 무리 — 느리고 약해서 **다가오는 것 말고는** 할 일이 없다.</summary>
        private static List<EnemySpawn> Crowd(int n)
        {
            var list = new List<EnemySpawn>(n);
            for (int i = 0; i < n; i++)
                list.Add(new EnemySpawn
                {
                    label = "보병", hp = 100000f, def = 0f, atk = 0f,
                    moveSpeed = 1.5f, attackRange = 1f, attackInterval = 1f,
                    radius = 0.334f,
                });
            return list;
        }

        private static CombatEntity At(Vector2 p, float radius = 0.3f) =>
            new CombatEntity { position = p, hp = 1f, maxHp = 1f, radius = radius };

        private static float Nearest(CombatSimulation sim)
        {
            float best = float.MaxValue;
            foreach (CombatEntity e in sim.Enemies)
                if (e.IsAlive)
                    best = Mathf.Min(best, (e.position - sim.RobotPosition).magnitude);
            return best;
        }

        /// <summary>
        /// 서른 기가 한꺼번에 나와 저희끼리 막는 판. **다 못 와도 좋다 — 앞줄은 와야 한다.**
        ///
        /// 고치기 전에는 예산이 0 으로 태어나 뒷줄이 통째로 굳었고, 화면에서는
        /// 「무리가 오른쪽에 뭉쳐 정지」로 보였다.
        /// </summary>
        [Test]
        public void 무리가_로봇에게_다가온다()
        {
            var sim = new CombatSimulation(Robot(100000f), Crowd(30),
                arenaRadius: 12f, challengeTime: 120f, spawnCadence: 0f);
            sim.SetSideDetourCells(3f);

            float before = Nearest(sim);
            for (int i = 0; i < 60 * 8; i++) sim.Tick(Dt);   // 8초
            float after = Nearest(sim);

            Assert.Less(after, before,
                $"8초가 지나도 아무도 안 가까워졌다 — {before:F2} → {after:F2}");
        }

        /// <summary>
        /// 앞을 **무리가 길게** 막은 판 — 반경이 안 겹치게 0.68 간격으로 세운다(진짜 적처럼).
        ///
        /// ⚠️ 막는 것을 하나만 두면 굳는 판이 안 만들어진다 — 적이 그 하나를 지나쳐
        ///    앞이 뚫리고 예산이 다시 찬다. 처음에 그렇게 쓰고 **헛통과**를 받았다.
        /// </summary>
        private static List<CombatEntity> Wall(CombatEntity self)
        {
            var all = new List<CombatEntity> { self };
            for (int i = -9; i <= 9; i++)
                all.Add(At(new Vector2(0.7f, i * 0.68f), 0.334f));
            return all;
        }

        /// <summary>뒤쪽 절반 동안 **몇 틱이나 움직였는가**.</summary>
        private static int MovedLate(float recoverPerSecond)
        {
            CombatEntity self = At(Vector2.zero, 0.334f);
            List<CombatEntity> all = Wall(self);
            var target = new Vector2(5f, 0f);

            float hold = 0f, budget = 3f;
            Vector2 dir = Vector2.zero;
            const float dt = 1f / 60f, step = 1.5f * dt;
            const int total = 60 * 15;

            int late = 0;
            for (int i = 0; i < total; i++)
            {
                Vector2? next = GridMovement.StepOrSide(self.position, target, step, self.radius,
                    self, all, null, ref hold, ref dir, 0.4f, dt,
                    ref budget, budgetCells: 3f, recoverPerSecond: recoverPerSecond);
                if (next.HasValue) self.position = next.Value;
                if (i >= total / 2 && next.HasValue) late++;
            }
            return late;
        }

        /// <summary>
        /// **예산이 돌아오면 더 오래 비벼 본다** (2026-09-21 육안 ⑤).
        ///
        /// ⚠️⚠️ **이것으로 ⑤ 가 다 고쳐진다고 적지 않는다.** 실측에서 둘 다 결국
        /// 무리 사이 **주머니에 끼여** 선다 — 앞도 양옆도 막힌 자리라 예산과 무관하다.
        /// 그 끼임은 「밀어내기 없음 · 경로 탐색 없음 · 4방향」이라는 **규칙**의 결과이고,
        /// 그것을 바꾸는 것은 사람이 고를 일이다(밸런스가 통째로 움직인다).
        ///
        /// 📌 여기서 지키는 것은 하나다 — **다 쓴 예산이 영영 안 돌아오지는 않는다.**
        /// </summary>
        [Test]
        public void 예산이_돌아오면_더_오래_비벼_본다()
        {
            int with = MovedLate(3f / 2f);
            int without = MovedLate(0f);
            Assert.Greater(with, without,
                $"회복이 있는 쪽이 더 못 움직였다 — 있음 {with} · 없음 {without}");
        }

    }
}
