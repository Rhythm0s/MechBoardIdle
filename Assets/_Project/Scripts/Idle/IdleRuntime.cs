using MBI.Core;
using MBI.Data;
using UnityEngine;

namespace MBI.Idle
{
    /// <summary>
    /// 방치 런타임(§5-7) — 세이브를 읽고, 지갑을 들고, 주기적으로 저장한다.
    ///
    /// 순수 로직(<see cref="SaveDataV1"/>·<see cref="CurrencyWallet"/>·<see cref="ISaveStore"/>)은
    /// 전부 MBI.Core에 있고 여기는 **얇은 어댑터**다 — 씬 수명주기와 시간만 붙인다.
    /// MBI.Idle은 Combat/Logistics를 참조하지 않는다. 결합이 필요하면 static 채널로 연결한다.
    ///
    /// 실행 순서를 앞으로 당겨 둔다: 다른 컴포넌트가 Start에서 세이브를 읽을 때
    /// 이미 로드가 끝나 있어야 "첫 프레임엔 값이 비어 있다"는 종류의 버그가 안 생긴다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class IdleRuntime : MonoBehaviour
    {
        [Tooltip("자동 저장 주기(초). 밸런스가 아니라 운영치 — 웹빌드엔 원자적 쓰기가 없어 너무 잦으면 손상 위험만 커진다.")]
        [SerializeField] private float autosaveIntervalSeconds = 30f;
        [Tooltip("경제 미확정치(마리당 고철·오프라인 계수·기본 시급). 씬 생성기가 주입.")]
        [SerializeField] private EconomyConfig economy;
        [Tooltip("밸런스 계약 미러(오프라인 상한 36h). 씬 생성기가 주입.")]
        [SerializeField] private BalanceConfig balance;

        /// <summary>현재 세이브 데이터. 다른 시스템은 이걸 읽고 쓴 뒤 저장은 이 클래스에 맡긴다.</summary>
        public SaveDataV1 Data { get; private set; }

        /// <summary>재화 지갑(고철/강화재료 분리 — E2).</summary>
        public CurrencyWallet Wallet { get; private set; }

        /// <summary>이번 접속에서 지급된 오프라인 보상(표시용). 없으면 scrap 0.</summary>
        public OfflineRewardResult LastOfflineReward { get; private set; }

        private ISaveStore _store;
        private IClock _clock;
        private ResourceTicker _autosave;

        private void Awake()
        {
            IdleSignals.Reset(); // 도메인 리로드 비활성 시 이전 Play의 신호가 남는 것 방지
            _store = new PlayerPrefsSaveStore();
            _clock = new SystemClock();
            _autosave = new ResourceTicker(autosaveIntervalSeconds);

            // 파싱 실패·첫 실행은 둘 다 "기록 없음"으로 같게 다룬다(예외로 게임을 죽이지 않는다).
            Data = _store.TryLoad(out SaveDataV1 loaded) ? loaded : new SaveDataV1();
            Wallet = new CurrencyWallet(Data.scrap, Data.enhMaterial, Data.gold);

            // 튜토리얼을 이미 했는지 전투 쪽에 알린다. **Awake에서 한다** —
            // Stage0Session이 자기 Awake에서 이 값을 읽어 들어갈지 말지를 정하므로,
            // 실행 순서(-100 → -50)가 뒤집히면 매번 튜토리얼이 다시 열린다.
            IdleSignals.TutorialCleared = Data.HasCleared(IdleSignals.TutorialId);

            // ⚠️ **로봇 B 해금도 저장에서 복원한다**(2026-10-13 · #84).
            //    튜토리얼 복원과 **같은 자리 같은 꼴**이다 — 둘 다 「깼는가」가 답이다.
            RobotBGate.S4Cleared = Data.HasCleared(RobotBGate.UnlockStageId);

            // ⚠️ **스테이지 목록의 해금도 같은 장부에서 온다**(2026-10-13 · 순서 5).
            //    위 둘과 같은 자리 같은 꼴이다 — 통과 기록 하나가 셋을 다 먹인다.
            //    해금을 따로 저장하지 않는 까닭이 이것이다(지침 §7 · StageUnlockRule).
            IdleSignals.PublishClearedStages(Data.clearedStageIds);

            // 저장된 보드를 물류 쪽에 내놓는다. **Awake 에서 한다** —
            // `BoardController` 가 자기 `Start` 에서 이것을 읽어 판을 세우므로,
            // 실행 순서(-100 → 기본)가 뒤집히면 **매번 시작 보드로 돌아간다.**
            // ⚠️ **구 저장의 한 판을 목록으로 옮긴 뒤 내놓는다**(2026-09-16 · 판이 둘이 됐다).
            // 이주를 안 하면 이미 나간 저장의 A 판이 **조용히 사라진다.**
            Data.MigrateLegacyBoard();
            foreach (MountOwner o in BoardOwners)
                IdleSignals.SetBoardState(o, Data.BoardOf(o));

            SettleOffline();
        }

        /// <summary>
        /// 판을 가진 로봇들 — **한 곳에만 적는다**(2026-09-16). 불러오기·저장·초기화 셋이
        /// 같은 목록을 돌아야 한다. 따로 적으면 한 군데에서 B 를 빠뜨려도 신호가 없다.
        /// </summary>
        private static readonly MountOwner[] BoardOwners = { MountOwner.RobotA, MountOwner.RobotB };

        private void Update()
        {
            // 촬영용 저장 초기화 — 요청이 왔을 때만. 기록이 사라지므로 튜토리얼도 다시 열린다.
            if (IdleSignals.DrainSaveReset())
            {
                ResetSave();
                // #141 · 저장은 비웠는데 메모리 장부가 남아 T3 안 뜸·S2 열림 — 새 저장으로 갈아끼운다
                IdleSignals.RepublishAfterReset(Data);
                RobotBGate.Reset();   // 처음부터면 B 도 다시 잠긴다
            }

            CreditSignals();
            PublishWallet();
            if (_autosave.TryConsume(Time.unscaledDeltaTime, out _)) Save();
        }

        /// <summary>
        /// 전투가 놓고 간 신호를 재화로 바꾼다. **적립 규칙이 사는 유일한 자리다** —
        /// 전투 코드가 지갑을 직접 만지면 규칙이 두 곳으로 흩어진다.
        /// 신호는 가져가며 비우므로 같은 처치·클리어를 두 번 세지 않는다.
        /// </summary>
        private void CreditSignals()
        {
            int kills = IdleSignals.DrainKills();
            if (kills > 0)
            {
                double perKill = economy != null ? economy.scrapPerKillTbd : 0d;
                Wallet.AddScrap(KillRewardRule.Scrap(kills, perKill));
                Data.totalKills += kills;

                // ⚠️⚠️ **골드 적립이 사는 유일한 자리다**(2026-09-18 · 고철과 같은 규약).
                //    화면은 규칙을 다시 세지 않고 **결과만** 읽는다 — 아래 `ReportGoldAwarded`
                //    가 그 통로이며, 남은 처치 수는 **저장 칸 하나**가 든다(지침 §7).
                int gold = GoldRewardRule.Award(
                    ref Data.killsTowardGold, kills,
                    economy != null ? economy.killsPerGoldAward : 0,
                    economy != null ? economy.goldPerAward : 0);
                if (gold > 0)
                {
                    Wallet.AddGold(gold);
                    IdleSignals.ReportGoldAwarded(gold);
                }
            }

            // 마일스톤 보상 — **한 번만 청구된다**(카드가 제 안에서 잠근다).
            // ⚠️ 여기서 다시 「받았는가」를 세지 않는다 — 두 곳이 세면 어긋나는 날이 온다.
            if (IdleSignals.TryDrainMilestoneReward(out int mGold, out double mScrap))
            {
                Wallet.AddGold(mGold);
                Wallet.AddScrap(mScrap);
            }

            // 파밍 바퀴 기록 — 스테이지별 **최고 한 바퀴 시급**만 남는다(기획서 6장).
            // ⚠️ 적는 자리는 여기 하나다. 전투 쪽이 세이브를 직접 만지면 규칙이 흩어진다.
            if (IdleSignals.TryDrainFarmLap(out string lapStage, out float lapRate))
                Data.TryRecordFarmRate(lapStage, lapRate);

            if (IdleSignals.TryDrainClear(out ClearReport clear))
            {
                // 최초 클리어에만 강화재료. 재지급하면 Σ(S1~S3)=s4Cost인 닫힌 곡선이 무너진다.
                if (Data.MarkCleared(clear.stageId))
                    Wallet.AddEnhMaterial(clear.enhMaterialReward);

                // ⚠️ **깬 그 자리에서 다음 판을 연다** — 다음 실행까지 기다리면 통과 직후의
                //    목록에서 아직 잠겨 있다. 저장에 쓴 **뒤** 사본을 갱신한다(원천은 저장이다).
                IdleSignals.NoteClearedStage(clear.stageId);

                // ⚠️ **깬 그 자리에서 B 를 연다**(2026-10-13 · #84). 다음 실행까지 기다리면
                //    S4 를 깬 직후의 화면에서는 아직 잠겨 있다 — 깨자마자 열려야 말이 된다.
                //    ⚠️ `MarkCleared` 가 거짓(이미 깬 판)이어도 다시 읽는다 — 저장이 참이면 참이다.
                if (!RobotBGate.S4Cleared)
                    RobotBGate.S4Cleared = Data.HasCleared(RobotBGate.UnlockStageId);
            }
        }

        /// <summary>
        /// 꺼둔 동안의 보상을 지급한다(§5-7). 상주 스테이지 기록 한 곳만 쓰고, 고철만 준다.
        /// 계수·기본 시급이 TBD(0)면 지급이 0이 된다 — 미확정을 기본값으로 덮어 감추지 않는다.
        /// </summary>
        private void SettleOffline()
        {
            double coef = economy != null ? economy.offlineCoefTbd : 0d;
            double baseRate = economy != null ? economy.offlineBaseRateTbd : 0d;
            // 오프라인 값 셋이 한 자산에 산다 (2026-10-13 · 상한이 BalanceConfig 에서 옮겨 왔다).
            double capHours = economy != null ? economy.offlineCapHours : 36d;

            OfflineRewardResult r = OfflineRewardCalculator.FromSave(Data, _clock, coef, capHours, baseRate);
            LastOfflineReward = r;

            if (r.scrap > 0d) Wallet.AddScrap(r.scrap);
            PublishWallet(); // 첫 프레임 HUD가 오프라인 지급까지 반영된 잔액을 읽도록
        }

        /// <summary>잔액을 중립 채널에 게시한다 — 전투 HUD가 여기서 읽는다(IdleSignals 주석).</summary>
        private void PublishWallet()
        {
            IdleSignals.WalletScrap = Wallet.Scrap;
            IdleSignals.WalletEnhMaterial = Wallet.EnhMaterial;
            IdleSignals.WalletGold = Wallet.Gold;
            IdleSignals.KillsTowardGold = Data != null ? Data.killsTowardGold : 0;

            // ⚠️ **마리당 고철을 여기서 게시한다** — 전투 화면의 드롭 팝이 「+n 스크랩」을
            //    적으려면 그 값이 필요한데, 전투가 `EconomyConfig` 를 직접 들면
            //    **같은 값을 두 자산에서 읽는 자리**가 생긴다. 규칙도 값도 여기 하나다.
            IdleSignals.ScrapPerKill = economy != null ? economy.scrapPerKillTbd : 0d;
        }

        // 웹빌드에서 탭 전환·최소화가 여기로 온다. 종료(OnApplicationQuit)는 브라우저에서 보장되지
        // 않으므로 의존하지 않고, 이 두 지점 + 주기 저장으로 커버한다.
        private void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Save();
        }

        /// <summary>지갑·접속 시각을 세이브에 반영해 저장한다.</summary>
        public void Save()
        {
            if (Data == null || _store == null) return;

            Data.scrap = Wallet.Scrap;
            Data.enhMaterial = Wallet.EnhMaterial;
            Data.gold = Wallet.Gold;   // ⚠️ `killsTowardGold` 는 CreditSignals 가 이미 Data 에 직접 쓴다
            Data.lastSeenUtcTicks = _clock.UtcNow.Ticks; // 꺼둔 시간 계산의 기준점

            // ⚠️⚠️ **끈 시점의 상주 파밍 판**(2026-10-13 · 순서 6). 이 한 줄이 없어서
            //    오프라인 정산이 **기록을 한 번도 쓰지 못하고** 기본 시급으로만 떨어졌다 —
            //    저장 칸은 처음부터 있었고 쓰는 쪽이 없었다.
            //
            // ⚠️ **빈 글자로는 덮지 않는다.** 전투 씬에 들어가기 전에 도는 자동저장이
            //    저장된 판 이름을 지우면, 다음에 켤 때 정산할 판이 사라진다
            //    (보드 저장과 같은 까닭이다 · 아래 주석).
            if (!string.IsNullOrEmpty(StageLayerSignals.FarmStageId))
                Data.lastFarmStageId = StageLayerSignals.FarmStageId;

            // 보드 한 판. ⚠️ 물류가 아직 안 섬으면 `null` 이고, 그때는
            //    **새로 쓰지 않고 불러온 것을 그대로 다시 싣는다** — 그러지 않으면
            //    판이 서기 전에 도는 자동저장이 저장된 판을 지운다.
            foreach (MountOwner o in BoardOwners)
            {
                BoardStateV1 live = IdleSignals.BoardStateOf(o);
                if (live != null) Data.SetBoard(o, live);
            }
            _store.Save(Data);
        }

        /// <summary>세이브 삭제 후 초기 상태로. 시연·디버그용.</summary>
        public void ResetSave()
        {
            _store.Delete();
            Data = new SaveDataV1();
            // 저장을 지웠으면 보드도 **둘 다** 같이 지운다
            foreach (MountOwner o in BoardOwners) IdleSignals.SetBoardState(o, null);
            Wallet = new CurrencyWallet();
        }
    }
}
