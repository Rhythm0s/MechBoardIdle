using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>#242 종류별 상한 · #243 과부하 타격 간격 · #244 최대 유지 시간(2026-10-20 사용자 확정).</summary>
    public sealed class DroneCapLifeTests
    {
        private static RobotSetup Drones(int kindCap, float perHit) => new RobotSetup
        {
            hp = 100000f, mountCoef = 1f, moduleMult = 1f, attackRange = 100f, radius = 0f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f, lines = new List<AmmoLine>(), ammoCapacity = 0f,
            droneSlots = 3, droneReleaseRate = 1f, droneCharge = 100f, droneAttackRange = 100f,
            droneDamagePerHit = perHit, droneKindCap = kindCap,
        };

        private static List<EnemySpawn> Sandbag() => new List<EnemySpawn>
        {
            new EnemySpawn { label = "샌드백", hp = 1e9f, def = 0f, atk = 0f, moveSpeed = 0f, attackRange = 0.5f, attackInterval = 1f },
        };

        private static void Run(CombatSimulation sim, float seconds, float dt = 0.02f)
        {
            int n = Mathf.CeilToInt(seconds / dt);
            for (int i = 0; i < n && sim.Result == CombatResult.InProgress; i++) sim.Tick(dt);
        }

        [Test]
        public void KindCap_LimitsDronesOnField()
        {
            var sim = new CombatSimulation(Drones(3, 1f), Sandbag(), 6f, 120f, 0f) { StackDroneArrivalRate = 100f };
            Run(sim, 3f);
            Assert.LessOrEqual(sim.Drones.Count, 3, "누적형 3기 상한");
            Assert.GreaterOrEqual(sim.Drones.Count, 3, "상한까지는 찬다");
        }

        [Test]
        public void KindCap_Zero_IsUnlimited()
        {
            var sim = new CombatSimulation(Drones(0, 1f), Sandbag(), 6f, 120f, 0f) { StackDroneArrivalRate = 100f };
            Run(sim, 3f);
            Assert.Greater(sim.Drones.Count, 3, "0 = 상한 없음(종전)");
        }

        [Test]
        public void KindCap_Rule_EnhanceAddsTwo()
        {
            Assert.AreEqual(3, DroneReleaseRule.KindCap(3, 2, false));
            Assert.AreEqual(5, DroneReleaseRule.KindCap(3, 2, true));
        }

        [Test]
        public void MaxLife_RetiresDronesEvenWithCharge()
        {
            var sim = new CombatSimulation(Drones(0, 1f), Sandbag(), 6f, 120f, 0f) { StackDroneArrivalRate = 100f, DroneMaxLife = 1f };
            Run(sim, 4f);
            // 처리량 3기/초 × 수명 1초 → 필드 약 3기(충전 100 · 타격 1 이라 충전으로는 안 사라진다)
            Assert.LessOrEqual(sim.Drones.Count, 4, "1초 지나면 소멸");
        }

        [Test]
        public void OverloadHitInterval_Wired()
        {
            string src = System.IO.File.ReadAllText("Assets/_Project/Scripts/Core/Combat/CombatSimulation.cs");
            StringAssert.Contains("d.HitCooldown = DroneHitInterval * (IsOverloaded ? Mathf.Clamp(OverloadDroneHitMult, 0.01f, 1f) : 1f);", src);
            string sr = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            StringAssert.Contains("_sim.OverloadDroneHitMult = bal.overloadDroneHitMult;", sr);
            StringAssert.Contains("_sim.DroneMaxLife = bal.droneMaxLife;", sr);
        }
    }
}
