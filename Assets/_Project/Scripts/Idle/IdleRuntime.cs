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
            Wallet = new CurrencyWallet(Data.scrap, Data.enhMaterial);

            // 튜토리얼을 이미 했는지 전투 쪽에 알린다. **Awake에서 한다** —
            // Stage0Session이 자기 Awake에서 이 값을 읽어 들어갈지 말지를 정하므로,
            // 실행 순서(-100 → -50)가 뒤집히면 매번 튜토리얼이 다시 열린다.
            IdleSignals.TutorialCleared = Data.HasCleared(IdleSignals.TutorialId);

            SettleOffline();
        }

        private void Update()
        {
            CreditSignals();
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
            }

            if (IdleSignals.TryDrainClear(out ClearReport clear))
            {
                // 최초 클리어에만 강화재료. 재지급하면 Σ(S1~S3)=s4Cost인 닫힌 곡선이 무너진다.
                if (Data.MarkCleared(clear.stageId))
                    Wallet.AddEnhMaterial(clear.enhMaterialReward);
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
            double capHours = balance != null ? balance.offlineCapHours : 36d;

            OfflineRewardResult r = OfflineRewardCalculator.FromSave(Data, _clock, coef, capHours, baseRate);
            LastOfflineReward = r;

            if (r.scrap > 0d) Wallet.AddScrap(r.scrap);
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
            Data.lastSeenUtcTicks = _clock.UtcNow.Ticks; // 꺼둔 시간 계산의 기준점
            _store.Save(Data);
        }

        /// <summary>세이브 삭제 후 초기 상태로. 시연·디버그용.</summary>
        public void ResetSave()
        {
            _store.Delete();
            Data = new SaveDataV1();
            Wallet = new CurrencyWallet();
        }
    }
}
