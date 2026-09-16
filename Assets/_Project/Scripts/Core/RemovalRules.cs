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

        /// <summary>
        /// **지우는 드래그인가** (2026-09-15 사용자 확정 · 개편 ① 개정).
        ///
        /// 규칙은 **시작 칸에 무엇이 있는가** 하나다 —
        /// · **빈 칸**에서 시작 → 벨트 **설치**(탭이든 드래그든)
        /// · **노드나 벨트**가 있는 칸에서 시작 → **제거**(둘을 안 가린다)
        ///
        /// ⚠️ **한 칸(탭)은 제거가 아니다.** 벨트를 탭하면 아무 일도 없고 노드를 탭하면
        /// 팝오버가 뜬다 — 실수로 한 번 눌러 지워지는 일이 없다(09-15 판정거리가 닫혔다).
        ///
        /// ⚠️ **코어에서 시작하면 아무것도 안 지운다.** 코어는 제거할 수 없고, 코어를
        /// 시작점으로 잡은 드래그는 **애초에 지우려는 뜻이 아니다**(보드를 밀려던 손이다).
        /// </summary>
        public static bool IsRemovalDrag(Vector2Int start, int cellCount,
            System.Func<Vector2Int, bool> isOccupied, System.Func<Vector2Int, bool> isBelt,
            System.Func<Vector2Int, bool> isCore)
        {
            if (cellCount <= 1) return false;                      // 탭은 제거가 아니다
            if (isCore != null && isCore(start)) return false;     // 코어에서 시작하면 안 지운다

            bool occupied = isOccupied != null && isOccupied(start);
            bool belt = isBelt != null && isBelt(start);
            return occupied || belt;
        }

        /// <summary>
        /// 실제로 지울 칸만 남긴다 — **코어는 뺀다** (2026-09-15 사용자 확정).
        ///
        /// ✅ **설계 확정이다**(2026-09-16 · `260915_W01` 판정 4). 종전 표기는 「구현 가정」이었다 —
        /// 09-15 에 구현이 고르고 09-16 에 설계가 같은 쪽으로 판정했다. 이제 되돌릴 값이 아니다.
        ///
        /// 갈래 둘 중 「코어만 남긴다」다. 다른 갈래는 「코어가 끼면 드래그 전체를 무시」인데,
        /// 그러면 **긴 줄을 지우다 코어를 스치기만 해도 아무것도 안 지워진다** — 손이 한 일이 통째로 사라지는 쪽이
        /// 한 칸 덜 지워지는 쪽보다 나쁘다.
        /// </summary>
        public static List<Vector2Int> Removable(IReadOnlyList<Vector2Int> cells,
            System.Func<Vector2Int, bool> isCore)
        {
            var kept = new List<Vector2Int>();
            if (cells == null) return kept;

            for (int i = 0; i < cells.Count; i++)
            {
                if (isCore != null && isCore(cells[i])) continue;
                kept.Add(cells[i]);
            }
            return kept;
        }
    }
}
