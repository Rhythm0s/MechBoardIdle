using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **합체 중 태그 스킬은 나가고 교대는 잠긴다** (사용자 확정 #163 · 2026-10-16).
    /// </summary>
    public sealed class MergeTagSkillTests
    {
        private static MountLoad Known(int slots = 4) => new MountLoad(slots, new Dictionary<MountItem, float>
        {
            { MountItem.Pierce, 10f }, { MountItem.Standard, 10f }, { MountItem.Explosive, 10f }, { MountItem.Drone, 10f },
        });

        private static TagBattle MergedBattle(out MountLoad a, out MountLoad b)
        {
            a = Known(); b = Known();
            a.Load(MountItem.Standard, 20f);
            b.Load(MountItem.Standard, b.FullCapacity);   // 대기 만충
            var battle = new TagBattle(a, b) { SkillStrike = _ => true };
            battle.Locked = true; battle.SkillWhileLocked = true;   // 합체가 잠근 판
            return battle;
        }

        [Test]
        public void 합체_중_만충이면_교대없이_스킬만_나간다()
        {
            TagBattle battle = MergedBattle(out _, out MountLoad b);
            Assert.IsTrue(battle.CanSkillWhileLocked);
            Assert.IsTrue(battle.TickAuto(0.1f), "자동 스킬이 나간다");
            Assert.AreEqual(0, battle.ActiveIndex, "교대는 안 된다");
            Assert.IsFalse(battle.LastTagSwapped);
            Assert.AreEqual(1, battle.PendingSkillIndex, "대기 로봇(B)의 마운트로 때린다");
            Assert.IsTrue(battle.ResolvePendingSkill());
            Assert.IsTrue(b.IsEmpty, "B 마운트를 비운다");
            Assert.Greater(battle.Tag.CooldownRemaining, 0f, "쿨다운은 똑같이 먹는다");
        }

        [Test]
        public void 합체_중_수동도_스킬만이고_만충이_아니면_아무_일도_없다()
        {
            TagBattle battle = MergedBattle(out _, out MountLoad b);
            b.DrainAll();
            b.Load(MountItem.Standard, 5f);
            Assert.IsFalse(battle.CanSkillWhileLocked);
            Assert.IsFalse(battle.TryManualTag(), "만충 아니면 교대도 스킬도 없다");
            Assert.AreEqual(0, battle.ActiveIndex);
        }

        [Test]
        public void 잠금만_세운_판은_종전대로_아무것도_안_나간다()
        {
            TagBattle battle = MergedBattle(out _, out _);
            battle.SkillWhileLocked = false;
            Assert.IsFalse(battle.TickAuto(0.1f));
            Assert.IsFalse(battle.TryManualTag());
        }

        [Test]
        public void 합체가_잠그고_풀_때_스킬_허용도_같이_따라간다()
        {
            string src = System.IO.File.ReadAllText("Assets/_Project/Scripts/Core/Combat/CombatSimulation.cs");
            StringAssert.Contains("Tag.Locked = true; Tag.SkillWhileLocked = false;", src);   // #228 — 합체 중 태그 스킬 없음
            StringAssert.Contains("Tag.Locked = false; Tag.SkillWhileLocked = false;", src);
            StringAssert.Contains("RobotSide side = _sides[Tag.PendingSkillIndex];", src);
        }
    }
}
