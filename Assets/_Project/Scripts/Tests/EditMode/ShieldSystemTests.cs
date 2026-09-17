using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 쉴드 — 생존 세 층의 가운데 (2026-09-17 사용자 「보호막 되살릴 것」 · `260917_W05` 4장).
    ///
    /// 지키는 것은 셋이다.
    /// 1. **넘치면 HP 로 간다** — 「1 이라도 남으면 통째로 막는다」면 쉴드 1 = 무적 1회라
    ///    회피와 같은 일을 하는 층이 둘 생긴다.
    /// 2. **합체 중에는 두 보드의 게이지를 합산해 쓴다**(사용자 확정 · `260917_W06` 5장).
    /// 3. **최대치 0 이면 지금 배포와 거동이 같다** — 값이 정해지기 전에 켜지지 않는다.
    /// </summary>
    public sealed class ShieldSystemTests
    {
        private const float D = 0.0001f;

        // ── 순수 로직 ────────────────────────────────────────────────────────

        [Test]
        public void 최대치가_0이면_쉴드가_없는_것과_같다()
        {
            var s = new ShieldSystem { ChargeRate = 100f };
            s.Tick(10f);

            Assert.IsFalse(s.Exists, "최대치 0 인데 쉴드가 있다고 한다");
            Assert.AreEqual(0f, s.Value, D, "그릇이 0 인데 찼다");
            Assert.AreEqual(50f, s.Absorb(50f), D, "쉴드가 없는데 피해를 먹었다");
        }

        [Test]
        public void 한_방이_남은_쉴드보다_크면_넘친_만큼_돌려준다()
        {
            // ⚠️⚠️ **이 시험이 「회피와 같은 층이 둘」을 막는다.**
            //    통째로 막는 쪽으로 누가 바꾸면 여기서 걸린다.
            var s = new ShieldSystem { Max = 100f, ChargeRate = 1000f };
            s.Tick(1f);
            Assert.AreEqual(100f, s.Value, D, "시험 전제가 깨졌다 — 만충이 아니다");

            Assert.AreEqual(50f, s.Absorb(150f), D, "넘친 50 이 HP 로 안 갔다");
            Assert.AreEqual(0f, s.Value, D, "다 먹었는데 게이지가 남았다");
            Assert.AreEqual(100f, s.Absorbed, D, "막은 양이 안 세졌다");
        }

        [Test]
        public void 만충이면_더_안_찬다()
        {
            // 부스터와 같은 문법 — **가득이면 재료를 안 먹어 공급이 정체된다**.
            var s = new ShieldSystem { Max = 10f, ChargeRate = 5f };
            s.Tick(10f);

            Assert.AreEqual(10f, s.Value, D, "최대치를 넘겼다");
            Assert.IsTrue(s.IsFull);
        }

        [Test]
        public void 최대치를_줄이면_넘치는_분은_잘린다()
        {
            // 노드를 뽑았는데 게이지가 그대로면 보드가 결과를 못 바꾼다.
            var s = new ShieldSystem { Max = 100f, ChargeRate = 1000f };
            s.Tick(1f);

            s.Max = 30f;
            Assert.AreEqual(30f, s.Value, D, "그릇을 줄였는데 담긴 것이 그대로다");
        }

        [Test]
        public void 되돌리면_빈_채로_시작한다()
        {
            var s = new ShieldSystem { Max = 100f, ChargeRate = 1000f };
            s.Tick(1f);
            s.Absorb(40f);

            s.Reset();
            Assert.AreEqual(0f, s.Value, D, "재시작인데 게이지가 남았다");
            Assert.AreEqual(0f, s.Absorbed, D, "막은 양이 이전 판에서 흘러왔다");
            Assert.AreEqual(100f, s.Max, D, "최대치까지 지워졌다 — 그것은 자산의 것이다");
        }

        // ── 충전률은 보드에서 온다 ───────────────────────────────────────────

        [Test]
        public void 발생_노드가_없으면_재료를_아무리_만들어도_0이다()
        {
            // ⚠️⚠️ 이 고리가 없으면 쉴드는 보드와 무관한 **공짜 HP** 다.
            Assert.AreEqual(0f, ShieldSystem.ChargeFrom(1000f, 0, 1f, 5f), D);
        }

        [Test]
        public void 충전률은_먹은_재료_곱하기_개당_충전량이다()
        {
            // 노드 하나가 1초에 1개를 먹고 개당 5 를 채우면 5/초.
            Assert.AreEqual(5f, ShieldSystem.ChargeFrom(10f, 1, 1f, 5f), D,
                "재료가 남아도 먹는 입만큼만 먹는다");

            // 재료가 모자라면 **모자란 쪽**이 정한다 — 그것이 벨트가 생존에 닿는 자리다.
            Assert.AreEqual(1.5f, ShieldSystem.ChargeFrom(0.3f, 2, 1f, 5f), D,
                "재료가 모자란데 노드 수대로 찼다");
        }

        // ── 자산이 원천 ──────────────────────────────────────────────────────

        [Test]
        public void 최대치는_노드_수_곱하기_계수다()
        {
            // ✅ **2026-09-17 사용자 확정** — 회피의 「부스터 대수 × 4」와 **같은 규칙**이다.
            //    🗑️ 구 규칙 「최대치는 로봇의 것 · 노드는 속도만」 폐기(`260917_V04` 구현 판단 4).
            Assert.AreEqual(200f, ShieldSystem.MaxFrom(1, 200f), D, "노드 하나면 그릇 200");
            Assert.AreEqual(600f, ShieldSystem.MaxFrom(3, 200f), D, "노드 셋이면 세 배");
        }

        [Test]
        public void 발생_노드가_없으면_그릇도_없다()
        {
            // ⚠️⚠️ **여기에 배포 거동이 걸려 있다.** 시작 보드에는 쉴드 줄이 없으므로
            //    노드가 0 이고, 그러면 최대치도 0 이라 **쉴드가 없는 것과 같다.**
            //    이 줄이 무너지면 값이 정해지기도 전에 배포가 바뀐다.
            Assert.AreEqual(0f, ShieldSystem.MaxFrom(0, 200f), D);
            Assert.AreEqual(0f, ShieldSystem.MaxFrom(-1, 200f), D);
        }

        [Test]
        public void 계수는_자산에서_온다()
        {
            // ⚠️ **하드코딩 금지** — 원천은 `balance_v4.json` 이고 `BalanceConfig` 가 미러한다.
            var bal = AssetDatabase.LoadAssetAtPath<BalanceConfig>(
                "Assets/_Project/ScriptableObjects/BalanceConfig.asset");
            Assert.IsNotNull(bal, "BalanceConfig 자산이 없다 — 생성기를 먼저 돌린다");

            Assert.AreEqual(200f, bal.shieldMaxPerNode, D,
                "params shieldMaxPerNode = 200 (2026-09-17 사용자 확정)");
            Assert.AreEqual(bal.shieldMaxPerNode, new BalanceConfig().shieldMaxPerNode, D,
                "코드 기본값이 자산과 갈렸다 — 자산을 못 읽는 판이 다른 그릇을 세운다");

            // 🗑️ 구 칸은 **0 으로 남아 있어야 한다** — 읽는 곳이 남아 있어도 옛 규칙이 안 살아난다.
            Assert.AreEqual(0f, bal.shieldMax, D, "폐기한 로봇 고정 그릇에 값이 들어왔다");

            Assert.AreEqual(1f, bal.shieldMaterialPerSec, D,
                "대당 재료 소비는 측정용 고정값 1 이다");
        }

        // ── 전투에 실제로 걸리는가 ───────────────────────────────────────────

        private static RobotSetup Robot(float hp) => new RobotSetup
        {
            hp = hp, mountCoef = 1f, moduleMult = 1f,
            attackRange = 0f, radius = 0f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = new List<AmmoLine>(),          // 안 쏜다 — 맞는 쪽만 본다
            ammoCapacity = 0f, ammoStore = AmmoFixture.Pierce(0f, 0f),
            droneSlots = 0, droneReleaseRate = 0f, droneCharge = 0f, droneAttackRange = 0f,
        };

        /// <summary>원거리에서 1초에 한 번 때리는 적 하나.</summary>
        private static List<EnemySpawn> Gunner(float atk) => new List<EnemySpawn>
        {
            new EnemySpawn { label = "포격", hp = 10000000f, def = 0f, atk = atk,
                moveSpeed = 0f, attackRange = 100f, attackInterval = 1f },
        };

        private static void Run(CombatSimulation sim, float seconds, float dt = 0.1f)
        {
            int steps = Mathf.CeilToInt(seconds / dt);
            for (int i = 0; i < steps && sim.Result == CombatResult.InProgress; i++) sim.Tick(dt);
        }

        [Test]
        public void 쉴드가_있으면_HP_대신_쉴드가_준다()
        {
            var sim = new CombatSimulation(Robot(1000f), Gunner(30f),
                arenaRadius: 6f, challengeTime: 1000f, spawnCadence: 0f)
            { ShieldMax = 500f, ShieldChargeRate = 0f };
            sim.Shield.Add(500f);

            Run(sim, 5f);

            Assert.Greater(sim.HitsTaken, 0, "시험 전제가 깨졌다 — 한 대도 안 맞았다");
            Assert.Greater(sim.ShieldAbsorbed, 0f, "맞았는데 쉴드가 아무것도 안 막았다");
            Assert.AreEqual(1000f, sim.Robot.hp, D,
                "쉴드가 남아 있는데 HP 가 줄었다 — 차례가 회피 → 쉴드 → HP 가 아니다");
            Assert.AreEqual(0f, sim.DamageTaken, D, "쉴드가 먹은 몫이 HP 피해로도 세졌다");
        }

        [Test]
        public void 쉴드가_바닥나면_그_뒤부터_HP_가_준다()
        {
            var sim = new CombatSimulation(Robot(1000f), Gunner(30f),
                arenaRadius: 6f, challengeTime: 1000f, spawnCadence: 0f)
            { ShieldMax = 50f, ShieldChargeRate = 0f };
            sim.Shield.Add(50f);

            Run(sim, 10f);

            Assert.AreEqual(50f, sim.ShieldAbsorbed, D, "쉴드가 제 그릇보다 더 막았다");
            Assert.Greater(sim.DamageTaken, 0f, "쉴드가 바닥났는데 HP 가 그대로다");
            Assert.AreEqual(1000f - sim.DamageTaken, sim.Robot.hp, D);
        }

        // ── 합체 중에는 두 보드 게이지를 합산해 쓴다 (`260917_W06` 5장) ──────

        private static Dictionary<MountItem, float> Stacks() =>
            new Dictionary<MountItem, float>
            {
                { MountItem.Pierce, 100f }, { MountItem.Standard, 100f },
                { MountItem.Explosive, 100f }, { MountItem.Drone, 100f },
            };

        /// <summary>
        /// 합체 게이지는 **싸우는 중에만** 찬다 — 쏘지 않는 로봇으로는 안 걸린다.
        /// 그래서 이쪽만 관통 한 줄을 들려 보낸다(적은 샌드백이라 안 죽는다).
        /// </summary>
        private static RobotSetup Fighter() => new RobotSetup
        {
            // ⚠️ HP 를 크게 잡는 까닭 — 게이지가 차는 데 90초가 걸린다(`MergeSystem.GaugeFullSeconds`).
            //    1000 으로 두면 **채우는 동안 죽어서** 합체가 영영 안 걸린다(실제로 걸렸다).
            hp = 1000000f, mountCoef = 1f, moduleMult = 1f,
            attackRange = 100f, radius = 0f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = new List<AmmoLine> { new AmmoLine(AmmoKind.Pierce, 20f, 1f) },
            ammoCapacity = 100000f, ammoStore = AmmoFixture.Pierce(100000f, 100000f),
            droneSlots = 0, droneReleaseRate = 0f, droneCharge = 0f, droneAttackRange = 0f,
        };

        private static CombatSimulation TwoRobots(float atk) =>
            new CombatSimulation(Fighter(), Fighter(),
                new MountLoad(4, Stacks()), new MountLoad(4, Stacks()),
                Gunner(atk), arenaRadius: 6f, challengeTime: 1000f, spawnCadence: 0f);

        [Test]
        public void 합체_중에는_대기_보드의_쉴드도_쓴다()
        {
            // 설계가 낸 그림 그대로 — A 쉴드 0 · B 쉴드 50 · 피해 30
            // → **B 에서 빠지고 HP 는 안 준다.**
            CombatSimulation sim = TwoRobots(atk: 30f);
            // ⚠️ 마운트가 만재라 **자동 교대가 끼어든다** — 잠가서 진영이 안 바뀌게 한다.
            sim.Tag.Locked = true;
            Run(sim, MergeSystem.GaugeFullSeconds + 1f, dt: 0.5f);
            sim.Tag.Locked = false;
            Assert.IsTrue(sim.TryMerge(), "시험 전제가 깨졌다 — 합체가 안 걸렸다");

            // ⚠️ 최대치는 이제 **판마다 다르다**(노드 수 × 대당) — 양쪽에 따로 건다.
            sim.ShieldMax = 50f;
            sim.StandbyShieldMax = 50f;
            sim.ShieldChargeRate = 0f;
            sim.StandbyShieldChargeRate = 0f;
            sim.Shield.Reset();                  // 싸우는 쪽 0
            sim.StandbyShield.Reset();
            sim.StandbyShield.Add(50f);          // 대기 쪽 50

            float hp = sim.Robot.hp;
            float taken = sim.DamageTaken;
            int hits = sim.HitsTaken;

            Run(sim, 1.5f);

            Assert.Greater(sim.HitsTaken, hits, "합체 뒤 한 대도 안 맞았다");
            Assert.Less(sim.StandbyShield.Value, 50f,
                "싸우는 쪽 게이지가 0 인데 대기 쪽이 안 줄었다 — 합산이 안 걸렸다");
            Assert.AreEqual(hp, sim.Robot.hp, D, "대기 쉴드가 남았는데 HP 가 줄었다");
            Assert.AreEqual(taken, sim.DamageTaken, D);
        }

        [Test]
        public void 합체가_아니면_대기_보드의_쉴드를_안_쓴다()
        {
            // 짝 시험 — 위의 0 이 「합체 덕」임을 못 박는다. 평상시엔 제 게이지만 쓴다.
            CombatSimulation sim = TwoRobots(atk: 30f);
            sim.Tag.Locked = true;   // 교대가 끼면 대기 쪽이 싸우는 쪽이 되어 전제가 깨진다
            sim.ShieldMax = 50f;
            sim.StandbyShieldMax = 50f;
            sim.ShieldChargeRate = 0f;
            sim.StandbyShieldChargeRate = 0f;
            sim.StandbyShield.Add(50f);

            Run(sim, 3f);

            Assert.AreEqual(50f, sim.StandbyShield.Value, D,
                "합체도 안 했는데 대기 쪽 게이지가 줄었다");
            Assert.Greater(sim.DamageTaken, 0f, "제 쉴드가 0 인데 HP 가 안 줄었다");
        }

        [Test]
        public void 쉴드는_시간으로_찬다()
        {
            // ⚠️ 프레임이 아니라 시간 — 30fps 기기에서 두 배가 되면 안 된다.
            var sim = new CombatSimulation(Robot(1000f), Gunner(0f),
                arenaRadius: 6f, challengeTime: 1000f, spawnCadence: 0f)
            { ShieldMax = 100f, ShieldChargeRate = 10f };

            Run(sim, 3f, dt: 0.1f);
            float fine = sim.Shield.Value;

            var coarse = new CombatSimulation(Robot(1000f), Gunner(0f),
                arenaRadius: 6f, challengeTime: 1000f, spawnCadence: 0f)
            { ShieldMax = 100f, ShieldChargeRate = 10f };
            Run(coarse, 3f, dt: 0.5f);

            Assert.AreEqual(fine, coarse.Shield.Value, 0.01f,
                "틱 간격이 달라지자 충전량이 갈렸다 — 프레임으로 세고 있다");
        }
    }
}
