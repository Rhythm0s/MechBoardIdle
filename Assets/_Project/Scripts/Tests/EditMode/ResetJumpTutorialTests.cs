using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>#249 초기화 = 벨트 아이템 버림 · #250 S5 점프 = 폭발탄 튜토리얼이 걸음 ①부터(2026-10-20 사용자 확정).</summary>
    public sealed class ResetJumpTutorialTests
    {
        [Test]
        public void Reset_ReplacesBeltItemFlow_BeforeResolve()
        {
            string bc = System.IO.File.ReadAllText("Assets/_Project/Scripts/Logistics/BoardController.cs");
            int a = bc.IndexOf("_flows[Index(owner)] = new BeltItemFlow();", System.StringComparison.Ordinal);
            int b = bc.IndexOf("ResolveFlow(g, _flows[Index(owner)]);", System.StringComparison.Ordinal);
            Assert.Greater(a, 0, "초기화가 새 흐름을 짓는다");
            Assert.Greater(b, a, "새 흐름을 지은 뒤 푼다");
        }

        [Test]
        public void FreshFlow_HasNoItemsOrArrivals()
        {
            var f = new BeltItemFlow();
            Assert.AreEqual(0, f.PendingMountArrivals.Count, "새 흐름 = 대기 도착 0");
        }

        [Test]
        public void S5Jump_StartsExplosiveTutorialAtStepOne()
        {
            Assert.IsFalse(PresetBoards.ExplosiveOnJump(5), "S5 점프는 프리셋을 안 세운다");
            // 시작 판 = (6,7) 변환기가 아직 발전재료가 아니다 → 걸음 ①
            Assert.AreEqual(ExplosiveTutorialRule.Step.PowerMaterial, ExplosiveTutorialRule.Next(false, false, false, false, false));
            Assert.IsTrue(ExplosiveTutorialRule.IsActive(false, 5, true, ExplosiveTutorialRule.Step.PowerMaterial, true, 300d, 20d),
                "점프 저장(튜토리얼 미통과) · 고철 300(점프 보장) → 막이 선다");
        }
    }
}
