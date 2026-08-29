using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// **이어진 노드만 센다**(260829_V03 §판정① A안, 순수·결정론).
    ///
    /// 이것이 없던 동안 집계는 격자에 놓인 노드를 **전부** 셌고, 벨트를 아예 보지 않았다.
    /// 그래서 코어까지 한 칸도 안 이어져도 출력이 그대로 나왔다 —
    /// 「물류 **라인**을 최적화하는 행위가 재미있는가」에서 최적화할 라인이 코드에 없었다.
    ///
    /// 판정 규칙(링크 성립 자체는 <see cref="BeltRouting.BuildLinks"/>가 정한다 — 여기서 다시 만들지 않는다):
    ///   - **코어는 허브라 언제나 센다.** 물류가 모이는 곳이지 어딘가로 보내는 곳이 아니다
    ///   - **산출이 있는 노드**는 그 산출이 **다른 노드에 도달**해야 센다.
    ///     받아 줄 곳 없이 만드는 것은 라인이 아니다
    ///   - **받기만 하는 노드**(부스터)는 **이어진 공급원이 도달**해야 센다.
    ///     추진제가 안 오는 부스터는 회피를 못 만든다
    ///
    /// ⚠️ 호출 전에 <see cref="BeltAutoOrient"/> → <see cref="BeltFlow"/> 순으로 풀려 있어야 한다.
    /// 면이 안 잡히면 링크가 안 서고, 품목이 안 흐르면 벨트끼리도 안 이어진다.
    /// 이 클래스는 격자를 **읽기만** 한다.
    /// </summary>
    public static class LogisticsReach
    {
        /// <summary>집계에 들어갈 노드 셀. 벨트 셀은 담지 않는다.</summary>
        public static HashSet<Vector2Int> ConnectedNodes(BoardGrid grid)
        {
            var connected = new HashSet<Vector2Int>();
            if (grid == null) return connected;

            List<BeltLink> links = BeltRouting.BuildLinks(grid);
            Dictionary<Vector2Int, List<Vector2Int>> next = BuildAdjacency(links);

            var producers = new List<Vector2Int>();
            var consumers = new List<Vector2Int>();

            for (int x = 0; x < grid.Columns; x++)
            for (int y = 0; y < grid.Rows; y++)
            {
                var cell = new Vector2Int(x, y);
                NodeInstance node = grid.GetAt(cell);
                if (node == null || node.Definition == null || !node.Definition.implemented) continue;

                if (node.Definition.type == NodeType.Core) { connected.Add(cell); continue; }

                if (HasOutput(node)) producers.Add(cell);
                else consumers.Add(cell);
            }

            // 산출이 있는 노드: 다른 노드에 도달하면 라인에 속한다.
            var fed = new HashSet<Vector2Int>();
            foreach (Vector2Int p in producers)
            {
                List<Vector2Int> reachedNodes = NodesReachableFrom(grid, next, p);
                if (reachedNodes.Count == 0) continue;

                connected.Add(p);
                foreach (Vector2Int r in reachedNodes) fed.Add(r);
            }

            // 코어에서 나가는 것도 공급이다(가공이 코어의 산출을 받는다).
            foreach (Vector2Int c in connected)
                if (grid.GetAt(c) != null && grid.GetAt(c).Definition.type == NodeType.Core)
                    foreach (Vector2Int r in NodesReachableFrom(grid, next, c)) fed.Add(r);

            // 받기만 하는 노드: 이어진 공급원이 닿아야 센다.
            foreach (Vector2Int c in consumers)
                if (fed.Contains(c)) connected.Add(c);

            return connected;
        }

        /// <summary>
        /// 탄약이 흐르는 **경로 수**. 총 대역 = 경로 수 × 한 줄 처리량(벨트 등급)이다 —
        /// 길이는 대역이 아니라 지연을 늘리므로 칸 수로 세지 않는다(260829_V03 §판정②).
        ///
        /// 경로 = 탄약을 나르는 벨트 체인 하나, 또는 벨트를 안 거치고 노드끼리 직접 선 탄약 링크 하나
        /// (벨트 0칸짜리 경로다 — 직접 붙였다고 대역이 없어지지는 않는다).
        /// </summary>
        public static int AmmoPathCount(BoardGrid grid)
        {
            if (grid == null) return 0;

            int paths = 0;

            // 노드 ↔ 노드 직결.
            foreach (BeltLink l in BeltRouting.BuildLinks(grid))
                if (l.kind == FlowKind.Ammo && grid.GetAt(l.fromCell) != null && grid.GetAt(l.toCell) != null)
                    paths++;

            // 탄약을 나르는 체인. 어느 쪽 끝도 노드에 안 붙은 체인은 아무것도 안 나른다.
            foreach (BeltChain chain in BeltRouting.BuildChains(grid))
            {
                if (chain.nodeSides <= 0 || chain.cells == null) continue;

                foreach (Vector2Int cell in chain.cells)
                {
                    BeltInstance b = grid.GetBeltAt(cell);
                    if (b == null || b.Kind != FlowKind.Ammo) continue;
                    paths++;
                    break; // 체인 하나가 경로 하나다
                }
            }

            return paths;
        }

        private static Dictionary<Vector2Int, List<Vector2Int>> BuildAdjacency(List<BeltLink> links)
        {
            var next = new Dictionary<Vector2Int, List<Vector2Int>>();
            foreach (BeltLink l in links)
            {
                if (!next.TryGetValue(l.fromCell, out List<Vector2Int> to))
                {
                    to = new List<Vector2Int>();
                    next[l.fromCell] = to;
                }
                to.Add(l.toCell);
            }
            return next;
        }

        /// <summary>출발 셀에서 링크를 타고 닿을 수 있는 **노드** 셀들. 출발점 자신은 빼고 센다.</summary>
        private static List<Vector2Int> NodesReachableFrom(BoardGrid grid,
            Dictionary<Vector2Int, List<Vector2Int>> next, Vector2Int start)
        {
            var found = new List<Vector2Int>();
            var seen = new HashSet<Vector2Int> { start };
            var stack = new Stack<Vector2Int>();
            stack.Push(start);

            while (stack.Count > 0)
            {
                Vector2Int cur = stack.Pop();
                if (!next.TryGetValue(cur, out List<Vector2Int> outs)) continue;

                foreach (Vector2Int nb in outs)
                {
                    if (!seen.Add(nb)) continue;

                    if (grid.GetAt(nb) != null) { found.Add(nb); continue; } // 노드에 닿았다 — 여기서 멈춘다
                    stack.Push(nb);                                          // 벨트면 계속 간다
                }
            }
            return found;
        }

        private static bool HasOutput(NodeInstance node)
        {
            if (node.Definition == null || node.Definition.ports == null) return false;
            foreach (NodePort p in node.Definition.ports)
                if (p.io == PortIO.Output) return true;
            return false;
        }
    }
}
