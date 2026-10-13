using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 상주 파밍 한 판(§5-7) — 바퀴 시급 산출과 스테이지별 기록 갱신.
    /// 원천 = 스테이지 기획서「파밍 규칙」·「오프라인 보상」.
    ///
    /// ⚠️⚠️ **고철 단가가 세션에서 빠졌다**(2026-10-13 · 종류별 고철 #112 ⑤).
    /// 단가가 종류마다 갈린 뒤로 `마릿수 × 단가` 는 **틀린 셈**이다 — 보병 열과 포격 열이
    /// 같은 수가 된다. 이제 세션은 **번 액수를 받아** 적는다.
    ///
    /// 아래 시험들은 **「마리당 2 고철인 판」**을 흉내 내려고 `Scrap(n)` 으로 액수를 만든다 —
    /// 수는 테스트용 가상값이고 밸런스 단정이 아니다.
    /// </summary>
    public sealed class FarmSessionTests
    {
        private const double D = 0.001d;

        /// <summary>마리당 2 고철인 판을 흉내 낸다 — 단가는 **시험 쪽**에 산다.</summary>
        private const double PerKill = 2d;
        private static double Scrap(int kills) => kills * PerKill;

        // 정원 10 · 간격 20초 (전부 테스트용 가상값)
        private static FarmSession Session() => new FarmSession(10, 20f);

        [Test]
        public void Kills_AccrueScrapImmediately_NotAtLapEnd()
        {
            // 고철은 킬 시점에 들어온다. 바퀴 시급은 기록일 뿐 수입이 아니다(이중 적립 방지).
            FarmTickResult r = Session().Tick(1f, 10, 3, Scrap(3));

            Assert.AreEqual(6d, r.scrapEarned, D, "3마리 × 2고철");
            Assert.IsFalse(r.lapClosed);
            Assert.AreEqual(0d, r.lapHourlyRate, D);
        }

        [Test]
        public void LapClose_ComputesHourlyRate_AndResetsWindow()
        {
            FarmSession s = Session();
            s.Tick(10f, 10, 6, Scrap(6));            // 바퀴 전반부에 6마리
            FarmTickResult r = s.Tick(10f, 4, 4, Scrap(4)); // 20초 도달 — 총 10마리

            Assert.IsTrue(r.lapClosed);
            Assert.AreEqual(10, r.lapKills);
            Assert.AreEqual(20d, r.lapScrap, D, "10마리 × 2고철");
            // 20고철 ÷ 20초 × 3600 = 3,600 고철/시간
            Assert.AreEqual(3600d, r.lapHourlyRate, D);
            Assert.AreEqual(6, r.refill, "정원 10 − 생존 4");
            Assert.AreEqual(0, s.KillsThisLap, "창은 비워진다");
            Assert.AreEqual(0d, s.ScrapThisLap, D, "액수 창도 비워진다");
        }

        [Test]
        public void KillsAtLapEdge_CountToClosingLap_NotNext()
        {
            // 바퀴가 닫히는 그 틱에 잡은 적은 닫히는 바퀴에 들어가야 한다.
            FarmSession s = Session();
            FarmTickResult r = s.Tick(20f, 0, 10, Scrap(10));

            Assert.IsTrue(r.lapClosed);
            Assert.AreEqual(10, r.lapKills, "경계에서 잡은 적이 다음 바퀴로 밀리면 안 된다");
            Assert.AreEqual(3600d, r.lapHourlyRate, D);
        }

        [Test]
        public void IdleLap_YieldsZeroRate_NotSkipped()
        {
            // 아무도 안 잡은 바퀴도 바퀴다. 시급 0으로 닫힌다 —
            // 빈 시간을 빼면 실제로는 벌 수 없는 속도가 기록된다.
            FarmTickResult r = Session().Tick(20f, 10, 0, 0d);

            Assert.IsTrue(r.lapClosed);
            Assert.AreEqual(0, r.lapKills);
            Assert.AreEqual(0d, r.lapHourlyRate, D);
        }

        [Test]
        public void HigherFirepower_DoesNotShortenLap()
        {
            // 화력이 세도 바퀴는 N초 그대로다(한 틱 전량 보충의 귀결).
            FarmSession slow = Session(), fast = Session();
            FarmTickResult a = slow.Tick(20f, 10, 5, Scrap(5));
            FarmTickResult b = fast.Tick(20f, 0, 10, Scrap(10));

            Assert.IsTrue(a.lapClosed && b.lapClosed);
            Assert.AreEqual(2d * a.lapHourlyRate, b.lapHourlyRate, D, "두 배 잡으면 시급도 두 배");
        }

        /// <summary>
        /// ⚠️ **같은 마릿수라도 번 액수가 다르면 시급이 다르다** (2026-10-13 신설).
        ///
        /// 종류별 단가가 생긴 뒤로 「몇 마리 잡았나」만으로는 시급을 못 낸다 —
        /// 포격 다섯(4 씩)이 보병 다섯(2 씩)보다 두 배를 번다. 종전 식은 둘을 같게 봤다.
        /// </summary>
        [Test]
        public void 같은_마릿수라도_액수가_다르면_시급이_다르다()
        {
            FarmSession infantry = Session(), cannon = Session();

            FarmTickResult a = infantry.Tick(20f, 0, 5, 5 * 2d);   // 보병 다섯
            FarmTickResult b = cannon.Tick(20f, 0, 5, 5 * 4d);     // 포격 다섯

            Assert.AreEqual(a.lapKills, b.lapKills, "마릿수는 같다");
            Assert.AreEqual(2d * a.lapHourlyRate, b.lapHourlyRate, D,
                "단가가 두 배면 시급도 두 배라야 한다 — 마릿수로만 세면 둘이 같아진다");
        }

        [Test]
        public void Unconfigured_NeverClosesLap_ButStillAccruesScrap()
        {
            // 정원·간격 TBD(0) → 파밍은 안 돌지만, 도전 층에서 잡은 킬의 적립까지 막지는 않는다.
            var s = new FarmSession(0, 0f);
            FarmTickResult r = s.Tick(1000f, 0, 5, Scrap(5));

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

            FarmTickResult first = s.Tick(20f, 0, 10, Scrap(10));   // 3,600
            Assert.IsTrue(save.TryRecordFarmRate("S1", (float)first.lapHourlyRate));

            FarmTickResult worse = s.Tick(20f, 0, 5, Scrap(5));     // 1,800
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

            FarmTickResult idle = Session().Tick(20f, 10, 0, 0d); // 시급 0인 바퀴
            Assert.IsFalse(save.TryRecordFarmRate("S1", (float)idle.lapHourlyRate));
            Assert.AreEqual(3600f, save.BestFarmRate("S1"), 0.1f, "최고 기록은 유지된다");
        }
    }
}
