namespace MBI.Core
{
    /// <summary>스테이지 최초 클리어 보고 1건.</summary>
    public struct ClearReport
    {
        public string stageId;
        public float enhMaterialReward;
    }

    /// <summary>
    /// 전투 → 방치 런타임 중립 채널(§5-7). <see cref="LogisticsOutputBridge"/>와 같은 패턴이다.
    ///
    /// MBI.Combat과 MBI.Idle이 서로를 참조하지 않게 하려고 둔다. 전투는 "무슨 일이 있었는지"만
    /// 여기에 놓고, 재화로 바꾸는 일은 방치 런타임이 한다 — 전투 코드가 지갑을 직접 만지면
    /// 적립 규칙이 두 곳으로 흩어진다.
    ///
    /// ⚠️ 값은 **가져가며 비운다**(Drain). 그냥 읽게 두면 같은 처치·클리어를 매 프레임 다시 세어
    /// 재화가 무한히 불어난다.
    /// </summary>
    public static class IdleSignals
    {
        private static int _kills;
        private static ClearReport _clear;
        private static bool _hasClear;

        /// <summary>
        /// 스테이지 0(튜토리얼)의 고정 식별자. **스테이지가 아니지만 클리어 여부는 저장에 남는다**
        /// (260902_W08 §2-2) — 미클리어면 켤 때마다 튜토리얼로 들어가야 하기 때문이다.
        /// </summary>
        public const string TutorialId = "S0";

        /// <summary>
        /// 튜토리얼을 이미 끝냈는가 — **방치 런타임이 저장에서 읽어 게시하고 전투가 읽는다.**
        ///
        /// 이 한 칸 때문에 `MBI.Combat`이 `MBI.Idle`을 참조하지 않아도 된다.
        /// 기본값 false = 「아직 안 했다」이므로, 저장 계층이 없는 씬(격리 전투 씬)에서도
        /// 튜토리얼이 정상으로 열린다.
        /// </summary>
        public static bool TutorialCleared;

        /// <summary>
        /// 보드 한 판 — 물류가 쓰고 방치 런타임이 저장에 싣는다 (2026-09-16).
        ///
        /// ⚠️⚠️ **이 칸이 다리다.** `BoardController` 는 저장소를 모르고 `IdleRuntime` 은
        /// 격자를 모른다 — 둘이 서로를 부르게 두면 두 살림이 얽힌다.
        /// 다른 신호들과 같은 규약이다(`TutorialCleared` 와 같은 자리).
        ///
        /// ⚠️ `null` 은 **「저장된 판이 없다」**이며 그때 보드는 시작 보드로 간다 —
        /// **빈 판과 다르다.** 빈 판은 플레이어가 지운 결과라 그대로 되살려야 한다.
        /// </summary>
        /// <remarks>
        /// 🗑️ **폐기 — 판이 둘이 됐다**(2026-09-16). 남은 뜻은 **로봇 A 의 판** 하나이며,
        /// <see cref="BoardStateOf"/> 와 **같은 칸을 가리킨다**(지침 §7 — 값이 둘이 되면 안 된다).
        /// 새로 쓰는 자리는 <see cref="BoardStateOf"/>·<see cref="SetBoardState"/> 를 쓸 것.
        /// </remarks>
        public static BoardStateV1 BoardState
        {
            get => _boardStates[0];
            set => _boardStates[0] = value;
        }

        /// <summary>판 둘 — 차례는 <see cref="MBI.Data.MountOwner"/> 값 그대로다(A 0 · B 1).</summary>
        private static readonly BoardStateV1[] _boardStates = new BoardStateV1[2];

        /// <summary>그 로봇의 저장된 판. <c>null</c> 이면 시작 보드로 간다.</summary>
        public static BoardStateV1 BoardStateOf(MBI.Data.MountOwner owner)
            => _boardStates[owner == MBI.Data.MountOwner.RobotB ? 1 : 0];

        /// <summary>그 로봇의 판을 올린다.</summary>
        public static void SetBoardState(MBI.Data.MountOwner owner, BoardStateV1 state)
            => _boardStates[owner == MBI.Data.MountOwner.RobotB ? 1 : 0] = state;

        /// <summary>
        /// 지갑 잔액 게시 — **방치 런타임이 쓰고 전투 HUD가 읽는다.** 고철.
        ///
        /// ⚠️ 위의 처치·클리어와 달리 **가져가며 비우지 않는다.** 저건 「무슨 일이 있었나」라
        /// 두 번 세면 재화가 불어나지만, 이건 「지금 얼마인가」라 늘 같은 값을 읽어야 한다.
        /// 사건과 상태를 한 파일에 두는 대신, 이름과 이 주석으로 갈라 둔다.
        ///
        /// 이 두 칸 덕분에 `MBI.Combat`이 `MBI.Idle`을 참조하지 않고도 잔액을 그릴 수 있다.
        /// 화면에 자리가 없어 새 패널을 놓지 못하고 전투 상태 칸에 한 줄로 붙였다 —
        /// 재화가 안 보이면 방치 사슬이 도는지 확인할 방법이 없다.
        /// </summary>
        public static double WalletScrap;

        /// <summary>지갑 잔액 게시 — 강화재료. <see cref="WalletScrap"/>와 같은 규약이다.</summary>
        public static double WalletEnhMaterial;

        /// <summary>지갑 잔액 게시 — 골드(2026-09-18). <see cref="WalletScrap"/>와 같은 규약이다.</summary>
        public static double WalletGold;

        /// <summary>다음 골드까지 얼마나 남았는가를 화면이 읽는다 — **상태**(비우지 않는다).</summary>
        public static int KillsTowardGold;

        /// <summary>
        /// 마리당 고철 — **방치 런타임이 게시하고 전투 화면이 읽는다**(2026-09-18).
        ///
        /// ⚠️ 전투가 `EconomyConfig` 를 직접 들면 같은 값이 두 자산 경로로 읽힌다.
        /// 값이 사는 곳은 자산 하나이고, 이 칸은 그 값을 **옮기는 관**이다.
        /// </summary>
        public static double ScrapPerKill;

        /// <summary>
        /// 방금 지급된 골드 — **사건**이라 가져가며 비운다(2026-09-18).
        ///
        /// ⚠️⚠️ 화면의 골드 드롭이 이것을 읽는다. 잔액 증가를 보고 짐작하지 않는 까닭은
        /// 오프라인 정산·마일스톤 보상도 잔액을 올리기 때문이다 — **처치로 떨어진 골드**만
        /// 바닥에 떨어져야 한다.
        /// </summary>
        private static int _goldAwarded;

        /// <summary>골드 지급 보고(방치 런타임만 부른다).</summary>
        public static void ReportGoldAwarded(int gold)
        {
            if (gold > 0) _goldAwarded += gold;
        }

        /// <summary>지급된 골드를 가져가며 비운다.</summary>
        public static int DrainGoldAwarded()
        {
            int g = _goldAwarded;
            _goldAwarded = 0;
            return g;
        }

        /// <summary>
        /// 마일스톤 보상 청구 — **사건**이라 가져가며 비운다(2026-09-18 설계 지시 ④).
        ///
        /// ⚠️⚠️ **전투가 지갑을 직접 안 만진다.** 카드는 「받았다」만 제 안에서 잠그고
        /// 실제 적립은 방치 런타임이 한다 — 적립 규칙이 한 곳에 사는 규약 그대로다.
        /// </summary>
        private static int _milestoneGold;
        private static double _milestoneScrap;

        /// <summary>보상 청구를 올린다(전투 화면만 부른다).</summary>
        public static void ReportMilestoneReward(int gold, double scrap)
        {
            if (gold > 0) _milestoneGold += gold;
            if (scrap > 0d) _milestoneScrap += scrap;
        }

        /// <summary>청구를 가져가며 비운다. 아무것도 없으면 false.</summary>
        public static bool TryDrainMilestoneReward(out int gold, out double scrap)
        {
            gold = _milestoneGold;
            scrap = _milestoneScrap;
            _milestoneGold = 0;
            _milestoneScrap = 0d;
            return gold > 0 || scrap > 0d;
        }

        /// <summary>
        /// 저장을 지워 달라 — **개발 빌드 전용 촬영 도구**(260902_W09 §1-2 승인).
        /// 방치 런타임이 가져가며 내린다.
        /// </summary>
        private static bool _resetSave;

        /// <summary>저장 초기화를 요청한다(바로가기 버튼).</summary>
        public static void RequestSaveReset() => _resetSave = true;

        /// <summary>요청을 가져가며 비운다. 방치 런타임만 부른다.</summary>
        public static bool DrainSaveReset()
        {
            bool had = _resetSave;
            _resetSave = false;
            return had;
        }

        /// <summary>처치 보고(전투 측). CombatSimulation.ConsumeKills로 가져온 값을 넣는다.</summary>
        public static void AddKills(int count)
        {
            if (count > 0) _kills += count;
        }

        /// <summary>쌓인 처치 수를 가져가며 비운다.</summary>
        public static int DrainKills()
        {
            int k = _kills;
            _kills = 0;
            return k;
        }

        /// <summary>최초 클리어 보고(전투 측). 재클리어는 보고하지 않는다 — 닫힌 곡선 보호.</summary>
        public static void ReportClear(string stageId, float enhMaterialReward)
        {
            if (string.IsNullOrEmpty(stageId)) return;
            _clear = new ClearReport { stageId = stageId, enhMaterialReward = enhMaterialReward };
            _hasClear = true;
        }

        /// <summary>클리어 보고를 가져가며 비운다.</summary>
        public static bool TryDrainClear(out ClearReport report)
        {
            report = _clear;
            bool had = _hasClear;
            _hasClear = false;
            _clear = default;
            return had;
        }

        /// <summary>씬 진입 시 초기화. 에디터에서 도메인 리로드를 끄면 이전 Play 값이 남는다.</summary>
        public static void Reset()
        {
            _kills = 0;
            _clear = default;
            _hasClear = false;
            _resetSave = false;
            TutorialCleared = false;
            for (int i = 0; i < _boardStates.Length; i++) _boardStates[i] = null;
            WalletScrap = 0d;
            WalletEnhMaterial = 0d;
            WalletGold = 0d;
            KillsTowardGold = 0;
            ScrapPerKill = 0d;
            _goldAwarded = 0;
            _milestoneGold = 0;
            _milestoneScrap = 0d;
        }
    }
}
