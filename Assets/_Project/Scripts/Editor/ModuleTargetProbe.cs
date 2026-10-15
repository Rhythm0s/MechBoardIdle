using System.Collections.Generic;
using System.Text;
using MBI.Core;
using MBI.Data;
using UnityEditor;
using UnityEngine;

namespace MBI.EditorTools
{
    /// <summary>
    /// **T4 대상 노드 고르기** — 시작 보드에서 모듈을 붙였을 때 **마운트 도착이 가장 많이 느는 노드**
    /// (2026-10-15 · 사용자 확정 #138 「대상 노드 = 구현이 고름 · 하네스로 재서」).
    ///
    /// 노드마다 판을 새로 세우고 M(또는 R) 하나를 붙인 뒤, 붙이지 않은 판과 **게임이 내는 출력**
    /// (`MunitionsLineFactory.BaseOutput` × 전력·발열 배율)을 견준다.
    ///
    /// ⚠️ 판 세우는 셈은 <see cref="BoardTickCostProbe"/> 와 같다(둘 다 `StartingBoard` 를 읽는다).
    /// 배치 실행: <c>-executeMethod MBI.EditorTools.ModuleTargetProbe.RunBatch</c>
    /// </summary>
    public static class ModuleTargetProbe
    {
        private const string NodeRoot = "Assets/_Project/ScriptableObjects/Nodes";
        private const string ModuleRoot = "Assets/_Project/ScriptableObjects/Modules";

        [MenuItem("MBI/Probe Module Target (T4)")]
        public static void RunMenu() => Debug.Log(Run());

        public static void RunBatch()
        {
            Debug.Log(Run());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== T4 대상 노드 실측 (2026-10-15 · #138) ===");
            var robot = AssetDatabase.LoadAssetAtPath<RobotDefinition>(SoRoot + "/Robots/Robot_A.asset");
            var config = AssetDatabase.LoadAssetAtPath<LogisticsConfig>(SoRoot + "/LogisticsConfig.asset");
            if (robot == null) return sb.AppendLine("Robot_A 자산 없음").ToString();

            // 📌 1차(품목 틱 · 마운트 도착 수)는 노드마다 증가 0 이었다 — 모듈 배수는 품목 틱이 아니라
            //    **물류 집계**(`LogisticsNetwork`)에 곱해진다. 그래서 게임이 「출력」을 내는 함수
            //    (`MunitionsLineFactory.BaseOutput` × 전력·발열 배율 · `LogisticsOutputProvider` 와 같다)로 잰다.
            float baseline = Output(null, null, robot, config, out string baseNote);
            sb.AppendLine($"[기준] 모듈 없음 · 출력 {baseline:F3} · {baseNote}");

            foreach (string kind in new[] { "M", "R" })
            {
                var module = AssetDatabase.LoadAssetAtPath<ModuleDefinition>(
                    ModuleRoot + "/Module_" + kind + ".asset");
                if (module == null) { sb.AppendLine($"  Module_{kind} 자산 없음"); continue; }

                sb.AppendLine($"[모듈 {kind}] {module.displayName}");
                Vector2Int best = new Vector2Int(-1, -1);
                float bestGain = float.MinValue;
                foreach (StartingBoard.Slot slot in StartingBoard.Nodes)
                {
                    if (slot.cell == StartingBoard.CoreCell) continue;   // 코어는 모듈 대상이 아니다
                    float got = Output(slot.cell, module, robot, config, out string note);
                    float gain = got - baseline;
                    sb.AppendLine($"  ({slot.cell.x},{slot.cell.y}) {slot.nodeId,-6} 출력 {got:F3} · 증가 {gain:+0.000;-0.000;0} · {note}");
                    if (gain > bestGain + 1e-4f) { bestGain = gain; best = slot.cell; }
                }
                sb.AppendLine($"  → 가장 큼: ({best.x},{best.y}) 증가 {bestGain:F3}");
            }
            return sb.ToString();
        }

        private const string SoRoot = "Assets/_Project/ScriptableObjects";

        private static float Output(Vector2Int? target, ModuleDefinition module,
            RobotDefinition robot, LogisticsConfig config, out string note)
        {
            note = string.Empty;
            BoardGrid g = Board();
            if (target.HasValue && module != null)
            {
                NodeInstance n = g.GetAt(target.Value);
                if (n == null || !n.TryAttachModule(module)) { note = "못 붙음"; return -1f; }
            }
            var connected = LogisticsReach.ConnectedNodes(g);
            WorkloadRate.Result work = WorkloadRate.Compute(g, connected, robot.balanceRef);
            NetworkAggregate agg = LogisticsNetwork.Aggregate(g, connected, work);
            float heatThreshold = config != null ? config.heatThreshold : 12f;
            ProductionThrottle throttle = LogisticsSimulation.Throttles(
                agg.powerSupply, agg.powerDraw,
                agg.heatGenerate, config != null ? config.moduleCoolingTbd : 0f, heatThreshold);
            float baseEff = MunitionsLineFactory.BaseOutput(robot, agg, new List<MunitionsLine>());
            note = $"기본 {baseEff:F3} · 배율 {throttle.Scale:F3} · 탄약 {agg.ammoProduce:F2} · 전력 {agg.powerDraw:F1}/{agg.powerSupply:F1}";
            return baseEff * throttle.Scale;
        }

        private static BoardGrid Board()
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f,
                Vector2.zero, PartLayout.BuildMask());
            StartingBoard.Apply(g, Node);
            foreach (StartingBoard.Run r in StartingBoard.Belts) StartingBoard.Place(g, r);
            StartingBoard.Place(g, StartingBoard.FillsEmptySlot);
            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        private static NodeDefinition Node(string id)
            => AssetDatabase.LoadAssetAtPath<NodeDefinition>(NodeRoot + "/Node_" + id + ".asset");
    }
}
