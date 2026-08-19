using System.Reflection;
using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 경제 순수 로직(§5-7). 원천 = 스테이지 기획서「파밍 규칙」·「오프라인 보상」.
    /// 핵심은 E2 분리 — 고철과 강화재료가 서로 새지 않는지를 구조로 못 박는다.
    /// </summary>
    public sealed class EconomyTests
    {
        private const double D = 0.0001d;

        // ---- CurrencyWallet (E2 분리) ----

        [Test]
        public void AddScrap_DoesNotAffectEnhMaterial()
        {
            var w = new CurrencyWallet();
            w.AddScrap(100d);

            Assert.AreEqual(100d, w.Scrap, D);
            Assert.AreEqual(0d, w.EnhMaterial, D, "고철 적립이 강화재료를 늘리면 안 된다");
        }

        [Test]
        public void AddEnhMaterial_DoesNotAffectScrap()
        {
            var w = new CurrencyWallet();
            w.AddEnhMaterial(30d);

            Assert.AreEqual(0d, w.Scrap, D);
            Assert.AreEqual(30d, w.EnhMaterial, D);
        }

        [Test]
        public void TrySpend_FailsAndKeepsBalance_WhenInsufficient()
        {
            var w = new CurrencyWallet(scrap: 50d);

            Assert.IsFalse(w.TrySpendScrap(80d));
            Assert.AreEqual(50d, w.Scrap, D, "실패하면 한 푼도 깎이지 않는다");
            Assert.IsTrue(w.TrySpendScrap(50d));
            Assert.AreEqual(0d, w.Scrap, D);
        }

        [Test]
        public void Wallet_NegativeAdd_IsIgnored()
        {
            var w = new CurrencyWallet(scrap: 10d);
            w.AddScrap(-5d);
            Assert.AreEqual(10d, w.Scrap, D, "차감 경로는 TrySpend 하나뿐이어야 한다");
        }

        [Test]
        public void Wallet_ExposesNoCrossCurrencyConversion()
        {
            // 변환 메서드가 생기면 두 재화가 사실상 하나가 된다 — 이름으로 막아 둔다.
            foreach (MethodInfo m in typeof(CurrencyWallet).GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                string n = m.Name.ToLowerInvariant();
                Assert.IsFalse(n.Contains("convert") || n.Contains("exchange") || n.Contains("trade"),
                    $"재화 변환 API가 생겼다: {m.Name}");
            }
        }

        // ---- KillRewardRule ----

        [Test]
        public void KillReward_IsKillsTimesPerKill()
        {
            Assert.AreEqual(80d, KillRewardRule.Scrap(40, 2d), D, "40마리 × 2 = 80");
        }

        [Test]
        public void KillReward_ZeroOrNegative_IsZero()
        {
            Assert.AreEqual(0d, KillRewardRule.Scrap(0, 2d), D);
            Assert.AreEqual(0d, KillRewardRule.Scrap(-3, 2d), D);
            Assert.AreEqual(0d, KillRewardRule.Scrap(10, 0d), D, "마리당 고철 미확정(0)이면 적립 없음");
        }

        // ---- ScrapFarmingRate (표시 전용) ----

        [Test]
        public void FarmingRate_TakesMinOfSpawnAndCapacity()
        {
            Assert.AreEqual(200d, ScrapFarmingRate.PerHour(100d, 500d, 2d), D, "스폰이 병목");
            Assert.AreEqual(200d, ScrapFarmingRate.PerHour(500d, 100d, 2d), D, "화력이 병목");
        }

        [Test]
        public void FarmingRate_ZeroSpawnSentinel_IsZero()
        {
            // 스폰속도 미측정(0) → 예측치 없음. 화면은 "측정 중"을 띄우고 실적립은 킬 기반으로 돈다.
            Assert.AreEqual(0d, ScrapFarmingRate.PerHour(0d, 500d, 2d), D);
        }

        [Test]
        public void SpawnPerHour_IsCapOverInterval()
        {
            // 정원 10 · 간격 20초 → 시간당 1,800마리.
            Assert.AreEqual(1800d, ScrapFarmingRate.SpawnPerHour(10, 20f), 0.001d);
            Assert.AreEqual(0d, ScrapFarmingRate.SpawnPerHour(10, 0f), D, "간격 미확정이면 0");
        }

        // ---- ResourceTicker ----

        [Test]
        public void Ticker_EmitsOneTickPerInterval()
        {
            var t = new ResourceTicker(1f);
            Assert.IsFalse(t.TryConsume(0.5f, out int a));
            Assert.AreEqual(0, a);
            Assert.IsTrue(t.TryConsume(0.5f, out int b));
            Assert.AreEqual(1, b);
        }

        [Test]
        public void Ticker_LargeDt_EmitsMultipleTicks()
        {
            // 프레임이 튀어도 넘긴 만큼 돌려준다 — 삼키면 수입이 샌다.
            var t = new ResourceTicker(1f);
            Assert.IsTrue(t.TryConsume(3.5f, out int ticks));
            Assert.AreEqual(3, ticks);
            Assert.AreEqual(0.5f, t.Elapsed, 0.001f, "나머지는 다음 창으로 이월");
        }

        [Test]
        public void Ticker_ZeroInterval_NeverTicks()
        {
            var t = new ResourceTicker(0f);
            Assert.IsFalse(t.TryConsume(100f, out int ticks));
            Assert.AreEqual(0, ticks, "간격 미확정(TBD)이면 돌지 않는다");
        }
    }
}
