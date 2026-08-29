using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 마운트 적재(260827_V03 §2·§3). **만충·소진 판정의 주체**가 창고에서 여기로 옮겨졌다.
    ///
    /// 스택 수치는 검증 대장 TBD(확정은 추진제 3 하나)이므로 여기 값은 규칙 확인용이며
    /// 밸런스 단정이 아니다.
    /// </summary>
    public sealed class MountLoadTests
    {
        private const float D = 0.001f;

        private static Dictionary<MountItem, float> Stacks(float ammo = 10f, float drone = 5f) =>
            new Dictionary<MountItem, float>
            {
                { MountItem.Pierce, ammo }, { MountItem.Split, ammo },
                { MountItem.Explosive, ammo }, { MountItem.Drone, drone },
            };

        // 로봇 A = 4슬롯 / 로봇 B = 8슬롯 (V03 §3 확정)
        private static MountLoad RobotA(Dictionary<MountItem, float> stacks = null) =>
            new MountLoad(4, stacks ?? Stacks());
        private static MountLoad RobotB() => new MountLoad(8, Stacks());

        // ---- 슬롯 규칙 ----

        [Test]
        public void SlotCounts_MatchConfirmedSpec()
        {
            Assert.AreEqual(4, RobotA().SlotCount, "로봇 A");
            Assert.AreEqual(8, RobotB().SlotCount, "로봇 B — 단발 고밀도라 천천히 채워 크게 쓴다");
        }

        /// <summary>슬롯 하나에는 **아이디가 같은 것만** 쌓인다.</summary>
        [Test]
        public void OneSlot_HoldsOneItemId()
        {
            MountLoad m = RobotA();

            m.Load(MountItem.Pierce, 10f);   // 슬롯 하나를 꽉 채운다
            m.Load(MountItem.Split, 3f);

            Assert.AreEqual(MountItem.Pierce, m.ItemAt(0));
            Assert.AreEqual(MountItem.Split, m.ItemAt(1), "다른 품목은 새 슬롯을 연다");
            Assert.AreEqual(10f, m.AmountAt(0), D);
            Assert.AreEqual(3f, m.AmountAt(1), D);
        }

        /// <summary>같은 품목이 **여러 슬롯**을 차지할 수 있다 — 네 칸 전부 관통탄도 된다.</summary>
        [Test]
        public void SameItem_CanOccupyMultipleSlots()
        {
            MountLoad m = RobotA();

            float loaded = m.Load(MountItem.Pierce, 40f); // 스택 10 × 4슬롯

            Assert.AreEqual(40f, loaded, D);
            Assert.AreEqual(4, m.SlotsUsedBy(MountItem.Pierce), "네 칸 전부 관통탄");
            Assert.AreEqual(40f, m.Total, D);
        }

        /// <summary>
        /// **먼저 도착한 것이 슬롯을 차지한다.** 분류기를 안 쓰면 한 탄종이 칸을 다 먹는데
        /// 그것이 의도된 결과다 — 자리가 없으면 뒤에 온 것은 못 들어간다.
        /// </summary>
        [Test]
        public void FirstArrival_ClaimsSlots_LaterItemsAreTurnedAway()
        {
            MountLoad m = RobotA();

            m.Load(MountItem.Pierce, 40f); // 네 칸 독식
            float rejected = m.Load(MountItem.Explosive, 10f);

            Assert.AreEqual(0f, rejected, D, "자리가 없으면 한 발도 못 들어간다");
            Assert.AreEqual(0, m.SlotsUsedBy(MountItem.Explosive));
        }

        /// <summary>넘치는 분은 들어가지 않고 반환값으로 알린다 — 호출자가 창고에 남긴다.</summary>
        [Test]
        public void Load_ReturnsWhatActuallyWentIn()
        {
            MountLoad m = RobotA();

            Assert.AreEqual(40f, m.Load(MountItem.Pierce, 100f), D, "상한 40까지만");
            Assert.AreEqual(40f, m.Total, D);
        }

        /// <summary>적재량 = Σ(품목별 슬롯 수 × 그 품목의 스택).</summary>
        [Test]
        public void Capacity_IsSumOverSlotsOfTheirItemStacks()
        {
            MountLoad m = RobotA(Stacks(ammo: 10f, drone: 5f));

            m.Load(MountItem.Pierce, 10f);  // 슬롯 1개 × 10
            m.Load(MountItem.Drone, 5f);    // 슬롯 1개 × 5

            Assert.AreEqual(15f, m.Capacity, D, "10 + 5");
        }

        // ---- 만충 판정 (태그 발동 조건) ----

        [Test]
        public void IsFull_RequiresEverySlotClaimedAndFilled()
        {
            MountLoad m = RobotA();

            m.Load(MountItem.Pierce, 30f); // 세 칸만
            Assert.IsFalse(m.IsFull, "빈 슬롯이 있으면 만충이 아니다");

            m.Load(MountItem.Pierce, 10f);
            Assert.IsTrue(m.IsFull);
        }

        /// <summary>
        /// **스택 수치가 없으면 만충을 판정할 수 없다.** 상한을 모르는데 「가득 찼다」고
        /// 말할 수 없기 때문이다 — 임의 상한을 끼우지 않는다.
        /// 탄약·드론 스택은 검증 대장 TBD이므로 지금 실제로 이 상태다.
        /// </summary>
        [Test]
        public void UnsetStack_MakesFullnessUnjudgeable_NotAutomaticallyFull()
        {
            var m = new MountLoad(4, new Dictionary<MountItem, float>()); // 상한 전부 미확정

            // 상한이 없으면 한 슬롯이 무제한으로 먹으므로, 전 슬롯을 차지하려면 품목을 넷 다르게 넣는다.
            m.Load(MountItem.Pierce, 999f);
            m.Load(MountItem.Split, 999f);
            m.Load(MountItem.Explosive, 999f);
            m.Load(MountItem.Drone, 999f);

            Assert.IsTrue(m.AllSlotsClaimed, "네 칸이 다 찼다");
            Assert.IsFalse(m.IsFull, "그래도 상한을 모르면 만충이라고 하지 않는다");
            Assert.IsFalse(m.CanJudgeFullness, "판정 불가임을 알린다");
        }

        /// <summary>
        /// 슬롯이 비어 있으면 스택을 몰라도 **「만충 아님」은 확실하다** — 판정 가능이다.
        /// 판정이 막히는 것은 전 슬롯이 찬 뒤 「이게 끝인가」를 물을 때뿐이다.
        /// </summary>
        [Test]
        public void EmptySlots_AreJudgeableEvenWithUnknownStacks()
        {
            var m = new MountLoad(4, new Dictionary<MountItem, float>());
            m.Load(MountItem.Pierce, 999f); // 한 칸만 차지

            Assert.IsFalse(m.AllSlotsClaimed);
            Assert.IsTrue(m.CanJudgeFullness, "빈 칸이 있으면 만충이 아님이 확실하다");
            Assert.IsFalse(m.IsFull);
        }

        [Test]
        public void CanJudgeFullness_IsTrueWhenStacksAreKnown()
        {
            MountLoad m = RobotA();
            m.Load(MountItem.Pierce, 40f);

            Assert.IsTrue(m.CanJudgeFullness);
            Assert.IsTrue(m.IsFull);
        }

        // ---- 소비 ----

        /// <summary>그 품목만 본다 — 다른 품목이 쌓여 있어도 대신 쓰지 않는다.</summary>
        [Test]
        public void TryConsume_DoesNotBorrowFromAnotherItem()
        {
            MountLoad m = RobotA();
            m.Load(MountItem.Explosive, 20f);

            Assert.IsFalse(m.TryConsume(MountItem.Pierce, 1f), "관통이 없으면 폭발로 대신 쏘지 않는다");
            Assert.AreEqual(20f, m.AmountOf(MountItem.Explosive), D, "실패가 남의 것을 깎지 않는다");
            Assert.IsTrue(m.TryConsume(MountItem.Explosive, 1f));
        }

        /// <summary>다 쓴 슬롯은 놓아 준다 — 그래야 다른 품목이 그 자리를 쓸 수 있다.</summary>
        [Test]
        public void EmptiedSlot_IsReleasedForOtherItems()
        {
            MountLoad m = RobotA();
            m.Load(MountItem.Pierce, 40f);
            Assert.AreEqual(0f, m.Load(MountItem.Split, 5f), D, "지금은 자리가 없다");

            m.TryConsume(MountItem.Pierce, 20f); // 두 칸 비움

            Assert.AreEqual(5f, m.Load(MountItem.Split, 5f), D, "비워진 칸에 들어간다");
        }

        [Test]
        public void TryConsume_FailsWhenInsufficient()
        {
            MountLoad m = RobotA();
            m.Load(MountItem.Pierce, 3f);

            Assert.IsFalse(m.TryConsume(MountItem.Pierce, 5f));
            Assert.AreEqual(3f, m.AmountOf(MountItem.Pierce), D);
        }

        // ---- 태그 스킬 (V03 §4) ----

        /// <summary>
        /// 태그 스킬은 **마운트 재고 전량을 소진**한다. 저장 노드는 남는다 —
        /// 마운트가 빈 동안 화력이 죽고 벨트가 다시 채우며, 물류가 좋을수록 그 공백이 짧다.
        /// </summary>
        [Test]
        public void DrainAll_EmptiesMountAndReportsAmount()
        {
            MountLoad m = RobotA();
            m.Load(MountItem.Pierce, 20f);
            m.Load(MountItem.Split, 10f);

            float drained = m.DrainAll();

            Assert.AreEqual(30f, drained, D, "소진량을 알린다 — 특공 피해 계산의 입력");
            Assert.IsTrue(m.IsEmpty);
            Assert.IsFalse(m.AllSlotsClaimed, "슬롯도 전부 풀린다");
        }

        /// <summary>빈 마운트를 비워도 아무 일도 없다 — 0을 돌려준다.</summary>
        [Test]
        public void DrainAll_OnEmptyMount_IsZero()
        {
            Assert.AreEqual(0f, RobotA().DrainAll(), D);
        }

        // ---- 층위 분리 (V03 §2) ----

        /// <summary>
        /// **창고는 만충 판정에 세지 않는다.** 2026-08-27 개정으로 판정 주체가 마운트로 옮겨졌다.
        /// 창고가 가득 차도 마운트가 비어 있으면 태그는 발동하지 않는다.
        /// </summary>
        [Test]
        public void WarehouseFullness_NoLongerDrivesTagCondition()
        {
            var warehouse = new AmmoInventory(40f);
            warehouse.Fill(AmmoKind.Pierce);
            Assert.IsTrue(warehouse.IsFull, "창고는 가득 찼다");

            MountLoad mount = RobotA();
            Assert.IsFalse(mount.IsFull, "그래도 마운트가 비었으면 만충이 아니다");
        }

        /// <summary>반대로 마운트가 차면 창고가 비어 있어도 만충이다.</summary>
        [Test]
        public void MountFullness_StandsAlone()
        {
            var warehouse = new AmmoInventory(40f);
            Assert.IsTrue(warehouse.IsEmpty);

            MountLoad mount = RobotA();
            mount.Load(MountItem.Pierce, 40f);

            Assert.IsTrue(mount.IsFull, "창고와 무관하게 마운트로 판정한다");
        }

        /// <summary>
        /// 슬롯 수는 상수로 소유한다 — 러너가 4·8을 직접 적으면 비대칭이 두 곳에서 갈린다.
        /// 비대칭은 의도다: A는 다발형이라 자주 채우고 자주 쓰고, B는 단발 고밀도라 크게 쓴다.
        /// </summary>
        [Test]
        public void SlotCounts_AreOwnedHere_AndAsymmetric()
        {
            Assert.AreEqual(4, MountLoad.SlotsRobotA);
            Assert.AreEqual(8, MountLoad.SlotsRobotB);
            Assert.AreEqual(MountLoad.SlotsRobotA, RobotA().SlotCount);
            Assert.AreEqual(MountLoad.SlotsRobotB, RobotB().SlotCount);
            Assert.Less(MountLoad.SlotsRobotA, MountLoad.SlotsRobotB, "B가 더 많이 든다");
        }

        /// <summary>
        /// 상한을 안 넘기면 **만충이 서지 않는다.** 러너가 실제로 이렇게 만든다 —
        /// 탄약·드론 스택이 검증 대장 TBD라 하드코딩한 상한을 끼우지 않기 때문이다.
        ///
        /// 상한이 없으면 한 칸이 얼마든 받으므로 나머지 칸이 영영 안 열린다.
        /// 그래서 자동 태그를 여는 것은 만충 쪽이 아니라 **소진 쪽**뿐이다 —
        /// 스택이 확정되기 전까지 게임에서 관찰될 태그는 전부 소진 트리거다.
        /// </summary>
        [Test]
        public void WithoutStackLimits_FullnessNeverStands()
        {
            var mount = new MountLoad(MountLoad.SlotsRobotA);
            mount.Load(MountItem.Pierce, 999f);

            Assert.AreEqual(1, mount.SlotsUsedBy(MountItem.Pierce), "상한이 없으면 한 칸이 전부 먹는다");
            Assert.IsFalse(mount.AllSlotsClaimed, "나머지 칸은 열리지 않는다");
            Assert.IsFalse(mount.IsFull, "그래서 만충 트리거가 안 열린다");
            Assert.IsFalse(mount.IsEmpty, "소진 판정은 그래도 선다 — 태그는 이쪽으로 열린다");
        }
    }
}
