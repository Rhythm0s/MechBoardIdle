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
    /// 벨트 체인 1건(§5-4 ⑤): 벨트끼리 이어진 한 덩어리와 그 끝단.
    /// 끝단(Terminals) = 체인 내부 연결이 한쪽뿐(또는 없음)인 셀. 순환 벨트는 끝단 0개.
    /// 분류기(다중 출력)·병합기(다중 입력)면 끝단이 2개를 넘을 수 있어 리스트로 둔다.
    /// </summary>
    public struct BeltChain
    {
        public List<Vector2Int> cells;
        public List<Vector2Int> terminals;  // 체인 밖과 맞닿는 면을 가진 셀(= 끝단)
        public int nodeSides;               // 끝단 면 중 노드에 실제 접속된 수
        public int openSides;               // 끝단 면 중 아무것도 안 붙은 수
        public bool partiallyConnected;     // nodeSides>0 && openSides>0 → 배선 미완성
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
                    // ⚠️ 나가는 것은 **포트에 적힌 종류가 아니라 조합표가 정한다**.
                    // 군수 노드의 출력 포트는 「탄약」 하나뿐인데 추진제를 돌리면 추진제가 나간다 —
                    // 포트만 보면 추진제 라인이 부스터에 링크가 서지 않는다(BeltFlow와 같은 원천).
                    FlowKind outKind = BeltFlow.OutputKindOf(node);
                    foreach (NodePort p in node.Definition.ports)
                        if (p.io == PortIO.Output)
                            TryLink(grid, links, cell, p.face, outKind);
                    continue;
                }
                BeltInstance belt = grid.GetBeltAt(cell);
                if (belt != null)
                    foreach (PortFace outFace in belt.OutFaces) // 분류기: 다중 출력면
                        TryLink(grid, links, cell, outFace, belt.Kind);
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
            if (nbBelt != null && nbBelt.Kind == kind && HasInFace(nbBelt, need)) // 병합기: 다중 입력면 수용
                links.Add(new BeltLink { fromCell = cell, toCell = nb, kind = kind });
        }

        /// <summary>벨트가 해당 면을 입력면으로 갖는가(병합기 다중 입력 포함).</summary>
        private static bool HasInFace(BeltInstance belt, PortFace face)
        {
            foreach (PortFace f in belt.InFaces) if (f == face) return true;
            return false;
        }

        private static bool HasInputPort(NodeDefinition def, PortFace face, FlowKind kind)
        {
            if (def == null) return false;
            foreach (NodePort p in def.ports)
                if (p.io == PortIO.Input && p.face == face && p.kind == kind) return true;
            return false;
        }

        /// <summary>
        /// 벨트 체인 분해 + 끝단 접속 판정(§5-4 ⑤, 순수·결정론).
        /// `BuildLinks` 결과를 재사용한다 — 링크 성립 규칙(면 반대·FlowKind 일치)을 여기서 다시 정의하지 않는다(§3).
        /// 판정은 **면 단위**다: 벨트가 노드에 닿기만 한 건 연결이 아니고, 올바른 입출력 면으로 링크가 서야 접속이다.
        /// 분류기(다중 출력)·병합기(다중 입력)도 면별로 세므로 한 출구만 비어도 잡힌다.
        /// </summary>
        public static List<BeltChain> BuildChains(BoardGrid grid)
        {
            var chains = new List<BeltChain>();
            if (grid == null) return chains;

            var linkSet = new HashSet<(Vector2Int from, Vector2Int to)>();
            foreach (BeltLink l in BuildLinks(grid)) linkSet.Add((l.fromCell, l.toCell));

            var visited = new HashSet<Vector2Int>();
            for (int x = 0; x < grid.Columns; x++)
            for (int y = 0; y < grid.Rows; y++)
            {
                var start = new Vector2Int(x, y);
                if (grid.GetBeltAt(start) == null || visited.Contains(start)) continue;

                // 벨트↔벨트 링크만 따라가며 한 덩어리를 수집(BFS, 결정론적 시작점).
                var cells = new List<Vector2Int>();
                var terminals = new List<Vector2Int>();
                int nodeSides = 0, openSides = 0;

                var queue = new Queue<Vector2Int>();
                queue.Enqueue(start);
                visited.Add(start);

                while (queue.Count > 0)
                {
                    Vector2Int cell = queue.Dequeue();
                    cells.Add(cell);
                    BeltInstance belt = grid.GetBeltAt(cell);
                    bool isTerminal = false;

                    foreach (PortFace f in belt.InFaces)
                        Classify(grid, linkSet, cell, f, true, queue, visited,
                                 ref nodeSides, ref openSides, ref isTerminal);
                    foreach (PortFace f in belt.OutFaces)
                        Classify(grid, linkSet, cell, f, false, queue, visited,
                                 ref nodeSides, ref openSides, ref isTerminal);

                    if (isTerminal) terminals.Add(cell);
                }

                chains.Add(new BeltChain
                {
                    cells = cells,
                    terminals = terminals,
                    nodeSides = nodeSides,
                    openSides = openSides,
                    partiallyConnected = nodeSides > 0 && openSides > 0,
                });
            }
            return chains;
        }

        // 면 1개 분류: 체인 내부(벨트) / 노드 접속 / 미접속. 체인 내부면 BFS 큐에 넣는다.
        private static void Classify(BoardGrid grid, HashSet<(Vector2Int, Vector2Int)> linkSet,
            Vector2Int cell, PortFace face, bool isInFace,
            Queue<Vector2Int> queue, HashSet<Vector2Int> visited,
            ref int nodeSides, ref int openSides, ref bool isTerminal)
        {
            Vector2Int nb = cell + Delta(face);
            // 입력면은 이웃→나, 출력면은 나→이웃 방향의 링크가 서야 접속이다.
            bool linked = isInFace ? linkSet.Contains((nb, cell)) : linkSet.Contains((cell, nb));

            if (linked && grid.GetBeltAt(nb) != null)
            {
                if (visited.Add(nb)) queue.Enqueue(nb);
                return; // 체인 내부 — 끝단 아님
            }

            isTerminal = true;
            if (linked) nodeSides++; // 링크가 섰는데 벨트가 아니면 노드
            else openSides++;
        }

        /// <summary>
        /// 경고 아이콘을 띄울 셀(§5-4 ⑤ 사양): 한쪽만 접속된 체인의 **끝단 전부**.
        /// 양끝 미접촉(작업 중) · 양끝 접속(완성)은 표시하지 않는다.
        /// </summary>
        public static List<Vector2Int> DanglingWarningCells(BoardGrid grid)
        {
            var cells = new List<Vector2Int>();
            foreach (BeltChain c in BuildChains(grid))
                if (c.partiallyConnected) cells.AddRange(c.terminals);
            return cells;
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
