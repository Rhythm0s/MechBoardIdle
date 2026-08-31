using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 일감률 — **그 노드가 지금 실제로 일하고 있는 비율**(260831_V07 승인분).
    ///
    /// 기본식은 `일감률 = 연결성(0/1) × 라인 가동률`이고, 노드 종류마다 예외가 붙는다.
    /// 쓰이는 곳은 변동비 전력이다: 수요 = Σ(대당 전력 × 일감률).
    /// 놀고 있는 노드가 일하는 노드와 같은 전력을 먹으면 「덜어내라」가 해답이 되지 않는다.
    ///
    /// **표시 규칙**: 스펙을 넘긴 노드는 노는 것으로 **몰아서** 0을 주고, 보드 총합은 평균으로 낸다
    /// (승인 원문). 초과분에 0.6씩 골고루 나눠 주면 화면상 어느 노드를 빼야 할지가 사라진다.
    ///
    /// ⚠️ **코어는 대상이 아니다.** 대당 전력이 0이고 「가동률」이라는 축 자체가 없다 —
    /// 평균에도 넣지 않는다. 넣으면 코어 하나가 늘 1이라 평균이 위로 뜬다.
    /// </summary>
    public static class WorkloadRate
    {
        /// <summary>한 보드의 일감률.</summary>
        public struct Result
        {
            /// <summary>노드별 일감률(코어 제외). 화면이 셀 단위로 읽는다.</summary>
            public Dictionary<Vector2Int, float> perNode;

            /// <summary>보드 총합 = 코어를 뺀 노드들의 **평균**. 노드가 없으면 0.</summary>
            public float average;

            public float Of(Vector2Int cell) =>
                perNode != null && perNode.TryGetValue(cell, out float v) ? v : 1f;
        }

        /// <summary>
        /// 일감률 계산.
        /// </summary>
        /// <param name="connected">이어진 노드 집합. null이면 연결성 판정을 건너뛴다(전부 이어진 것으로 본다).</param>
        /// <param name="boosterPropellantFull">
        /// 부스터가 받는 추진제 스택이 찼는가. ⚠️ **지금은 늘 false로 들어온다** —
        /// 스택은 <c>CombatSimulation.RobotSide</c>가 들고 있고 물류 집계는 보드만 보기 때문이다.
        /// 승인 원문의 「읽을 수 없으면 잠정 1 + 주장 대조 대장 등재」가 이 자리다(260831_V08 판정 요청 3).
        /// </param>
        public static Result Compute(BoardGrid grid, ICollection<Vector2Int> connected,
            BalanceConfig balance, bool boosterPropellantFull = false)
        {
            var result = new Result { perNode = new Dictionary<Vector2Int, float>() };
            if (grid == null) return result;

            float perNodeRate = balance != null ? balance.muniPerNode : 1f;

            // 탄종별로 몇 번째 노드인지 세어 스펙을 넘긴 순번을 노는 것으로 민다.
            var seen = new Dictionary<AmmoKind, int>();

            float sum = 0f;
            int counted = 0;

            // 순회 순서는 LogisticsNetwork.Aggregate와 같다 — 「몇 번째 노드가 초과분인가」가
            // 집계마다 달라지면 같은 보드에서 화면이 흔들린다.
            for (int x = 0; x < grid.Columns; x++)
            for (int y = 0; y < grid.Rows; y++)
            {
                var cell = new Vector2Int(x, y);
                NodeInstance node = grid.GetAt(cell);
                if (node == null || node.Definition == null || !node.Definition.implemented) continue;

                NodeType type = node.Definition.type;
                if (type == NodeType.Core) continue; // 코어 무관

                bool linked = connected == null || connected.Contains(cell);
                float rate = RateOf(node, type, linked, balance, perNodeRate, boosterPropellantFull, seen);

                result.perNode[cell] = rate;
                sum += rate;
                counted++;
            }

            result.average = counted > 0 ? sum / counted : 0f;
            return result;
        }

        private static float RateOf(NodeInstance node, NodeType type, bool linked,
            BalanceConfig balance, float perNodeRate, bool boosterPropellantFull,
            Dictionary<AmmoKind, int> seen)
        {
            switch (type)
            {
                // 에너지는 **늘 1**이다. 발전은 무엇이 이어져 있든 돌아가고, 오히려 아무것도
                // 안 이어졌을 때 놀고 있다고 잡으면 전력이 0이 되어 보드 전체가 멈춘다.
                case NodeType.Energy:
                    return 1f;

                // 가공·저장은 **연결성만** 본다. 라인 가동률에 해당하는 축(탄종 스펙)이 없다.
                case NodeType.Processing:
                case NodeType.Storage:
                    return linked ? 1f : 0f;

                // 부스터는 추진제 스택이 차면 놀아야 한다 — 지금은 그 값을 못 읽어 잠정 1이다.
                case NodeType.Booster:
                    if (!linked) return 0f;
                    return boosterPropellantFull ? 0f : 1f;

                case NodeType.Munitions:
                    return linked ? MunitionsRate(node, balance, perNodeRate, seen) : 0f;

                default:
                    return linked ? 1f : 0f;
            }
        }

        /// <summary>
        /// 군수 = 연결성 × 라인 가동률. 라인 가동률은 <see cref="AmmoLineProduction"/>이 이미 쥐고 있으므로
        /// 여기서 다시 만들지 않는다 — 두 곳이 다른 가동률을 말하면 화면과 생산량이 갈린다.
        ///
        /// 스펙(관통 5 / 분열 4 / 폭발 2)까지가 1, 그 뒤 순번은 0이다.
        /// </summary>
        private static float MunitionsRate(NodeInstance node, BalanceConfig balance, float perNodeRate,
            Dictionary<AmmoKind, int> seen)
        {
            NodeRecipe recipe = node.CurrentRecipe;

            // ⚠️ 드론 몸체·추진제는 **상한이 확정되지 않았다.** 탄종 스펙에 해당하는 값이 원천에
            // 없으므로 발명하지 않고 1로 둔다(주장 대조 대장 등재 대상).
            if (recipe.kind == RecipeKind.DroneBody || recipe.kind == RecipeKind.Propellant) return 1f;

            AmmoKind kind = node.AmmoKind;
            float spec = balance != null ? balance.LineSpecOf(kind) : 0f;
            if (spec <= 0f || perNodeRate <= 0f) return 0f;

            seen.TryGetValue(kind, out int index);
            seen[kind] = index + 1;

            // 이 탄종에서 몇 번째인가. 스펙을 채우는 데 필요한 수까지가 일하는 노드다.
            return index < AmmoLineProduction.NodesForFullLine(spec, perNodeRate) ? 1f : 0f;
        }
    }
}
