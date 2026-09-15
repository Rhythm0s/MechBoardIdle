using System.Collections.Generic;
using System.Text;
using MBI.Core;
using MBI.Data;
using UnityEditor;
using UnityEngine;

namespace MBI.EditorTools
{
    /// <summary>
    /// 【조사】 튜토리얼 목표가 왜 안 닫히는가 (2026-09-15 · 사용자 육안 4차 ⑤).
    ///
    /// 증상 — 빈 칸을 채워도 **목표 두 줄이 안 끝난다.** 띠에 「나가는 곳이 없다」가 뜬다.
    ///
    /// 목표는 둘이고 **차례가 규칙**이다 —
    /// ⓐ `SlotFilled` = 비워 둔 칸 (6,5) 가 채워졌는가 (보드가 켠다)
    /// ⓑ `MountFilled` = ⓐ 뒤에 **활성 마운트가 만충**인가 (전투가 켠다)
    ///
    /// ⚠️ **값만 낸다. 고치지 않는다.**
    ///
    /// 가르는 것 — 채운 판에서 **마운트에 물건이 닿기는 하는가.** 0 이면 운반로가
    /// 끊긴 것이고, 닿는데 40 을 못 채우면 **속도**의 문제다. 둘은 고치는 자리가 다르다.
    ///
    /// 배치 실행: <c>-executeMethod MBI.EditorTools.TutorialGoalProbe.RunBatch</c>
    /// </summary>
    public static class TutorialGoalProbe
    {
        private const string NodeRoot = "Assets/_Project/ScriptableObjects/Nodes";
        private const float Dt = 0.05f;
        private const float Seconds = 90f;

        [MenuItem("MBI/Probe Tutorial Goal")]
        public static void RunMenu() => Debug.Log(Run());

        public static void RunBatch()
        {
            Debug.Log(Run());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 튜토리얼 목표 실측 (2026-09-15 · 육안 4차 5) ===");
            sb.AppendLine("빈 칸 = " + StartingBoard.EmptySlot);
            sb.AppendLine();
            sb.AppendLine("[안 채운 판]");
            sb.AppendLine(Measure(false));
            sb.AppendLine("[채운 판 — 튜토리얼을 마친 상태]");
            sb.AppendLine(Measure(true));
            return sb.ToString();
        }

        private static string Measure(bool fill)
        {
            var sb = new StringBuilder();
            BoardGrid grid = Board(fill);
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            var arrivals = new Dictionary<FlowKind, int>();
            int total = 0;
            float firstAt = -1f;

            int steps = Mathf.RoundToInt(Seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                BoardItemTick.Step(grid, flow, Dt, 1f);
                IReadOnlyList<MountArrival> got = flow.PendingMountArrivals;
                for (int k = 0; k < got.Count; k++)
                {
                    arrivals.TryGetValue(got[k].kind, out int n);
                    arrivals[got[k].kind] = n + 1;
                    total++;
                    if (firstAt < 0f) firstAt = i * Dt;
                }
                flow.ClearPendingMountArrivals();
            }

            sb.AppendLine($"  마운트 도착 {Seconds:F0}초 동안 **{total}개** · 첫 도착 "
                          + (firstAt < 0f ? "**없음**" : $"{firstAt:F1}초"));
            foreach (KeyValuePair<FlowKind, int> kv in arrivals)
                sb.AppendLine($"    {kv.Key} ({(int)kv.Key}) · {kv.Value}개");

            // 만충 40 에 언제 닿는가 — 도착 수로만 본 어림이다(품목 구분 없음).
            if (total > 0)
            {
                float rate = total / Seconds;
                sb.AppendLine($"  초당 {rate:F2}개 → 40개까지 어림 "
                              + (rate > 0f ? $"{40f / rate:F1}초" : "안 참"));
            }

            // 끊긴 자리를 지목한다 — 화면의 「나가는 곳이 없다」와 **같은 규칙**을 쓴다
            // (`BoardController` 가 표식을 고를 때 쓰는 그것). 두 곳이 다른 규칙을 쓰면
            // 프로브가 화면과 다른 판을 재게 된다.
            var stuck = new List<string>();
            foreach (Vector2Int c in BeltRouting.DanglingWarningCells(grid))
            {
                NodeInstance inst = grid.GetAt(c);
                string who = inst != null && inst.Definition != null
                    ? inst.Definition.displayName : "(벨트)";
                if (stuck.Count < 10) stuck.Add($"{who}@{c}");
            }
            sb.AppendLine($"  「나가는 곳이 없다」 칸 **{stuck.Count}개**"
                          + (stuck.Count > 0 ? " — " + string.Join(" · ", stuck) : ""));
            return sb.ToString();
        }

        private static BoardGrid Board(bool fill)
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

        private static NodeDefinition Node(string id) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>(NodeRoot + "/Node_" + id + ".asset");
    }
}
