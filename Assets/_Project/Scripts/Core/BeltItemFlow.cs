using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>벨트 한 칸 위에 얹힌 아이템 하나.</summary>
    public struct BeltItem
    {
        /// <summary>무엇인가. 벨트의 <see cref="BeltInstance.Kind"/>와 같아야 한다.</summary>
        public FlowKind kind;

        /// <summary>칸 안 위치. 0 = 입력면에 막 들어옴 · 1 = 출력면에 닿음.</summary>
        public float progress;
    }

    /// <summary>
    /// 벨트 위 **개별 아이템의 이동**(2026-09-03 신설 · `260903_W01` 4장).
    ///
    /// 종전에는 벨트가 「무엇을 나르는가」(<see cref="BeltFlow"/>의 <see cref="FlowKind"/>)만 알았고
    /// 개수도 위치도 없었다. 그래서 「정체」와 「막힘」이라는 상태가 벨트에 존재하지 않았고,
    /// 조립 시스템 문서「벨트와 병목 시각화」의 「과잉 = 정체로 꽉 참 / 부족 = 텅 비었음」이
    /// 문서에만 있었다. 이 클래스가 그 자리를 만든다.
    ///
    /// **계산 경로를 대체하지 않는다.** <see cref="LogisticsNetwork"/>의 합계 기반 산출은 그대로
    /// 두고 이것을 옆에 쌓는다 — 도착량을 실제 전투력으로 쓸지는 판정 대기이며(`260903_V02` 3장),
    /// 어느 쪽으로 정해져도 버릴 것이 없게 하기 위해서다.
    ///
    /// 순수·결정론이다. <see cref="BoardGrid"/>를 읽고 자기 상태만 들며 씬을 모른다.
    /// </summary>
    public sealed class BeltItemFlow
    {
        /// <summary>
        /// 아이템이 한 칸을 지나는 속도(칸/초). **잠정치 — 설계가 실측 후 확정한다**(`260903_W01` 7-3).
        ///
        /// 4와 <see cref="MinGapCells"/> 1/3을 곱하면 한 줄 처리량이 12/초가 되어
        /// 밸런스 문서「수치 산정 대상 카탈로그」의 벨트 단일 규격과 맞는다.
        /// 값을 바꾸려면 그 규격과 함께 봐야 한다.
        /// </summary>
        public const float CellsPerSecondTbd = 4f;

        /// <summary>
        /// 아이템 사이 최소 간격(칸). 정체 시 이 간격으로 붙어 선다.
        /// **잠정치** — 처리량 = 속도 ÷ 간격이므로 위 상수와 한 쌍이다.
        /// </summary>
        public const float MinGapCells = 1f / 3f;

        /// <summary>한 칸에 설 수 있는 최대 개수. 간격에서 파생되며 따로 정하지 않는다.</summary>
        public static int MaxPerCell => Mathf.FloorToInt(1f / MinGapCells);

        // 셀 → 그 칸에 얹힌 아이템들. **앞선 것(progress 큰 것)이 앞에 온다.**
        private readonly Dictionary<Vector2Int, List<BeltItem>> _lanes =
            new Dictionary<Vector2Int, List<BeltItem>>();

        // 셀 → 다음 셀. 링크가 없으면 그 칸이 라인의 끝이다.
        private readonly Dictionary<Vector2Int, Vector2Int> _next =
            new Dictionary<Vector2Int, Vector2Int>();

        /// <summary>라인 끝에서 빠져나간 누적 개수. 도착량을 세는 자리다.</summary>
        public int DeliveredCount { get; private set; }

        /// <summary>
        /// 배치가 바뀔 때마다 부른다. 링크를 다시 잡고 **없어진 칸의 아이템은 버린다** —
        /// 벨트를 걷어내면 그 위의 물건도 같이 사라지는 것이 자연스럽다.
        /// </summary>
        public void Rebuild(BoardGrid grid)
        {
            _next.Clear();
            if (grid == null) { _lanes.Clear(); return; }

            foreach (BeltLink link in BeltRouting.BuildLinks(grid))
            {
                // 분류기는 출력이 여럿이라 한 칸이 여러 다음을 가질 수 있다.
                // 개체 단위 분배는 3번 덩어리 소관이므로 여기서는 첫 링크만 잡는다.
                if (!_next.ContainsKey(link.fromCell)) _next[link.fromCell] = link.toCell;
            }

            var gone = new List<Vector2Int>();
            foreach (Vector2Int cell in _lanes.Keys)
                if (!grid.HasBelt(cell)) gone.Add(cell);
            foreach (Vector2Int cell in gone) _lanes.Remove(cell);
        }

        /// <summary>이 칸에 아이템을 하나 올린다. 자리가 없으면 <c>false</c> — 그것이 상류 정지 신호다.</summary>
        public bool TryInsert(Vector2Int cell, FlowKind kind)
        {
            List<BeltItem> lane = LaneOf(cell);
            if (!HasRoomAtEntry(lane)) return false;

            lane.Add(new BeltItem { kind = kind, progress = 0f });
            return true;
        }

        /// <summary>
        /// 한 틱 밀어 낸다. **앞선 아이템부터 처리한다** — 뒤에서부터 밀면
        /// 앞이 아직 안 비었는데 뒤가 들어가 한 틱 동안 겹친다.
        /// </summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            float step = CellsPerSecondTbd * dt;

            // 하류부터 처리해야 앞 칸이 비운 자리를 뒤 칸이 같은 틱에 쓸 수 있다.
            foreach (Vector2Int cell in OrderedDownstreamFirst())
            {
                if (!_lanes.TryGetValue(cell, out List<BeltItem> lane) || lane.Count == 0) continue;

                for (int i = 0; i < lane.Count; i++)
                {
                    BeltItem item = lane[i];
                    float limit = CeilingFor(lane, i);
                    item.progress = Mathf.Min(item.progress + step, limit);

                    if (item.progress >= 1f && TryHandOff(cell, item))
                    {
                        lane.RemoveAt(i);
                        i--;
                        continue;
                    }

                    lane[i] = item;
                }
            }
        }

        /// <summary>
        /// 이 칸에서 물건이 나가는 다음 칸. 없으면 라인의 끝이다.
        ///
        /// **노드 셀도 키가 된다** — <see cref="BeltRouting.BuildLinks"/>가 노드의 출력 포트에서도
        /// 링크를 만들기 때문이다. 노드가 출력 버퍼를 벨트로 밀어 넣을 때 이것으로 목적지를 찾는다.
        /// </summary>
        public bool TryNextOf(Vector2Int cell, out Vector2Int to) => _next.TryGetValue(cell, out to);

        /// <summary>이 칸의 아이템들. 렌더링과 진단이 읽는다.</summary>
        public IReadOnlyList<BeltItem> ItemsAt(Vector2Int cell) =>
            _lanes.TryGetValue(cell, out List<BeltItem> lane) ? lane : System.Array.Empty<BeltItem>();

        /// <summary>
        /// 이 칸이 막혔는가 — 맨 앞 아이템이 출력면에 닿은 채 못 나가고 있다.
        /// 상류 전파(2번 덩어리)와 병목 표시가 이것을 읽는다.
        /// </summary>
        public bool IsBlocked(Vector2Int cell) =>
            _lanes.TryGetValue(cell, out List<BeltItem> lane)
            && lane.Count > 0 && lane[0].progress >= 1f;

        private List<BeltItem> LaneOf(Vector2Int cell)
        {
            if (!_lanes.TryGetValue(cell, out List<BeltItem> lane))
            {
                lane = new List<BeltItem>();
                _lanes[cell] = lane;
            }
            return lane;
        }

        // 입구에 자리가 있는가. 마지막(가장 뒤) 아이템이 간격만큼 들어가 있어야 한다.
        private static bool HasRoomAtEntry(List<BeltItem> lane)
        {
            if (lane.Count >= MaxPerCell) return false;
            return lane.Count == 0 || lane[lane.Count - 1].progress >= MinGapCells;
        }

        // i번째가 갈 수 있는 최대 위치. 앞이 있으면 그 뒤 간격까지, 없으면 출력면까지.
        private static float CeilingFor(List<BeltItem> lane, int i) =>
            i == 0 ? 1f : lane[i - 1].progress - MinGapCells;

        // 다음 칸으로 넘긴다. 다음이 없으면 라인의 끝이므로 배출로 센다.
        private bool TryHandOff(Vector2Int cell, BeltItem item)
        {
            if (!_next.TryGetValue(cell, out Vector2Int to))
            {
                DeliveredCount++;
                return true;
            }

            if (!HasRoomAtEntry(LaneOf(to))) return false;

            LaneOf(to).Add(new BeltItem { kind = item.kind, progress = 0f });
            return true;
        }

        // 하류가 먼저 오도록 정렬한다. 링크를 거슬러 깊이를 재고 깊은 쪽(끝단)부터 돌린다.
        private List<Vector2Int> OrderedDownstreamFirst()
        {
            var cells = new List<Vector2Int>(_lanes.Keys);
            var depth = new Dictionary<Vector2Int, int>();

            foreach (Vector2Int cell in cells)
            {
                int d = 0;
                Vector2Int at = cell;
                // 순환 벨트가 허용돼 있으므로(조립 문서「연결 규칙」) 칸 수로 상한을 둔다.
                while (_next.TryGetValue(at, out Vector2Int nxt) && d < cells.Count)
                {
                    at = nxt;
                    d++;
                }
                depth[cell] = d;
            }

            cells.Sort((a, b) => depth[a].CompareTo(depth[b]));
            return cells;
        }
    }
}
