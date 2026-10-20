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
    ///
    /// ✅ **#229(2026-10-20 사용자 확정)** — 순간 버스트 300% 폐기 → 「과부하」 상태(발사 ×2 · 산출 ×2.5 · 지속 overloadSec).
    /// 이 파일은 이제 **과부하 배선**을 잰다(파일 이름은 그대로).
    /// </summary>
    public sealed class BurstWiringTests
    {
        private const float D = 0.5f;
        private const float SandbagHp = 10000000f;

        private static Dictionary<MountItem, float> Stacks() =>
            new Dictionary<MountItem, float>
            {
                { MountItem.Pierce, 100f }, { MountItem.Standard, 100f },
                { MountItem.Explosive, 100f }, { MountItem.Drone, 100f },
            };

        /// <summary>관통 20 × 1발/초 = 20 DPS.</summary>
        private static RobotSetup Robot(float damagePerShot, float shotsPerSec = 1f) => new RobotSetup
        {
            hp = 100000f, mountCoef = 1f, moduleMult = 1f,
            attackRange = 100f, radius = 0f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = new List<AmmoLine> { new AmmoLine(AmmoKind.Pierce, damagePerShot, shotsPerSec) },
            ammoCapacity = 1000f, ammoStore = AmmoFixture.Pierce(1000f, 1000f),
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

        // ---- #229 과부하 (🗑️ 순간 버스트 300% 시험 넷 — Merging_FiresTheBurst · BurstIsThreeHundredPercent… · BurstMultipliesAfterDefence · BurstFiresOnce) ----

        /// <summary>#229 — 합체 순간 **피해가 없다**(순간 폭발 폐기). TryMerge 는 틱 밖이라 사격도 안 낀다.</summary>
        [Test]
        public void Merging_DealsNoInstantDamage()
        {
            CombatSimulation sim = TwoRobots(dmgA: 20f, dmgB: 30f);
            ChargeGauge(sim);

            float before = sim.Enemies[0].hp;
            Assert.IsTrue(sim.TryMerge());
            Assert.AreEqual(before, sim.Enemies[0].hp, D, "합체 순간 = 피해 0");
            Assert.AreEqual(50f, sim.LastMergeSnapshot, D, "연출용 스냅샷은 그대로 (20 + 30)");
        }

        /// <summary>#229 — 과부하 동안 발사 ×2 · 피해 ×1.8 은 그대로 곱 = (20 + 30) × 1.8 × 2.</summary>
        [Test]
        public void Overload_DoublesFireRate_KeepsMergeMultiplier()
        {
            CombatSimulation sim = TwoRobots(dmgA: 20f, dmgB: 30f);
            sim.OverloadFireRate = 2f;
            ChargeGauge(sim);
            sim.TryMerge();
            Assert.IsTrue(sim.IsOverloaded);

            float start = sim.Enemies[0].hp;
            Run(sim, 2f);
            float dealt = start - sim.Enemies[0].hp;
            Assert.AreEqual(2f * (20f + 30f) * MergeSystem.MergeMultiplier * 2f, dealt, 20f, "2초 × 90 × 2");
        }

        /// <summary>과부하가 끝나면 합체 화력만 — 과부하 1초 · 합체는 20초.</summary>
        [Test]
        public void Overload_Ends_MergeContinuesAtNormalRate()
        {
            CombatSimulation sim = TwoRobots(dmgA: 20f, dmgB: 30f);
            sim.OverloadFireRate = 2f;
            ChargeGauge(sim);
            sim.Merge.OverloadSeconds = 1f;   // 마운트 재고(충전 90초 뒤 남은 몫) 안에서 잰다
            sim.TryMerge();

            Run(sim, 2f);
            Assert.IsFalse(sim.IsOverloaded, "과부하 끝");
            Assert.IsTrue(sim.Merge.IsActive, "합체는 이어진다");

            float start = sim.Enemies[0].hp;
            Run(sim, 2f);
            Assert.AreEqual(2f * (20f + 30f) * MergeSystem.MergeMultiplier, start - sim.Enemies[0].hp, 20f, "2초 × 90");
        }

        /// <summary>과부하 배율 기본 1 — 격리 경로는 종전 합체 화력 그대로.</summary>
        [Test]
        public void DefaultOverloadRate_IsOne()
        {
            CombatSimulation sim = TwoRobots(dmgA: 20f, dmgB: 30f);
            ChargeGauge(sim);
            sim.TryMerge();

            float start = sim.Enemies[0].hp;
            Run(sim, 2f);
            Assert.AreEqual(2f * (20f + 30f) * MergeSystem.MergeMultiplier, start - sim.Enemies[0].hp, 20f);
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

        // 🗑️ FailedMerge_FiresNothing · NoTarget_KeepsTheBurst — #229 로 순간 버스트가 없다.

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

        /// <summary>#228 — 합체가 교대를 잠그고 **스킬 허용도 끈다**(🗑️ #163 「스킬은 나간다」).</summary>
        [Test]
        public void Merging_LocksTag_NoSkillWhileLocked()
        {
            CombatSimulation sim = TwoRobots();
            ChargeGauge(sim);
            sim.TryMerge();

            Assert.IsTrue(sim.Tag.Locked);
            Assert.IsFalse(sim.Tag.SkillWhileLocked, "#228");
            Assert.IsFalse(sim.Tag.CanSkillWhileLocked);
        }

        // ---- 결정론 ----

        [Test]
        public void Deterministic_SameSetupSameSnapshot()
        {
            CombatSimulation a = TwoRobots(), b = TwoRobots();
            ChargeGauge(a); ChargeGauge(b);

            a.TryMerge(); b.TryMerge();

            Assert.AreEqual(a.LastMergeSnapshot, b.LastMergeSnapshot, 0.001f);
        }
    }
}
