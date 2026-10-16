using System.Collections.Generic;
using MBI.Core;
using MBI.Core.Combat;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **합체 중에는 A 의 탄과 B 의 드론이 둘 다 나간다** (사용자 확정 #157 · 2026-10-16 · 🗑️ 나가 있던 쪽 드론만).
    /// </summary>
    public sealed class MergeDronesTests
    {
        private static Dictionary<MountItem, float> Stacks() => new Dictionary<MountItem, float>
        {
            { MountItem.Pierce, 100f }, { MountItem.Standard, 100f },
            { MountItem.Explosive, 100f }, { MountItem.Drone, 100f }, { MountItem.DroneAoe, 100f },
        };

        private static RobotSetup Gunner() => new RobotSetup
        {
            hp = 100000f, mountCoef = 1f, moduleMult = 1f, attackRange = 100f, radius = 0f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = new List<AmmoLine> { new AmmoLine(AmmoKind.Pierce, 20f, 1f) },
            ammoCapacity = 1000f, ammoStore = AmmoFixture.Pierce(1000f, 1000f),
        };

        private static RobotSetup DroneCarrier() => new RobotSetup
        {
            hp = 100000f, mountCoef = 1f, moduleMult = 1f, attackRange = 100f, radius = 0f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = new List<AmmoLine>(), ammoCapacity = 1000f, ammoStore = new AmmoInventory(1000f),
            droneSlots = 3, droneReleaseRate = 5f, droneCharge = 100f, droneDamagePerHit = 50f,
            droneAttackRange = 100f,
        };

        private static CombatSimulation Pair(out MountLoad mountB)
        {
            mountB = new MountLoad(4, Stacks());
            mountB.Load(MountItem.Drone, 20f);
            var spawns = new List<EnemySpawn>
            {
                new EnemySpawn { label = "샌드백", hp = 1e7f, def = 0f, atk = 0f, moveSpeed = 0f,
                    attackRange = 0.5f, attackInterval = 1f },
            };
            return new CombatSimulation(Gunner(), DroneCarrier(), new MountLoad(4, Stacks()), mountB,
                spawns, arenaRadius: 3f, challengeTime: 1000f, spawnCadence: 0f);
        }

        private static void Run(CombatSimulation sim, float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.05f) sim.Tick(0.05f);
        }

        [Test]
        public void 합체_전에는_A가_나가_있으면_B_드론이_안_나간다()
        {
            CombatSimulation sim = Pair(out _);
            Assert.AreEqual(0, sim.ActiveRobotIndex);
            Run(sim, 2f);
            Assert.AreEqual(0f, sim.DroneDamageDealt, 1e-3f);
        }

        [Test]
        public void 합체하면_A가_나가_있어도_B_드론이_B_마운트에서_나간다()
        {
            CombatSimulation sim = Pair(out MountLoad mountB);
            sim.Tick(0.05f);
            sim.Merge.SetCharge(MergeSystem.GaugeFullSeconds);
            Assert.IsTrue(sim.TryMerge());
            float before = mountB.AmountOf(MountItem.Drone);
            Run(sim, 2f);
            Assert.Greater(sim.DroneDamageDealt, 0f, "B 드론이 안 쳤다");
            Assert.Less(mountB.AmountOf(MountItem.Drone), before, "B 마운트에서 안 뺐다");
        }

        [Test]
        public void 합체_중_드론_목록은_양쪽을_다_보여준다()
        {
            string src = System.IO.File.ReadAllText("Assets/_Project/Scripts/Core/Combat/CombatSimulation.cs");
            StringAssert.Contains("DroneTickSide(_sides[i], dt, MergeSystem.MergeMultiplier, _sides[i] == Act);", src);
            StringAssert.Contains("for (int i = 0; i < _sides.Length; i++) _dronesAll.AddRange(_sides[i].drones);", src);
        }
    }
}
