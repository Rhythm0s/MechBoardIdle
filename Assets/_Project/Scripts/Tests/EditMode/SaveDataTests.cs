using System;
using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 세이브 스키마 v1(§5-7) — JsonUtility 왕복과 기록 규칙.
    /// 규칙 원천 = 스테이지 기획서「오프라인 보상」: 스테이지별 보관 · 더 높을 때만 교체 ·
    /// 최초 클리어 1회만 강화재료.
    /// </summary>
    public sealed class SaveDataTests
    {
        [Test]
        public void RoundTripsThroughJsonUtility()
        {
            var src = new SaveDataV1
            {
                lastSeenUtcTicks = new DateTimeOffset(2026, 8, 19, 3, 0, 0, TimeSpan.Zero).Ticks,
                scrap = 1234.5,
                enhMaterial = 30,
                lastFarmStageId = "S3",
                totalKills = 77,
            };
            src.TryRecordFarmRate("S1", 120f);
            src.TryRecordFarmRate("S3", 480f);
            src.MarkCleared("S1");

            SaveDataV1 back = JsonUtility.FromJson<SaveDataV1>(JsonUtility.ToJson(src));

            Assert.AreEqual(SaveDataV1.CurrentVersion, back.schemaVersion);
            Assert.AreEqual(src.lastSeenUtcTicks, back.lastSeenUtcTicks);
            Assert.AreEqual(1234.5, back.scrap, 0.001);
            Assert.AreEqual("S3", back.lastFarmStageId);
            Assert.AreEqual(77, back.totalKills);
            Assert.AreEqual(120f, back.BestFarmRate("S1"), 0.001f, "스테이지별 기록이 왕복해야 한다");
            Assert.AreEqual(480f, back.BestFarmRate("S3"), 0.001f);
            Assert.IsTrue(back.HasCleared("S1"));
            Assert.IsFalse(back.HasCleared("S2"));
        }

        [Test]
        public void FarmRate_KeepsPerStage_DoesNotMix()
        {
            var d = new SaveDataV1();
            d.TryRecordFarmRate("S1", 100f);
            d.TryRecordFarmRate("S3", 500f);

            Assert.AreEqual(100f, d.BestFarmRate("S1"), 0.001f, "S1과 S3 기록은 섞이지 않는다");
            Assert.AreEqual(500f, d.BestFarmRate("S3"), 0.001f);
        }

        [Test]
        public void FarmRate_ReplacesOnlyWhenHigher()
        {
            var d = new SaveDataV1();
            Assert.IsTrue(d.TryRecordFarmRate("S1", 100f), "첫 기록");
            Assert.IsFalse(d.TryRecordFarmRate("S1", 80f), "더 낮으면 교체 안 함");
            Assert.AreEqual(100f, d.BestFarmRate("S1"), 0.001f);
            Assert.IsTrue(d.TryRecordFarmRate("S1", 150f), "더 높으면 교체");
            Assert.AreEqual(150f, d.BestFarmRate("S1"), 0.001f);
        }

        [Test]
        public void FarmRate_UnknownStage_IsZero()
        {
            // 기록 없음 = 0. 호출자가 오프라인 기본 시급으로 대체한다(값은 TBD).
            Assert.AreEqual(0f, new SaveDataV1().BestFarmRate("S5"), 0.001f);
        }

        [Test]
        public void MarkCleared_OnlyFirstTimeIsTrue()
        {
            // 재클리어 재지급을 허용하면 Σ(S1..S3)=s4Cost인 닫힌 곡선이 즉시 무너진다.
            var d = new SaveDataV1();
            Assert.IsTrue(d.MarkCleared("S1"), "최초 클리어");
            Assert.IsFalse(d.MarkCleared("S1"), "두 번째부터는 미지급");
        }

        [Test]
        public void SystemClock_ReturnsUtc()
        {
            Assert.AreEqual(TimeSpan.Zero, new SystemClock().UtcNow.Offset, "UTC 기준이어야 기기 표준시에 흔들리지 않는다");
        }
    }
}
