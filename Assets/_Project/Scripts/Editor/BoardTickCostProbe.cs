using System.Diagnostics;
using System.Text;
using MBI.Core;
using MBI.Data;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MBI.EditorTools
{
    /// <summary>
    /// 보드 한 틱이 **얼마나 걸리는가** (2026-09-16 신설 · 플랜 §74-16 ① 착수 전 질문).
    ///
    /// **왜 재는가.** 보드를 로봇별 둘로 가르면 「둘 다 매 틱 돈다」가 규칙이다
    /// (대기 로봇 보드도 채워야 한다). 그러면 이 비용이 **두 배**가 되는데,
    /// 두 배가 WebGL 프레임 예산을 먹는지 아닌지를 **재지 않고는 모른다.**
    ///
    /// 📌 **재는 것은 `BoardItemTick.Step` 하나다** — 물류 집계·조립·HUD 는 안 잰다.
    /// 가르는 것은 틱이고, 나머지는 보드 수와 함께 늘지 않거나 이 묶음 밖이다.
    /// 그것까지 재서 한 수로 내면 **무엇이 두 배가 되는지가 흐려진다.**
    ///
    /// ⚠️ **에디터에서 잰 수다.** WebGL 은 같은 코드를 다른 기계에서 돌린다 —
    /// 절대값이 아니라 **한 배 대 두 배**를 보는 데 쓴다.
    /// </summary>
    public static class BoardTickCostProbe
    {
        private const string NodeRoot = "Assets/_Project/ScriptableObjects/Nodes";

        /// <summary>한 틱의 길이 — 60fps 한 프레임.</summary>
        private const float Dt = 1f / 60f;

        /// <summary>몇 틱을 재는가. 짧으면 첫 틱의 준비 비용이 평균을 지배한다.</summary>
        private const int Steps = 6000;

        /// <summary>재기 전에 몇 틱 굴려 두는가 — 벨트가 빈 판은 할 일이 없어 빠르다.</summary>
        private const int WarmSteps = 1200;

        [MenuItem("MBI/Probe Board Tick Cost")]
        public static void RunMenu() => Debug.Log(Run());

        public static void RunBatch()
        {
            Debug.Log(Run());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 보드 한 틱 비용 (2026-09-16) ===");

            BoardGrid grid = Board();
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            // 판을 채운다 — 빈 벨트는 밀 것이 없어 실제보다 빠르게 나온다.
            for (int i = 0; i < WarmSteps; i++) BoardItemTick.Step(grid, flow, Dt, 1f);

            var watch = Stopwatch.StartNew();
            for (int i = 0; i < Steps; i++) BoardItemTick.Step(grid, flow, Dt, 1f);
            watch.Stop();

            double perTickMs = watch.Elapsed.TotalMilliseconds / Steps;
            double budgetMs = 1000.0 / 60.0;

            sb.AppendLine($"[판] {PartLayout.Columns}x{PartLayout.Rows} · 시작 배치 · {Steps} 틱 (준비 {WarmSteps} 틱)");
            sb.AppendLine($"  이어진 품목 {flow.DeliveredCount} 개 도착 뒤의 판에서 쟀다");
            sb.AppendLine();
            sb.AppendLine("[결과]");
            sb.AppendLine($"  한 보드 한 틱 **{perTickMs * 1000.0:F1} 마이크로초** ({perTickMs:F4} ms)");
            sb.AppendLine($"  보드 둘   한 틱 **{perTickMs * 2000.0:F1} 마이크로초** ({perTickMs * 2:F4} ms)");
            sb.AppendLine($"  60fps 한 프레임 예산 {budgetMs:F2} ms 의 " +
                          $"**{perTickMs * 2 / budgetMs * 100.0:F2} %**");
            sb.AppendLine();

            // 판정을 여기서 낸다 — 수만 내면 읽는 사람이 매번 같은 나눗셈을 한다.
            if (perTickMs * 2 < budgetMs * 0.05)
                sb.AppendLine("  ✅ **둘을 매 틱 돌려도 된다** — 프레임 예산의 5% 미만이다.");
            else if (perTickMs * 2 < budgetMs * 0.20)
                sb.AppendLine("  ⚠️ 둘을 돌리면 프레임 예산의 5~20% 다 — 돌아는 가지만 지켜볼 것.");
            else
                sb.AppendLine("  ⛔ **둘을 매 틱 돌리면 프레임을 먹는다** — 대기 보드 반 틱 갈래를 볼 것.");

            return sb.ToString();
        }

        // ⚠️ 판 세우는 셈은 `StageClearHarness` 와 **같아야 한다** — 다르면 다른 판을 재게 된다.
        // 지금은 복사본이다(둘 다 `StartingBoard` 를 읽으므로 값은 한 곳에 산다).
        private static BoardGrid Board()
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f,
                Vector2.zero, PartLayout.BuildMask());
            foreach (StartingBoard.Slot s in StartingBoard.Nodes)
                g.TryPlace(s.cell, Node(s.nodeId), out _);
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
