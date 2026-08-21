using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 레이트 기반 물류 흐름 시뮬(§5-5, L4-R) — 이중값(expected/actual/gap)·앵커 순서(100→145→배율초과)·
    /// 갭 분해(telescoping)·병목 배율. 순수 로직(자산 불필요).
    /// </summary>
    public sealed class LogisticsSimTests
    {
        private const float Delta = 0.001f;
        private const float Origin = 100f;

        // 병목 없음 기준 입력(baseOutput만 변주).
        private static LogisticsResult NoBottleneck(float baseOutput) =>
            LogisticsSimulation.Compute(
                baseOutput,
                powerSupply: 80f, powerDraw: 66f,          // 80>66 → 효율 1
                heatGenerate: 8f, heatDissipate: 0f, heatThreshold: 12f, // net 8 < 12 → 1
                beltCapacity: 14f, beltDemand: 10f,         // 10 < 14 → 1
                origin: Origin);

        // --- 앵커 순서(§L4-R #3): 100(기본공장) → 145(대표 최적화) → 배율초과(RED) ---

        [Test]
        public void BaseFactory_Anchor100()
        {
            // 기본 공장 출력 = 원점 100(모든 요구치의 분모). 병목 없음 → expected = actual = 100.
            LogisticsResult r = NoBottleneck(100f);
            Assert.AreEqual(100f, r.expected, Delta);
            Assert.AreEqual(100f, r.actual, Delta);
            Assert.AreEqual(0f, r.gap, Delta);
            Assert.AreEqual(1.0f, r.multiple, Delta, "배율 = expected/origin = 1.0");
            Assert.IsFalse(r.bottlenecked);
        }

        [Test]
        public void Representative_145()
        {
            // 대표 최적화 = 145(관통1+분열1+폭발2). 병목 없음 → expected = actual = 145, 배율 1.45(<1.6).
            LogisticsResult r = NoBottleneck(145f);
            Assert.AreEqual(145f, r.expected, Delta);
            Assert.AreEqual(145f, r.actual, Delta);
            Assert.AreEqual(1.45f, r.multiple, Delta);
        }

        /// <summary>
        /// 천장을 넘어도 시뮬은 막지 않는다(2026-08-21 개정). 145는 S3 돌파 실측선이지 상한이 아니고,
        /// 물류 단독 상한 160의 담보는 **보드 물리 상한**(그리드·라인 수)이 진다 — 규칙이 아니라 물리다.
        /// 탄종 조합의 이론 최대치는 160을 넘을 수 있다(폭발만 상한까지 채우면 50×6=300).
        /// </summary>
        [Test]
        public void AboveCeiling_IsNotClampedNorFlagged()
        {
            LogisticsResult r = NoBottleneck(200f);
            Assert.AreEqual(200f, r.expected, Delta, "클램프 없음 — expected 그대로");
            Assert.AreEqual(200f, r.actual, Delta, "병목이 없으면 actual도 그대로");
            Assert.AreEqual(2.0f, r.multiple, Delta, "배율은 표시용으로 계산만 한다");
            Assert.IsFalse(r.bottlenecked, "천장 초과는 병목이 아니다");
        }

        // --- 병목: actual < expected, gap 발생 ---

        [Test]
        public void PowerStarved_HalvesActual()
        {
            // 공급 33 / 소비 66 → 효율 0.5.
            LogisticsResult r = LogisticsSimulation.Compute(
                145f, 33f, 66f, 8f, 0f, 12f, 14f, 10f, Origin);
            Assert.AreEqual(0.5f, r.powerEfficiency, Delta);
            Assert.AreEqual(145f, r.expected, Delta);
            Assert.AreEqual(72.5f, r.actual, Delta);
            Assert.AreEqual(72.5f, r.gap, Delta);
            Assert.IsTrue(r.bottlenecked);
        }

        [Test]
        public void Heat_OverThreshold_Throttles()
        {
            // 순발열 24 > 임계 12 → 감쇠 0.5.
            LogisticsResult r = LogisticsSimulation.Compute(
                145f, 80f, 66f, 24f, 0f, 12f, 14f, 10f, Origin);
            Assert.AreEqual(0.5f, r.heatThrottle, Delta);
            Assert.AreEqual(72.5f, r.actual, Delta);
        }

        [Test]
        public void Belt_OverCapacity_Throttles()
        {
            // 필요 28 > 용량 14 → 감쇠 0.5.
            LogisticsResult r = LogisticsSimulation.Compute(
                145f, 80f, 66f, 8f, 0f, 12f, 14f, 28f, Origin);
            Assert.AreEqual(0.5f, r.beltThrottle, Delta);
            Assert.AreEqual(72.5f, r.actual, Delta);
        }

        [Test]
        public void Throttles_Multiply()
        {
            // 전력 0.5 × 발열 0.5 = 0.25 → 145×0.25 = 36.25.
            LogisticsResult r = LogisticsSimulation.Compute(
                145f, 33f, 66f, 24f, 0f, 12f, 14f, 10f, Origin);
            Assert.AreEqual(36.25f, r.actual, Delta);
            Assert.IsTrue(r.bottlenecked);
        }

        [Test]
        public void Gap_Decomposes_ByCause()
        {
            // 전력 0.5 × 발열 0.5 on 145: expected 145, actual 36.25, gap 108.75.
            //   gapPower = 145×(1−0.5) = 72.5 / gapHeat = 145×0.5×(1−0.5) = 36.25 / gapBelt = 0
            // 순차 귀속(telescoping) → gapPower+gapHeat+gapBelt == gap 정확.
            LogisticsResult r = LogisticsSimulation.Compute(
                145f, 33f, 66f, 24f, 0f, 12f, 14f, 10f, Origin);
            Assert.AreEqual(145f, r.expected, Delta);
            Assert.AreEqual(36.25f, r.actual, Delta);
            Assert.AreEqual(108.75f, r.gap, Delta);
            Assert.AreEqual(72.5f, r.gapPower, Delta);
            Assert.AreEqual(36.25f, r.gapHeat, Delta);
            Assert.AreEqual(0f, r.gapBelt, Delta);
            Assert.AreEqual(r.gap, r.gapPower + r.gapHeat + r.gapBelt, Delta, "분해 합 == gap");
        }
    }
}
