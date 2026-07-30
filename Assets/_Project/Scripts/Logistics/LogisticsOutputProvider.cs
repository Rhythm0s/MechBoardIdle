using MBI.Core;
using MBI.Data;
using UnityEngine;

namespace MBI.Logistics
{
    /// <summary>
    /// 라이브 물류 네트워크 → 출력 반영(§5-6). 배치된 노드를 집계(LogisticsNetwork) → 흐름시뮬
    /// (LogisticsSimulation, 전력효율·발열·벨트·천장) → LogisticsOutputBridge.Output에 기록.
    /// 전투 HUD(StageRunner)가 그 값을 읽어 표시 → 물류 배치가 전투 출력에 실시간 반영.
    ///
    /// MVP 모델: 코어(허브) 없으면 출력 0. 있으면 baseOutput(로봇 명목 145) × 탄약공급율 × 병목 배율.
    /// ⚠️ 연결성/체인 미강제(합계 기반) — 정밀 흐름은 향후. 수치 전부 TBD(LogisticsConfig).
    /// </summary>
    public sealed class LogisticsOutputProvider : MonoBehaviour
    {
        [Tooltip("물류 보드. 씬 생성기가 주입.")]
        public BoardController board;
        [Tooltip("병목 파라미터(TBD). 없으면 기본값.")]
        public LogisticsConfig config;
        [Tooltip("로봇 명목 출력(전 공급 시). 대표 145.")]
        public float baseOutput = 145f;
        [Tooltip("물류 천장 = origin×ceil.")]
        public float ceiling = 160f;
        [Tooltip("마운트 탄약 수요(발/초). capA = 6.")]
        public float ammoDemand = 6f;

        private void Update()
        {
            if (board == null) return;
            BoardGrid grid = board.Grid;
            if (grid == null) return;

            NetworkAggregate agg = LogisticsNetwork.Aggregate(grid);
            if (!agg.hasCore)
            {
                LogisticsOutputBridge.Output = 0f; // 물류 허브(코어) 없음 → 전투로 나가는 출력 없음
                return;
            }

            float ammoFactor = ammoDemand > 0f ? Mathf.Clamp01(agg.ammoProduce / ammoDemand) : 0f;
            float baseEff = baseOutput * ammoFactor;

            float heatThreshold = config != null ? config.heatThreshold : 12f;
            float beltCapacity = config != null ? config.beltCapacity : 14f;

            LogisticsResult r = LogisticsSimulation.Compute(
                baseEff,
                agg.powerSupply, agg.powerDraw,
                agg.heatGenerate, agg.heatDissipate, heatThreshold,
                beltCapacity, agg.ammoProduce, // 운송 필요 proxy = 탄약 생산량
                ceiling);

            LogisticsOutputBridge.Output = r.output;
        }
    }
}
