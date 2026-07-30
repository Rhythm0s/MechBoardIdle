using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 레이트 기반 물류 흐름 시뮬(§5-5) — 전력효율·발열감쇠·벨트캡·천장클램프·대표상태 145 재현.
    /// 순수 로직(자산 불필요).
    /// </summary>
    public sealed class LogisticsSimTests
    {
        private const float Delta = 0.001f;

        // 병목 없음(대표 상태) 기준 입력. base 145 = 관통1+분열1+폭발2(§9).
        private static LogisticsResult Representative(float baseOutput = 145f) =>
            LogisticsSimulation.Compute(
                baseOutput,
                powerSupply: 80f, powerDraw: 66f,          // 80>66 → 효율 1
                heatGenerate: 8f, heatDissipate: 0f, heatThreshold: 12f, // 8<12 → 1
                beltCapacity: 14f, beltDemand: 10f,         // 10<14 → 1
                ceiling: 160f);

        [Test]
        public void Representative_NoBottleneck_Reproduces145()
        {
            LogisticsResult r = Representative();
            Assert.AreEqual(145f, r.output, Delta, "대표 상태 출력 = 145(관통1+분열1+폭발2)");
            Assert.AreEqual(1f, r.powerEfficiency, Delta);
            Assert.AreEqual(1f, r.heatThrottle, Delta);
            Assert.AreEqual(1f, r.beltThrottle, Delta);
            Assert.IsFalse(r.bottlenecked);
        }

        [Test]
        public void PowerStarved_HalvesOutput()
        {
            // 공급 33 / 소비 66 → 효율 0.5.
            LogisticsResult r = LogisticsSimulation.Compute(
                145f, 33f, 66f, 8f, 0f, 12f, 14f, 10f, 160f);
            Assert.AreEqual(0.5f, r.powerEfficiency, Delta);
            Assert.AreEqual(72.5f, r.output, Delta);
            Assert.IsTrue(r.bottlenecked);
        }

        [Test]
        public void Heat_OverThreshold_Throttles()
        {
            // 순발열 24 > 임계 12 → 감쇠 0.5.
            LogisticsResult r = LogisticsSimulation.Compute(
                145f, 80f, 66f, 24f, 0f, 12f, 14f, 10f, 160f);
            Assert.AreEqual(0.5f, r.heatThrottle, Delta);
            Assert.AreEqual(72.5f, r.output, Delta);
        }

        [Test]
        public void Belt_OverCapacity_Throttles()
        {
            // 필요 28 > 용량 14 → 감쇠 0.5.
            LogisticsResult r = LogisticsSimulation.Compute(
                145f, 80f, 66f, 8f, 0f, 12f, 14f, 28f, 160f);
            Assert.AreEqual(0.5f, r.beltThrottle, Delta);
            Assert.AreEqual(72.5f, r.output, Delta);
        }

        [Test]
        public void Ceiling_ClampsAt160()
        {
            // base 200, 병목 없음 → 천장 160으로 클램프(물류 단독 상한).
            LogisticsResult r = LogisticsSimulation.Compute(
                200f, 80f, 66f, 8f, 0f, 12f, 14f, 10f, 160f);
            Assert.AreEqual(160f, r.output, Delta);
        }

        [Test]
        public void Throttles_Multiply()
        {
            // 전력 0.5 × 발열 0.5 = 0.25 → 145×0.25 = 36.25.
            LogisticsResult r = LogisticsSimulation.Compute(
                145f, 33f, 66f, 24f, 0f, 12f, 14f, 10f, 160f);
            Assert.AreEqual(36.25f, r.output, Delta);
            Assert.IsTrue(r.bottlenecked);
        }
    }
}
