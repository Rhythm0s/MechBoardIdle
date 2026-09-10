namespace MBI.Core
{
    /// <summary>
    /// 공급 정지 표시가 볼 값 — **전투 층이 넣고 조립 층이 읽는다**
    /// (2026-09-09 신설 · UI 문서 12장 · `260909_W01` 3장).
    ///
    /// <see cref="LogisticsOutputBridge"/>와 같은 꼴이며 방향만 반대다. 마운트 적재와
    /// 저장 재고는 <c>MBI.Combat</c>이 들고 있는데 <c>MBI.Logistics</c>는 그쪽을 참조하지
    /// 않으므로, 값을 코어에 놓고 양쪽이 여기만 본다.
    ///
    /// ⚠️ **비율을 여기서 만들지 않는다.** 0인지 아닌지는 규칙(<see cref="SupplyStopRules"/>)이
    /// 판정하고, 이 자리는 **숫자를 그대로 나른다**. 다리에서 나누면 화면마다 다른 기준이 생긴다.
    ///
    /// ⚠️ **없는 값과 0을 가른다.** 전투가 아직 안 돌면 <see cref="HasCombat"/>가 거짓이며,
    /// 그때 마운트 0을 「탄약이 떨어졌다」로 읽으면 **씬을 열자마자 경고가 뜬다.**
    /// </summary>
    public static class SupplySignals
    {
        /// <summary>전투 층이 값을 넣고 있는가. 거짓이면 아래 값들은 뜻이 없다.</summary>
        public static bool HasCombat;

        /// <summary>지금 나선 로봇의 마운트 총 적재량. 0이면 공격이 멈춘 자리다.</summary>
        public static float MountTotal;

        /// <summary>저장 노드(창고)의 탄약 총 재고. 조립 화면이 보는 층이다(UI 문서 12-1).</summary>
        public static float StorageStock;

        /// <summary>
        /// **지금 전투에 나와 있는 로봇** (2026-09-10 사용자 확정 · 리허설 1차 ⑦).
        ///
        /// 물류 도착을 이 로봇 것만 센다 — 마운트는 로봇마다 따로이고, 대기 중인 로봇에게
        /// 닿은 것은 **지금 싸우는 화력이 아니다.** 종전에는 둘을 합산해 **B 라인을 깔면
        /// A로 싸우는 동안에도 전투력이 올라가 보였다.**
        /// </summary>
        public static MBI.Data.MountOwner ActiveOwner = MBI.Data.MountOwner.RobotA;

        public static void Reset()
        {
            HasCombat = false;
            MountTotal = 0f;
            StorageStock = 0f;
            ActiveOwner = MBI.Data.MountOwner.RobotA;
        }
    }
}
