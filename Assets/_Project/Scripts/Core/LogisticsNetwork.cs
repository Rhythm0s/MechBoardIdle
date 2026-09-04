using System.Collections.Generic;
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
        public float powerDraw;    // Σ 변동비(대당 전력 × 일감률) — 노는 노드는 0을 먹는다
        public float workloadAverage; // 보드 일감률 평균(코어 제외). 표시용
        public float heatGenerate; // Σ 발열(노드 대당 값의 합)
        // 냉각은 노드가 들지 않는다 — 모듈 F 소유(260829_V03). LogisticsConfig가 든다.
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

        /// <summary>탄약이 흐르는 **경로 수**. 총 대역 = 이 값 × 한 줄 처리량(벨트 등급).</summary>
        public int ammoPaths;

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
        /// <summary>
        /// 보드 집계. <paramref name="connectedOnly"/>를 주면 **거기 든 노드만** 센다
        /// (260829_V03 §판정① A안 — 코어까지 안 이어진 노드는 라인이 아니다).
        ///
        /// null이면 전부 센다. 격자만 놓고 계산을 보는 단위 테스트가 그 경로를 쓴다 —
        /// 게이트를 켠 실측은 <see cref="LogisticsReach.ConnectedNodes"/>를 넘겨 잰다.
        /// </summary>
        public static NetworkAggregate Aggregate(BoardGrid grid,
            ICollection<Vector2Int> connectedOnly = null,
            WorkloadRate.Result? workload = null)
        {
            var a = new NetworkAggregate();
            if (grid == null) return a;

            // 일감률을 안 주면 **전부 만가동**으로 본다 — 종전 동작이 그대로 남는다.
            WorkloadRate.Result work = workload ?? default;
            bool hasWork = workload.HasValue;
            a.workloadAverage = hasWork ? work.average : 1f;

            a.ammoPaths = LogisticsReach.AmmoPathCount(grid);

            for (int x = 0; x < grid.Columns; x++)
            for (int y = 0; y < grid.Rows; y++)
            {
                var cell = new Vector2Int(x, y);
                NodeInstance node = grid.GetAt(cell);
                if (node == null || node.Definition == null || !node.Definition.implemented) continue;

                // 안 이어진 노드는 라인이 아니다 — 놓여 있다고 세지 않는다.
                if (connectedOnly != null && !connectedOnly.Contains(cell)) continue;

                if (node.Definition.type == NodeType.Core) a.hasCore = true;
                if (node.Definition.type == NodeType.Booster) a.boosterCount++;
                a.nodeCount++;

                NodeResourceProfile r = node.Definition.resources;

                // **전력은 변동비다**(260830_V01 개정 · 260831_V07 일감률 승인).
                // 수요 = Σ(대당 전력 × 일감률). 노는 노드는 0을 먹는다 —
                // ⚠️ **대기 전력은 없다**(260901_V02 §2층 확정 · 새티스팩토리 방식).
                // 검토하던 「유휴 시 대당의 3~5%」 밴드는 **폐기됐다** — 승인 대기가 아니라 없는 것이다.
                // 놓아두기만 한 노드는 0을 먹는다.
                float w = hasWork ? work.Of(cell) : 1f;

                a.powerSupply += r.powerSupply;
                a.powerDraw += r.powerDraw * w;

                // ⚠️ 발열은 그대로 둔다 — 변동비로 바뀐 것은 전력뿐이고, 발열의 일감률 연동은
                // 승인 범위 밖이다. 노드 대당 발열은 영상 이후로 연기된 항목이기도 하다.
                a.heatGenerate += r.heatGenerate;

                if (node.Definition.type != NodeType.MunitionsBasic)
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
