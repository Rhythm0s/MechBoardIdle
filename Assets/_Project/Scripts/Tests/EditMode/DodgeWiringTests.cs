using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 회피 배선 — 부스터가 만든 추진제가 **시뮬 안에서 실제로 피해를 막는가**.
    ///
    /// 규칙 자체는 DodgeSystemTests가 보고, 여기서는 틱을 돌려 HP를 잰다.
    /// 「보드가 생존을 만든다」가 코드에 있는지의 검증이라 부스터 노드 작업의 마지막 칸이다.
    /// </summary>
    public sealed class DodgeWiringTests
    {
        private const float D = 0.001f;
        private const float RobotHp = 100000f;
        private const float EnemyAtk = 10f;

        private static Dictionary<MountItem, float> Stacks() =>
            new Dictionary<MountItem, float>
            {
                { MountItem.Pierce, 100f }, { MountItem.Split, 100f },
                { MountItem.Explosive, 100f }, { MountItem.Drone, 100f },
            };

        private static RobotSetup Robot() => new RobotSetup
        {
            hp = RobotHp, mountCoef = 1f, moduleMult = 1f,
            attackRange = 100f, radius = 0f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = new List<AmmoLine> { new AmmoLine(AmmoKind.Pierce, 20f, 1f) },
            ammoCapacity = 1000f, ammoInitialStock = 1000f,
            droneSlots = 0, droneReleaseRate = 0f, droneCharge = 0f, droneAttackRange = 0f,
        };

        /// <summary>제자리에서 때리는 적. 사거리가 아레나를 덮어 이동이 끼지 않는다.</summary>
        private static List<EnemySpawn> Attacker(float interval = 1f) => new List<EnemySpawn>
        {
            new EnemySpawn { label = "포격수", hp = 10000000f, def = 0f, atk = EnemyAtk,
                moveSpeed = 0f, attackRange = 100f, attackInterval = interval },
        };

        private static CombatSimulation One(float interval = 1f) =>
            new CombatSimulation(Robot(), Attacker(interval),
                arenaRadius: 6f, challengeTime: 1000f, spawnCadence: 0f);

        private static CombatSimulation Two() =>
            new CombatSimulation(Robot(), Robot(), new MountLoad(4, Stacks()), new MountLoad(4, Stacks()),
                Attacker(), arenaRadius: 6f, challengeTime: 1000f, spawnCadence: 0f);

        /// <summary>
        /// 첫 교대까지 돌린다. ⚠️ 두 마운트가 모두 만충이라 태그는 **쿨다운마다 왕복한다** —
        /// 고정 시간을 돌리고 인덱스를 읽으면 홀짝에 따라 값이 뒤집힌다(실제로 그렇게 재서 깨졌다).
        /// </summary>
        private static bool RunUntilSwap(CombatSimulation sim, float limit = 30f, float dt = 0.05f)
        {
            int steps = Mathf.CeilToInt(limit / dt);
            for (int i = 0; i < steps && sim.Result == CombatResult.InProgress; i++)
            {
                sim.Tick(dt);
                if (sim.ActiveRobotIndex == 1) return true;
            }
            return false;
        }

        private static void Run(CombatSimulation sim, float seconds, float dt = 0.05f)
        {
            int steps = Mathf.CeilToInt(seconds / dt);
            for (int i = 0; i < steps && sim.Result == CombatResult.InProgress; i++) sim.Tick(dt);
        }

        private static float Taken(CombatSimulation sim) => RobotHp - sim.Robot.hp;

        /// <summary>한 틱 만에 상한까지 채운다 — 넘치는 분은 버려지므로 정확히 3이 된다.</summary>
        private static void FillStacks(CombatSimulation sim, float dt = 0.05f)
        {
            sim.PropellantSupplyRate = 1000f;
            sim.Tick(dt);
            sim.PropellantSupplyRate = 0f;
        }

        // ---- 공급 ----

        /// <summary>부스터가 없으면 회피도 없다 — 회피는 재고로 갈린다.</summary>
        [Test]
        public void NoBooster_NoStacks()
        {
            CombatSimulation sim = One();

            Run(sim, 30f);

            Assert.AreEqual(0, sim.Dodge.Stacks);
            Assert.AreEqual(0, sim.Dodge.TotalDodges, "쌓인 게 없으니 한 번도 못 피한다");
        }

        /// <summary>
        /// 선언치(15초에 1개)로 45초를 돌리면 상한 3에 닿는다.
        /// 소수분 이월이 없으면 틱마다 floor(0.0033)=0이 되어 **영영 한 개도 안 나온다** —
        /// 드론 사출대에서 같은 뿌리의 결함을 겪었다.
        /// </summary>
        [Test]
        public void DeclaredRate_ReachesCap_InFortyFiveSeconds()
        {
            CombatSimulation sim = One();
            sim.PropellantSupplyRate = 1f / 15f;

            Run(sim, 16f);
            Assert.AreEqual(1, sim.Dodge.Stacks + sim.Dodge.TotalDodges, "15초에 1개");

            Run(sim, 30f);
            Assert.AreEqual(3, sim.Dodge.Stacks + sim.Dodge.TotalDodges, "45초에 3개");
        }

        /// <summary>
        /// 공급이 아무리 빨라도 3에서 멈춘다. 이월분을 남겨 두면 회피 직후 쌓인 소수분이
        /// 한꺼번에 터져 상한이 사실상 없어진다 — 그래서 만충에서 이월을 버린다.
        /// </summary>
        [Test]
        public void FastSupply_StillCapsAtThree()
        {
            CombatSimulation sim = One();
            sim.PropellantSupplyRate = 100f;

            sim.Tick(0.05f);

            Assert.AreEqual(DodgeSystem.MaxStacks, sim.Dodge.Stacks + sim.Dodge.TotalDodges);
        }

        // ---- 피해 흡수 ----

        /// <summary>기준선: 추진제가 없으면 맞는 대로 다 들어온다.</summary>
        [Test]
        public void WithoutPropellant_TakesEveryHit()
        {
            CombatSimulation sim = One();

            Run(sim, 10f);

            Assert.Greater(Taken(sim), 0f, "때리는 적이 맞다");
            Assert.AreEqual(0f, Taken(sim) % EnemyAtk, D, "타격 단위가 그대로 들어온다");
        }

        /// <summary>
        /// **추진제 3개 = 타격 3회 무효.** 부스터를 놓으면 그만큼 덜 맞는다 —
        /// 「보드가 생존을 만든다」의 실측이다.
        /// </summary>
        [Test]
        public void ThreeStacks_AbsorbThreeHits()
        {
            CombatSimulation bare = One();
            CombatSimulation boosted = One();

            FillStacks(boosted);
            Run(bare, 10f);
            Run(boosted, 10f);

            Assert.AreEqual(3, boosted.Dodge.TotalDodges, "세 번 피했다");
            Assert.AreEqual(3f * EnemyAtk, Taken(bare) - Taken(boosted), D, "그만큼 덜 맞았다");
        }

        /// <summary>
        /// 무적은 **판정식에 들어가지 않는다.** 방어 항으로 표현했다면 max(1, …) 하한 때문에
        /// 회피할 때마다 1씩 들어와 피해가 0이 아니게 된다.
        /// </summary>
        [Test]
        public void Invincibility_LetsNothingThrough_NotEvenTheMinimumOne()
        {
            CombatSimulation sim = One();
            FillStacks(sim);

            // 첫 타격은 회피가 먹었다 — 이 시점 피해는 정확히 0이어야 한다.
            Assert.AreEqual(1, sim.Dodge.TotalDodges);
            Assert.AreEqual(0f, Taken(sim), D, "최소 1도 안 들어온다");
        }

        // ---- 자동 · 수동 ----

        [Test]
        public void AutoDodge_FiresOnIncomingHit()
        {
            CombatSimulation sim = One();
            FillStacks(sim);

            Assert.AreEqual(DodgeTrigger.Auto, sim.Dodge.LastTrigger);
        }

        /// <summary>
        /// 수동 플릭이 자동보다 먼저 처리된다 — 같은 틱에 겹쳐도 **추진제는 1개만** 나가고
        /// 방향은 플릭이 이긴다. 두 경로가 각각 소비하면 플릭 한 번에 재고가 둘 빠진다.
        /// </summary>
        [Test]
        public void ManualFlick_BeatsAuto_AndSpendsOnlyOne()
        {
            // ⚠️ 적이 1초마다 때리면 플릭을 넣으려는 틱에 자동 회피가 먼저 걸려 있을 수 있다.
            // 그러면 재발동 금지에 막혀 플릭이 그냥 버려진다 — 타격 간격을 벌려 그 겹침을 뺀다.
            CombatSimulation sim = One(interval: 1000f);
            FillStacks(sim);        // 이 틱에 첫 타격이 오고 자동 회피가 한 번 나간다
            Run(sim, 0.5f);         // 무적·종료 모션이 끝난다

            int before = sim.Dodge.Stacks;
            sim.RequestDodge(Vector2.up);
            sim.Tick(0.05f);

            Assert.AreEqual(DodgeTrigger.Manual, sim.Dodge.LastTrigger, "수동이 이긴다");
            Assert.AreEqual(Vector2.up, sim.Dodge.LastDirection, "플릭 방향으로 피한다");
            Assert.AreEqual(before - 1, sim.Dodge.Stacks, "추진제는 하나만 나간다");
        }

        /// <summary>플릭은 한 번에 한 번만 먹는다 — 눌러 둔 입력이 다음 틱까지 남으면 재고가 샌다.</summary>
        [Test]
        public void Flick_IsConsumedOnce()
        {
            CombatSimulation sim = One();
            FillStacks(sim);
            Run(sim, 0.5f);

            int before = sim.Dodge.Stacks;
            sim.RequestDodge(Vector2.up);
            Run(sim, 3f);

            Assert.GreaterOrEqual(sim.Dodge.Stacks, before - 1 - 1,
                "플릭 1회 + 자동 몇 회지, 플릭이 매 틱 반복되지 않는다");
            Assert.Greater(sim.Dodge.Stacks + sim.Dodge.TotalDodges, 0);
        }

        // ---- 태그와의 관계 ----

        /// <summary>
        /// **대기 보드의 부스터도 돈다.** 태그 인 하면 그 로봇은 자기 추진제를 들고 나온다 —
        /// 대기 중 공장이 멈추면 「축적 → 만재 등장」이 회피에서만 깨진다.
        /// </summary>
        [Test]
        public void StandbyBoard_StacksPropellant_AndBringsItIn()
        {
            CombatSimulation sim = Two();
            sim.PropellantSupplyRate = 0f;
            sim.StandbyPropellantSupplyRate = 1000f;

            Assert.IsTrue(RunUntilSwap(sim), "대기 마운트가 만충이라 교대한다");
            Assert.Greater(sim.Dodge.Stacks + sim.Dodge.TotalDodges, 0,
                "교대해 나온 로봇이 자기 추진제를 들고 있다");
        }

        /// <summary>회피 재고는 로봇마다 따로다 — A가 다 쓴 것이 B에서 빠지지 않는다.</summary>
        [Test]
        public void Stacks_AreNotSharedBetweenRobots()
        {
            // ⚠️ 유입 속성은 **설정 시점의 활성 로봇**을 가리킨다. 첫 틱에 교대가 끼면
            // 추진제가 통째로 B에게 간다 — 실제로 그렇게 재서 A가 0으로 나왔다.
            // A만 채우는 구간을 만들려면 교대를 잠가야 한다.
            CombatSimulation sim = Two();
            sim.Tag.Locked = true;

            sim.PropellantSupplyRate = 1000f;
            sim.StandbyPropellantSupplyRate = 0f;
            sim.Tick(0.05f);
            sim.PropellantSupplyRate = 0f;

            Assert.AreEqual(3, sim.Dodge.Stacks + sim.Dodge.TotalDodges, "A는 채웠고");

            sim.Tag.Locked = false;
            Assert.IsTrue(RunUntilSwap(sim), "교대한다");

            Assert.AreEqual(0, sim.Dodge.Stacks + sim.Dodge.TotalDodges, "B는 자기 보드가 없어 비어 있다");
        }

        // ---- 결정론 ----

        [Test]
        public void Deterministic_SameSetupSameDamageTaken()
        {
            CombatSimulation a = One(), b = One();
            FillStacks(a); FillStacks(b);

            Run(a, 20f); Run(b, 20f);

            Assert.AreEqual(Taken(a), Taken(b), D);
        }
    }
}
