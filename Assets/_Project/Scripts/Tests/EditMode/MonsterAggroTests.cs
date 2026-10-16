using System.Collections.Generic;
using MBI.Core;
using MBI.Core.Combat;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **도전 층 전원 스폰 · 몬스터 탐색 범위** (사용자 확정 #152 · 2026-10-16).
    /// </summary>
    public sealed class MonsterAggroTests
    {
        // 쏘지 않는 로봇 — 이 파일은 몬스터 거동만 본다.
        private static RobotSetup Idle() => new RobotSetup
        {
            hp = 1000f, mountCoef = 1f, moduleMult = 1f, attackRange = 0f, radius = 0.5f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = new List<AmmoLine>(), ammoCapacity = 40f, ammoStore = new AmmoInventory(40f),
        };

        private static List<EnemySpawn> Walkers(int n, float projectileSpeed = 0f)
        {
            var l = new List<EnemySpawn>();
            for (int i = 0; i < n; i++)
                l.Add(new EnemySpawn { label = "보병", hp = 10f, def = 0f, atk = 5f, moveSpeed = 1.5f,
                    attackRange = projectileSpeed > 0f ? 7f : 1f, attackInterval = 1f, radius = 0.3f,
                    projectileSpeed = projectileSpeed });
            return l;
        }

        private static void Run(CombatSimulation sim, float seconds, float dt = 0.1f)
        {
            int steps = Mathf.CeilToInt(seconds / dt);
            for (int i = 0; i < steps && sim.Result == CombatResult.InProgress; i++) sim.Tick(dt);
        }

        [Test]
        public void 도전_층은_첫_틱에_전원이_선다()
        {
            var sim = new CombatSimulation(Idle(), Walkers(30), 12f, 60f, 1f) { SpawnAllAtOnce = true };
            sim.SetWave(5, 10f);   // 묶음 규칙이 있어도 안 본다
            sim.Tick(0.1f);
            Assert.AreEqual(0, sim.Unspawned);
            Assert.AreEqual(30, sim.Remaining);
        }

        [Test]
        public void 한꺼번에_낸_적은_서로_겹치지_않는다()
        {
            var sim = new CombatSimulation(Idle(), Walkers(60), 3f, 60f, 1f) { SpawnAllAtOnce = true };
            sim.Tick(0.01f);
            IReadOnlyList<CombatEntity> es = sim.Enemies;
            for (int i = 0; i < es.Count; i++)
                for (int j = i + 1; j < es.Count; j++)
                    Assert.GreaterOrEqual((es[i].position - es[j].position).magnitude,
                        es[i].radius + es[j].radius - 0.001f, $"{i}·{j} 겹침");
        }

        [Test]
        public void 탐색_범위_밖이면_제자리이고_안_때린다()
        {
            var sim = new CombatSimulation(Idle(), Walkers(4), 12f, 60f, 0f) { SpawnAllAtOnce = true };
            sim.SetAggroRange(5f);   // 띠 12 > 5
            sim.Tick(0.01f);
            var before = new List<Vector2>();
            foreach (CombatEntity e in sim.Enemies) before.Add(e.position);
            Run(sim, 5f);
            for (int i = 0; i < before.Count; i++)
                Assert.AreEqual(before[i], sim.Enemies[i].position, $"{i} 이 움직였다");
            Assert.AreEqual(1000f, sim.Robot.hp, 0.001f);
        }

        [Test]
        public void 원거리형도_탐색_전에는_안_쏜다()
        {
            var sim = new CombatSimulation(Idle(), Walkers(4, projectileSpeed: 4f), 6f, 60f, 0f) { SpawnAllAtOnce = true };
            sim.SetAggroRange(3f);   // 띠 6 · 포탄 사거리 7 이지만 깨기 전이라 안 쏜다
            Run(sim, 5f);
            Assert.AreEqual(1000f, sim.Robot.hp, 0.001f);
            Assert.AreEqual(0, sim.EnemyProjectiles.Count);
        }

        [Test]
        public void 범위에_들면_깨어_다가오고_되돌아가지_않는다()
        {
            var sim = new CombatSimulation(Idle(), Walkers(1), 6f, 60f, 0f) { SpawnAllAtOnce = true };
            sim.SetAggroRange(8f);   // 띠 6 < 8 — 바로 깬다
            sim.Tick(0.01f);
            CombatEntity e = sim.Enemies[0];
            float d0 = (e.position - sim.Robot.position).magnitude;
            Run(sim, 1f);
            Assert.IsTrue(e.aggro);
            Assert.Less((e.position - sim.Robot.position).magnitude, d0);

            sim.SetAggroRange(0.5f);  // 범위를 줄여도 깬 적은 계속 쫓는다
            float d1 = (e.position - sim.Robot.position).magnitude;
            Run(sim, 1f);
            Assert.LessOrEqual((e.position - sim.Robot.position).magnitude, d1 + 0.001f);
        }

        [Test]
        public void 범위_0_이면_종전처럼_처음부터_다가온다()
        {
            var sim = new CombatSimulation(Idle(), Walkers(1), 12f, 60f, 0f) { SpawnAllAtOnce = true };
            sim.Tick(0.01f);
            float d0 = (sim.Enemies[0].position - sim.Robot.position).magnitude;
            Run(sim, 1f);
            Assert.Less((sim.Enemies[0].position - sim.Robot.position).magnitude, d0);
        }

        [Test]
        public void 러너와_하네스가_같은_문을_지난다()
        {
            string runner = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            string harness = System.IO.File.ReadAllText("Assets/_Project/Scripts/Editor/StageClearHarness.cs");
            StringAssert.Contains("_sim.SetAggroRange(tuning.enemyAggroRangeTbd);", runner);
            StringAssert.Contains("_sim.SpawnAllAtOnce = true;", runner);
            Assert.AreEqual(2, System.Text.RegularExpressions.Regex.Matches(harness, @"sim\.SetAggroRange\(tuning\.enemyAggroRangeTbd\)").Count);
        }
    }
}
