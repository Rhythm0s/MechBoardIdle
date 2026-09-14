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

        // ── 마운트 슬롯 상태 (2026-09-14 신설 · 플랜 §71-41 · UI 문서 12-4 적재 그리드) ──
        //
        // **왜 합만으로는 부족한가.** `MountTotal` 하나로는 「얼마나 찼나」만 말할 수 있고
        // **무엇이 몇 칸에 들어 있나**는 못 말한다. 12-4 의 적재 그리드는 칸마다 탄종색으로
        // 그려야 하므로 슬롯 단위가 필요하다.
        //
        // ⚠️ **배열을 매 틱 새로 만들지 않는다** — 전투가 도는 내내 프레임마다 할당하면
        // 쓰레기가 쌓인다. 길이가 달라질 때만 다시 잡고 그 뒤에는 덮어 쓴다.
        //
        // ⚠️ **지금 나선 로봇 것만이다** — `ActiveOwner` 와 같은 이유다. 대기 중인 로봇의
        // 마운트를 함께 실으면 **안 싸우는 쪽 적재가 화면에 섞인다.**

        /// <summary>지금 나선 로봇의 슬롯 수(A 4 · B 8). 0이면 슬롯 상태가 없다.</summary>
        public static int MountSlotCount;

        /// <summary>슬롯마다 무엇이 들었는가. 길이는 <see cref="MountSlotCount"/> 이상이다.</summary>
        public static MBI.Data.MountItem[] MountSlotItem = System.Array.Empty<MBI.Data.MountItem>();

        /// <summary>슬롯마다 얼마나 들었는가.</summary>
        public static float[] MountSlotAmount = System.Array.Empty<float>();

        /// <summary>스택 상한(확정 10 · `260901_V03`). 채움 비율의 **분모**다. 0이면 못 나눈다.</summary>
        public static float MountStackLimit;

        /// <summary>
        /// **탄종별 마운트 도착률**(발/초) — 관통·표준·폭발 차례 (2026-09-15 사용자 확정).
        ///
        /// ⚠️ **발사율이 이것으로 배분된다.** 종전에는 「명목 출력 대비 전역 비율」이라
        /// **표준탄만 오는 보드에서도 관통·폭발이 발사율을 받았고**, 마운트에 그 탄이 없어
        /// `ConsumeRound` 가 실패해 **한 발도 안 나갔다** — HUD 만 쏘는 척했다.
        /// </summary>
        public static readonly float[] MountArrivalRate = new float[3];

        /// <summary>그 탄종의 도착률. 범위 밖이면 0.</summary>
        public static float ArrivalRateOf(MBI.Data.AmmoKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < MountArrivalRate.Length ? MountArrivalRate[i] : 0f;
        }

        /// <summary>슬롯 배열을 길이에 맞춰 잡는다. 길이가 같으면 아무것도 안 한다.</summary>
        public static void EnsureSlots(int count)
        {
            if (count < 0) count = 0;
            MountSlotCount = count;
            if (MountSlotItem.Length >= count && MountSlotAmount.Length >= count) return;

            MountSlotItem = new MBI.Data.MountItem[count];
            MountSlotAmount = new float[count];
        }

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
            for (int i = 0; i < MountArrivalRate.Length; i++) MountArrivalRate[i] = 0f;
            HasCombat = false;
            MountTotal = 0f;
            StorageStock = 0f;
            MountSlotCount = 0;
            MountStackLimit = 0f;
            ActiveOwner = MBI.Data.MountOwner.RobotA;
        }
    }
}
