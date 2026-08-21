using System;
using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 오프라인 정산(스테이지 기획서「오프라인 보상」):
    /// 고철 = 상주 스테이지 최고 파밍 시급 × min(꺼둔 시간, 36h) × 계수.
    ///
    /// ⚠️ 계수·기본 시급은 검증 대장 TBD다. 여기 수치는 계산 엔진을 확인하기 위한 가상값이며
    /// 밸런스 단정이 아니다 — 이 항목이 낀 결과에 "검증 완료"를 붙이지 않는다(§7).
    /// </summary>
    public sealed class OfflineRewardTests
    {
        private const double D = 0.001d;
        private const double CapHours = 36d; // 확정치(밸런스 계약)

        private sealed class FakeClock : IClock
        {
            public DateTimeOffset UtcNow { get; set; }
        }

        private static long TicksAt(int y, int mo, int d, int h, int mi) =>
            new DateTimeOffset(y, mo, d, h, mi, 0, TimeSpan.Zero).UtcTicks;

        // ---- 공식 ----

        [Test]
        public void Compute_MatchesContractFormula()
        {
            // 시급 400 × 8시간 × 0.5 = 1,600
            OfflineRewardResult r = OfflineRewardCalculator.Compute(400d, 8d, 0.5d, CapHours);

            Assert.AreEqual(1600d, r.scrap, D);
            Assert.AreEqual(8d, r.creditedHours, D);
            Assert.IsFalse(r.capped);
        }

        [Test]
        public void Compute_ClampsAt36Hours()
        {
            OfflineRewardResult r = OfflineRewardCalculator.Compute(400d, 100d, 0.5d, CapHours);

            Assert.AreEqual(36d, r.creditedHours, D, "상한 초과분은 인정하지 않는다");
            Assert.AreEqual(400d * 36d * 0.5d, r.scrap, D);
            Assert.IsTrue(r.capped);
        }

        [Test]
        public void Compute_ExactlyAtCap_IsNotFlaggedAsCapped()
        {
            OfflineRewardResult r = OfflineRewardCalculator.Compute(400d, 36d, 0.5d, CapHours);

            Assert.AreEqual(36d, r.creditedHours, D);
            Assert.IsFalse(r.capped, "경계값은 상한 초과가 아니다");
        }

        [Test]
        public void Compute_NegativeElapsed_YieldsZero()
        {
            // 시계가 되감겨도 음수 보상이 나오지 않는다(롤백 '방어'가 아니라 음수 차단).
            OfflineRewardResult r = OfflineRewardCalculator.Compute(400d, -5d, 0.5d, CapHours);

            Assert.AreEqual(0d, r.scrap, D);
            Assert.AreEqual(0d, r.creditedHours, D);
        }

        [Test]
        public void Compute_UnconfiguredNumbers_YieldZero()
        {
            Assert.AreEqual(0d, OfflineRewardCalculator.Compute(0d, 8d, 0.5d, CapHours).scrap, D, "시급 미확정");
            Assert.AreEqual(0d, OfflineRewardCalculator.Compute(400d, 8d, 0d, CapHours).scrap, D, "계수 미확정");
        }

        [Test]
        public void Result_HasNoEnhMaterialField()
        {
            // 강화재료를 담을 자리가 아예 없어야 꺼둔 시간으로 S4 벽을 우회할 수 없다.
            foreach (var f in typeof(OfflineRewardResult).GetFields())
                Assert.IsFalse(f.Name.ToLowerInvariant().Contains("enh"),
                    $"오프라인 결과에 강화재료 필드가 생겼다: {f.Name}");
        }

        // ---- 세이브 연동 ----

        [Test]
        public void FromSave_UsesResidentStageRecord_NotTheHighest()
        {
            // S3 기록이 더 높아도, 끈 시점의 상주 스테이지가 S1이면 S1 기록으로 정산한다.
            var save = new SaveDataV1 { lastFarmStageId = "S1", lastSeenUtcTicks = TicksAt(2026, 8, 21, 0, 0) };
            save.TryRecordFarmRate("S1", 100f);
            save.TryRecordFarmRate("S3", 900f);
            var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 8, 21, 4, 0, 0, TimeSpan.Zero) };

            OfflineRewardResult r = OfflineRewardCalculator.FromSave(save, clock, 0.5d, CapHours, defaultHourlyRate: 0d);

            Assert.AreEqual(100d, r.hourlyRate, D, "여러 기록 중 최댓값을 고르지 않는다");
            Assert.AreEqual(4d, r.creditedHours, D);
            Assert.AreEqual(200d, r.scrap, D);
            Assert.IsFalse(r.usedDefaultRate);
        }

        [Test]
        public void FromSave_NoRecordForStage_FallsBackToDefault()
        {
            // 상위 스테이지로 막 옮긴 직후 = 기록 없음 → 기본 시급(TBD).
            var save = new SaveDataV1 { lastFarmStageId = "S4", lastSeenUtcTicks = TicksAt(2026, 8, 21, 0, 0) };
            save.TryRecordFarmRate("S1", 100f);
            var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 8, 21, 2, 0, 0, TimeSpan.Zero) };

            OfflineRewardResult r = OfflineRewardCalculator.FromSave(save, clock, 0.5d, CapHours, defaultHourlyRate: 50d);

            Assert.IsTrue(r.usedDefaultRate);
            Assert.AreEqual(50d, r.hourlyRate, D);
            Assert.AreEqual(50d, r.scrap, D, "50 × 2시간 × 0.5");
        }

        [Test]
        public void FromSave_FirstRun_NoTimestamp_YieldsZero()
        {
            var save = new SaveDataV1 { lastFarmStageId = "S1" }; // lastSeenUtcTicks = 0
            save.TryRecordFarmRate("S1", 400f);
            var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 8, 21, 2, 0, 0, TimeSpan.Zero) };

            OfflineRewardResult r = OfflineRewardCalculator.FromSave(save, clock, 0.5d, CapHours, 0d);

            Assert.AreEqual(0d, r.scrap, D, "첫 실행은 꺼둔 시간이 없다");
        }

        [Test]
        public void FromSave_LongAbsence_IsCappedAt36Hours()
        {
            var save = new SaveDataV1 { lastFarmStageId = "S1", lastSeenUtcTicks = TicksAt(2026, 8, 1, 0, 0) };
            save.TryRecordFarmRate("S1", 400f);
            var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero) }; // 20일

            OfflineRewardResult r = OfflineRewardCalculator.FromSave(save, clock, 0.5d, CapHours, 0d);

            Assert.IsTrue(r.capped);
            Assert.AreEqual(36d, r.creditedHours, D);
        }

        [Test]
        public void ElapsedHours_MeasuresFromLastSeen()
        {
            var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 8, 21, 6, 30, 0, TimeSpan.Zero) };
            double h = OfflineRewardCalculator.ElapsedHours(TicksAt(2026, 8, 21, 0, 0), clock);

            Assert.AreEqual(6.5d, h, D);
        }
    }
}
