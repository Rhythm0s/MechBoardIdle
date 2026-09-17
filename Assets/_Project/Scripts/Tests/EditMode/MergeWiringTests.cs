using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 합체 배선 — 시뮬 안에서 **합체 화력이 실제로 (A+B) × 1.8**이 되는가.
    ///
    /// 규칙 자체는 MergeSystemTests가 보고, 여기서는 틱을 돌려 피해량을 잰다.
    /// </summary>
    public sealed class MergeWiringTests
    {
        private const float D = 0.001f;
        private const float SandbagHp = 10000000f;

        private static Dictionary<MountItem, float> Stacks() =>
            new Dictionary<MountItem, float>
            {
                { MountItem.Pierce, 100f }, { MountItem.Standard, 100f },
                { MountItem.Explosive, 100f }, { MountItem.Drone, 100f },
            };

        /// <summary>관통 20 × 1발/초 = 20 DPS짜리 로봇. 탄약은 넉넉히 채워 둔다.</summary>
        private static RobotSetup Robot(float damagePerShot) => new RobotSetup
        {
            hp = 100000f, mountCoef = 1f, moduleMult = 1f,
            attackRange = 100f, radius = 0f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = new List<AmmoLine> { new AmmoLine(AmmoKind.Pierce, damagePerShot, 1f) },
            ammoCapacity = 1000f, ammoStore = AmmoFixture.Pierce(1000f, 1000f),
            droneSlots = 0, droneReleaseRate = 0f, droneCharge = 0f, droneAttackRange = 0f,
        };

        private static List<EnemySpawn> Sandbag(float def = 0f) => new List<EnemySpawn>
        {
            new EnemySpawn { label = "샌드백", hp = SandbagHp, def = def, atk = 0f,
                moveSpeed = 0f, attackRange = 0.5f, attackInterval = 1f },
        };

        private static CombatSimulation TwoRobots(float dmgA = 20f, float dmgB = 30f, float def = 0f)
        {
            return new CombatSimulation(Robot(dmgA), Robot(dmgB),
                new MountLoad(4, Stacks()), new MountLoad(4, Stacks()),
                Sandbag(def), arenaRadius: 6f, challengeTime: 1000f, spawnCadence: 0f);
        }

        /// <summary>
        /// 이 구간에 들어간 피해. ⚠️ 첫 틱 전에는 아직 **스폰되지 않아** Enemies가 비어 있으므로,
        /// before를 그때 읽으면 0이 잡혀 값이 뒤집힌다 — 최대 HP 기준 누적으로 잰다.
        /// </summary>
        private static float Run(CombatSimulation sim, float seconds, float dt = 0.02f)
        {
            float before = Dealt(sim);
            int steps = Mathf.CeilToInt(seconds / dt);
            for (int i = 0; i < steps && sim.Result == CombatResult.InProgress; i++) sim.Tick(dt);
            return Dealt(sim) - before;
        }

        private static float Dealt(CombatSimulation sim)
        {
            if (sim.Enemies.Count > 0) return SandbagHp - sim.Enemies[0].hp;

            // 리스트가 비는 경우가 둘이다 — **스폰 전**과 **사망 후**. 둘을 같게 보면
            // 첫 측정에서 "전부 죽였다"로 읽혀 값이 뒤집힌다.
            return sim.Elapsed <= 0f ? 0f : SandbagHp;
        }

        private static void ChargeGauge(CombatSimulation sim)
        {
            // 게이지는 전투 수행 중에만 찬다 — 틱을 돌려 채운다.
            Run(sim, MergeSystem.GaugeFullSeconds + 1f, dt: 0.5f);
        }

        // ---- 존재 ----

        [Test]
        public void SingleRobot_HasNoMerge()
        {
            var sim = new CombatSimulation(Robot(20f), Sandbag(), 6f, 120f, 0f);

            Assert.IsNull(sim.Merge, "합칠 상대가 없다");
            Assert.IsFalse(sim.TryMerge());
        }

        [Test]
        public void TwoRobots_HaveMerge()
        {
            CombatSimulation sim = TwoRobots();

            Assert.NotNull(sim.Merge);
            Assert.IsFalse(sim.Merge.IsReady, "처음엔 게이지가 비어 있다");
        }

        // ---- 화력 ----

        /// <summary>
        /// 합체 중 1초 피해 = **(A + B) × 1.8**.
        /// A 20 DPS + B 30 DPS = 50 → 90.
        /// 배율을 발사율에 얹었다면 발사 리듬이 바뀌어 이 값이 안 나온다.
        /// </summary>
        [Test]
        public void MergedOutput_IsSumOfBothTimesMultiplier()
        {
            CombatSimulation sim = TwoRobots(dmgA: 20f, dmgB: 30f);
            ChargeGauge(sim);

            Assert.IsTrue(sim.TryMerge(), "만충이면 발동한다");

            float dealt = Run(sim, 4f);

            Assert.AreEqual(360f, dealt, 20f, "4초 × (20 + 30) × 1.8 = 360");
        }

        /// <summary>
        /// 합체 전에는 **활성 로봇만** 쏜다 — 대기 쪽 화력은 안 들어간다.
        ///
        /// ⚠️ 교대를 막고 재야 한다. 안 그러면 대기 마운트가 만충이 되어 태그 인이 일어나고
        /// 두 로봇의 화력이 섞인다 — 실제로 처음엔 그렇게 재서 A 3발 + B 2발 = 120이 나왔다.
        /// </summary>
        [Test]
        public void BeforeMerge_OnlyActiveRobotFires()
        {
            CombatSimulation sim = TwoRobots(dmgA: 20f, dmgB: 30f);
            sim.Tag.Locked = true; // 교대를 막아 활성 화력만 잰다

            float dealt = Run(sim, 4f);

            Assert.AreEqual(80f, dealt, 20f, "4초 × 20 = 80 (A만)");
        }

        /// <summary>
        /// 반대로 잠그지 않으면 태그가 끼어든다 — 대기 마운트가 차면 교대하는 것이 정상이다.
        /// 이 테스트는 그 사실 자체를 고정한다(위 테스트가 왜 잠가야 하는지의 근거).
        /// </summary>
        [Test]
        public void WithoutLock_TagInterleavesBothRobots()
        {
            CombatSimulation sim = TwoRobots(dmgA: 20f, dmgB: 30f);

            Run(sim, 4f);

            Assert.AreEqual(1, sim.ActiveRobotIndex, "대기가 만충이라 교대했다");
        }

        /// <summary>
        /// 합체 배율은 **판정식 결과에** 곱해진다. 방어 45에 A 100·B 100이면
        /// (100−45) + (100−45) = 110이고 × 1.8 = 198이다.
        /// 발당피해에 곱했다면 (180−45) × 2 = 270이 되어 값이 달라진다.
        /// </summary>
        [Test]
        public void MergeMultiplier_AppliesAfterDefence()
        {
            CombatSimulation sim = TwoRobots(dmgA: 100f, dmgB: 100f, def: 45f);
            ChargeGauge(sim);
            sim.TryMerge();

            float dealt = Run(sim, 4f);

            Assert.AreEqual(792f, dealt, 40f, "4초 × ((100−45)+(100−45)) × 1.8 = 792");
            Assert.Less(dealt, 4f * 270f, "발당피해에 곱했을 때의 값(1080)보다 낮다");
        }

        // ---- 지속과 종료 ----

        [Test]
        public void Merge_LastsTwentySeconds_ThenReverts()
        {
            CombatSimulation sim = TwoRobots();
            ChargeGauge(sim);
            sim.TryMerge();

            Assert.IsTrue(sim.Merge.IsActive);

            Run(sim, MergeSystem.DurationSeconds + 1f, dt: 0.5f);

            Assert.IsFalse(sim.Merge.IsActive, "20초 뒤 풀린다");
        }

        /// <summary>합체 중에는 태그가 잠기고, 끝나면 풀린다(전투 문서 4장 상위 잠금).</summary>
        [Test]
        public void Merge_LocksAndUnlocksTagging()
        {
            CombatSimulation sim = TwoRobots();
            ChargeGauge(sim);

            sim.TryMerge();
            Assert.IsTrue(sim.Tag.Locked, "합체 중 태그 잠금");

            Run(sim, MergeSystem.DurationSeconds + 1f, dt: 0.5f);

            Assert.IsFalse(sim.Tag.Locked, "끝나면 풀린다");
        }

        /// <summary>**스테이지당 1회.** 끝난 뒤에도 다시 못 쓴다.</summary>
        [Test]
        public void Merge_CannotBeUsedTwiceInAStage()
        {
            CombatSimulation sim = TwoRobots();
            ChargeGauge(sim);
            sim.TryMerge();

            Run(sim, MergeSystem.DurationSeconds + MergeSystem.GaugeFullSeconds + 5f, dt: 0.5f);

            Assert.IsTrue(sim.Merge.UsedThisStage);
            Assert.IsFalse(sim.TryMerge(), "두 번째는 없다");
        }

        [Test]
        public void Merge_FailsBeforeGaugeIsFull()
        {
            CombatSimulation sim = TwoRobots();
            Run(sim, 10f, dt: 0.5f); // 90초에 못 미친다

            Assert.IsFalse(sim.TryMerge());
            Assert.IsFalse(sim.Tag.Locked, "실패했으니 잠기지도 않는다");
        }

        // ---- 결정론 ----

        [Test]
        public void Deterministic_SameSetupSameMergedDamage()
        {
            CombatSimulation a = TwoRobots(), b = TwoRobots();
            ChargeGauge(a); ChargeGauge(b);
            a.TryMerge(); b.TryMerge();

            Assert.AreEqual(Run(a, 3f), Run(b, 3f), D);
        }
    
        // ---- 회피 — 합체 중에는 두 보드의 스택을 모두 쓴다 (2026-09-17 · `260917_W03` 7-2 #1) ----

        /// <summary>
        /// **A 스택 0 · B 스택 2 인데 합체 중이면 피격이 무효가 된다.**
        ///
        /// ⚠️⚠️ **여기서 진짜 깨지기 쉬운 것은 무적 판정 쪽이다.** 대기 쪽이 피했는데
        /// 판정이 `Act.dodge.IsInvincible` 만 보면 **피하고도 맞는다** —
        /// 스택만 축나고 HP 는 그대로 깎이므로 화면에서는 「회피가 안 먹는다」로 보인다.
        /// 무적은 진영이 아니라 **몸**에 걸린다.
        /// </summary>
        [Test]
        public void 합체_중에는_대기_보드의_회피_스택도_쓴다()
        {
            CombatSimulation sim = Attacker();

            sim.BoosterCount = 0;            // 활성 쪽 그릇 없음
            sim.StandbyBoosterCount = 1;     // 대기 쪽 2 칸 (대수 × 2)
            sim.StandbyDodge.AddStacks(2);

            ChargeGauge(sim);
            Assert.IsTrue(sim.TryMerge(), "합체가 안 걸렸다 — 시험 전제가 깨졌다");

            float hp0 = sim.Robot.hp;
            for (int i = 0; i < 60 && sim.Result == CombatResult.InProgress; i++) sim.Tick(0.05f);

            Assert.Greater(sim.HitsTaken, 0, "맞지를 않았다 — 시험 전제가 깨졌다");
            Assert.Greater(sim.DamageAvoided, 0f,
                "대기 쪽 스택이 있는데 무효가 된 피해가 0 이다 — 합체가 그 그릇을 안 쓴다");
            Assert.Less(sim.StandbyDodge.Stacks, 2, "대기 쪽 스택이 안 줄었다");
            Assert.Greater(hp0 - sim.Robot.hp, -1f); // HP 는 줄 수도 있다(스택이 다 떨어진 뒤)
        }

        /// <summary>합체가 아니면 대기 쪽 스택은 **안 쓴다** — 로봇별 귀속이 기본이다.</summary>
        [Test]
        public void 합체가_아니면_대기_스택은_안_쓴다()
        {
            CombatSimulation sim = Attacker();

            sim.BoosterCount = 0;
            sim.StandbyBoosterCount = 1;
            sim.StandbyDodge.AddStacks(2);

            for (int i = 0; i < 60 && sim.Result == CombatResult.InProgress; i++) sim.Tick(0.05f);

            Assert.AreEqual(2, sim.StandbyDodge.Stacks,
                "합체도 아닌데 대기 로봇의 회피 스택이 줄었다 — 회피 재고는 로봇별이다");
            Assert.AreEqual(0f, sim.DamageAvoided, D);
        }

        /// <summary>때리는 적을 세운 판 — 위 회피 시험 둘이 쓴다.</summary>
        /// <summary>
        /// 때리는 적을 세운 판.
        ///
        /// ⚠️⚠️ **자동 교대를 끈다.** 켜 두면 마운트가 비는 순간 **진영이 뒤바뀌어**
        /// 「대기 쪽 스택」이 활성 쪽 스택이 된다 — 첫 판에서 그것 때문에
        /// 「합체도 아닌데 대기 스택이 줄었다」로 빨개졌고, **틀린 것은 코드가 아니라
        /// 시험의 전제**였다. 회피를 보는 시험에서 교대는 다른 축이다.
        /// </summary>
        private static CombatSimulation Attacker()
        {
            var spawns = new List<EnemySpawn>
            {
                new EnemySpawn { label = "때리는 적", hp = 100000f, def = 0f, atk = 5f,
                    attackRange = 1f, attackInterval = 0.2f, moveSpeed = 20f },
            };

            var sim = new CombatSimulation(Robot(20f), Robot(30f),
                new MountLoad(4, Stacks()), new MountLoad(4, Stacks()),
                spawns, arenaRadius: 3f, challengeTime: 1000f, spawnCadence: 0f);
            sim.AutoTagEnabled = false;
            return sim;
        }
}
}
