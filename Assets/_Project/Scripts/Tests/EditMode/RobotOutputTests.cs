using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 출력 산출의 단일 원천(§5-6 커밋 A). 앵커는 balance_v4.json 확정치(전부 confirmed:true):
    /// pA 1/1/2 · dA 20/25/50 · enh 1.45. 대표 출력 145 = s3Break, 강화 시 210.25(S4 밴드 186~215 안).
    /// </summary>
    public sealed class RobotOutputTests
    {
        private const float Delta = 0.001f;

        /// <summary>대표 상태 무기 3종(관통1×20 · 분열1×25 · 폭발2×50).</summary>
        private static List<WeaponSpec> Representative() => new List<WeaponSpec>
        {
            new WeaponSpec(AmmoKind.Pierce, 20f, 1f),
            new WeaponSpec(AmmoKind.Split, 25f, 1f),
            new WeaponSpec(AmmoKind.Explosive, 50f, 2f),
        };

        [Test]
        public void Nominal_Representative_Equals145()
        {
            // 20 + 25 + 100 = 145 (물류 단위 = 마운트계수 1).
            Assert.AreEqual(145f, RobotOutput.Nominal(Representative(), 1f, 1f), Delta);
        }

        [Test]
        public void Nominal_EnhancedMount_Equals210_25()
        {
            // 강화 ×1.45 → 29 + 36.25 + 145 = 210.25. CLAUDE.md §9 "S4 도달 210.3"과 대조.
            float enhanced = RobotOutput.Nominal(Representative(), 1.45f, 1f);
            Assert.AreEqual(210.25f, enhanced, Delta);
            Assert.That(enhanced, Is.InRange(186f, 215f), "S4 요구 밴드 안이어야 한다");
        }

        [Test]
        public void Nominal_NullWeapons_IsZero()
        {
            Assert.AreEqual(0f, RobotOutput.Nominal(null, 1f, 1f), Delta);
        }

        [Test]
        public void Nominal_UsesDamageFormula_FloorOfOne()
        {
            // 판정식 하한(max(1, …))을 그대로 탄다 — 여기서 다시 계산하지 않는다는 계약.
            var weak = new List<WeaponSpec> { new WeaponSpec(AmmoKind.Pierce, 0f, 3f) };
            Assert.AreEqual(3f, RobotOutput.Nominal(weak, 1f, 1f), Delta);
        }
    }
}
