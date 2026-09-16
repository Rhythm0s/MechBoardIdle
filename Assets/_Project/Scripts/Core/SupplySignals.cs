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

        // 로봇별로 갈랐다 (2026-09-16 · 사용자 확정 · 플랜 §74-16 ①③)
        //
        // ⚠️⚠️ **왜 갈랐나.** 보드가 로봇별 둘이 되었고(①), 전투 HUD 가 **대기 로봇의
        // 적재**를 보여야 한다(③). 종전에는 이 칸들이 「지금 나선 로봇」 하나만 들어서
        // **대기 쪽은 물어볼 자리 자체가 없었다.**
        //
        // 📌 **옛 이름을 지우지 않는다.** 아래 `MountTotal` 등은 그대로 두되 저장소가
        //    아니라 **「지금 나선 로봇」을 가리키는 창**이 된다 — 값이 두 곳에 살면
        //    답이 둘이 된다(지침 §7). 읽는 곳 수십 군데가 그대로 맞는다.

        private static int Index(MBI.Data.MountOwner owner) =>
            owner == MBI.Data.MountOwner.RobotB ? 1 : 0;

        private static readonly float[] _mountTotal = new float[2];
        private static readonly float[] _storageStock = new float[2];

        /// <summary>그 로봇의 마운트 총 적재량.</summary>
        public static float MountTotalOf(MBI.Data.MountOwner owner) => _mountTotal[Index(owner)];

        /// <summary>그 로봇의 마운트 총 적재량을 올린다.</summary>
        public static void SetMountTotal(MBI.Data.MountOwner owner, float value)
            => _mountTotal[Index(owner)] = value;

        /// <summary>그 로봇 창고의 탄약 총 재고.</summary>
        public static float StorageStockOf(MBI.Data.MountOwner owner) => _storageStock[Index(owner)];

        /// <summary>그 로봇 창고의 재고를 올린다.</summary>
        public static void SetStorageStock(MBI.Data.MountOwner owner, float value)
            => _storageStock[Index(owner)] = value;

        /// <summary>지금 나선 로봇의 마운트 총 적재량. 0이면 공격이 멈춘 자리다.</summary>
        public static float MountTotal
        {
            get => _mountTotal[Index(ActiveOwner)];
            set => _mountTotal[Index(ActiveOwner)] = value;
        }

        /// <summary>저장 노드(창고)의 탄약 총 재고. 조립 화면이 보는 층이다(UI 문서 12-1).</summary>
        public static float StorageStock
        {
            get => _storageStock[Index(ActiveOwner)];
            set => _storageStock[Index(ActiveOwner)] = value;
        }

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

        private static readonly int[] _slotCount = new int[2];
        private static readonly MBI.Data.MountItem[][] _slotItem =
            { System.Array.Empty<MBI.Data.MountItem>(), System.Array.Empty<MBI.Data.MountItem>() };
        private static readonly float[][] _slotAmount =
            { System.Array.Empty<float>(), System.Array.Empty<float>() };
        private static readonly float[] _stackLimit = new float[2];

        /// <summary>그 로봇의 슬롯 수(A 4 · B 8). 0이면 슬롯 상태가 없다.</summary>
        public static int MountSlotCountOf(MBI.Data.MountOwner owner) => _slotCount[Index(owner)];

        /// <summary>그 로봇의 슬롯마다 무엇이 들었는가.</summary>
        public static MBI.Data.MountItem[] MountSlotItemOf(MBI.Data.MountOwner owner)
            => _slotItem[Index(owner)];

        /// <summary>그 로봇의 슬롯마다 얼마나 들었는가.</summary>
        public static float[] MountSlotAmountOf(MBI.Data.MountOwner owner)
            => _slotAmount[Index(owner)];

        /// <summary>그 로봇의 스택 상한. 채움 비율의 분모다.</summary>
        public static float MountStackLimitOf(MBI.Data.MountOwner owner) => _stackLimit[Index(owner)];

        /// <summary>그 로봇의 스택 상한을 올린다.</summary>
        public static void SetMountStackLimit(MBI.Data.MountOwner owner, float value)
            => _stackLimit[Index(owner)] = value;

        /// <summary>지금 나선 로봇의 슬롯 수(A 4 · B 8). 0이면 슬롯 상태가 없다.</summary>
        public static int MountSlotCount => _slotCount[Index(ActiveOwner)];

        /// <summary>슬롯마다 무엇이 들었는가. 길이는 <see cref="MountSlotCount"/> 이상이다.</summary>
        public static MBI.Data.MountItem[] MountSlotItem => _slotItem[Index(ActiveOwner)];

        /// <summary>슬롯마다 얼마나 들었는가.</summary>
        public static float[] MountSlotAmount => _slotAmount[Index(ActiveOwner)];

        /// <summary>스택 상한(확정 10 · `260901_V03`). 채움 비율의 **분모**다. 0이면 못 나눈다.</summary>
        public static float MountStackLimit
        {
            get => _stackLimit[Index(ActiveOwner)];
            set => _stackLimit[Index(ActiveOwner)] = value;
        }

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

        /// <summary>
        /// **그 탄종이 마운트에 지금 몇 발 실려 있는가** (2026-09-15 사용자 확정 · 발사 규칙 (가)).
        ///
        /// ⚠️ **왜 도착률만으로는 모자랐나.** 도착률은 **흐름**이고 재고는 **고임**이다.
        /// 마운트가 40 발로 가득 차 있어도 그 순간 벨트가 아무것도 안 나르면 도착률은 0 이라,
        /// 도착률만 보면 **실탄을 40 발 지고도 안 쏘는** 로봇이 된다.
        ///
        /// 슬롯을 훑어 합한다 — 같은 탄이 여러 슬롯에 나뉘어 들어가므로(<see cref="MountLoad"/>)
        /// 슬롯 하나만 보면 모자라게 센다.
        /// </summary>
        public static float MountStockOf(MBI.Data.AmmoKind kind)
            => MountStockOf(kind, ActiveOwner);

        /// <summary>그 로봇의 마운트에 그 탄종이 몇 발 실려 있는가.</summary>
        public static float MountStockOf(MBI.Data.AmmoKind kind, MBI.Data.MountOwner owner)
        {
            MBI.Data.MountItem want = MBI.Data.MountItemMap.From(kind);
            if (want == MBI.Data.MountItem.None) return 0f;

            MBI.Data.MountItem[] items = MountSlotItemOf(owner);
            float[] amounts = MountSlotAmountOf(owner);

            float sum = 0f;
            int n = MountSlotCountOf(owner);
            if (n > items.Length) n = items.Length;
            if (n > amounts.Length) n = amounts.Length;
            for (int i = 0; i < n; i++)
                if (items[i] == want) sum += amounts[i];
            return sum;
        }

        /// <summary>슬롯 배열을 길이에 맞춰 잡는다. 길이가 같으면 아무것도 안 한다.</summary>
        public static void EnsureSlots(int count) => EnsureSlots(ActiveOwner, count);

        /// <summary>그 로봇의 슬롯 배열을 길이에 맞춰 잡는다.</summary>
        public static void EnsureSlots(MBI.Data.MountOwner owner, int count)
        {
            if (count < 0) count = 0;
            int i = Index(owner);
            _slotCount[i] = count;
            if (_slotItem[i].Length >= count && _slotAmount[i].Length >= count) return;

            _slotItem[i] = new MBI.Data.MountItem[count];
            _slotAmount[i] = new float[count];
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
            // ⚠️ **둘 다 비운다** — 하나만 비우면 대기 쪽 적재가 다음 판으로 새어 간다.
            for (int i = 0; i < 2; i++)
            {
                _mountTotal[i] = 0f;
                _storageStock[i] = 0f;
                _slotCount[i] = 0;
                _stackLimit[i] = 0f;
            }
            ActiveOwner = MBI.Data.MountOwner.RobotA;
        }
    }
}
