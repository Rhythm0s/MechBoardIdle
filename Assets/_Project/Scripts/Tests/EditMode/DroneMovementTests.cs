using System.Collections.Generic;
using MBI.Core;
using MBI.Core.Combat;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **드론이 사출 뒤 어떻게 움직이는가** (2026-09-16 사용자 확정 · 플랜 §74-21 · §74-3 #30).
    ///
    /// 🗑️ **구 규칙 「정박」 폐기.** 사출되는 순간 로봇 둘레에 놓고 **그 뒤 아무도 위치를
    /// 안 바꿨다** — 로봇이 걸어가면 드론만 뒤에 남았다. 문서에 이동 규칙이 없어
    /// 구현이 고른 가정이었고, 사용자가 화면을 보고 뒤집었다.
    ///
    /// · **광역형** — 로봇 주변 궤도. 로봇이 걸어도 따라온다. 주위 적 전부 친다.
    /// · **누적형** — 한 적에게 날아가 붙어 충전량을 다 쓸 때까지 때린다. 죽으면 다음 적.
    ///
    /// ⚠️ **값 넷은 전부 가정이다**(CombatTuning 의 drone*Tbd · 설계 역기입 자리).
    /// </summary>
    public sealed class DroneMovementTests
    {
        private const float Dt = 1f / 60f;

        private static RobotSetup DroneRobot() => new RobotSetup
        {
            hp = 100000f, mountCoef = 1f, moduleMult = 1f,
            attackRange = 0f, radius = 0.5f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 0f,
            lines = new List<AmmoLine>(),
            ammoCapacity = 40f, ammoStore = new AmmoInventory(40f),
            droneSlots = 3, droneReleaseRate = 10f, droneCharge = 100f,
            // ⚠️ **기당 피해를 충전량보다 작게 둔다** — 같으면 드론이 **붙는 그 틱에**
            //    전량을 쏟고 사라져 이동 규칙이 화면에도 시험에도 안 보인다.
            //    (지금 게임 값은 dB 100 = 충전량 100 이라 한 방이다 — 밸런스 판정 자리.)
            droneDamagePerHit = 5f,
            droneAttackRange = 9.2f,
            mountStackLimit = 10f,
        };

        private static List<EnemySpawn> Enemies(int n, float hp)
        {
            var list = new List<EnemySpawn>(n);
            for (int i = 0; i < n; i++)
                list.Add(new EnemySpawn
                {
                    label = "표적", hp = hp, def = 0f, atk = 0f,
                    moveSpeed = 0f, attackRange = 0f, attackInterval = 1f,
                    radius = 0f, projectileSpeed = 0f,
                });
            return list;
        }

        /// <summary>드론이 뜨는 판 하나. 유입을 크게 줘 바로 사출되게 한다.</summary>
        private static CombatSimulation Sim(int enemies, float enemyHp, float aoeShare)
        {
            var sim = new CombatSimulation(DroneRobot(), Enemies(enemies, enemyHp),
                arenaRadius: 6f, challengeTime: 600f, spawnCadence: 0f);
            sim.DroneInflowRate = 50f;
            sim.AoeDroneShare = aoeShare;
            return sim;
        }

        // ── 종 가르기 ────────────────────────────────────────────────────

        [Test]
        public void 몫이_0이면_전부_누적형이다()
        {
            var picker = new DroneKindPicker();
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(DroneKind.Stack, picker.Next(0f));
        }

        [Test]
        public void 몫이_1이면_전부_광역형이다()
        {
            var picker = new DroneKindPicker();
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(DroneKind.Aoe, picker.Next(1f));
        }

        [Test]
        public void 반반이면_번갈아_나고_난수가_아니다()
        {
            // ⚠️ **결정론이 시뮬의 규약이다** — 주사위를 굴리면 하네스가 재는 값이
            //    매번 달라진다. 같은 몫에서 같은 차례가 나와야 한다.
            var a = new DroneKindPicker();
            var b = new DroneKindPicker();
            int aoe = 0;
            for (int i = 0; i < 20; i++)
            {
                DroneKind x = a.Next(0.5f), y = b.Next(0.5f);
                Assert.AreEqual(x, y, $"{i}번째에서 두 판이 갈렸다 — 결정론이 깨졌다");
                if (x == DroneKind.Aoe) aoe++;
            }
            Assert.AreEqual(10, aoe, "반반인데 절반이 안 나왔다");
        }

        [Test]
        public void 보드가_종을_가른다()
        {
            // 집계가 몫을 낸다 — 전투가 아니라 **보드**가 정한다.
            var agg = new NetworkAggregate { droneStackProduce = 3f, droneAoeProduce = 1f };
            Assert.AreEqual(0.25f, agg.AoeShare, 0.0001f);

            var none = new NetworkAggregate();
            Assert.AreEqual(0f, none.AoeShare, 0.0001f, "안 만들면 누적형 쪽이 기본이다");
        }

        // ── 광역형: 따라 돈다 ────────────────────────────────────────────

        [Test]
        public void 광역형은_로봇이_걸어도_따라온다()
        {
            // **결함 그 자체** — 구 규칙에서는 로봇이 걸어가면 드론만 뒤에 남았다.
            CombatSimulation sim = Sim(enemies: 1, enemyHp: 1e9f, aoeShare: 1f);
            for (int i = 0; i < 300; i++) sim.Tick(Dt);
            Assert.Greater(sim.Drones.Count, 0, "드론이 안 떴다 — 시험 전제가 깨졌다");

            sim.Robot.position = new Vector2(20f, -13f);   // 한참 걸어갔다
            for (int i = 0; i < 120; i++) sim.Tick(Dt);

            foreach (DroneUnit d in sim.Drones)
            {
                float dist = (d.Position - sim.Robot.position).magnitude;
                Assert.LessOrEqual(dist, sim.DroneOrbitRadius + 0.01f,
                    "광역형이 로봇을 안 따라왔다 — 뒤에 남았다");
            }
        }

        [Test]
        public void 광역형은_궤도_반경_위에_선다()
        {
            CombatSimulation sim = Sim(enemies: 1, enemyHp: 1e9f, aoeShare: 1f);
            for (int i = 0; i < 300; i++) sim.Tick(Dt);

            foreach (DroneUnit d in sim.Drones)
                Assert.AreEqual(sim.DroneOrbitRadius,
                    (d.Position - sim.Robot.position).magnitude, 0.01f);
        }

        [Test]
        public void 광역형은_돈다()
        {
            CombatSimulation sim = Sim(enemies: 1, enemyHp: 1e9f, aoeShare: 1f);
            for (int i = 0; i < 120; i++) sim.Tick(Dt);
            Assert.Greater(sim.Drones.Count, 0);

            // ⚠️ **차례가 아니라 그 기체를 본다.** 드론은 충전량을 다 쓰면 사라지고
            //    같은 자리에 새 기체가 난다 — 목록 0번을 두 시점에 비교하면 **다른 기체**를
            //    견주게 되고, 궤도 각이 차례로 정해지므로 **자리가 같게 나올 수도 있다.**
            DroneUnit watched = sim.Drones[0];
            float angleBefore = watched.OrbitAngle;
            Vector2 before = watched.Position;

            for (int i = 0; i < 10; i++) sim.Tick(Dt);

            Assert.Greater(watched.OrbitAngle, angleBefore, "각이 안 늘었다");
            Assert.Greater((watched.Position - before).magnitude, 0.01f,
                "각속도가 있는데 안 움직였다");
        }

        // ── 누적형: 날아가 붙는다 ────────────────────────────────────────

        [Test]
        public void 누적형은_표적에_붙는다()
        {
            CombatSimulation sim = Sim(enemies: 1, enemyHp: 1e9f, aoeShare: 0f);
            for (int i = 0; i < 300; i++) sim.Tick(Dt);
            Assert.Greater(sim.Drones.Count, 0);

            CombatEntity target = null;
            foreach (CombatEntity e in sim.Enemies) { target = e; break; }
            Assert.IsNotNull(target);

            // ⚠️ **전부는 아니다** — 유입이 계속 있어 **방금 사출된 기체**는 아직 나는 중이다.
            //    「하나도 못 붙었다」가 결함이고, 「전부 붙었다」는 시험이 판을 잘못 세운 것이다.
            int attached = 0;
            foreach (DroneUnit d in sim.Drones)
            {
                Assert.AreEqual(DroneKind.Stack, d.Kind);
                if (!d.Attached) continue;
                attached++;
                Assert.AreEqual(0f, (d.Position - target.position).magnitude, 0.01f,
                    "붙었다는데 자리가 다르다");
            }
            Assert.Greater(attached, 0, "날아가기만 하고 한 기도 안 붙었다");
        }

        [Test]
        public void 붙은_적이_죽으면_다음_적으로_옮긴다()
        {
            // ⚠️ **가정**이다(설계 역기입 자리) — 문서에 「죽으면 어떻게 하는가」가 없다.
            //    남은 충전량을 버리는 것이 더 나쁘다고 보고 옮기는 쪽을 골랐다.
            CombatSimulation sim = Sim(enemies: 2, enemyHp: 30f, aoeShare: 0f);

            bool killed = false;
            for (int i = 0; i < 2400 && !killed; i++)
            {
                sim.Tick(Dt);
                if (sim.TotalKills > 0) killed = true;
            }
            Assert.IsTrue(killed, "한 기도 못 죽였다 — 시험 전제가 깨졌다");

            // ⚠️ **한 틱 뒤에 본다.** 죽인 그 틱에는 아직 붙어 있는 것이 맞다 —
            //    옮기는 일은 다음 틱의 이동 단계가 한다. 그 한 틱을 결함으로 세면
            //    시험이 없는 병을 잡는다.
            for (int i = 0; i < 5; i++) sim.Tick(Dt);

            foreach (DroneUnit d in sim.Drones)
            {
                var t = d.Target as CombatEntity;
                if (t == null || !d.Attached) continue;
                // 죽은 것을 붙들고 있으면 남은 충전량이 통째로 버려진다.
                Assert.IsTrue(t.IsAlive, "죽은 적에 붙어 있다 — 다음 적으로 안 옮겼다");
            }
        }

        [Test]
        public void 누적형은_붙은_적만_친다()
        {
            // 최근접을 다시 고르면 「붙어서 다 쓴다」가 성립하지 않는다 —
            // 옆을 지나가는 적이 있으면 표적이 갈아탄다.
            CombatSimulation sim = Sim(enemies: 3, enemyHp: 1e9f, aoeShare: 0f);
            for (int i = 0; i < 300; i++) sim.Tick(Dt);

            foreach (DroneUnit d in sim.Drones)
                if (d.Attached)
                    Assert.IsNotNull(d.Target as CombatEntity, "붙었는데 표적이 없다");
        }
    }
}
