using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using UnityEngine;

namespace MBI.Logistics
{
    /// <summary>
    /// 라이브 물류 네트워크 → 출력 반영(§5-6, L4-R). 배치 노드 집계(LogisticsNetwork) → 흐름시뮬
    /// (LogisticsSimulation: expected/actual/gap) → LogisticsOutputBridge(Output=actual·Expected·Gap·GlobalCause).
    /// 노드별 진단(LogisticsDiagnostics) → BoardController 상태색. actual은 60초 롤링(움직이는 거울).
    ///
    /// ⚠️ 연결성/체인 미강제(합계 기반) — 정밀 흐름/노드 cause는 근사(R1). 병목 수치 전부 TBD(LogisticsConfig).
    /// </summary>
    public sealed class LogisticsOutputProvider : MonoBehaviour
    {
        [Tooltip("물류 보드. 씬 생성기가 주입.")]
        public BoardController board;
        [Tooltip("병목 파라미터(TBD). 없으면 기본값.")]
        public LogisticsConfig config;
        [Tooltip("로봇 명목 출력(전 공급 시). 대표 145.")]
        public float baseOutput = 145f;
        [Tooltip("원점 출력(요구치 분모). balance origin = 100.")]
        public float origin = 100f;
        [Tooltip("물류 상한 배율. balance ceil = 1.6. 명목 배율이 이를 넘으면 over-build 경고(클램프 아님).")]
        public float ceilMult = 1.6f;
        [Tooltip("마운트 탄약 수요(발/초). capA = 6.")]
        public float ammoDemand = 6f;
        [Tooltip("실측(actual) 롤링 창(초). '움직이는 거울' — 배치 변화가 이 시간에 걸쳐 반영됨.")]
        public float rollingWindow = 60f;

        private readonly Queue<float> _sampleTimes = new Queue<float>();
        private readonly Queue<float> _sampleValues = new Queue<float>();
        private float _rollingSum;
        private bool _wasOverCeiling;

        private void Update()
        {
            if (board == null) return;
            BoardGrid grid = board.Grid;
            if (grid == null) return;

            NetworkAggregate agg = LogisticsNetwork.Aggregate(grid);
            LogisticsOutputBridge.AmmoProduce = agg.ammoProduce; // 전투 HUD 저장고/탄약 표시(§C-2)

            if (!agg.hasCore)
            {
                LogisticsOutputBridge.Output = 0f;   // 물류 허브(코어) 없음 → 전투로 나가는 출력 없음
                LogisticsOutputBridge.Expected = 0f;
                LogisticsOutputBridge.Gap = 0f;
                LogisticsOutputBridge.GlobalCause = ConstraintCause.None;
                board.ClearDiagnostics();
                ResetRolling();
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
                origin, ceilMult);

            // over-build 경고(전이 시 1회): 물류 단독 명목 배율이 물리 상한 초과 = 설계 오류 신호(클램프 삭제 §L4-R #2).
            if (r.overCeiling && !_wasOverCeiling)
                Debug.LogWarning($"[MBI] 물류 명목 배율 {r.multiple:F2} > 천장 {ceilMult:F1} — over-build(물리 상한 초과). balance/보드 물리 상한 확인.");
            _wasOverCeiling = r.overCeiling;

            float rolled = Rolling(r.actual);
            LogisticsOutputBridge.Output = rolled;                          // 실측(60초 롤링)
            LogisticsOutputBridge.Expected = r.expected;                    // 예상(명목, 즉시)
            LogisticsOutputBridge.Gap = Mathf.Max(0f, r.expected - rolled); // 갭
            LogisticsOutputBridge.GlobalCause = GlobalCause(r);

            board.ApplyDiagnostics(LogisticsDiagnostics.Evaluate(grid, r)); // 노드 상태색
        }

        /// <summary>전역 원인(변수패널 아이콘·점멸): Power → Heat 우선(§3-4-1). 벨트는 아이콘 아님(gapBelt 담당).</summary>
        private static ConstraintCause GlobalCause(LogisticsResult r)
        {
            if (r.powerEfficiency < 1f) return ConstraintCause.Power;
            if (r.heatThrottle < 1f) return ConstraintCause.Heat;
            return ConstraintCause.None;
        }

        /// <summary>actual의 롤링 평균(움직이는 거울) — 시간창 rollingWindow 내 샘플 평균.</summary>
        private float Rolling(float actual)
        {
            float now = Time.time;
            _sampleTimes.Enqueue(now);
            _sampleValues.Enqueue(actual);
            _rollingSum += actual;
            while (_sampleTimes.Count > 0 && now - _sampleTimes.Peek() > rollingWindow)
            {
                _sampleTimes.Dequeue();
                _rollingSum -= _sampleValues.Dequeue();
            }
            return _sampleValues.Count > 0 ? _rollingSum / _sampleValues.Count : actual;
        }

        private void ResetRolling()
        {
            _sampleTimes.Clear();
            _sampleValues.Clear();
            _rollingSum = 0f;
        }
    }
}
