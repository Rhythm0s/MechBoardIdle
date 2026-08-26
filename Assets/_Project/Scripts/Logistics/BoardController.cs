using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MBI.Logistics
{
    /// <summary>시작 배치 1건(셀 + 노드 + 탄종). 씬 생성기가 채운다.</summary>
    [System.Serializable]
    public struct InitialNode
    {
        public Vector2Int cell;
        public NodeDefinition node;
        /// <summary>군수 노드가 만드는 탄종(§1). 군수가 아니면 무시된다.</summary>
        public AmmoKind ammoKind;
    }

    /// <summary>
    /// 물류 보드 씬 글루(§5-3) — BoardRoot에 부착. 탭 입력을 셀로 판정해 배치/선택한다.
    ///
    /// 순수 격자 로직은 BoardGrid(MBI.Core)에 있고, 이 클래스는 씬 연동만 담당:
    ///   - BoardConfig 치수로 중앙 정렬 격자 생성(origin = 파생값, 하드코딩 아님).
    ///   - InputSystem 탭 → 화면→월드→셀 → 빈 셀 배치 / 점유 셀 선택 / 격자 밖 무시.
    ///   - 배치 시 최소 플레이스홀더 마커 스폰(§5-4 아트 전 임시).
    /// 벨트·면 연결(§5-4), 노드 팔레트·스텁 필터(§8)는 범위 밖.
    /// </summary>
    public sealed class BoardController : MonoBehaviour
    {
        [Header("설정")]
        [Tooltip("격자 치수·셀 크기(§5-3 BoardConfig). 씬 생성기가 주입.")]
        [SerializeField] private BoardConfig config;
        [Tooltip("팔레트가 비었을 때 폴백 배치 노드.")]
        [SerializeField] private NodeDefinition placeTarget;
        [Tooltip("배치 가능한 노드 팔레트(조립 뷰에서 선택). 씬 생성기가 주입.")]
        [SerializeField] private List<NodeDefinition> palette = new List<NodeDefinition>();
        [Tooltip("좌표 변환 카메라. 비우면 Camera.main.")]
        [SerializeField] private Camera boardCamera;
        [Tooltip("시작 배치(온보딩). 빈 보드로 시작하면 플레이어가 무엇을 해야 할지 알 수 없다 — 거의 완성된 라인을 주고 한 칸만 비워 둔다(튜토리얼 10장 '왼팔만 비움'을 보드에 적용).")]
        [SerializeField] private List<InitialNode> initialLayout = new List<InitialNode>();

        private int _selectedNode; // 팔레트에서 선택된 노드 인덱스
        private bool _pointerOverPalette; // 팔레트 버튼 위 클릭은 보드 무시

        /// <summary>배치 상태 격자(§5-5 출력 집계용). Awake 후 유효.</summary>
        public BoardGrid Grid => _grid;

        private BoardGrid _grid;
        private InputAction _press;
        private readonly Dictionary<Vector2Int, GameObject> _markers = new Dictionary<Vector2Int, GameObject>();
        private readonly Dictionary<Vector2Int, Color> _nodeColors = new Dictionary<Vector2Int, Color>(); // 현재 상태색(선택 복원용)
        private Vector2Int? _selected;

        // 드래그 설치(§5-4 L1b): press→drag(경로 셀 누적)→release.
        private bool _dragging;
        private readonly List<Vector2Int> _dragCells = new List<Vector2Int>();

        // ---- 조작 모드(UI 문서 9장) ----
        // 벨트 설치도 화면 이동도 「터치 후 드래그」다. 한 동작에 두 뜻이 붙으면 기계가 구분할 수
        // 없으므로 모드로 가른다. 기본은 **이동** — 처음 보는 사람이 실수로 벨트를 깔지 않게.
        private BoardMode _mode = BoardMode.Pan;
        private BoardPan _pan;
        private bool _panning;
        private Vector2 _panLastWorld;
        private Vector3 _baseWorldPosition;
        private GameObject _dimOverlay;

        [Tooltip("화면에 보이는 보드 범위(월드 유닛). UI 문서 9-3 기준 가로 7.5칸 · 세로 약 7칸.")]
        [SerializeField] private Vector2 viewSizeCells = new Vector2(7.5f, 7f);

        /// <summary>현재 조작 모드. 모드 버튼이 토글한다.</summary>
        public BoardMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;
                _panning = false;
                _dragging = false;
                _dragCells.Clear();
                ApplyModeVisual();
            }
        }

        /// <summary>모드 토글(UI 문서 9-2: 버튼 하나를 탭할 때마다 번갈아 바뀐다).</summary>
        public void ToggleMode() => Mode = _mode == BoardMode.Pan ? BoardMode.Build : BoardMode.Pan;

        /// <summary>스크롤 상태(미니맵·테스트용).</summary>
        public BoardPan Pan => _pan;

        // 벨트 마커 루트 + 방향 표시 SR(§5-4 L2 연결 색/제거용).
        private readonly Dictionary<Vector2Int, GameObject> _beltMarkers = new Dictionary<Vector2Int, GameObject>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> _beltArrows = new Dictionary<Vector2Int, SpriteRenderer>();
        // 미연결 경고 아이콘(§5-4 ⑤). 마커의 자식이라 마커 파괴 시 함께 사라진다.
        private readonly Dictionary<Vector2Int, SpriteRenderer> _beltWarnings = new Dictionary<Vector2Int, SpriteRenderer>();

        private bool _removeMode; // 제거 모드 — 탭으로 노드/벨트 삭제

        private static readonly Color SelectedColor = new Color(0.98f, 0.85f, 0.30f, 1f);
        private static readonly Color BeltColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color BeltArrowColor = new Color(0.95f, 0.85f, 0.3f, 1f);       // 미연결(dangling)
        private static readonly Color BeltConnectedColor = new Color(0.35f, 0.9f, 0.4f, 1f);    // 자동연결됨
        private static readonly Color BeltWarningColor = new Color(0.98f, 0.45f, 0.25f, 1f);   // 끝단 미연결 경고
        private static readonly Color GridBgColor = new Color(0.11f, 0.13f, 0.17f, 0.55f);      // 설치 영역 배경
        private static readonly Color GridLineColor = new Color(0.40f, 0.85f, 0.60f, 0.35f);    // 셀 경계선
        private static readonly Color GridBorderColor = new Color(0.45f, 0.9f, 0.65f, 0.85f);   // 바깥 테두리
        private static readonly Color PanDimColor = new Color(0.03f, 0.05f, 0.08f, 0.55f);     // 이동 모드 흐림 막
        // 종류별 배색은 **아트 자체의 색**이다(V02 §1). 코드는 밝기만 곱한다.
        // 아트 투입 전 플레이스홀더는 흰 사각이라 세 단계가 흰색/회색/짙은 회색으로 나온다 —
        // 상태는 구분되고 종류는 아직 구분되지 않는 것이 맞는 상태다.
        private static readonly Color NodeBaseColor = Color.white;
        private static Sprite _unitSprite;

        /// <summary>
        /// 노드 표시색 = **기본색 × 산출률 밝기 배율**(UI 문서「노드 상태 표시」· V02 §1 개정).
        /// 기본색은 아트 자체의 색이고 코드는 밝기만 곱한다 — 한 노드에 색 축이 둘 겹치면
        /// 빨간 노드가 「정지」인지 「군수 노드」인지 구분되지 않아 진단 체계가 무너진다.
        /// 판정 규칙 자체는 NodeStatusTint(MBI.Core)에 있다 — UI는 판정 없이 매핑만.
        /// </summary>
        private static Color SeverityColor(float ratio)
        {
            float tint = NodeStatusTint.Of(ratio);
            Color c = NodeBaseColor * tint;
            c.a = NodeBaseColor.a; // 알파는 밝기 축이 아니다 — 곱하면 노드가 투명해진다
            return c;
        }

        /// <summary>노드 상태색 적용(§L4-R #5). 진단은 Provider(LogisticsDiagnostics)가 공급 — UI는 색 매핑만.
        /// 선택 중인 셀은 선택 하이라이트 유지.</summary>
        public void ApplyDiagnostics(IReadOnlyList<NodeDiagnostic> diags)
        {
            if (diags == null) return;
            foreach (NodeDiagnostic d in diags)
            {
                float ratio = d.targetRate > 0f ? d.actualRate / d.targetRate : 1f;
                Color c = SeverityColor(ratio);
                _nodeColors[d.cell] = c;
                if (_selected.HasValue && _selected.Value == d.cell) continue; // 선택 하이라이트 유지
                if (_markers.TryGetValue(d.cell, out GameObject m) && m != null)
                    m.GetComponent<SpriteRenderer>().color = c;
            }
        }

        /// <summary>진단 없음(코어 미배치 등) → 노드를 정상색으로.</summary>
        public void ClearDiagnostics()
        {
            foreach (KeyValuePair<Vector2Int, GameObject> kv in _markers)
            {
                if (kv.Value == null) continue;
                _nodeColors[kv.Key] = NodeBaseColor;
                if (_selected.HasValue && _selected.Value == kv.Key) continue;
                kv.Value.GetComponent<SpriteRenderer>().color = NodeBaseColor;
            }
        }

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError("[MBI] BoardController: BoardConfig 미할당 — 격자 생성 불가.");
                enabled = false;
                return;
            }
            if (boardCamera == null) boardCamera = Camera.main;

            Vector2 origin = ComputeOrigin(config, transform.position);
            // 실루엣 마스크: 팔·다리 사이 빈칸을 배치 불가로 만든다(조립 문서 11장, 유효 117칸).
            _grid = new BoardGrid(config.columns, config.rows, config.cellSize, origin,
                config.usePartLayout ? PartLayout.BuildMask() : null);

            _baseWorldPosition = transform.position;
            _pan = new BoardPan(
                new Vector2(config.columns * config.cellSize, config.rows * config.cellSize),
                new Vector2(viewSizeCells.x * config.cellSize, viewSizeCells.y * config.cellSize));

            BuildGridVisual(); // §C-4 설치 가능 그리드 영역 표시(런타임).
            BuildDimOverlay(); // 이동 모드 표시(UI 문서 9-2)
            ApplyModeVisual();
            ApplyInitialLayout();
        }

        // 시작 배치를 깐다. 배치 경로는 플레이어 조작과 동일(TryPlace + 마커) — 별도 경로를 만들지 않는다.
        private void ApplyInitialLayout()
        {
            if (initialLayout == null) return;
            foreach (InitialNode item in initialLayout)
            {
                if (item.node == null) continue;
                if (!_grid.TryPlace(item.cell, item.node, out NodeInstance placed)) continue;
                placed.AmmoKind = item.ammoKind; // 군수 노드가 만드는 탄종(§1). 다른 타입에서는 읽히지 않는다.
                SpawnNodeMarker(item.cell);
            }
            RefreshConnections();
        }

        // §C-4: 노드/벨트 설치 가능 영역을 런타임에 표시 — 배경 + 셀 경계선 + 바깥 테두리(최초 1회).
        private void BuildGridVisual()
        {
            Vector2 o = _grid.Origin;
            float w = config.columns * config.cellSize;
            float h = config.rows * config.cellSize;
            float cx = o.x + w * 0.5f, cy = o.y + h * 0.5f;

            var root = new GameObject("GridVisual");
            root.transform.SetParent(transform, false);
            float t = Mathf.Max(config.cellSize * 0.03f, 0.01f);
            float b = t * 1.8f;

            if (config.usePartLayout)
            {
                // 실루엣은 직사각형이 아니다 — 파츠 단위로 배경과 테두리를 그린다.
                // 팔·다리 사이 빈칸에는 아무것도 그리지 않아 "여기는 못 놓는다"가 그림으로 보인다.
                foreach (PartRect p in PartLayout.Parts)
                {
                    float pw = p.size.x * config.cellSize;
                    float ph = p.size.y * config.cellSize;
                    float px = o.x + (p.origin.x + p.size.x * 0.5f) * config.cellSize;
                    float py = o.y + (p.origin.y + p.size.y * 0.5f) * config.cellSize;

                    SpawnQuad(root.transform, px, py, pw, ph, GridBgColor, -3);

                    // 파츠 안쪽 셀선.
                    for (int x = 1; x < p.size.x; x++)
                        SpawnQuad(root.transform, o.x + (p.origin.x + x) * config.cellSize, py, t, ph, GridLineColor, -2);
                    for (int y = 1; y < p.size.y; y++)
                        SpawnQuad(root.transform, px, o.y + (p.origin.y + y) * config.cellSize, pw, t, GridLineColor, -2);

                    // 파츠 경계(진하게) — UI 문서 9-2 「조립 모드에서 격자와 파츠 경계가 또렷해진다」.
                    SpawnQuad(root.transform, px, py - ph * 0.5f, pw, b, GridBorderColor, -2);
                    SpawnQuad(root.transform, px, py + ph * 0.5f, pw, b, GridBorderColor, -2);
                    SpawnQuad(root.transform, px - pw * 0.5f, py, b, ph, GridBorderColor, -2);
                    SpawnQuad(root.transform, px + pw * 0.5f, py, b, ph, GridBorderColor, -2);
                }
                return;
            }

            // 마스크 없음 = 직사각 전체가 유효(구 동작).
            SpawnQuad(root.transform, cx, cy, w, h, GridBgColor, -3);
            for (int x = 0; x <= config.columns; x++)
                SpawnQuad(root.transform, o.x + x * config.cellSize, cy, t, h, GridLineColor, -2);
            for (int y = 0; y <= config.rows; y++)
                SpawnQuad(root.transform, cx, o.y + y * config.cellSize, w, t, GridLineColor, -2);

            SpawnQuad(root.transform, cx, o.y, w, b, GridBorderColor, -2);       // 하
            SpawnQuad(root.transform, cx, o.y + h, w, b, GridBorderColor, -2);   // 상
            SpawnQuad(root.transform, o.x, cy, b, h, GridBorderColor, -2);       // 좌
            SpawnQuad(root.transform, o.x + w, cy, b, h, GridBorderColor, -2);   // 우
        }

        // 이동 모드에서 보드를 덮는 반투명 막. 실루엣 전체를 덮되 노드보다 위에 그린다.
        private void BuildDimOverlay()
        {
            Vector2 o = _grid.Origin;
            float w = config.columns * config.cellSize;
            float h = config.rows * config.cellSize;

            _dimOverlay = new GameObject("PanDim");
            _dimOverlay.transform.SetParent(transform, false);
            _dimOverlay.transform.position = new Vector3(o.x + w * 0.5f, o.y + h * 0.5f, 0f);
            _dimOverlay.transform.localScale = new Vector3(w, h, 1f);

            var sr = _dimOverlay.AddComponent<SpriteRenderer>();
            sr.sprite = UnitSprite();
            sr.color = PanDimColor;
            sr.sortingOrder = SortingLayers.Hud; // 이동 모드 흐림 막 — 보드 요소 전부보다 위
        }

        // 중심(cx,cy)·크기(w,h)의 단색 사각 스프라이트 하나.
        private void SpawnQuad(Transform parent, float cx, float cy, float w, float h, Color col, int order)
        {
            var g = new GameObject("q");
            g.transform.SetParent(parent, false);
            g.transform.position = new Vector3(cx, cy, 0f);
            g.transform.localScale = new Vector3(w, h, 1f);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = UnitSprite();
            sr.color = col;
            sr.sortingOrder = order;
        }

        private void OnEnable()
        {
            _press = new InputAction("BoardPress", InputActionType.Button, "<Pointer>/press");
            _press.started += OnPressStart;
            _press.canceled += OnPressEnd;
            _press.Enable();
        }

        private void OnDisable()
        {
            if (_press == null) return;
            _press.started -= OnPressStart;
            _press.canceled -= OnPressEnd;
            _press.Disable();
            _press.Dispose();
            _press = null;
        }

        private void OnPressStart(InputAction.CallbackContext ctx)
        {
            if (_grid == null) return;
            // 레이어/팔레트 버튼 위 클릭, 또는 조립 뷰가 아닐 때는 보드 무시(오배치 방지).
            if (GameLayerController.PointerOverButton || _pointerOverPalette) return;

            // 이동 모드: 같은 드래그가 스크롤이 된다(UI 문서 9-1 제스처 충돌 해소).
            if (Mode == BoardMode.Pan)
            {
                if (!TryPointerWorld(out Vector2 world)) return;
                _panning = true;
                _panLastWorld = world;
                return;
            }

            if (!TryCellUnderPointer(out Vector2Int cell) || !_grid.IsInside(cell)) return;
            _dragging = true;
            _dragCells.Clear();
            _dragCells.Add(cell);
        }

        private void Update()
        {
            if (_panning)
            {
                if (TryPointerWorld(out Vector2 world))
                {
                    // 손가락을 끈 만큼 보드가 따라온다. 카메라를 옮기지 않고 보드를 옮기는 이유는
                    // 전투 화면이 같은 카메라에 상단 30%로 병존하기 때문이다(UI 문서 9-5).
                    _pan.Drag(world - _panLastWorld);
                    ApplyPan();
                    _panLastWorld = world;
                }
                return;
            }

            if (!_dragging || _grid == null) return;
            if (!TryCellUnderPointer(out Vector2Int cell) || !_grid.IsInside(cell)) return;

            Vector2Int last = _dragCells[_dragCells.Count - 1];
            if (cell == last) return;
            // 직교 인접만 누적(빠른 드래그로 건너뛴 칸은 무시 — MVP).
            if (Mathf.Abs(cell.x - last.x) + Mathf.Abs(cell.y - last.y) == 1)
                _dragCells.Add(cell);
        }

        private void OnPressEnd(InputAction.CallbackContext ctx)
        {
            if (_panning) { _panning = false; return; }
            if (!_dragging) return;
            _dragging = false;

            if (_dragCells.Count == 1)
            {
                Vector2Int cell = _dragCells[0];
                if (_removeMode) RemoveAt(cell);         // 제거 모드 = 탭으로 삭제
                else if (_grid.IsOccupied(cell)) Select(cell);
                else Place(cell);
            }
            else if (_dragCells.Count > 1)
            {
                LayBelts(_dragCells);
            }
            _dragCells.Clear();
        }

        // 드래그 경로 → 벨트 세그먼트 설치(§5-4). 점유(노드/기존벨트) 셀은 건너뜀.
        private void LayBelts(List<Vector2Int> cells)
        {
            List<BeltSegmentSpec> segs = BeltPath.Build(cells);
            int placed = 0;
            foreach (BeltSegmentSpec s in segs)
            {
                if (!_grid.TryPlaceBelt(s.cell, s.inFace, s.outFace, FlowKind.Material, out _)) continue;
                SpawnBeltMarker(s.cell, s.outFace);
                placed++;
            }
            RefreshConnections();
            Debug.Log($"[MBI] 벨트 설치: 드래그 {cells.Count}칸 → 세그먼트 {segs.Count}, 신규 배치 {placed}.");
        }

        private bool TryCellUnderPointer(out Vector2Int cell)
        {
            cell = default;
            if (!TryPointerWorld(out Vector2 world)) return false;
            // 스크롤한 만큼 보드가 밀려 있으므로 되돌린 뒤 셀을 구한다 —
            // 안 빼면 스크롤 후 탭이 엉뚱한 칸에 꽂힌다.
            cell = _grid.WorldToCell(world - PanOffset);
            return true;
        }

        /// <summary>포인터의 월드 좌표(보드 평면 z=0).</summary>
        private bool TryPointerWorld(out Vector2 world)
        {
            world = default;
            if (Pointer.current == null) return false;
            if (boardCamera == null) boardCamera = Camera.main;
            if (boardCamera == null) return false;

            Vector2 screen = Pointer.current.position.ReadValue();
            // orthographic: z = 카메라→보드 평면(z=0) 거리 = -카메라 z.
            Vector3 w = boardCamera.ScreenToWorldPoint(
                new Vector3(screen.x, screen.y, -boardCamera.transform.position.z));
            world = new Vector2(w.x, w.y);
            return true;
        }

        private Vector2 PanOffset => _pan != null ? _pan.Offset : Vector2.zero;

        /// <summary>셀 중심의 화면상 월드 좌표(스크롤 반영). 마커 배치는 전부 이걸 쓴다.</summary>
        private Vector3 CellWorld(Vector2Int cell) => (Vector3)(Vector2)_grid.CellToWorld(cell) + (Vector3)PanOffset;

        // 보드 전체를 스크롤한다. 카메라를 옮기지 않는 이유는 같은 카메라에 전투 화면이
        // 상단 30%로 병존하기 때문이다(UI 문서 9-5 연속성 원칙).
        private void ApplyPan()
        {
            transform.position = _baseWorldPosition + (Vector3)_pan.Offset;
        }

        // 이동 모드에서는 보드를 흐리게, 조립 모드에서는 또렷하게(UI 문서 9-2).
        // 색이나 문구가 아니라 보드 자체의 선명도로 지금 무엇을 할 수 있는지 알린다.
        //
        // 렌더러마다 알파를 건드리지 않고 반투명 막 하나를 덮는다 — 마커는 배치할 때마다 새로 생기므로
        // 개별 알파를 추적하면 원래 값이 어긋나고, 상태색(진단)까지 흐려져 판독이 망가진다.
        private void ApplyModeVisual()
        {
            if (_dimOverlay == null) return;
            _dimOverlay.SetActive(_mode == BoardMode.Pan);
        }

        /// <summary>현재 팔레트에서 선택된 배치 노드(비었으면 placeTarget 폴백).</summary>
        private NodeDefinition CurrentNode()
        {
            if (palette != null && palette.Count > 0)
                return palette[Mathf.Clamp(_selectedNode, 0, palette.Count - 1)];
            return placeTarget;
        }

        private void Place(Vector2Int cell)
        {
            NodeDefinition node = CurrentNode();
            if (node == null)
            {
                Debug.LogWarning("[MBI] BoardController: 배치할 노드 없음(팔레트/placeTarget 미할당).");
                return;
            }
            if (!_grid.TryPlace(cell, node, out _)) return;

            SpawnNodeMarker(cell);
            RefreshConnections(); // 노드 추가로 인접 벨트 연결 상태 변화 반영.
            Debug.Log($"[MBI] 배치: {node.displayName} @ 셀({cell.x},{cell.y}) → 월드 {_grid.CellToWorld(cell)}.");
        }

        // 노드 마커 스폰(플레이어 배치·시작 배치 공용).
        private void SpawnNodeMarker(Vector2Int cell)
        {
            var marker = new GameObject($"Node_{cell.x}_{cell.y}");
            marker.transform.SetParent(transform, false);
            marker.transform.position = CellWorld(cell);
            // 한 칸 가득. 노드 타일 아트가 192px = 정확히 한 칸이므로(ArtSpec, V02 §4)
            // 플레이스홀더도 같은 자리를 차지해야 교체 때 밀도가 안 바뀐다. 칸 경계는 격자선이 그린다.
            marker.transform.localScale = Vector3.one * (_grid.CellSize * ArtSpec.TileSize);
            var sr = marker.AddComponent<SpriteRenderer>();
            sr.sprite = UnitSprite();
            Color c = SeverityColor(1f); // 초기 = 정상(초록). Provider가 라이브 진단으로 갱신(§L4-R #5).
            sr.color = c;
            _markers[cell] = marker;
            _nodeColors[cell] = c;
        }

        // 배치된 노드/벨트 제거(§5-4 제거 모드).
        private void RemoveAt(Vector2Int cell)
        {
            if (_grid.IsOccupied(cell))
            {
                _grid.TryRemove(cell);
                if (_markers.TryGetValue(cell, out GameObject m) && m != null) Destroy(m);
                _markers.Remove(cell);
                _nodeColors.Remove(cell);
                if (_selected.HasValue && _selected.Value == cell) _selected = null;
            }
            else if (_grid.HasBelt(cell))
            {
                _grid.TryRemoveBelt(cell);
                if (_beltMarkers.TryGetValue(cell, out GameObject bm) && bm != null) Destroy(bm);
                _beltMarkers.Remove(cell);
                _beltArrows.Remove(cell);
                _beltWarnings.Remove(cell); // 아이콘 GameObject는 마커의 자식이라 위 Destroy로 함께 사라짐
            }
            else return;

            RefreshConnections();
            Debug.Log($"[MBI] 제거 @ 셀({cell.x},{cell.y}).");
        }

        // 조립 뷰에서만 노드 팔레트(우측 세로 버튼) — 선택으로 탭 배치 노드 변경 + 제거 모드.
        private void OnGUI()
        {
            _pointerOverPalette = false;
            if (!GameLayerController.BoardViewActive) return;

            DrawModeButton();
            DrawMiniMap();

            if (palette == null || palette.Count == 0) return;

            var style = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            const float w = 130f, h = 34f, pad = 6f;
            float x = Screen.width - w - 12f;
            float y0 = 90f;

            // 이동 모드에서는 팔레트를 흐리게 — 지금은 놓을 수 없다는 것을 버튼 상태로 알린다.
            GUI.enabled = _mode == BoardMode.Build;

            GUI.Label(new Rect(x, y0 - 24f, w, 22f), "노드 팔레트");
            int i;
            for (i = 0; i < palette.Count; i++)
            {
                if (palette[i] == null) continue;
                var rect = new Rect(x, y0 + i * (h + pad), w, h);
                if (rect.Contains(Event.current.mousePosition)) _pointerOverPalette = true;

                bool sel = !_removeMode && i == _selectedNode;
                if (GUI.Button(rect, (sel ? "● " : "") + palette[i].displayName, style))
                {
                    _selectedNode = i;
                    _removeMode = false;
                }
            }

            // 제거 토글.
            var rmRect = new Rect(x, y0 + i * (h + pad) + 4f, w, h);
            if (rmRect.Contains(Event.current.mousePosition)) _pointerOverPalette = true;
            if (GUI.Button(rmRect, (_removeMode ? "● " : "") + "제거", style)) _removeMode = !_removeMode;

            GUI.Label(new Rect(x, y0 + (i + 1) * (h + pad) + 8f, w, 56f),
                _removeMode ? "제거 모드\n탭=노드/벨트 삭제" : "탭=노드 배치\n드래그=벨트");

            GUI.enabled = true;
        }

        // 모드 버튼 — 화면 우측 하단 1개(UI 문서 9-2).
        // **버튼이 표시를 겸한다.** 문구가 현재 모드를 그대로 나타내므로 별도 모드 표시를 두지 않는다.
        // 모드를 바꾸는 곳과 확인하는 곳이 같은 자리가 되고, 화면 요소도 하나 아낀다.
        private void DrawModeButton()
        {
            var style = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            var rect = new Rect(Screen.width - 152f, Screen.height - 78f, 140f, 46f);
            if (rect.Contains(Event.current.mousePosition)) _pointerOverPalette = true;

            string label = _mode == BoardMode.Pan ? "이동 모드" : "조립 모드";
            if (GUI.Button(rect, label, style)) ToggleMode();
        }

        // 미니맵 — 부유 요소 띠 좌측(UI 문서 2장). 실루엣 전체 + 현재 보고 있는 범위.
        // 보드가 화면 밖으로 나가는 것은 허용된 설계이므로, 지금 어디를 보는지는 이것이 알린다(9-3).
        private void DrawMiniMap()
        {
            if (_pan == null || config == null) return;

            const float mapW = 96f;
            float mapH = mapW * config.rows / Mathf.Max(1, config.columns);
            var box = new Rect(12f, Screen.height - mapH - 32f, mapW, mapH);
            if (box.Contains(Event.current.mousePosition)) _pointerOverPalette = true;

            GUI.Box(box, GUIContent.none);

            // 유효 셀만 찍어 실루엣 형태가 드러나게 한다 — 직사각형을 그리면 못 놓는 칸이 감춰진다.
            float cw = box.width / config.columns, ch = box.height / config.rows;
            foreach (PartRect p in PartLayout.Parts)
            {
                var r = new Rect(
                    box.x + p.origin.x * cw,
                    box.y + box.height - (p.origin.y + p.size.y) * ch, // GUI는 y가 아래로 증가
                    p.size.x * cw, p.size.y * ch);
                GUI.Box(r, GUIContent.none);
            }

            // 현재 뷰포트 위치.
            Vector2 c01 = _pan.ViewportCenter01;
            float vw = Mathf.Clamp01(viewSizeCells.x / config.columns) * box.width;
            float vh = Mathf.Clamp01(viewSizeCells.y / config.rows) * box.height;
            var view = new Rect(
                box.x + (box.width - vw) * c01.x,
                box.y + (box.height - vh) * (1f - c01.y),
                vw, vh);

            Color prev = GUI.color;
            GUI.color = new Color(0.98f, 0.85f, 0.30f, 0.5f);
            GUI.Box(view, GUIContent.none);
            GUI.color = prev;
        }

        private void Select(Vector2Int cell)
        {
            // 이전 선택 색 복원(현재 상태색으로, §L4-R #5).
            if (_selected.HasValue && _markers.TryGetValue(_selected.Value, out GameObject prev) && prev != null)
            {
                prev.GetComponent<SpriteRenderer>().color =
                    _nodeColors.TryGetValue(_selected.Value, out Color pc) ? pc : NodeBaseColor;
            }

            _selected = cell;
            if (_markers.TryGetValue(cell, out GameObject cur) && cur != null)
                cur.GetComponent<SpriteRenderer>().color = SelectedColor;

            NodeInstance inst = _grid.GetAt(cell);
            Debug.Log($"[MBI] 선택: {(inst != null ? inst.Definition.displayName : "?")} @ 셀({cell.x},{cell.y}).");
        }

        /// <summary>격자 좌하단 코너 월드 좌표 = 보드 위치 중심 정렬(파생값).</summary>
        private static Vector2 ComputeOrigin(BoardConfig cfg, Vector3 boardPos)
        {
            return new Vector2(boardPos.x, boardPos.y)
                   - new Vector2(cfg.columns * cfg.cellSize, cfg.rows * cfg.cellSize) * 0.5f;
        }

        // 벨트 마커: 회색 셀 사각 + outFace 쪽 밝은 방향 표시(플레이스홀더).
        private void SpawnBeltMarker(Vector2Int cell, PortFace outFace)
        {
            var m = new GameObject($"Belt_{cell.x}_{cell.y}");
            m.transform.SetParent(transform, false);
            m.transform.position = CellWorld(cell);
            m.transform.localScale = Vector3.one * (_grid.CellSize * 0.85f);
            var sr = m.AddComponent<SpriteRenderer>();
            sr.sprite = UnitSprite();
            sr.color = BeltColor;

            var arrow = new GameObject("dir");
            arrow.transform.SetParent(m.transform, false);
            Vector2 off = FaceOffset(outFace);
            arrow.transform.localPosition = new Vector3(off.x * 0.32f, off.y * 0.32f, 0f);
            arrow.transform.localScale = new Vector3(0.34f, 0.34f, 1f);
            var asr = arrow.AddComponent<SpriteRenderer>();
            asr.sprite = UnitSprite();
            asr.color = BeltArrowColor;
            asr.sortingOrder = SortingLayers.Tile + 1; // 벨트 방향 표시 — 타일 층 안

            // 끝단 미연결 경고(§5-4 ⑤): 셀 위쪽 모서리에 작은 표식. 기본 off — RefreshConnections가 켠다.
            var warn = new GameObject("warn");
            warn.transform.SetParent(m.transform, false);
            warn.transform.localPosition = new Vector3(0f, 0.30f, 0f);
            warn.transform.localScale = new Vector3(0.26f, 0.26f, 1f);
            var wsr = warn.AddComponent<SpriteRenderer>();
            wsr.sprite = UnitSprite();
            wsr.color = BeltWarningColor;
            wsr.sortingOrder = SortingLayers.Tile + 2; // 미연결 경고 아이콘
            wsr.enabled = false;

            _beltArrows[cell] = asr;
            _beltWarnings[cell] = wsr;
            _beltMarkers[cell] = m;
        }

        // §5-4 L2: 배치 후 연결 그래프 재계산 → 벨트 방향 표시 색(연결=초록/미연결=노랑) + 끝단 경고(⑤).
        // 설치 확정 시점(설치·배치·제거)에만 호출된다 → 드래그 중에는 판정하지 않는다는 사양이 자동 충족.
        private void RefreshConnections()
        {
            List<BeltLink> links = BeltRouting.BuildLinks(_grid);
            var connected = new HashSet<Vector2Int>();
            foreach (BeltLink l in links)
            {
                // 양방향으로 센다. fromCell만 담으면 입력이 없는 시작단이 초록으로 잘못 표시된다.
                connected.Add(l.fromCell);
                connected.Add(l.toCell);
            }

            foreach (KeyValuePair<Vector2Int, SpriteRenderer> kv in _beltArrows)
                if (kv.Value != null)
                    kv.Value.color = connected.Contains(kv.Key) ? BeltConnectedColor : BeltArrowColor;

            // 판정은 전부 Core(BeltRouting) — 여기서는 켜고 끄기만 한다(§3 UI는 매핑만).
            var warn = new HashSet<Vector2Int>(BeltRouting.DanglingWarningCells(_grid));
            foreach (KeyValuePair<Vector2Int, SpriteRenderer> kv in _beltWarnings)
                if (kv.Value != null)
                    kv.Value.enabled = warn.Contains(kv.Key);
        }

        private static Vector2 FaceOffset(PortFace face)
        {
            switch (face)
            {
                case PortFace.East: return new Vector2(1f, 0f);
                case PortFace.West: return new Vector2(-1f, 0f);
                case PortFace.North: return new Vector2(0f, 1f);
                default: return new Vector2(0f, -1f); // South
            }
        }

        private static Sprite UnitSprite()
        {
            if (_unitSprite != null) return _unitSprite;
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            // ppu=1 → 1×1 텍스처가 1 월드 유닛(스케일로 cellSize 반영).
            _unitSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _unitSprite;
        }

        // 씬 뷰 격자 시각화(아트 없이 스냅/치수 확인).
        private void OnDrawGizmos()
        {
            if (config == null) return;
            Vector2 origin = ComputeOrigin(config, transform.position);
            float w = config.columns * config.cellSize;
            float h = config.rows * config.cellSize;

            Gizmos.color = new Color(0.4f, 0.9f, 0.6f, 0.5f);
            for (int x = 0; x <= config.columns; x++)
            {
                float px = origin.x + x * config.cellSize;
                Gizmos.DrawLine(new Vector3(px, origin.y, 0f), new Vector3(px, origin.y + h, 0f));
            }
            for (int y = 0; y <= config.rows; y++)
            {
                float py = origin.y + y * config.cellSize;
                Gizmos.DrawLine(new Vector3(origin.x, py, 0f), new Vector3(origin.x + w, py, 0f));
            }
        }
    }
}
