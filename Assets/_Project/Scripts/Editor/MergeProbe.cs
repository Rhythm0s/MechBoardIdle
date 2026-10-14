using System.Collections.Generic;
using System.Text;
using MBI.Core;
using MBI.Data;
using UnityEditor;
using UnityEngine;

namespace MBI.EditorTools
{
    /// <summary>
    /// 【프로브】 **병합기 줄의 칸별 통과량** (2026-10-14 사용자 지시).
    ///
    /// 물음 — 사용자가 8125 육안에서 **「병합기 뒤로 탄이 안 나오는 것 같다」**고 봤다.
    /// 그래서 네 칸(+비교 하나)에서 **무엇이 · 초당 몇 개** 지나가는지 센다.
    ///
    /// ⚠️ **판정하지 않는다. 고치지도 않는다.** 수만 낸다(촬영 빌드 동결).
    /// ⚠️ **게임 코드를 안 건드린다** — 이 파일은 `Editor` 폴더라 빌드에 안 들어간다.
    ///
    /// 📌 **보드는 <see cref="BeltItemKindProbe.Board"/> 가 세운다** — 같은 판을 두 번
    /// 세우면 한쪽만 고쳐지는 날이 온다(지침 §7). 튜토리얼을 마친 판(빈 칸 채움)이다.
    ///
    /// ── 재는 법 (지침 §10) ───────────────────────────────────────────────
    ///
    /// **진입 수**를 센다. 아이템은 한 틱에 <c>CellsPerSecondTbd × Dt</c> = 0.2 칸 움직이므로,
    /// `progress &lt; 0.2` 인 아이템은 **이번 틱에 그 칸으로 들어온 것**이다.
    /// 같은 아이템을 두 번 세지 않으려고 **직전 틱의 수와 견주어 늘어난 만큼만** 더한다.
    ///
    /// ⚠️ **한계** — 칸이 정체되어 아이템이 입구에 멈춰 있으면 그 사이 들어오는 것을
    /// 놓칠 수 있다. 이 보드는 초당 두어 개라 간격이 열 틱쯤이어서 겹칠 일이 드물지만,
    /// **정체가 보이면 이 수는 하한으로 읽어야 한다.**
    ///
    /// 📌 **노드의 「출력」은 다음 칸의 「진입」으로 잰다** — 노드 칸에는 아이템이 안 얹힌다.
    ///   · 군수 (7,8) 출력 = 벨트 (8,8) 진입
    ///   · 군수 (7,7) 출력 = 병합기 (8,7) 진입(서면)
    ///   · 병합기 (8,7) 출력 = 벨트 (8,6) 진입
    ///
    /// 배치 실행: <c>-executeMethod MBI.EditorTools.MergeProbe.RunBatch</c>
    /// </summary>
    public static class MergeProbe
    {
        private const float Dt = 0.05f;
        private const float Seconds = 60f;

        /// <summary>한 틱에 움직이는 칸 수 — 이보다 덜 간 것이 이번 틱에 들어온 것이다.</summary>
        private static float StepCells => BeltItemFlow.CellsPerSecondTbd * Dt;

        /// <summary>보는 칸들. ⚠️ 좌표는 <c>StartingBoard</c> 에서 읽은 것이다(지어내지 않았다).</summary>
        private static readonly (Vector2Int cell, string label)[] Watched =
        {
            (new Vector2Int(8, 8), "벨트 (8,8)  ← 동 줄 군수 (7,8) 가 내는 것"),
            (new Vector2Int(8, 7), "병합기 (8,7) ← 남 줄 군수 (7,7)[서] + 동 줄[북]"),
            (new Vector2Int(8, 6), "벨트 (8,6)  ← **병합기가 내는 것**"),
            (new Vector2Int(8, 5), "벨트 (8,5)  ← 그 아래 (운반로로 꺾기 전)"),
            (new Vector2Int(1, 4), "벨트 (1,4)  ← 마운트 고정 포트 바로 앞"),
        };

        [MenuItem("MBI/Probe Merge Line")]
        public static void RunMenu() => Debug.Log(Run());

        public static void RunBatch()
        {
            Debug.Log(Run());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 병합기 줄 칸별 통과량 (2026-10-14 · 사용자 지시) ===");
            sb.AppendLine();
            sb.AppendLine($"판 = 시작 보드 A · 튜토리얼 마친 판(빈 칸 채움) · {Seconds:F0}초 · 틱 {Dt:F2}초");
            sb.AppendLine($"한 틱 이동 = {StepCells:F2} 칸 — 그보다 덜 간 것을 「이번 틱 진입」으로 센다");
            sb.AppendLine("⚠️ 로봇 화력과 무관하다 — 소비처는 마운트뿐이고 전투는 안 돈다");
            sb.AppendLine();

            BoardGrid grid = BeltItemKindProbe.Board();
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            // 칸 → 품목 → 진입 수
            var entered = new Dictionary<Vector2Int, Dictionary<FlowKind, int>>();
            var prevNew = new Dictionary<Vector2Int, int>();
            var everOccupied = new Dictionary<Vector2Int, int>();
            foreach ((Vector2Int c, string _) in Watched)
            {
                entered[c] = new Dictionary<FlowKind, int>();
                prevNew[c] = 0;
                everOccupied[c] = 0;
            }

            // 노드가 받은 것(소비) — 어느 노드가 무엇을 먹는지
            var consumed = new Dictionary<Vector2Int, Dictionary<FlowKind, int>>();
            var mount = new Dictionary<FlowKind, int>();

            int steps = Mathf.RoundToInt(Seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                BoardItemTick.Step(grid, flow, Dt, 1f);

                foreach (Arrival a in flow.PendingArrivals)
                {
                    if (!consumed.TryGetValue(a.cell, out Dictionary<FlowKind, int> m))
                        consumed[a.cell] = m = new Dictionary<FlowKind, int>();
                    m[a.kind] = (m.TryGetValue(a.kind, out int n) ? n : 0) + 1;
                }
                flow.ClearPendingArrivals();

                foreach (MountArrival ma in flow.PendingMountArrivals)
                    mount[ma.kind] = (mount.TryGetValue(ma.kind, out int n) ? n : 0) + 1;
                flow.ClearPendingMountArrivals();

                foreach ((Vector2Int c, string _) in Watched)
                {
                    IReadOnlyList<BeltItem> items = flow.ItemsAt(c);
                    if (items.Count > 0) everOccupied[c]++;

                    int fresh = 0;
                    for (int k = 0; k < items.Count; k++)
                        if (items[k].progress < StepCells) fresh++;

                    // ⚠️ 늘어난 만큼만 센다 — 멈춰 있는 것을 매 틱 다시 세지 않으려고.
                    int add = fresh - prevNew[c];
                    if (add > 0)
                    {
                        // 어떤 품목인지는 갓 들어온 것들에서 고른다.
                        int tagged = 0;
                        for (int k = 0; k < items.Count && tagged < add; k++)
                        {
                            if (items[k].progress >= StepCells) continue;
                            FlowKind kind = items[k].kind;
                            entered[c][kind] = (entered[c].TryGetValue(kind, out int n) ? n : 0) + 1;
                            tagged++;
                        }
                    }
                    prevNew[c] = fresh;
                }
            }

            sb.AppendLine("## 칸별 진입 (= 그 칸을 지나간 것)");
            sb.AppendLine();
            sb.AppendLine("| 칸 | 품목 | 60초 개수 | 개수/초 | 점유 틱 |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach ((Vector2Int c, string label) in Watched)
            {
                if (entered[c].Count == 0)
                {
                    sb.AppendLine($"| {label} | **없음** | **0** | **0.00** | {everOccupied[c]} |");
                    continue;
                }
                foreach (KeyValuePair<FlowKind, int> kv in entered[c])
                    sb.AppendLine($"| {label} | {kv.Key} | {kv.Value} | "
                                  + $"{kv.Value / Seconds:F2} | {everOccupied[c]} |");
            }

            sb.AppendLine();
            sb.AppendLine("## 진단 — 모든 벨트 칸의 점유 틱 (상위 16)");
            sb.AppendLine();
            var occ = new List<KeyValuePair<Vector2Int, int>>();
            foreach (StartingBoard.Run r in StartingBoard.Belts)
            {
                var c = new Vector2Int(r.cell.x, r.cell.y);
                occ.Add(new KeyValuePair<Vector2Int, int>(c, flow.ItemsAt(c).Count));
            }
            occ.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; i < occ.Count && i < 16; i++)
                sb.AppendLine($"  ({occ[i].Key.x},{occ[i].Key.y}) 지금 {occ[i].Value}개");

            sb.AppendLine();
            sb.AppendLine("## 노드가 받은 것 (소비처 도착)");
            sb.AppendLine();
            if (consumed.Count == 0) sb.AppendLine("(없음)");
            foreach (KeyValuePair<Vector2Int, Dictionary<FlowKind, int>> kv in consumed)
                foreach (KeyValuePair<FlowKind, int> k2 in kv.Value)
                    sb.AppendLine($"  ({kv.Key.x},{kv.Key.y}) ← {k2.Key} {k2.Value}개 "
                                  + $"· {k2.Value / Seconds:F2}/초");

            sb.AppendLine();
            sb.AppendLine("## 마운트 도착");
            sb.AppendLine();
            if (mount.Count == 0) sb.AppendLine("  **0 개** — 줄 어딘가에서 끊겼다");
            foreach (KeyValuePair<FlowKind, int> kv in mount)
                sb.AppendLine($"  {kv.Key} {kv.Value}개 · {kv.Value / Seconds:F2}/초");

            sb.AppendLine();
            sb.AppendLine("=== 끝 ===");
            return sb.ToString();
        }
    }
}
