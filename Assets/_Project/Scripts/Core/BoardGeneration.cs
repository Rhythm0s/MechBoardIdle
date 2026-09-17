using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 시작 보드 **세대 표식** — 배치 내용에서 뽑은 해시 (2026-09-16 사용자 결정 §74-9 ·
    /// 2026-09-17 **주인별로 가름**).
    ///
    /// ⚠️⚠️ **왜 갈랐나.** 표식을 만들 때도 견줄 때도 `StartingBoard` **하나**를 보고 있었다.
    /// 그런데 시작 보드는 2026-09-16 에 **둘**이 됐다(조립 시스템 문서 11-7).
    /// 그래서 `StartingBoardB` 를 고쳐도 표식이 안 바뀌고, 크기(12×14)도 주인도 같으므로
    /// **옛 B 판이 새 B 시작 보드 위에 조용히 올라온다** — 09-15 에 A 에서 났던 그 사고와
    /// 같은 모양이다(화면에는 「벨트를 이었는데 마운트 적재 0」으로 보였다).
    ///
    /// 📌 **셈하는 자리는 여기 하나다.** 판마다 따로 적으면 한쪽만 고쳐져 두 판의 표식이
    /// 다른 규칙으로 나온다 — 지침 §7 「한 값이 두 곳에 살면 답이 둘이 된다」.
    ///
    /// ⚠️ **사람이 읽을 값이 아니다** — 로그에 찍어 견주기만 한다.
    /// ⚠️ **손으로 올리는 버전 번호를 안 쓴다.** 올리는 것을 잊으면 표식이 거짓말을 하고,
    /// 그것은 표식이 없는 것보다 나쁘다. 배치가 바뀌면 해시가 저절로 바뀐다.
    /// </summary>
    public static class BoardGeneration
    {
        /// <summary>표식에 들어가는 노드 하나.</summary>
        public readonly struct NodeKey
        {
            public readonly Vector2Int cell;
            public readonly string nodeId;

            /// <summary>
            /// 고른 조합표. **없는 판은 비운다.**
            ///
            /// ⚠️ B 판은 이것이 **꼭 필요하다** — 가공 노드 둘이 같은 자산인데 조합표만
            /// 다르다(발전재료 · 배터리). 조합표를 안 섞으면 그 둘을 맞바꿔도 표식이 그대로다.
            /// ⚠️ **A 판도 2026-09-17 부터 섞는다** — 추진제 줄이 들어오면서 이 판에도
            /// 조합표로만 갈리는 노드가 생겼다. 🗑️ 구 규칙 「A 는 비운 채로 둔다(저장 보존)」는
            /// 폐기 — 그날 배치 자체가 바뀌어 어차피 A 저장도 한 번 버려진다(사용자 감수).
            /// 비우는 길은 조합표를 아예 안 쓰는 판을 위해 남겨 둔다.
            /// </summary>
            public readonly RecipeKind? recipe;

            public NodeKey(Vector2Int cell, string nodeId, RecipeKind? recipe = null)
            {
                this.cell = cell;
                this.nodeId = nodeId;
                this.recipe = recipe;
            }
        }

        /// <summary>
        /// 배치에서 표식을 뽑는다. FNV-1a 32 비트 — 암호용이 아니라 **달라졌는가**만 본다.
        /// </summary>
        public static string Of(int columns, int rows,
            IEnumerable<NodeKey> nodes, IEnumerable<StartingBoard.Run> belts)
        {
            unchecked
            {
                uint h = 2166136261u;
                void Mix(int v)
                {
                    for (int b = 0; b < 4; b++)
                    {
                        h ^= (uint)((v >> (b * 8)) & 0xFF);
                        h *= 16777619u;
                    }
                }

                Mix(columns);
                Mix(rows);

                if (nodes != null)
                    foreach (NodeKey n in nodes)
                    {
                        foreach (char c in n.nodeId ?? string.Empty) Mix(c);
                        Mix(n.cell.x); Mix(n.cell.y);
                        if (n.recipe.HasValue) Mix((int)n.recipe.Value);
                    }

                if (belts != null)
                    foreach (StartingBoard.Run r in belts)
                    {
                        Mix(r.cell.x); Mix(r.cell.y);
                        Mix((int)r.inFace); Mix((int)r.outFace);
                        // ⚠️ 2026-09-17 — 구 `merger ? 1 : 0` 폐기. 분류기가 들어오면서
                        //    참/거짓으로는 병합기와 분류기를 못 가른다.
                        Mix((int)r.element);
                    }

                return h.ToString("x8");
            }
        }

        /// <summary>
        /// **그 주인의** 시작 보드 표식. 저장을 뜰 때도 견줄 때도 이것을 쓴다 —
        /// 두 곳이 다른 판을 보면 규칙 5 가 한쪽에서만 선다.
        /// </summary>
        public static string Of(MountOwner owner)
            => owner == MountOwner.RobotB ? StartingBoardB.Generation : StartingBoard.Generation;
    }
}
