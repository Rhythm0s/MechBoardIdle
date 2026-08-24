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

        // ---- V05 §1 앵커 재현: 원점 100 · 돌파 145 · 라인 단절 80 ----

        /// <summary>원점 100 = 관통 라인 하나(20 × 5발/초). 검증 시나리오 ①의 "관통탄 20×5발 기본 라인".</summary>
        [Test]
        public void Origin_PierceOnly_FiveShots_Equals100()
        {
            var pierceLine = new List<WeaponSpec> { new WeaponSpec(AmmoKind.Pierce, 20f, 5f) };
            Assert.AreEqual(100f, RobotOutput.Nominal(pierceLine, 1f, 1f), Delta);
        }

        /// <summary>
        /// V05 §1-3 검산: 관통 5발 라인에서 하나가 끊겨 4발이 되면 **80**이다. 0이 아니다.
        /// 부분 출력이 계수 보정 없이 생산량만으로 나온다는 확인 지점.
        /// </summary>
        [Test]
        public void BrokenLine_FourShots_Equals80_NotZero()
        {
            var reduced = new List<WeaponSpec> { new WeaponSpec(AmmoKind.Pierce, 20f, 4f) };
            float output = RobotOutput.Nominal(reduced, 1f, 1f);

            Assert.AreEqual(80f, output, Delta, "20 × 4발 = 80");
            Assert.Greater(output, 0f, "라인이 끊겨도 0으로 접히지 않는다");
        }

        /// <summary>
        /// 밸런스 문서「예상 전투력 공식」의 원식과 동치 확인:
        /// 예상 = 발당 가중평균 피해 × 총 생산량. 대표 상태 = 36.25 × 4 = 145.
        /// </summary>
        [Test]
        public void WeightedAverage_TimesTotalRate_MatchesNominal()
        {
            List<WeaponSpec> w = Representative();

            float totalRate = 0f, totalDamage = 0f;
            foreach (WeaponSpec s in w)
            {
                totalRate += s.shotsPerSec;
                totalDamage += s.shotsPerSec * s.damagePerShot;
            }
            float weightedAvg = totalDamage / totalRate;

            Assert.AreEqual(4f, totalRate, Delta, "관통1 + 분열1 + 폭발2 = 4발/초");
            Assert.AreEqual(36.25f, weightedAvg, Delta, "(20 + 25 + 100) ÷ 4");
            Assert.AreEqual(RobotOutput.Nominal(w, 1f, 1f), weightedAvg * totalRate, Delta,
                "Σ(발사수 × 피해) 와 가중평균 × 총량은 같은 식이다");
        }

        /// <summary>
        /// 총 생산량이 5발에서 4발로 **줄었는데** 출력은 100 → 145로 올랐다.
        /// 성장 축이 총량과 조합 둘이라는 것 — 조합으로 오른 값은 보너스가 아니라 다른 물건을 만든 결과다.
        /// </summary>
        [Test]
        public void FewerShots_CanYieldHigherOutput_ViaMix()
        {
            float origin = RobotOutput.Nominal(
                new List<WeaponSpec> { new WeaponSpec(AmmoKind.Pierce, 20f, 5f) }, 1f, 1f);
            float breakthrough = RobotOutput.Nominal(Representative(), 1f, 1f);

            Assert.AreEqual(100f, origin, Delta);
            Assert.AreEqual(145f, breakthrough, Delta);
            Assert.Greater(breakthrough, origin, "생산량은 5→4로 줄고 출력은 올랐다");
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
