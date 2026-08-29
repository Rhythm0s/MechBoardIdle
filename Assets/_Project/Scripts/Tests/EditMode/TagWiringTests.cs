using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 태그 배선 — `TagBattle`이 전투 시뮬 안에서 실제로 도는가.
    ///
    /// 순수 조정자 테스트(TagBattleTests)는 규칙을 보고, 여기서는 **시뮬 틱을 돌려**
    /// 대기 로봇의 공장이 실제로 차고 교대가 일어나는지를 본다.
    ///
    /// ⚠️ 교대가 일어나면 `Tag.StandbyMount`가 가리키는 쪽이 바뀐다 —
    /// 「A의 마운트」를 계속 보려면 객체를 들고 있어야 한다.
    /// </summary>
    public sealed class TagWiringTests
    {
        private const float D = 0.001f;

        private static Dictionary<MountItem, float> Stacks(float ammo = 10f) =>
            new Dictionary<MountItem, float>
            {
                { MountItem.Pierce, ammo }, { MountItem.Split, ammo },
                { MountItem.Explosive, ammo }, { MountItem.Drone, ammo },
            };

        private static RobotSetup Robot(float hp = 100000f) => new RobotSetup
        {
            hp = hp, mountCoef = 1f, moduleMult = 1f,
            attackRange = 100f, radius = 0f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = new List<AmmoLine> { new AmmoLine(AmmoKind.Pierce, 20f, 1f) },
            ammoCapacity = 40f, ammoInitialStock = 0f,
            droneSlots = 0, droneReleaseRate = 0f, droneCharge = 0f, droneAttackRange = 0f,
        };

        private static List<EnemySpawn> Sandbag() => new List<EnemySpawn>
        {
            new EnemySpawn { label = "샌드백", hp = 1000000f, def = 0f, atk = 0f,
                moveSpeed = 0f, attackRange = 0.5f, attackInterval = 1f },
        };

        private static CombatSimulation TwoRobots(out MountLoad mountA, out MountLoad mountB, int slots = 4)
        {
            mountA = new MountLoad(slots, Stacks());
            mountB = new MountLoad(slots, Stacks());
            return new CombatSimulation(Robot(), Robot(), mountA, mountB,
                Sandbag(), arenaRadius: 6f, challengeTime: 120f, spawnCadence: 0f);
        }

        private static CombatSimulation TwoRobots(int slots = 4) => TwoRobots(out _, out _, slots);

        private static void Run(CombatSimulation sim, float seconds, float dt = 0.05f)
        {
            int steps = Mathf.CeilToInt(seconds / dt);
            for (int i = 0; i < steps && sim.Result == CombatResult.InProgress; i++) sim.Tick(dt);
        }

        // ---- 단일 로봇 경로가 그대로 돈다 ----

        /// <summary>로봇이 하나면 태그가 없다 — 격리 전투와 기존 경로가 그대로다.</summary>
        [Test]
        public void SingleRobot_HasNoTagPartner()
        {
            var sim = new CombatSimulation(Robot(), Sandbag(), 6f, 120f, 0f);

            Assert.IsFalse(sim.HasTagPartner);
            Assert.IsNull(sim.Tag, "태그 조정자가 없다");
            Assert.AreEqual(0, sim.ActiveRobotIndex);
        }

        [Test]
        public void TwoRobots_HaveTagPartner()
        {
            CombatSimulation sim = TwoRobots();

            Assert.IsTrue(sim.HasTagPartner);
            Assert.NotNull(sim.Tag);
        }

        // ---- 대기 로봇의 공장이 돈다 ----

        /// <summary>
        /// **대기 중에도 창고가 차고 마운트가 채워진다**(전투 문서 1장).
        /// 이게 없으면 태그 인 순간 빈손으로 나와 「축적 → 만재 등장」이 성립하지 않는다.
        /// </summary>
        [Test]
        public void StandbyRobot_KeepsProducing()
        {
            CombatSimulation sim = TwoRobots(out MountLoad mountA, out MountLoad mountB);
            sim.AmmoSupplyRate = 0f;          // 활성(A)은 놀린다
            sim.StandbyAmmoSupplyRate = 4f;   // 대기(B) 보드만 돌린다

            Run(sim, 2f);

            Assert.Greater(mountB.Total, 0f, "B의 마운트에 쌓인다");
            Assert.AreEqual(0f, mountA.Total, D, "A는 놀렸으니 비어 있다");
        }

        /// <summary>
        /// 대기에 물건이 생기면 **소진 트리거가 먼저 터진다.** 전투 시작 시 양쪽 마운트가
        /// 비어 있으므로 이것이 정상 흐름이다 — 갈 곳이 생긴 순간 마른 쪽이 물러난다.
        /// </summary>
        [Test]
        public void ActiveEmpty_StandbyHasSomething_SwapsOnDepletedTrigger()
        {
            CombatSimulation sim = TwoRobots();
            sim.AmmoSupplyRate = 0f;
            sim.StandbyAmmoSupplyRate = 4f;

            Run(sim, 2f);

            Assert.AreEqual(1, sim.ActiveRobotIndex, "B가 나왔다");
            Assert.IsFalse(sim.Tag.LastTagFiredSkill, "만충이 아니라 소진이므로 스킬은 없다");
        }

        /// <summary>대기가 만충이면 **만재 등장**으로 나오고 태그 스킬이 붙는다.</summary>
        [Test]
        public void StandbyFull_TriggersTagIn_WithSkill()
        {
            CombatSimulation sim = TwoRobots(out MountLoad mountA, out MountLoad mountB, slots: 1);

            // 활성(A)에 미리 실어 둔다 — 안 그러면 첫 틱에 소진 트리거가 먼저 터져
            // 만충 경로를 볼 수 없다. 양쪽이 빈 채로 시작하는 것이 기본 상태이기 때문이다.
            mountA.Load(MountItem.Pierce, 5f);

            sim.AmmoSupplyRate = 0f;
            sim.StandbyAmmoSupplyRate = 20f;  // 대기(B)는 금방 만충(슬롯 1 × 스택 10)

            Run(sim, 2f);

            Assert.AreEqual(1, sim.ActiveRobotIndex, "만충 트리거로 B가 나온다");
            Assert.IsTrue(sim.Tag.LastTagFiredSkill, "만재 등장이므로 태그 스킬이 나간다");
            Assert.Greater(sim.Tag.LastTagSkillDrained, 0f, "적재를 전량 소진했다");

            // 소진 직후 마운트는 비어 있지 않다 — **벨트가 곧바로 다시 채운다.**
            // 「마운트가 빈 동안 화력이 죽고, **물류가 좋을수록 그 공백이 짧다**」가 설계이고,
            // 유입 20/초로 돌린 이 구성에서는 공백이 사실상 0이다. 그것이 물류를 키우는 보상이다.
            Assert.Greater(mountB.Total, 0f, "다시 채워지고 있다");
        }

        [Test]
        public void TagSkill_ReportsDrainedAmountForDamage()
        {
            CombatSimulation sim = TwoRobots(out MountLoad mountA, out _, slots: 1);
            mountA.Load(MountItem.Pierce, 5f); // 소진 트리거를 막아 만충 경로를 본다

            sim.AmmoSupplyRate = 0f;
            sim.StandbyAmmoSupplyRate = 20f;

            Run(sim, 2f);

            Assert.Greater(sim.Tag.LastTagSkillDrained, 0f, "소진량이 보고된다");
            Assert.Greater(sim.Tag.TagSkillDamage(52.6f), 0f, "그 값이 피해 계산의 입력이다");
        }

        // ---- 활성 로봇이 바뀐다 ----

        /// <summary>교대하면 `Robot`이 가리키는 몸체도 바뀐다 — HP가 각자다.</summary>
        [Test]
        public void Tagging_SwapsTheActiveBody()
        {
            CombatSimulation sim = TwoRobots(slots: 1);
            CombatEntity before = sim.Robot;

            sim.AmmoSupplyRate = 0f;
            sim.StandbyAmmoSupplyRate = 20f;
            Run(sim, 2f);

            Assert.AreNotSame(before, sim.Robot, "다른 몸체가 나와 있다");
            Assert.AreEqual("로봇B", sim.Robot.label);
        }

        /// <summary>
        /// 각 로봇이 **자기 창고**를 갖는다. 창고를 공유하면 대기 보드를 돌릴 이유가 사라진다.
        /// </summary>
        [Test]
        public void EachRobot_HasItsOwnWarehouse()
        {
            CombatSimulation sim = TwoRobots(out MountLoad mountA, out MountLoad mountB);
            sim.AmmoSupplyRate = 4f;
            sim.StandbyAmmoSupplyRate = 0f;

            Run(sim, 1f);

            Assert.Greater(mountA.Total, 0f, "활성 쪽만 찬다");
            Assert.AreEqual(0f, mountB.Total, D, "대기 쪽은 안 찬다");
        }

        // ---- 결정론 ----

        [Test]
        public void Deterministic_SameSetupSameTagTiming()
        {
            CombatSimulation a = TwoRobots(slots: 1), b = TwoRobots(slots: 1);
            a.AmmoSupplyRate = 0f; a.StandbyAmmoSupplyRate = 20f;
            b.AmmoSupplyRate = 0f; b.StandbyAmmoSupplyRate = 20f;

            Run(a, 4f);
            Run(b, 4f);

            Assert.AreEqual(a.ActiveRobotIndex, b.ActiveRobotIndex);
            Assert.AreEqual(a.Tag.Tag.TotalTags, b.Tag.Tag.TotalTags, "태그 횟수까지 재현");
        }

        /// <summary>쿨다운이 매 프레임 교대를 막는다 — 안 막으면 활성이 진동한다.</summary>
        [Test]
        public void Cooldown_PreventsPerFrameSwapping()
        {
            CombatSimulation sim = TwoRobots(slots: 1);
            sim.AmmoSupplyRate = 20f;
            sim.StandbyAmmoSupplyRate = 20f; // 둘 다 금방 찬다

            Run(sim, 4f);

            Assert.LessOrEqual(sim.Tag.Tag.TotalTags, 1, "4초 안에는 쿨다운 5초 때문에 한 번뿐");
        }
    }
}
