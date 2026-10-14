using System.Collections.Generic;
using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **적이 한 기도 없는 판은 「전멸」로 이길 수 없다** (2026-10-14 · 사용자 육안).
    ///
    /// 실제로 뜬 그림 — 메뉴에서 「게임 시작」을 누르자마자 **「승리 — 적 전멸」 결과 화면**이
    /// 떴다. 튜토리얼 판은 몬스터 구성이 비어 있어 승리 조건
    /// 「전원 스폰 완료 · 생존 0」이 **첫 틱에 참**이 된다(`0 >= 0 && 0 == 0`).
    ///
    /// ⚠️⚠️ **실행 순서로는 못 막는다.** 2026-09-01 에 `Stage0Session` 을
    /// `[DefaultExecutionOrder(-50)]` 으로 러너보다 앞세워 `Endless` 를 걸게 했는데,
    /// **시뮬이 러너의 `Update` 안에서 태어난다.** 메뉴가 떠 있는 동안 러너는 `Begin()` 을
    /// 미루므로 `runner.Sim` 이 null 이고 억제가 안 걸린다. 「게임 시작」을 누른 그 프레임에
    /// 시뮬이 태어나 **같은 프레임에 판정까지 난다** — 앞서 돌아도 **없는 것에는 못 건다.**
    ///
    /// 📌 그래서 판정 자리에서 막는다. 싸울 것이 없는 판은 **이기지도 지지도 않는다.**
    /// </summary>
    public sealed class EmptyRosterNoWinTests
    {
        private const float Dt = 0.05f;

        /// <summary>적이 때리지 않는 판 — 로봇 파괴만 가려 보려고.</summary>
        private static RobotSetup Robot(float hp = 3000f) => new RobotSetup
        {
            hp = hp, mountCoef = 1f, moduleMult = 1f,
            attackRange = 0f, radius = 0f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = new List<AmmoLine>(),
            ammoCapacity = 0f,
            ammoStore = new AmmoInventory(0f),
            mountStackLimit = 0f,
        };

        /// <summary>튜토리얼 판과 같은 꼴 — 구성이 **비어 있다**.</summary>
        private static CombatSimulation Tutorial(float challengeTime = 9999f) =>
            new CombatSimulation(Robot(), new List<EnemySpawn>(),
                arenaRadius: 6f, challengeTime: challengeTime, spawnCadence: 0.8f);

        [Test]
        public void 첫_틱에_이기지_않는다()
        {
            // ⚠️ **이 한 줄이 사용자가 본 그림이다** — 억제를 안 걸고 한 틱만 돌린다.
            CombatSimulation sim = Tutorial();
            Assert.IsFalse(sim.Endless, "억제 없이 재는 것이 이 시험의 요점이다");

            sim.Tick(Dt);

            Assert.AreEqual(CombatResult.InProgress, sim.Result,
                "적이 0 기인 판에서 「전멸」 승리가 났다 — 없는 적을 다 잡은 셈이다");
        }

        [Test]
        public void 한참_돌려도_안_끝난다()
        {
            CombatSimulation sim = Tutorial();
            for (int i = 0; i < 400; i++) sim.Tick(Dt);

            Assert.AreEqual(CombatResult.InProgress, sim.Result);
        }

        [Test]
        public void 제한시간이_지나도_안_진다()
        {
            // 싸울 것이 없으면 **지지도 않는다** — 시간으로 지면 「못 잡아서 졌다」가 되는데
            // 잡을 것이 애초에 없다.
            CombatSimulation sim = Tutorial(challengeTime: 1f);
            for (int i = 0; i < 100; i++) sim.Tick(Dt);   // 5 초 — 제한 1 초를 한참 넘겼다

            Assert.AreEqual(CombatResult.InProgress, sim.Result,
                "적이 0 기인 판이 제한시간으로 졌다");
        }

        [Test]
        public void 로봇이_부서지면_그것은_진다()
        {
            // ⚠️ **전부를 막는 것이 아니다.** 튜토리얼에도 로봇은 있고, 부서지는 일은
            //    여전히 판정된다(막아 버리면 「아무 일도 안 일어나는 판」이 된다).
            CombatSimulation sim = Tutorial();
            sim.Tick(Dt);
            Assert.AreEqual(CombatResult.InProgress, sim.Result);

            sim.Robot.hp = 0f;
            sim.Tick(Dt);

            Assert.AreEqual(CombatResult.LoseDead, sim.Result,
                "적이 없어도 로봇 파괴는 판정된다");
        }

        [Test]
        public void 적이_있는_판은_종전대로_이긴다()
        {
            // 📌 이 시험이 없으면 위 넷은 **승리 자체를 없애도** 초록이다.
            var spawns = new List<EnemySpawn>
            {
                new EnemySpawn
                {
                    label = "보병", hp = 30f, def = 0f, atk = 0f,
                    moveSpeed = 0f, attackRange = 1f, attackInterval = 1f,
                    radius = 0f, projectileSpeed = 0f,
                },
            };
            var sim = new CombatSimulation(Robot(), spawns,
                arenaRadius: 6f, challengeTime: 600f, spawnCadence: 0.8f);

            sim.Tick(Dt);
            Assert.AreEqual(1, sim.Remaining, "한 기가 나와야 한다");

            foreach (CombatEntity e in sim.Enemies) if (e != null) e.hp = 0f;
            sim.Tick(Dt);

            Assert.AreEqual(CombatResult.Win, sim.Result, "적이 있는 판은 종전대로 이긴다");
        }
    }
}
