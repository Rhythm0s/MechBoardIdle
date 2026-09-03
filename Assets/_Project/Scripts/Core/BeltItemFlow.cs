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

        // 셀 → 나갈 수 있는 다음 칸들. 비었으면 그 칸이 라인의 끝이다.
        // **분류기는 여럿이다** — 한 입력면에서 여러 출력면으로 갈린다(조립 문서「연결 규칙」).
        private readonly Dictionary<Vector2Int, List<Vector2Int>> _next =
            new Dictionary<Vector2Int, List<Vector2Int>>();

        // 분류기에서 마지막으로 고른 갈래. 품목이 안 맞을 때 돌아가며 고르기 위한 자리다.
        private readonly Dictionary<Vector2Int, int> _cursor = new Dictionary<Vector2Int, int>();

        // 칸 → 그 벨트가 나르는 품목. 분류기가 갈래를 고를 때 읽는다.
        private readonly Dictionary<Vector2Int, FlowKind> _cellKind =
            new Dictionary<Vector2Int, FlowKind>();

        // 노드가 놓인 칸. **벨트가 아니므로 아이템을 얹지 않고 받아 삼킨다.**
        private readonly HashSet<Vector2Int> _consumers = new HashSet<Vector2Int>();

        // 품목 → 소비처로 도착한 누적 개수. 출력 교체(6번 덩어리)가 읽을 값이다.
        private readonly Dictionary<FlowKind, int> _arrived = new Dictionary<FlowKind, int>();

        /// <summary>
        /// 소비처(노드)로 도착한 누적 개수.
        ///
        /// ⚠️ **소비처 없는 라인 끝은 여기 안 들어온다** (2026-09-03 · `260903_W01` 7-1).
        /// 종전에는 다음 칸이 없으면 무조건 이 값을 올리고 아이템을 지웠다. 그러면 벨트를
        /// 허공으로 뻗어 놓아도 물건이 계속 빠져나가 **「단절」이라는 상태가 성립하지 않았다** —
        /// 병합기가 영영 안 차고 상류가 멈추지 않았다.
        /// </summary>
        public int DeliveredCount { get; private set; }

        /// <summary>이 품목이 소비처에 도착한 누적 개수.</summary>
        public int ArrivedOf(FlowKind kind) =>
            _arrived.TryGetValue(kind, out int n) ? n : 0;

        /// <summary>
        /// 배치가 바뀔 때마다 부른다. 링크를 다시 잡고 **없어진 칸의 아이템은 버린다** —
        /// 벨트를 걷어내면 그 위의 물건도 같이 사라지는 것이 자연스럽다.
        /// </summary>
        public void Rebuild(BoardGrid grid)
        {
            _next.Clear();
            _cursor.Clear();
            _cellKind.Clear();
            _consumers.Clear();
            if (grid == null) { _lanes.Clear(); return; }

            // 칸마다 무엇을 나르는 벨트인지 미리 적어 둔다. 분류기가 갈래를 고를 때 읽는다 —
            // 매번 격자를 다시 묻지 않기 위해서다.
            // 노드 칸은 따로 적어 둔다. `BeltRouting.BuildLinks`가 벨트에서 노드의 입력면으로도
            // 링크를 만들기 때문에(그쪽 TryLink), 노드 셀이 `_next`의 목적지로 들어온다.
            for (int x = 0; x < grid.Columns; x++)
            for (int y = 0; y < grid.Rows; y++)
            {
                var c = new Vector2Int(x, y);
                if (grid.GetAt(c) != null) { _consumers.Add(c); continue; }
                BeltInstance belt = grid.GetBeltAt(c);
                if (belt != null) _cellKind[c] = belt.Kind;
            }

            foreach (BeltLink link in BeltRouting.BuildLinks(grid))
            {
                if (!_next.TryGetValue(link.fromCell, out List<Vector2Int> outs))
                {
                    outs = new List<Vector2Int>();
                    _next[link.fromCell] = outs;
                }
                if (!outs.Contains(link.toCell)) outs.Add(link.toCell);
            }

            var gone = new List<Vector2Int>();
            foreach (Vector2Int cell in _lanes.Keys)
                if (!grid.HasBelt(cell)) gone.Add(cell);
            foreach (Vector2Int cell in gone) _lanes.Remove(cell);
        }

        /// <summary>
        /// 이 칸에 아이템을 하나 올린다. 자리가 없으면 <c>false</c> — 그것이 상류 정지 신호다.
        ///
        /// **목적지가 노드면 얹지 않고 도착으로 센다.** 노드끼리 맞붙은 배치에서
        /// <see cref="BoardItemTick"/>이 이 함수로 바로 넘기기 때문이다.
        /// </summary>
        public bool TryInsert(Vector2Int cell, FlowKind kind)
        {
            if (_consumers.Contains(cell)) { Arrive(kind); return true; }

            List<BeltItem> lane = LaneOf(cell);
            if (!HasRoomAtEntry(lane)) return false;

            lane.Add(new BeltItem { kind = kind, progress = 0f });
            return true;
        }

        // 소비처가 받았다. 총계와 품목별을 함께 올린다.
        private void Arrive(FlowKind kind)
        {
            DeliveredCount++;
            _arrived[kind] = (_arrived.TryGetValue(kind, out int n) ? n : 0) + 1;
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
        /// <remarks>
        /// 갈래가 여럿이면 **첫 번째**를 준다. 노드의 출력 포트는 보통 하나라 이것으로 충분하고,
        /// 여럿인 노드가 생기면 <see cref="TryHandOff"/>처럼 골라 주는 쪽으로 바꿔야 한다.
        /// </remarks>
        public bool TryNextOf(Vector2Int cell, out Vector2Int to)
        {
            to = default;
            if (!_next.TryGetValue(cell, out List<Vector2Int> outs) || outs.Count == 0) return false;
            to = outs[0];
            return true;
        }

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

        /// <summary>
        /// 다음 칸으로 넘긴다. **넘길 곳이 없으면 <c>false</c> — 그 자리에 선다.**
        ///
        /// 소비처(노드)에 닿으면 도착으로 세고, 아무 데도 안 닿은 라인 끝에서는 쌓인다.
        /// 종전에는 끝에서 무조건 배출해 **벨트를 허공으로 뻗어 놔도 물건이 계속 빠져나갔다** —
        /// 그래서 조립 시스템 문서가 말하는 「단절」이 화면에 성립하지 않았다.
        ///
        /// **분류기가 뜻을 갖는 자리다.** 갈래가 여럿이면 품목이 맞는 쪽을 먼저 보고,
        /// 맞는 곳이 없거나 다 찼으면 돌아가며 고른다 — 한쪽만 계속 먹으면 나머지 갈래가
        /// 영영 비어 「섞으려면 분류기를 놓는다」가 성립하지 않는다.
        /// </summary>
        private bool TryHandOff(Vector2Int cell, BeltItem item)
        {
            if (!_next.TryGetValue(cell, out List<Vector2Int> outs) || outs.Count == 0)
                return false; // 소비처 없는 라인 끝 — 출력면에 붙어 멈춘다

            for (int i = 0; i < outs.Count; i++)
            {
                Vector2Int to = outs[i];

                // 노드로 가는 링크는 품목이 맞을 때만 서므로(`BeltRouting.TryLink`의 HasInputPort)
                // 여기 왔다는 것 자체가 받을 수 있다는 뜻이다. 노드 칸에는 아이템을 얹지 않는다.
                if (_consumers.Contains(to)) { Arrive(item.kind); return true; }

                if (!_cellKind.TryGetValue(to, out FlowKind kind) || kind != item.kind) continue;
                if (!HasRoomAtEntry(LaneOf(to))) continue;

                LaneOf(to).Add(new BeltItem { kind = item.kind, progress = 0f });
                return true;
            }

            int start = _cursor.TryGetValue(cell, out int c) ? c : 0;
            for (int n = 0; n < outs.Count; n++)
            {
                int idx = (start + n) % outs.Count;
                Vector2Int to = outs[idx];
                if (!HasRoomAtEntry(LaneOf(to))) continue;

                LaneOf(to).Add(new BeltItem { kind = item.kind, progress = 0f });
                _cursor[cell] = (idx + 1) % outs.Count;
                return true;
            }

            return false; // 모든 갈래가 찼다 — 이 칸이 정체된다
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
                // 갈래가 여럿이면 첫 번째만 따라간다 — 깊이는 순서를 정하는 근사치면 충분하다.
                while (_next.TryGetValue(at, out List<Vector2Int> outs) && outs.Count > 0
                       && d < cells.Count)
                {
                    at = outs[0];
                    d++;
                }
                depth[cell] = d;
            }

            cells.Sort((a, b) => depth[a].CompareTo(depth[b]));
            return cells;
        }
    }
}
