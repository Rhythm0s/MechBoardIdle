using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 벨트가 **무엇을 나르는가**를 상류에서 흘려 내린다(순수·결정론).
    ///
    /// 왜 필요한가: 벨트를 놓을 때는 그 벨트가 뭘 나를지 알 수 없다. 상류에 무엇이 붙느냐가
    /// 나중에 정해지기 때문이다. 그래서 설치 시점에는 품목을 정하지 못하고, **배치가 바뀔 때마다
    /// 다시 흘려야** 한다.
    ///
    /// ⚠️ 이것이 없으면 벨트가 아예 연결되지 않는다. <see cref="BeltRouting.BuildLinks"/>는
    /// 벨트↔벨트 링크에 **FlowKind 일치**를 요구하는데, 설치 시 임의 품목으로 깔린 벨트는
    /// 군수 노드(탄약)의 출력과 종류가 안 맞아 링크가 서지 않는다.
    ///
    /// 전파 규칙:
    ///   - 출발점은 **노드의 출력 포트**다. 벨트는 스스로 품목을 만들지 않는다
    ///   - 벨트가 그 면을 입력면으로 가질 때만 받는다 — 닿기만 한 건 연결이 아니다
    ///   - 받은 벨트는 자기 출력면으로 그대로 넘긴다(분류기는 여러 면으로)
    ///   - 아무것도 안 닿은 벨트는 <see cref="FlowKind.None"/> — 「비어 있다」가 값으로 남는다
    ///
    /// 두 상류가 서로 다른 품목을 같은 벨트로 밀면 **먼저 닿은 쪽이 이긴다**. 마운트 슬롯이
    /// 「먼저 도착한 것이 칸을 차지한다」인 것과 같은 규칙이라 따로 배울 것이 없고,
    /// 순회 순서가 결정론적이라 같은 보드는 언제나 같은 결과가 된다.
    /// </summary>
    public static class BeltFlow
    {
        /// <summary>
        /// 보드 전체의 벨트 품목을 다시 계산한다. 품목이 정해진 벨트 수를 돌려준다.
        /// 배치·제거 직후에 부른다 — 노드 하나를 빼면 그 하류 라인이 통째로 비어야 한다.
        /// </summary>
        public static int Resolve(BoardGrid grid)
        {
            if (grid == null) return 0;

            // 먼저 전부 비운다. 안 비우면 상류 노드를 뽑아도 옛 품목이 남아
            // 라인이 계속 살아 있는 것처럼 보인다.
            var belts = new List<BeltInstance>();
            for (int x = 0; x < grid.Columns; x++)
            for (int y = 0; y < grid.Rows; y++)
            {
                BeltInstance b = grid.GetBeltAt(new Vector2Int(x, y));
                if (b == null) continue;
                b.Kind = FlowKind.None;
                belts.Add(b);
            }
            if (belts.Count == 0) return 0;

            var queue = new Queue<BeltInstance>();

            // 씨앗 = 노드의 출력 포트. 결정론을 위해 셀 순서대로 돈다.
            for (int x = 0; x < grid.Columns; x++)
            for (int y = 0; y < grid.Rows; y++)
            {
                var cell = new Vector2Int(x, y);
                NodeInstance node = grid.GetAt(cell);
                if (node == null || node.Definition == null || node.Definition.ports == null) continue;

                FlowKind output = OutputKindOf(node);
                foreach (NodePort p in node.Definition.ports)
                {
                    if (p.io != PortIO.Output) continue;
                    Feed(grid, cell, p.face, output, queue);
                }
            }

            // 체인을 따라 내려보낸다.
            while (queue.Count > 0)
            {
                BeltInstance b = queue.Dequeue();
                foreach (PortFace outFace in b.OutFaces)
                    Feed(grid, b.Cell, outFace, b.Kind, queue);
            }

            int resolved = 0;
            for (int i = 0; i < belts.Count; i++)
                if (belts[i].Kind != FlowKind.None) resolved++;
            return resolved;
        }

        /// <summary>
        /// 그 노드가 실제로 내보내는 품목.
        ///
        /// ⚠️ **포트에 적힌 종류가 아니라 조합표가 정한다.** 군수 노드의 출력 포트는 단일이고
        /// 「탄약」으로 적혀 있지만, 추진제 조합표를 돌리면 나오는 것은 추진제다. 포트만 보면
        /// 추진제를 만드는 노드에서 탄약 벨트가 뻗어 나가 부스터에 안 붙는다.
        /// </summary>
        public static FlowKind OutputKindOf(NodeInstance node)
        {
            if (node == null || node.Definition == null) return FlowKind.None;

            NodeRecipe recipe = node.CurrentRecipe;
            if (recipe.IsRunnable) return recipe.output;

            // 조합표가 없는 노드(코어·에너지·저장)는 포트에 적힌 것이 그대로 산출이다.
            foreach (NodePort p in node.Definition.ports)
                if (p.io == PortIO.Output) return p.kind;

            return FlowKind.None;
        }

        /// <summary>한 칸의 출력면 → 이웃 벨트에 품목을 넘긴다. 이미 정해진 벨트는 건드리지 않는다.</summary>
        private static void Feed(BoardGrid grid, Vector2Int cell, PortFace outFace, FlowKind kind,
            Queue<BeltInstance> queue)
        {
            if (kind == FlowKind.None) return;

            Vector2Int nb = cell + BeltRouting.Delta(outFace);
            if (!grid.IsInside(nb)) return;

            BeltInstance belt = grid.GetBeltAt(nb);
            if (belt == null || belt.Kind != FlowKind.None) return; // 노드이거나, 먼저 닿은 쪽이 이미 정했다

            // 벨트가 그 면을 **입력면으로** 가져야 받는다. 닿기만 한 건 연결이 아니다.
            PortFace need = NodeConnectionRules.Opposite(outFace);
            bool accepts = false;
            foreach (PortFace f in belt.InFaces) if (f == need) accepts = true;
            if (!accepts) return;

            belt.Kind = kind;
            queue.Enqueue(belt);
        }

        /// <summary>그 칸의 벨트가 나르는 품목. 벨트가 없거나 비었으면 None.</summary>
        public static FlowKind KindAt(BoardGrid grid, Vector2Int cell)
        {
            BeltInstance b = grid != null ? grid.GetBeltAt(cell) : null;
            return b != null ? b.Kind : FlowKind.None;
        }
    }
}
