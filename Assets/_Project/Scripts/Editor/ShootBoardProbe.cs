using System.Collections.Generic;
using System.Text;
using MBI.Core;
using MBI.Data;
using UnityEditor;
using UnityEngine;

namespace MBI.EditorTools
{
    /// <summary>
    /// 촬영용 배치 표를 짓기 위한 실측 (2026-09-14 밤 · §72-18 4번).
    ///
    /// ⚠️ **값만 낸다. 판정하지 않는다.** 여기서 하는 일은 「B구간 재설계로 무엇을 어디에
    /// 더 놓으면 S3 126 을 넘는가」를 **지어내지 않고 재는 것**이다.
    ///
    /// ⚠️ **게임 코드는 안 건드린다** — 이 파일은 에디터 전용이라 빌드에 안 들어간다.
    ///
    /// 배치 실행: <c>-executeMethod MBI.EditorTools.ShootBoardProbe.RunBatch</c>
    /// </summary>
    public static class ShootBoardProbe
    {
        private const string NodeRoot = "Assets/_Project/ScriptableObjects/Nodes";

        [MenuItem("MBI/Probe Shoot Board")]
        public static void RunMenu() => Debug.Log(Run());

        public static void RunBatch()
        {
            Debug.Log(Run());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 촬영용 배치 실측 (2026-09-14 · §72-18 4) ===");
            PortTable(sb);
            FreeMap(sb);
            ExplosiveLine(sb);
            MixedDelivery(sb);
            return sb.ToString();
        }

        // ────────────────────────────────────────────────────────────────
        //  1. 노드 면 표 — 무엇을 어느 면으로 받고 어느 면으로 내는가
        // ────────────────────────────────────────────────────────────────

        private static void PortTable(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("[1] 노드 면 — 면 | 입출력 | 품목");
            foreach (string id in new[] { "core", "proc", "muni", "munix", "ener", "stor" })
            {
                NodeDefinition n = Node(id);
                if (n == null) { sb.AppendLine($"  {id} | (자산 없음)"); continue; }
                sb.AppendLine($"  {id} ({n.displayName}) 전력 {n.resources.powerDraw:F0}");
                foreach (NodePort p in n.ports)
                    sb.AppendLine($"    {p.face} | {p.io} | {p.kind}");
                foreach (NodeRecipe r in n.recipes)
                {
                    var ins = new StringBuilder();
                    foreach (RecipeInput i in r.inputs) ins.Append($"{i.kind} x{i.perOutput} ");
                    sb.AppendLine($"    조합표 {r.kind} : [{ins.ToString().Trim()}] -> {r.output} " +
                                  $"{r.outputPerSec:F0}/초 · 돌릴 수 있는가 {r.IsRunnable}");
                }
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  2. 시작 보드에서 남는 칸 — 어디에 더 놓을 수 있는가
        // ────────────────────────────────────────────────────────────────

        private static void FreeMap(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("[2] 시작 보드 지도 — . 빈 칸 · # 실루엣 밖 · 노드/벨트는 글자");

            BoardGrid g = Starting(true);
            HashSet<Vector2Int> mask = PartLayout.BuildMask();

            sb.Append("     ");
            for (int x = 0; x < PartLayout.Columns; x++) sb.Append($"{x,3}");
            sb.AppendLine();

            int free = 0;
            for (int y = PartLayout.Rows - 1; y >= 0; y--)
            {
                sb.Append($"  y{y,2} ");
                for (int x = 0; x < PartLayout.Columns; x++)
                {
                    var c = new Vector2Int(x, y);
                    if (!mask.Contains(c)) { sb.Append("  #"); continue; }

                    NodeInstance node = g.GetAt(c);
                    if (node?.Definition != null) { sb.Append($"{Short(node.Definition.nodeId),3}"); continue; }

                    BeltInstance belt = g.GetBeltAt(c);
                    if (belt != null) { sb.Append(belt.Element == BeltElementKind.Merger ? "  M" : "  ="); continue; }

                    sb.Append("  ."); free++;
                }
                sb.AppendLine();
            }
            sb.AppendLine($"  빈 칸 {free}개 (실루엣 안 {mask.Count} 중)");
        }

        private static string Short(string id)
        {
            switch (id)
            {
                case "core": return "C";
                case "proc": return "P";
                case "muni": return "m";
                case "munix": return "X";
                case "ener": return "E";
                default: return "?";
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  3. 폭발탄 라인이 보드에서 배선되는가 — **배치 표 전체의 전제**
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 폭발탄 = 표준탄 + **발전재료**(복합 군수). 그런데 자산의 면 표를 보면
        /// 복합 군수의 둘째 입력면이 **기초재료·부품 하나로 박혀** 있고, 가공의 출력면도
        /// 마찬가지다. 조합표를 발전재료로 바꿔도 **면이 그것을 안 받으면 안 흐른다.**
        ///
        /// 여기서 재는 것은 그 하나다 — **격자 위에서 폭발탄이 한 발이라도 나오는가.**
        /// </summary>
        private static void ExplosiveLine(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("[3] 폭발탄 라인이 배선되는가 — 최소 판으로 재 본다");

            foreach (RecipeKind ammo in new[] { RecipeKind.PierceAmmo, RecipeKind.ExplosiveAmmo })
            {
                var g = new BoardGrid(12, 14, 1f, Vector2.zero, PartLayout.BuildMask());

                // 코어(5,8) 동면 -> 가공(6,8) -> 기초 군수(7,8) -> 표준탄 -> 복합 군수(8,8) 서면
                g.TryPlace(new Vector2Int(5, 8), Node("core"), out _);
                g.TryPlace(new Vector2Int(6, 8), Node("proc"), out NodeInstance procA);
                g.TryPlace(new Vector2Int(7, 8), Node("muni"), out _);
                g.TryPlace(new Vector2Int(8, 8), Node("munix"), out NodeInstance mx);

                // ⚠️ **둘째 재료는 남면으로 들어간다** — 가공은 **동쪽으로 내므로**
                // 복합 군수 밑에 바로 붙이면 안 닿는다. 벨트 한 칸이 꺾어 올려 준다.
                // 코어 남면 -> (5,7) -> (6,7) -> 가공(7,7) -> (8,7) 벨트가 북으로.
                g.TryPlaceBelt(new Vector2Int(5, 7), PortFace.North, PortFace.East, FlowKind.None, out _);
                g.TryPlaceBelt(new Vector2Int(6, 7), PortFace.West, PortFace.East, FlowKind.None, out _);
                g.TryPlace(new Vector2Int(7, 7), Node("proc"), out NodeInstance procB);
                g.TryPlaceBelt(new Vector2Int(8, 7), PortFace.West, PortFace.North, FlowKind.None, out _);

                // 복합 군수 산출(동면) -> 내려가 y4 를 타고 서쪽 -> 마운트(0,6)
                g.TryPlaceBelt(new Vector2Int(9, 8), PortFace.West, PortFace.South, FlowKind.None, out _);
                for (int y = 7; y >= 5; y--)
                    g.TryPlaceBelt(new Vector2Int(9, y), PortFace.North, PortFace.South, FlowKind.None, out _);
                g.TryPlaceBelt(new Vector2Int(9, 4), PortFace.North, PortFace.West, FlowKind.None, out _);
                for (int x = 8; x >= 1; x--)
                    g.TryPlaceBelt(new Vector2Int(x, 4), PortFace.East, PortFace.West, FlowKind.None, out _);
                g.TryPlaceBelt(new Vector2Int(0, 4), PortFace.East, PortFace.North, FlowKind.None, out _);
                g.TryPlaceBelt(new Vector2Int(0, 5), PortFace.South, PortFace.North, FlowKind.None, out _);
                g.TryPlaceBelt(new Vector2Int(0, 6), PortFace.South, PortFace.West, FlowKind.None, out _);

                // ⚠️ 조합표를 손으로 고른다 — 게임에서도 노드를 누르면 이 패널이 뜬다.
                bool pickedAmmo = mx != null && mx.SelectRecipe(ammo);
                bool pickedMat = procB != null && procB.SelectRecipe(
                    ammo == RecipeKind.ExplosiveAmmo ? RecipeKind.PowerMaterial : RecipeKind.BasicParts);

                BeltAutoOrient.Resolve(g);
                BeltFlow.Resolve(g);

                int got = Arrivals(g, 60f, out float firstAt, out Dictionary<FlowKind, int> byKind);

                sb.AppendLine();
                sb.AppendLine($"  --- {ammo} ---");
                sb.AppendLine($"  조합표 선택 — 복합 군수 {pickedAmmo} · 둘째 재료 가공 {pickedMat}");
                sb.AppendLine($"  복합 군수가 실제로 도는 조합표 = {mx?.CurrentRecipe.kind}");
                sb.AppendLine($"  둘째 재료 가공 조합표 = {procB?.CurrentRecipe.kind} " +
                              $"(첫째 {procA?.CurrentRecipe.kind})");
                sb.AppendLine($"  60초 도착 {got}개 · 첫 도착 {firstAt:F1}초");
                foreach (KeyValuePair<FlowKind, int> kv in byKind)
                    sb.AppendLine($"    {kv.Key} {kv.Value}개");
                var conn = new HashSet<Vector2Int>(LogisticsReach.ConnectedNodes(g));
                sb.AppendLine($"  링크 {BeltRouting.BuildLinks(g).Count}개 · 라인에 든 노드 {conn.Count}개");
                sb.AppendLine("  벨트 | 입력면 | 출력면 | 나르는 것");
                for (int y = 13; y >= 0; y--)
                for (int x = 0; x < 12; x++)
                {
                    BeltInstance b = g.GetBeltAt(new Vector2Int(x, y));
                    if (b != null) sb.AppendLine($"    ({x},{y}) | {b.InFace} | {b.OutFace} | {b.Kind}");
                }
                if (got == 0) sb.AppendLine("  ⚠️ **한 발도 안 닿았다** — 이 조합은 격자 위에서 안 선다.");
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  4. **한 마운트 포트에 두 탄종이 들어가는가** — 140·180 의 전제
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 「표준 4 + 폭발 2 = 140」은 **두 탄종이 같은 마운트에 닿는다**는 전제 위에 선다.
        /// 그런데 벨트는 <c>BeltInstance.Kind</c> 하나만 나르고, 마운트에 넣는 것은
        /// **포트 칸의 벨트 한 장**뿐이다. 그 한 장이 한 품목만 나른다면 전제가 깨진다.
        ///
        /// 여기서 재는 것은 그 하나다 — **두 줄을 한 포트로 모으면 무엇이 닿는가.**
        /// </summary>
        private static void MixedDelivery(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("[4] 한 포트에 두 탄종을 모으면 — 140/180 의 전제");
            const RecipeKind ammo = RecipeKind.ExplosiveAmmo;

            var g = new BoardGrid(12, 14, 1f, Vector2.zero, PartLayout.BuildMask());

            // 표준 줄 — 코어(5,8) 동면 -> 가공(6,8) -> 기초 군수(7,8) -> 표준탄
            g.TryPlace(new Vector2Int(5, 8), Node("core"), out _);
            g.TryPlace(new Vector2Int(6, 8), Node("proc"), out _);
            g.TryPlace(new Vector2Int(7, 8), Node("muni"), out _);
            g.TryPlaceBelt(new Vector2Int(8, 8), PortFace.West, PortFace.South, FlowKind.None, out _);

            // 코어 서면 -> 분류기(4,7) 가 폭발 줄 둘(재료·발전재료)에 나눠 먹인다
            g.TryPlaceBelt(new Vector2Int(4, 8), PortFace.East, PortFace.South, FlowKind.None, out _);
            g.TryPlaceBeltElement(new Vector2Int(4, 7), BeltElementKind.Sorter,
                new[] { PortFace.North }, new[] { PortFace.East, PortFace.South },
                FlowKind.None, out _);
            g.TryPlaceBelt(new Vector2Int(4, 6), PortFace.North, PortFace.East, FlowKind.None, out _);

            // 폭발 본줄 (y7) — 가공(기초재료) -> 기초 군수(표준탄) -> 복합 군수(폭발탄)
            g.TryPlace(new Vector2Int(5, 7), Node("proc"), out _);
            g.TryPlace(new Vector2Int(6, 7), Node("muni"), out _);
            g.TryPlace(new Vector2Int(7, 7), Node("munix"), out NodeInstance mx);
            mx.SelectRecipe(ammo);

            // ⚠️ 둘째 재료는 **남면**으로 든다 — 아래 줄(y6)이 꺾어 올려 준다.
            g.TryPlace(new Vector2Int(5, 6), Node("proc"), out NodeInstance pm);
            pm.SelectRecipe(ammo == RecipeKind.ExplosiveAmmo
                ? RecipeKind.PowerMaterial : RecipeKind.BasicParts);
            g.TryPlaceBelt(new Vector2Int(6, 6), PortFace.West, PortFace.East, FlowKind.None, out _);
            g.TryPlaceBelt(new Vector2Int(7, 6), PortFace.West, PortFace.North, FlowKind.None, out _);

            // x8 기둥에서 표준 줄과 폭발 줄을 **병합기**로 모아 운반로 y5 -> 마운트 A(1,4 남면)
            g.TryPlaceBeltElement(new Vector2Int(8, 7), BeltElementKind.Merger,
                StartingBoard.MergerInFaces(PortFace.South), new[] { PortFace.South },
                FlowKind.None, out _);
            g.TryPlaceBelt(new Vector2Int(8, 6), PortFace.North, PortFace.South, FlowKind.None, out _);
            g.TryPlaceBelt(new Vector2Int(8, 5), PortFace.North, PortFace.West, FlowKind.None, out _);
            for (int x = 7; x >= 2; x--)
                g.TryPlaceBelt(new Vector2Int(x, 5), PortFace.East, PortFace.West, FlowKind.None, out _);
            g.TryPlaceBelt(new Vector2Int(1, 5), PortFace.East, PortFace.South, FlowKind.None, out _);
            g.TryPlaceBelt(new Vector2Int(1, 4), PortFace.North, PortFace.South, FlowKind.None, out _);

            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);

            sb.AppendLine($"  운반로 (5,5) 가 나르는 것 = {BeltFlow.KindAt(g, new Vector2Int(5, 5))}");
            sb.AppendLine($"  포트 칸 (1,4) 가 나르는 것 = {BeltFlow.KindAt(g, new Vector2Int(1, 4))}");

            int got = Arrivals(g, 60f, out float firstAt, out Dictionary<FlowKind, int> byKind);
            sb.AppendLine($"  60초 도착 {got}개 · 첫 도착 {firstAt:F1}초");
            foreach (KeyValuePair<FlowKind, int> kv in byKind)
                sb.AppendLine($"    {kv.Key} {kv.Value}개");
            if (byKind.Count < 2)
                sb.AppendLine("  ⚠️ **한 종류만 닿았다** — 한 포트는 한 품목만 받는다.");
        }

        // ---- 도구 ----

        private static BoardGrid Starting(bool fill)
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f,
                Vector2.zero, PartLayout.BuildMask());
            foreach (StartingBoard.Slot s in StartingBoard.Nodes)
                g.TryPlace(s.cell, Node(s.nodeId), out _);
            foreach (StartingBoard.Run r in StartingBoard.Belts) Place(g, r);
            if (fill) Place(g, StartingBoard.FillsEmptySlot);
            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        private static void Place(BoardGrid g, StartingBoard.Run run)
        {
            if (run.merger)
                g.TryPlaceBeltElement(run.cell, BeltElementKind.Merger,
                    StartingBoard.MergerInFaces(run.outFace), new[] { run.outFace },
                    FlowKind.None, out _);
            else
                g.TryPlaceBelt(run.cell, run.inFace, run.outFace, FlowKind.None, out _);
        }

        private static int Arrivals(BoardGrid grid, float seconds, out float firstAt,
            out Dictionary<FlowKind, int> byKind)
        {
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);
            byKind = new Dictionary<FlowKind, int>();

            const float dt = 0.05f;
            int count = 0;
            firstAt = 0f;

            int steps = Mathf.RoundToInt(seconds / dt);
            for (int i = 0; i < steps; i++)
            {
                BoardItemTick.Step(grid, flow, dt, 1f);
                IReadOnlyList<MountArrival> pending = flow.PendingMountArrivals;
                if (pending.Count > 0 && count == 0) firstAt = (i + 1) * dt;
                foreach (MountArrival a in pending)
                {
                    byKind.TryGetValue(a.kind, out int n);
                    byKind[a.kind] = n + 1;
                    count++;
                }
                flow.ClearPendingMountArrivals();
            }
            return count;
        }

        private static NodeDefinition Node(string id) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodeRoot}/Node_{id}.asset");
    }
}
