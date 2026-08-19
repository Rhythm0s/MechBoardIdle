namespace MBI.Core
{
    /// <summary>
    /// 처치 → 고철 환산(스테이지 기획서「파밍 규칙」). 고철은 킬 기반이므로 이 한 줄이 유일한 적립 규칙이다.
    ///
    /// ⚠️ 반환 타입에 강화재료 자리가 없다 — 킬로는 강화재료가 나오지 않는다(E2 분리).
    ///
    /// 시간당 고철 예측치(<see cref="ScrapFarmingRate"/>)와 **이중으로 적립하지 않는다.**
    /// 실제 적립은 이 규칙(킬 이벤트) 하나만 쓰고, 예측치는 화면 표시 전용이다.
    /// 실제 플레이에서 min(스폰속도, 잡는능력)은 "스폰된 적 이상은 죽일 수 없다"는 물리로 자연히 실현되므로,
    /// 두 경로로 각각 적립하면 §7 [2026-07-10] 시뮬 수식 이중계산과 같은 실수가 된다.
    /// </summary>
    public static class KillRewardRule
    {
        public static double Scrap(int kills, double scrapPerKill)
        {
            if (kills <= 0 || scrapPerKill <= 0d) return 0d;
            return kills * scrapPerKill;
        }
    }
}
