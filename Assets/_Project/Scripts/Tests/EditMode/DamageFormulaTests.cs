using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 판정식(07 1장): 실피해 = max(1, 발당피해 × 마운트계수 × 모듈배율 − 방어). 난수 0.
    /// 경계: 방어 히트당 뺄셈 / 하한 1 클램프 / 강화·모듈 곱. 순수 로직(자산 불필요).
    /// </summary>
    public sealed class DamageFormulaTests
    {
        private const float Delta = 0.001f;

        [Test]
        public void PierceBase_MinusDef1_Is19()
        {
            // S1 보병 def1 vs 관통 20, 물류 상태(마운트 1.0·모듈 1.0).
            Assert.AreEqual(19f, DamageFormula.PerHit(20f, 1f, 1f, 1f), Delta);
        }

        [Test]
        public void EnhancedPierce_MinusDef6_Is23()
        {
            // 강화 마운트 1.45: 20×1.45×1.0 = 29, − def6 = 23.
            Assert.AreEqual(23f, DamageFormula.PerHit(20f, 1.45f, 1f, 6f), Delta);
        }

        [Test]
        public void ExplosiveBase_MinusBossDef12_Is38()
        {
            // S6 보스 def12 vs 폭발 50: 50 − 12 = 38.
            Assert.AreEqual(38f, DamageFormula.PerHit(50f, 1f, 1f, 12f), Delta);
        }

        [Test]
        public void HighDef_ClampsToOne()
        {
            // S5 장갑 def45 vs 관통 20(물류): 20 − 45 = −25 → 하한 1.
            Assert.AreEqual(1f, DamageFormula.PerHit(20f, 1f, 1f, 45f), Delta);
        }

        [Test]
        public void ModuleMult_Scales()
        {
            // 모듈배율 2.0(가상): 20×1×2 = 40, def0.
            Assert.AreEqual(40f, DamageFormula.PerHit(20f, 1f, 2f, 0f), Delta);
        }
    }
}
