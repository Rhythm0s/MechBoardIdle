using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>방향 연결 1건: fromCell의 출력이 toCell의 입력으로 흐름(kind).</summary>
    public struct BeltLink
    {
        public Vector2Int fromCell;
        public Vector2Int toCell;
        public FlowKind kind;
    }

    /// <summary>
    /// 면 자동연결(§5-4 L2, 순수·결정론). 보드의 노드+벨트를 훑어 방향 연결 그래프를 만든다.
    /// 성립: 출력(노드 Output 포트 or 벨트 OutFace) → 인접 셀의 입력(노드 Input 포트 or 벨트 InFace),
    ///       맞닿은 면이 반대이고 FlowKind가 같을 때. `NodeConnectionRules.Opposite` 재사용.
    /// 스텁 노드(implemented=false, 포트 없음)는 자연히 제외된다.
    /// </summary>
    public static class BeltRouting
    {
        public static List<BeltLink> BuildLinks(BoardGrid grid)
        {
            var links = new List<BeltLink>();
            if (grid == null) return links;

            for (int x = 0; x < grid.Columns; x++)
            for (int y = 0; y < grid.Rows; y++)
            {
                var cell = new Vector2Int(x, y);
                NodeInstance node = grid.GetAt(cell);
                if (node != null)
                {
                    foreach (NodePort p in node.Definition.ports)
                        if (p.io == PortIO.Output)
                            TryLink(grid, links, cell, p.face, p.kind);
                    continue;
                }
                BeltInstance belt = grid.GetBeltAt(cell);
                if (belt != null)
                    TryLink(grid, links, cell, belt.OutFace, belt.Kind);
            }
            return links;
        }

        private static void TryLink(BoardGrid grid, List<BeltLink> links,
            Vector2Int cell, PortFace outFace, FlowKind kind)
        {
            Vector2Int nb = cell + Delta(outFace);
            if (!grid.IsInside(nb)) return;
            PortFace need = NodeConnectionRules.Opposite(outFace); // 이웃이 맞닿는 면

            NodeInstance nbNode = grid.GetAt(nb);
            if (nbNode != null)
            {
                if (HasInputPort(nbNode.Definition, need, kind))
                    links.Add(new BeltLink { fromCell = cell, toCell = nb, kind = kind });
                return;
            }

            BeltInstance nbBelt = grid.GetBeltAt(nb);
            if (nbBelt != null && nbBelt.InFace == need && nbBelt.Kind == kind)
                links.Add(new BeltLink { fromCell = cell, toCell = nb, kind = kind });
        }

        private static bool HasInputPort(NodeDefinition def, PortFace face, FlowKind kind)
        {
            if (def == null) return false;
            foreach (NodePort p in def.ports)
                if (p.io == PortIO.Input && p.face == face && p.kind == kind) return true;
            return false;
        }

        /// <summary>면 방향의 셀 델타.</summary>
        public static Vector2Int Delta(PortFace face)
        {
            switch (face)
            {
                case PortFace.East: return new Vector2Int(1, 0);
                case PortFace.West: return new Vector2Int(-1, 0);
                case PortFace.North: return new Vector2Int(0, 1);
                default: return new Vector2Int(0, -1); // South
            }
        }
    }
}
