using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 롤링 창(§5-6 커밋 B) — "움직이는 거울"의 평균·만료·솎기, 그리고 갭 분해 보존.
    /// 핵심은 마지막 테스트다: 예전 코드는 '즉시 expected − 롤링 actual'을 섞어
    /// 변수패널의 분해 합이 총갭과 어긋났다. 같은 창으로 굴리면 선형성으로 정확히 맞는다.
    /// </summary>
    public sealed class RollingWindowTests
    {
        private const float Delta = 0.001f;

        [Test]
        public void Average_OverWindow()
        {
            var w = new RollingWindow(1, 60f, 0.1f);
            w.TrySample(0f, new[] { 100f });
            w.TrySample(1f, new[] { 200f });

            Assert.AreEqual(2, w.SampleCount);
            Assert.AreEqual(150f, w.Average(0), Delta);
        }

        [Test]
        public void Expires_OldSamples()
        {
            var w = new RollingWindow(1, 60f, 0.1f);
            w.TrySample(0f, new[] { 100f });
            w.TrySample(100f, new[] { 200f }); // 창(60초) 밖으로 밀려남

            Assert.AreEqual(1, w.SampleCount);
            Assert.AreEqual(200f, w.Average(0), Delta);
        }

        [Test]
        public void SampleInterval_ThinsBurst()
        {
            var w = new RollingWindow(1, 60f, 0.1f);
            w.TrySample(0f, new[] { 100f });
            bool taken = w.TrySample(0.01f, new[] { 999f }); // 간격 미달 → 버림

            Assert.IsFalse(taken);
            Assert.AreEqual(1, w.SampleCount);
            Assert.AreEqual(100f, w.Average(0), Delta, "솎인 샘플이 평균을 오염시키면 안 된다");
        }

        [Test]
        public void MismatchedChannelCount_Rejected()
        {
            var w = new RollingWindow(3, 60f, 0f);
            Assert.IsFalse(w.TrySample(0f, new[] { 1f, 2f }));
            Assert.AreEqual(0, w.SampleCount);
        }

        [Test]
        public void Empty_AverageIsZero()
        {
            var w = new RollingWindow(2, 60f, 0.1f);
            Assert.AreEqual(0f, w.Average(0), Delta);
            Assert.AreEqual(0f, w.Average(1), Delta);
        }

        /// <summary>
        /// 롤링 후에도 gapPower+gapHeat+gapBelt == expected−actual.
        /// 병목이 시간에 따라 변하는(전력만 → 전력+발열 → 정상) 구간을 섞어 넣는다.
        /// </summary>
        [Test]
        public void Linearity_GapDecompositionHoldsAfterRolling()
        {
            var w = new RollingWindow(5, 60f, 0f); // 솎기 없이 전부 담아 검증

            // 배율과 도착량을 함께 준다(2026-09-05 개정). 앞 셋은 「만든 만큼 다 닿았다」이고
            // 넷째만 운송에서 절반을 잃는다 — 갭의 출처가 프레임마다 다른 구간을 섞는 것이 요점이다.
            LogisticsResult[] frames =
            {
                Frame(1f,   1f,   145f),    // 병목 없음
                Frame(0.5f, 1f,   72.5f),   // 전력 0.5 → 절반만 만든다
                Frame(0.5f, 0.5f, 36.25f),  // 전력·발열
                Frame(1f,   1f,   72.5f),   // 생산은 멀쩡한데 운송에서 절반을 잃었다
            };

            for (int i = 0; i < frames.Length; i++)
            {
                LogisticsResult r = frames[i];
                w.TrySample(i, new[] { r.expected, r.actual, r.gapPower, r.gapHeat, r.gapBelt });
            }

            float expected = w.Average(0), actual = w.Average(1);
            float decomposed = w.Average(2) + w.Average(3) + w.Average(4);

            Assert.AreEqual(expected - actual, decomposed, Delta, "분해 합 == 총갭이 롤링 후에도 성립해야 한다");
            Assert.Greater(expected - actual, 0f, "잃은 프레임이 섞여 있으므로 총갭은 양수다");
        }

        /// <summary>배율과 관측 도착으로 한 프레임을 만든다.</summary>
        private static LogisticsResult Frame(float power, float heat, float observed) =>
            LogisticsSimulation.Compute(145f, new ProductionThrottle(power, heat), observed, 100f);
    }
}
