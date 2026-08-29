using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 격자에 설치된 벨트 1칸의 순수 논리 표현(§5-4). 아이템은 입력면(들)에서 들어와 출력면(들)으로 나감.
    ///   - 직선(Straight): 1-in / 1-out, In/Out이 반대 면(예: West→East).
    ///   - 코너(Corner)  : 1-in / 1-out, 인접 면(예: West→North).
    ///   - 병합기(Merger): 다중-in / 1-out(여러 In면 → 한 Out면으로 합류, §5-4 L3).
    ///   - 분류기(Sorter): 1-in / 다중-out(한 In면 → 여러 Out면으로 분배, §5-4 L3).
    /// Kind = 운송 자원 종류(FlowKind; 물류 품목=Material 기본). 전력/탄약/발열 라인 구분에도 사용.
    ///
    /// 면은 InFaces/OutFaces 배열이 단일 소스 — 직선/코너는 원소 1개.
    /// 편의 접근자 InFace/OutFace는 각 배열의 첫 원소(1-in/1-out 요소용).
    /// GameObject/비주얼은 두지 않는다 — 씬 표현은 BoardController가 셀→마커 매핑으로 관리(Core 순수성).
    /// </summary>
    public sealed class BeltInstance
    {
        public readonly Vector2Int Cell;
        public readonly BeltElementKind Element;
        public FlowKind Kind;

        /// <summary>입력면. 병합기·분류기는 이웃에 따라 다시 잡힌다(<see cref="Reorient"/>).</summary>
        public PortFace[] InFaces { get; private set; }

        /// <summary>출력면. 병합기·분류기는 이웃에 따라 다시 잡힌다(<see cref="Reorient"/>).</summary>
        public PortFace[] OutFaces { get; private set; }

        /// <summary>
        /// 면을 다시 잡는다(병합기·분류기 전용).
        ///
        /// 왜 배치 시점에 고정하지 않는가: 요소를 **먼저 놓고 나중에 이웃을 붙이는** 순서가
        /// 자연스러운데, 그때 방향을 고정해 두면 조용히 안 이어진 채로 남는다.
        /// 벨트 품목(<c>BeltFlow</c>)과 같은 이유로 배치가 바뀔 때마다 다시 잡는다.
        ///
        /// 직선·코너는 드래그 경로가 방향을 정하므로 여기 오지 않는다.
        /// </summary>
        public void Reorient(PortFace[] inFaces, PortFace[] outFaces)
        {
            if (Element != BeltElementKind.Merger && Element != BeltElementKind.Sorter) return;
            if (inFaces != null && inFaces.Length > 0) InFaces = inFaces;
            if (outFaces != null && outFaces.Length > 0) OutFaces = outFaces;
        }

        /// <summary>단일 입력면(직선/코너/분류기). 다중 입력이면 첫 면.</summary>
        public PortFace InFace => InFaces[0];

        /// <summary>단일 출력면(직선/코너/병합기). 다중 출력이면 첫 면.</summary>
        public PortFace OutFace => OutFaces[0];

        /// <summary>직선/코너(1-in/1-out) 생성자. In/Out 반대면이면 직선, 아니면 코너로 판별.</summary>
        public BeltInstance(Vector2Int cell, PortFace inFace, PortFace outFace, FlowKind kind)
            : this(cell,
                   outFace == NodeConnectionRules.Opposite(inFace) ? BeltElementKind.Straight : BeltElementKind.Corner,
                   new[] { inFace }, new[] { outFace }, kind)
        {
        }

        /// <summary>일반 생성자(병합기/분류기 포함, 다중 면). 배열은 소유권 이전 — 호출자가 이후 변형하지 않는다.</summary>
        public BeltInstance(Vector2Int cell, BeltElementKind element, PortFace[] inFaces, PortFace[] outFaces, FlowKind kind)
        {
            Cell = cell;
            Element = element;
            InFaces = inFaces != null && inFaces.Length > 0 ? inFaces : new[] { PortFace.West };
            OutFaces = outFaces != null && outFaces.Length > 0 ? outFaces : new[] { PortFace.East };
            Kind = kind;
        }

        /// <summary>직선 벨트(1-in/1-out, 입출력이 반대 면)인가.</summary>
        public bool IsStraight => Element == BeltElementKind.Straight;
    }
}
