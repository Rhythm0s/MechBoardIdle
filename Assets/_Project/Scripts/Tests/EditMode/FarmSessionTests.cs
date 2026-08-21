using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 상주 파밍 한 판(§5-7) — 바퀴 시급 산출과 스테이지별 기록 갱신.
    /// 원천 = 스테이지 기획서「파밍 규칙」·「오프라인 보상」.
    /// 수치(정원·간격·마리당고철)는 TBD라 테스트가 정한 가상값이며 밸런스 단정이 아니다.
    /// </summary>
    public sealed class FarmSessionTests
    {
        private const double D = 0.001d;

        // 정원 10 · 간격 20초 · 마리당 2고철 (전부 테스트용 가상값)
        private static FarmSession Session() => new FarmSession(10, 20f, 2d);

        [Test]
        public void Kills_AccrueScrapImmediately_NotAtLapEnd()
        {
            // 고철은 킬 시점에 들어온다. 바퀴 시급은 기록일 뿐 수입이 아니다(이중 적립 방지).
            FarmTickResult r = Session().Tick(1f, 10, killsSinceLastTick: 3);

            Assert.AreEqual(6d, r.scrapEarned, D, "3마리 × 2고철");
            Assert.IsFalse(r.lapClosed);
            Assert.AreEqual(0d, r.lapHourlyRate, D);
        }

        [Test]
        public void LapClose_ComputesHourlyRate_AndResetsWindow()
        {
            FarmSession s = Session();
            s.Tick(10f, 10, 6);            // 바퀴 전반부에 6마리
            FarmTickResult r = s.Tick(10f, 4, 4); // 20초 도달 — 총 10마리

            Assert.IsTrue(r.lapClosed);
            Assert.AreEqual(10, r.lapKills);
            // (10 × 2) ÷ 20초 × 3600 = 3,600 고철/시간
            Assert.AreEqual(3600d, r.lapHourlyRate, D);
            Assert.AreEqual(6, r.refill, "정원 10 − 생존 4");
            Assert.AreEqual(0, s.KillsThisLap, "창은 비워진다");
        }

        [Test]
        public void KillsAtLapEdge_CountToClosingLap_NotNext()
        {
            // 바퀴가 닫히는 그 틱에 잡은 적은 닫히는 바퀴에 들어가야 한다.
            FarmSession s = Session();
            FarmTickResult r = s.Tick(20f, 0, killsSinceLastTick: 10);

            Assert.IsTrue(r.lapClosed);
            Assert.AreEqual(10, r.lapKills, "경계에서 잡은 적이 다음 바퀴로 밀리면 안 된다");
            Assert.AreEqual(3600d, r.lapHourlyRate, D);
        }

        [Test]
        public void IdleLap_YieldsZeroRate_NotSkipped()
        {
            // 아무도 안 잡은 바퀴도 바퀴다. 시급 0으로 닫힌다 —
            // 빈 시간을 빼면 실제로는 벌 수 없는 속도가 기록된다.
            FarmTickResult r = Session().Tick(20f, 10, 0);

            Assert.IsTrue(r.lapClosed);
            Assert.AreEqual(0, r.lapKills);
            Assert.AreEqual(0d, r.lapHourlyRate, D);
        }

        [Test]
        public void HigherFirepower_DoesNotShortenLap()
        {
            // 화력이 세도 바퀴는 N초 그대로다(한 틱 전량 보충의 귀결).
            FarmSession slow = Session(), fast = Session();
            FarmTickResult a = slow.Tick(20f, 10, 5);
            FarmTickResult b = fast.Tick(20f, 0, 10);

            Assert.IsTrue(a.lapClosed && b.lapClosed);
            Assert.AreEqual(2d * a.lapHourlyRate, b.lapHourlyRate, D, "두 배 잡으면 시급도 두 배");
        }

        [Test]
        public void Unconfigured_NeverClosesLap_ButStillAccruesScrap()
        {
            // 정원·간격 TBD(0) → 파밍은 안 돌지만, 도전 층에서 잡은 킬의 적립까지 막지는 않는다.
            var s = new FarmSession(0, 0f, 2d);
            FarmTickResult r = s.Tick(1000f, 0, 5);

            Assert.IsFalse(s.IsConfigured);
            Assert.IsFalse(r.lapClosed);
            Assert.AreEqual(10d, r.scrapEarned, D);
        }

        // ---- 기록 갱신(스테이지별·더 높을 때만) ----

        [Test]
        public void Record_KeepsPerStage_AndOnlyImproves()
        {
            var save = new SaveDataV1();
            FarmSession s = Session();

            FarmTickResult first = s.Tick(20f, 0, 10);   // 3,600
            Assert.IsTrue(save.TryRecordFarmRate("S1", (float)first.lapHourlyRate));

            FarmTickResult worse = s.Tick(20f, 0, 5);    // 1,800
            Assert.IsFalse(save.TryRecordFarmRate("S1", (float)worse.lapHourlyRate), "낮으면 교체 안 함");
            Assert.AreEqual(3600f, save.BestFarmRate("S1"), 0.1f);

            // 다른 스테이지 기록은 섞이지 않는다.
            Assert.AreEqual(0f, save.BestFarmRate("S3"), 0.1f);
        }

        [Test]
        public void Record_ZeroRateLap_DoesNotOverwriteBest()
        {
            var save = new SaveDataV1();
            save.TryRecordFarmRate("S1", 3600f);

            FarmTickResult idle = Session().Tick(20f, 10, 0); // 시급 0인 바퀴
            Assert.IsFalse(save.TryRecordFarmRate("S1", (float)idle.lapHourlyRate));
            Assert.AreEqual(3600f, save.BestFarmRate("S1"), 0.1f, "최고 기록은 유지된다");
        }
    }
}
