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

                if (merger)
                {
                    // 병합기는 **어느 쪽에서 와도 받는다** — 남는 입력면은 경고를 안 부른다
                    // (`BeltRouting.Classify` 의 「안 들어오는 것과 못 나가는 것은 다르다」).
                    b.Reorient(rest.ToArray(), new[] { single });
                }
                else
                {
                    // ⚠️⚠️ **분류기의 출력면도 이웃을 보고 잡는다** (2026-09-17).
                    //
                    // 종전에는 **입력면을 뺀 셋 전부**를 출력면으로 박았다. 시작 보드는
                    // 그중 둘만 쓰므로 남는 한 면이 늘 「열린 끝」이 되고, **다 이어 놓은
                    // 판에 「나가는 곳이 없다」가 영영 떴다**(2026-09-17 실측 — 채운 판 (9,9)).
                    //
                    // 📌 **경고 규칙을 고치지 않았다.** 「출구가 비면 막힌 것」은 그대로 옳고,
                    //    고칠 것은 **안 쓸 면을 출구라고 적어 둔 쪽**이다. 이 클래스의 이름과
                    //    주석이 처음부터 「이웃에서 다시 잡는다」였는데 출력면만 이웃을
                    //    안 보고 있었다 — 입력면 쪽은 보고 있었다.
                    //
                    // ⚠️ **이웃이 하나도 없으면 셋 전부로 둔다** — 아직 배치 중일 수 있고,
                    //    여기서 면을 0 개로 만들면 다음에 벨트를 붙여도 안 이어진다.
                    var usable = new List<PortFace>(3);
                    foreach (PortFace f in rest)
                        if (CanReceive(grid, cell, f)) usable.Add(f);

                    b.Reorient(new[] { single },
                        usable.Count > 0 ? usable.ToArray() : rest.ToArray());
                }

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
            // 2) 없으면 중간(벨트) — **그 벨트가 이쪽에서 오는 것을 받는 면인지 본다.**
            //
            // ⚠️ **종전에는 안 봤다**(2026-09-11 실측). 면 순서대로 **아무 벨트나** 골라서,
            // 위 칸이 서쪽에서만 받는 벨트인데도 북쪽을 출력면으로 잡았다 —
            // 시작 보드 네 줄의 합류가 통째로 엉뚱한 쪽으로 흘렀다.
            // 이 메서드의 이름과 주석은 처음부터 「받아 줄 수 있는가」였고, 노드 분기는
            // `HasPort` 로 제대로 확인하고 있었다. **벨트 분기만 그 확인이 빠져 있었다.**
            foreach (PortFace f in FaceOrder)
            {
                Vector2Int nb = cell + BeltRouting.Delta(f);
                if (!grid.IsInside(nb) || grid.GetAt(nb) != null) continue;

                BeltInstance nbBelt = grid.GetBeltAt(nb);
                if (nbBelt == null) continue;
                if (!Receives(nbBelt, NodeConnectionRules.Opposite(f))) continue;
                return f;
            }

            // 받아 줄 벨트가 없으면 **붙어 있기만 한 벨트**라도 고른다 — 아직 배치 중일 수 있고,
            // 여기서 손을 놓으면 다음 배치에서 다시 잡힐 기회가 사라진다.
            foreach (PortFace f in FaceOrder)
            {
                Vector2Int nb = cell + BeltRouting.Delta(f);
                if (grid.IsInside(nb) && grid.GetAt(nb) == null && grid.GetBeltAt(nb) != null) return f;
            }

            // 아직 아무것도 안 붙었다. 동쪽으로 두고 이웃이 붙으면 다시 잡힌다.
            return PortFace.East;
        }

        /// <summary>
        /// 그 면의 이웃이 **이 칸에서 오는 것을 받을 수 있는가** — 분류기 출력면 판정.
        /// 노드면 맞은 면에 입력 포트가 있어야 하고, 벨트면 그 면으로 받는 벨트여야 한다.
        /// ⚠️ **마운트 고정 포트도 받는 곳이다** — 운반로의 끝이 노드가 아닐 수 있다.
        /// </summary>
        private static bool CanReceive(BoardGrid grid, Vector2Int cell, PortFace face)
        {
            Vector2Int nb = cell + BeltRouting.Delta(face);

            if (PartLayout.TryGetMountPort(cell, face, grid.Owner, out _)) return true;
            if (!grid.IsInside(nb)) return false;

            NodeInstance node = grid.GetAt(nb);
            if (node != null) return HasPort(node, PortIO.Input, NodeConnectionRules.Opposite(face));

            BeltInstance nbBelt = grid.GetBeltAt(nb);
            return nbBelt != null && Receives(nbBelt, NodeConnectionRules.Opposite(face));
        }

        /// <summary>이 벨트가 그 면으로 들어오는 것을 받는가.</summary>
        private static bool Receives(BeltInstance belt, PortFace face)
        {
            PortFace[] faces = belt.InFaces;
            if (faces == null) return false;
            for (int i = 0; i < faces.Length; i++)
                if (faces[i] == face) return true;
            return false;
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
            foreach (NodePort p in node.Ports())
                if (p.io == io && p.face == face) return true;
            return false;
        }
    }
}
