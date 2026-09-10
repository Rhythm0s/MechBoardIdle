using System.Text;
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
