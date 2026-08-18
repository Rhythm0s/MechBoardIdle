using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 고효율 우선 사격 배분(§9 spectrum, capA=6). 입력 발사율 = 물류 생산율 pA.
    /// - 대표 상태(pA 1/1/2, 합 4 ≤ cap6): 전량 사격 → 폭발×2+분열×1+관통×1 = 145.
    /// - 생산 초과(5/4/2, 합 11 > cap6): 고효율 우선 캡 → 폭발×2+분열×4 = 200(관통 0).
    /// 순수 로직.
    /// </summary>
    public sealed class ShotAllocatorTests
    {
        // 대표 상태 물류 생산율(mock): 관통1 / 분열1 / 폭발2.
        private static List<WeaponSpec> Representative() => new List<WeaponSpec>
        {
            new WeaponSpec(AmmoKind.Pierce, 20f, 1f),
            new WeaponSpec(AmmoKind.Split, 25f, 1f),
            new WeaponSpec(AmmoKind.Explosive, 50f, 2f),
        };

        // 생산이 소비 상한을 초과하는 가상 상태: 5/4/2(합 11).
        private static List<WeaponSpec> OverCap() => new List<WeaponSpec>
        {
            new WeaponSpec(AmmoKind.Pierce, 20f, 5f),
            new WeaponSpec(AmmoKind.Split, 25f, 4f),
            new WeaponSpec(AmmoKind.Explosive, 50f, 2f),
        };

        private readonly List<AmmoLine> _buf = new List<AmmoLine>();

        private float RateOf(AmmoKind kind)
        {
            float r = 0f;
            foreach (AmmoLine l in _buf) if (l.kind == kind) r += l.shotsPerSec;
            return r;
        }

        /// <summary>라인 기준 출력 = Σ(초당 발사수 × 발당피해). 정수 발수가 아니라 발사율로 센다.</summary>
        private float Output()
        {
            float total = 0f;
            foreach (AmmoLine l in _buf) total += l.shotsPerSec * l.damagePerShot;
            return total;
        }

        [Test]
        public void Representative_UnderCap_FiresAll_Total145()
        {
            ShotAllocator.AllocateRates(Representative(), 6f, 1f, _buf);

            Assert.AreEqual(3, _buf.Count, "탄종 3라인");
            Assert.AreEqual(2f, RateOf(AmmoKind.Explosive), 0.001f, "폭발 2발/초");
            Assert.AreEqual(1f, RateOf(AmmoKind.Split), 0.001f, "분열 1발/초");
            Assert.AreEqual(1f, RateOf(AmmoKind.Pierce), 0.001f, "관통 1발/초");
            Assert.AreEqual(145f, Output(), 0.001f, "폭발50×2 + 분열25 + 관통20 = 145 = s3Break");
        }

        [Test]
        public void OverCap_HighEfficiencyFirst_Total200()
        {
            ShotAllocator.AllocateRates(OverCap(), 6f, 1f, _buf);

            Assert.AreEqual(2f, RateOf(AmmoKind.Explosive), 0.001f, "폭발 2(고효율 우선)");
            Assert.AreEqual(4f, RateOf(AmmoKind.Split), 0.001f, "분열 4(잔여)");
            Assert.AreEqual(0f, RateOf(AmmoKind.Pierce), 0.001f, "관통 0(상한 소진)");
            Assert.AreEqual(200f, Output(), 0.001f, "폭발50×2 + 분열25×4 = 200");
        }

        [Test]
        public void ZeroCap_YieldsNoLines()
        {
            ShotAllocator.AllocateRates(Representative(), 0f, 1f, _buf);
            Assert.AreEqual(0, _buf.Count);
        }

        /// <summary>
        /// 절반 공급에서 관통·분열이 사라지지 않는다. 정수 반올림 경로였다면
        /// RoundToInt(0.5f)=0(half-to-even)이라 폭발만 남아 50이 됐다.
        /// </summary>
        [Test]
        public void Scale_Half_HalvesRates_NoQuantizationLoss()
        {
            ShotAllocator.AllocateRates(Representative(), 6f, 0.5f, _buf);

            Assert.AreEqual(3, _buf.Count, "라인이 사라지면 안 된다");
            Assert.AreEqual(1f, RateOf(AmmoKind.Explosive), 0.001f);
            Assert.AreEqual(0.5f, RateOf(AmmoKind.Split), 0.001f);
            Assert.AreEqual(0.5f, RateOf(AmmoKind.Pierce), 0.001f);
            Assert.AreEqual(72.5f, Output(), 0.001f, "출력도 정확히 절반");
        }

        [Test]
        public void Scale_Zero_YieldsNoFire()
        {
            ShotAllocator.AllocateRates(Representative(), 6f, 0f, _buf);
            Assert.AreEqual(0, _buf.Count);
        }

        [Test]
        public void Scale_PreservesMixRatio_WhenCapNotBinding()
        {
            ShotAllocator.AllocateRates(Representative(), 6f, 0.25f, _buf);

            // 상한이 안 걸리면 배합비(1:1:2)는 그대로여야 한다.
            Assert.AreEqual(RateOf(AmmoKind.Pierce), RateOf(AmmoKind.Split), 0.001f);
            Assert.AreEqual(2f * RateOf(AmmoKind.Pierce), RateOf(AmmoKind.Explosive), 0.001f);
            Assert.AreEqual(36.25f, Output(), 0.001f, "145 × 0.25");
        }

        [Test]
        public void RoundRobin_OneEach_OrderedSingleMultishotAoe()
        {
            // 입력 순서를 뒤집어도 관통→분열→폭발로 정렬되어야 함.
            var reversed = new List<WeaponSpec>
            {
                new WeaponSpec(AmmoKind.Explosive, 50f, 2f),
                new WeaponSpec(AmmoKind.Split, 25f, 1f),
                new WeaponSpec(AmmoKind.Pierce, 20f, 1f),
            };
            List<AllocatedShot> shots = ShotAllocator.RoundRobin(reversed);

            Assert.AreEqual(3, shots.Count, "무기당 1발");
            Assert.AreEqual(AmmoKind.Pierce, shots[0].kind, "1) 싱글샷");
            Assert.AreEqual(AmmoKind.Split, shots[1].kind, "2) 멀티샷");
            Assert.AreEqual(AmmoKind.Explosive, shots[2].kind, "3) AoE");
        }
    }
}
