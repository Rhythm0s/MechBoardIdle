using System.Collections.Generic;
using UnityEngine;

namespace MBI.Data
{
    /// <summary>
    /// 로봇 파츠 8종. 격자 소속 태그이자 시각 표시 단위(UI 문서 9-2 「파츠 경계가 또렷해진다」).
    ///
    /// ⚠️ **L·R은 로봇 기준이다** (2026-09-05 확정 · `260905_W01` 1장).
    /// 로봇이 카메라를 마주 보므로(`Direction: South`) **로봇의 오른쪽이 화면 왼쪽**이고,
    /// 격자에서 x가 작은 쪽이다. 즉 `ArmR`이 x 0~2, `ArmL`이 x 9~11이다.
    ///
    /// 2026-09-05까지 이 라벨이 **화면 기준으로 붙어 있었다** — x 0~2가 `ArmL`이었다.
    /// 그 상태로 「마운트는 팔R」이라는 조립 문서를 읽으면 마운트가 반대쪽 팔에 붙는다.
    /// 좌표는 처음부터 화면 왼쪽이라 맞았고, 어긋난 것은 이름뿐이었다.
    /// </summary>
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

    /// <summary>마운트가 붙은 로봇. 마운트 수는 A·B 각 1개다 — 포트가 둘이어도 마운트는 하나다.</summary>
    public enum MountOwner
    {
        RobotA,
        RobotB,
    }

    /// <summary>
    /// 마운트 고정 포트 — 보드에서 만든 것이 마운트 적재로 넘어가는 자리
    /// (2026-09-04 신설 · `260904_W01` 2장).
    ///
    /// **노드가 아니다.** 지침 3장이 노드를 「더 놓으면 결과가 바뀌는 것」으로 정의하는데
    /// 이것은 붙박이라 더 놓을 수도 뺄 수도 없다. **격자도 안 먹는다** — 칸이 아니라 칸의
    /// 경계에 붙으므로 117칸은 그대로다.
    ///
    /// <see cref="face"/>는 그 칸에서 **바깥을 향하는 면**이다. 벨트의 출력면이 이 면과
    /// 같으면 물건이 마운트로 나가며, 그 도착이 곧 적재다 — 사이에 층을 두지 않는다(W01 2-2).
    /// </summary>
    public readonly struct MountPort
    {
        public readonly Vector2Int cell;
        public readonly PortFace face;
        public readonly MountOwner owner;

        public MountPort(Vector2Int cell, PortFace face, MountOwner owner)
        {
            this.cell = cell;
            this.face = face;
            this.owner = owner;
        }
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
    ///   가로 = 팔R 3 + 몸통 6 + 팔L 3 = 12 (정확히 일치 · L·R은 로봇 기준이라 R이 화면 왼쪽)
    ///   세로 = 머리 3 + 몸통 6 + 다리 4 = 13 (정확히 일치)
    ///   다리R 3 + 다리L 3 = 6 = 몸통 폭 (정확히 일치)
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
            // x가 작은 쪽 = 화면 왼쪽 = **로봇의 오른쪽**이다(위 열거 주석 참조).
            new PartRect(RobotPart.LegR,      3,  0, 3, 4),
            new PartRect(RobotPart.LegL,      6,  0, 3, 4),
            new PartRect(RobotPart.Torso,     3,  4, 6, 6),
            new PartRect(RobotPart.ArmR,      0,  4, 3, 5),
            new PartRect(RobotPart.ArmL,      9,  4, 3, 5),
            new PartRect(RobotPart.ShoulderR, 0,  9, 3, 3),
            new PartRect(RobotPart.ShoulderL, 9,  9, 3, 3),
            new PartRect(RobotPart.Head,      4, 10, 3, 3),
        };

        public static IReadOnlyList<PartRect> Parts => Layout;

        /// <summary>
        /// 마운트 고정 포트 셋 (2026-09-04 · `260904_W01` 2-3).
        ///
        /// A는 팔 바깥면 1개, B는 어깨 L·R 바깥면 각 1개다. 바깥 경계면에 둔 근거는
        /// 안쪽이나 아래에 두면 벨트가 어깨를 지나갈 필요가 없어져 **어깨 9칸이 물류에서
        /// 빠지기** 때문이다 — 로봇 B는 라인을 두 갈래로 갈라야 하고, 그래서 병합기·분류기를
        /// 쓸 이유가 늘어난다.
        ///
        /// ✅ **좌우가 확정됐다** (2026-09-05 · `260905_W01` 1장). 「로봇 기준 오른팔, 화면 기준
        /// 왼쪽」이다. 근거는 승인본 실측(화면 왼쪽 팔이 아래까지 내려와 끝이 평평한 총구이고,
        /// 오른쪽 팔은 둥근 주먹으로 끝난다)과 프롬프트의 `Direction: South (facing camera)`다.
        ///
        /// 종전에 「구현이 고른 가정」이라 적어 두었던 것이 **결과적으로 맞았다** — 화면 왼쪽을
        /// 골랐고 그것이 로봇의 오른팔이다. 다만 그때 부른 이름(`팔L`)이 화면 기준이라 틀렸다.
        /// 세로 중앙 칸에 둔 것은 그대로 유지한다 — 라인이 어느 쪽에서 와도 거리가 같은 자리다.
        /// </summary>
        private static readonly MountPort[] Mounts =
        {
            // 로봇 A — 팔R(x 0~2 · y 4~8) 서쪽 바깥면, 세로 중앙 y=6. 화면에서는 왼쪽 팔이다.
            new MountPort(new Vector2Int(0, 6), PortFace.West, MountOwner.RobotA),

            // 로봇 B — 어깨R(x 0~2 · y 9~11) 서쪽, 어깨L(x 9~11) 동쪽. 세로 중앙 y=10
            new MountPort(new Vector2Int(0, 10), PortFace.West, MountOwner.RobotB),
            new MountPort(new Vector2Int(11, 10), PortFace.East, MountOwner.RobotB),
        };

        public static IReadOnlyList<MountPort> MountPorts => Mounts;

        /// <summary>이 칸의 이 면에 마운트 고정 포트가 붙어 있는가.</summary>
        public static bool TryGetMountPort(Vector2Int cell, PortFace face, out MountPort port)
        {
            for (int i = 0; i < Mounts.Length; i++)
            {
                if (Mounts[i].cell != cell || Mounts[i].face != face) continue;
                port = Mounts[i];
                return true;
            }
            port = default;
            return false;
        }

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
