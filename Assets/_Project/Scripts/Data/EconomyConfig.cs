using UnityEngine;

namespace MBI.Data
{
    /// <summary>
    /// 경제 미확정치(§5-7). ⚠️ 전부 TBD placeholder — 확정 밸런스가 아니다.
    ///
    /// `BalanceConfig`(밸런스 계약 미러)와 **분리한다.** 계약 SO는 생성기가 재실행될 때마다
    /// json 값으로 덮어쓰므로, 미확정치를 거기 넣으면 인스펙터에서 만져 본 값이 조용히 날아간다.
    /// `CombatTuning`·`LogisticsConfig`·`BoardConfig`가 같은 이유로 분리돼 있다(§3).
    /// 생성기는 이 자산을 만들기만 하고(LoadOrCreate) 값은 덮어쓰지 않는다.
    ///
    /// 확정 경로: 검증 대장 실측 → Notion 기획서 → `balance_v4.json` → 여기로 역기입.
    /// 확정되면 `confirmed: true`가 되고, 그때 계약 SO로 승격할지 판단한다.
    /// </summary>
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "MBI/Economy Config (TBD)", order = 21)]
    public sealed class EconomyConfig : ScriptableObject
    {
        [Header("재화 (⚠️ TBD — balance_v4 economy 그룹, confirmed:false)")]
        [Tooltip("TBD — 마리당 고철. 노드 가격 카탈로그와 결합 확정. 원천: 스테이지 기획서「파밍 규칙」")]
        public double scrapPerKillTbd = 2d;

        [Tooltip("TBD — 오프라인 계수. 1 미만이어야 '꺼두는 편이 이득'이 되지 않는다. 원천: 스테이지 기획서「오프라인 보상」")]
        public double offlineCoefTbd = 0.5d;

        [Tooltip("TBD — 상주 스테이지에 파밍 기록이 없을 때 쓰는 시급(고철/시간). 0 = 미측정 센티넬(지급 0).")]
        public double offlineBaseRateTbd = 0d;

        [Header("골드 (2026-09-18 사용자 확정 — TBD 아님)")]
        [Tooltip("몇 마리마다 골드를 주는가. 사용자 확정 20 — 「몬스터 20처치마다 5골드」.")]
        public int killsPerGoldAward = 20;

        [Tooltip("한 번에 주는 골드. 사용자 확정 5. ⚠️ 활용처는 아직 없다(설계 몫).")]
        public int goldPerAward = 5;

        /// <summary>
        /// 오프라인 보상 인정 상한(시간) — **2026-10-13 에 `BalanceConfig` 에서 옮겨 왔다**(사용자 확정 ③).
        ///
        /// 오프라인 값 셋(상한 · 계수 · 기본 시급)이 **한 자산 한 표**에 모이게 하려는 것이다.
        ///
        /// ⚠️ **확정치다.** 원천은 `balance_v4.json` 의 `economy.offline.capHours = 36` 이며,
        /// 옮겨 온 것은 **칸뿐**이고 값도 확정 여부도 안 바뀌었다.
        /// 같은 자산에 있는 `offlineCoefTbd`·`offlineBaseRateTbd` 는 미확정치다 — 한 묶음이지만
        /// 확정 여부는 저마다 다르다.
        /// </summary>
        [Tooltip("오프라인 보상 인정 상한(시간). ECONOMY 표 offlineCapHours. ⚠️ 미확정 — 설계 역기입 자리.")]
        public float offlineCapHours = 36f;

        [Header("운영 (비밸런스 — §3 분리)")]
        [Tooltip("자동 저장 주기(초). 웹빌드엔 원자적 쓰기가 없어 너무 잦으면 손상 위험만 커진다.")]
        public float autosaveIntervalSeconds = 30f;

        // ── 소비 · 해금 · 초기 지급 (2026-10-19 사용자 확정 #172 · #173 · #174 · ECONOMY 표 5~10행) ──

        [Header("소비 (#172) · 해금 (#173) · 초기 지급 (#174)")]
        [Tooltip("노드 설치 고철(종류 무관). 벨트·병합기·분류기는 0. 떼기·지우기 환불 없음. #172 확정.")]
        public int nodeInstallScrapCost = 20;

        [Tooltip("모듈 장착 고철. 환불 없음. #172 확정.")]
        public int moduleInstallScrapCost = 30;

        [Tooltip("마운트 강화 고철 — 재료(s4Cost)와 **둘 다** 있어야 한다. #172 확정.")]
        public int enhanceScrapCost = 300;

        [Tooltip("로봇 B 해금 골드(S4 파밍부터 단추). #173 확정.")]
        public int robotBUnlockGoldCost = 50;

        [Tooltip("새 저장 · 「처음부터」 때 한 번 주는 고철. #174 확정.")]
        public int startingScrap = 300;

        [Tooltip("새 저장 · 「처음부터」 때 한 번 주는 골드. #174 확정.")]
        public int startingGold = 50;

        /// <summary>확정 전까지는 파밍 수입이 계산되지 않는다는 사실을 감추지 않기 위한 표식.</summary>
        public bool HasConfirmedEconomy => scrapPerKillTbd > 0d && offlineCoefTbd > 0d;
    }
}
