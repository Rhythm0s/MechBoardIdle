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

        [Test]
        public void Representative_UnderCap_FiresAll_Total145()
        {
            List<AllocatedShot> shots = ShotAllocator.AllocatePerSecond(Representative(), 6f);

            Assert.AreEqual(4, shots.Count, "생산 합 4발 전량(상한 미만)");
            Assert.AreEqual(2, shots.FindAll(s => s.kind == AmmoKind.Explosive).Count, "폭발 2");
            Assert.AreEqual(1, shots.FindAll(s => s.kind == AmmoKind.Split).Count, "분열 1");
            Assert.AreEqual(1, shots.FindAll(s => s.kind == AmmoKind.Pierce).Count, "관통 1");

            float total = 0f;
            foreach (AllocatedShot s in shots) total += s.damagePerShot;
            Assert.AreEqual(145f, total, 0.001f, "폭발50×2 + 분열25 + 관통20 = 145 = s3Break");
        }

        [Test]
        public void OverCap_HighEfficiencyFirst_Total200()
        {
            List<AllocatedShot> shots = ShotAllocator.AllocatePerSecond(OverCap(), 6f);

            Assert.AreEqual(6, shots.Count, "상한 6발");
            Assert.AreEqual(2, shots.FindAll(s => s.kind == AmmoKind.Explosive).Count, "폭발 2(고효율 우선)");
            Assert.AreEqual(4, shots.FindAll(s => s.kind == AmmoKind.Split).Count, "분열 4(잔여)");
            Assert.AreEqual(0, shots.FindAll(s => s.kind == AmmoKind.Pierce).Count, "관통 0(상한 소진)");

            float total = 0f;
            foreach (AllocatedShot s in shots) total += s.damagePerShot;
            Assert.AreEqual(200f, total, 0.001f, "폭발50×2 + 분열25×4 = 200");
        }

        [Test]
        public void ZeroCap_YieldsNoShots()
        {
            Assert.AreEqual(0, ShotAllocator.AllocatePerSecond(Representative(), 0f).Count);
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
