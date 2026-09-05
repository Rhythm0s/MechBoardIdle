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
    ///   - **코어는 허브라 언제나 센다.** 이제 모이는 곳이 아니라 **원천**이지만(W03 1장),
    ///     어느 쪽이든 배선 여부로 껐다 켰다 할 대상이 아니라는 점은 같다
    ///   - **산출이 있는 노드**는 그 산출이 **다른 노드나 마운트 고정 포트에 도달**해야 센다.
    ///     받아 줄 곳 없이 만드는 것은 라인이 아니다
    ///
    ///     ⚠️ **마운트도 도착지다**(2026-09-05 · `260904_W03` 1장). 코어가 탄약을 받던 동안에는
    ///     모든 라인이 노드에서 끝나 노드 도달만 보면 됐지만, 코어가 시작이 되면서 라인의 끝이
    ///     마운트가 됐다. 노드만 보면 **라인의 마지막 노드가 통째로 0이 된다** — 플레이어가
    ///     가장 신경 쓰는 그 노드가 조용히 안 세어지고, 출력이 낮게 나오되 일정하게 낮아
    ///     결함으로도 안 보인다. <see cref="BeltItemFlow"/>가 아이템을 넘길 때 쓰는 규칙과
    ///     같은 규칙을 여기서도 쓴다
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

            // 산출이 있는 노드: 다른 노드나 마운트에 도달하면 라인에 속한다.
            var fed = new HashSet<Vector2Int>();
            foreach (Vector2Int p in producers)
            {
                List<Vector2Int> reachedNodes =
                    NodesReachableFrom(grid, next, p, out bool reachedMount);
                if (reachedNodes.Count == 0 && !reachedMount) continue;

                connected.Add(p);
                foreach (Vector2Int r in reachedNodes) fed.Add(r);
            }

            // 코어에서 나가는 것도 공급이다(가공이 코어의 산출을 받는다).
            foreach (Vector2Int c in connected)
                if (grid.GetAt(c) != null && grid.GetAt(c).Definition.type == NodeType.Core)
                    foreach (Vector2Int r in NodesReachableFrom(grid, next, c, out _)) fed.Add(r);

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
                if (IsAmmo(l.kind) && grid.GetAt(l.fromCell) != null && grid.GetAt(l.toCell) != null)
                    paths++;

            // 탄약을 나르는 체인. 어느 쪽 끝도 노드에 안 붙은 체인은 아무것도 안 나른다.
            foreach (BeltChain chain in BeltRouting.BuildChains(grid))
            {
                if (chain.nodeSides <= 0 || chain.cells == null) continue;

                foreach (Vector2Int cell in chain.cells)
                {
                    BeltInstance b = grid.GetBeltAt(cell);
                    if (b == null || !IsAmmo(b.Kind)) continue;
                    paths++;
                    break; // 체인 하나가 경로 하나다
                }
            }

            return paths;
        }

        /// <summary>
        /// 탄약으로 세는 품목. 구 <c>FlowKind.Ammo</c> 하나였던 것이 W01 3-2 개정으로
        /// 표준탄·관통탄·폭발탄 셋으로 갈렸다 — 하나만 보면 **대역이 통째로 0이 된다.**
        /// 드론은 탄약이 아니라 별도 장치라 여기 넣지 않는다.
        /// </summary>
        private static bool IsAmmo(FlowKind kind)
        {
            switch (kind)
            {
                case FlowKind.Ammo:            // 폐기 — 구 자산 호환용으로만 남는다
                case FlowKind.StandardAmmo:
                case FlowKind.PierceAmmo:
                case FlowKind.ExplosiveAmmo:
                    return true;
                default:
                    return false;
            }
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

        /// <summary>
        /// 출발 셀에서 링크를 타고 닿을 수 있는 **노드** 셀들. 출발점 자신은 빼고 센다.
        /// <paramref name="reachedMount"/>는 도중에 **마운트 고정 포트**로 나가는 칸을 지났는지다 —
        /// 그 칸에서 라인이 끝나므로 노드 목록에는 안 잡히지만 도착지가 있는 것은 맞다.
        /// </summary>
        private static List<Vector2Int> NodesReachableFrom(BoardGrid grid,
            Dictionary<Vector2Int, List<Vector2Int>> next, Vector2Int start, out bool reachedMount)
        {
            var found = new List<Vector2Int>();
            var seen = new HashSet<Vector2Int> { start };
            var stack = new Stack<Vector2Int>();
            stack.Push(start);
            reachedMount = ExitsToMount(grid, start);

            while (stack.Count > 0)
            {
                Vector2Int cur = stack.Pop();
                if (!next.TryGetValue(cur, out List<Vector2Int> outs)) continue;

                foreach (Vector2Int nb in outs)
                {
                    if (!seen.Add(nb)) continue;

                    if (ExitsToMount(grid, nb)) reachedMount = true;
                    if (grid.GetAt(nb) != null) { found.Add(nb); continue; } // 노드에 닿았다 — 여기서 멈춘다
                    stack.Push(nb);                                          // 벨트면 계속 간다
                }
            }
            return found;
        }

        /// <summary>
        /// 이 칸의 출력면이 마운트 고정 포트로 향하는가. 포트는 격자 밖을 보므로
        /// <see cref="BeltRouting.BuildLinks"/>가 링크를 만들지 않는다 — 여기서 따로 본다.
        /// 벨트든 노드든 같은 규칙이다(노드를 마운트에 직접 붙일 수도 있다).
        /// </summary>
        private static bool ExitsToMount(BoardGrid grid, Vector2Int cell)
        {
            NodeInstance node = grid.GetAt(cell);
            if (node != null)
            {
                if (node.Definition == null || node.Definition.ports == null) return false;
                foreach (NodePort p in node.Definition.ports)
                    if (p.io == PortIO.Output && PartLayout.TryGetMountPort(cell, p.face, out _)) return true;
                return false;
            }

            BeltInstance belt = grid.GetBeltAt(cell);
            if (belt == null || belt.OutFaces == null) return false;
            foreach (PortFace f in belt.OutFaces)
                if (PartLayout.TryGetMountPort(cell, f, out _)) return true;
            return false;
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
