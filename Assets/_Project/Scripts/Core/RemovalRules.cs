using System.Collections.Generic;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// **무엇을 지울 때 묻는가** (2026-09-15 사용자 확정 · 육안 ⑧).
    ///
    /// 제거는 모드가 아니라 **제스처**다 — 벨트 위에서 끌면 지워진다. 그런데 손이 지나간
    /// 자리에 노드가 섞여 있으면 얘기가 다르다.
    ///
    /// · **벨트만** — 즉시 지운다. 한 칸이 곧 한 조각이고 다시 끌면 되돌아온다.
    ///   물어보는 값이 지우는 값보다 작다.
    /// · **노드가 하나라도** — 묻는다. 노드는 조합표·탄종·모듈·회전을 지고 있어서
    ///   **다시 끄는 것으로 안 돌아온다.**
    ///
    /// ⚠️ **묻고 나서는 경로 전체를 지운다** — 벨트만 지우고 노드를 남기면 손이 지나간
    /// 자리와 결과가 달라진다.
    ///
    /// 순수 정적 — 격자를 직접 안 본다(<paramref name="isNode"/> 로 받는다). EditMode 에서 돈다.
    /// </summary>
    public static class RemovalRules
    {
        /// <summary>이 경로를 지우기 전에 한 번 더 물어야 하는가.</summary>
        public static bool NeedsConfirm(IReadOnlyList<Vector2Int> cells,
            System.Func<Vector2Int, bool> isNode)
        {
            if (cells == null || isNode == null) return false;
            for (int i = 0; i < cells.Count; i++)
                if (isNode(cells[i])) return true;
            return false;
        }

        /// <summary>경로 안의 노드 수 — 물음 문구가 「노드 n대를 포함해」라고 말한다.</summary>
        public static int NodeCount(IReadOnlyList<Vector2Int> cells,
            System.Func<Vector2Int, bool> isNode)
        {
            if (cells == null || isNode == null) return 0;
            int n = 0;
            for (int i = 0; i < cells.Count; i++) if (isNode(cells[i])) n++;
            return n;
        }
    }
}
