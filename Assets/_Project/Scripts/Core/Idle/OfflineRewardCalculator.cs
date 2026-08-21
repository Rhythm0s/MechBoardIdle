using System;

namespace MBI.Core
{
    /// <summary>
    /// 오프라인 정산 결과. **강화재료 필드가 없다** — 꺼둔 시간으로 S4 강화 벽을 우회할 수 없어야
    /// 닫힌 곡선(Σ S1~S3 클리어 보상 = s4Cost)이 유지되므로, 지급 재화를 타입 수준에서 고철로 못박는다.
    /// </summary>
    public struct OfflineRewardResult
    {
        public double scrap;          // 지급 고철
        public double creditedHours;  // 실제로 인정된 시간(상한 적용 후)
        public bool capped;           // 상한에 걸렸는가
        public bool usedDefaultRate;  // 상주 스테이지에 기록이 없어 기본 시급을 썼는가
        public double hourlyRate;     // 정산에 쓴 시급(추적·표시용)
    }

    /// <summary>
    /// 꺼 둔 동안의 고철 정산(스테이지 기획서「오프라인 보상」, 2026-08-19 확정).
    ///
    ///   고철 = 상주 스테이지의 최고 파밍 시급 × min(꺼 둔 시간, 36시간) × 오프라인 계수
    ///
    /// 구 공식(실측 전투력 × 시간 × 계수)은 폐기됐다 — 좌변이 초당 피해량 단위인데 결과는 고철 개수라
    /// 둘을 잇는 변환식이 문서에 없었다. 계산이 불가능한 식이었다.
    ///
    /// **어느 스테이지의 기록을 쓰는가**: 게임을 끈 시점에 상주 파밍 중이던 스테이지, 그 한 곳뿐이다.
    /// 여러 기록 중 최댓값을 고르지 않는다 — 켜두고 벌 수 있는 양과 꺼두고 받는 양이 같은 곳을 가리켜야
    /// 어느 스테이지에 머물지에 대한 판단이 온라인과 오프라인에서 갈리지 않는다.
    ///
    /// 계수·상한·기본 시급은 전부 설정값으로 받는다(§3 수치 하드코딩 금지). 계수는 아직 TBD다.
    /// </summary>
    public static class OfflineRewardCalculator
    {
        /// <summary>공식 그대로. 시간은 [0, capHours]로 자른다(음수 경과가 음수 보상이 되지 않도록).</summary>
        public static OfflineRewardResult Compute(double hourlyRate, double elapsedHours, double coef, double capHours)
        {
            var r = new OfflineRewardResult { hourlyRate = hourlyRate };

            double hours = elapsedHours > 0d ? elapsedHours : 0d;
            if (capHours > 0d && hours > capHours)
            {
                hours = capHours;
                r.capped = true;
            }

            r.creditedHours = hours;
            if (hourlyRate <= 0d || coef <= 0d || hours <= 0d) return r; // 미확정 수치면 지급 0

            r.scrap = hourlyRate * hours * coef;
            return r;
        }

        /// <summary>
        /// 세이브에서 상주 스테이지 기록과 마지막 접속 시각을 읽어 정산한다.
        /// 기록이 없으면 <paramref name="defaultHourlyRate"/>(TBD)를 쓴다.
        /// </summary>
        public static OfflineRewardResult FromSave(SaveDataV1 save, IClock clock,
            double coef, double capHours, double defaultHourlyRate)
        {
            if (save == null || clock == null) return default;

            double rate = save.BestFarmRate(save.lastFarmStageId);
            bool usedDefault = rate <= 0f;
            if (usedDefault) rate = defaultHourlyRate;

            double hours = ElapsedHours(save.lastSeenUtcTicks, clock);

            OfflineRewardResult r = Compute(rate, hours, coef, capHours);
            r.usedDefaultRate = usedDefault;
            return r;
        }

        /// <summary>마지막 접속 이후 경과 시간(시간 단위). 기록이 없으면 0.</summary>
        public static double ElapsedHours(long lastSeenUtcTicks, IClock clock)
        {
            if (lastSeenUtcTicks <= 0L || clock == null) return 0d; // 첫 실행 = 꺼둔 시간 없음
            long now = clock.UtcNow.UtcTicks;
            long delta = now - lastSeenUtcTicks;
            return delta > 0L ? delta / (double)TimeSpan.TicksPerHour : 0d;
        }
    }
}
