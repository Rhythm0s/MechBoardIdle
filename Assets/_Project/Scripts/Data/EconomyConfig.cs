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

        [Header("운영 (비밸런스 — §3 분리)")]
        [Tooltip("자동 저장 주기(초). 웹빌드엔 원자적 쓰기가 없어 너무 잦으면 손상 위험만 커진다.")]
        public float autosaveIntervalSeconds = 30f;

        /// <summary>확정 전까지는 파밍 수입이 계산되지 않는다는 사실을 감추지 않기 위한 표식.</summary>
        public bool HasConfirmedEconomy => scrapPerKillTbd > 0d && offlineCoefTbd > 0d;
    }
}
