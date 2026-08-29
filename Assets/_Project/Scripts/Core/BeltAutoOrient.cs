using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 병합기·분류기의 면을 **이웃에서 다시 잡는다**(순수·결정론, 260829_V03 §판정③).
    ///
    /// 왜 배치 시점에 안 정하는가: 요소를 먼저 놓고 나중에 이웃을 붙이는 순서가 자연스러운데,
    /// 그때 방향을 고정해 두면 조용히 안 이어진 채로 남는다. 벨트 품목(<see cref="BeltFlow"/>)과
    /// 같은 이유로 **배치가 바뀔 때마다** 다시 잡는다.
    ///
    /// 규칙:
    ///   - 병합기 = 여러 면으로 받아 **한 면으로** 낸다. 출력면 = 받아 줄 이웃이 있는 면
    ///   - 분류기 = 한 면으로 받아 **여러 면으로** 낸다. 입력면 = 내보낼 이웃이 있는 면
    ///   - **노드가 벨트보다 우선한다.** 벨트는 중간이고 노드가 목적지다 —
    ///     안 그러면 코어 옆 병합기가 코어 대신 옆 벨트를 가리킨다
    ///   - 나머지 세 면은 반대 역할을 맡는다. 아무것도 안 붙은 면은 그냥 안 이어질 뿐이라
    ///     남겨 둬도 해가 없고, 덕분에 붙이는 순서가 자유로워진다
    ///
    /// 면 우선순위는 북 → 동 → 남 → 서 고정이다. 같은 보드가 언제나 같은 결과가 돼야 한다.
    ///
    /// ⚠️ 품목(FlowKind)은 보지 않는다 — 품목은 이 결과 위에서 <see cref="BeltFlow"/>가 정하므로,
    /// 여기서 품목을 보면 두 계산이 서로를 기다리게 된다.
    /// </summary>
    public static class BeltAutoOrient
    {
        private static readonly PortFace[] FaceOrder =
        {
            PortFace.North, PortFace.East, PortFace.South, PortFace.West,
        };

        /// <summary>보드의 모든 병합기·분류기 방향을 다시 잡는다. 방향이 잡힌 요소 수를 돌려준다.</summary>
        public static int Resolve(BoardGrid grid)
        {
            if (grid == null) return 0;

            int touched = 0;
            for (int x = 0; x < grid.Columns; x++)
            for (int y = 0; y < grid.Rows; y++)
            {
                var cell = new Vector2Int(x, y);
                BeltInstance b = grid.GetBeltAt(cell);
                if (b == null) continue;
                if (b.Element != BeltElementKind.Merger && b.Element != BeltElementKind.Sorter) continue;

                bool merger = b.Element == BeltElementKind.Merger;
                PortFace single = merger ? FindConsumerFace(grid, cell) : FindProducerFace(grid, cell);

                var rest = new List<PortFace>(3);
                foreach (PortFace f in FaceOrder)
                    if (f != single) rest.Add(f);

                if (merger) b.Reorient(rest.ToArray(), new[] { single });
                else b.Reorient(new[] { single }, rest.ToArray());

                touched++;
            }
            return touched;
        }

        /// <summary>그 면의 이웃이 **받아 줄 수 있는가**(노드 입력 포트 또는 벨트). 병합기 출력면.</summary>
        private static PortFace FindConsumerFace(BoardGrid grid, Vector2Int cell)
        {
            // 1) 목적지(노드) 먼저.
            foreach (PortFace f in FaceOrder)
            {
                NodeInstance node = NodeAt(grid, cell, f);
                if (node != null && HasPort(node, PortIO.Input, NodeConnectionRules.Opposite(f))) return f;
            }
            // 2) 없으면 중간(벨트).
            foreach (PortFace f in FaceOrder)
            {
                Vector2Int nb = cell + BeltRouting.Delta(f);
                if (grid.IsInside(nb) && grid.GetAt(nb) == null && grid.GetBeltAt(nb) != null) return f;
            }
            // 아직 아무것도 안 붙었다. 동쪽으로 두고 이웃이 붙으면 다시 잡힌다.
            return PortFace.East;
        }

        /// <summary>그 면의 이웃이 **내보낼 수 있는가**(노드 출력 포트 또는 벨트). 분류기 입력면.</summary>
        private static PortFace FindProducerFace(BoardGrid grid, Vector2Int cell)
        {
            foreach (PortFace f in FaceOrder)
            {
                NodeInstance node = NodeAt(grid, cell, f);
                if (node != null && HasPort(node, PortIO.Output, NodeConnectionRules.Opposite(f))) return f;
            }
            foreach (PortFace f in FaceOrder)
            {
                Vector2Int nb = cell + BeltRouting.Delta(f);
                if (grid.IsInside(nb) && grid.GetAt(nb) == null && grid.GetBeltAt(nb) != null) return f;
            }
            return PortFace.West;
        }

        private static NodeInstance NodeAt(BoardGrid grid, Vector2Int cell, PortFace face)
        {
            Vector2Int nb = cell + BeltRouting.Delta(face);
            return grid.IsInside(nb) ? grid.GetAt(nb) : null;
        }

        private static bool HasPort(NodeInstance node, PortIO io, PortFace face)
        {
            if (node.Definition == null || node.Definition.ports == null) return false;
            foreach (NodePort p in node.Definition.ports)
                if (p.io == io && p.face == face) return true;
            return false;
        }
    }
}
