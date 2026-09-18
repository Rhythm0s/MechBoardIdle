using MBI.Core.Combat;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **한 발을 세 발로 · 대미지 1/2** (2026-09-18 사용자 확정).
    ///
    /// 여기서 지키는 것 둘 —
    /// ① **소비는 안 바뀐다**(한 발은 한 발이다 · 물류가 흔들리지 않는다)
    /// ② **총량은 1.5 배**이고 그 수는 **정한 것이 아니라 따라 나온 것**이다.
    /// </summary>
    public sealed class ShotSplitTests
    {
        private const float D = 0.0001f;

        [Test]
        public void 한_발이_세_발로_나간다()
        {
            Assert.AreEqual(3, ShotSplitRule.ShotsPerRound(3));
        }

        [Test]
        public void 한_발의_피해는_절반이다()
        {
            Assert.AreEqual(0.5f, ShotSplitRule.DamageFactor(0.5f), D);
        }

        [Test]
        public void 총량은_한_발_반이다()
        {
            // 📌 3 × 1/2 = 1.5. 「세 발」과 「절반」은 각각 사용자 값이고 이 곱은 따라 나온다.
            Assert.AreEqual(1.5f, ShotSplitRule.ThroughputRatio(3, 0.5f), D);
        }

        [Test]
        public void 안_정하면_구_거동이다()
        {
            // ⚠️ 「안 정했다」를 「0 발」이나 「피해 0」으로 읽지 않는다.
            Assert.AreEqual(1, ShotSplitRule.ShotsPerRound(0), "0 발을 쏘면 탄만 사라진다");
            Assert.AreEqual(1, ShotSplitRule.ShotsPerRound(-5));
            Assert.AreEqual(1f, ShotSplitRule.DamageFactor(0f), D);
            Assert.AreEqual(1f, ShotSplitRule.DamageFactor(-1f), D);

            Assert.AreEqual(1f, ShotSplitRule.ThroughputRatio(0, 0f), D, "둘 다 안 정하면 그대로다");
        }

        [Test]
        public void 값이_기본으로_사용자_확정에_서_있다()
        {
            // ⚠️ 이 둘은 TBD 가 아니라 **사용자가 준 수**다.
            Assert.AreEqual(3, ShotSplitRule.DefaultShotsPerRound);
            Assert.AreEqual(0.5f, ShotSplitRule.DefaultDamageFactor, D);
        }
    }
}
