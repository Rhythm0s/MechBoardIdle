using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 드론 사출(260827_V02 §5 · 밸런스 10-3). 확정 규칙은 **min(유입, 슬롯 × 방출률)** 하나다.
    /// 슬롯 수·방출률·기당 피해는 TBD placeholder이므로 여기 값은 규칙 확인용이며 밸런스 단정이 아니다.
    /// </summary>
    public sealed class DroneBayTests
    {
        private const float D = 0.0001f;

        // 밸런스 10-3의 실측 구성: 유입 1.0 · 슬롯 3 · r 1.0 → min(1.0, 3.0) = 1.0
        private static DroneBay Bay(int slots = 3, float rate = 1f, float charge = 100f) =>
            new DroneBay(slots, rate, charge);

        // ---- min(유입, 슬롯 × 방출률) ----

        /// <summary>밸런스 10-3 실측: min(유입 1.0, 슬롯 3 × r 1.0) = 1.0.</summary>
        [Test]
        public void EffectiveRelease_MatchesBalanceMeasurement()
        {
            DroneBay bay = Bay();

            Assert.AreEqual(3f, bay.SlotThroughput, D, "슬롯 3 × r 1.0");
            Assert.AreEqual(1f, bay.EffectiveRelease(1f), D, "유입이 낮아 유입이 병목");
        }

        /// <summary>
        /// 두 병목 중 **낮은 쪽**이 이긴다. 유입이 모자라면 슬롯이 놀고,
        /// 슬롯이 모자라면 만든 드론이 쌓인다.
        /// </summary>
        [Test]
        public void EffectiveRelease_TakesTheLowerBottleneck()
        {
            DroneBay bay = Bay(slots: 3, rate: 1f); // 처리량 3

            Assert.AreEqual(1f, bay.EffectiveRelease(1f), D, "유입 병목");
            Assert.AreEqual(3f, bay.EffectiveRelease(10f), D, "슬롯 병목 — 유입이 넘쳐도 3");
            Assert.IsTrue(bay.InflowLimited(1f));
            Assert.IsFalse(bay.InflowLimited(10f));
        }

        [Test]
        public void EffectiveRelease_IsZeroWithoutSlotsOrInflow()
        {
            Assert.AreEqual(0f, Bay(slots: 0).EffectiveRelease(5f), D);
            Assert.AreEqual(0f, Bay().EffectiveRelease(0f), D);
            Assert.AreEqual(0f, Bay().EffectiveRelease(-3f), D, "음수 유입은 0으로 본다");
        }

        // ---- 출격 ----

        [Test]
        public void Launch_TakesFromPending_AndOccupiesSlots()
        {
            DroneBay bay = Bay();
            bay.Produce(dt: 3f, inflowPerSec: 1f); // 3기 대기

            int launched = bay.Launch(1f);

            Assert.AreEqual(3, launched);
            Assert.AreEqual(3, bay.Active);
            Assert.AreEqual(0f, bay.Pending, D);
        }

        /// <summary>슬롯이 다 차면 더 못 나간다 — 만든 드론은 대기열에 남는다(버려지지 않는다).</summary>
        [Test]
        public void Launch_BlockedByOccupiedSlots_KeepsPending()
        {
            DroneBay bay = Bay(slots: 2);
            bay.Produce(dt: 10f, inflowPerSec: 1f); // 10기 대기

            bay.Launch(10f);
            Assert.AreEqual(2, bay.Active, "슬롯 2가 상한");

            float pendingBefore = bay.Pending;
            Assert.AreEqual(0, bay.Launch(10f), "빈 슬롯이 없으면 0기");
            Assert.AreEqual(pendingBefore, bay.Pending, D, "대기열은 그대로 — 버리지 않는다");
        }

        /// <summary>방출률이 시간당 상한이므로 짧은 틱에는 그만큼만 나간다.</summary>
        [Test]
        public void Launch_IsRateLimitedPerTick()
        {
            DroneBay bay = Bay(slots: 10, rate: 1f); // 처리량 10/초
            bay.Produce(dt: 100f, inflowPerSec: 1f);

            Assert.AreEqual(2, bay.Launch(0.2f), "10/초 × 0.2초 = 2기");
        }

        /// <summary>소수 유입은 이월된다 — 0.5기씩 두 번이면 1기가 나간다.</summary>
        [Test]
        public void FractionalInflow_CarriesOver()
        {
            DroneBay bay = Bay();

            bay.Produce(1f, 0.5f);
            Assert.AreEqual(0, bay.Launch(1f), "0.5기로는 못 나간다");

            bay.Produce(1f, 0.5f);
            Assert.AreEqual(1, bay.Launch(1f), "합쳐서 1기");
        }

        /// <summary>
        /// 회귀 방지: **작은 dt를 반복해도 드론이 나간다.**
        /// 방출 허용량을 틱마다 버리면 처리량 3기/초에 dt 0.02일 때 틱당 0.06기가 되고,
        /// 정수로 내리면 언제나 0이라 영영 못 나간다 — 실제로 그 상태였고 전투 테스트가 잡았다.
        /// </summary>
        [Test]
        public void SmallTicks_StillLaunch_AllowanceCarriesOver()
        {
            DroneBay bay = Bay(); // 처리량 3기/초
            int launched = 0;

            for (int i = 0; i < 50; i++) // 1초를 0.02초로 쪼갠다
            {
                bay.Produce(0.02f, 1f);
                launched += bay.Launch(0.02f);
            }

            Assert.AreEqual(1, launched, "유입 1기/초 × 1초 = 1기");
        }

        /// <summary>허용량이 무한히 쌓여 한꺼번에 터지지 않는다 — 쓴 만큼만 깎인다.</summary>
        [Test]
        public void Allowance_DoesNotStockpileIntoABurst()
        {
            DroneBay bay = Bay(slots: 3, rate: 1f);

            for (int i = 0; i < 100; i++) bay.Launch(0.1f); // 유입 없이 10초 — 허용량만 흐른다
            bay.Produce(10f, 1f);                            // 이제 10기가 대기

            Assert.LessOrEqual(bay.Launch(0.1f), 3, "슬롯 3을 넘어 한꺼번에 나가지 않는다");
        }

        // ---- 수명: 충전량 = 피해 총량 (§5-3 회신분) ----

        /// <summary>
        /// 드론은 **B의 탄약**이다. 충전량이 곧 피해 총량이자 수명이라,
        /// 다 쓰면 소멸하고 회수되지 않는다 — 탄약이 되돌아오지 않는 것과 같다.
        /// </summary>
        [Test]
        public void Drone_SpendsChargeAsDamage_ThenDies()
        {
            var drone = new DroneUnit(Vector2.zero, charge: 100f, damagePerHit: 40f, attackRange: 100f);

            Assert.AreEqual(40f, drone.Fire(), D);
            Assert.AreEqual(40f, drone.Fire(), D);
            Assert.AreEqual(80f, 100f - drone.Charge, 0.001f, "40 두 발 = 80 소비");
            Assert.AreEqual(20f, drone.Charge, 0.001f, "20 남음");
            Assert.IsTrue(drone.IsAlive);

            Assert.AreEqual(20f, drone.Fire(), D, "마지막 발은 남은 만큼만");
            Assert.IsFalse(drone.IsAlive);
            Assert.AreEqual(0f, drone.Fire(), D, "죽은 드론은 못 쏜다");
        }

        /// <summary>총 피해량은 충전량과 정확히 같다 — 넘지도 모자라지도 않는다.</summary>
        [Test]
        public void TotalDamage_EqualsCharge()
        {
            var drone = new DroneUnit(Vector2.zero, charge: 100f, damagePerHit: 30f, attackRange: 1f);

            float total = 0f;
            for (int i = 0; i < 10; i++) total += drone.Fire();

            Assert.AreEqual(100f, total, D, "30 × 3 + 10 = 100");
        }

        /// <summary>
        /// 소멸하면 **슬롯이 즉시 빈다.** 늦게 비우면 방출률 r이 의미를 잃고
        /// min(유입, 슬롯 × r) 식이 성립하지 않는다.
        /// </summary>
        [Test]
        public void Retire_FreesSlotImmediately()
        {
            DroneBay bay = Bay(slots: 2);
            bay.Produce(10f, 1f);
            bay.Launch(10f);
            Assert.AreEqual(2, bay.Active);

            bay.Retire();

            Assert.AreEqual(1, bay.Active);
            Assert.AreEqual(1, bay.Launch(10f), "빈 슬롯이 바로 채워진다");
        }

        [Test]
        public void Retire_DoesNotGoNegative()
        {
            DroneBay bay = Bay();
            bay.Retire(5);
            Assert.AreEqual(0, bay.Active);
        }

        /// <summary>전투 종료 정리 — 필드가 없어지므로 나가 있던 드론도 함께 사라진다.</summary>
        [Test]
        public void Reset_ClearsFieldAndQueue()
        {
            DroneBay bay = Bay();
            bay.Produce(10f, 1f);
            bay.Launch(10f);

            bay.Reset();

            Assert.AreEqual(0, bay.Active);
            Assert.AreEqual(0f, bay.Pending, D);
        }

        // ---- 사거리 ----

        /// <summary>드론 사거리는 본체와 동일하게 둔다(C-3 확정).</summary>
        [Test]
        public void DroneRange_IsWhateverItIsGiven()
        {
            var drone = new DroneUnit(Vector2.zero, 100f, 10f, attackRange: 100f);
            Assert.AreEqual(100f, drone.AttackRange, D);
        }

        /// <summary>드론 몸체 조합표가 유입원이다 — 보드가 만드는 것이 그대로 유입이 된다.</summary>
        [Test]
        public void DroneBodyRecipe_FeedsTheBay()
        {
            var recipe = new NodeRecipe
            {
                kind = RecipeKind.DroneBody, output = FlowKind.Drone,
                outputPerSec = 1f, stackLimitTbd = 0f, implemented = true,
            };

            float produced = NodeProduction.Produce(recipe, bufferNow: 0f, dt: 3f);

            DroneBay bay = Bay();
            bay.Produce(1f, produced / 3f); // 초당 유입으로 환산
            Assert.AreEqual(1f, bay.Pending, D);
        }
    }
}
