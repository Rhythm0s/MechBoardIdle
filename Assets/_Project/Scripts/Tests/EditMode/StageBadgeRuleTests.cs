using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **배지 셋째 줄 규칙** (2026-10-13 · 피어 실측 결함 회귀).
    ///
    /// 실제로 뜬 그림 — 튜토리얼 전용 판에 **「남은 몬스터 0 · 남은 시간 166:39」**.
    /// 전투가 없는 판에 도전 문구가 붙었고, 166:39 는 자산의 `challengeTime: 9999`를
    /// 제한시간으로 믿고 적은 수였다. 두 가지를 다 여기서 막는다.
    /// </summary>
    public sealed class StageBadgeRuleTests
    {
        [Test]
        public void 튜토리얼_판은_셋째_줄이_없다()
        {
            // 번호를 못 읽는 판(StageMapName.NumberOf 가 0) — 층이 무엇이든 None.
            Assert.AreEqual(StageBadgeRule.StatusKind.None,
                            StageBadgeRule.StatusOf(0, true));
            Assert.AreEqual(StageBadgeRule.StatusKind.None,
                            StageBadgeRule.StatusOf(0, false));
            Assert.AreEqual(StageBadgeRule.StatusKind.None,
                            StageBadgeRule.StatusOf(-1, false));
        }

        [Test]
        public void 여섯에_드는_판은_층이_줄을_정한다()
        {
            Assert.AreEqual(StageBadgeRule.StatusKind.Farm,
                            StageBadgeRule.StatusOf(1, true));
            Assert.AreEqual(StageBadgeRule.StatusKind.Challenge,
                            StageBadgeRule.StatusOf(1, false));
            Assert.AreEqual(StageBadgeRule.StatusKind.Farm,
                            StageBadgeRule.StatusOf(6, true));
        }

        [Test]
        public void 튜토리얼_판의_실제_번호가_0_이다()
        {
            // ⚠️ 규칙이 받는 수가 **진짜 그 판에서 0 인가**를 같이 묶어 둔다 —
            //    규칙만 맞고 입력이 틀리면 화면은 그대로 틀린다.
            Assert.AreEqual(0, StageMapName.NumberOf("S0"));
            Assert.Greater(StageMapName.NumberOf("S1"), 0);
        }

        [Test]
        public void 제한_없음_표식은_시계로_안_적는다()
        {
            // 자산이 쓰는 수 — Stage_S0.asset 의 challengeTime: 9999.
            Assert.IsFalse(StageBadgeRule.ClockIsMeaningful(9999f),
                           "9999 를 시계로 적으면 166:39 가 뜬다");
            Assert.IsFalse(StageBadgeRule.ClockIsMeaningful(0f));
            Assert.IsFalse(StageBadgeRule.ClockIsMeaningful(-1f));
        }

        [Test]
        public void 도전_판의_제한시간은_시계로_적는다()
        {
            // 자산이 쓰는 수 — Stage_S1~S6 의 challengeTime: 120.
            Assert.IsTrue(StageBadgeRule.ClockIsMeaningful(120f));
            Assert.IsTrue(StageBadgeRule.ClockIsMeaningful(1f));
        }

        [Test]
        public void 경계는_한_시간이다()
        {
            // ⚠️ 가정 — 한 판이 한 시간을 넘는 설계가 없다(설계 역기입 자리).
            Assert.AreEqual(3600f, StageBadgeRule.NoLimitSentinel);
            Assert.IsTrue(StageBadgeRule.ClockIsMeaningful(3599f));
            Assert.IsFalse(StageBadgeRule.ClockIsMeaningful(3600f));
        }
    }
}
