using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>벨트 1칸의 설치 사양(셀·입력면·출력면). BoardGrid.TryPlaceBelt에 그대로 넘긴다.</summary>
    public struct BeltSegmentSpec
    {
        public Vector2Int cell;
        public PortFace inFace;
        public PortFace outFace;
    }

    /// <summary>
    /// 드래그한 셀 경로 → 벨트 세그먼트 배향(순수·결정론, §5-4 L1). 아이템은 경로 방향(c0→cn)으로 흐른다.
    ///   - 첫 칸: 출력면=다음 칸 방향, 입력면=그 반대(뒤에서 유입).
    ///   - 중간 칸: 입력면=이전 칸 방향, 출력면=다음 칸 방향(꺾이면 코너).
    ///   - 끝 칸: 입력면=이전 칸 방향, 출력면=그 반대(앞으로 배출).
    /// 인접(직교 1칸) 경로 가정 — BoardController가 드래그를 직교 인접 셀로 샘플링.
    /// </summary>
    public static class BeltPath
    {
        public static List<BeltSegmentSpec> Build(IReadOnlyList<Vector2Int> cells)
        {
            var segs = new List<BeltSegmentSpec>();
            if (cells == null || cells.Count == 0) return segs;

            if (cells.Count == 1)
            {
                // 방향 정보 없음 → 기본 직선(서→동).
                segs.Add(new BeltSegmentSpec { cell = cells[0], inFace = PortFace.West, outFace = PortFace.East });
                return segs;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                PortFace inFace, outFace;
                if (i == 0)
                {
                    outFace = FaceTo(cells[1] - cells[0]);
                    inFace = NodeConnectionRules.Opposite(outFace);
                }
                else if (i == cells.Count - 1)
                {
                    inFace = FaceTo(cells[i - 1] - cells[i]);
                    outFace = NodeConnectionRules.Opposite(inFace);
                }
                else
                {
                    inFace = FaceTo(cells[i - 1] - cells[i]);
                    outFace = FaceTo(cells[i + 1] - cells[i]);
                }
                segs.Add(new BeltSegmentSpec { cell = cells[i], inFace = inFace, outFace = outFace });
            }
            return segs;
        }

        /// <summary>델타(인접 이웃 방향)가 가리키는 면. 직교 인접 가정.</summary>
        public static PortFace FaceTo(Vector2Int delta)
        {
            if (delta.x > 0) return PortFace.East;
            if (delta.x < 0) return PortFace.West;
            if (delta.y > 0) return PortFace.North;
            return PortFace.South;
        }
    }
}
