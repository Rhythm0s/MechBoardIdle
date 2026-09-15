using System.Collections.Generic;
using MBI.Core;
using MBI.Core.Combat;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **적은 접촉하면 때린다** (2026-09-15 · 육안 ⑤ 결함).
    ///
    /// ⚠️ **무슨 일이 있었나.** 사거리를 **중심 사이 거리**와 견줬는데, 적은 반경 합까지만
    /// 다가갈 수 있다(`GridMovement.IsBlocked`). 로봇 0.666 + 보병 0.334 = **1.000** 이고
    /// 보병 사거리도 **1.000** 이라 둘이 정확히 같았다 — 적은 늘 「조금 먼」 쪽에 서서
    /// **영원히 이동 분기에 머물렀다.**
    ///
    /// 실측(30초 · 로봇 정지): 고치기 전 **로봇 HP 가 한 대도 안 깎였고**, 고친 뒤 990 깎였다.
    /// </summary>
    public sealed class EnemyReachTests
    {
        private static CombatSimulation Sim(float enemyRange, float enemyRadius,
            float robotRadius, float startDistance)
        {
            var setup = new RobotSetup
            {
                hp = 1000f,
                mountCoef = 1f,
                moduleMult = 1f,
                attackRange = 0f,               // 로봇은 안 쏜다 — 이 시험은 **적**만 본다
                radius = robotRadius,
                lines = new List<AmmoLine>(),
                ammoCapacity = 1f,
            };

            var spawns = new List<EnemySpawn>
            {
                new EnemySpawn
                {
                    label = "보병", hp = 100f, def = 0f, atk = 10f,
                    moveSpeed = 0f,             // **안 움직인다** — 자리를 시험이 정한다
                    attackRange = enemyRange,
                    attackInterval = 1f,
                    radius = enemyRadius,
                },
            };

            var sim = new CombatSimulation(setup, spawns, 999f, 999f, 0f);
            sim.SetSpawnBand(startDistance, startDistance);
            return sim;
        }

        [Test]
        public void 반경_합과_사거리가_같아도_때린다()
        {
            // 09-15 에 실제로 터진 값 그대로 — 접촉 1.000 · 사거리 1.000.
            CombatSimulation sim = Sim(1f, 0.334f, 0.666f, 1f);

            float before = sim.Robot.hp;
            for (int i = 0; i < 60; i++) sim.Tick(0.05f);

            Assert.That(sim.Robot.hp, Is.LessThan(before),
                "접촉했는데 못 때리면 적이 영영 붙어만 있는다 — 09-15 육안 ⑤ 가 그것이다");
        }

        [Test]
        public void 사거리_밖이면_안_때린다()
        {
            // 표면 사이 거리가 사거리를 넘는 자리. reach = 1 + 0.666 + 0.334 = 2.0 이므로
            // 3.0 에 세우면 닿지 않는다.
            CombatSimulation sim = Sim(1f, 0.334f, 0.666f, 3f);

            float before = sim.Robot.hp;
            for (int i = 0; i < 60; i++) sim.Tick(0.05f);

            Assert.That(sim.Robot.hp, Is.EqualTo(before),
                "닿지도 않는 거리에서 맞으면 사거리가 뜻을 잃는다");
        }

        [Test]
        public void 큰_적이_더_불리해지지_않는다()
        {
            // ⚠️ 중심 거리로 재면 **반경이 큰 적일수록 사거리가 짧아진다** — 보스가
            // 덩치 때문에 더 못 때리게 된다. 그 이상함이 이 결함의 뿌리였다.
            CombatSimulation big = Sim(1f, 1.5f, 0.666f, 1.5f + 0.666f);

            float before = big.Robot.hp;
            for (int i = 0; i < 60; i++) big.Tick(0.05f);

            Assert.That(big.Robot.hp, Is.LessThan(before),
                "덩치가 큰 적도 접촉하면 때려야 한다");
        }
    }
}
