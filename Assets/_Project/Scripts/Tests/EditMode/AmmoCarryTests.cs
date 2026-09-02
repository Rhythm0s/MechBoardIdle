using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 전투 시작 재고 — **스테이지 전환은 재고에 손대지 않는다**(260902_W08 §1 확정).
    ///
    /// 종전에는 <c>RobotSetup.ammoInitialStock</c>이 전투가 열릴 때마다 창고를 만재로 채웠다.
    /// 원천에 없는 값을 러너가 고른 것이었고, 그 탓에 생산이 0인 스테이지 0에서도 마운트가
    /// 놓기 전에 이미 차 있어 「이어지면 쌓인다」가 한 번도 안 보였다.
    ///
    /// 지금은 창고를 러너가 들고 시뮬이 빌려 쓴다. 새 저장의 자연 상태가 0이고, 그 뒤는
    /// 물류가 잇는다 — **공장은 스테이지를 넘어도 계속 돌고 있다**(지침 §3).
    /// </summary>
    public sealed class AmmoCarryTests
    {
        private const float D = 0.01f;
        private const float Capacity = 40f;   // balance store 확정치
        private const float MountStack = 10f; // 260901_V03 확정. A 4슬롯 × 10 = 적재량 40

        /// <summary>관통 한 줄. 스테이지 0의 시작 보드가 내는 것과 같은 구성이다.</summary>
        private static List<AmmoLine> PierceLine() =>
            new List<AmmoLine> { new AmmoLine(AmmoKind.Pierce, 20f, 1f) };

        private static RobotSetup Robot(AmmoInventory store) => new RobotSetup
        {
            hp = 100000f, mountCoef = 1f, moduleMult = 1f,
            attackRange = 100f, radius = 0f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = PierceLine(),
            ammoCapacity = Capacity,
            ammoStore = store,
            mountStackLimit = MountStack,
        };

        /// <summary>
        /// 때릴 것이 없어야 재고가 소비되지 않는다 — 채워지는 쪽만 본다.
        ///
        /// ⚠️ <c>Endless</c>를 켜지 않으면 **적이 0기라 첫 틱에 승리 판정이 나고 시뮬이 멈춘다.**
        /// 그러면 생산이 한 틱만 돌아 4초를 재도 0.5발이 나온다(2026-09-02 실측).
        /// 스테이지 0이 전투 없이 도는 방식과 같다.
        /// </summary>
        private static CombatSimulation Sim(AmmoInventory store, MountLoad mount = null)
        {
            RobotSetup r = Robot(store);
            r.mount = mount;
            var sim = new CombatSimulation(r, new List<EnemySpawn>(),
                arenaRadius: 6f, challengeTime: 100000f, spawnCadence: 0f);
            sim.Endless = true;
            return sim;
        }

        // ---- 만들어 주지 않는다 ----

        /// <summary>
        /// **창고를 안 주면 빈 창고다.** 이것이 새 저장의 자연 상태이고, 스테이지 0이
        /// 0에서 시작하는 근거다 — 튜토리얼만의 예외 규칙이 아니다.
        /// </summary>
        [Test]
        public void NoStore_StartsEmpty()
        {
            CombatSimulation sim = Sim(null);

            Assert.AreEqual(0f, sim.ActiveMount.Total, D, "마운트가 비어 있다");
        }

        /// <summary>
        /// **전투가 열려도 재고가 생기지 않는다.** 종전에는 여기서 40이 나왔다 —
        /// 생산이 0인데 마운트가 만충이 되던 것이 그 때문이었다.
        /// </summary>
        [Test]
        public void OpeningCombat_DoesNotManufactureStock()
        {
            CombatSimulation sim = Sim(new AmmoInventory(Capacity));

            for (int i = 0; i < 200; i++) sim.Tick(0.1f); // 20초를 돌려도

            Assert.AreEqual(0f, sim.ActiveMount.Total, D, "생산이 0이면 마운트도 0이다");
        }

        // ---- 이어진다 ----

        /// <summary>
        /// **같은 창고를 넘기면 재고가 이어진다.** 스테이지 전환 = 시뮬을 새로 만드는 일인데,
        /// 창고를 러너가 들고 있으므로 그 경계를 넘어 살아남는다.
        /// </summary>
        [Test]
        public void SameStore_CarriesAcrossSimulations()
        {
            var store = new AmmoInventory(Capacity);
            var mount = new MountLoad(MountLoad.SlotsRobotA, MountLoad.StandardStacks(MountStack));

            CombatSimulation first = Sim(store, mount);
            first.AmmoSupplyRate = 5f;
            for (int i = 0; i < 40; i++) first.Tick(0.1f); // 4초 → 20발

            float carried = first.ActiveMount.Total;
            Assert.AreEqual(20f, carried, D, "4초 × 5발/초");

            // 다음 스테이지 — 시뮬은 새것이고 창고·마운트는 그대로다.
            CombatSimulation second = Sim(store, mount);

            Assert.AreEqual(carried, second.ActiveMount.Total, D,
                "전환이 재고에 손대지 않는다");
        }

        /// <summary>
        /// **창고만 이어서는 모자란다.** 이송에 속도 제한이 없어 물건은 곧바로 마운트로
        /// 옮겨 앉고, 그래서 전환 시점의 창고는 비어 있다 — 마운트를 안 이으면
        /// 스테이지를 넘는 순간 재고가 통째로 사라진다(2026-09-02 이 테스트가 잡았다).
        /// </summary>
        [Test]
        public void StoreAlone_DoesNotCarry_BecauseTheGoodsSitInTheMount()
        {
            var store = new AmmoInventory(Capacity);
            CombatSimulation first = Sim(store, new MountLoad(MountLoad.SlotsRobotA,
                MountLoad.StandardStacks(MountStack)));
            first.AmmoSupplyRate = 5f;
            for (int i = 0; i < 40; i++) first.Tick(0.1f);

            Assert.AreEqual(0f, first.AmmoStock, D, "창고는 비어 있다 — 통로일 뿐이다");
            Assert.AreEqual(20f, first.ActiveMount.Total, D, "물건은 마운트에 있다");
        }

        // ---- 스테이지 0의 8초 ----

        /// <summary>
        /// **빈 창고에서 5발/초면 마운트 40이 8초에 찬다** — 촬영 스크립트 A구간의 그 8초다.
        ///
        /// 창고를 0으로 두면 생산분이 마운트로 가기 전에 창고를 먼저 채워 8초가 늘어날 수
        /// 있다는 우려가 있었다(W08 §1). 늘어나지 않는다 — 이송(<c>RefillMount</c>)에는
        /// 속도 제한이 없어 같은 틱에 창고를 거쳐 마운트로 간다. 창고는 병목이 아니라 통로다.
        /// </summary>
        [Test]
        public void EmptyStore_FillsMountInEightSeconds()
        {
            CombatSimulation sim = Sim(new AmmoInventory(Capacity));
            sim.AmmoSupplyRate = 5f; // 관통 5노드 × 1발/초 — 시작 보드가 병합기를 얻은 뒤의 값

            float elapsed = 0f;
            const float dt = 0.05f;
            while (!sim.ActiveMount.IsFull && elapsed < 30f)
            {
                sim.Tick(dt);
                elapsed += dt;
            }

            Assert.IsTrue(sim.ActiveMount.IsFull, "30초 안에 찬다");
            Assert.AreEqual(8f, elapsed, 0.2f, "적재량 40 ÷ 5발/초 = 8초");
        }

        /// <summary>
        /// 종전 방식이었다면 **0초**다. 회귀 방지 — 이 값이 다시 0이 되면 A구간이 못 찍힌다.
        /// </summary>
        [Test]
        public void PrefilledStore_IsFullImmediately()
        {
            AmmoInventory store = AmmoFixture.Pierce(Capacity, Capacity); // 종전의 만재 시작
            CombatSimulation sim = Sim(store);

            sim.Tick(0.01f);

            Assert.IsTrue(sim.ActiveMount.IsFull,
                "만재로 시작하면 첫 틱에 이미 찬다 — 이것이 8초를 삼키던 것이다");
        }
    }
}
