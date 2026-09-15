using System.Collections.Generic;
using System.Text;
using MBI.Core;
using MBI.Data;
using UnityEditor;
using UnityEngine;

namespace MBI.EditorTools
{
    /// <summary>
    /// 【조사】 벨트 위 주황 정사각의 정체 (2026-09-15 · 사용자 육안 ⑤).
    ///
    /// 증상 — 벨트·병합기 위에 **작은 주황 정사각**이 흐른다. 품목 그림이 아니라 색 사각이다.
    ///
    /// ⚠️ **값만 낸다. 고치지 않는다.**
    ///
    /// 갈래 둘을 가른다 —
    /// ⓐ **폐기된 구 `FlowKind.Ammo`(1)** 로 흐른다 → 대응표에 없으니 사각이 맞고,
    ///    **폐기 kind 를 만드는 경로**가 결함이다.
    /// ⓑ **표준탄(11)** 으로 흐른다 → 그림이 배선돼 있으므로(`ammo_standard.png`)
    ///    **그리기** 쪽 결함이다.
    ///
    /// 📌 **둘이 화면에서 같은 색이다** — 구 `Ammo` 와 표준탄이 둘 다 `(0.95, 0.55, 0.40)`
    /// 주황이라 눈으로는 못 가른다. 그래서 세어야 한다.
    ///
    /// 배치 실행: <c>-executeMethod MBI.EditorTools.BeltItemKindProbe.RunBatch</c>
    /// </summary>
    public static class BeltItemKindProbe
    {
        private const string NodeRoot = "Assets/_Project/ScriptableObjects/Nodes";
        private const float Dt = 0.05f;
        private const float Seconds = 40f;

        [MenuItem("MBI/Probe Belt Item Kinds")]
        public static void RunMenu() => Debug.Log(Run());

        public static void RunBatch()
        {
            Debug.Log(Run());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 벨트 위 품목 실측 (2026-09-15 · 육안 5) ===");

            BoardArtSet art = AssetDatabase.LoadAssetAtPath<BoardArtSet>(
                "Assets/_Project/ScriptableObjects/BoardArtSet.asset");

            BoardGrid grid = Board();
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            // 지금 벨트 위에 얹혀 있는 것 (스냅샷)
            var live = new Dictionary<FlowKind, int>();
            // 한 번이라도 흐른 것 (누적)
            var everSeen = new Dictionary<FlowKind, int>();

            int steps = Mathf.RoundToInt(Seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                BoardItemTick.Step(grid, flow, Dt, 1f);
                flow.ClearPendingMountArrivals();

                foreach (StartingBoard.Run r in StartingBoard.Belts)
                {
                    var c = new Vector2Int(r.cell.x, r.cell.y);
                    foreach (BeltItem it in flow.ItemsAt(c))
                    {
                        everSeen.TryGetValue(it.kind, out int n);
                        everSeen[it.kind] = n + 1;
                    }
                }
            }

            foreach (StartingBoard.Run r in StartingBoard.Belts)
            {
                var c = new Vector2Int(r.cell.x, r.cell.y);
                foreach (BeltItem it in flow.ItemsAt(c))
                {
                    live.TryGetValue(it.kind, out int n);
                    live[it.kind] = n + 1;
                }
            }

            sb.AppendLine();
            sb.AppendLine("[1] 40초 동안 벨트 위에 있던 품목 (틱 누적)");
            foreach (var kv in everSeen)
                sb.AppendLine("  " + Row(kv.Key, kv.Value, art));

            sb.AppendLine();
            sb.AppendLine("[2] 40초 뒤 벨트 위에 얹혀 있는 것");
            if (live.Count == 0) sb.AppendLine("  (없음)");
            foreach (var kv in live)
                sb.AppendLine("  " + Row(kv.Key, kv.Value, art));

            sb.AppendLine();
            sb.AppendLine("[3] 폐기 kind 가 흐르는가 — Material(0)·Ammo(1)·Power(2)·Heat(3)·Drone(4)");
            bool legacy = false;
            foreach (var kv in everSeen)
                if ((int)kv.Key <= 4) { legacy = true; sb.AppendLine("  ⚠️ " + Row(kv.Key, kv.Value, art)); }
            if (!legacy) sb.AppendLine("  없다 — 폐기 kind 는 한 번도 안 흘렀다");

            return sb.ToString();
        }

        private static string Row(FlowKind kind, int count, BoardArtSet art)
        {
            Sprite sp = art != null ? art.ItemSprite(kind) : null;
            return $"{kind} ({(int)kind}) · {count} 틱 · 그림 "
                   + (sp != null ? sp.name : "**없음**");
        }

        private static BoardGrid Board()
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f,
                Vector2.zero, PartLayout.BuildMask());
            foreach (StartingBoard.Slot s in StartingBoard.Nodes)
                g.TryPlace(s.cell, Node(s.nodeId), out _);
            foreach (StartingBoard.Run r in StartingBoard.Belts) Place(g, r);
            Place(g, StartingBoard.FillsEmptySlot);   // 튜토리얼을 마친 판으로 본다
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

        private static NodeDefinition Node(string id) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>(NodeRoot + "/Node_" + id + ".asset");
    }
}
