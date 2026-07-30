using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 격자에 설치된 벨트 1칸의 순수 논리 표현(§5-4).
    /// 방향 = 입력면(InFace)에서 들어와 출력면(OutFace)으로 나감.
    ///   - 직선 벨트: InFace/OutFace가 반대(예: West→East).
    ///   - 코너 벨트: 인접 면(예: West→North).
    /// Kind = 운송 자원 종류(물류 품목=Material 기본). 전력/탄약/발열 라인 구분에도 사용.
    ///
    /// GameObject/비주얼은 두지 않는다 — 씬 표현은 BoardController가 셀→마커 매핑으로 관리(Core 순수성).
    /// </summary>
    public sealed class BeltInstance
    {
        public readonly Vector2Int Cell;
        public PortFace InFace;
        public PortFace OutFace;
        public FlowKind Kind;

        public BeltInstance(Vector2Int cell, PortFace inFace, PortFace outFace, FlowKind kind)
        {
            Cell = cell;
            InFace = inFace;
            OutFace = outFace;
            Kind = kind;
        }

        /// <summary>직선 벨트(입출력이 반대 면)인가. 아니면 코너.</summary>
        public bool IsStraight => OutFace == NodeConnectionRules.Opposite(InFace);
    }
}
