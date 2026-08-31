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

        // ---- 스킬이 실제로 때린다(260831_V09) ----

        /// <summary>
        /// **태그 스킬이 피해를 준다.** 이 파일의 존재 이유가 여기로 옮겨 왔다 —
        /// 종전에는 마운트를 비우기만 하고 `TagSkillDamage`를 부르는 곳이 테스트뿐이라,
        /// 스킬이 나갈 때마다 화력이 죽고 **아무 일도 일어나지 않았다.** 순손실이었다.
        /// </summary>
        [Test]
        public void TagSkill_ActuallyDealsDamage()
        {
            CombatSimulation sim = TwoRobots(out MountLoad mountA, out _, slots: 1);
            mountA.Load(MountItem.Pierce, 5f);
            sim.AmmoSupplyRate = 0f;
            sim.StandbyAmmoSupplyRate = 20f;

            Run(sim, 0.05f); // 적은 첫 틱에 등장한다 — 그 전에 읽으면 목록이 비어 있다
            float hp0 = sim.Enemies[0].hp;
            Run(sim, 2f);

            Assert.IsTrue(sim.Tag.LastTagFiredSkill, "스킬이 나갔다");
            Assert.Greater(sim.LastTagSkillDamage, 0f, "피해가 실제로 들어갔다");
            Assert.Less(sim.Enemies[0].hp, hp0, "적 HP가 줄었다");
        }

        /// <summary>
        /// 피해 = **적재량 × 평균 발당 실피해**(GrandEntrance 확정식).
        /// 관통만 10발 실린 마운트에서 발당 20이면 200이다.
        /// </summary>
        [Test]
        public void TagSkill_DamageIsLoadTimesAverage()
        {
            CombatSimulation sim = TwoRobots(out MountLoad mountA, out _, slots: 1);
            mountA.Load(MountItem.Pierce, 5f);
            sim.AmmoSupplyRate = 0f;
            sim.StandbyAmmoSupplyRate = 20f;

            Run(sim, 2f);

            Assert.AreEqual(sim.Tag.LastTagSkillDrained * 20f, sim.LastTagSkillDamage, 0.5f,
                "소진량 × 발당 20");
        }

        /// <summary>
        /// **표적이 없으면 발동을 보류하고 재고도 안 비운다**(260831_V09).
        /// 비우기와 때리기는 한 동작이라, 때릴 것이 없으면 둘 다 일어나지 않는다.
        /// </summary>
        [Test]
        public void TagSkill_WithoutTargets_KeepsTheMountFull()
        {
            var mountA = new MountLoad(1, Stacks());
            var mountB = new MountLoad(1, Stacks());
            var sim = new CombatSimulation(Robot(), Robot(), mountA, mountB,
                new List<EnemySpawn>(), arenaRadius: 6f, challengeTime: 120f, spawnCadence: 0f);
            sim.Endless = true; // 적이 없어도 승리로 끝나지 않게

            mountA.Load(MountItem.Pierce, 5f);
            sim.AmmoSupplyRate = 0f;
            sim.StandbyAmmoSupplyRate = 20f;

            Run(sim, 2f);

            Assert.IsFalse(sim.Tag.LastTagFiredSkill, "스킬은 보류됐다");
            Assert.AreEqual(0f, sim.LastTagSkillDamage, D);
            Assert.Greater(mountB.Total, 0f, "만재를 유지한다 — 적이 나타나면 그때 터진다");
        }

        /// <summary>
        /// **최근접 1체를 때린다**(버스트와 같은 규칙). 광역이 아니므로 먼 적은 멀쩡하다.
        /// </summary>
        [Test]
        public void TagSkill_HitsTheNearestOnly()
        {
            var mountA = new MountLoad(1, Stacks());
            var mountB = new MountLoad(1, Stacks());
            var enemies = new List<EnemySpawn>
            {
                new EnemySpawn { label = "가까운", hp = 1000000f, def = 0f, atk = 0f,
                    moveSpeed = 0f, attackRange = 0.5f, attackInterval = 1f },
                new EnemySpawn { label = "먼", hp = 1000000f, def = 0f, atk = 0f,
                    moveSpeed = 0f, attackRange = 0.5f, attackInterval = 1f },
            };
            var sim = new CombatSimulation(Robot(), Robot(), mountA, mountB,
                enemies, arenaRadius: 6f, challengeTime: 120f, spawnCadence: 0f);

            mountA.Load(MountItem.Pierce, 5f);
            sim.AmmoSupplyRate = 0f;
            sim.StandbyAmmoSupplyRate = 20f;

            Run(sim, 2f);

            Assert.IsTrue(sim.Tag.LastTagFiredSkill);

            // 둘 다 사격에 맞으므로 「한쪽만 줄었다」로는 못 잰다 —
            // 스킬 피해만큼의 격차가 벌어졌는지로 잰다.
            float lower = Mathf.Min(sim.Enemies[0].hp, sim.Enemies[1].hp);
            float higher = Mathf.Max(sim.Enemies[0].hp, sim.Enemies[1].hp);
            Assert.Greater(higher - lower, sim.LastTagSkillDamage * 0.5f,
                "한 체만 스킬을 맞았다 — 광역이 아니다");
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

        // ---- 수동 태그(HUD 버튼) ----

        /// <summary>
        /// 수동 태그는 **시뮬의 활성 인덱스까지** 바꾼다.
        /// 조정자만 바꾸면 화면에는 B가, 사격은 A가 도는 어긋남이 생긴다.
        /// </summary>
        [Test]
        public void ManualTag_SwapsSimulationActiveIndex()
        {
            CombatSimulation sim = TwoRobots();

            Assert.AreEqual(0, sim.ActiveRobotIndex);
            Assert.IsTrue(sim.TryManualTag());

            Assert.AreEqual(1, sim.ActiveRobotIndex, "시뮬이 따라왔다");
            Assert.AreEqual(sim.Tag.ActiveIndex, sim.ActiveRobotIndex, "조정자와 시뮬이 같은 곳을 본다");
        }

        /// <summary>수동 태그도 쿨다운을 지킨다 — 버튼 연타로 규칙을 우회하지 못한다.</summary>
        [Test]
        public void ManualTag_RespectsCooldown()
        {
            CombatSimulation sim = TwoRobots();
            sim.TryManualTag();

            Assert.IsFalse(sim.TryManualTag(), "쿨다운 중엔 다시 안 된다");
            Assert.AreEqual(1, sim.ActiveRobotIndex, "실패했으니 자리도 그대로다");
        }

        /// <summary>합체 중에는 수동 태그도 잠긴다(상위 잠금).</summary>
        [Test]
        public void ManualTag_IsLockedDuringMerge()
        {
            CombatSimulation sim = TwoRobots();
            sim.Tag.Locked = true;

            Assert.IsFalse(sim.TryManualTag());
            Assert.AreEqual(0, sim.ActiveRobotIndex);
        }

        /// <summary>로봇이 하나면 수동 태그가 없다 — 갈 곳이 없다.</summary>
        [Test]
        public void ManualTag_DoesNothing_WithOneRobot()
        {
            var sim = new CombatSimulation(Robot(), Sandbag(), 6f, 120f, 0f);

            Assert.IsFalse(sim.TryManualTag());
        }

        // ---- 드론 로봇의 마운트 (260829_V03 §판정②) ----

        /// <summary>드론 운용기. 본체 무기가 없고 화력이 전부 드론에서 나온다.</summary>
        private static RobotSetup DroneRobot()
        {
            RobotSetup r = Robot();
            r.lines = new List<AmmoLine>();
            r.ammoInitialStock = 0f;
            r.droneSlots = 3;
            r.droneReleaseRate = 1f;
            r.droneCharge = 100f;
            r.droneAttackRange = 100f;
            return r;
        }

        /// <summary>
        /// **드론은 마운트에 실린다.** 사출대는 재고 층이 아니라 화면상의 출구다(260829_V03).
        ///
        /// 이걸 사출대가 자기 큐로 들고 있던 동안 로봇 B의 마운트는 **구조적으로 영구히 비어**
        /// 「활성 소진 → 대기 복귀」 트리거가 켜질 수 없었다 — 태그 두 트리거가 둘 다 막혀 있었다.
        /// </summary>
        [Test]
        public void DroneRobot_LoadsItsMount_UnblockingTheDepletionTag()
        {
            var mountA = new MountLoad(MountLoad.SlotsRobotA, Stacks());
            var mountB = new MountLoad(MountLoad.SlotsRobotB); // 드론 스택 상한 미확정 → 안 넘긴다
            var sim = new CombatSimulation(Robot(), DroneRobot(), mountA, mountB,
                Sandbag(), arenaRadius: 6f, challengeTime: 120f, spawnCadence: 0f);

            sim.Tag.Locked = true; // 교대를 막고 대기 축적만 잰다
            sim.StandbyDroneInflowRate = 2f;

            Assert.IsTrue(mountB.IsEmpty, "처음엔 비어 있다");

            Run(sim, 3f);

            Assert.AreEqual(6f, mountB.AmountOf(MountItem.Drone), 0.2f, "2기/초 × 3초");
            Assert.IsFalse(sim.Tag.StandbyMount.IsEmpty, "소진 복귀 트리거의 전제가 섰다");
        }

        /// <summary>
        /// 사출은 **마운트에서 뺀다.** 유입이 슬롯 처리량보다 느리면 실린 즉시 나가므로
        /// 마운트는 거의 비어 있고, 나간 만큼이 그대로 피해가 된다.
        ///
        /// ⚠️ 드론은 **한 번 쏘고 소멸한다**(1기 = 1회 타격 = 충전량 전량). 그래서
        /// 나간 수를 `Drones.Count`로 세면 안 된다 — 같은 틱에 이미 사라져 있다.
        /// </summary>
        [Test]
        public void LaunchingDrones_DrainsTheMount()
        {
            var mountA = new MountLoad(MountLoad.SlotsRobotB);
            var sim = new CombatSimulation(DroneRobot(), Robot(), mountA,
                new MountLoad(MountLoad.SlotsRobotA, Stacks()),
                Sandbag(), arenaRadius: 6f, challengeTime: 120f, spawnCadence: 0f);

            sim.Tag.Locked = true;
            sim.DroneInflowRate = 2f; // 슬롯 처리량 3기/초보다 느리다 → 유입이 병목

            Run(sim, 6f);

            Assert.AreEqual(1200f, sim.DroneDamageDealt, 200f, "2기/초 × 6초 × 기당 100");
            Assert.Less(mountA.AmountOf(MountItem.Drone), 1.5f,
                "실린 만큼 그대로 나갔다 — 마운트에 쌓이지 않는다");
        }

        /// <summary>
        /// **슬롯이 병목이면 마운트에 쌓인다.** 실효 방출량 = min(유입, 슬롯 × 방출률)이고,
        /// 넘치는 분이 갈 곳이 마운트라는 것이 「마운트가 재고 층」의 실측이다.
        /// 사출대가 자기 큐를 들고 있던 동안에는 이 잉여가 마운트 밖에 쌓여
        /// 태그가 볼 수 없었다.
        /// </summary>
        [Test]
        public void WhenSlotsAreTheBottleneck_SurplusPilesUpInTheMount()
        {
            var mountA = new MountLoad(MountLoad.SlotsRobotB);
            var sim = new CombatSimulation(DroneRobot(), Robot(), mountA,
                new MountLoad(MountLoad.SlotsRobotA, Stacks()),
                Sandbag(), arenaRadius: 6f, challengeTime: 120f, spawnCadence: 0f);

            sim.Tag.Locked = true;
            sim.DroneInflowRate = 10f; // 슬롯 처리량 3기/초를 훨씬 넘는다

            Run(sim, 6f);

            Assert.AreEqual(1800f, sim.DroneDamageDealt, 300f, "3기/초 상한 × 6초 × 100");
            Assert.AreEqual(42f, mountA.AmountOf(MountItem.Drone), 4f, "60 실려 18 나갔다");
        }
    }
}
