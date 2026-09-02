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
        }
    }
}
