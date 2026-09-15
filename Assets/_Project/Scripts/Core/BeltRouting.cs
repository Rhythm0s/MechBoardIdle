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
                    foreach (NodePort p in node.Ports())
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
            // ⚠️ **전력은 벨트로 안 나른다**(2026-09-11 · 문서와 구현 불일치 해소).
            //
            // 밸런스 문서의 전력망은 **전역**이다. 그런데 에너지 노드에 `Power` 출력 포트가
            // 있어서 여기가 링크를 세웠고, 그 링크가 코어 남면의 `Power` 입력으로 이어졌다 —
            // **벨트로 전기를 나르는 그림**이 코드에만 있었다.
            //
            // 포트는 남긴다. 화면에서 「여기가 발전소다」를 보여 주는 **표시**이고,
            // 지우면 보드 아트 4장의 마커가 근거를 잃는다.
            if (kind == FlowKind.Power) return;

            Vector2Int nb = cell + Delta(outFace);
            if (!grid.IsInside(nb)) return;
            PortFace need = NodeConnectionRules.Opposite(outFace); // 이웃이 맞닿는 면

            NodeInstance nbNode = grid.GetAt(nb);
            if (nbNode != null)
            {
                if (HasInputPort(nbNode, need, kind))
                    links.Add(new BeltLink { fromCell = cell, toCell = nb, kind = kind });
                return;
            }

            // ⚠️ **벨트끼리는 품목을 안 가린다**(2026-09-14 사용자 확정 · §72-29).
            //
            // 종전에는 `nbBelt.Kind == kind` 로 **이웃 벨트의 품목까지** 맞춰야 링크가 섰다.
            // 그래서 표준탄 줄과 폭발탄 줄을 병합기로 모으면 **한 종류로 굳고 나머지가
            // 끊겼고**, 한 마운트 포트에 두 탄종이 영영 못 닿았다(하네스 `203897e`).
            //
            // 벨트는 **아이템 단위**다 — 위에 품목이 각자 놓여 섞여 흐르고, 병합기·분류기는
            // 품목이 달라도 합류·분배한다. **품목을 가리는 곳은 노드 입력 하나뿐**이며
            // 그것은 위 `HasInputPort` 가 조합표로 판정한다(`0a973da` 그대로).
            BeltInstance nbBelt = grid.GetBeltAt(nb);
            if (nbBelt != null && HasInFace(nbBelt, need)) // 병합기: 다중 입력면 수용
                links.Add(new BeltLink { fromCell = cell, toCell = nb, kind = kind });
        }

        /// <summary>벨트가 해당 면을 입력면으로 갖는가(병합기 다중 입력 포함).</summary>
        private static bool HasInFace(BeltInstance belt, PortFace face)
        {
            foreach (PortFace f in belt.InFaces) if (f == face) return true;
            return false;
        }

        /// <summary>
        /// 그 노드가 이 면으로 이 품목을 **받는가**.
        ///
        /// ⚠️ **면은 자산이 정하고 품목은 조합표가 정한다**(2026-09-14 · §72-19 ·
        /// 사용자 판정 (2) · 조립 3장 「노드 코드를 건드리지 않고 데이터만 늘려 레시피를
        /// 추가할 수 있어야 한다」의 이행).
        ///
        /// ⚠️ **왜 바꿨는가.** 종전에는 `p.kind == kind` 로 **포트에 적힌 품목까지** 대조했다.
        /// 포트 품목은 노드마다 **하나로 박혀** 있어(가공 = 기초재료·부품 · 복합 군수 남면 =
        /// 기초재료·부품), 조합표를 바꿔도 면이 그것을 안 받았다. 실측으로는 같은 판에서
        /// **관통탄 53개 / 폭발탄 0개**였고, 발전재료를 내는 가공이 **라인에서 통째로
        /// 빠졌다**(2026-09-14 · `ShootBoardProbe`). 조합표에는 폭발탄·드론·배터리가
        /// 다 있는데 **면이 없어 격자 위에 한 줄도 못 서던** 자리다.
        ///
        /// 이제 **면만 자산이 정하고**, 그 면이 무엇을 받는지는 **지금 돌리는 조합표의
        /// 입력 목록**이 정한다. 포트에 적힌 품목은 **기본값 표시**로 남는다.
        ///
        /// ⚠️ **저장 노드는 전부 받는다** — 조합표가 없고 「무엇이든 맡아 둔다」가 그 뜻이다.
        /// </summary>
        private static bool HasInputPort(NodeInstance node, PortFace face, FlowKind kind)
        {
            if (node?.Definition == null) return false;

            // 면이 열려 있는가 — 이것만 자산이 정한다.
            bool faceOpen = false;
            foreach (NodePort p in node.Ports())
                if (p.io == PortIO.Input && p.face == face) { faceOpen = true; break; }
            if (!faceOpen) return false;

            // ⚠️ **저장만 무엇이든 받는다** — 「맡아 둔다」가 그 뜻이다.
            //
            // 「조합표가 없으면 전부」로 넓게 잡았더니 **부스터가 탄약을 받았다**
            // (시험 `AmmoLine_DoesNotFeedTheBooster`). 부스터·쉴드도 조합표가 없지만
            // 받는 것은 정해져 있다 — 그 둘은 포트에 적힌 품목이 그대로 잣대다.
            if (node.Definition.type == NodeType.Storage) return true;

            List<NodeRecipe> recipes = node.Definition.recipes;
            if (recipes == null || recipes.Count == 0)
            {
                foreach (NodePort p in node.Ports())
                    if (p.io == PortIO.Input && p.face == face && p.kind == kind) return true;
                return false;
            }

            // 지금 돌리는 조합표가 먹는 것인가.
            NodeRecipe current = node.CurrentRecipe;
            if (current.inputs == null) return false;
            foreach (RecipeInput i in current.inputs)
                if (i.kind == kind) return true;

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
            if (linked) { nodeSides++; return; } // 링크가 섰는데 벨트가 아니면 노드

            // ⚠️⚠️ **마운트도 나가는 곳이다**(2026-09-15 · 사용자 육안 5차 ⑦ · 실측).
            //
            // 종전에는 벨트가 **노드**로 이어질 때만 「이어졌다」로 봤다. 그런데 운반로의
            // 끝은 노드가 아니라 **로봇 마운트**다 — 거기로 들어간 탄이 실제로 쌓인다
            // (`BeltItemFlow` 가 `PartLayout.TryGetMountPort` 로 그 출구를 안다).
            //
            // 그래서 **같은 판을 두 규칙이 다르게 읽고 있었다** — 물건은 멀쩡히 도착하는데
            // (실측 90초 339개) 화면에는 **「나가는 곳이 없다」**가 떴다. 사용자가
            // 「이미 다 되어 있는데 어떻게 하라는 거냐」고 물은 자리가 이것이다.
            //
            // ⚠️ **진실이 둘이면 둘 다 고쳐야 하는 것이 아니라, 하나를 없애야 한다**(지침 §7).
            // 출구를 아는 쪽은 `PartLayout` 이므로 여기서도 그것에 묻는다.
            if (!isInFace && PartLayout.TryGetMountPort(cell, face, out _)) { nodeSides++; return; }

            // ⚠️⚠️ **비어 있는 입력면은 「새는 곳」이 아니다**
            // (2026-09-15 · 육안 6차 ⑦ 재조사 · 시작 보드를 두 줄로 줄이며 드러났다).
            //
            // 경고 문구는 **「나가는 곳이 없다」**인데, 종전에는 **들어오는 면**이 비어 있어도
            // 같이 세고 있었다. 병합기는 `MergerInFaces` 가 **출력면을 뺀 세 면 전부**를
            // 입력으로 선언하므로, 셋 중 둘만 쓰면 **남는 한 면이 늘 「열린 끝」**이 된다 —
            // 그러면 그 체인의 **끝단이 전부** 경고를 받는다. 멀쩡한 판에서 경고가
            // 여섯 칸씩 뜨던 까닭이 이것이다.
            //
            // 📌 **안 들어오는 것과 못 나가는 것은 다르다.** 앞은 그냥 물건이 안 오는
            // 것이고(병합기 설계 그대로), 뒤는 **물건이 갈 데가 없어 막힌다.**
            // 잡아야 할 것은 뒤쪽 하나다.
            if (isInFace) return;

            openSides++;
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
