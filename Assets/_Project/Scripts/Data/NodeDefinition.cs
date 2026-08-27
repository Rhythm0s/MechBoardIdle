using System.Collections.Generic;
using UnityEngine;

namespace MBI.Data
{
    /// <summary>
    /// 물류 노드 정의(단일 원천 SO). CLAUDE.md §5-2 핵심 산출물.
    ///
    /// 노드 6종을 NodeType enum으로 구분한다. S1~S4 등장 5종(Core~Storage)은 implemented=true,
    /// 6번째 Shield(쉴드 발생)는 implemented=false 스텁(스키마 자리만, 구현 보류 — §4 스코프).
    ///
    /// 공통 변수(전력/탄약/발열)는 NodeResourceProfile, 면별 입출력은 ports가 담는다.
    /// 모든 수치는 이 .asset에 저장 — 코드 리터럴 금지(§3).
    /// </summary>
    [CreateAssetMenu(fileName = "Node", menuName = "MBI/Node Definition", order = 1)]
    public sealed class NodeDefinition : ScriptableObject
    {
        [Header("정체")]
        [Tooltip("안정 키. 예: core / proc / muni / ener / stor / shield.")]
        public string nodeId;
        [Tooltip("표시명. 예: 코어 / 가공 / 군수 / 에너지 / 저장 / 쉴드 발생.")]
        public string displayName;
        [Tooltip("노드 6종 구분.")]
        public NodeType type;
        [Tooltip("false = 스키마 자리만(쉴드 발생). 연결 규칙·시뮬은 이 노드를 스킵.")]
        public bool implemented = true;

        [Header("공통 변수 — 전력 / 탄약 / 발열")]
        public NodeResourceProfile resources;

        [Header("조합표 — 이 종류의 노드가 고를 수 있는 레시피 후보")]
        [Tooltip("노드 한 대는 이 중 **하나만** 돌린다(260827_V01 §3). 레시피 추가는 여기 한 행을 늘리는 것이고 노드 코드는 건드리지 않는다.")]
        public List<NodeRecipe> recipes = new List<NodeRecipe>();

        [Header("연결 — 면별 입출력 포트")]
        [Tooltip("각 면(N/E/S/W)의 입력/출력과 흐르는 자원 종류. NodeConnectionRules가 사용.")]
        public List<NodePort> ports = new List<NodePort>();

        [Header("참조")]
        [Tooltip("전역 밸런스 앵커(원점/천장/enh) 단일 원천.")]
        public BalanceConfig balanceRef;
    }
}
