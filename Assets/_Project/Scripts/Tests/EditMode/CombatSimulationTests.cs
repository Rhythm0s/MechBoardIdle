using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 실시간 탑뷰 전투 시뮬 메커니즘 검증(밸런스 시간 단정이 아니라 승/패/타임아웃·데미지·결정론).
    /// 타이밍/HP는 TBD placeholder이므로 정확한 초수는 단정하지 않는다(§7).
    /// </summary>
    public sealed class CombatSimulationTests
    {
        // 대표 상태 물류 생산율(pA 1/1/2) → 폭발×2+분열×1+관통×1 = 145/초.
        private static List<AllocatedShot> RepresentativeShots()
        {
            var weapons = new List<WeaponSpec>
            {
                new WeaponSpec(AmmoKind.Pierce, 20f, 1f),
                new WeaponSpec(AmmoKind.Split, 25f, 1f),
                new WeaponSpec(AmmoKind.Explosive, 50f, 2f),
            };
            return ShotAllocator.AllocatePerSecond(weapons, 6f); // 145/초
        }

        private static RobotSetup Robot(float hp) => new RobotSetup
        {
            hp = hp,
            mountCoef = 1f,
            moduleMult = 1f,
            attackRange = 100f, // 아레나 전체 커버
            multiShotCount = 1, // 단일 타겟(이 테스트는 메커니즘 검증 — 패턴은 HitResolverTests)
            aoeRadius = 0f,
            aoeSplashFactor = 1f,
            shots = RepresentativeShots(),
        };

        private static void Run(CombatSimulation sim, float seconds, float dt = 0.1f)
        {
            int steps = Mathf.CeilToInt(seconds / dt);
            for (int i = 0; i < steps && sim.Result == CombatResult.InProgress; i++)
                sim.Tick(dt);
        }

        [Test]
        public void Win_WhenAllEnemiesDead()
        {
            // 정지 표적 1기 hp100 def0 atk0 — 로봇 200/초 → 1초 내 격파.
            var spawns = new List<EnemySpawn>
            {
                new EnemySpawn { label = "표적", hp = 100f, def = 0f, atk = 0f,
                    moveSpeed = 0f, attackRange = 0.5f, attackInterval = 1f },
            };
            var sim = new CombatSimulation(Robot(1000f), spawns, arenaRadius: 3f, challengeTime: 120f, spawnCadence: 0f);

            Run(sim, 2f);

            Assert.AreEqual(CombatResult.Win, sim.Result);
            Assert.AreEqual(0, sim.Remaining, "적 전멸");
            Assert.Greater(sim.Robot.hp, 0f, "로봇 생존");
        }

        [Test]
        public void LoseDead_WhenRobotHpDepleted()
        {
            // 원거리 공격 적(attackRange 100) atk100, 로봇 hp10 → 첫 타에 사망.
            var spawns = new List<EnemySpawn>
            {
                new EnemySpawn { label = "포격", hp = 100000f, def = 0f, atk = 100f,
                    moveSpeed = 0f, attackRange = 100f, attackInterval = 1f },
            };
            var sim = new CombatSimulation(Robot(10f), spawns, arenaRadius: 3f, challengeTime: 120f, spawnCadence: 0f);

            Run(sim, 5f);

            Assert.AreEqual(CombatResult.LoseDead, sim.Result);
            Assert.AreEqual(0f, sim.Robot.hp, 0.001f);
        }

        [Test]
        public void LoseTimeout_WhenCannotClearInTime()
        {
            // 초거대 HP·무해(atk0) 적 → 시간 내 격파 불가, 로봇도 안 죽음 → 타임아웃.
            var spawns = new List<EnemySpawn>
            {
                new EnemySpawn { label = "벽", hp = 1_000_000f, def = 0f, atk = 0f,
                    moveSpeed = 0f, attackRange = 0.5f, attackInterval = 1f },
            };
            var sim = new CombatSimulation(Robot(1000f), spawns, arenaRadius: 3f, challengeTime: 1f, spawnCadence: 0f);

            Run(sim, 2f);

            Assert.AreEqual(CombatResult.LoseTimeout, sim.Result);
        }

        [Test]
        public void Deterministic_SameSetupSameOutcome()
        {
            List<EnemySpawn> Spawns() => new List<EnemySpawn>
            {
                new EnemySpawn { label = "a", hp = 400f, def = 6f, atk = 8f,
                    moveSpeed = 1.5f, attackRange = 1f, attackInterval = 1f },
                new EnemySpawn { label = "b", hp = 400f, def = 6f, atk = 8f,
                    moveSpeed = 1.5f, attackRange = 1f, attackInterval = 1f },
            };

            var a = new CombatSimulation(Robot(500f), Spawns(), 6f, 120f, 0.15f);
            var b = new CombatSimulation(Robot(500f), Spawns(), 6f, 120f, 0.15f);
            Run(a, 30f);
            Run(b, 30f);

            Assert.AreEqual(a.Result, b.Result, "결과 재현");
            Assert.AreEqual(a.Robot.hp, b.Robot.hp, 0.001f, "로봇 HP 재현");
            Assert.AreEqual(a.Remaining, b.Remaining, "잔존 수 재현");
        }
    }
}
