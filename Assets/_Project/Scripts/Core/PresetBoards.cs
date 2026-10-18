using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// **프리셋 보드** (사용자 확정 #167 · #168 · 2026-10-18).
    ///
    /// 설정 바로가기로 **S5 · S6** 에 뛰면 로봇 A 보드를 「표준탄 + 폭발탄」 판으로 세운다(#156 · #162 와 같은
    /// 심사자용 결 · 「그 판에 자연 진행으로 온 상태」 흉내). 자연 진행 S5 · S6 은 플레이어 보드 그대로이고,
    /// S4 이하 점프는 시작 보드 그대로다. 저장된 보드가 있어도 점프 때 덮는다(⚠️ 가정 · #167).
    ///
    /// **뼈대 = `Editor/ShootBoardProbe.cs` 의 검증 배치**(복합 가공소 서면 = 표준탄 · 남면 = 둘째 재료).
    /// 시작 보드 위에 바꾸는 것만 적는다 — 시작 보드는 <see cref="StartingBoard"/> 하나가 든다.
    ///
    ///   남 줄   코어 남면 → 벨(5,7) → 변환기(6,7) **조합표 발전재료** → 벨(7,7) 서→동 → 벨(8,7) 서→북
    ///   동 줄   코어 동면 → 변환기(6,8) → 기초 가공소(7,8) 표준탄 → **복합 가공소(8,8) 폭발탄**(서 표준탄 · 남 발전재료)
    ///   산출    복합 가공소 동면 → 벨(9,8) 서→남 → (9,7)(9,6)(9,5) 북→남 → (9,4) 북→서 → y4 (8..3,4) 동→서 → (2,4) 동→북 → **병합기(2,5)**(동 운반로 · 남 새 줄 → 북) → (2,6)(1,6)(1,5) → 마운트 포트 (1,4)
    ///
    /// 🗑️ 시작 보드에서 빠지는 것: 남 줄 기초 가공소(7,7) · 병합기(8,7) · 벨(8,8). 추진제 줄 · 쉴드 줄 · 에너지 셋은 그대로.
    /// ⚠️ 남 줄 변환기는 (7,7) 이 아니라 **(6,7) 의 조합표만** 발전재료로 바꾼다 — 변환기는 코어 에너지를 먹으므로
    ///    (7,7) 에 두면 앞의 (6,7)(기초재료를 내는 변환기)에서 받을 것이 없다(탐침도 (5,7)(6,7) 을 벨트로 둔다).
    /// </summary>
    public static class PresetBoards
    {
        /// <summary>이 판으로 뛰면 폭발탄 프리셋을 세우는가 — S5 · S6(사용자 확정 #167 · #168).</summary>
        public static bool ExplosiveOnJump(int stageNumber) => stageNumber == 5 || stageNumber == 6;

        /// <summary>
        /// **폭발탄 프리셋 요청 — 사건**(바로가기가 놓고 보드가 가져간다 · `TutorialSignals.FillEmptySlotRequested` 와 같은 결).
        /// </summary>
        public static bool ExplosiveRequested;

        public static readonly Vector2Int ExplosiveNodeCell = new Vector2Int(8, 7);
        public static readonly Vector2Int SplitCell = new Vector2Int(8, 8);
        public static readonly Vector2Int PowerMaterialCell = new Vector2Int(6, 7);
        public static readonly Vector2Int MergeCell = new Vector2Int(2, 5);

        /// <summary>
        /// 시작 보드(비워 둔 칸 채운 판)를 폭발탄 판으로 바꾼다. 면 풀기(`BeltAutoOrient` → `BeltFlow`)는 부르는 쪽 몫이다.
        /// </summary>
        /// <returns>모든 칸이 섰으면 참.</returns>
        public static bool ApplyExplosive(BoardGrid grid, System.Func<string, NodeDefinition> nodeById)
        {
            if (grid == null || nodeById == null) return false;
            bool ok = true;

            // 남 줄 — (6,7) 변환기를 발전재료로 · (7,7) 기초 가공소 → 벨트
            NodeInstance pm = grid.GetAt(PowerMaterialCell);
            ok &= pm != null && pm.SelectRecipe(RecipeKind.PowerMaterial);
            Clear(grid, new Vector2Int(7, 7));
            ok &= grid.TryPlaceBelt(new Vector2Int(7, 7), PortFace.West, PortFace.East, FlowKind.None, out _);
            // ✅ **표준탄도 마운트에 닿게**(사용자 확정 #169 · 2026-10-18 · #168 「표준탄 + 폭발탄」).
            //    동 줄 표준탄을 (8,8) **분류기**로 가른다(균등 분배 · 반씩) — 남 갈래 = 복합 가공소 · 동 갈래 = 표준탄 줄.
            Clear(grid, SplitCell);
            ok &= grid.TryPlaceBeltElement(SplitCell, BeltElementKind.Sorter,
                new[] { PortFace.West }, new[] { PortFace.South, PortFace.East }, FlowKind.None, out _);

            // 복합 가공소(8,7) · 한 번 돌림(시계 90°) — 북면 = 표준탄(분류기 남 갈래) · 서면 = 발전재료((7,7)) · 남면 = 폭발탄 산출
            //    → 옛 기둥 (8,6)(8,5) → 운반로 y5 → 마운트.  🗑️ #167 첫 판 「(8,8) 복합 가공소 · (8,7) 벨트 서→북」.
            Clear(grid, ExplosiveNodeCell);
            NodeDefinition munix = nodeById(StartingBoardB.ComplexId);
            NodeInstance x = null;
            bool placed = munix != null && grid.TryPlace(ExplosiveNodeCell, munix, out x);
            if (placed) x.Rotation = 1;
            ok &= placed && x.SelectRecipe(RecipeKind.ExplosiveAmmo);

            // 표준탄 갈래 — 분류기 동 → x=9 기둥 남 → y4 서 → 병합기(2,5)
            ok &= Belt(grid, 9, 8, PortFace.West, PortFace.South);
            for (int y = 7; y >= 5; y--) ok &= Belt(grid, 9, y, PortFace.North, PortFace.South);
            ok &= Belt(grid, 9, 4, PortFace.North, PortFace.West);
            for (int xx = 8; xx >= 3; xx--) ok &= Belt(grid, xx, 4, PortFace.East, PortFace.West);
            ok &= Belt(grid, 2, 4, PortFace.East, PortFace.North);

            // 기존 운반로 (2,5) 를 병합기로 — 운반로(동)와 새 줄(남)이 합쳐 북(2,6)으로 올라 마운트 포트(1,4)로 간다.
            // ⚠️ 마운트 포트 칸(1,4) 자체를 병합기로 두면 `BeltAutoOrient` 가 출력면을 북으로 돌려 줄이 막혔다(2026-10-18 하네스 실측).
            Clear(grid, MergeCell);
            ok &= grid.TryPlaceBeltElement(MergeCell, BeltElementKind.Merger,
                new[] { PortFace.East, PortFace.South }, new[] { PortFace.North }, FlowKind.None, out _);
            return ok;
        }

        private static bool Belt(BoardGrid g, int x, int y, PortFace inFace, PortFace outFace)
        {
            var c = new Vector2Int(x, y);
            Clear(g, c);
            return g.TryPlaceBelt(c, inFace, outFace, FlowKind.None, out _);
        }

        private static void Clear(BoardGrid g, Vector2Int c)
        {
            if (g.GetAt(c) != null) g.TryRemove(c);
            if (g.GetBeltAt(c) != null) g.TryRemoveBelt(c);
        }
    }
}
