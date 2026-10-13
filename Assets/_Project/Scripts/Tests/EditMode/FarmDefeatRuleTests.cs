using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **파밍 층에서 부서지면 한 판 뒤로** (2026-10-13 사용자 확정 #113).
    ///
    /// 🗑️ 「제자리 부활」 가정은 폐기됐다 — 그러면 파괴에 값이 없고 파밍이
    /// 「죽지 않는 자동 사냥」이 된다.
    /// </summary>
    public sealed class FarmDefeatRuleTests
    {
        [Test]
        public void 한_판_뒤로_간다()
        {
            Assert.AreEqual(2, FarmDefeatRule.NextStageNumber(3), "S3 에서 부서지면 S2");
            Assert.AreEqual(4, FarmDefeatRule.NextStageNumber(5));
            Assert.AreEqual(5, FarmDefeatRule.NextStageNumber(6));
        }

        /// <summary>⚠️ **가정** — S1 앞에는 판이 없으므로 그 자리에 다시 선다.</summary>
        [Test]
        public void S1_은_갈_데가_없어_제자리다()
        {
            Assert.AreEqual(1, FarmDefeatRule.NextStageNumber(1));
            Assert.IsFalse(FarmDefeatRule.MovesBack(1), "S1 은 뒤로 안 밀린다");
        }

        /// <summary>여섯에 안 드는 판(튜토리얼)은 이 규칙을 안 탄다.</summary>
        [Test]
        public void 여섯_밖은_규칙_밖이다()
        {
            Assert.AreEqual(0, FarmDefeatRule.NextStageNumber(0));
            Assert.AreEqual(0, FarmDefeatRule.NextStageNumber(-3));
            Assert.IsFalse(FarmDefeatRule.MovesBack(0));
        }

        [Test]
        public void S2_위에서는_뒤로_밀린다()
        {
            for (int k = 2; k <= StageMapName.TotalStages; k++)
                Assert.IsTrue(FarmDefeatRule.MovesBack(k), $"S{k} 는 뒤로 밀려야 한다");
        }

        /// <summary>⚠️ 어느 판에서 부서져도 **여섯 안에 머문다** — 0 이나 음수로 안 떨어진다.</summary>
        [Test]
        public void 여섯_안에_머문다()
        {
            for (int k = 1; k <= StageMapName.TotalStages; k++)
            {
                int next = FarmDefeatRule.NextStageNumber(k);
                Assert.GreaterOrEqual(next, 1, $"S{k} 에서 여섯 밖으로 떨어졌다");
                Assert.LessOrEqual(next, StageMapName.TotalStages);
            }
        }

        /// <summary>기다리는 초가 **0 보다 크다** — 0 이면 무엇이 일어났는지 안 읽힌다.</summary>
        [Test]
        public void 다시_서기까지_한_번_읽을_틈이_있다()
        {
            Assert.Greater(FarmDefeatRule.RespawnDelaySeconds, 0f);
            Assert.Less(FarmDefeatRule.RespawnDelaySeconds, 10f, "방치형에서 손이 묶인다");
        }
    }
}
