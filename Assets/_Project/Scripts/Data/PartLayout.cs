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
        /// <summary>
        /// 격자 세로 — **14** (2026-09-14 · §72-6 사용자 확정 · 구 13).
        ///
        /// ⚠️ **맨 윗줄(y13)은 파츠가 아니다** — 어느 파츠 사각에도 안 들어가므로
        /// <see cref="IsValid"/> 가 거짓이고 **노드를 놓을 수 없다.** 마운트 적재 칸만 그 줄을 쓴다.
        /// 그래서 줄이 하나 늘었는데도 <see cref="ValidCells"/> 는 그만큼 안 는다.
        /// </summary>
        public const int Rows = 14;

        /// <summary>
        /// 실루엣 안 칸 수 — **120** (§72-6 · 구 117).
        /// 머리가 3×3 에서 **4×3** 으로 넓어진 셋만큼 늘었다.
        /// </summary>
        public const int ValidCells = 120;

        // y는 아래에서 위로 증가한다(격자 좌하단 원점).
        //   y 0~3   다리 (4칸)
        //   y 4~9   몸통 (6칸) · 팔은 y 4~8, 어깨는 y 9~11
        //   y 10~12 머리 (**x 4~7 · 4칸 폭** · §72-6)
        //   y 13    **파츠 없음** — 마운트 적재 칸 전용 줄이다(배치 불가)
        //
        // ⚠️ **머리 옆 x3 · x8 은 y10~12 에서 비어 있다**(어깨는 x0~2 · x9~11).
        // 그 빈 두 열이 y13 과 이어져 **B 마운트 적재 칸 넷씩**이 선다(§72-6).
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
            new PartRect(RobotPart.Head,      4, 10, 4, 3),
        };

        public static IReadOnlyList<PartRect> Parts => Layout;

        /// <summary>
        /// 구역 라벨 여덟 — 화면에 그리는 이름표의 문자열 (2026-09-05 확정 · `260905_W01` 1장).
        ///
        /// **붙여 쓴다.** 코드의 <see cref="RobotPart"/>와 한 글자씩 대응시키기 위해서다
        /// (`ArmL` = `팔L`). 띄어 쓰거나 빗금으로 묶으면(「팔 L / R」) 문서 전수 검색에서
        /// 안 걸려 개정이 전파되지 않는다 — 용어 사전「구역 라벨」 항목이 원천이다.
        ///
        /// **좌우는 로봇 기준이다.** 로봇이 카메라를 마주 보므로 `팔R`이 화면 왼쪽에 그려진다.
        /// </summary>
        public static string LabelOf(RobotPart part)
        {
            switch (part)
            {
                case RobotPart.Head:      return "머리";
                case RobotPart.Torso:     return "몸통";
                case RobotPart.ShoulderL: return "어깨L";
                case RobotPart.ShoulderR: return "어깨R";
                case RobotPart.ArmL:      return "팔L";
                case RobotPart.ArmR:      return "팔R";
                case RobotPart.LegL:      return "다리L";
                case RobotPart.LegR:      return "다리R";
                default:                  return string.Empty;
            }
        }

        /// <summary>
        /// 구역 경계선 한 도막 — 격자 **모서리** 좌표에서 시작해 한 방향으로 <see cref="length"/>칸.
        ///
        /// 칸이 아니라 칸의 꼭짓점 좌표다. 가로 도막 <c>(3, 4, 길이 6)</c>은 (3,4)에서 (9,4)까지의
        /// 선이며, 그 아래위 칸이 아니라 **그 둘 사이의 금**을 가리킨다.
        /// </summary>
        public readonly struct BoundaryRun
        {
            public readonly Vector2Int from;
            public readonly int length;      // 칸 수
            public readonly bool horizontal;

            public BoundaryRun(Vector2Int from, int length, bool horizontal)
            {
                this.from = from;
                this.length = length;
                this.horizontal = horizontal;
            }
        }

        private static BoundaryRun[] _boundaryRuns;

        /// <summary>
        /// 구역 경계선 전체를 **겹치지 않는** 도막들로 준다.
        ///
        /// ⚠️ 파츠마다 네 변을 따로 그리면 **맞닿은 변이 두 번 그려진다.** 여덟 파츠의 변을
        /// 다 더하면 120칸인데 서로 다른 변은 89칸뿐이다 — 31칸이 겹친다(몸통↔팔 등).
        /// 경계선이 반투명(불투명도 40%)이라 겹친 자리만 진해지고, 두 파츠의 변 길이가 달라
        /// 점선 위상까지 어긋나 그 자리가 실선처럼 보인다. 그래서 **먼저 합치고 나서 그린다.**
        ///
        /// 도막으로 잇는 이유는 점선 주기를 도막 길이에 맞춰야 끝에서 잘리지 않기 때문이다.
        /// 한 칸씩 그리면 칸마다 주기가 새로 시작해 경계가 촘촘한 점선으로 보인다.
        /// </summary>
        public static IReadOnlyList<BoundaryRun> BoundaryRuns()
        {
            if (_boundaryRuns != null) return _boundaryRuns;

            var horizontal = new HashSet<Vector2Int>();
            var vertical = new HashSet<Vector2Int>();
            foreach (PartRect p in Layout)
            {
                for (int x = p.origin.x; x < p.origin.x + p.size.x; x++)
                {
                    horizontal.Add(new Vector2Int(x, p.origin.y));                // 아랫변
                    horizontal.Add(new Vector2Int(x, p.origin.y + p.size.y));     // 윗변
                }
                for (int y = p.origin.y; y < p.origin.y + p.size.y; y++)
                {
                    vertical.Add(new Vector2Int(p.origin.x, y));                  // 왼변
                    vertical.Add(new Vector2Int(p.origin.x + p.size.x, y));       // 오른변
                }
            }

            var runs = new List<BoundaryRun>();
            AppendRuns(horizontal, true, runs);
            AppendRuns(vertical, false, runs);
            _boundaryRuns = runs.ToArray();
            return _boundaryRuns;
        }

        /// <summary>한 줄(가로변은 y가, 세로변은 x가 같은 것)에서 이어진 칸들을 한 도막으로 묶는다.</summary>
        private static void AppendRuns(HashSet<Vector2Int> edges, bool horizontal, List<BoundaryRun> into)
        {
            int laneMax  = horizontal ? Rows : Columns;      // 줄 번호의 상한(모서리라 칸 수 + 1개)
            int alongMax = horizontal ? Columns : Rows;      // 줄을 따라 갈 수 있는 칸 수

            for (int lane = 0; lane <= laneMax; lane++)
            {
                int start = -1;
                for (int a = 0; a <= alongMax; a++)          // alongMax에서 한 번 더 돌아 끝 도막을 닫는다
                {
                    bool has = a < alongMax &&
                        edges.Contains(horizontal ? new Vector2Int(a, lane) : new Vector2Int(lane, a));

                    if (has && start < 0) start = a;
                    else if (!has && start >= 0)
                    {
                        into.Add(new BoundaryRun(
                            horizontal ? new Vector2Int(start, lane) : new Vector2Int(lane, start),
                            a - start, horizontal));
                        start = -1;
                    }
                }
            }
        }

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
            // 로봇 A — 팔R(x 0~2 · y 4~8) 서쪽 **바깥면**, 세로 중앙 y=6. 화면에서는 왼쪽 팔이다.
            // ⚠️ **(0,6) 서면 → (1,4) 남면**(2026-09-14 사용자 확정 · §72-24).
            //
            // 구 자리는 팔R **바깥면**이라 운반로가 실루엣 왼쪽 끝까지 돌아 나갔고,
            // 도착 칸이 **그림 열(x1 · y0~3)과 따로 놀았다.** 남면으로 내리면 도착 칸이
            // **(1,3)** 이 되어 **묶음 맨 윗 칸과 같은 자리**가 된다 — 물건이 어디로
            // 들어가는지가 그림에서 바로 읽힌다.
            //
            // 그림 열은 **그대로 x1 · y0~3** 이다(`MountDisplay.RobotAColumn`).
            new MountPort(new Vector2Int(1, 4), PortFace.South, MountOwner.RobotA),

            // ⚠️ **로봇 B 의 바깥면 둘은 폐기됐다**(2026-09-14 · §72-6).
            // 구: (0,10) 서면 · (11,10) 동면 — 실루엣 **바깥**이라 화면 끝에서 잘렸고,
            // 양쪽에 하나씩이라 **한 화면에 둘 다 못 넣었다.**
            // 신: 어깨의 **안쪽 면**을 써서 머리 옆 빈 열(x3 · x8)로 적재 칸이 선다 —
            // 실루엣 **안**이라 스크롤 없이 보인다.
            new MountPort(new Vector2Int(2, 10), PortFace.East, MountOwner.RobotB),   // 어깨R 안쪽
            new MountPort(new Vector2Int(9, 10), PortFace.West, MountOwner.RobotB),   // 어깨L 안쪽
        };

        /// <summary>
        /// 포트 **전부**. ⚠️ 판 하나를 볼 때는 쓰지 말 것 — 보드가 로봇별로 갈린 뒤
        /// (2026-09-16) 한 판에는 **자기 주인의 포트만** 있다. 이 목록은 그림을 깔거나
        /// 「어디에 마운트가 있나」를 통째로 셀 때만 쓴다.
        /// </summary>
        public static IReadOnlyList<MountPort> MountPorts => Mounts;

        /// <summary>그 로봇의 포트만. A 는 하나 · B 는 둘이다.</summary>
        public static IEnumerable<MountPort> MountPortsFor(MountOwner owner)
        {
            for (int i = 0; i < Mounts.Length; i++)
                if (Mounts[i].owner == owner) yield return Mounts[i];
        }

        /// <summary>
        /// 이 칸의 이 면에 **누구든** 마운트 고정 포트가 붙어 있는가.
        ///
        /// ⚠️ **판 하나를 읽는 자리에서는 주인을 같이 물을 것**
        /// (<see cref="TryGetMountPort(Vector2Int,PortFace,MountOwner,out MountPort)"/>).
        /// 주인을 안 물으면 **A 판에 깐 운반로가 B 포트로 흘러드는 것으로 읽힌다** —
        /// 그림은 멀쩡한데 도착이 엉뚱한 로봇에게 세어지는, 신호 없는 결함이 된다.
        /// </summary>
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

        /// <summary>
        /// 이 칸의 이 면에 **그 로봇의** 마운트 고정 포트가 붙어 있는가
        /// (2026-09-16 신설 · 보드 로봇별 분리).
        ///
        /// 판이 자기 주인을 들고 있으므로(`BoardGrid.Owner`) 판을 받는 함수는 전부
        /// 이쪽을 부른다 — 서명이 안 바뀌고 주인이 두 곳에 안 산다.
        /// </summary>
        public static bool TryGetMountPort(Vector2Int cell, PortFace face, MountOwner owner,
            out MountPort port)
        {
            for (int i = 0; i < Mounts.Length; i++)
            {
                if (Mounts[i].cell != cell || Mounts[i].face != face) continue;
                if (Mounts[i].owner != owner) continue;
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
