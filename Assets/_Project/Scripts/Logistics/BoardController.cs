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
        [Tooltip("탭으로 배치할 노드(당장 1종). 팔레트 UI는 §8.")]
        [SerializeField] private NodeDefinition placeTarget;
        [Tooltip("좌표 변환 카메라. 비우면 Camera.main.")]
        [SerializeField] private Camera boardCamera;

        private BoardGrid _grid;
        private InputAction _press;
        private readonly Dictionary<Vector2Int, GameObject> _markers = new Dictionary<Vector2Int, GameObject>();
        private Vector2Int? _selected;

        // 드래그 설치(§5-4 L1b): press→drag(경로 셀 누적)→release.
        private bool _dragging;
        private readonly List<Vector2Int> _dragCells = new List<Vector2Int>();

        // 벨트 방향 표시 SR(§5-4 L2 연결 상태 색 갱신용).
        private readonly Dictionary<Vector2Int, SpriteRenderer> _beltArrows = new Dictionary<Vector2Int, SpriteRenderer>();

        private static readonly Color PlacedColor = new Color(0.55f, 0.75f, 0.95f, 1f);
        private static readonly Color SelectedColor = new Color(0.98f, 0.85f, 0.30f, 1f);
        private static readonly Color BeltColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color BeltArrowColor = new Color(0.95f, 0.85f, 0.3f, 1f);       // 미연결(dangling)
        private static readonly Color BeltConnectedColor = new Color(0.35f, 0.9f, 0.4f, 1f);    // 자동연결됨
        private static Sprite _unitSprite;

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
            if (GameLayerController.PointerOverButton) return; // 레이어 버튼 위 클릭은 보드 무시(오배치 방지).
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
                // 제자리 탭 = 노드 배치 / 선택(기존 동작).
                Vector2Int cell = _dragCells[0];
                if (_grid.IsOccupied(cell)) Select(cell);
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

        private void Place(Vector2Int cell)
        {
            if (placeTarget == null)
            {
                Debug.LogWarning("[MBI] BoardController: placeTarget 미할당 — 배치할 노드 없음.");
                return;
            }
            if (!_grid.TryPlace(cell, placeTarget, out _)) return;

            GameObject marker = new GameObject($"Node_{cell.x}_{cell.y}");
            marker.transform.SetParent(transform, false);
            marker.transform.position = _grid.CellToWorld(cell);
            marker.transform.localScale = Vector3.one * (_grid.CellSize * 0.9f);
            var sr = marker.AddComponent<SpriteRenderer>();
            sr.sprite = UnitSprite();
            sr.color = PlacedColor;
            _markers[cell] = marker;

            RefreshConnections(); // 노드 추가로 인접 벨트 연결 상태 변화 반영.
            Debug.Log($"[MBI] 배치: {placeTarget.displayName} @ 셀({cell.x},{cell.y}) → 월드 {_grid.CellToWorld(cell)}.");
        }

        private void Select(Vector2Int cell)
        {
            // 이전 선택 색 복원.
            if (_selected.HasValue && _markers.TryGetValue(_selected.Value, out GameObject prev) && prev != null)
                prev.GetComponent<SpriteRenderer>().color = PlacedColor;

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
