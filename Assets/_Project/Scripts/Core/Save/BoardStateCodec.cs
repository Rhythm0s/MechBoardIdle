using System;
using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 보드 ↔ 저장 꼴 사이의 유일한 변환기 (2026-09-16 · `Docs/board_save_design.md`).
    ///
    /// ⚠️⚠️ **순수하게 둔다** — `MonoBehaviour` 를 안 만진다. 그래야 시험이 보드를 직접 짓고
    /// 왕복시킬 수 있다. 09-15 에 「프로브는 초록인데 화면은 낡았다」를 여러 번 겪은 자리라,
    /// **화면을 거치는 부분과 값을 옮기는 부분을 갈라 둔다.**
    ///
    /// 📌 **면을 비트로 접는 이유** — `JsonUtility` 는 배열의 배열을 못 삼킨다.
    /// 그래서 `PortFace[]` 를 정수 하나로 접는다. N=1 · E=2 · S=4 · W=8.
    /// </summary>
    public static class BoardStateCodec
    {
        /// <summary>면 하나의 비트. `PortFace` 의 값 순서(N·E·S·W)를 그대로 민다.</summary>
        public static int BitOf(PortFace face) => 1 << (int)face;

        /// <summary>면들을 정수 하나로 접는다. 빈 배열·null 은 0 이다.</summary>
        public static int MaskOf(IReadOnlyList<PortFace> faces)
        {
            int mask = 0;
            if (faces == null) return mask;
            for (int i = 0; i < faces.Count; i++) mask |= BitOf(faces[i]);
            return mask;
        }

        /// <summary>
        /// 정수를 면들로 편다. **늘 N·E·S·W 차례로 나온다.**
        ///
        /// ⚠️ 접었다 펴면 **원래 순서는 잃는다** — 면 목록은 집합이지 줄이 아니라고 보는 것이
        /// 이 저장의 전제다. 직선·코너는 면이 하나씩이라 영향이 없고, 병합기는 받는 면 셋이
        /// 대등하다(`StartingBoard.MergerInFaces` 도 순서에 뜻을 두지 않는다).
        /// </summary>
        public static PortFace[] FacesOf(int mask)
        {
            var list = new List<PortFace>(4);
            for (int i = 0; i < 4; i++)
                if ((mask & (1 << i)) != 0) list.Add((PortFace)i);
            return list.ToArray();
        }

        /// <summary>지금 판을 저장 꼴로 뜬다.</summary>
        public static BoardStateV1 Capture(BoardGrid grid)
        {
            if (grid == null) return null;

            var state = new BoardStateV1
            {
                columns = grid.Columns,
                rows = grid.Rows,
                // ⚠️ **주인의 판을 본다**(2026-09-17). A 의 표식을 B 저장에 찍으면
                //    B 시작 보드를 고쳐도 표식이 안 바뀌어 규칙 5 가 B 에서 안 선다.
                generation = BoardGeneration.Of(grid.Owner),
                owner = (int)grid.Owner,   // 판이 자기 주인을 들고 간다(2026-09-16)
            };

            foreach (NodeInstance n in grid.Nodes)
            {
                if (n?.Definition == null) continue;
                state.nodes.Add(new BoardNodeEntry
                {
                    nodeId = n.Definition.nodeId,
                    x = n.Cell.x,
                    y = n.Cell.y,
                    rotation = n.Rotation,
                    recipe = (int)n.SelectedRecipe,
                    ammo = (int)n.AmmoKind,
                    module0 = IdOf(n.ModuleAt(0)),
                    module1 = IdOf(n.ModuleAt(1)),
                });
            }

            foreach (BeltInstance b in grid.Belts)
            {
                if (b == null) continue;
                state.belts.Add(new BoardBeltEntry
                {
                    x = b.Cell.x,
                    y = b.Cell.y,
                    element = (int)b.Element,
                    inMask = MaskOf(b.InFaces),
                    outMask = MaskOf(b.OutFaces),
                });
            }

            return state;
        }

        /// <summary>
        /// 이 저장을 이 격자에 놓을 수 있는가 — **크기가 같아야 한다.**
        ///
        /// ⚠️ 없거나 크기가 다르면 **버린다.** 억지로 놓으면 좌표가 밀린 판이 나오는데,
        /// 그것은 에러 없이 조용히 틀린 판이라 가장 나쁘다(설계 규칙 2).
        /// </summary>
        public static bool Fits(BoardStateV1 state, BoardGrid grid)
            => state != null && grid != null
               && state.columns == grid.Columns && state.rows == grid.Rows
               && state.generation == BoardGeneration.Of(grid.Owner)
               && state.owner == (int)grid.Owner;

        /// <summary>
        /// 왜 안 맞는지 한 줄로 — 로그에 찍는다. 맞으면 <c>null</c>.
        ///
        /// ⚠️ **조용히 버리지 않는다.** 저장이 사라지는 것은 플레이어에게 큰 일이라
        /// 적어도 이유는 남긴다.
        /// </summary>
        public static string WhyNotFit(BoardStateV1 state, BoardGrid grid)
        {
            if (state == null) return "저장된 판이 없다";
            if (grid == null) return "격자가 없다";
            if (state.columns != grid.Columns || state.rows != grid.Rows)
                return $"격자 크기가 다르다 — 저장 {state.columns}x{state.rows} · 지금 {grid.Columns}x{grid.Rows}";
            // ⚠️ 주인이 어긋나면 **판이 통째로 남의 것**이다 — 이것을 안 보면
            //    A 판이 B 자리에 조용히 들어앉는다.
            //
            // ⚠️⚠️ **주인을 세대보다 먼저 본다**(2026-09-17). 표식이 주인별이 되면서
            //    남의 판은 **세대도 함께 어긋난다** — 세대를 먼저 보면 까닭이
            //    「세대가 다르다」로 적히고, 그것은 참이지만 **덜 정확한 말**이다.
            //    진짜 까닭은 판이 남의 것이라는 쪽이고, 읽는 사람이 고칠 자리도 그쪽이다.
            if (state.owner != (int)grid.Owner)
                return $"판의 주인이 다르다 — 저장 {(MountOwner)state.owner} · 지금 {grid.Owner}";

            // ⚠️ **견주는 것도 주인의 판이다** — 뜰 때와 다른 판을 보면 늘 어긋난다.
            string want = BoardGeneration.Of(grid.Owner);
            if (state.generation != want)
                return $"시작 보드 세대가 다르다({grid.Owner}) — 저장 '{state.generation ?? "(없음)"}' · 지금 '{want}'";
            return null;
        }

        /// <summary>
        /// 저장을 빈 격자에 푼다. 놓인 것만 <paramref name="onNode"/>·<paramref name="onBelt"/> 로 알린다.
        ///
        /// ⚠️ **되돌아온 판이 반드시 저장과 같지는 않다** — id 로 찾은 자산이 없거나
        /// 칸이 막혀 있으면 그 하나만 건너뛴다. 통째로 버리지 않는 이유는, 자산 하나가
        /// 사라졌다고 플레이어의 나머지 판까지 날리는 편이 더 나쁘기 때문이다.
        /// 몇 개를 못 놓았는지는 반환값으로 센다.
        ///
        /// ⚠️ **면·품목은 여기서 안 정한다** — 부른 쪽이 `BeltAutoOrient.Resolve` →
        /// `BeltFlow.Resolve` 를 돌린다(설계 규칙 3). 저장이 그것까지 들고 있으면 진실이 둘이 된다.
        /// </summary>
        /// <returns>못 놓은 항목 수. 0 이면 저장 그대로 섰다.</returns>
        public static int Restore(
            BoardStateV1 state, BoardGrid grid,
            Func<string, NodeDefinition> nodeById,
            Func<string, ModuleDefinition> moduleById = null,
            Action<Vector2Int> onNode = null,
            Action<Vector2Int, PortFace> onBelt = null)
        {
            if (!Fits(state, grid)) return -1;

            int missed = 0;

            if (state.nodes != null)
                foreach (BoardNodeEntry e in state.nodes)
                {
                    NodeDefinition def = nodeById?.Invoke(e.nodeId);
                    var cell = new Vector2Int(e.x, e.y);
                    if (def == null || !grid.TryPlace(cell, def, out NodeInstance placed)) { missed++; continue; }

                    placed.Rotation = e.rotation;
                    placed.AmmoKind = (AmmoKind)e.ammo;

                    // ⚠️ `SelectRecipe` 는 **이 노드가 실제로 돌릴 수 있는 조합표만** 받는다.
                    //    거절당하면 기본값으로 남는다 — 그것도 못 놓은 것으로 센다.
                    var recipe = (RecipeKind)e.recipe;
                    if (recipe != RecipeKind.None && !placed.SelectRecipe(recipe)) missed++;

                    if (!AttachModule(placed, 0, e.module0, moduleById)) missed++;
                    if (!AttachModule(placed, 1, e.module1, moduleById)) missed++;

                    onNode?.Invoke(cell);
                }

            if (state.belts != null)
                foreach (BoardBeltEntry e in state.belts)
                {
                    var cell = new Vector2Int(e.x, e.y);
                    PortFace[] ins = FacesOf(e.inMask);
                    PortFace[] outs = FacesOf(e.outMask);
                    if (outs.Length == 0) { missed++; continue; }   // 나가는 면이 없으면 벨트가 아니다

                    if (!grid.TryPlaceBeltElement(cell, (BeltElementKind)e.element, ins, outs,
                            FlowKind.None, out _)) { missed++; continue; }

                    onBelt?.Invoke(cell, outs[0]);
                }

            return missed;
        }

        private static bool AttachModule(NodeInstance node, int slot, string id,
            Func<string, ModuleDefinition> moduleById)
        {
            if (string.IsNullOrEmpty(id)) return true;   // 빈 칸은 빈 칸으로 두는 것이 맞다
            ModuleDefinition m = moduleById?.Invoke(id);
            return m != null && node.TryAttachModuleAt(slot, m);
        }

        private static string IdOf(ModuleDefinition m) => m != null ? m.moduleId : string.Empty;
    }
}
