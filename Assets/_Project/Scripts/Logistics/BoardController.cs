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
        private static readonly Color StatusNormal = new Color(0.40f, 0.85f, 0.50f);  // 초록 = 정상(산출률 1.0)
        private static readonly Color StatusSlow = new Color(0.95f, 0.80f, 0.25f);    // 노랑 = 감속·유휴(0<산출률<1)
        private static readonly Color StatusStopped = new Color(0.90f, 0.30f, 0.30f); // 빨강 = 정지(산출률 0)
        private static Sprite _unitSprite;

        /// <summary>
        /// 노드 상태색(§L4-R #5 — C-③ 타입색 대체). 색 = 산출률(actualRate/targetRate):
        /// 초록=1.0(정상) / 노랑=0<x<1(감속·유휴) / 빨강=0(정지). 4번째 색 없음(모듈 과부하는 MVP 밖·문서 이월).
        /// UI 문서 13 §3-4-1이 원천 — UI는 판정 없이 매핑만.
        /// </summary>
        private static Color SeverityColor(float ratio)
        {
            if (ratio <= 0.0001f) return StatusStopped;   // 완전 정지
            if (ratio < 0.999f) return StatusSlow;        // 깎여서 돌아감
            return StatusNormal;                          // 설계대로
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
                _nodeColors[kv.Key] = StatusNormal;
                if (_selected.HasValue && _selected.Value == kv.Key) continue;
                kv.Value.GetComponent<SpriteRenderer>().color = StatusNormal;
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

            SpawnNodeMarker(cell);
            RefreshConnections(); // 노드 추가로 인접 벨트 연결 상태 변화 반영.
            Debug.Log($"[MBI] 배치: {node.displayName} @ 셀({cell.x},{cell.y}) → 월드 {_grid.CellToWorld(cell)}.");
        }

        // 노드 마커 스폰(플레이어 배치·시작 배치 공용).
        private void SpawnNodeMarker(Vector2Int cell)
        {
            var marker = new GameObject($"Node_{cell.x}_{cell.y}");
            marker.transform.SetParent(transform, false);
            marker.transform.position = _grid.CellToWorld(cell);
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
            // 이전 선택 색 복원(현재 상태색으로, §L4-R #5).
            if (_selected.HasValue && _markers.TryGetValue(_selected.Value, out GameObject prev) && prev != null)
            {
                prev.GetComponent<SpriteRenderer>().color =
                    _nodeColors.TryGetValue(_selected.Value, out Color pc) ? pc : StatusNormal;
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

            // 끝단 미연결 경고(§5-4 ⑤): 셀 위쪽 모서리에 작은 표식. 기본 off — RefreshConnections가 켠다.
            var warn = new GameObject("warn");
            warn.transform.SetParent(m.transform, false);
            warn.transform.localPosition = new Vector3(0f, 0.30f, 0f);
            warn.transform.localScale = new Vector3(0.26f, 0.26f, 1f);
            var wsr = warn.AddComponent<SpriteRenderer>();
            wsr.sprite = UnitSprite();
            wsr.color = BeltWarningColor;
            wsr.sortingOrder = 2;
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
