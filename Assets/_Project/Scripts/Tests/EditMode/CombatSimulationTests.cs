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
        // 대표 상태 물류 생산율(pA 1/1/2) → 폭발2 + 분열1 + 관통1 발/초 = 145/초.
        private static List<WeaponSpec> RepresentativeWeapons() => new List<WeaponSpec>
        {
            new WeaponSpec(AmmoKind.Pierce, 20f, 1f),
            new WeaponSpec(AmmoKind.Split, 25f, 1f),
            new WeaponSpec(AmmoKind.Explosive, 50f, 2f),
        };

        private static List<AmmoLine> RepresentativeLines(float scale = 1f)
        {
            var lines = new List<AmmoLine>();
            ShotAllocator.AllocateRates(RepresentativeWeapons(), 6f, scale, lines);
            return lines;
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
            lines = RepresentativeLines(),
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

        // ---- 라인 기반 발사(§5-6 D1) ----

        // ---- 처치 집계(§5-7 고철 적립의 유일한 입력) ----

        /// <summary>정지·무공격 약한 적 n기.</summary>
        private static List<EnemySpawn> Fodder(int n)
        {
            var list = new List<EnemySpawn>();
            for (int i = 0; i < n; i++)
                list.Add(new EnemySpawn { label = $"약졸{i}", hp = 1f, def = 0f, atk = 0f,
                    moveSpeed = 0f, attackRange = 0.5f, attackInterval = 1f });
            return list;
        }

        /// <summary>
        /// AoE 한 발이 여러 기를 죽여도 처치 수는 죽은 개체 수와 같다.
        /// 데미지를 준 지점에서 셌다면 히트마다 세어 수입이 부풀려졌을 것이다.
        /// </summary>
        [Test]
        public void KillsThisTick_CountsEachDeathOnce_EvenWithAoe()
        {
            RobotSetup setup = Robot(1000f);
            setup.aoeRadius = 10f;        // 아레나 전체를 덮는 폭발
            setup.aoeSplashFactor = 1f;
            setup.lines = new List<AmmoLine> { new AmmoLine(AmmoKind.Explosive, 50f, 2f) };

            var sim = new CombatSimulation(setup, Fodder(3), arenaRadius: 1f, challengeTime: 120f, spawnCadence: 0f);
            Run(sim, 1f, 0.02f);

            Assert.AreEqual(3, sim.TotalKills, "죽은 개체 수 = 3. 히트 수로 세면 3보다 커진다");
            Assert.AreEqual(0, sim.Remaining);
        }

        /// <summary>
        /// 전투가 끝나면 Tick이 즉시 반환하므로 KillsThisTick이 마지막 값에 멈춘다.
        /// 그 값을 매 프레임 더하면 승리 화면에서 고철이 무한히 불어난다 —
        /// ConsumeKills가 가져가며 비우므로 두 번째부터는 0이다.
        /// </summary>
        [Test]
        public void ConsumeKills_DrainsOnce_NoDoubleCountAfterWin()
        {
            var sim = new CombatSimulation(Robot(1000f), Fodder(1), 1f, 120f, 0f);
            Run(sim, 1f, 0.02f);
            Assert.AreEqual(CombatResult.Win, sim.Result);

            Assert.AreEqual(1, sim.ConsumeKills(), "죽은 만큼 한 번 가져간다");
            Assert.AreEqual(0, sim.ConsumeKills(), "두 번째 읽기는 0 — 무한 적립 차단");

            sim.Tick(0.02f); // 종료 후 틱은 무동작
            Assert.AreEqual(0, sim.ConsumeKills());
            Assert.AreEqual(1, sim.TotalKills, "누적 집계는 유지된다");
        }

        [Test]
        public void TotalKills_StartsAtZero()
        {
            var sim = new CombatSimulation(Robot(1000f), Fodder(2), 1f, 120f, 0f);
            Assert.AreEqual(0, sim.TotalKills);
            Assert.AreEqual(0, sim.KillsThisTick);
        }

        /// <summary>맞기만 하고 죽지 않는 샌드백 1기(def 0, 무공격).</summary>
        private static List<EnemySpawn> Dummy(float hp) => new List<EnemySpawn>
        {
            new EnemySpawn { label = "샌드백", hp = hp, def = 0f, atk = 0f,
                moveSpeed = 0f, attackRange = 0.5f, attackInterval = 1f },
        };

        /// <summary>
        /// §5-6 계약: 1초 동안 준 피해 == 명목 출력(145). 라인별 주기로 쏘아도 총량은 같아야 한다.
        /// </summary>
        [Test]
        public void FireLines_OneSecond_DealsNominalOutput()
        {
            var sim = new CombatSimulation(Robot(1000f), Dummy(100000f), 6f, 120f, 0f);
            Run(sim, 1f, 0.02f);

            float dealt = 100000f - sim.Enemies[0].hp;
            Assert.AreEqual(145f, dealt, 0.5f, "1초 누적 피해 = Σ(발사율 × 발당피해)");
        }

        /// <summary>
        /// 절반 공급이면 피해도 절반. 0.5발/초는 2초에 한 발이므로 2초를 돌려 145(=72.5×2)로 본다.
        /// 정수 반올림 경로였다면 관통·분열이 0으로 접혀 100만 나온다.
        /// </summary>
        [Test]
        public void FireLines_HalfSupply_HalvesDamage()
        {
            RobotSetup setup = Robot(1000f);
            setup.lines = RepresentativeLines(0.5f);

            var sim = new CombatSimulation(setup, Dummy(100000f), 6f, 120f, 0f);
            Run(sim, 2f, 0.02f);

            float dealt = 100000f - sim.Enemies[0].hp;
            Assert.AreEqual(145f, dealt, 0.5f, "2초 누적 = 명목의 절반 × 2초");
        }

        /// <summary>
        /// 전투 중 라인 교체가 DPS를 바꾸되, 매 프레임 교체해도 발사가 멈추지 않아야 한다.
        /// (SetFireLines가 누산기를 리셋하면 1.0에 영영 도달하지 못해 영구 무발사가 된다 — 회귀 방지.)
        /// </summary>
        [Test]
        public void SetFireLines_EveryFrame_DoesNotStallFiring()
        {
            var sim = new CombatSimulation(Robot(1000f), Dummy(100000f), 6f, 120f, 0f);
            List<AmmoLine> half = RepresentativeLines(0.5f);

            for (int i = 0; i < 100; i++) // 2초를 0.02초로 쪼개 매 스텝 교체
            {
                sim.SetFireLines(half);
                sim.Tick(0.02f);
            }

            float dealt = 100000f - sim.Enemies[0].hp;
            Assert.Greater(dealt, 0f, "매 프레임 교체해도 발사가 멈추면 안 된다");
            Assert.AreEqual(145f, dealt, 0.5f, "교체 후에도 절반 공급 DPS가 그대로 나온다");
        }
    }
}
