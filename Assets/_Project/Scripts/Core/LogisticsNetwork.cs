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
        public float ammoProduce;  // Σ 탄약 생산(군수) — 벨트 운송 필요량 proxy

        // 탄종별 군수 노드 수. **출력은 이 셋에서 나온다**(§1: 라인 생산량 = min(스펙, 노드 수)).
        // 합계 ammoProduce만으로는 「무엇을 몇 발 만드는가」가 표현되지 않는다.
        public int muniPierce;
        public int muniSplit;
        public int muniExplosive;

        // 군수 노드가 탄약 말고 다른 조합표를 돌리면 산출이 이쪽으로 간다(2026-08-27 레시피 선택형).
        public float droneProduce;      // Σ 드론 몸체(기/초) — 사출대 유입
        public float propellantProduce; // Σ 추진제(개/초) — 부스터가 받아 회피 스택으로 바꾼다

        /// <summary>부스터 대수. **회피 스택 상한 = 이 값 × 2**(260829_V02) — 상수가 아니다.</summary>
        public int boosterCount;

        public int MuniCountOf(AmmoKind kind)
        {
            switch (kind)
            {
                case AmmoKind.Pierce: return muniPierce;
                case AmmoKind.Split: return muniSplit;
                default: return muniExplosive;
            }
        }
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
                if (node.Definition.type == NodeType.Booster) a.boosterCount++;
                a.nodeCount++;

                NodeResourceProfile r = node.Definition.resources;
                a.powerSupply += r.powerSupply;
                a.powerDraw += r.powerDraw;
                a.heatGenerate += r.heatGenerate;
                a.heatDissipate += r.heatDissipate;

                if (node.Definition.type != NodeType.Munitions)
                {
                    a.ammoProduce += r.ammoProduce;
                }
                else
                {
                    // 군수 노드는 조합표 **하나**를 돌린다. 무엇을 돌리는지에 따라 산출이 갈린다 —
                    // 조합표를 안 보고 전부 탄약으로 세던 것이 2026-08-27 이전 모델이었다.
                    NodeRecipe recipe = node.CurrentRecipe;
                    switch (recipe.kind)
                    {
                        case RecipeKind.DroneBody:
                            a.droneProduce += recipe.outputPerSec;
                            break;
                        case RecipeKind.Propellant:
                            a.propellantProduce += recipe.outputPerSec;
                            break;
                        default:
                            // 탄약(미선택 폴백 포함). 라인 생산량은 **노드 수**로 계산되므로
                            // 탄종별로도 센다 — min(스펙, 노드 수)가 그 식이다.
                            // ⚠️ ammoProduce를 조합표 밖에서 더하면 추진제를 만드는 노드가
                            // 탄약도 만드는 것으로 집계된다. 노드 하나는 조합표 하나다.
                            a.ammoProduce += r.ammoProduce;
                            switch (node.AmmoKind)
                            {
                                case AmmoKind.Pierce: a.muniPierce++; break;
                                case AmmoKind.Split: a.muniSplit++; break;
                                default: a.muniExplosive++; break;
                            }
                            break;
                    }
                }
            }
            return a;
        }
    }
}
