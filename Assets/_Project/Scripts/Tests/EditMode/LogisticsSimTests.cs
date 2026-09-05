using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 물류 흐름 결과 조립(§5-5, L4-R) — 이중값(expected/actual/gap)·앵커 순서(100→145→배율초과)·
    /// 갭 분해·병목 배율. 순수 로직(자산 불필요).
    ///
    /// ⚠️ **2026-09-05에 축이 바뀌었다** (`260904_W04` 2-1 4번). `actual`이 계산값에서
    /// **관측치**가 됐다 — 종전에는 `명목 × 전력 × 발열 × 벨트`였고 이제는 마운트를 통과한 것을
    /// 세어 넣는다.
    ///
    /// **값 앵커는 하나도 안 바뀌었다.** 100 · 145 · 72.5 · 36.25 · 108.75가 그대로다.
    /// 바뀐 것은 그 수가 어디서 오는가뿐이라, 여기서는 「전력이 절반이면 도착도 절반」처럼
    /// 관측치를 넣어 같은 수를 만든다. 확정치를 재산출한 것이 아니다(`260904_W04` 4장).
    /// </summary>
    public sealed class LogisticsSimTests
    {
        private const float Delta = 0.001f;
        private const float Origin = 100f;

        /// <summary>배율과 관측 도착을 직접 주는 조립.</summary>
        private static LogisticsResult Assemble(float baseOutput, float power, float heat, float observed) =>
            LogisticsSimulation.Compute(baseOutput, new ProductionThrottle(power, heat), observed, Origin);

        /// <summary>
        /// 병목 없음 기준. 배율이 1이고 명목만큼 도착했다 —
        /// 라인이 완벽하면 만든 것이 다 닿는다는 뜻이다.
        /// </summary>
        private static LogisticsResult NoBottleneck(float baseOutput) =>
            Assemble(baseOutput, 1f, 1f, baseOutput);

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
            // 대표 최적화 = 145(관통1+분열1+폭발2). 병목 없음 → expected = actual = 145, 배율 1.45.
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

        // --- 배율은 생산에 걸린다 (Throttles) ---

        /// <summary>공급 33 / 소비 66 → 효율 0.5. 전력은 고정비 전용이라 강화로 못 넘긴다.</summary>
        [Test]
        public void Throttles_Power_IsSupplyOverDraw()
        {
            ProductionThrottle t = LogisticsSimulation.Throttles(33f, 66f, 8f, 0f, 12f);
            Assert.AreEqual(0.5f, t.power, Delta);
            Assert.AreEqual(1f, t.heat, Delta, "순발열 8 < 임계 12 → 감쇠 없음");
            Assert.AreEqual(0.5f, t.Scale, Delta, "노드에 걸리는 배율");
        }

        /// <summary>순발열 24 > 임계 12 → 감쇠 0.5.</summary>
        [Test]
        public void Throttles_Heat_OverThreshold()
        {
            ProductionThrottle t = LogisticsSimulation.Throttles(80f, 66f, 24f, 0f, 12f);
            Assert.AreEqual(1f, t.power, Delta);
            Assert.AreEqual(0.5f, t.heat, Delta);
        }

        /// <summary>전력 0.5 × 발열 0.5 = 0.25. 노드는 이 배율만큼만 만든다.</summary>
        [Test]
        public void Throttles_Multiply_IntoOneScale()
        {
            ProductionThrottle t = LogisticsSimulation.Throttles(33f, 66f, 24f, 0f, 12f);
            Assert.AreEqual(0.25f, t.Scale, Delta);
        }

        /// <summary>수요가 0이면 나눌 것이 없다 — 아무도 안 먹으면 부족도 없다.</summary>
        [Test]
        public void Throttles_NoDraw_IsFullPower()
        {
            Assert.AreEqual(1f, LogisticsSimulation.Throttles(0f, 0f, 0f, 0f, 12f).power, Delta);
        }

        // --- 병목: actual < expected, gap 발생 ---

        /// <summary>
        /// 전력이 절반이면 노드가 절반만 만들고, 절반만 도착한다 → actual 72.5.
        ///
        /// ⚠️ **전력을 두 번 곱하지 않는다.** 도착량이 이미 배율을 반영하므로 조립에서 또
        /// 곱하면 145 × 0.5 × 0.5 = 36.25가 된다. 그 제곱이 이번 개정의 가장 큰 위험이라
        /// 여기서 못 박는다.
        /// </summary>
        [Test]
        public void PowerStarved_HalvesActual_ButIsNotSquared()
        {
            LogisticsResult r = Assemble(145f, power: 0.5f, heat: 1f, observed: 72.5f);

            Assert.AreEqual(0.5f, r.powerEfficiency, Delta);
            Assert.AreEqual(145f, r.expected, Delta);
            Assert.AreEqual(72.5f, r.actual, Delta, "절반이지 4분의 1이 아니다");
            Assert.AreEqual(72.5f, r.gap, Delta);
            Assert.AreEqual(1f, r.beltThrottle, Delta, "운송에서는 안 잃었다");
            Assert.IsTrue(r.bottlenecked);
        }

        /// <summary>발열이 절반이면 마찬가지로 절반만 도착한다.</summary>
        [Test]
        public void Heat_OverThreshold_HalvesActual()
        {
            LogisticsResult r = Assemble(145f, power: 1f, heat: 0.5f, observed: 72.5f);

            Assert.AreEqual(0.5f, r.heatThrottle, Delta);
            Assert.AreEqual(72.5f, r.actual, Delta);
            Assert.AreEqual(1f, r.beltThrottle, Delta);
        }

        /// <summary>
        /// **운송 손실은 계산하지 않고 나머지로 남는다.** 배율은 멀쩡한데 절반만 닿았으면
        /// 그 절반은 라인이 못 나른 것이다 — 정체·갈래·거리 무엇이든 관측에 이미 들어 있다.
        ///
        /// 종전에는 「필요 28 > 용량 14 → 0.5」라는 근사식이 이 수를 만들었다. 그 식은 라인의
        /// 모양을 안 봐서, 벨트를 어떻게 깔든 노드 수만 같으면 같은 수가 나왔다.
        /// </summary>
        [Test]
        public void BeltLoss_IsTheRemainder_NotAFormula()
        {
            LogisticsResult r = Assemble(145f, power: 1f, heat: 1f, observed: 72.5f);

            Assert.AreEqual(0.5f, r.beltThrottle, Delta, "역산 — 절반만 살아서 왔다");
            Assert.AreEqual(72.5f, r.actual, Delta);
            Assert.AreEqual(72.5f, r.gapBelt, Delta, "잃은 몫이 전부 운송으로 귀속된다");
            Assert.IsTrue(r.bottlenecked);
        }

        /// <summary>
        /// 전력 0.5 × 발열 0.5 on 145: expected 145, actual 36.25, gap 108.75.
        ///   gapPower = 145×(1−0.5) = 72.5 / gapHeat = 145×0.5×(1−0.5) = 36.25 / gapBelt = 0
        /// 셋을 더하면 정확히 gap이다.
        /// </summary>
        [Test]
        public void Gap_Decomposes_ByCause()
        {
            LogisticsResult r = Assemble(145f, power: 0.5f, heat: 0.5f, observed: 36.25f);

            Assert.AreEqual(145f, r.expected, Delta);
            Assert.AreEqual(36.25f, r.actual, Delta);
            Assert.AreEqual(108.75f, r.gap, Delta);
            Assert.AreEqual(72.5f, r.gapPower, Delta);
            Assert.AreEqual(36.25f, r.gapHeat, Delta);
            Assert.AreEqual(0f, r.gapBelt, Delta, "만든 만큼 다 닿았다");
            Assert.AreEqual(r.gap, r.gapPower + r.gapHeat + r.gapBelt, Delta, "분해 합 == gap");
        }

        /// <summary>
        /// 배율이 깎고 **그 위에 운송이 또 깎는** 경우도 분해가 맞아야 한다.
        /// 전력 0.5 → 72.5까지 살고, 그중 40만 닿았다 → 운송 손실 32.5.
        /// </summary>
        [Test]
        public void Gap_Decomposes_WhenBothProductionAndTransportLose()
        {
            LogisticsResult r = Assemble(145f, power: 0.5f, heat: 1f, observed: 40f);

            Assert.AreEqual(105f, r.gap, Delta);
            Assert.AreEqual(72.5f, r.gapPower, Delta);
            Assert.AreEqual(0f, r.gapHeat, Delta);
            Assert.AreEqual(32.5f, r.gapBelt, Delta, "생산에서 살아남은 것 중 못 나른 몫");
            Assert.AreEqual(r.gap, r.gapPower + r.gapHeat + r.gapBelt, Delta, "분해 합 == gap");
        }

        /// <summary>
        /// **쌓였던 것이 빠지면 gapBelt가 음수다.** 버그가 아니라 버퍼가 비워지는 중이라는 뜻이다.
        ///
        /// 0으로 덮지 않는 이유는 분해 합이 총갭과 어긋나기 때문이다 — 덮으면 변수 패널의
        /// 「예상 − 실제 = 전력 + 발열 + 벨트」가 성립하지 않아 패널이 거짓말을 한다.
        /// </summary>
        [Test]
        public void Surplus_IsNotClamped_SoDecompositionStillHolds()
        {
            LogisticsResult r = Assemble(100f, power: 1f, heat: 1f, observed: 120f);

            Assert.AreEqual(-20f, r.gap, Delta, "예상보다 많이 닿았다");
            Assert.AreEqual(-20f, r.gapBelt, Delta);
            Assert.AreEqual(r.gap, r.gapPower + r.gapHeat + r.gapBelt, Delta, "분해 합 == gap");
        }

        /// <summary>명목이 0이면 나눌 것이 없다 — 역산 배율이 폭발하지 않는다.</summary>
        [Test]
        public void ZeroExpected_DoesNotBlowUpTheBeltRatio()
        {
            LogisticsResult r = Assemble(0f, power: 1f, heat: 1f, observed: 0f);

            Assert.AreEqual(0f, r.actual, Delta);
            Assert.AreEqual(1f, r.beltThrottle, Delta, "0 나누기를 하지 않는다");
            Assert.IsFalse(r.bottlenecked);
        }
    }
}
