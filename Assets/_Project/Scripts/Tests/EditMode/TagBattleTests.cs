using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// A↔B 교대(전투 시스템 문서 2-1장).
    ///
    /// **태그와 태그 스킬은 조건이 다르다**(2026-08-27 정정) — 둘을 한 조건으로 묶으면
    /// 스택 수치가 없다는 이유로 교대까지 멈춘다.
    /// </summary>
    public sealed class TagBattleTests
    {
        private const float D = 0.001f;

        private static Dictionary<MountItem, float> Stacks(float ammo = 10f) =>
            new Dictionary<MountItem, float>
            {
                { MountItem.Pierce, ammo }, { MountItem.Split, ammo },
                { MountItem.Explosive, ammo }, { MountItem.Drone, ammo },
            };

        /// <summary>스택 상한을 아는 마운트(만충 판정 가능).</summary>
        private static MountLoad Known(int slots = 4) => new MountLoad(slots, Stacks());

        /// <summary>스택 상한을 **모르는** 마운트 — 탄약·드론이 검증 대장 TBD인 현재 상태.</summary>
        private static MountLoad Unknown(int slots = 4) =>
            new MountLoad(slots, new Dictionary<MountItem, float>());

        // ---- 태그는 스택과 무관하게 돈다 ----

        /// <summary>
        /// **스택 수치를 몰라도 소진 트리거는 작동한다.** 「비었는가」는 상한과 무관하기 때문이다.
        /// 이게 안 되면 스택이 확정될 때까지 교대 자체가 멈춘다.
        /// </summary>
        [Test]
        public void DepletedTrigger_WorksWithoutKnownStacks()
        {
            MountLoad a = Unknown(), b = Unknown();
            b.Load(MountItem.Pierce, 20f); // 대기 로봇에는 있다
            var battle = new TagBattle(a, b);

            Assert.IsTrue(a.IsEmpty, "활성이 말랐다");
            Assert.IsFalse(a.CanJudgeFullness == false, "빈 마운트는 판정 가능하다");

            Assert.IsTrue(battle.TickAuto(0.1f), "소진 트리거로 교대한다");
            Assert.AreEqual(1, battle.ActiveIndex, "B가 나간다");
        }

        /// <summary>만충 트리거는 스택이 없으면 안 켜진다 — 그러나 그것이 교대를 막지는 않는다.</summary>
        [Test]
        public void FullTrigger_NeedsStacks_ButDoesNotBlockTagging()
        {
            MountLoad a = Unknown(), b = Unknown();
            a.Load(MountItem.Pierce, 5f);   // 활성은 살아 있다 → 소진 트리거 없음
            b.Load(MountItem.Pierce, 999f); // 대기는 잔뜩 있으나 상한을 모른다

            var battle = new TagBattle(a, b);

            Assert.IsFalse(b.IsFull, "상한을 모르면 만충이 아니다");
            Assert.IsFalse(battle.TickAuto(0.1f), "지금은 교대 사유가 없다");
            Assert.AreEqual(0, battle.ActiveIndex);
        }

        /// <summary>스택이 확정되면 만충 트리거가 그대로 켜진다 — 코드를 고칠 것이 없다.</summary>
        [Test]
        public void FullTrigger_TurnsOnOnceStacksAreKnown()
        {
            MountLoad a = Known(), b = Known();
            a.Load(MountItem.Pierce, 5f);
            b.Load(MountItem.Pierce, 40f); // 4슬롯 × 10 = 만충

            var battle = new TagBattle(a, b);

            Assert.IsTrue(b.IsFull);
            Assert.IsTrue(battle.TickAuto(0.1f), "만충 트리거로 교대");
            Assert.AreEqual(1, battle.ActiveIndex);
        }

        // ---- 태그 스킬은 스택이 있어야 한다 ----

        /// <summary>
        /// 태그 스킬은 **들어오는 쪽의 마운트가 만충**일 때만 나간다.
        /// 만재 등장이 보상형인 이유가 이것이고, 소진 트리거에는 붙지 않는다.
        /// </summary>
        [Test]
        public void TagSkill_FiresOnFullEntry_AndDrainsTheMount()
        {
            MountLoad a = Known(), b = Known();
            a.Load(MountItem.Pierce, 5f);
            b.Load(MountItem.Pierce, 40f);

            var battle = new TagBattle(a, b);
            battle.TickAuto(0.1f);

            Assert.IsTrue(battle.LastTagFiredSkill);
            Assert.AreEqual(40f, battle.LastTagSkillDrained, D, "적재 전량이 소진된다");
            Assert.IsTrue(battle.ActiveMount.IsEmpty, "나온 로봇의 마운트가 비었다");
        }

        /// <summary>소진 트리거로 교대하면 스킬이 없다 — 미완충의 처벌이 유지돼야 한다.</summary>
        [Test]
        public void TagSkill_DoesNotFireOnDepletedEntry()
        {
            MountLoad a = Known(), b = Known();
            b.Load(MountItem.Pierce, 20f); // 만충은 아니다(40이 만충)

            var battle = new TagBattle(a, b);
            Assert.IsTrue(battle.TickAuto(0.1f), "소진으로 교대는 한다");

            Assert.IsFalse(battle.LastTagFiredSkill, "스킬은 안 나간다");
            Assert.AreEqual(20f, battle.ActiveMount.Total, D, "적재도 그대로 남는다");
        }

        /// <summary>스택을 모르면 스킬이 안 나간다 — 교대는 되지만 스킬만 잠긴다.</summary>
        [Test]
        public void TagSkill_StaysOffWhileStacksAreUnknown()
        {
            MountLoad a = Unknown(), b = Unknown();
            b.Load(MountItem.Pierce, 999f);

            var battle = new TagBattle(a, b);
            battle.TickAuto(0.1f);

            Assert.AreEqual(1, battle.ActiveIndex, "교대는 됐다");
            Assert.IsFalse(battle.LastTagFiredSkill, "스킬만 잠긴다");
            Assert.AreEqual(999f, battle.ActiveMount.Total, D, "소진되지 않았다");
        }

        /// <summary>스킬 피해식은 GrandEntrance 하나를 쓴다(합체 경로와 공용).</summary>
        [Test]
        public void TagSkillDamage_UsesSharedFormula()
        {
            MountLoad a = Known(), b = Known();
            a.Load(MountItem.Pierce, 5f);
            b.Load(MountItem.Pierce, 40f);

            var battle = new TagBattle(a, b);
            battle.TickAuto(0.1f);

            Assert.AreEqual(2104f, battle.TagSkillDamage(52.6f), 1f, "40 × 52.6 — params tagspec 대조");
        }

        // ---- 쿨다운·잠금 ----

        [Test]
        public void Cooldown_BlocksImmediateRetag()
        {
            MountLoad a = Known(), b = Known();
            b.Load(MountItem.Pierce, 5f); // 갈 곳이 있어야 소진 트리거가 성립한다
            var battle = new TagBattle(a, b);

            Assert.IsTrue(battle.TickAuto(0.1f), "활성이 비었고 대기에는 있다");
            int after = battle.ActiveIndex;

            Assert.IsFalse(battle.TickAuto(0.1f), "쿨다운 중에는 못 바꾼다");
            Assert.AreEqual(after, battle.ActiveIndex, "제자리 — 매 프레임 진동하지 않는다");
        }

        [Test]
        public void Locked_BlocksTagDuringMerge()
        {
            MountLoad a = Known(), b = Known();
            b.Load(MountItem.Pierce, 40f);

            var battle = new TagBattle(a, b) { Locked = true };

            Assert.IsFalse(battle.TickAuto(0.1f), "합체 중에는 태그 불가");
            Assert.AreEqual(0, battle.ActiveIndex);
        }

        [Test]
        public void ManualTag_Works_AndRespectsCooldown()
        {
            MountLoad a = Known(), b = Known();
            a.Load(MountItem.Pierce, 10f);
            b.Load(MountItem.Pierce, 10f);

            var battle = new TagBattle(a, b);

            Assert.IsTrue(battle.TryManualTag());
            Assert.AreEqual(1, battle.ActiveIndex);
            Assert.IsFalse(battle.TryManualTag(), "쿨다운 중 수동도 막힌다");
        }

        /// <summary>
        /// **갈 곳이 없으면 교대하지 않는다.** 대기도 비었는데 소진 트리거로 넘어가면
        /// 상태는 그대로인 채 5초 쿨다운만 먹고, 정작 대기가 찼을 때 못 나간다.
        /// 둘 다 빈 것은 교대 사유가 아니라 실패 조건이다.
        /// </summary>
        [Test]
        public void DepletedTrigger_DoesNotFireWhenStandbyIsAlsoEmpty()
        {
            MountLoad a = Known(), b = Known();
            var battle = new TagBattle(a, b);

            Assert.IsTrue(battle.BothDepleted);
            Assert.IsFalse(battle.TickAuto(0.1f), "헛교대하지 않는다");
            Assert.AreEqual(0, battle.ActiveIndex);
            Assert.IsTrue(battle.Tag.CanTag, "쿨다운도 안 먹는다 — 대기가 차면 바로 나갈 수 있다");
        }

        // ---- 실패 조건 ----

        /// <summary>**A·B 동시 고갈 → 공격 정지.** 한쪽만 비면 교대로 넘어가면 된다.</summary>
        [Test]
        public void BothDepleted_IsTheFailureCondition()
        {
            MountLoad a = Known(), b = Known();
            var battle = new TagBattle(a, b);

            Assert.IsTrue(battle.BothDepleted, "둘 다 비었다 = 공격 정지");

            b.Load(MountItem.Pierce, 5f);
            Assert.IsFalse(battle.BothDepleted, "한쪽에 있으면 아직 실패가 아니다");
        }

        // ---- 대기 로봇도 채워진다 ----

        /// <summary>
        /// 대기 로봇의 공장도 계속 돈다 — 그 산출이 대기 마운트에 쌓여 태그 인 순간
        /// 비축 화력이 된다. 저장 노드의 존재 이유가 이것이다.
        /// </summary>
        [Test]
        public void StandbyMount_AccumulatesWhileWaiting()
        {
            MountLoad a = Known(), b = Known();
            a.Load(MountItem.Pierce, 40f);
            var battle = new TagBattle(a, b);

            battle.StandbyMount.Load(MountItem.Split, 25f); // 대기 중 적립

            Assert.AreEqual(25f, battle.StandbyMount.Total, D);
            Assert.AreEqual(40f, battle.ActiveMount.Total, D, "활성 쪽은 그대로");
        }

        [Test]
        public void Reset_ReturnsToRobotA()
        {
            MountLoad a = Known(), b = Known();
            var battle = new TagBattle(a, b);
            battle.TryManualTag();

            battle.Reset();

            Assert.AreEqual(0, battle.ActiveIndex);
            Assert.IsTrue(battle.Tag.CanTag);
        }
    }
}
