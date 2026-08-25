using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 격자에 배치된 노드 1기의 순수 논리 표현(§5-3).
    /// NodeDefinition(스키마)와 놓인 셀 좌표만 담는다.
    ///
    /// GameObject/비주얼 참조는 두지 않는다 — 씬 표현(마커 등)은 BoardController가
    /// 셀→GameObject 매핑으로 따로 관리한다(Core 순수성 유지, EditMode 테스트 가능).
    /// </summary>
    public sealed class NodeInstance
    {
        public readonly NodeDefinition Definition;
        public readonly Vector2Int Cell;

        /// <summary>
        /// 이 노드가 만드는 탄종(군수 노드에만 의미 — 다른 타입에서는 읽지 않는다).
        ///
        /// 노드 1개 = 1발/초이고 탄종은 **노드별로 지정**된다(260824_V02 §1).
        /// 노드 카탈로그를 3종으로 쪼개는 대신 인스턴스 속성으로 둔 이유는 **노드 6종 확정**을 지키기 위해서다.
        ///
        /// 기본값이 관통인 근거: `origin`(원점 100)의 basis가 「관통탄 20×5발 **기본 라인**」이다.
        /// 관통이 원천상 기본 라인이므로 임의 선택이 아니다.
        /// </summary>
        public AmmoKind AmmoKind { get; set; } = AmmoKind.Pierce;

        public NodeInstance(NodeDefinition definition, Vector2Int cell)
        {
            Definition = definition;
            Cell = cell;
        }
    }
}
