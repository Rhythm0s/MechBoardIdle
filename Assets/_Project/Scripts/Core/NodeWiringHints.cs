using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>노드 한 면의 화살표 한 개 — 어느 면이고 어느 쪽을 향하는가.</summary>
    public readonly struct PortHint
    {
        public readonly Vector2Int cell;
        public readonly PortFace face;

        /// <summary>참이면 **밖으로**(출구) · 거짓이면 **안으로**(입구).</summary>
        public readonly bool outward;

        public PortHint(Vector2Int cell, PortFace face, bool outward)
        {
            this.cell = cell;
            this.face = face;
            this.outward = outward;
        }
    }

    /// <summary>
    /// **아직 다 안 이어진 노드의 입출력 면을 화살표로 알린다**
    /// (2026-09-16 신설 · 사용자 확정 · 플랜 §74-3 #34).
    ///
    /// **왜 필요한가.** 노드를 놓고 나면 **어느 면으로 들어오고 어느 면으로 나가는지**를
    /// 화면이 안 말한다. 포트 탭이 있긴 하나 작고, 「이 노드에 아직 할 일이 남았다」를
    /// 말하지는 않는다 — 플레이어는 벨트를 어디 붙일지 찍어 볼 수밖에 없다.
    ///
    /// ⚠️⚠️ **다 이어진 노드에는 안 그린다**(사용자 확정). 안내는 **할 일이 남았을 때만**
    /// 뜻이 있다 — 다 이은 판에까지 화살표가 남으면 **보드 전체가 화살표 밭**이 되고,
    /// 그때는 무엇을 봐야 하는지가 오히려 흐려진다. **이어지는 순간 사라진다.**
    ///
    /// 📌 **판정을 새로 만들지 않는다.** 면이 이어졌는가는
    /// <see cref="BeltRouting.BuildLinks"/> 가 정한다 — 링크 성립 규칙(면 반대 · 품목 일치)을
    /// 여기서 다시 쓰면 **같은 물음에 답이 둘**이 된다(지침 §7).
    /// 마운트 고정 포트도 **나가는 곳**으로 센다 — `LogisticsReach` 가 이미 그렇게 읽는다.
    /// </summary>
    public static class NodeWiringHints
    {
        /// <summary>
        /// 판 전체를 한 번에 본다 — **링크를 한 번만 짓는다.**
        /// 노드마다 따로 부르면 칸 수만큼 링크를 다시 짓게 된다.
        /// </summary>
        public static List<PortHint> Collect(BoardGrid grid)
        {
            var hints = new List<PortHint>();
            if (grid == null) return hints;

            // 링크를 방향째로 담는다 — 입력면은 이웃 → 나, 출력면은 나 → 이웃이다.
            var links = new HashSet<(Vector2Int, Vector2Int)>();
            foreach (BeltLink l in BeltRouting.BuildLinks(grid)) links.Add((l.fromCell, l.toCell));

            for (int x = 0; x < grid.Columns; x++)
            for (int y = 0; y < grid.Rows; y++)
            {
                var cell = new Vector2Int(x, y);
                NodeInstance node = grid.GetAt(cell);
                if (node?.Definition?.ports == null) continue;

                IReadOnlyList<NodePort> ports = node.Ports();
                if (ports.Count == 0) continue;

                if (IsWired(grid, links, cell, ports)) continue;

                for (int i = 0; i < ports.Count; i++)
                    hints.Add(new PortHint(cell, ports[i].face, ports[i].io == PortIO.Output));
            }

            return hints;
        }

        /// <summary>그 노드가 **다 이어졌는가**(시험·진단용).</summary>
        public static bool IsFullyWired(BoardGrid grid, Vector2Int cell)
        {
            NodeInstance node = grid?.GetAt(cell);
            if (node?.Definition?.ports == null) return true;

            IReadOnlyList<NodePort> ports = node.Ports();
            if (ports.Count == 0) return true;

            var links = new HashSet<(Vector2Int, Vector2Int)>();
            foreach (BeltLink l in BeltRouting.BuildLinks(grid)) links.Add((l.fromCell, l.toCell));
            return IsWired(grid, links, cell, ports);
        }

        /// <summary>
        /// **다 이어졌는가** — 규칙 셋 (2026-09-16 · ⚠️ 셋째·둘째는 구현 판단 · 설계 확인 요망).
        ///
        /// ① **입력면은 전부 이어져야 한다.** 복합 군수는 둘 중 하나만 물려도 안 돈다 —
        ///    그때가 바로 「할 일이 남은」 자리다.
        ///
        /// ② **출력면은 하나만 이어지면 된다.** ⚠️ 첫 판은 「전부」로 두었다가
        ///    **코어에 화살표 넷이 영영 남았다** — 코어는 출력면이 넷인데 시작 보드가 둘만
        ///    쓰고, 남은 둘은 `StartingBoard` 가 「**플레이어가 줄을 더 놓을 자리**」라고
        ///    적어 둔 것이다. 그것은 **할 일이 아니라 여지**다.
        ///
        /// ③ **전력 포트는 벨트를 안 문다**(`FlowKind.Power`). 전력망은 전역이라
        ///    에너지 노드는 놓기만 하면 발전한다 — `StartingBoard` 가 「벨트를 안 문다」로
        ///    확정해 둔 자리다. 이어진 것으로 센다. ⚠️ 첫 판은 이것도 안 봐서
        ///    **에너지 셋에 화살표가 영영 남았다.**
        ///
        /// 📌 셋 다 **시험이 잡았다** — 「다 이은 시작 보드에 화살표가 0 개」가 안 서서
        ///    규칙이 덜 여물었음이 드러났다.
        /// </summary>
        private static bool IsWired(BoardGrid grid,
            HashSet<(Vector2Int, Vector2Int)> links, Vector2Int cell, IReadOnlyList<NodePort> ports)
        {
            bool hasOutput = false, anyOutputWired = false;

            for (int i = 0; i < ports.Count; i++)
            {
                NodePort p = ports[i];

                // ③ 전력은 벨트 축이 아니다.
                if (p.kind == FlowKind.Power) continue;

                if (p.io == PortIO.Input)
                {
                    if (!FaceIsWired(grid, links, cell, p)) return false;   // ①
                    continue;
                }

                hasOutput = true;
                if (FaceIsWired(grid, links, cell, p)) anyOutputWired = true;
            }

            return !hasOutput || anyOutputWired;   // ②
        }

        private static bool FaceIsWired(BoardGrid grid,
            HashSet<(Vector2Int, Vector2Int)> links, Vector2Int cell, NodePort port)
        {
            Vector2Int nb = cell + Delta(port.face);

            if (port.io == PortIO.Output)
            {
                // ⚠️ **마운트 고정 포트도 나가는 곳이다** — 격자 밖을 향하므로 링크가 안 선다.
                //    이것을 안 보면 운반로 끝 노드가 영영 「덜 이어진」 것으로 남는다.
                if (PartLayout.TryGetMountPort(cell, port.face, grid.Owner, out _)) return true;
                return links.Contains((cell, nb));
            }

            return links.Contains((nb, cell));
        }

        private static Vector2Int Delta(PortFace face)
        {
            switch (face)
            {
                case PortFace.North: return new Vector2Int(0, 1);
                case PortFace.East: return new Vector2Int(1, 0);
                case PortFace.South: return new Vector2Int(0, -1);
                default: return new Vector2Int(-1, 0);
            }
        }
    }
}
