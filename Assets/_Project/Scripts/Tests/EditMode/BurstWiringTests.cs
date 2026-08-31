using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 버스트 적용 지점 — **계산기는 있는데 부르는 곳이 0건**이던 것을 잇는다(260831_V07 §2층).
    ///
    /// 지침 §7 [08-30]의 「실패하지 않는 결함」 그 자체였다: `MergeSystem.BurstDamage`가
    /// 테스트까지 있는 채로 두 달을 지났고, 아무것도 실패하지 않았다.
    ///
    /// 규칙은 MergeSystemTests가 보고, 여기서는 **합체를 발동시켜 실제로 터지는지**를 잰다.
    /// </summary>
    public sealed class BurstWiringTests
    {
        private const float D = 0.5f;
        private const float SandbagHp = 10000000f;

        private static Dictionary<MountItem, float> Stacks() =>
            new Dictionary<MountItem, float>
            {
                { MountItem.Pierce, 100f }, { MountItem.Split, 100f },
                { MountItem.Explosive, 100f }, { MountItem.Drone, 100f },
            };

        /// <summary>관통 20 × 1발/초 = 20 DPS.</summary>
        private static RobotSetup Robot(float damagePerShot, float shotsPerSec = 1f) => new RobotSetup
        {
            hp = 100000f, mountCoef = 1f, moduleMult = 1f,
            attackRange = 100f, radius = 0f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = new List<AmmoLine> { new AmmoLine(AmmoKind.Pierce, damagePerShot, shotsPerSec) },
            ammoCapacity = 1000f, ammoInitialStock = 1000f,
            droneSlots = 0, droneReleaseRate = 0f, droneCharge = 0f, droneAttackRange = 0f,
        };

        private static List<EnemySpawn> Sandbag(float def = 0f) => new List<EnemySpawn>
        {
            new EnemySpawn { label = "샌드백", hp = SandbagHp, def = def, atk = 0f,
                moveSpeed = 0f, attackRange = 0.5f, attackInterval = 1f },
        };

        private static CombatSimulation TwoRobots(float dmgA = 20f, float dmgB = 30f, float def = 0f) =>
            new CombatSimulation(Robot(dmgA), Robot(dmgB),
                new MountLoad(4, Stacks()), new MountLoad(4, Stacks()),
                Sandbag(def), arenaRadius: 6f, challengeTime: 1000f, spawnCadence: 0f);

        private static void Run(CombatSimulation sim, float seconds, float dt = 0.5f)
        {
            int steps = Mathf.CeilToInt(seconds / dt);
            for (int i = 0; i < steps && sim.Result == CombatResult.InProgress; i++) sim.Tick(dt);
        }

        /// <summary>게이지는 전투 수행 중에만 찬다 — 틱을 돌려 채운다.</summary>
        private static void ChargeGauge(CombatSimulation sim) =>
            Run(sim, MergeSystem.GaugeFullSeconds + 1f);

        // ---- 불리는가 ----

        /// <summary>
        /// **합체를 발동하면 버스트가 터진다.** 이것이 이 파일의 존재 이유다 —
        /// 종전에는 계산기만 있고 호출자가 0건이었다.
        /// </summary>
        [Test]
        public void Merging_FiresTheBurst()
        {
            CombatSimulation sim = TwoRobots();
            ChargeGauge(sim);

            Assert.AreEqual(0f, sim.LastBurstDamage, "발동 전에는 안 터진다");
            Assert.IsTrue(sim.TryMerge());

            Assert.Greater(sim.LastBurstDamage, 0f, "발동 순간 터진다");
        }

        /// <summary>
        /// 스냅샷 = 그 순간 **두 로봇이 합쳐 내는 초당 실피해**, 거기에 300%.
        /// A 20 + B 30 = 50 → 150.
        /// </summary>
        [Test]
        public void BurstIsThreeHundredPercentOfBothRobotsCombined()
        {
            CombatSimulation sim = TwoRobots(dmgA: 20f, dmgB: 30f);
            ChargeGauge(sim);

            sim.TryMerge();

            Assert.AreEqual(150f, sim.LastBurstDamage, D, "(20 + 30) × 300%");
        }

        /// <summary>
        /// **방어를 뺀 뒤에 300%를 곱한다.** 합체 배율과 같은 규칙이다(260829_V01) —
        /// 발당피해에 곱했다면 (100×3 − 45) × 2 = 510이 되어 값이 달라진다.
        /// </summary>
        [Test]
        public void BurstMultipliesAfterDefence()
        {
            CombatSimulation sim = TwoRobots(dmgA: 100f, dmgB: 100f, def: 45f);
            ChargeGauge(sim);

            sim.TryMerge();

            Assert.AreEqual(330f, sim.LastBurstDamage, D, "((100−45) + (100−45)) × 300%");
            Assert.Less(sim.LastBurstDamage, 510f, "발당피해에 곱했을 때의 값보다 낮다");
        }

        /// <summary>
        /// 버스트는 **한 번**이다. 합체가 지속되는 동안 계속 터지지 않는다.
        ///
        /// ⚠️ 「이후 피해가 버스트보다 작다」로는 못 잰다 — 합체 중 화력이 90/초라
        /// 2초만 지나도 버스트 150을 넘는다. **합체 이후 증가분이 합체 화력과 같은지**로 잰다.
        /// </summary>
        [Test]
        public void BurstFiresOnce_NotEveryTick()
        {
            CombatSimulation sim = TwoRobots(dmgA: 20f, dmgB: 30f);
            ChargeGauge(sim);

            float before = SandbagHp - sim.Enemies[0].hp;
            sim.TryMerge();

            // TryMerge는 틱 밖이라 사격이 끼지 않는다 — 늘어난 만큼이 정확히 버스트다.
            float afterMerge = SandbagHp - sim.Enemies[0].hp;
            Assert.AreEqual(sim.LastBurstDamage, afterMerge - before, D, "합체 순간 = 버스트 하나뿐");

            Run(sim, 2f);

            float dealtSince = (SandbagHp - sim.Enemies[0].hp) - afterMerge;
            float mergedDps = (20f + 30f) * MergeSystem.MergeMultiplier;

            Assert.AreEqual(2f * mergedDps, dealtSince, 20f,
                "이후 증가분은 합체 화력만 — 버스트가 다시 안 터졌다");
        }

        /// <summary>
        /// **스테이지당 1회**라 두 번째 합체가 없고, 따라서 두 번째 버스트도 없다.
        /// </summary>
        [Test]
        public void NoSecondBurst_BecauseMergeIsOncePerStage()
        {
            CombatSimulation sim = TwoRobots();
            ChargeGauge(sim);
            sim.TryMerge();

            Run(sim, MergeSystem.DurationSeconds + MergeSystem.GaugeFullSeconds + 5f);

            Assert.IsFalse(sim.TryMerge(), "두 번째 합체는 없다");
        }

        /// <summary>게이지가 안 찼으면 합체가 실패하므로 버스트도 없다.</summary>
        [Test]
        public void FailedMerge_FiresNothing()
        {
            CombatSimulation sim = TwoRobots();
            Run(sim, 5f);

            Assert.IsFalse(sim.TryMerge());
            Assert.AreEqual(0f, sim.LastBurstDamage, "발동에 실패했으면 안 터진다");
        }

        /// <summary>
        /// 때릴 것이 없으면 **터뜨리지 않는다** — 스테이지당 1회짜리를 허공에 버리지 않는다.
        /// </summary>
        [Test]
        public void NoTarget_KeepsTheBurst()
        {
            var sim = new CombatSimulation(Robot(20f), Robot(30f),
                new MountLoad(4, Stacks()), new MountLoad(4, Stacks()),
                new List<EnemySpawn>(), arenaRadius: 6f, challengeTime: 1000f, spawnCadence: 0f);
            sim.Endless = true; // 적이 없어도 승리로 끝나지 않게

            ChargeGauge(sim);
            sim.TryMerge();

            Assert.AreEqual(0f, sim.LastBurstDamage);
        }

        /// <summary>
        /// **태그 스킬은 부르지 않는다**(2026-08-29 확정). 합체 발동 순간에 일어나는 것은
        /// 버스트 하나다 — 둘 다 터지면 예산식이 두 번 계상된다.
        ///
        /// ⚠️ 게이지를 채우는 90초 동안 **태그가 저절로 일어나** 스킬이 나간다.
        /// 그 이력이 남아 있으면 「합체가 스킬을 불렀다」와 구분이 안 되므로 태그를 잠그고 잰다
        /// — 실제로 그렇게 재서 이 테스트가 처음에 깨졌다.
        /// </summary>
        [Test]
        public void MergingDoesNotFireTheTagSkill()
        {
            CombatSimulation sim = TwoRobots();
            sim.Tag.Locked = true; // 교대를 막아 합체만 남긴다
            ChargeGauge(sim);

            sim.TryMerge();

            Assert.IsFalse(sim.Tag.LastTagFiredSkill, "태그 스킬은 안 나간다");
            Assert.AreEqual(0f, sim.Tag.LastTagSkillDrained, D, "마운트도 안 비운다");
        }

        // ---- 결정론 ----

        [Test]
        public void Deterministic_SameSetupSameBurst()
        {
            CombatSimulation a = TwoRobots(), b = TwoRobots();
            ChargeGauge(a); ChargeGauge(b);

            a.TryMerge(); b.TryMerge();

            Assert.AreEqual(a.LastBurstDamage, b.LastBurstDamage, 0.001f);
        }
    }
}
