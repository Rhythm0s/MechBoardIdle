using System.Collections.Generic;
using System.Text;
using MBI.Core;
using MBI.Data;
using UnityEditor;
using UnityEngine;

namespace MBI.EditorTools
{
    /// <summary>
    /// 물류 실측 하네스 (2026-09-04 신설 · `260903_W02` 2-3).
    ///
    /// **빌드도 Play도 없이 60초를 돌린다.** 아이템 모델이 전부 순수 클래스라
    /// (<see cref="BeltItemFlow"/> · <see cref="BoardItemTick"/> · <see cref="NodeProduction"/>)
    /// 배치모드에서 <see cref="BoardItemTick.Step"/>을 정해진 횟수만큼 부르면 된다.
    ///
    /// **왜 필요한가.** 「60초를 언제부터 세는가」가 미결인데, 운송 지연 구간이 창에 들어가면
    /// 도착량이 항상 낮게 나오고 **일정하게 낮아서 결함으로도 안 보인다.** 틀린 줄 모르는
    /// 숫자가 가장 비싸므로, 출력을 도착량으로 바꾸기 전에 지연이 몇 초인지 먼저 잰다.
    ///
    /// **값을 만들지 않는다.** 노드 자산을 그대로 읽어 쓰며, 이 파일에는 밸런스 수치가 없다.
    /// 여기서 나오는 것은 판정이 아니라 관측치이고, 확정은 설계가 한다.
    ///
    /// 배치 실행: <c>-executeMethod MBI.EditorTools.LogisticsProbe.RunBatch</c>
    /// </summary>
    public static class LogisticsProbe
    {
        private const string NodeRoot = "Assets/_Project/ScriptableObjects/Nodes";

        // 관측 해상도. 작을수록 지연을 잘게 보지만 틱 수가 늘어난다.
        private const float StepSeconds = 0.05f;
        private const float ObserveSeconds = 60f;

        [MenuItem("MBI/Probe Logistics (60s)")]
        public static void RunMenu() => Debug.Log(Run());

        /// <summary>배치모드 진입점. 로그로만 남긴다 — 자산을 건드리지 않는다.</summary>
        public static void RunBatch()
        {
            Debug.Log(Run());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 물류 실측 (60초) ===");

            NodeDefinition muni = Load("Node_muni");
            NodeDefinition stor = Load("Node_stor");
            if (muni == null || stor == null)
                return "노드 자산을 못 찾았다 — MBI/Generate Balance + Nodes 먼저.";

            for (int lineLength = 1; lineLength <= 9; lineLength += 4)
            {
                sb.AppendLine();
                sb.AppendLine(Measure(muni, stor, lineLength));
            }
            return sb.ToString();
        }

        private static NodeDefinition Load(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodeRoot}/{name}.asset");

        /// <summary>
        /// 군수 노드 → 벨트 <paramref name="lineLength"/>칸 → 저장 노드.
        ///
        /// 자산이 정한 면을 그대로 쓴다 — 군수·저장 둘 다 **서쪽 입력 · 동쪽 출력**이라
        /// 라인은 서에서 동으로 흐른다. y=6은 팔L·몸통·팔R을 관통해 12칸이 전부 유효하다.
        /// </summary>
        private static string Measure(NodeDefinition muni, NodeDefinition stor, int lineLength)
        {
            var grid = new BoardGrid(12, 13, 1f, Vector2.zero, PartLayout.BuildMask());

            var nodeCell = new Vector2Int(0, 6);
            if (!grid.TryPlace(nodeCell, muni, out NodeInstance node))
                return $"라인 {lineLength}칸 — 생산 노드를 못 놓았다 (셀 {nodeCell})";

            for (int x = 1; x <= lineLength; x++)
            {
                grid.TryPlaceBelt(new Vector2Int(x, 6), PortFace.West, PortFace.East,
                    FlowKind.Ammo, out _);
            }

            var sinkCell = new Vector2Int(lineLength + 1, 6);
            if (!grid.TryPlace(sinkCell, stor, out _))
                return $"라인 {lineLength}칸 — 저장 노드를 못 놓았다 (셀 {sinkCell})";

            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            int steps = Mathf.RoundToInt(ObserveSeconds / StepSeconds);
            float firstArrivalAt = -1f;
            var perTenSec = new List<int>();
            int lastMark = 0;

            for (int i = 1; i <= steps; i++)
            {
                BoardItemTick.Step(grid, flow, StepSeconds, 1f);

                int arrived = flow.ArrivedOf(FlowKind.Ammo);
                if (firstArrivalAt < 0f && arrived > 0) firstArrivalAt = i * StepSeconds;

                if (i % Mathf.RoundToInt(10f / StepSeconds) != 0) continue;
                perTenSec.Add(arrived - lastMark);
                lastMark = arrived;
            }

            int total = flow.ArrivedOf(FlowKind.Ammo);
            var sb = new StringBuilder();
            sb.AppendLine($"[라인 {lineLength}칸] 군수 {nodeCell} → 벨트 {lineLength}칸 → 저장 {sinkCell}");
            sb.AppendLine($"  첫 도착: {(firstArrivalAt < 0f ? "없음" : firstArrivalAt.ToString("F2") + "초")}");
            sb.AppendLine($"  60초 총 도착: {total}개  (평균 {total / ObserveSeconds:F2}/초)");
            sb.AppendLine($"  10초 구간별: {string.Join(" · ", perTenSec)}");
            sb.AppendLine($"  노드 출력버퍼 잔량: {node.OutputBuffer:F2}");
            return sb.ToString();
        }
    }
}
