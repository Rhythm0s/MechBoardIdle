using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MBI.Logistics
{
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

        private int _selectedNode; // 팔레트에서 선택된 노드 인덱스
        private bool _pointerOverPalette; // 팔레트 버튼 위 클릭은 보드 무시

        /// <summary>배치 상태 격자(§5-5 출력 집계용). Awake 후 유효.</summary>
        public BoardGrid Grid => _grid;

        private BoardGrid _grid;
        private InputAction _press;
        private readonly Dictionary<Vector2Int, GameObject> _markers = new Dictionary<Vector2Int, GameObject>();
        private Vector2Int? _selected;

        // 드래그 설치(§5-4 L1b): press→drag(경로 셀 누적)→release.
        private bool _dragging;
        private readonly List<Vector2Int> _dragCells = new List<Vector2Int>();

        // 벨트 마커 루트 + 방향 표시 SR(§5-4 L2 연결 색/제거용).
        private readonly Dictionary<Vector2Int, GameObject> _beltMarkers = new Dictionary<Vector2Int, GameObject>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> _beltArrows = new Dictionary<Vector2Int, SpriteRenderer>();

        private bool _removeMode; // 제거 모드 — 탭으로 노드/벨트 삭제

        private static readonly Color PlacedColor = new Color(0.55f, 0.75f, 0.95f, 1f);
        private static readonly Color SelectedColor = new Color(0.98f, 0.85f, 0.30f, 1f);
        private static readonly Color BeltColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color BeltArrowColor = new Color(0.95f, 0.85f, 0.3f, 1f);       // 미연결(dangling)
        private static readonly Color BeltConnectedColor = new Color(0.35f, 0.9f, 0.4f, 1f);    // 자동연결됨
        private static readonly Color GridBgColor = new Color(0.11f, 0.13f, 0.17f, 0.55f);      // 설치 영역 배경
        private static readonly Color GridLineColor = new Color(0.40f, 0.85f, 0.60f, 0.35f);    // 셀 경계선
        private static readonly Color GridBorderColor = new Color(0.45f, 0.9f, 0.65f, 0.85f);   // 바깥 테두리
        private static Sprite _unitSprite;

        /// <summary>노드 타입별 색(§C-3). 스텁(쉴드)은 회색.</summary>
        private static Color NodeTypeColor(NodeType t)
        {
            switch (t)
            {
                case NodeType.Core: return new Color(0.95f, 0.45f, 0.35f);        // 코어 = 주홍
                case NodeType.Processing: return new Color(0.72f, 0.50f, 0.95f);  // 가공 = 보라
                case NodeType.Munitions: return new Color(0.95f, 0.62f, 0.20f);   // 군수 = 주황
                case NodeType.Energy: return new Color(0.40f, 0.85f, 0.50f);      // 에너지 = 초록
                case NodeType.Storage: return new Color(0.40f, 0.70f, 0.95f);     // 저장 = 파랑
                default: return new Color(0.60f, 0.60f, 0.62f);                   // 쉴드(스텁) = 회색
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
            _grid = new BoardGrid(config.columns, config.rows, config.cellSize, origin);

            BuildGridVisual(); // §C-4 설치 가능 그리드 영역 표시(런타임).
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

            // 배경 패널(설치 영역).
            SpawnQuad(root.transform, cx, cy, w, h, GridBgColor, -3);

            // 셀 경계선(세로/가로).
            float t = Mathf.Max(config.cellSize * 0.03f, 0.01f);
            for (int x = 0; x <= config.columns; x++)
                SpawnQuad(root.transform, o.x + x * config.cellSize, cy, t, h, GridLineColor, -2);
            for (int y = 0; y <= config.rows; y++)
                SpawnQuad(root.transform, cx, o.y + y * config.cellSize, w, t, GridLineColor, -2);

            // 바깥 테두리(진하게) — 상/하/좌/우.
            float b = t * 1.8f;
            SpawnQuad(root.transform, cx, o.y, w, b, GridBorderColor, -2);       // 하
            SpawnQuad(root.transform, cx, o.y + h, w, b, GridBorderColor, -2);   // 상
            SpawnQuad(root.transform, o.x, cy, b, h, GridBorderColor, -2);       // 좌
            SpawnQuad(root.transform, o.x + w, cy, b, h, GridBorderColor, -2);   // 우
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
            if (!TryCellUnderPointer(out Vector2Int cell) || !_grid.IsInside(cell)) return;
            _dragging = true;
            _dragCells.Clear();
            _dragCells.Add(cell);
        }

        private void Update()
        {
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
            if (Pointer.current == null) return false;
            if (boardCamera == null) boardCamera = Camera.main;
            if (boardCamera == null) return false;

            Vector2 screen = Pointer.current.position.ReadValue();
            // orthographic: z = 카메라→보드 평면(z=0) 거리 = -카메라 z.
            Vector3 world = boardCamera.ScreenToWorldPoint(
                new Vector3(screen.x, screen.y, -boardCamera.transform.position.z));
            cell = _grid.WorldToCell(world);
            return true;
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

            GameObject marker = new GameObject($"Node_{cell.x}_{cell.y}");
            marker.transform.SetParent(transform, false);
            marker.transform.position = _grid.CellToWorld(cell);
            marker.transform.localScale = Vector3.one * (_grid.CellSize * 0.9f);
            var sr = marker.AddComponent<SpriteRenderer>();
            sr.sprite = UnitSprite();
            sr.color = NodeTypeColor(node.type); // §C-3 노드 타입별 색
            _markers[cell] = marker;

            RefreshConnections(); // 노드 추가로 인접 벨트 연결 상태 변화 반영.
            Debug.Log($"[MBI] 배치: {node.displayName} @ 셀({cell.x},{cell.y}) → 월드 {_grid.CellToWorld(cell)}.");
        }

        // 배치된 노드/벨트 제거(§5-4 제거 모드).
        private void RemoveAt(Vector2Int cell)
        {
            if (_grid.IsOccupied(cell))
            {
                _grid.TryRemove(cell);
                if (_markers.TryGetValue(cell, out GameObject m) && m != null) Destroy(m);
                _markers.Remove(cell);
                if (_selected.HasValue && _selected.Value == cell) _selected = null;
            }
            else if (_grid.HasBelt(cell))
            {
                _grid.TryRemoveBelt(cell);
                if (_beltMarkers.TryGetValue(cell, out GameObject bm) && bm != null) Destroy(bm);
                _beltMarkers.Remove(cell);
                _beltArrows.Remove(cell);
            }
            else return;

            RefreshConnections();
            Debug.Log($"[MBI] 제거 @ 셀({cell.x},{cell.y}).");
        }

        // 조립 뷰에서만 노드 팔레트(우측 세로 버튼) — 선택으로 탭 배치 노드 변경 + 제거 모드.
        private void OnGUI()
        {
            _pointerOverPalette = false;
            if (!GameLayerController.BoardViewActive || palette == null || palette.Count == 0) return;

            var style = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            const float w = 130f, h = 34f, pad = 6f;
            float x = Screen.width - w - 12f;
            float y0 = 90f;

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
        }

        private void Select(Vector2Int cell)
        {
            // 이전 선택 색 복원(해당 노드 타입 색으로, §C-3).
            if (_selected.HasValue && _markers.TryGetValue(_selected.Value, out GameObject prev) && prev != null)
            {
                NodeInstance prevInst = _grid.GetAt(_selected.Value);
                prev.GetComponent<SpriteRenderer>().color =
                    prevInst != null ? NodeTypeColor(prevInst.Definition.type) : PlacedColor;
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
            m.transform.position = _grid.CellToWorld(cell);
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
            asr.sortingOrder = 1;
            _beltArrows[cell] = asr;
            _beltMarkers[cell] = m;
        }

        // §5-4 L2: 배치 후 연결 그래프 재계산 → 벨트 방향 표시 색(연결=초록/미연결=노랑).
        private void RefreshConnections()
        {
            List<BeltLink> links = BeltRouting.BuildLinks(_grid);
            var connected = new HashSet<Vector2Int>();
            foreach (BeltLink l in links) connected.Add(l.fromCell);

            foreach (KeyValuePair<Vector2Int, SpriteRenderer> kv in _beltArrows)
                if (kv.Value != null)
                    kv.Value.color = connected.Contains(kv.Key) ? BeltConnectedColor : BeltArrowColor;
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
