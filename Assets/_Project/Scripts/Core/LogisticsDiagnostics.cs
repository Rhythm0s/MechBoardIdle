using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 노드 산출 제약 원인(§L4-R #6). UI 아이콘은 **전역 원인 Power/Heat만** 표시하고(UI 문서 13 §3-4-1),
    /// NoInput/Blocked는 enum에 유지하되 UI 미매핑 — 진단·로그(P-1 지표) 전용.
    /// 벨트 병목은 독립 원인이 아니다: 노드 관점에서 상류=NoInput / 하류=Blocked로 관측되며,
    /// 벨트 자체 기여는 변수 패널의 gapBelt가 담당한다(한 개념=한 책임).
    /// </summary>
    public enum ConstraintCause { None, NoInput, Blocked, Power, Heat }

    /// <summary>노드 1기의 진단 스냅샷. 색 = actualRate/targetRate, 아이콘(전역) = cause(Power/Heat).</summary>
    public struct NodeDiagnostic
    {
        public Vector2Int cell;
        public NodeType type;
        public float targetRate;   // 노드 명목 처리율(정상 = 1)
        public float actualRate;   // 적용 병목 후(구조적 정지 = 0)
        public ConstraintCause cause;
    }

    /// <summary>
    /// 노드별 진단 산출(§L4-R #6, 순수·결정론). 전역 시뮬 결과 + 보드 인접 연결로 노드 상태를 근사.
    ///
    /// ⚠️ R1: LogisticsNetwork가 연결성 미강제(합계 기반)이므로 노드별 actualRate/cause는 **근사**다
    ///   (전역 throttle 균등 적용 + 인접 셀 존재 기반 링크 판정). 정밀 노드별 흐름은 연결 그래프
    ///   정밀화가 필요 — 그전까지 "검증 완료" 표기 금지(§7).
    /// </summary>
    public static class LogisticsDiagnostics
    {
        public static List<NodeDiagnostic> Evaluate(BoardGrid grid, LogisticsResult result)
        {
            var list = new List<NodeDiagnostic>();
            if (grid == null) return list;

            // 전역 산출률(actual/expected) = 병목 배율 곱. 노드 색(감속 = 노랑)의 기준.
            float globalRatio = result.expected > 0f ? Mathf.Clamp01(result.actual / result.expected) : 1f;

            for (int x = 0; x < grid.Columns; x++)
            for (int y = 0; y < grid.Rows; y++)
            {
                var cell = new Vector2Int(x, y);
                NodeInstance node = grid.GetAt(cell);
                if (node == null || node.Definition == null || !node.Definition.implemented) continue;

                bool hasOut = false, hasIn = false, outLinked = false, inLinked = false;
                foreach (NodePort p in node.Definition.ports)
                {
                    bool neighbor = HasNeighbor(grid, cell, p.face);
                    if (p.io == PortIO.Output) { hasOut = true; if (neighbor) outLinked = true; }
                    else { hasIn = true; if (neighbor) inLinked = true; }
                }

                // 우선순위: 구조(Blocked/NoInput) → 전역(Power → Heat). 전역 힌트는 Power→Heat 2단계(§3-4-1).
                ConstraintCause cause;
                if (hasOut && !outLinked) cause = ConstraintCause.Blocked;          // 뒤로 안 빠짐
                else if (hasIn && !inLinked) cause = ConstraintCause.NoInput;        // 앞에서 안 옴
                else if (result.powerEfficiency < 1f) cause = ConstraintCause.Power; // 전역 전력
                else if (result.heatThrottle < 1f) cause = ConstraintCause.Heat;     // 전역 발열
                else cause = ConstraintCause.None;

                bool stopped = cause == ConstraintCause.Blocked || cause == ConstraintCause.NoInput;

                list.Add(new NodeDiagnostic
                {
                    cell = cell,
                    type = node.Definition.type,
                    targetRate = 1f,
                    actualRate = stopped ? 0f : globalRatio,   // 구조적 정지 = 0(빨강), 그 외 = 전역 산출률
                    cause = cause,
                });
            }
            return list;
        }

        /// <summary>셀의 해당 면 인접 칸에 노드나 벨트가 있는가(연결 근사 — R1).</summary>
        private static bool HasNeighbor(BoardGrid grid, Vector2Int cell, PortFace face)
        {
            Vector2Int nb = cell + BeltRouting.Delta(face);
            if (!grid.IsInside(nb)) return false;
            return grid.IsOccupied(nb) || grid.HasBelt(nb);
        }
    }
}
