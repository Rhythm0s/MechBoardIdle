using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>배치된 노드 네트워크의 집계(자원 합계 + 코어 존재). LogisticsSimulation 입력.</summary>
    public struct NetworkAggregate
    {
        public bool hasCore;       // 물류 허브(코어) 존재 — 없으면 전투로 나가는 출력 없음
        public int nodeCount;
        public float powerSupply;  // Σ 발전(에너지)
        public float powerDraw;    // Σ 고정비(전 노드)
        public float heatGenerate; // Σ 발열(가공)
        public float heatDissipate;
        public float ammoProduce;  // Σ 탄약 생산(군수)
    }

    /// <summary>
    /// 보드에 배치된 노드를 집계(§5-5, 순수·결정론). 각 셀의 NodeResourceProfile을 합산.
    /// 연결성(벨트로 이어졌는지)은 이 MVP 집계에서 강제하지 않음 — 합계 기반(향후 연결 그래프로 정밀화).
    /// 스텁 노드(implemented=false)는 제외.
    /// </summary>
    public static class LogisticsNetwork
    {
        public static NetworkAggregate Aggregate(BoardGrid grid)
        {
            var a = new NetworkAggregate();
            if (grid == null) return a;

            for (int x = 0; x < grid.Columns; x++)
            for (int y = 0; y < grid.Rows; y++)
            {
                NodeInstance node = grid.GetAt(new Vector2Int(x, y));
                if (node == null || node.Definition == null || !node.Definition.implemented) continue;

                if (node.Definition.type == NodeType.Core) a.hasCore = true;
                a.nodeCount++;

                NodeResourceProfile r = node.Definition.resources;
                a.powerSupply += r.powerSupply;
                a.powerDraw += r.powerDraw;
                a.heatGenerate += r.heatGenerate;
                a.heatDissipate += r.heatDissipate;
                a.ammoProduce += r.ammoProduce;
            }
            return a;
        }
    }
}
