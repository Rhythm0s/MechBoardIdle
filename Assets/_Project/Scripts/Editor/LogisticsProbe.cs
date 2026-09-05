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

            sb.AppendLine();
            sb.AppendLine(MeasureChain());
            return sb.ToString();
        }

        /// <summary>
        /// **4단 체인의 첫 도착** (2026-09-04 · `260904_W02` 2-2 요청).
        ///
        /// 코어 → 가공 → 기초 군수 → 복합 군수 → 마운트(어깨 R). 단계마다 생산 시간이 붙고
        /// 그 사이마다 벨트가 있어, 1단 라인의 지연과는 자릿수가 다르다. 60초 창의
        /// **상한**(첫 도착이 영영 안 오면 0으로 확정)을 정하는 근거가 이 값이다.
        ///
        /// ⚠️ **한 줄에 늘어놓을 수 없다**(2026-09-05 재작성). 종전 판은 y=6 한 줄에 노드
        /// 넷을 번갈아 놓았는데, 그 배치는 노드가 전부 서→동이라는 가정 위에 있었다.
        /// 실제 포트는 그렇지 않다:
        ///   - **코어는 북으로 낸다**(남으로 전력을 받는다) — 동쪽 이웃에게 아무것도 안 준다
        ///   - **복합 군수는 두 면으로 받는다**(서=표준탄 · 남=부품) — 부품 라인을 갈라야 한다
        /// 그래서 라인이 층을 갈아타고, 분류기가 부품을 두 갈래로 나눈다. 이 굴곡 자체가
        /// 지연의 큰 몫이므로 **직선으로 재면 실제보다 짧게 나온다.**
        ///
        /// <code>
        ///   y=10  어깨R                          벨트(11,10)→마운트◀
        ///   y=7                        벨트(9,7)→복합군수(10,7)→벨트(11,7)↑
        ///   y=5   코어(5,4)↑ 벨트(5,5) 가공(6,5) 분류기(7,5) 기초군수(8,5) 벨트(9,5)↑
        ///   y=4                        분류기↓ 벨트(7,4)→(8,4)→(9,4)→(10,4)↑→(10,5)(10,6)↑
        /// </code>
        /// </summary>
        private static string MeasureChain()
        {
            NodeDefinition core = Load("Node_core");
            NodeDefinition proc = Load("Node_proc");
            NodeDefinition muni = Load("Node_muni");
            NodeDefinition munix = Load("Node_munix");
            if (core == null || proc == null || muni == null || munix == null)
                return "[4단 체인] 노드 자산이 모자란다";

            var grid = new BoardGrid(12, 13, 1f, Vector2.zero, PartLayout.BuildMask());
            var sb = new StringBuilder();
            var placed = new List<string>();

            void Node(int nx, int ny, NodeDefinition def)
            {
                if (grid.TryPlace(new Vector2Int(nx, ny), def, out _))
                    placed.Add($"{def.displayName}({nx},{ny})");
                else
                    sb.AppendLine($"  ⚠️ {def.displayName}을 ({nx},{ny})에 못 놓았다");
            }

            void Belt(int bx, int by, PortFace inF, PortFace outF)
            {
                if (!grid.TryPlaceBelt(new Vector2Int(bx, by), inF, outF, FlowKind.None, out _))
                    sb.AppendLine($"  ⚠️ 벨트 ({bx},{by})를 못 놓았다");
            }

            // 1단 — 코어. 북으로 코어 에너지를 낸다.
            Node(5, 4, core);
            Belt(5, 5, PortFace.South, PortFace.East);

            // 2단 — 가공. 코어 에너지를 먹어 기초재료·부품을 낸다.
            Node(6, 5, proc);

            // 부품을 두 갈래로: 동쪽은 기초 군수, 남쪽은 복합 군수의 둘째 입력.
            if (!grid.TryPlaceBeltElement(new Vector2Int(7, 5), BeltElementKind.Sorter,
                    new[] { PortFace.West }, new[] { PortFace.East, PortFace.South },
                    FlowKind.None, out _))
                sb.AppendLine("  ⚠️ 분류기 (7,5)를 못 놓았다");

            // 3단 — 기초 군수. 부품을 먹어 표준탄을 낸다.
            Node(8, 5, muni);

            // 표준탄 라인 — 위로 올라가 복합 군수의 서쪽 면으로.
            Belt(9, 5, PortFace.West, PortFace.North);
            Belt(9, 6, PortFace.South, PortFace.North);
            Belt(9, 7, PortFace.South, PortFace.East);

            // 부품 라인 — 아래로 돌아 복합 군수의 남쪽 면으로.
            Belt(7, 4, PortFace.North, PortFace.East);
            Belt(8, 4, PortFace.West, PortFace.East);
            Belt(9, 4, PortFace.West, PortFace.East);
            Belt(10, 4, PortFace.West, PortFace.North);
            Belt(10, 5, PortFace.South, PortFace.North);
            Belt(10, 6, PortFace.South, PortFace.North);

            // 4단 — 복합 군수. 표준탄 + 부품을 먹어 관통탄을 낸다.
            Node(10, 7, munix);
            NodeInstance complex = grid.GetAt(new Vector2Int(10, 7));

            // 마운트로 — 어깨 R 바깥면(11,10)이 고정 포트다.
            Belt(11, 7, PortFace.West, PortFace.North);
            Belt(11, 8, PortFace.South, PortFace.North);
            Belt(11, 9, PortFace.South, PortFace.North);
            Belt(11, 10, PortFace.South, PortFace.East);

            // 실제 경로와 같은 순서로 푼다: 면 → 품목. 이걸 빼면 벨트 kind가 None으로 남아
            // 링크가 안 서고, 노드는 도는데 아무것도 안 흐르는 상태가 된다.
            BeltAutoOrient.Resolve(grid);
            BeltFlow.Resolve(grid);

            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            int steps = Mathf.RoundToInt(ObserveSeconds / StepSeconds);
            float firstMount = -1f, firstAnywhere = -1f;
            for (int i = 1; i <= steps; i++)
            {
                BoardItemTick.Step(grid, flow, StepSeconds, 1f);
                if (firstAnywhere < 0f && flow.DeliveredCount > 0) firstAnywhere = i * StepSeconds;
                if (firstMount < 0f && flow.MountArrivedOf(FlowKind.PierceAmmo) > 0)
                    firstMount = i * StepSeconds;
            }

            sb.AppendLine($"[4단 체인] {string.Join(" → ", placed)}");
            sb.AppendLine($"  첫 도착(어디로든): " +
                          $"{(firstAnywhere < 0f ? "60초 안에 없음" : firstAnywhere.ToString("F2") + "초")}");
            sb.AppendLine($"  첫 마운트 도착(관통탄): " +
                          $"{(firstMount < 0f ? "60초 안에 없음" : firstMount.ToString("F2") + "초")}");
            sb.AppendLine($"  60초 마운트 도착: {flow.MountArrivedOf(FlowKind.PierceAmmo)}개");
            sb.AppendLine($"  60초 노드 간 도착 총계: {flow.DeliveredCount}개");
            sb.AppendLine($"  중간 산출 — 부품 {flow.ArrivedOf(FlowKind.BasicParts)} · " +
                          $"표준탄 {flow.ArrivedOf(FlowKind.StandardAmmo)} · " +
                          $"관통탄 {flow.ArrivedOf(FlowKind.PierceAmmo)}");

            // 마지막 단이 왜 안 도는지 — 굶었는가, 산출이 갈 곳이 없는가.
            if (complex != null)
            {
                sb.AppendLine($"  복합 군수 조합표: {complex.CurrentRecipe.kind} " +
                              $"(가동 {complex.CurrentRecipe.IsRunnable} · " +
                              $"{complex.CurrentRecipe.outputPerSec:F3}/초)");
                sb.Append("  복합 군수 입력버퍼:");
                foreach (KeyValuePair<FlowKind, float> kv in complex.InputBuffer)
                    sb.Append($" {kv.Key}={kv.Value:F1}");
                sb.AppendLine($"  (굶음 {complex.IsStarved})");
                sb.AppendLine($"  복합 군수 출력버퍼: {complex.OutputBuffer:F2} ({complex.BufferKind})");
            }
            return sb.ToString();
        }

        private static NodeDefinition Load(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodeRoot}/{name}.asset");

        /// <summary>
        /// 군수 노드 → 벨트 <paramref name="lineLength"/>칸 → 저장 노드.
        ///
        /// 자산이 정한 면을 그대로 쓴다 — 군수·저장 둘 다 **서쪽 입력 · 동쪽 출력**이라
        /// 라인은 서에서 동으로 흐른다. y=6은 팔R·몸통·팔L을 관통해 12칸이 전부 유효하다.
        ///
        /// ⚠️ **재료를 매 틱 먹인다**(2026-09-05). 여기서 재는 것은 「벨트가 한 칸을 나르는 데
        /// 몇 초 걸리는가」이지 「체인이 재료를 대는가」가 아니다. 레시피 개정으로 기초 군수가
        /// 부품을 먹기 시작하면서 이 하네스가 통째로 0을 냈는데, 그것은 벨트가 느린 것이 아니라
        /// **앞단이 없는 것**이라 지연 측정에 섞이면 안 된다. 상류를 무한 공급으로 고정해
        /// 벨트 구간만 남긴다.
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
                    FlowKind.StandardAmmo, out _);
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
                // 상류 무한 공급 — 굶어서 안 나오는 것과 벨트가 느린 것을 가른다.
                foreach (RecipeInput need in node.CurrentRecipe.inputs ?? new List<RecipeInput>())
                    node.TakeInput(need.kind, 100f);

                BoardItemTick.Step(grid, flow, StepSeconds, 1f);

                int arrived = flow.ArrivedOf(FlowKind.StandardAmmo);
                if (firstArrivalAt < 0f && arrived > 0) firstArrivalAt = i * StepSeconds;

                if (i % Mathf.RoundToInt(10f / StepSeconds) != 0) continue;
                perTenSec.Add(arrived - lastMark);
                lastMark = arrived;
            }

            int total = flow.ArrivedOf(FlowKind.StandardAmmo);
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
