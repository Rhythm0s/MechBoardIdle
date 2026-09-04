using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 보드 한 틱 — 생산 → 출력 버퍼 → 벨트 (2026-09-03 신설 · `260903_W01` 4-3).
    ///
    /// **문서와 구현 불일치 3번을 푸는 자리다.** <see cref="NodeInstance.OutputBuffer"/>와
    /// <see cref="NodeProduction"/>의 네 함수는 이미 있었으나 **부르는 곳이 0건**이었다.
    /// 조립 시스템 문서「재고가 놓이는 세 층」의 첫 층이 타입으로만 존재하던 상태이며,
    /// 지침 §7 「실패하지 않는 결함」의 사례 그대로다 — 테스트도 빌드도 통과하니 신호가 없었다.
    ///
    /// 여기서 상류 전파가 성립한다. 벨트가 차면 버퍼가 안 비고, 버퍼가 차면
    /// <see cref="NodeProduction.Produce"/>가 0을 돌려주어 그 노드가 멈춘다.
    /// 막힘이 하류에서 상류로 거슬러 오르는 것이 아니라, **각자 자기 앞이 막혔는지만 보면**
    /// 결과적으로 거슬러 오른다.
    ///
    /// ⚠️ **계산 경로를 대체하지 않는다.** <see cref="WorkloadRate"/>는 아직 연결성 기반이고
    /// 지침 §3이 말하는 「버퍼 상태로만 정해진다」로 바꾸는 것은 출력 교체(6번 덩어리)와 한 쌍이다.
    /// 지금은 옆에서 돌기만 한다.
    /// </summary>
    public static class BoardItemTick
    {
        /// <summary>아이템 하나로 세는 단위. 낱개로 나간다.</summary>
        private const float OneItem = 1f;

        public static void Step(BoardGrid grid, BeltItemFlow flow, float dt)
        {
            if (grid == null || flow == null || dt <= 0f) return;

            // 벨트를 먼저 민다. 앞이 비워야 이번 틱에 뒤가 들어갈 자리가 생긴다 —
            // 나중에 밀면 생산분이 한 틱씩 늦게 출발한다.
            flow.Tick(dt);

            // 도착한 재료를 노드 입력 버퍼로 옮긴다. **생산보다 먼저** — 나중에 옮기면
            // 이번 틱에 닿은 재료가 한 틱 늦게 쓰이고 그만큼 라인 전체가 밀린다.
            DrainArrivals(grid, flow);

            for (int x = 0; x < grid.Columns; x++)
            for (int y = 0; y < grid.Rows; y++)
            {
                var cell = new Vector2Int(x, y);
                NodeInstance node = grid.GetAt(cell);
                if (node?.Definition == null || !node.Definition.implemented) continue;

                Produce(node);
                PushToBelt(flow, node, cell);
            }

            void Produce(NodeInstance node)
            {
                NodeRecipe recipe = node.CurrentRecipe;
                if (!recipe.IsRunnable) return;

                // 버퍼가 가득하거나 재료가 없으면 0을 돌려준다 — 둘 다 정지다.
                // 여기서 사유를 가리지 않는다. 가리는 것은 진단의 몫이다.
                float made = NodeProduction.Produce(recipe, node.OutputBuffer, dt, node.InputBuffer);
                if (made <= 0f) return;

                // 만든 만큼만 먹는다. 순서를 바꾸면 버릴 산출의 재료까지 먼저 사라진다.
                NodeProduction.ConsumeFor(recipe, made, node.InputBuffer);

                node.OutputBuffer += made;
                node.BufferKind = recipe.output;
            }
        }

        /// <summary>
        /// 소비처에 닿은 물건을 그 노드의 입력 버퍼로 옮긴다 (2026-09-04 · `260904_W01` 3장).
        ///
        /// <see cref="BeltItemFlow"/>는 도착을 세기만 하고 노드를 모른다 — 순수를 지키기
        /// 위해서다. 둘을 잇는 것이 이 자리다.
        /// </summary>
        private static void DrainArrivals(BoardGrid grid, BeltItemFlow flow)
        {
            System.Collections.Generic.IReadOnlyList<Arrival> pending = flow.PendingArrivals;
            for (int i = 0; i < pending.Count; i++)
            {
                NodeInstance node = grid.GetAt(pending[i].cell);
                node?.TakeInput(pending[i].kind, OneItem);
            }
            flow.ClearPendingArrivals();
        }

        /// <summary>
        /// 출력 버퍼를 벨트로 낱개씩 밀어 넣는다. **못 넣으면 그대로 남긴다** —
        /// 남은 것이 다음 틱의 생산을 막고, 그것이 상류 전파다.
        /// </summary>
        private static void PushToBelt(BeltItemFlow flow, NodeInstance node, Vector2Int cell)
        {
            if (!flow.TryNextOf(cell, out Vector2Int to)) return;

            while (node.OutputBuffer >= OneItem)
            {
                // 먼저 자리를 잡고 그 다음에 버퍼를 던다. 순서를 바꾸면 벨트가 찼을 때
                // 물건이 버퍼에서 빠진 채 어디에도 없는 상태가 된다.
                if (!flow.TryInsert(to, node.BufferKind)) return;

                float taken = NodeProduction.Withdraw(node.OutputBuffer, OneItem, out float after);
                if (taken <= 0f) return;
                node.OutputBuffer = after;
            }
        }
    }
}
