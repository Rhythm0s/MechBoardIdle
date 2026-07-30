namespace MBI.Core
{
    /// <summary>
    /// 물류 출력 ↔ 전투력 공유 지점(§5-6). 물류측(LogisticsOutputProvider)이 라이브 네트워크 출력을 쓰고,
    /// 전투측(StageRunner)이 읽는다. Combat↔Logistics 직접 참조를 피하는 중립 채널(둘 다 MBI.Core 참조).
    /// 기본값 = 대표 출력(라이브 네트워크 없을 때 = 격리 전투 씬). 145 = 관통1+분열1+폭발2.
    /// </summary>
    public static class LogisticsOutputBridge
    {
        public static float Output = 145f;

        /// <summary>라이브 네트워크 군수 생산율(발/초). 전투 HUD의 저장고/탄약 표시용(라이브 없으면 0).</summary>
        public static float AmmoProduce = 0f;
    }
}
