using System.Collections.Generic;
using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **줄을 비워도 승패는 시뮬이 제 문으로 낸다** (2026-10-13 · 개발 도구 「도전 즉시 종료」).
    ///
    /// 📌 **무엇을 지키는가.** 결과 화면을 95 초 안 기다리고 보려고 길을 하나 냈는데,
    /// 그 길이 `Result` 를 **직접 쓰면** 사람이 지나는 문을 안 지난 판을 보게 된다
    /// (09-15 에 겪은 자리 — 프로브는 초록인데 화면은 낡았다).
    ///
    /// 그래서 <see cref="CombatSimulation.DrainSpawnQueue"/> 는 승리 조건
    /// 「전원 스폰 완료 · 생존 0」의 **앞 절반만** 참으로 만든다. 뒤 절반은 적을 실제로
    /// 죽여야 서고, 판정은 늘 시뮬 제 손에서 나온다. **이 시험이 그 경계를 지킨다.**
    /// </summary>
    public sealed class DrainSpawnQueueTests
    {
        private const float Dt = 0.05f;

        private static List<EnemySpawn> Mobs(int n)
        {
            var list = new List<EnemySpawn>(n);
            for (int i = 0; i < n; i++)
                list.Add(new EnemySpawn
                {
                    label = "보병", hp = 30f, def = 0f, atk = 0f,
                    moveSpeed = 0f, attackRange = 1f, attackInterval = 1f,
                    radius = 0f, projectileSpeed = 0f,
                });
            return list;
        }

        /// <summary>적이 때리지 않는 판 — 로봇이 안 죽어야 「승리/진행 중」만 갈린다.</summary>
        private static RobotSetup Robot() => new RobotSetup
        {
            hp = 3000f, mountCoef = 1f, moduleMult = 1f,
            attackRange = 0f, radius = 0f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = new List<AmmoLine>(),
            ammoCapacity = 0f,
            ammoStore = new AmmoInventory(0f),
            mountStackLimit = 0f,
        };

        private static CombatSimulation Sim(int enemies) =>
            new CombatSimulation(Robot(), Mobs(enemies),
                arenaRadius: 6f, challengeTime: 600f, spawnCadence: 0.8f);

        [Test]
        public void 줄만_비우면_아직_안_끝난다()
        {
            // ⚠️ 여기가 핵심이다 — 비우는 것만으로 이기면 **승패를 지어낸** 것이다.
            CombatSimulation sim = Sim(50);
            sim.Tick(Dt);                       // 첫 묶음이 나온다

            int skipped = sim.DrainSpawnQueue();
            Assert.Greater(skipped, 0, "아직 안 나온 적이 있어야 셈이 선다");

            sim.Tick(Dt);
            Assert.AreEqual(CombatResult.InProgress, sim.Result,
                "살아 있는 적이 남았는데 이겼다 — 판정을 건너뛴 것이다");
            Assert.Greater(sim.Remaining, 0);
        }

        [Test]
        public void 비우고_죽여야_이긴다()
        {
            CombatSimulation sim = Sim(50);
            sim.Tick(Dt);

            sim.DrainSpawnQueue();

            // 러너의 KillAllEnemies 가 하는 일 그대로 — 산 것의 hp 를 0 으로.
            foreach (CombatEntity e in sim.Enemies)
                if (e != null) e.hp = 0f;

            sim.Tick(Dt);
            Assert.AreEqual(CombatResult.Win, sim.Result,
                "전원 스폰 완료 · 생존 0 인데 승리가 안 났다");
        }

        [Test]
        public void 안_나온_수를_그대로_돌려준다()
        {
            CombatSimulation sim = Sim(50);
            sim.Tick(Dt);

            int alive = sim.Remaining;
            int skipped = sim.DrainSpawnQueue();

            Assert.AreEqual(50 - alive, skipped,
                "건너뛴 수 = 전체 − 이미 나온 수 라야 한다");
        }

        [Test]
        public void 두_번_비워도_0_이다()
        {
            CombatSimulation sim = Sim(50);
            sim.Tick(Dt);

            Assert.Greater(sim.DrainSpawnQueue(), 0);
            Assert.AreEqual(0, sim.DrainSpawnQueue(), "이미 빈 줄을 또 비울 것은 없다");
        }

        [Test]
        public void 비운_뒤_안_나온_수가_0_이다()
        {
            // ⚠️ **화면이 읽는 수다.** 빌드 실측에서 **이겼는데 「남은 몬스터 96」**이
            //    떴다 — HUD 가 `전체 − 처치 − 생존` 으로 짐작했기 때문이다.
            //    그 식은 「줄에서 빠진 적이 없다」를 전제로 하는데 여기서 깨진다.
            CombatSimulation sim = Sim(50);
            sim.Tick(Dt);

            Assert.Greater(sim.Unspawned, 0, "아직 안 나온 적이 있어야 한다");
            sim.DrainSpawnQueue();
            Assert.AreEqual(0, sim.Unspawned, "줄을 비웠으면 안 나온 수는 0 이다");

            // 구 식이었다면 여기서 0 이 아니다 — 그 차이가 화면의 거짓말이었다.
            int oldWay = sim.TotalEnemies - sim.TotalKills - sim.Remaining;
            Assert.Greater(oldWay, 0,
                "구 식이 0 을 준다면 이 시험은 아무것도 안 지킨다");
        }

        [Test]
        public void 이미_나온_적은_안_건드린다()
        {
            CombatSimulation sim = Sim(50);
            sim.Tick(Dt);

            int alive = sim.Remaining;
            sim.DrainSpawnQueue();

            Assert.AreEqual(alive, sim.Remaining, "줄만 비운다 — 서 있는 것은 그대로다");
        }

        [Test]
        public void 비운_뒤에는_더_안_나온다()
        {
            CombatSimulation sim = Sim(50);
            sim.Tick(Dt);

            int alive = sim.Remaining;
            sim.DrainSpawnQueue();

            // 한참 돌려도 새로 나오지 않는다(죽지도 않는 판이라 수가 그대로여야 한다).
            for (int i = 0; i < 200; i++) sim.Tick(Dt);

            Assert.AreEqual(alive, sim.Remaining, "줄을 비웠는데 또 나왔다");
        }
    }
}
