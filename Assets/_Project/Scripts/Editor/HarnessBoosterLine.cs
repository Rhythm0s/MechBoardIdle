using MBI.Core;
using MBI.Data;
using UnityEngine;

namespace MBI.EditorTools
{
    /// <summary>
    /// 【하네스 전용】 부스터 둘 + 추진제 줄 (2026-09-17 · `260917_W02` 2-2 측정 요청).
    ///
    /// ⚠️⚠️ **배포 시작 보드를 건드리지 않는다.** `StartingBoard` · `StartingBoardB` 는
    /// 내용에서 뽑은 **세대 표식**(`StartingBoard.Generation`)을 들고 있고, 배치가 바뀌면
    /// 해시가 바뀌어 **사용자 저장이 버려진다**(조립 시스템 문서「보드 저장」 규칙 5).
    /// 그래서 줄을 여기에 따로 두고 **하네스가 얹을 때만** 얹는다.
    /// 배포에 넣는 일(사용자 결정 `260917_W02` 3장)은 **측정 뒤 따로 지시가 온다.**
    ///
    /// 줄의 모양은 **구현 판단**이다(W02 2-2: 「추진제 줄의 모양은 구현 판단이다」).
    /// 레시피 차례만 문서가 정해 두었다 — 가공(발전재료) → 기초 군수(추진제) → 부스터 둘.
    ///
    /// 📌 **A 와 B 의 줄 모양이 다르다.** 같은 모양을 두 판에 쓸 수 없다 —
    /// A 는 합류 기둥(x=8)과 운반로(y=5)가, B 는 어깨L(x≥9)이 이미 차 있어서
    /// **서로 남는 자리가 다르다.** 노드 수·레시피·부스터 대수는 **둘이 같다.**
    ///
    /// ⚠️ **에너지 노드는 더 놓지 않았다**(W02 2-2 이 물은 것). 근거는 산수가 아니라
    /// 자산 값이다 — 에너지 셋이 공급 30 이고, 줄이 더 먹는 것은 가공 1 + 기초 군수 2
    /// + 부스터 2×2 = **7** 이다. 얹은 뒤 전력 효율을 실제로 재서 보고한다.
    /// </summary>
    public static class HarnessBoosterLine
    {
        public const string BoosterId = "boost";

        /// <summary>부스터 대수 — 사용자 확정 「부스터 둘」(`260917_W02` 3장).</summary>
        public const int BoosterCount = 2;

        /// <summary>
        /// 로봇 A 판에 얹는다 — **폐기된 북 줄 자리**(5,9)~(7,9)에서 어깨L 로 나간다.
        ///
        /// <code>
        ///   y=10                                      부스터(9,10)  ← 남면(회전 3)
        ///   y=9   벨(5,9)→ 가공(6,9)→ 벨(7,9)→ 군수(8,9)→ 분류기(9,9)→ 부스터(10,9)
        ///   y=8        코어(5,8)↑北面
        /// </code>
        ///
        /// ⚠️ 코어의 **북면**을 쓴다. 시작 보드가 넷 중 동·남 둘만 쓰므로 북은 비어 있다
        /// (`StartingBoard` 「남은 두 면은 열려 있고 플레이어가 줄을 더 놓을 자리」).
        ///
        /// ⚠️⚠️ **분류기는 군수 노드에 딱 붙여야 한다** (2026-09-17 · 첫 배치가 여기서 끊겼다).
        /// <see cref="BeltAutoOrient"/> 는 분류기의 **입력면을 이웃에서 다시 잡는데**,
        /// 노드를 먼저 보고 없으면 **면 차례(북→동→남→서)대로 아무 벨트나** 고른다.
        /// 첫 배치는 분류기 서쪽에 벨트를 한 칸 두었더니 **북쪽 벨트가 입력면으로 뽑혀**
        /// 추진제가 거꾸로 흘렀고, 부스터가 집계에 **0 대**로 잡혔다
        /// (`HarnessBoosterLineTests` 가 그것을 잡았다).
        /// 📌 **내가 적은 면은 남지 않는다** — 자동 배향이 다시 잡으므로, 자리로 말해야 한다.
        ///
        /// ⚠️ 부스터는 입력면이 **서쪽 하나**뿐이라, 북쪽 부스터는 **회전 3**(서→남)으로 돌려
        /// 분류기의 북쪽 출력을 받는다.
        /// </summary>
        public static bool ApplyToA(BoardGrid grid, out string why)
        {
            why = null;
            if (grid == null) { why = "판이 없다"; return false; }

            if (!Belt(grid, 5, 9, PortFace.South, PortFace.East, ref why)) return false;
            if (!Proc(grid, 6, 9, ref why)) return false;
            if (!Belt(grid, 7, 9, PortFace.West, PortFace.East, ref why)) return false;
            if (!Muni(grid, 8, 9, ref why)) return false;
            if (!Sorter(grid, 9, 9, ref why)) return false;
            if (!Booster(grid, 10, 9, 0, ref why)) return false;
            if (!Booster(grid, 9, 10, 3, ref why)) return false;

            BeltAutoOrient.Resolve(grid);
            BeltFlow.Resolve(grid);
            return true;
        }

        /// <summary>
        /// 로봇 B 판에 얹는다 — **코어 서면**으로 내려가 몸통 아래를 쓴다.
        ///
        /// <code>
        ///   y=8   벨(4,8)←코어(5,8) 西面
        ///   y=7   벨(4,7)↓
        ///   y=6   벨(4,6)→ 가공(5,6)→ 군수(6,6)→ 분류기(7,6)→ 부스터(8,6)
        ///   y=5                                   벨(7,5)→ 부스터(8,5)
        /// </code>
        ///
        /// ⚠️ B 는 어깨L 쪽(9~10 열)이 드론 운반로라 A 와 같은 자리를 못 쓴다.
        /// </summary>
        public static bool ApplyToB(BoardGrid grid, out string why)
        {
            why = null;
            if (grid == null) { why = "판이 없다"; return false; }

            if (!Belt(grid, 4, 8, PortFace.East, PortFace.South, ref why)) return false;
            if (!Belt(grid, 4, 7, PortFace.North, PortFace.South, ref why)) return false;
            if (!Belt(grid, 4, 6, PortFace.North, PortFace.East, ref why)) return false;
            if (!Proc(grid, 5, 6, ref why)) return false;
            if (!Muni(grid, 6, 6, ref why)) return false;
            if (!Sorter(grid, 7, 6, ref why)) return false;   // 서쪽이 군수 노드 — 입력면이 바로 잡힌다
            if (!Booster(grid, 8, 6, 0, ref why)) return false;
            if (!Belt(grid, 7, 5, PortFace.North, PortFace.East, ref why)) return false;
            if (!Booster(grid, 8, 5, 0, ref why)) return false;

            BeltAutoOrient.Resolve(grid);
            BeltFlow.Resolve(grid);
            return true;
        }

        // ── 놓는 손 ────────────────────────────────────────────────────────
        //
        // ⚠️ **실패를 삼키지 않는다.** 한 칸이라도 못 놓으면 그 자리를 말하고 false 를 낸다 —
        //    조용히 넘어가면 「부스터를 얹었는데 회피가 0」인 판을 재고 그것을 결과로 적게 된다.

        private static bool Belt(BoardGrid g, int x, int y, PortFace inFace, PortFace outFace,
            ref string why)
        {
            if (g.TryPlaceBelt(new Vector2Int(x, y), inFace, outFace, FlowKind.None, out _)) return true;
            why = $"벨트를 ({x},{y}) 에 못 깔았다";
            return false;
        }

        /// <summary>
        /// 분류기 한 대. **면은 적어도 남지 않는다** — <see cref="BeltAutoOrient"/> 가
        /// 입력면을 이웃에서 다시 잡고 **나머지 셋을 전부 출력면**으로 만든다.
        /// 그래서 여기서는 자리만 잡고 면은 서쪽으로 둔다(놓기 위한 초기값).
        /// </summary>
        private static bool Sorter(BoardGrid g, int x, int y, ref string why)
        {
            if (g.TryPlaceBeltElement(new Vector2Int(x, y), BeltElementKind.Sorter,
                    new[] { PortFace.West },
                    new[] { PortFace.North, PortFace.East, PortFace.South },
                    FlowKind.None, out _)) return true;
            why = $"분류기를 ({x},{y}) 에 못 놓았다";
            return false;
        }

        private static bool Proc(BoardGrid g, int x, int y, ref string why)
            => Node(g, x, y, StartingBoard.ProcId, RecipeKind.PowerMaterial, 0, ref why);

        private static bool Muni(BoardGrid g, int x, int y, ref string why)
            => Node(g, x, y, StartingBoard.MuniId, RecipeKind.Propellant, 0, ref why);

        /// <summary>부스터 한 대. <paramref name="rotation"/> 은 사분면 수 — 서면이 그만큼 시계로 돈다.</summary>
        private static bool Booster(BoardGrid g, int x, int y, int rotation, ref string why)
            => Node(g, x, y, BoosterId, RecipeKind.None, rotation, ref why);

        private static bool Node(BoardGrid g, int x, int y, string nodeId, RecipeKind recipe,
            int rotation, ref string why)
        {
            NodeDefinition def = UnityEditor.AssetDatabase.LoadAssetAtPath<NodeDefinition>(
                $"Assets/_Project/ScriptableObjects/Nodes/Node_{nodeId}.asset");
            if (def == null) { why = $"노드 자산이 없다 — Node_{nodeId}.asset"; return false; }

            if (!g.TryPlace(new Vector2Int(x, y), def, out NodeInstance placed))
            {
                why = $"{nodeId} 를 ({x},{y}) 에 못 놓았다 — 실루엣 밖이거나 이미 찼다";
                return false;
            }

            placed.Rotation = rotation;

            // ⚠️ **조합표를 안 고르면 기본값으로 돈다** — 기초 군수의 기본은 **표준탄**이라,
            //    안 고르면 추진제가 한 개도 안 나오고 부스터는 영영 빈 그릇이 된다.
            if (recipe != RecipeKind.None && !placed.SelectRecipe(recipe))
            {
                why = $"{nodeId} 가 조합표 {recipe} 를 못 받는다";
                return false;
            }
            return true;
        }
    }
}
