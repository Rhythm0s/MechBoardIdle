namespace MBI.Core
{
    /// <summary>
    /// 시간당 고철 예측치(스테이지 기획서「파밍 규칙」의 farmingRule 원문 그대로):
    /// <c>시간당 고철 = min(스폰속도, 잡는능력) × 마리당고철</c>.
    ///
    /// **HUD 표시 전용.** 실제 적립은 <see cref="KillRewardRule"/> 하나만 담당한다 —
    /// 두 경로로 각각 적립하면 이중계산이 된다.
    /// 스폰속도가 미측정(0)이면 0을 반환한다 — 화면은 수치 대신 "측정 중"을 띄우고,
    /// 실적립은 킬 기반이라 정상 동작한다. 즉 TBD가 게임을 막지 않는다.
    /// </summary>
    public static class ScrapFarmingRate
    {
        public static double PerHour(double spawnPerHour, double killCapacityPerHour, double scrapPerKill)
        {
            if (spawnPerHour <= 0d || killCapacityPerHour <= 0d || scrapPerKill <= 0d) return 0d;
            double bottleneck = spawnPerHour < killCapacityPerHour ? spawnPerHour : killCapacityPerHour;
            return bottleneck * scrapPerKill;
        }

        /// <summary>스폰속도(마리/시간) = 정원 ÷ 스폰간격. 간격이 0(미측정)이면 0.</summary>
        public static double SpawnPerHour(int spawnCap, float spawnIntervalSeconds)
        {
            if (spawnCap <= 0 || spawnIntervalSeconds <= 0f) return 0d;
            return spawnCap / (double)spawnIntervalSeconds * 3600d;
        }
    }
}
