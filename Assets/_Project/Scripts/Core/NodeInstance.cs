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

        public NodeInstance(NodeDefinition definition, Vector2Int cell)
        {
            Definition = definition;
            Cell = cell;
        }
    }
}
