using UnityEngine;

namespace MBI.Data
{
    /// <summary>
    /// 물류 흐름 시뮬 병목 파라미터(§5-5). ⚠️ 전부 TBD placeholder — balance "병목" 그룹(confirmed:false).
    ///
    /// CLAUDE.md §3 역할 경계: 밸런스 계약과 분리(CombatTuning·BoardConfig 선례). balance_v4 병목 그룹에서
    /// 시드하되 미확정 — 사용자 chat+Notion 확정 대상. LogisticsSimulation이 라이브 네트워크 집계와 함께 사용.
    ///
    /// 데이터흐름(§9): 전력=고정비 전용(강화 불가·긴장 영구화). 물류 무개입(효율은 물리에서만).
    /// </summary>
    [CreateAssetMenu(fileName = "LogisticsConfig", menuName = "MBI/Logistics Config (TBD)", order = 20)]
    public sealed class LogisticsConfig : ScriptableObject
    {
        [Header("전력 (⚠️ TBD)")]
        [Tooltip("TBD — 전 노드 가동 고정비 합. balance pw = 66. 효율 = min(1, 공급/소비).")]
        public float powerDraw = 66f;
        [Tooltip("TBD — 발전 공급 용량. balance pwc = 80. pwc<pw면 효율<1(전력 긴장).")]
        public float powerSupply = 80f;

        [Header("벨트 (⚠️ TBD)")]
        [Tooltip("⚠️ TBD — **한 줄(노랑 등급)**의 처리 용량(/초). balance belt = 14. 총 대역 = 경로 수 × 이 값 — 길이는 대역이 아니라 지연을 늘리므로 칸 수로 세지 않는다(260829_V03 §판정②). 빨강·파랑 등급 값은 미확정(검증 대장).")]
        public float beltCapacity = 14f;

        [Header("발열 (⚠️ TBD)")]
        [Tooltip("⚠️ TBD — 모듈 F의 냉각량(/초). 0 = 미측정 센티넬. 냉각은 **노드의 값이 아니다** — 구 냉각 노드가 2026-07-02에 모듈 F로 전환됐다(260829_V03). 모듈 시스템이 서면 이 필드가 그쪽으로 간다.")]
        public float moduleCoolingTbd = 0f;

        [Tooltip("TBD — 발열 발생(/초). balance heat = 8. ⚠️ 네트워크 합계다 — 노드 대당 값은 노드 SO가 든다.")]
        public float heatGenerate = 8f;
        [Tooltip("TBD — 발열 임계. balance heatc = 12. heat>heatc면 감쇠.")]
        public float heatThreshold = 12f;

        [Tooltip("⚠️ TBD — 저장 노드 1개가 더하는 재고 용량(발). 0 = 미확정 센티넬. 40발이 어디에 붙는 용량인지(저장 노드 없이도 40인지, 노드 하나가 40인지, 기본 재고에 얹는지)가 문서에서 갈리지 않았다 — 검증 대장 이월.")]
        public float storageCapacityPerNodeTbd = 0f;
    }
}
