using System.Collections.Generic;
using System.Text;
using MBI.Core;
using MBI.Data;
using UnityEditor;
using UnityEngine;

namespace MBI.EditorTools
{
    /// <summary>
    /// 표준탄 좌표 확정 뒤의 실측 넷 (`260909_W01` 2-6).
    ///
    /// ⚠️ **값만 낸다. 판정하지 않는다.** W01 2-6이 물은 것은 「통과시키는가」·「실제로
    /// 나오는가」·「갈아탈 이유가 있는가」인데, 그 답은 설계가 낸다. 여기서 하는 일은
    /// **견줄 두 숫자를 같은 화면에 올려 놓는 것**뿐이다.
    ///
    /// ⚠️ **기준점 100을 여기서 재산출하지 않는다.** W01 2-5가 그것을 재산출 목록에
    /// 올려 두면서 「값은 오늘 정하지 않는다 — 산정 방법론대로 실측에 스냅한다」고 적었다.
    /// 이 하네스는 스냅할 실측을 대는 것이지 스냅을 대신하는 것이 아니다.
    ///
    /// **값을 만들지 않는다** — 노드·스테이지 자산과 W01 2-3 회계표만 읽는다.
    ///
    /// 배치 실행: <c>-executeMethod MBI.EditorTools.AmmoCoordProbe.RunBatch</c>
    /// </summary>
    public static class AmmoCoordProbe
    {
        private const string NodeRoot = "Assets/_Project/ScriptableObjects/Nodes";
        private const string StageRoot = "Assets/_Project/ScriptableObjects/Stages";

        /// <summary>
        /// 벨트 한 줄 처리량(개/초). <c>BeltItemFlow</c> 주석이 적어 둔 값 — 칸당 속도 4와
        /// 최소 간격 1/3에서 나온다. **여기서 정하는 값이 아니라 옮겨 적은 값이다.**
        /// </summary>
        private const float BeltLaneThroughput = 12f;

        /// <summary>
        /// W01 2-3 「탄종별 노드 회계」 — 1발/초에 드는 노드가 무엇 무엇인가.
        /// **코어 라인은 세지 않는다**(표가 그렇게 적었다).
        /// </summary>
        private readonly struct Chain
        {
            public readonly string label;
            public readonly float damage;
            public readonly float lineSpec;
            public readonly string[] nodesPerShot;

            public Chain(string label, float damage, float lineSpec, params string[] nodesPerShot)
            {
                this.label = label; this.damage = damage;
                this.lineSpec = lineSpec; this.nodesPerShot = nodesPerShot;
            }
        }

        [MenuItem("MBI/Probe Ammo Coordinates")]
        public static void RunMenu() => Debug.Log(Run());

        public static void RunBatch()
        {
            Debug.Log(Run());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string Run()
        {
            BalanceConfig bal = AssetDatabase.LoadAssetAtPath<BalanceConfig>(
                "Assets/_Project/ScriptableObjects/BalanceConfig.asset");
            if (bal == null) return "BalanceConfig을 못 찾았다 — MBI/Generate Balance + Nodes 먼저.";

            // 체인 구성은 W01 2-3 표 그대로다.
            // 표준 = 기초 군수 + 부품 가공.
            // 관통 = 복합 군수 + 표준탄 라인 둘 + 부품 가공.
            // 폭발 = 복합 군수 + 표준탄 라인 둘 + 발전재료 가공.
            // ⚠️ 가공 노드 자산이 하나(proc)라 부품과 발전재료가 같은 값을 쓴다.
            Chain std = new Chain("표준", 10f, bal.LineSpecOf(AmmoKind.Standard), "muni", "proc");
            Chain pierce = new Chain("관통", 20f, bal.LineSpecOf(AmmoKind.Pierce),
                "munix", "muni", "proc", "proc");
            Chain expl = new Chain("폭발", 50f, bal.LineSpecOf(AmmoKind.Explosive),
                "munix", "muni", "proc", "proc");

            var sb = new StringBuilder();
            sb.AppendLine("=== 표준탄 좌표 실측 (260909_W01 2-6) ===");
            sb.AppendLine("주의: 값만이다. 판정과 기준점 재산출은 설계 몫이다.");

            One(sb, std);
            Two(sb, pierce, expl);
            Three(sb, bal, std, pierce, expl);
            Four(sb, pierce, expl);
            Five(sb, bal, std);
            Six(sb);

            return sb.ToString();
        }

        // ---- 1. 표준탄 단독 60이 튜토리얼·S1을 통과시키는가 ----

        private static void One(StringBuilder sb, Chain std)
        {
            sb.AppendLine();
            sb.AppendLine("[1] 표준탄 단독 출력과 초반 스테이지 요구치");
            sb.AppendLine($"  표준탄 단독 최대 = {Max(std):F0} (라인 스펙 {std.lineSpec:F0} x 발당 {std.damage:F0})");
            sb.AppendLine("  스테이지 | 요구 종류 | 요구치 | 표준 단독 - 요구치");

            foreach (string id in new[] { "S0", "S1", "S2", "S3" })
            {
                var st = AssetDatabase.LoadAssetAtPath<StageDefinition>($"{StageRoot}/Stage_{id}.asset");
                if (st == null) { sb.AppendLine($"  {id} | (자산 없음)"); continue; }
                sb.AppendLine($"  {id} | {st.reqType} | {st.req:F0} | {Max(std) - st.req:F0}");
            }
            sb.AppendLine("  주의: 차가 음수면 표준 단독으로 그 요구치에 못 닿는다는 관측이다 — 통과 판정이 아니다.");
        }

        // ---- 2. 관통 4 + 폭발 2 = 180이 실제로 나오는가 ----

        private static void Two(StringBuilder sb, Chain pierce, Chain expl)
        {
            sb.AppendLine();
            sb.AppendLine("[2] 관통 4 + 폭발 2 — 벨트와 전력이 먼저 막는가");

            int nodes = 4 * pierce.nodesPerShot.Length + 2 * expl.nodesPerShot.Length;
            float output = 4f * pierce.damage + 2f * expl.damage;
            sb.AppendLine($"  출력 {output:F0} · 노드 {nodes}대 (W01 2-4 = 180 · 24대)");

            sb.AppendLine($"  마운트행 아이템 6/초 · 벨트 한 줄 처리량 {BeltLaneThroughput:F0}/초 " +
                          $"· 여유 {BeltLaneThroughput - 6f:F0}/초");
            sb.AppendLine("  주의: 중간 줄(표준탄 8/초 · 부품)은 배치에 따라 갈려 여기서 세지 않는다.");

            float draw = 4f * ChainDraw(pierce) + 2f * ChainDraw(expl);
            sb.AppendLine($"  전력 소비 {draw:F0}/초 (체인 전체 · 코어 라인 제외)");
            sb.AppendLine($"  {EnergyLine(draw)}");
        }

        // ---- 3. 140에서 180으로 갈아탈 이유가 있는가 ----

        private static void Three(StringBuilder sb, BalanceConfig bal, Chain std, Chain pierce, Chain expl)
        {
            sb.AppendLine();
            sb.AppendLine("[3] 조합별 출력·노드와 스테이지 요구치");
            sb.AppendLine("  조합 | 출력 | 노드");
            Combo(sb, "표준 6", 0, 6, 0, std, pierce, expl);
            Combo(sb, "표준 4 + 폭발 2", 0, 4, 2, std, pierce, expl);
            Combo(sb, "관통 5 + 폭발 1", 5, 0, 1, std, pierce, expl);
            Combo(sb, "관통 4 + 폭발 2", 4, 0, 2, std, pierce, expl);

            sb.AppendLine("  스테이지 요구치:");
            foreach (string id in new[] { "S1", "S2", "S3", "S4", "S5" })
            {
                var st = AssetDatabase.LoadAssetAtPath<StageDefinition>($"{StageRoot}/Stage_{id}.asset");
                if (st != null) sb.AppendLine($"    {id} = {st.req:F0} ({st.reqType})");
            }
            // ④ 대표 조합에 강화를 걸면 얼마가 되는가 (`260910_W01` 3-2).
            // **배수는 BalanceConfig 의 enh 를 그대로 쓴다** — 여기서 새 상수를 만들지 않는다.
            float rep = 4f * std.damage + 2f * expl.damage;
            sb.AppendLine($"  대표 조합(표준 4 + 폭발 2) {rep:F0} x 강화 {bal.enh:F2} = {rep * bal.enh:F0}");
            sb.AppendLine("  주의: 갈아탈 이유가 있는가는 이 두 표를 겹쳐 보고 설계가 답한다.");
        }

        // ---- 4. 복합 군수 전력 3에서 에너지 노드가 몇 대 드는가 ----

        private static void Four(StringBuilder sb, Chain pierce, Chain expl)
        {
            sb.AppendLine();
            sb.AppendLine("[4] 복합 군수 대당 전력 3에서 드는 에너지 노드 수");

            NodeDefinition munix = Node("munix");
            NodeDefinition ener = Node("ener");
            if (munix == null || ener == null) { sb.AppendLine("  노드 자산 없음"); return; }

            sb.AppendLine($"  복합 군수 대당 전력 = {munix.resources.powerDraw:F0} " +
                          $"(확정 여부 {munix.resources.confirm})");
            sb.AppendLine($"  에너지 대당 공급 {ener.resources.powerSupply:F0} · 자기 소비 " +
                          $"{ener.resources.powerDraw:F0} → 순 공급 {NetSupply(ener):F0}/대");

            float withThree = 4f * ChainDraw(pierce) + 2f * ChainDraw(expl);
            float withZero = withThree - 6f * munix.resources.powerDraw;
            sb.AppendLine($"  관통 4 + 폭발 2 소비 {withThree:F0}/초 → {EnergyLine(withThree)}");
            sb.AppendLine($"  복합 군수 전력이 0이었을 때 {withZero:F0}/초 → {EnergyLine(withZero)}");
            sb.AppendLine("  주의: 값 3은 잠정이다 — W01 2-6이 크기를 정할 잣대가 없다고 적은 자리다.");
        }

        // ---- 5. 시작 보드 (260910_W01 8-5 제약 넷·다섯) ----

        /// <summary>
        /// **W01 8-5 가 물은 셋을 잰다** — 완료 출력 · 마운트 채움 시간 · 보드 칸 수.
        ///
        /// ⚠️ **구성은 `MountDeliveryTests.BuildStartingBoard` 와 같게 맞춘다.**
        /// 처음에는 벨트를 깔고 <see cref="BeltAutoOrient"/>·<see cref="BeltFlow"/> 를 **안 풀어서**
        /// 아무것도 안 이어졌고, 그 상태로 「출력 0」을 냈다(폐기). 벨트는 놓는 것만으로 흐르지 않는다.
        ///
        /// ⚠️ **도착으로 잰다.** 노드를 세는 회계가 아니라 **마운트에 실제로 닿은 것**을 센다 —
        /// 밸런스 문서가 실제 전투력을 도착량으로 정했고, 벨트가 끊기면 회계는 안 변해도 도착은 0이 된다.
        /// </summary>
        private static void Five(StringBuilder sb, BalanceConfig bal, Chain std)
        {
            sb.AppendLine();
            sb.AppendLine("[5] 시작 보드 — W01 8-5 가 물은 셋");

            var mask = PartLayout.BuildMask();
            sb.AppendLine($"  보드 격자 {PartLayout.Columns} x {PartLayout.Rows} = " +
                          $"{PartLayout.Columns * PartLayout.Rows}칸 · 실루엣 안 {mask.Count}칸");

            const float Window = 60f;
            int emptyCount = Arrivals(BuildStartingBoard(false), Window, out _);
            int filledCount = Arrivals(BuildStartingBoard(true), Window, out float firstAt);

            sb.AppendLine($"  {Window:F0}초 도착 개수 — 빈 칸 채우기 전 {emptyCount} → 채운 뒤 {filledCount}");

            float perSec = filledCount / Window;
            float output = perSec * std.damage;
            sb.AppendLine($"  완료 출력 = {output:F1} ({perSec:F2}개/초 x 발당 {std.damage:F0})");
            sb.AppendLine($"  첫 도착까지 {firstAt:F1}초 (운송 지연)");

            float cap = bal.mountStackLimit * MountLoad.SlotsRobotA;
            if (perSec > 0f)
                sb.AppendLine($"  마운트 {cap:F0} 채움 = {firstAt + cap / perSec:F0}초 " +
                              $"(첫 도착 {firstAt:F1} + {cap:F0} / {perSec:F2})");
            else
                sb.AppendLine($"  마운트 {cap:F0} 채움 = 안 찬다 (도착 0)");

            sb.AppendLine("  주의: 제약 4(약 8초)와 5(S1 을 여유 있게)에 닿는지는 설계가 판정한다.");

            // ── 빈 칸을 어디로 둘 것인가 (2026-09-11 · V01 판정 요청 재료)
            //
            // 튜토리얼 기획서 4-2 는 「놓기 전 0」을 종료 조건으로 삼는다. 그런데 네 줄이
            // 되면서 **군수 한 대를 비워도 나머지 셋이 흐른다** — 수업이 안 선다.
            // 대안은 **합류 뒤 운반로 벨트 한 칸**을 비우는 것이다. 둘을 나란히 잰다.
            const float W = 60f;
            int nodeGap = Arrivals(BuildStartingBoard(false), W, out _);
            int beltGap = Arrivals(BuildStartingBoardMissingBelt(new Vector2Int(6, 5)), W, out _);
            int whole = Arrivals(BuildStartingBoard(true), W, out _);

            sb.AppendLine();
            sb.AppendLine("  빈 칸 자리 비교 — 60초 도착 개수");
            sb.AppendLine($"  (가) 군수 한 대를 비운다(현행) | 채우기 전 {nodeGap} | 채운 뒤 {whole}");
            sb.AppendLine($"  (나) 운반로 벨트 한 칸을 비운다 | 채우기 전 {beltGap} | 채운 뒤 {whole}");
            sb.AppendLine("  주의: 「놓기 전 0」이 서는 쪽이 튜토리얼 종료 조건과 맞는다 — 판정은 설계.");
        }

        /// <summary><c>MountDeliveryTests.BuildStartingBoard</c> 와 같은 구성.</summary>
        private static BoardGrid BuildStartingBoard(bool fillEmptySlot)
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f,
                Vector2.zero, PartLayout.BuildMask());

            foreach (StartingBoard.Slot slot in StartingBoard.Nodes)
                g.TryPlace(slot.cell, Node(slot.nodeId), out _);

            // ⚠️ **병합기는 병합기로 놓는다.** 종전에는 이 줄이 `merger` 를 통째로 무시해
            // 합류 칸이 직선 벨트가 됐다 — 하네스가 게임과 다른 보드를 재고 있었다(2026-09-11).
            foreach (StartingBoard.Run run in StartingBoard.Belts)
                PlaceRun(g, run);

            if (fillEmptySlot)
                g.TryPlace(StartingBoard.FillsEmptySlot.cell,
                    Node(StartingBoard.FillsEmptySlot.nodeId), out _);

            // ⚠️ **이 둘이 빠지면 아무것도 안 흐른다.** 놓는 것과 이어지는 것은 다른 단계다.
            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        // ---- 6. 시작 보드 진단 — 어디서 끊겼는가 (2026-09-11 · `260911_W01` 2-5) ----

        /// <summary>
        /// 도착이 0 일 때 **어디서 끊겼는지**를 낸다.
        ///
        /// 「0 이다」만으로는 배치가 틀린 것인지 규격이 막는 것인지 못 가른다 —
        /// 실제로 네 줄 첫 배치에서 그 자리에 섰다. 여기서 내는 것은 셋이다:
        /// **노드마다 라인에 들었는가 · 링크가 몇 개인가 · 전력이 서는가.**
        /// </summary>
        private static void Six(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("[6] 시작 보드 진단 — 빈 칸을 채운 판");

            BoardGrid grid = BuildStartingBoard(true);
            var connected = new HashSet<Vector2Int>(LogisticsReach.ConnectedNodes(grid));
            List<BeltLink> links = BeltRouting.BuildLinks(grid);

            sb.AppendLine($"  링크 {links.Count}개 · 라인에 든 노드 {connected.Count}개");
            sb.AppendLine("  칸 | 노드 | 라인에 드는가");

            for (int y = grid.Rows - 1; y >= 0; y--)
            for (int x = 0; x < grid.Columns; x++)
            {
                var cell = new Vector2Int(x, y);
                NodeInstance node = grid.GetAt(cell);
                if (node?.Definition == null) continue;
                sb.AppendLine($"  ({x},{y}) | {node.Definition.displayName} | " +
                              (connected.Contains(cell) ? "예" : "아니오"));
            }

            WorkloadRate.Result work = WorkloadRate.Compute(grid, connected, null);
            NetworkAggregate agg = LogisticsNetwork.Aggregate(grid, connected, work);
            sb.AppendLine($"  전력 공급 {agg.powerSupply:F1} · 소비 {agg.powerDraw:F1} " +
                          $"· 코어 있음 {agg.hasCore} · 탄약 생산 {agg.ammoProduce:F2}/초");
            sb.AppendLine($"  탄약 경로 수 {LogisticsReach.AmmoPathCount(grid)} " +
                          $"(한 줄 처리량 {BeltLaneThroughput:F0}/초 — 넘치면 벨트가 먼저 막힌다)");
            sb.AppendLine("  주의: 전력 공급이 0이면 화면에서는 아무것도 안 만들어진다 — " +
                          "하네스는 배율 1로 돌려 운송만 잰다.");

            // 60초 돌린 뒤 **어느 칸이 막혀 있는지** — W01 2-5 「벨트가 먼저 막히는지」의 답이다.
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);
            for (int i = 0; i < 1200; i++)
            {
                BoardItemTick.Step(grid, flow, 0.05f, 1f);
                flow.ClearPendingMountArrivals();
            }
            sb.AppendLine("  60초 뒤 칸 상태 — 개수 / 막힘");
            for (int y = grid.Rows - 1; y >= 0; y--)
            for (int x = 0; x < grid.Columns; x++)
            {
                var c = new Vector2Int(x, y);
                if (grid.GetBeltAt(c) == null) continue;
                int n = flow.ItemsAt(c).Count;
                if (n == 0 && !flow.IsBlocked(c)) continue;
                sb.AppendLine($"  ({x},{y}) | {n}개 | {(flow.IsBlocked(c) ? "막힘" : "-")}");
            }
            sb.AppendLine($"  이론 한 줄 처리량 = {BeltItemFlow.CellsPerSecondTbd:F0}칸/초 ÷ " +
                          $"{BeltItemFlow.MinGapCells:F2}칸 = {BeltItemFlow.CellsPerSecondTbd / BeltItemFlow.MinGapCells:F0}/초 " +
                          $"(한 칸 최대 {BeltItemFlow.MaxPerCell}개)");

            // 벨트 면은 `BeltAutoOrient` 가 이웃을 보고 다시 잡는다 — 적어 둔 면과 다를 수 있다.
            sb.AppendLine("  벨트 | 입력면 | 출력면 | 나르는 것");
            for (int y = grid.Rows - 1; y >= 0; y--)
            for (int x = 0; x < grid.Columns; x++)
            {
                BeltInstance b = grid.GetBeltAt(new Vector2Int(x, y));
                if (b == null) continue;
                sb.AppendLine($"  ({x},{y}) | {b.InFace} | {b.OutFace} | {b.Kind}");
            }
        }

        /// <summary>운반로 벨트 한 칸을 빼고 지은 판 — 「빈 칸 = 벨트」 안을 재려는 것이다.</summary>
        private static BoardGrid BuildStartingBoardMissingBelt(Vector2Int omit)
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f,
                Vector2.zero, PartLayout.BuildMask());

            foreach (StartingBoard.Slot slot in StartingBoard.Nodes)
                g.TryPlace(slot.cell, Node(slot.nodeId), out _);
            g.TryPlace(StartingBoard.FillsEmptySlot.cell,
                Node(StartingBoard.FillsEmptySlot.nodeId), out _);   // 군수는 다 놓는다

            foreach (StartingBoard.Run run in StartingBoard.Belts)
            {
                if (run.cell == omit) continue;
                PlaceRun(g, run);
            }

            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        /// <summary>벨트 하나를 놓는다. **병합기는 병합기로** 놓는다(받는 면은 규칙이 준다).</summary>
        private static void PlaceRun(BoardGrid g, StartingBoard.Run run)
        {
            if (run.merger)
                g.TryPlaceBeltElement(run.cell, BeltElementKind.Merger,
                    StartingBoard.MergerInFaces(run.outFace), new[] { run.outFace },
                    FlowKind.None, out _);
            else
                g.TryPlaceBelt(run.cell, run.inFace, run.outFace, FlowKind.None, out _);
        }

        /// <summary>보드를 돌려 마운트에 닿은 개수를 센다. 첫 도착 시각도 함께 낸다.</summary>
        private static int Arrivals(BoardGrid grid, float seconds, out float firstAt)
        {
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            const float dt = 0.05f;
            int count = 0;
            firstAt = 0f;

            int steps = Mathf.RoundToInt(seconds / dt);
            for (int i = 0; i < steps; i++)
            {
                BoardItemTick.Step(grid, flow, dt, 1f);
                int n = flow.PendingMountArrivals.Count;
                if (n > 0 && count == 0) firstAt = (i + 1) * dt;
                count += n;
                flow.ClearPendingMountArrivals();
            }
            return count;
        }

        // ---- 도구 ----

        private static float Max(Chain c) => c.lineSpec * c.damage;

        private static void Combo(StringBuilder sb, string label, int p, int s, int e,
            Chain std, Chain pierce, Chain expl)
        {
            float output = p * pierce.damage + s * std.damage + e * expl.damage;
            int nodes = p * pierce.nodesPerShot.Length
                      + s * std.nodesPerShot.Length
                      + e * expl.nodesPerShot.Length;
            sb.AppendLine($"    {label} | {output:F0} | {nodes}");
        }

        /// <summary>체인 1발/초분의 전력 소비 합. 노드 자산에서 읽는다.</summary>
        private static float ChainDraw(Chain c)
        {
            float sum = 0f;
            foreach (string id in c.nodesPerShot)
            {
                NodeDefinition n = Node(id);
                if (n != null) sum += n.resources.powerDraw;
            }
            return sum;
        }

        private static float NetSupply(NodeDefinition ener) =>
            ener.resources.powerSupply - ener.resources.powerDraw;

        private static string EnergyLine(float draw)
        {
            NodeDefinition ener = Node("ener");
            if (ener == null) return "에너지 노드 자산 없음";

            float net = NetSupply(ener);
            if (net <= 0f) return "에너지 노드가 자기 소비를 못 넘는다 — 몇 대를 놓아도 안 된다";

            int count = Mathf.CeilToInt(draw / net);
            return $"에너지 {count}대 (소비 {draw:F0} / 순 공급 {net:F0})";
        }

        private static NodeDefinition Node(string id) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodeRoot}/Node_{id}.asset");
    }
}
