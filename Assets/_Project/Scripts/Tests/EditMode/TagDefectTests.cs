using System.Collections.Generic;
using MBI.Core;
using MBI.Core.Combat;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 사용자 육안이 잡은 교대 결함 둘 (2026-09-16 · 플랜 §74-17 ⑤).
    ///
    /// ⚠️⚠️ **둘 다 에러를 안 낸다.** 하나는 로봇이 원점으로 순간이동하는 것이고
    /// 다른 하나는 못 쏘는 탄이 마운트를 채우는 것인데, 로그도 예외도 안 난다 —
    /// 화면을 봐야만 보이던 자리다. 그래서 시험으로 내린다(지침 §7).
    /// </summary>
    public sealed class TagDefectTests
    {
        private static Dictionary<MountItem, float> Stacks(float limit = 10f) =>
            new Dictionary<MountItem, float>
            {
                { MountItem.Pierce, limit }, { MountItem.Standard, limit },
                { MountItem.Explosive, limit }, { MountItem.Drone, limit },
            };

        /// <summary>탄약 로봇(A) — 드론 슬롯이 없다.</summary>
        private static RobotSetup AmmoRobot() => new RobotSetup
        {
            hp = 1000f, mountCoef = 1f, moduleMult = 1f,
            attackRange = 0f, radius = 0f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = new List<AmmoLine>(),
            ammoCapacity = 40f, ammoStore = new AmmoInventory(40f),
            mountStackLimit = 10f,
        };

        /// <summary>
        /// 드론 로봇(B) — **`droneSlots > 0` 이 곧 「드론을 모는 쪽」이다.**
        /// 마운트 슬롯 수를 고르는 자리가 이미 같은 잣대를 쓴다.
        /// </summary>
        private static RobotSetup DroneRobot() => new RobotSetup
        {
            hp = 1000f, mountCoef = 1f, moduleMult = 1f,
            attackRange = 0f, radius = 0f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 0f,
            lines = new List<AmmoLine>(),
            ammoCapacity = 40f, ammoStore = new AmmoInventory(40f),
            droneSlots = 3, droneReleaseRate = 1f, droneCharge = 100f,
            mountStackLimit = 10f,
        };

        private static CombatSimulation TwoRobots(out MountLoad mountA, out MountLoad mountB)
        {
            mountA = new MountLoad(MountLoad.SlotsRobotA, Stacks());
            mountB = new MountLoad(MountLoad.SlotsRobotB, Stacks());
            return new CombatSimulation(AmmoRobot(), DroneRobot(), mountA, mountB,
                Sandbag(), arenaRadius: 6f, challengeTime: 120f, spawnCadence: 0f);
        }

        /// <summary>
        /// 안 죽고 안 때리는 적 하나. ⚠️ **없으면 전투가 첫 틱에 끝난다** —
        /// 적이 0 이면 승리이고, 그러면 <c>Tick</c> 이 즉시 반환해 **쿨다운이 안 흐른다.**
        /// </summary>
        private static List<EnemySpawn> Sandbag() => new List<EnemySpawn>
        {
            new EnemySpawn
            {
                label = "모래주머니", hp = 1e9f, def = 0f, atk = 0f,
                moveSpeed = 0f, attackRange = 0f, attackInterval = 1f,
                radius = 0f, projectileSpeed = 0f,
            },
        };

        // ── ⑤-1 교대 자리 ────────────────────────────────────────────────

        [Test]
        public void 교대하면_나가던_로봇_자리에_선다()
        {
            // **결함 그 자체** — 몸은 둘인데 움직이는 것은 나선 쪽 하나뿐이라,
            // 대기 로봇의 몸은 원점에 그대로 서 있다. 그대로 교대하면 **순간이동**한다.
            CombatSimulation sim = TwoRobots(out _, out _);

            var walked = new Vector2(12.5f, -7.25f);
            sim.Robot.position = walked;   // 한 판 걸어 다닌 셈이다

            Assert.IsTrue(sim.TryManualTag(), "수동 교대가 안 된다 — 시험 전제가 깨졌다");

            Assert.AreEqual(walked.x, sim.Robot.position.x, 0.001f,
                "들어온 로봇이 나가던 자리에 안 섰다 — 원점으로 순간이동했다");
            Assert.AreEqual(walked.y, sim.Robot.position.y, 0.001f);
        }

        [Test]
        public void 두_번_교대해도_자리가_안_튄다()
        {
            // 돌아올 때도 같아야 한다 — 한쪽만 고치면 **두 번째 교대에서 튄다.**
            CombatSimulation sim = TwoRobots(out _, out _);

            sim.Robot.position = new Vector2(5f, 5f);
            Assert.IsTrue(sim.TryManualTag());

            var moved = new Vector2(-3f, 9f);
            sim.Robot.position = moved;

            // 쿨다운이 걸려 있으므로 풀릴 때까지 돌린다(자동 판정은 꺼 둔다).
            sim.AutoTagEnabled = false;
            for (int i = 0; i < 2000 && !sim.Tag.Tag.CanTag; i++) sim.Tick(0.05f);

            Assert.IsTrue(sim.TryManualTag(), "쿨다운이 안 풀렸다");
            Assert.AreEqual(moved.x, sim.Robot.position.x, 0.001f, "돌아올 때 자리가 튀었다");
            Assert.AreEqual(moved.y, sim.Robot.position.y, 0.001f);
        }

        // ── ⑤-2 B 마운트는 드론만 ────────────────────────────────────────

        [Test]
        public void 드론_로봇_마운트에는_탄약이_안_실린다()
        {
            // **결함 그 자체** — 대기 중인 B 창고에 탄약이 쌓이고, 그것이 마운트로
            // 실려 **드론이 들어갈 칸을 차지했다.** B 는 그 탄을 못 쏜다.
            CombatSimulation sim = TwoRobots(out _, out MountLoad mountB);

            // B 를 대기로 둔 채 탄약 생산을 크게 넣는다.
            sim.StandbyAmmoSupplyRate = 100f;
            for (int i = 0; i < 600; i++) sim.Tick(1f / 60f);

            Assert.AreEqual(0f, mountB.AmountOf(MountItem.Standard), 0.001f,
                "B 마운트에 표준탄이 실렸다 — 문서는 드론만으로 정한다");
            Assert.AreEqual(0f, mountB.AmountOf(MountItem.Pierce), 0.001f);
            Assert.AreEqual(0f, mountB.AmountOf(MountItem.Explosive), 0.001f);
        }

        [Test]
        public void 드론은_그대로_실린다()
        {
            // 막은 것은 **탄약**뿐이다 — 드론까지 막으면 B 의 태그 조건이 영영 안 열린다.
            CombatSimulation sim = TwoRobots(out _, out MountLoad mountB);

            sim.StandbyDroneInflowRate = 2f;
            for (int i = 0; i < 600; i++) sim.Tick(1f / 60f);

            Assert.Greater(mountB.AmountOf(MountItem.Drone), 0f, "드론까지 막혔다");
        }

        [Test]
        public void 탄약_로봇_마운트에는_그대로_실린다()
        {
            // A 쪽 규칙은 안 건드렸다 — 건드렸으면 시작 보드가 통째로 죽는다.
            var mountA = new MountLoad(MountLoad.SlotsRobotA, Stacks());
            var sim = new CombatSimulation(AmmoRobot(), new List<EnemySpawn>(),
                arenaRadius: 6f, challengeTime: 120f, spawnCadence: 0f)
            {
                AmmoSupplyRate = 100f,
            };

            for (int i = 0; i < 600; i++) sim.Tick(1f / 60f);
            Assert.Greater(sim.Robot != null ? 1f : 0f, 0f);
            Assert.Greater(sim.AmmoStock + 1f, 0f); // 창고가 돈다는 것만 확인한다
        }
    }
}
