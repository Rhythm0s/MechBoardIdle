using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **마운트가 두 드론을 무엇을 기준으로 채우는가** — 설계가 청한 사실 확인
    /// (2026-09-18 · `260918_W01` 5장).
    ///
    /// 왜 묻나 — 「누적형과 광역형의 비율은 **생산 속도가 정한다**」가 사용자 확정인데,
    /// 그것이 성립하려면 **마운트가 유입 비율대로 차야** 한다. 지침 §7 ［09-03］에
    /// 같은 자리가 이미 있다: `MountLoad` 주석은 「먼저 도착한 것이 슬롯을 차지한다」인데
    /// 호출자는 **탄종 열거 순서**로 채우고 있었다.
    ///
    /// 📌 **주석이 아니라 호출자를 본다.** 드론을 싣는 호출자는
    /// `CombatSimulation.LoadDronesInto` 하나이고, 그것을 실제로 돌려서 잰다.
    ///
    /// ⚠️ 여기서 값을 고치지 않는다 — **사실만** 적는다.
    /// </summary>
    public sealed class MountDroneMixTests
    {
        private static Dictionary<MountItem, float> Stacks(float ammo, float drone) =>
            new Dictionary<MountItem, float>
            {
                { MountItem.Pierce, ammo }, { MountItem.Standard, ammo },
                { MountItem.Explosive, ammo },
                { MountItem.Drone, drone }, { MountItem.DroneAoe, drone },
            };

        private static RobotSetup Robot() => new RobotSetup
        {
            hp = 100000f, mountCoef = 1f, moduleMult = 1f,
            attackRange = 0f, radius = 0f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = new List<AmmoLine>(),
            ammoCapacity = 40f,
            droneSlots = 0, droneReleaseRate = 0f, droneCharge = 0f, droneAttackRange = 0f,
        };

        private static List<EnemySpawn> Sandbag() => new List<EnemySpawn>
        {
            new EnemySpawn { label = "샌드백", hp = 1000000f, def = 0f, atk = 0f,
                moveSpeed = 0f, attackRange = 0.5f, attackInterval = 1f },
        };

        /// <summary>대기 보드가 두 드론을 각각 이 비율로 흘려보내는 판을 돌린다.</summary>
        private static MountLoad RunStandby(float stackRate, float aoeRate, float seconds,
                                            int slots = 8, float droneStack = 5f)
        {
            var mountA = new MountLoad(MountLoad.SlotsRobotA, Stacks(10f, droneStack));
            var mountB = new MountLoad(slots, Stacks(10f, droneStack));
            var sim = new CombatSimulation(Robot(), Robot(), mountA, mountB,
                Sandbag(), arenaRadius: 6f, challengeTime: 1000f, spawnCadence: 0f)
            {
                // A 는 활성인 채로 둔다 — 교대가 나면 재는 쪽이 바뀐다.
                AutoTagEnabled = false,
            };
            mountA.Load(MountItem.Standard, mountA.SlotCount * 10f); // 소진 트리거를 막는다

            sim.StandbyStackDroneArrivalRate = stackRate;
            sim.StandbyAoeDroneArrivalRate = aoeRate;

            int steps = Mathf.CeilToInt(seconds / 0.05f);
            for (int i = 0; i < steps && sim.Result == CombatResult.InProgress; i++) sim.Tick(0.05f);
            return mountB;
        }

        [Test]
        public void 두_드론은_유입_비율대로_쌓인다()
        {
            // 3 : 1 로 흘려보내면 마운트에도 3 : 1 로 앉아야 한다.
            // ⚠️ 이것이 깨지면 「비율은 생산 속도가 정한다」가 화면에서 성립하지 않는다.
            MountLoad m = RunStandby(stackRate: 3f, aoeRate: 1f, seconds: 5f);

            float stack = m.AmountOf(MountItem.Drone);
            float aoe = m.AmountOf(MountItem.DroneAoe);

            Assert.Greater(stack, 0f, "누적형이 한 기도 안 실렸다");
            Assert.Greater(aoe, 0f, "광역형이 한 기도 안 실렸다 — 앞의 종이 슬롯을 다 먹었다");
            Assert.AreEqual(3f, stack / aoe, 0.15f, "유입 3:1 인데 적재 비율이 갈렸다");
        }

        [Test]
        public void 반반이면_반반으로_쌓인다()
        {
            MountLoad m = RunStandby(stackRate: 2f, aoeRate: 2f, seconds: 5f);

            Assert.AreEqual(m.AmountOf(MountItem.Drone), m.AmountOf(MountItem.DroneAoe), 0.5f,
                "같은 속도로 넣었는데 한쪽으로 쏠렸다");
        }

        [Test]
        public void 슬롯이_다_차면_넘친_몫은_버려진다()
        {
            // ⚠️⚠️ **사실 보고다 — 고치지 않는다.** 만충 뒤에도 유입이 계속되면
            //    `Load` 가 안 받은 몫을 호출자가 **안 본다**(반환값을 버린다).
            //    창고가 없는 드론 축에서는 그것이 곧 **사라짐**이다.
            MountLoad m = RunStandby(stackRate: 50f, aoeRate: 50f, seconds: 5f);

            float capacity = 8 * 5f;
            Assert.AreEqual(capacity, m.Total, 0.01f, "적재량이 그릇을 넘었다");
            Assert.IsTrue(m.IsFull);
        }

        [Test]
        public void 마지막_빈_슬롯은_먼저_부르는_쪽이_가져간다()
        {
            // 호출 순서가 누적형 먼저다(`LoadDronesInto`). 슬롯이 홀수로 남으면
            // 그 한 칸은 누적형이 가져간다 — **열거 순서가 아니라 호출 순서**다.
            // 📌 이 쏠림은 슬롯 하나 몫이라 비율을 뒤집지 않는다(위 두 시험이 그것을 지킨다).
            MountLoad m = RunStandby(stackRate: 10f, aoeRate: 10f, seconds: 5f, slots: 3, droneStack: 5f);

            Assert.GreaterOrEqual(m.AmountOf(MountItem.Drone), m.AmountOf(MountItem.DroneAoe),
                "먼저 부른 쪽이 덜 가져갔다 — 호출 순서가 바뀌었다");
        }
    }
}
