using System.Collections.Generic;
using UnityEngine;

namespace MBI.Data
{
    /// <summary>로봇 파츠 8종. 격자 소속 태그이자 시각 표시 단위(UI 문서 9-2 「파츠 경계가 또렷해진다」).</summary>
    public enum RobotPart
    {
        None = 0,
        Head,
        Torso,
        ShoulderL,
        ShoulderR,
        ArmL,
        ArmR,
        LegL,
        LegR,
    }

    /// <summary>파츠 한 개가 차지하는 직사각 영역(셀 단위, 좌하단 기준).</summary>
    [System.Serializable]
    public struct PartRect
    {
        public RobotPart part;
        public Vector2Int origin; // 좌하단 셀
        public Vector2Int size;   // 가로 × 세로 (칸)

        public PartRect(RobotPart part, int x, int y, int w, int h)
        {
            this.part = part;
            origin = new Vector2Int(x, y);
            size = new Vector2Int(w, h);
        }

        public int Cells => size.x * size.y;

        public bool Contains(Vector2Int cell) =>
            cell.x >= origin.x && cell.x < origin.x + size.x &&
            cell.y >= origin.y && cell.y < origin.y + size.y;
    }

    /// <summary>
    /// 로봇 실루엣 격자(조립 시스템 문서 11장 「보드 격자 규격」, 2026-08-24 개정).
    ///
    /// 실루엣 12 × 13 = 156칸 중 **유효 117칸.** 나머지 39칸은 팔·다리 사이의 빈 공간이라
    /// 배치 불가다 — 격자가 직사각형이 아니라는 것이 이 개정의 핵심이다.
    ///
    /// 파츠별 크기는 11-2 표 확정값:
    ///   몸통 6×6=36 · 팔 L/R 3×5=15 · 다리 L/R 3×4=12 · 어깨 L/R 3×3=9 · 머리 3×3=9 → 117
    ///
    /// 최소 폭 3칸 원칙(11-2): 폭 2칸이면 병합기·분류기로 갈라질 자리가 없어 직선 한 줄만 나온다.
    ///
    /// ⚠️ **파츠의 실루엣 내 위치는 원천 문서에 없다**(조립 11장은 칸 수만, UI 9장은 화면만 정함).
    /// 아래 배치는 확정된 크기에서 산술로 도출한 것이다:
    ///   가로 = 팔L 3 + 몸통 6 + 팔R 3 = 12 (정확히 일치)
    ///   세로 = 머리 3 + 몸통 6 + 다리 4 = 13 (정확히 일치)
    ///   다리L 3 + 다리R 3 = 6 = 몸통 폭 (정확히 일치)
    /// 세 변이 모두 딱 떨어지므로 배치는 사실상 강제된다. 남는 자유도는 둘뿐이고 아래처럼 두었다:
    ///   (1) 머리의 좌우 오프셋 — 몸통 6칸 중앙에 3칸을 두되 좌측 정렬(x 4~6)
    ///   (2) 어깨·팔의 세로 위치 — 어깨를 몸통 상단에 맞추고 팔을 그 아래로
    /// 이 둘은 실루엣 외형에 영향을 주므로 아트 확정 시 대조가 필요하다.
    /// </summary>
    public static class PartLayout
    {
        public const int Columns = 12;
        public const int Rows = 13;
        public const int ValidCells = 117;

        // y는 아래에서 위로 증가한다(격자 좌하단 원점).
        //   y 0~3   다리 (4칸)
        //   y 4~9   몸통 (6칸) · 팔은 y 4~8, 어깨는 y 9~11
        //   y 10~12 머리 (3칸)
        private static readonly PartRect[] Layout =
        {
            new PartRect(RobotPart.LegL,      3,  0, 3, 4),
            new PartRect(RobotPart.LegR,      6,  0, 3, 4),
            new PartRect(RobotPart.Torso,     3,  4, 6, 6),
            new PartRect(RobotPart.ArmL,      0,  4, 3, 5),
            new PartRect(RobotPart.ArmR,      9,  4, 3, 5),
            new PartRect(RobotPart.ShoulderL, 0,  9, 3, 3),
            new PartRect(RobotPart.ShoulderR, 9,  9, 3, 3),
            new PartRect(RobotPart.Head,      4, 10, 3, 3),
        };

        public static IReadOnlyList<PartRect> Parts => Layout;

        /// <summary>이 셀이 속한 파츠. 유효 셀이 아니면 None.</summary>
        public static RobotPart PartAt(Vector2Int cell)
        {
            for (int i = 0; i < Layout.Length; i++)
                if (Layout[i].Contains(cell)) return Layout[i].part;
            return RobotPart.None;
        }

        /// <summary>배치 가능한 칸인가. 실루엣 사각형 안이어도 파츠에 속하지 않으면 무효다.</summary>
        public static bool IsValid(Vector2Int cell) => PartAt(cell) != RobotPart.None;

        /// <summary>유효 셀 마스크를 만든다. BoardGrid에 주입해 쓴다.</summary>
        public static HashSet<Vector2Int> BuildMask()
        {
            var mask = new HashSet<Vector2Int>();
            foreach (PartRect r in Layout)
                for (int x = r.origin.x; x < r.origin.x + r.size.x; x++)
                for (int y = r.origin.y; y < r.origin.y + r.size.y; y++)
                    mask.Add(new Vector2Int(x, y));
            return mask;
        }
    }
}
