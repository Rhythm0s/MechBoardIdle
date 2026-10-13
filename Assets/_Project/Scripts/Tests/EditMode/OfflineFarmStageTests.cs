using System;
using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **끈 시점의 상주 파밍 판으로 정산한다** (2026-10-13 · 순서 6).
    ///
    /// ⚠️⚠️ **여기서 막는 결함은 「아무도 안 썼다」다.** 저장에 `lastFarmStageId` 칸은
    /// 처음부터 있었는데 **쓰는 쪽이 없었다** — 늘 빈 글자라 `BestFarmRate("")` 가 0 을 주고
    /// 정산이 매번 **기본 시급**으로 떨어졌다. 기록을 쌓아도 쓰이지 않는 상태였다.
    ///
    /// 쓰는 자리는 둘이다 —
    /// 전투가 <see cref="StageLayerSignals.FarmStageId"/> 에 적고(바퀴가 실제로 선 뒤),
    /// 방치 런타임이 저장할 때 그것을 `lastFarmStageId` 로 옮긴다.
    /// </summary>
    public sealed class OfflineFarmStageTests
    {
        private sealed class FakeClock : IClock
        {
            public DateTimeOffset UtcNow { get; set; }
        }

        private static readonly DateTimeOffset Now =
            new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

        /// <summary>두 시간 전에 껐다.</summary>
        private static SaveDataV1 TwoHoursAgo()
        {
            return new SaveDataV1
            {
                lastSeenUtcTicks = Now.AddHours(-2).UtcTicks,
            };
        }

        [Test]
        public void 상주_판_이름이_비면_기록이_안_쓰인다()
        {
            // ⚠️ 고친 결함의 **그림**이다 — 기록은 있는데 가리키는 판이 없다.
            SaveDataV1 save = TwoHoursAgo();
            save.TryRecordFarmRate("S2", 500f);
            save.lastFarmStageId = string.Empty;

            OfflineRewardResult r = OfflineRewardCalculator.FromSave(
                save, new FakeClock { UtcNow = Now }, coef: 1d, capHours: 36d,
                defaultHourlyRate: 7d);

            Assert.IsTrue(r.usedDefaultRate, "가리키는 판이 없으면 기본 시급으로 떨어진다");
            Assert.AreEqual(7d, r.hourlyRate, 1e-9);
        }

        [Test]
        public void 상주_판을_적어_두면_그_판의_기록으로_정산한다()
        {
            SaveDataV1 save = TwoHoursAgo();
            save.TryRecordFarmRate("S1", 100f);
            save.TryRecordFarmRate("S2", 500f);
            save.lastFarmStageId = "S2";          // 끈 시점에 S2 에 상주했다

            OfflineRewardResult r = OfflineRewardCalculator.FromSave(
                save, new FakeClock { UtcNow = Now }, coef: 1d, capHours: 36d,
                defaultHourlyRate: 7d);

            Assert.IsFalse(r.usedDefaultRate);
            Assert.AreEqual(500d, r.hourlyRate, 1e-9);
            Assert.AreEqual(2d, r.creditedHours, 1e-9);
            Assert.AreEqual(1000d, r.scrap, 1e-6, "500 시급 × 2시간 × 계수 1");
        }

        [Test]
        public void 최댓값을_고르지_않는다()
        {
            // 📌 확정 규칙 — **상주했던 그 한 곳**이다. 켜두고 벌 양과 꺼두고 받을 양이
            //    같은 곳을 가리켜야 「어디에 머물까」 판단이 온·오프라인에서 갈리지 않는다.
            SaveDataV1 save = TwoHoursAgo();
            save.TryRecordFarmRate("S1", 100f);
            save.TryRecordFarmRate("S5", 9000f);
            save.lastFarmStageId = "S1";

            OfflineRewardResult r = OfflineRewardCalculator.FromSave(
                save, new FakeClock { UtcNow = Now }, coef: 1d, capHours: 36d,
                defaultHourlyRate: 7d);

            Assert.AreEqual(100d, r.hourlyRate, 1e-9, "S5 의 9000 을 고르지 않는다");
        }

        [Test]
        public void 층_신호는_처음에_비어_있다()
        {
            // ⚠️ 비어 있는 것이 기본이어야 한다 — 저장 계층이 없는 씬에서 **지어낸 판**으로
            //    정산하지 않게. 그래서 방치 런타임은 **빈 글자로는 덮지 않는다.**
            StageLayerSignals.Reset();
            Assert.AreEqual(string.Empty, StageLayerSignals.FarmStageId);
        }

        [Test]
        public void 도전에_들어가도_상주_판은_그대로다()
        {
            // 도전은 잠깐 다녀오는 곳이다 — 끈 시점의 상주 판은 바뀌지 않는다.
            StageLayerSignals.Reset();
            StageLayerSignals.FarmStageId = "S3";
            StageLayerSignals.Current = StageLayer.Challenge;

            Assert.AreEqual("S3", StageLayerSignals.FarmStageId);

            StageLayerSignals.Reset();   // 뒤 시험에 새지 않게
        }

        [Test]
        public void 기록은_더_좋은_바퀴만_갈아치운다()
        {
            SaveDataV1 save = TwoHoursAgo();
            Assert.IsTrue(save.TryRecordFarmRate("S2", 300f));
            Assert.IsFalse(save.TryRecordFarmRate("S2", 200f), "낮은 바퀴는 기록을 못 덮는다");
            Assert.IsTrue(save.TryRecordFarmRate("S2", 400f));
            Assert.AreEqual(400f, save.BestFarmRate("S2"));
        }
    }
}
