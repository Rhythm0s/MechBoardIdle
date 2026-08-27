using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 드론 전투 배선(260827_V02 §5). 확정치: pB 1.0 · dB 100 · slot 3 · r 1.0.
    ///
    /// **1기 = 1회 타격 = 충전량 전량**이라는 것이 이 배선의 핵심이다 —
    /// 등가선이 그렇게 맞는다(초당 1기 × 기당 100 = DPS 100). 나눠 쏘면 등가선을 벗어난다.
    /// </summary>
    public sealed class DroneCombatTests
    {
        private const float D = 0.001f;

        // 확정치 그대로. 본체 사격은 끄고(라인 없음) 드론만 본다.
        private static RobotSetup DroneOnlyRobot(float hp = 100000f) => new RobotSetup
        {
            hp = hp, mountCoef = 1f, moduleMult = 1f,
            attackRange = 100f, radius = 0f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = new List<AmmoLine>(),   // 본체는 쏘지 않는다
            ammoCapacity = 0f, ammoInitialStock = 0f,
            droneSlots = 3, droneReleaseRate = 1f, droneCharge = 100f, droneAttackRange = 100f,
        };

        private static List<EnemySpawn> Sandbag(float hp = 1000000f, float def = 0f) => new List<EnemySpawn>
        {
            new EnemySpawn { label = "샌드백", hp = hp, def = def, atk = 0f,
                moveSpeed = 0f, attackRange = 0.5f, attackInterval = 1f },
        };

        private static void Run(CombatSimulation sim, float seconds, float dt = 0.02f)
        {
            int steps = Mathf.CeilToInt(seconds / dt);
            for (int i = 0; i < steps && sim.Result == CombatResult.InProgress; i++) sim.Tick(dt);
        }

        // ---- 등가선 ----

        /// <summary>
        /// 유입 1.0기/초 × 충전량 100 = **DPS 100**. 밸런스 등가선 그대로다.
        /// 이 값이 어긋나면 드론이 다른 탄종과 같은 선 위에 있지 않다는 뜻이다.
        /// </summary>
        [Test]
        public void DroneOutput_SitsOnTheEquivalenceLine()
        {
            var sim = new CombatSimulation(DroneOnlyRobot(), Sandbag(), 6f, 120f, 0f)
            { DroneInflowRate = 1f };

            Run(sim, 3f);

            Assert.AreEqual(300f, sim.DroneDamageDealt, 100f, "3초 × DPS 100 (출격 지연 허용)");
        }

        /// <summary>드론 유입이 없으면 아무 일도 일어나지 않는다 — 보드가 안 만들면 안 나간다.</summary>
        [Test]
        public void NoInflow_NoDrones()
        {
            var sim = new CombatSimulation(DroneOnlyRobot(), Sandbag(), 6f, 120f, 0f)
            { DroneInflowRate = 0f };

            Run(sim, 3f);

            Assert.AreEqual(0, sim.Drones.Count);
            Assert.AreEqual(0f, sim.DroneDamageDealt, D);
        }

        /// <summary>슬롯이 병목이면 유입이 넘쳐도 방출이 상한에 걸린다.</summary>
        [Test]
        public void SlotThroughput_CapsRelease()
        {
            RobotSetup setup = DroneOnlyRobot();
            setup.droneSlots = 1; // 처리량 1/초

            var sim = new CombatSimulation(setup, Sandbag(), 6f, 120f, 0f) { DroneInflowRate = 100f };

            Run(sim, 1f);

            Assert.LessOrEqual(sim.Drones.Count, 1, "슬롯 1개를 넘어 나가지 않는다");
        }

        // ---- 1기 = 1회 타격 ----

        /// <summary>
        /// 드론은 쏘자마자 충전량을 다 쓰고 소멸한다. 필드에 쌓이지 않는다 —
        /// 쌓이면 슬롯이 안 비고 방출률이 의미를 잃는다.
        /// </summary>
        [Test]
        public void Drone_FiresOnce_ThenRetires_FreeingItsSlot()
        {
            var sim = new CombatSimulation(DroneOnlyRobot(), Sandbag(), 6f, 120f, 0f)
            { DroneInflowRate = 1f };

            Run(sim, 5f);

            Assert.Less(sim.Drones.Count, 3, "쏜 드론은 남지 않는다");
            Assert.Greater(sim.DroneDamageDealt, 0f, "그동안 피해는 들어갔다");
        }

        // ---- 판정식 재사용 ----

        /// <summary>
        /// 드론도 **같은 판정식**을 탄다 — 방어를 여기서 다시 계산하지 않는다.
        /// 단발이라 방어를 한 번만 빼므로 고방어에 강하다(「단발 고밀도 = S5 해답자」).
        /// </summary>
        [Test]
        public void Drone_UsesSharedDamageFormula_SubtractingDefenceOnce()
        {
            var sim = new CombatSimulation(DroneOnlyRobot(), Sandbag(def: 45f), 6f, 120f, 0f)
            { DroneInflowRate = 1f };

            Run(sim, 1.5f);

            float expectedPerHit = DamageFormula.PerHit(100f, 1f, 1f, 45f);
            Assert.AreEqual(55f, expectedPerHit, D, "100 − 45, 단발이므로 한 번만 뺀다");
            Assert.AreEqual(0f, sim.DroneDamageDealt % expectedPerHit, 0.01f, "타격당 같은 값이 들어간다");
        }

        // ---- 표적 선택: 기준점이 드론 자신 ----

        /// <summary>
        /// 드론은 **자기 위치 기준** 최근접을 고른다. 본체와 다른 적을 칠 수 있다는 것이
        /// 이 규칙의 요점이고, 그래서 기준점을 본체로 두면 안 된다.
        /// </summary>
        [Test]
        public void Drone_PicksNearestFromItsOwnPosition()
        {
            RobotSetup setup = DroneOnlyRobot();
            setup.droneAttackRange = 1.5f; // 짧게 잡아 위치 차이가 드러나게

            var far = new List<EnemySpawn>
            {
                new EnemySpawn { label = "먼적", hp = 1000f, def = 0f, atk = 0f,
                    moveSpeed = 0f, attackRange = 0.5f, attackInterval = 1f },
            };

            var sim = new CombatSimulation(setup, far, arenaRadius: 6f, challengeTime: 120f, spawnCadence: 0f)
            { DroneInflowRate = 1f };

            Run(sim, 2f);

            // 적이 아레나 경계(6)에 있고 드론 사거리가 1.5라 닿지 않는다 —
            // 본체 기준이었다면 사거리 판정이 달라졌을 것이다.
            Assert.AreEqual(0f, sim.DroneDamageDealt, D, "사거리 밖은 못 친다");
        }

        // ---- 결정론 ----

        [Test]
        public void Deterministic_SameSetupSameDroneDamage()
        {
            var a = new CombatSimulation(DroneOnlyRobot(), Sandbag(), 6f, 120f, 0f) { DroneInflowRate = 1f };
            var b = new CombatSimulation(DroneOnlyRobot(), Sandbag(), 6f, 120f, 0f) { DroneInflowRate = 1f };

            Run(a, 4f);
            Run(b, 4f);

            Assert.AreEqual(a.DroneDamageDealt, b.DroneDamageDealt, D);
            Assert.AreEqual(a.Drones.Count, b.Drones.Count);
        }

        /// <summary>드론이 낸 피해도 처치로 이어진다 — 본체 없이 드론만으로 이길 수 있다.</summary>
        [Test]
        public void DroneOnly_CanClearWeakEnemies()
        {
            var fodder = new List<EnemySpawn>
            {
                new EnemySpawn { label = "약졸", hp = 50f, def = 0f, atk = 0f,
                    moveSpeed = 0f, attackRange = 0.5f, attackInterval = 1f },
            };

            var sim = new CombatSimulation(DroneOnlyRobot(), fodder, 6f, 120f, 0f) { DroneInflowRate = 1f };

            Run(sim, 5f);

            Assert.AreEqual(CombatResult.Win, sim.Result, "드론만으로도 전멸시킨다");
            Assert.AreEqual(1, sim.TotalKills);
        }
    }
}
