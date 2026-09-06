using MBI.UI;
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
    /// 시작 벨트 1칸. 연결성이 출력의 조건이 된 뒤로(260829_V03 §판정① A안)
    /// 노드만 깔아 두면 출력이 0이라 「게임이 고장난 것처럼」 보인다 — 배선도 함께 준다.
    /// </summary>
    [System.Serializable]
    public struct InitialBelt
    {
        public Vector2Int cell;
        public PortFace inFace;
        public PortFace outFace;
        /// <summary>병합기·분류기면 true. 면은 이웃에서 다시 잡히므로 위 두 면은 무시된다.</summary>
        public bool merger;
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
        [Tooltip("시작 배치(온보딩). 빈 보드로 시작하면 플레이어가 무엇을 해야 할지 알 수 없다 — 거의 완성된 라인을 주고 한 칸만 비워 둔다(튜토리얼 10장 '한쪽 팔만 비움'을 보드에 적용).")]
        [SerializeField] private List<InitialNode> initialLayout = new List<InitialNode>();
        [Tooltip("시작 배선. 노드만 있고 벨트가 없으면 연결성 게이트에 걸려 출력이 0이다.")]
        [SerializeField] private List<InitialBelt> initialBelts = new List<InitialBelt>();

        private int _selectedNode; // 팔레트에서 선택된 노드 인덱스
        /// <summary>
        /// 이번 프레임 OnGUI가 그린 **버튼 자리들**. 누른 순간 포인터가 여기 있으면 보드는 무시한다.
        ///
        /// ⚠️ 종전에는 OnGUI에서 `bool` 하나를 세워 두고 눌릴 때 그 값을 읽었다. 그런데 입력
        /// 콜백은 그 프레임의 OnGUI **앞**에서 돈다 — 읽는 값이 항상 한 프레임 전 것이다.
        /// 마우스는 커서가 이미 그 자리에 얹혀 있었으니 맞았지만, **터치에는 얹혀 있는 시간이
        /// 없다.** 손가락이 닿는 첫 프레임에는 값이 false라서, 팔레트 버튼을 눌러도 그 아래
        /// 보드까지 같이 눌렸다. 배포가 WebGL이고 조작은 터치 기준이라 실기에서 늘 그랬다.
        ///
        /// 그래서 값이 아니라 **자리**를 남기고, 판정은 누르는 순간의 포인터로 한다.
        /// </summary>
        private readonly List<Rect> _uiRects = new List<Rect>();

        /// <summary>배치 상태 격자(§5-5 출력 집계용). Awake 후 유효.</summary>
        public BoardGrid Grid => _grid;

        /// <summary>
        /// 벨트 위 개별 아이템 (2026-09-04 배선 · `260904_W01` 6장).
        ///
        /// **보드가 소유한다.** 도는 것은 <see cref="LogisticsOutputProvider"/>이고 그리는 것은
        /// 벨트 흐름 애니메이션이라, 둘이 같은 객체를 봐야 화면과 숫자가 갈리지 않는다.
        /// </summary>
        public BeltItemFlow ItemFlow { get; } = new BeltItemFlow();

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

        /// <summary>노드 면에 붙은 입출력 표시. 마커의 자식이라 마커 파괴 시 함께 사라진다.</summary>
        private struct PortMarker
        {
            public SpriteRenderer sr;
            public PortIO io;
            public FlowKind declared; // 포트에 적힌 품목 — 출력은 조합표가 덮는다
        }

        private readonly Dictionary<Vector2Int, List<PortMarker>> _portMarkers =
            new Dictionary<Vector2Int, List<PortMarker>>();

        /// <summary>마지막 진단. 병목 힌트가 「무엇부터」를 고르는 데 쓴다.</summary>
        private IReadOnlyList<NodeDiagnostic> _lastDiagnostics;

        private bool _removeMode; // 제거 모드 — 탭으로 노드/벨트 삭제

        /// <summary>
        /// 선택된 벨트 요소(병합기·분류기). 없으면 탭은 노드를 놓는다.
        ///
        /// 직선·코너는 **드래그**가 만들고 경로가 방향을 정한다. 병합기·분류기는 방향이 여러 개라
        /// 경로로 표현되지 않으므로 **탭**으로 놓고, 면은 이웃에서 다시 잡는다(BeltAutoOrient).
        /// </summary>
        private BeltElementKind? _elementMode;

        private static readonly Color SelectedColor = new Color(0.98f, 0.85f, 0.30f, 1f);
        private static readonly Color BeltColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color BeltArrowColor = new Color(0.95f, 0.85f, 0.3f, 1f);       // 미연결(dangling)
        private static readonly Color BeltConnectedColor = new Color(0.35f, 0.9f, 0.4f, 1f);    // 자동연결됨
        private static readonly Color BeltWarningColor = new Color(0.98f, 0.45f, 0.25f, 1f);   // 끝단 미연결 경고
        private static readonly Color GridBgColor = new Color(0.11f, 0.13f, 0.17f, 0.55f);      // 설치 영역 배경
        private static readonly Color GridLineColor = new Color(0.40f, 0.85f, 0.60f, 0.35f);    // 셀 경계선
        private static readonly Color GridBorderColor = new Color(0.45f, 0.9f, 0.65f, 0.85f);   // 바깥 테두리
        private static readonly Color PanDimColor = new Color(0.03f, 0.05f, 0.08f, 0.55f);     // 이동 모드 흐림 막
        private static readonly Color HintColor = new Color(0.98f, 0.72f, 0.25f, 0.92f);       // 병목 힌트 바탕(경고 톤)
        // 종류별 배색은 **아트 자체의 색**이다(V02 §1). 코드는 밝기만 곱한다.
        // 아트가 아직 없어 전 노드가 흰 사각으로 나왔고, 그래서 보드에서 코어와 군수를 못 갈랐다.
        // 아래는 **아트가 들어오면 아트가 이기는 플레이스홀더 색상**이다 —
        // 색 축은 종류, 밝기 축은 상태로 그대로 유지된다(한 축에 둘을 겹치지 않는다).
        private static readonly Color NodeBaseColor = Color.white;

        /// <summary>노드 종류별 플레이스홀더 색. 아트 투입 시 이 표가 사라지고 스프라이트 색이 대신한다.</summary>
        private static Color NodeTypeColor(NodeType type)
        {
            switch (type)
            {
                case NodeType.Core: return new Color(0.98f, 0.85f, 0.35f);       // 코어 — 금색(허브)
                case NodeType.Processing: return new Color(0.60f, 0.70f, 0.95f); // 가공 — 청색
                case NodeType.MunitionsBasic: return new Color(0.95f, 0.50f, 0.45f);  // 군수 — 적색
                case NodeType.Energy: return new Color(0.55f, 0.90f, 0.60f);     // 에너지 — 녹색
                case NodeType.Storage: return new Color(0.75f, 0.72f, 0.66f);    // 저장 — 회백
                case NodeType.Booster: return new Color(0.80f, 0.55f, 0.95f);    // 부스터 — 보라
                default: return new Color(0.45f, 0.48f, 0.52f);                  // 쉴드(스텁) — 흐린 회색
            }
        }

        /// <summary>
        /// 벨트가 나르는 품목의 색. 비어 있으면(상류 없음) 짙은 회색 —
        /// 「깔았는데 아무것도 안 흐른다」가 색으로 먼저 보인다.
        ///
        /// ⚠️ **품목 열한 종이 2026-09-05에 들어왔다**(`260904_W01` 3-2). 여기를 같이 안 고치면
        /// 시작 보드의 벨트가 **전부 짙은 회색**으로 뜬다 — 「아무것도 안 흐른다」와
        /// 「색을 아직 안 정했다」가 화면에서 똑같이 보이는데, 앞은 고칠 일이고 뒤는 아니다.
        ///
        /// 계열로 묶었다: 탄약은 주황 계열 · 재료는 베이지 계열 · 전력계는 하늘 계열 ·
        /// 드론은 연두 계열. 계열 안에서 명도로 갈라 색각 이상에서도 라벨과 함께 읽히게 했다.
        /// </summary>
        private static Color FlowColor(FlowKind kind)
        {
            switch (kind)
            {
                case FlowKind.Material: return new Color(0.70f, 0.68f, 0.62f);   // 물류 품목 — 베이지
                case FlowKind.Ammo: return new Color(0.95f, 0.55f, 0.40f);       // 구 탄약(폐기) — 주황
                case FlowKind.Power: return new Color(0.55f, 0.85f, 0.98f);      // 전력 — 하늘
                case FlowKind.Heat: return new Color(0.95f, 0.40f, 0.30f);       // 발열 — 적
                case FlowKind.Drone: return new Color(0.60f, 0.95f, 0.70f);      // 구 드론 몸체 — 연두
                case FlowKind.Propellant: return new Color(0.82f, 0.60f, 0.96f); // 추진제 — 보라(부스터와 짝)

                // 원천·재료 계열
                case FlowKind.CoreEnergy: return new Color(0.98f, 0.90f, 0.45f);      // 코어 에너지 — 노랑
                case FlowKind.BasicParts: return new Color(0.78f, 0.74f, 0.66f);      // 기초재료·부품 — 밝은 베이지
                case FlowKind.PowerMaterial: return new Color(0.45f, 0.72f, 0.88f);   // 발전재료 — 짙은 하늘
                case FlowKind.Battery: return new Color(0.35f, 0.90f, 0.85f);         // 배터리 — 청록
                case FlowKind.DefenseMaterial: return new Color(0.62f, 0.66f, 0.78f); // 방어 재료 — 청회색

                // 탄약 계열 — 주황에서 갈라진다
                case FlowKind.StandardAmmo: return new Color(0.95f, 0.55f, 0.40f);    // 표준탄 — 주황
                case FlowKind.PierceAmmo: return new Color(0.99f, 0.72f, 0.35f);      // 관통탄 — 밝은 주황
                case FlowKind.ExplosiveAmmo: return new Color(0.88f, 0.38f, 0.30f);   // 폭발탄 — 붉은 주황

                // 드론 계열 — 연두에서 갈라진다
                case FlowKind.DroneBodyParts: return new Color(0.60f, 0.95f, 0.70f);  // 드론 몸체 부품 — 연두
                case FlowKind.StackDrone: return new Color(0.40f, 0.85f, 0.55f);      // 누적형 드론 — 짙은 연두
                case FlowKind.AoeDrone: return new Color(0.75f, 0.98f, 0.50f);        // 광역형 드론 — 라임

                default: return new Color(0.32f, 0.34f, 0.38f);                  // None — 비어 있다
            }
        }

        /// <summary>
        /// 품목 라벨(벨트 위 표시). 색만으로는 색각 이상에서 안 갈린다.
        ///
        /// 한 글자로 붙는 자리라 계열이 겹치는 것은 **둘째 글자**로 가른다
        /// (관통탄 「관」 · 폭발탄 「폭」 · 표준탄 「탄」). 색이 비슷한 것끼리 글자가
        /// 달라야 라벨이 색의 보조가 아니라 대체가 된다.
        /// </summary>
        private static string FlowLabel(FlowKind kind)
        {
            switch (kind)
            {
                case FlowKind.Material: return "품";
                case FlowKind.Ammo: return "탄";   // 구 탄약(폐기)
                case FlowKind.Power: return "전";
                case FlowKind.Heat: return "열";
                case FlowKind.Drone: return "드";  // 구 드론 몸체(폐기)
                case FlowKind.Propellant: return "추";

                case FlowKind.CoreEnergy: return "코";
                case FlowKind.BasicParts: return "부";
                case FlowKind.PowerMaterial: return "발";
                case FlowKind.Battery: return "배";
                case FlowKind.DefenseMaterial: return "방";

                case FlowKind.StandardAmmo: return "탄";
                case FlowKind.PierceAmmo: return "관";
                case FlowKind.ExplosiveAmmo: return "폭";

                case FlowKind.DroneBodyParts: return "드";
                case FlowKind.StackDrone: return "누";
                case FlowKind.AoeDrone: return "광";

                default: return "";
            }
        }

        /// <summary>
        /// 노드 라벨. 군수 노드는 **종류가 아니라 지금 만드는 것**을 적는다 —
        /// 넷 다 「군수」라고 적혀 있으면 보드에서 갈리지 않는다.
        /// </summary>
        private static string NodeLabel(NodeInstance inst)
        {
            if (inst == null || inst.Definition == null) return "";
            if (inst.Definition.type != NodeType.MunitionsBasic) return inst.Definition.displayName;

            switch (inst.CurrentRecipe.kind)
            {
                case RecipeKind.DroneBody: return "군수:드론";
                case RecipeKind.Propellant: return "군수:추진";
                case RecipeKind.ShieldMaterial: return "군수:쉴드";
                default: return "군수:" + AmmoLabel(inst.AmmoKind);
            }
        }

        // 보드 지역 그리기 순서. 격자 배경 -3 · 셀선 -2 아래에 맞춘 같은 축이다
        // (전역 SortingLayers와 섞지 않는다 — 섞었더니 화살표가 벨트 뒤로 사라졌다).
        private const int MarkerOrder = 0;
        private const int BeltArrowOrder = 1;
        private const int BeltWarningOrder = 2;
        // 물건은 화살표 위, 경고 아래. 경고는 「고쳐야 할 것」이라 무엇에도 가려지면 안 된다.
        private const int BeltItemOrder = 2;
        private const int PortInOrder = 1;   // 노드 몸통(0) 위
        private const int PortOutOrder = 2;  // 출력이 입력보다 위 — 겹칠 일은 없지만 의도를 남긴다

        /// <summary>
        /// 벨트 위 물건을 그리는 스프라이트 풀 (2026-09-05 신설).
        ///
        /// **매 프레임 만들고 지우지 않는다.** 한 칸에 최대 셋(<see cref="BeltItemFlow.MaxPerCell"/>)이고
        /// 격자가 117칸이라 최악에 350개인데, 그걸 프레임마다 Instantiate/Destroy 하면
        /// GC가 끊임없이 돈다. 쓰는 만큼 꺼내 쓰고 남는 것은 꺼 둔다.
        /// </summary>
        private readonly List<SpriteRenderer> _itemPool = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _itemBodies = new List<SpriteRenderer>();
        private Transform _itemRoot;

        private static Sprite _unitSprite;

        /// <summary>
        /// 튜토리얼 고스트 — **놓을 자리를 반투명으로 미리 보여 준다**(튜토리얼 기획서 3장).
        ///
        /// 신호가 꺼져 있으면 아무것도 하지 않는다. 그래서 스테이지 0을 떼어낼 때
        /// 이 메서드는 그냥 지나가는 코드가 되고 보드는 손댈 필요가 없다.
        ///
        /// **채워졌는지도 여기서 게시한다** — 그 칸의 사실을 아는 것은 보드뿐이다.
        /// </summary>
        private void DrawTutorialGhost()
        {
            Vector2Int? target = TutorialSignals.GhostCell;
            if (target == null || _grid == null) return;

            Vector2Int cell = target.Value;
            // ⚠️ **노드만 보면 안 된다.** 비워 둔 칸이 병합기 자리로 바뀌면서(2026-09-01)
            // 채우는 것이 벨트 요소가 됐다. `GetAt`은 노드만 보므로 그것만 쓰면
            // 병합기를 놓아도 영영 「안 채워짐」이고 스테이지 0이 끝나지 않는다.
            bool filled = _grid.GetAt(cell) != null || _grid.GetBeltAt(cell) != null;
            TutorialSignals.GhostCellFilled = filled;
            if (filled) return; // 놓았으면 고스트는 사라진다(3장 삭제 조건)

            Camera cam = boardCamera != null ? boardCamera : Camera.main;
            if (cam == null) return;

            Vector3 center = _grid.CellToWorld(cell);
            Vector3 sp = cam.WorldToScreenPoint(center);
            if (sp.z <= 0f) return;

            // 셀 한 칸을 화면 크기로 환산한다 — 줌이 바뀌어도 칸에 들어맞게.
            Vector3 edge = cam.WorldToScreenPoint(center + new Vector3(_grid.CellSize, 0f, 0f));
            float size = Mathf.Abs(edge.x - sp.x);
            if (size < 4f) return;

            var rect = new Rect(sp.x - size * 0.5f, Screen.height - sp.y - size * 0.5f, size, size);

            Color prev = GUI.color;
            // 깜빡인다 — 보드에 색이 많아 가만히 있으면 묻힌다.
            float pulse = 0.35f + 0.25f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f));
            GUI.color = new Color(1f, 0.92f, 0.35f, pulse);
            GUI.DrawTexture(rect, UnitSprite() != null ? UnitSprite().texture : Texture2D.whiteTexture);
            GUI.color = prev;
        }

        /// <summary>「노는 중」 글자색. 종류색·상태 밝기와 겹치지 않게 무채색에 가깝게 둔다.</summary>
        private static readonly Color IdleLabelColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        /// <summary>이 칸의 노드가 놀고 있는가. 일감률이 아직 안 실렸으면 「논다」고 말하지 않는다.</summary>
        private static bool IsIdle(Vector2Int cell)
        {
            var perNode = LogisticsOutputBridge.Workload.perNode;
            return perNode != null && perNode.TryGetValue(cell, out float rate) && rate <= 0f;
        }

        /// <summary>
        /// 노드 표시색 = **기본색 × 산출률 밝기 배율**(UI 문서「노드 상태 표시」· V02 §1 개정).
        /// 기본색은 아트 자체의 색이고 코드는 밝기만 곱한다 — 한 노드에 색 축이 둘 겹치면
        /// 빨간 노드가 「정지」인지 「군수 노드」인지 구분되지 않아 진단 체계가 무너진다.
        /// 판정 규칙 자체는 NodeStatusTint(MBI.Core)에 있다 — UI는 판정 없이 매핑만.
        /// </summary>
        private Color SeverityColor(Vector2Int cell, float ratio)
        {
            NodeInstance inst = _grid != null ? _grid.GetAt(cell) : null;
            Color baseColor = inst != null && inst.Definition != null
                ? NodeTypeColor(inst.Definition.type)
                : NodeBaseColor;

            float tint = NodeStatusTint.Of(ratio);
            Color c = baseColor * tint;
            c.a = baseColor.a; // 알파는 밝기 축이 아니다 — 곱하면 노드가 투명해진다
            return c;
        }

        /// <summary>노드 상태색 적용(§L4-R #5). 진단은 Provider(LogisticsDiagnostics)가 공급 — UI는 색 매핑만.
        /// 선택 중인 셀은 선택 하이라이트 유지.</summary>
        public void ApplyDiagnostics(IReadOnlyList<NodeDiagnostic> diags)
        {
            if (diags == null) return;
            _lastDiagnostics = diags;
            foreach (NodeDiagnostic d in diags)
            {
                float ratio = d.targetRate > 0f ? d.actualRate / d.targetRate : 1f;
                Color c = SeverityColor(d.cell, ratio);
                _nodeColors[d.cell] = c;
                if (_selected.HasValue && _selected.Value == d.cell) continue; // 선택 하이라이트 유지
                if (_markers.TryGetValue(d.cell, out GameObject m) && m != null)
                    m.GetComponent<SpriteRenderer>().color = c;
            }
        }

        /// <summary>진단 없음(코어 미배치 등) → 노드를 정상색으로.</summary>
        public void ClearDiagnostics()
        {
            _lastDiagnostics = null;
            foreach (KeyValuePair<Vector2Int, GameObject> kv in _markers)
            {
                if (kv.Value == null) continue;
                Color c = SeverityColor(kv.Key, 1f); // 진단이 없어도 **종류색은 남는다**
                _nodeColors[kv.Key] = c;
                if (_selected.HasValue && _selected.Value == kv.Key) continue;
                kv.Value.GetComponent<SpriteRenderer>().color = c;
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
        /// <summary>
        /// 비워 둔 칸을 채우는 **기초 군수 노드**를 놓는다 (2026-09-05 · `260904_W03` 1-1).
        ///
        /// 종전에는 병합기였다. 시작 보드가 표준탄 단일 라인으로 바뀌면서 합칠 갈래가
        /// 없어졌고, 빈 칸은 **부품을 탄으로 바꿀 노드**의 자리가 됐다.
        /// </summary>
        private void PlaceTutorialFill()
        {
            StartingBoard.Slot slot = StartingBoard.FillsEmptySlot;
            if (_grid.HasBelt(slot.cell) || _grid.IsOccupied(slot.cell)) return;

            NodeDefinition def = FindStartingNode(slot.nodeId);
            if (def == null) return;

            if (_grid.TryPlace(slot.cell, def, out NodeInstance placed))
            {
                placed.AmmoKind = slot.ammo;
                SpawnNodeMarker(slot.cell);
            }
        }

        // 시작 배치가 쓰는 노드 자산을 인스펙터 목록에서 찾는다. 시작 보드는 id로만 적고
        // 자산 참조를 갖지 않으므로(순수 데이터), 씬 쪽에서 이어 준다.
        private NodeDefinition FindStartingNode(string nodeId)
        {
            if (initialLayout == null) return null;
            foreach (InitialNode item in initialLayout)
                if (item.node != null && item.node.nodeId == nodeId) return item.node;

            if (palette == null) return null;
            foreach (NodeDefinition d in palette)
                if (d != null && d.nodeId == nodeId) return d;
            return null;
        }

        private void ApplyInitialLayout()
        {
            if (initialLayout != null)
                foreach (InitialNode item in initialLayout)
                {
                    if (item.node == null) continue;
                    if (!_grid.TryPlace(item.cell, item.node, out NodeInstance placed)) continue;
                    placed.AmmoKind = item.ammoKind; // 군수 노드가 만드는 탄종(§1). 다른 타입에서는 읽히지 않는다.
                    SpawnNodeMarker(item.cell);
                }

            if (initialBelts != null)
                foreach (InitialBelt b in initialBelts)
                {
                    bool ok = b.merger
                        ? _grid.TryPlaceBeltElement(b.cell, BeltElementKind.Merger,
                            new[] { PortFace.West }, new[] { PortFace.East }, FlowKind.None, out _)
                        : _grid.TryPlaceBelt(b.cell, b.inFace, b.outFace, FlowKind.None, out _);
                    if (ok) SpawnBeltMarker(b.cell, b.outFace);
                }

            // 튜토리얼을 이미 끝냈으면 **비워 둔 칸이 채워진 채로 시작한다**(260902_W09 §1-1 안 A).
            //
            // 그러지 않으면 클리어한 사람이 다시 들어왔을 때 끊긴 보드를 보게 된다 —
            // 고스트도 안내도 없이 방금 배운 것을 다시 해야 하는 화면이다.
            //
            // ⚠️ 이것은 **근본 해결이 아니다.** 진짜 구멍은 보드 배치가 저장되지 않는 것이고,
            // 플레이어가 확장한 보드는 여전히 사라진다(불일치 목록 등재분).
            if (IdleSignals.TutorialCleared) PlaceTutorialFill();

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
                }

                // 구역 경계 — 점선. UI 아트 요청 문서(20)「구역 표시 배치 규격」이 값을 정한다.
                // 구 실선(UI 문서 9-2 「파츠 경계가 또렷해진다」)을 대신한다 — 같은 대상이고
                // 배치 규격 쪽이 최신이며 굵기·색·끊는 길이까지 정한다.
                //
                // 파츠 루프 **밖**에서 그린다. 안에서 네 변씩 그리면 몸통↔팔처럼 맞닿은 변이
                // 두 번 그려져 그 자리만 진해지고 점선 위상이 어긋난다(PartLayout.BoundaryRuns 주석).
                foreach (PartLayout.BoundaryRun r in PartLayout.BoundaryRuns())
                {
                    float len = r.length * config.cellSize;
                    float ex = o.x + r.from.x * config.cellSize;
                    float ey = o.y + r.from.y * config.cellSize;
                    if (r.horizontal) SpawnDashedEdge(root.transform, ex + len * 0.5f, ey, len, true);
                    else              SpawnDashedEdge(root.transform, ex, ey + len * 0.5f, len, false);
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
        // ── 구역 표시 배치 규격 (UI 아트 요청 문서(20) 10장 · `260905_W04` 4-5) ────────────
        // 값은 **아트 픽셀** 기준이고 한 칸이 192픽셀이다. 월드 단위로 쓰려면 cellSize를 곱한다 —
        // 칸 크기가 바뀌어도 화면에서 보이는 비율이 그대로 유지된다.
        private const float ZonePx = 192f;      // 격자 한 칸 = 192 아트 픽셀
        private const float ZoneLineThickPx = 4f;   // 점선 굵기
        private const float ZoneDashPx = 16f;       // 그리는 길이
        private const float ZoneGapPx = 16f;        // 띄우는 길이
        private const float ZoneLabelInsetPx = 8f;  // 이름표를 구역 왼윗모서리 안쪽으로 들이는 양
        private const int ZoneLabelFontPx = 24;     // 이름표 글자 높이

        /// <summary>미색 · 불투명도 40% — 밑의 타일을 가리지 않는다(배치 규격).</summary>
        private static readonly Color ZoneLineColor = new Color(0.96f, 0.94f, 0.86f, 0.40f);

        /// <summary>
        /// 그리는 층: **타일 위 · 품목 아래.** 물건이 지나가는 것을 가리지 않는다(배치 규격).
        /// 지침 §1이 이 게임의 코어를 「물류와 그 애니메이션」으로 정했으므로, 구역 표시가
        /// 품목을 가리면 코어를 가리는 것이 된다.
        /// </summary>
        private const int ZoneLineOrder = -1;   // 셀선(-2) 위 · 노드 마커(0)와 품목(2) 아래

        /// <summary>
        /// 구역 경계 한 변을 점선으로 그린다. <paramref name="horizontal"/>이면 가로변이다.
        ///
        /// 조각을 <see cref="ZoneDashPx"/>/<see cref="ZoneGapPx"/> 주기로 놓되 **변의 양 끝에서
        /// 잘리지 않도록** 주기 수를 반올림해 간격을 변 길이에 맞춘다. 안 맞추면 파츠마다 끝
        /// 조각 길이가 달라져 경계가 어긋나 보인다.
        /// </summary>
        private void SpawnDashedEdge(Transform parent, float cx, float cy, float length, bool horizontal)
        {
            float unit = config.cellSize / ZonePx;
            float thick = Mathf.Max(ZoneLineThickPx * unit, 0.01f);
            float period = (ZoneDashPx + ZoneGapPx) * unit;
            if (period <= 0f || length <= 0f) return;

            int count = Mathf.Max(1, Mathf.RoundToInt(length / period));
            float step = length / count;                 // 변 길이에 맞춘 실제 주기
            float dash = step * (ZoneDashPx / (ZoneDashPx + ZoneGapPx));
            float start = (horizontal ? cx : cy) - length * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float c = start + step * i + dash * 0.5f; // 조각 중심
                if (horizontal) SpawnQuad(parent, c, cy, dash, thick, ZoneLineColor, ZoneLineOrder);
                else            SpawnQuad(parent, cx, c, thick, dash, ZoneLineColor, ZoneLineOrder);
            }
        }

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
            if (!GameLayerController.BoardViewActive) return;
            if (PointerOverUi()) return;

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

        /// <summary>
        /// 물건 그림은 <see cref="LateUpdate"/>에서 옮긴다.
        ///
        /// <see cref="LogisticsOutputProvider"/>가 자기 `Update`에서 아이템을 미는데, 실행 순서가
        /// 정해져 있지 않아 이쪽 `Update`가 먼저 돌 수 있다. 그러면 **한 프레임 전 자리**를
        /// 그리게 되어 물건이 끊겨 보인다. `LateUpdate`는 모든 `Update` 뒤라 그 경합이 없다.
        /// </summary>
        private void LateUpdate()
        {
            RefreshBeltItems();
        }

        private void Update()
        {
            ApplyZoom(); // 보드를 볼 때만 확대한다 — 나가면 원래 시야로 돌아간다

            // 촬영 복귀 요청 — 가져가며 내린다.
            if (TutorialSignals.ClearEmptySlotRequested)
            {
                TutorialSignals.ClearEmptySlotRequested = false;
                RemoveAt(StartingBoard.EmptySlot);
            }

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
                else if (_elementMode.HasValue) PlaceElement(cell, _elementMode.Value);
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

        /// <summary>
        /// 지금 포인터가 OnGUI 버튼 위에 있는가 — <see cref="_uiRects"/> 참조.
        ///
        /// IMGUI 좌표는 **위에서 아래로** 재고 포인터는 아래에서 위로 재므로 y를 뒤집는다.
        /// </summary>
        private bool PointerOverUi()
        {
            if (Pointer.current == null) return false;
            Vector2 p = Pointer.current.position.ReadValue();
            var gui = new Vector2(p.x, Screen.height - p.y);

            if (GameLayerController.ButtonRect.Contains(gui)) return true;
            for (int i = 0; i < _uiRects.Count; i++)
                if (_uiRects[i].Contains(gui)) return true;
            return false;
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

        /// <summary>
        /// **벨트 위 물건을 그린다** (2026-09-05 신설 · 아이템 모델 5번).
        ///
        /// ⚠️ **9월 5일까지 이 그림이 없었다.** <see cref="BeltItemFlow"/>가 물건을 칸마다 옮기고
        /// 그 도착이 곧 전투력이 되는데(`260904_W04` 2-1 4번), 화면에는 아무것도 안 지나갔다.
        /// 그러면 「물류 라인을 최적화하는 행위가 재미있는가」에서 **최적화한 결과가 안 보인다** —
        /// 라인이 막혀 물건이 쌓이는 것도, 갈래에서 갈리는 것도 숫자로만 알 수 있었다.
        ///
        /// 색과 글자는 벨트가 쓰는 것과 같은 표를 쓴다(<see cref="FlowColor"/>). 같은 품목이
        /// 벨트에서와 다른 색으로 보이면 그 둘이 같은 것인 줄 모른다.
        ///
        /// 스프라이트는 아직 <see cref="UnitSprite"/>(흰 사각형)에 색을 입힌 것이다.
        /// 품목 그림 10종은 시점 실패로 재생성 대기 중이고(`260905_W01` 3-4), 임포트도
        /// 아직 금지라(같은 문서 8장) 여기서 참조를 걸 자리만 남겨 둔다.
        /// </summary>
        private void RefreshBeltItems()
        {
            if (_grid == null) return;

            int used = 0;
            for (int x = 0; x < _grid.Columns; x++)
            for (int y = 0; y < _grid.Rows; y++)
            {
                var cell = new Vector2Int(x, y);
                BeltInstance belt = _grid.GetBeltAt(cell);
                if (belt == null) continue;

                IReadOnlyList<BeltItem> items = ItemFlow.ItemsAt(cell);
                if (items == null || items.Count == 0) continue;

                Vector3 centre = CellWorld(cell);
                for (int i = 0; i < items.Count; i++)
                {
                    // 코너에서 꺾이는 경로는 `BeltItemPose`가 안다 — 여기서 다시 풀지 않는다.
                    Vector2 off = BeltItemPose.LocalOffset(
                        belt.InFace, belt.OutFace, items[i].progress);

                    SpriteRenderer sr = RentItemSprite(used++, out SpriteRenderer body);
                    body.color = ItemColor(items[i].kind);
                    sr.transform.position = centre + (Vector3)(off * _grid.CellSize);
                }
            }

            // 이번 프레임에 안 쓴 것은 꺼 둔다. 지우지 않는 이유는 다음 프레임에 도로 쓰기 때문이다.
            for (int i = used; i < _itemPool.Count; i++)
            {
                if (_itemPool[i].enabled) _itemPool[i].enabled = false;
                if (_itemBodies[i].enabled) _itemBodies[i].enabled = false;
            }
        }

        /// <summary>
        /// 풀에서 <paramref name="index"/>번째를 꺼낸다. 모자라면 하나 더 만든다.
        ///
        /// **테두리와 본체 두 겹이다.** 부모가 어두운 사각형이고 그 위에 조금 작은 본체가 얹힌다 —
        /// 스프라이트가 흰 사각형 하나뿐이라 외곽선을 그릴 수 없어 겹쳐서 만든다.
        /// <paramref name="body"/>에 색을 칠하고, 반환된 부모로 위치를 옮긴다.
        /// </summary>
        private SpriteRenderer RentItemSprite(int index, out SpriteRenderer body)
        {
            if (_itemRoot == null)
            {
                var root = new GameObject("BeltItems");
                root.transform.SetParent(transform, false);
                _itemRoot = root.transform;
            }

            while (_itemPool.Count <= index)
            {
                var go = new GameObject($"item_{_itemPool.Count}");
                go.transform.SetParent(_itemRoot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = UnitSprite();
                sr.color = ItemEdgeColor;
                sr.sortingOrder = BeltItemOrder;
                // 한 칸에 셋이 서므로 최소 간격(1/3칸)보다 작아야 서로 안 겹친다.
                go.transform.localScale = Vector3.one * (_grid.CellSize * ItemDrawSize);

                var inner = new GameObject("body");
                inner.transform.SetParent(go.transform, false);
                inner.transform.localScale = Vector3.one * ItemBodyRatio;
                var isr = inner.AddComponent<SpriteRenderer>();
                isr.sprite = UnitSprite();
                isr.sortingOrder = BeltItemOrder + 1;

                _itemPool.Add(sr);
                _itemBodies.Add(isr);
            }

            SpriteRenderer got = _itemPool[index];
            if (!got.enabled) got.enabled = true;
            body = _itemBodies[index];
            if (!body.enabled) body.enabled = true;
            return got;
        }

        /// <summary>
        /// 벨트 위 물건의 색. **벨트 몸통보다 밝다.**
        ///
        /// ⚠️ 처음에는 <see cref="FlowColor"/>를 그대로 썼는데 **물건이 통째로 안 보였다.**
        /// <see cref="RefreshBeltColors"/>가 벨트 몸통을 같은 <see cref="FlowColor"/>로 칠하고,
        /// 물건은 그 벨트가 나르는 바로 그 품목이라 **언제나 배경과 정확히 같은 색**이 된다.
        /// 「같은 품목은 같은 색」이 맞는 원칙이었는데 그것이 물건을 지웠다.
        ///
        /// 색상은 유지하고 밝기만 올린다 — 품목을 알아보는 단서는 그대로 두면서 배경에서 떠오른다.
        /// 어두운 테두리(<see cref="ItemEdgeColor"/>)가 밝은 벨트 위에서도 같은 일을 한다.
        /// </summary>
        private static Color ItemColor(FlowKind kind)
        {
            Color c = FlowColor(kind);
            return new Color(
                Mathf.Min(1f, c.r * 1.45f + 0.10f),
                Mathf.Min(1f, c.g * 1.45f + 0.10f),
                Mathf.Min(1f, c.b * 1.45f + 0.10f),
                1f);
        }

        /// <summary>
        /// 물건 한 개의 크기(칸 대비). 최소 간격이 1/3칸이라 그보다 작아야 셋이 나란히 선다 —
        /// 겹쳐 보이면 몇 개가 흐르는지 셀 수 없고, 그 수가 곧 대역이다.
        /// </summary>
        private const float ItemDrawSize = 0.26f;

        /// <summary>물건의 테두리. 밝은 벨트 위에서 물건이 묻히지 않게 하는 어두운 바탕이다.</summary>
        private static readonly Color ItemEdgeColor = new Color(0.10f, 0.10f, 0.12f, 1f);

        /// <summary>테두리 대비 본체 크기. 사방에 테두리가 한 겹 남을 만큼만 줄인다.</summary>
        private const float ItemBodyRatio = 0.62f;

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

        /// <summary>
        /// 병합기·분류기 설치(§5-4 L3 · 260829_V03 §판정③).
        ///
        /// 면은 여기서 정하지 않는다 — 임시로 넣고 <see cref="RefreshConnections"/>가
        /// 이웃을 보고 다시 잡는다. 그래야 「요소 먼저, 이웃 나중」 순서가 성립한다.
        /// </summary>
        private void PlaceElement(Vector2Int cell, BeltElementKind element)
        {
            if (!_grid.IsInside(cell) || !_grid.IsFree(cell)) return;

            if (!_grid.TryPlaceBeltElement(cell, element,
                    new[] { PortFace.West }, new[] { PortFace.East }, FlowKind.None, out _)) return;

            SpawnBeltMarker(cell, PortFace.East);
            RefreshConnections();
            Debug.Log($"[MBI] {ElementLabel(element)} 설치 @ 셀({cell.x},{cell.y}).");
        }

        private static string ElementLabel(BeltElementKind e) =>
            e == BeltElementKind.Merger ? "병합기" : "분류기";

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
            sr.sortingOrder = MarkerOrder;
            Color c = SeverityColor(cell, 1f); // 초기 = 정상 밝기. Provider가 라이브 진단으로 갱신(§L4-R #5).
            sr.color = c;
            _markers[cell] = marker;
            _nodeColors[cell] = c;

            SpawnPortMarkers(cell, marker.transform);
        }

        /// <summary>
        /// 노드 면의 입출력 표시. **어느 면으로 들어오고 어느 면으로 나가는지**를 안 보여 주면
        /// 벨트를 어디에 붙여야 할지 찍어 볼 수밖에 없다.
        ///
        /// 출력은 셀 **밖으로 튀어나온** 탭, 입력은 셀 **안쪽에 파인** 탭이다 —
        /// 색을 못 가려도 형태로 갈린다. 색은 그 면을 흐르는 품목이다.
        /// </summary>
        private void SpawnPortMarkers(Vector2Int cell, Transform parent)
        {
            NodeInstance inst = _grid.GetAt(cell);
            if (inst == null || inst.Definition == null || inst.Definition.ports == null) return;

            var list = new List<PortMarker>();
            foreach (NodePort p in inst.Definition.ports)
            {
                Vector2 off = FaceOffset(p.face);
                bool outward = p.io == PortIO.Output;

                var tab = new GameObject(outward ? $"out_{p.face}" : $"in_{p.face}");
                tab.transform.SetParent(parent, false);
                // 출력은 면 밖으로 반쯤 나가고, 입력은 면 안쪽에 머문다.
                float dist = outward ? 0.52f : 0.36f;
                tab.transform.localPosition = new Vector3(off.x * dist, off.y * dist, 0f);
                // 면을 따라 납작하게 — 세로면이면 눕히고 가로면이면 세운다.
                bool horizontal = Mathf.Abs(off.x) > 0.5f;
                tab.transform.localScale = horizontal
                    ? new Vector3(0.16f, 0.34f, 1f)
                    : new Vector3(0.34f, 0.16f, 1f);

                var sr = tab.AddComponent<SpriteRenderer>();
                sr.sprite = UnitSprite();
                sr.sortingOrder = outward ? PortOutOrder : PortInOrder;
                list.Add(new PortMarker { sr = sr, io = p.io, declared = p.kind });
            }

            _portMarkers[cell] = list;
            RefreshPortColors(cell);
        }

        /// <summary>
        /// 포트 색 = 그 면을 흐르는 품목. **출력은 조합표가 정한다** — 군수 노드의 출력 포트는
        /// 「탄약」으로 적혀 있지만 추진제를 돌리면 나가는 것은 추진제다(BeltFlow와 같은 규칙).
        /// 입력은 어둡게 깔아 나가는 쪽과 한눈에 갈리게 한다.
        /// </summary>
        private void RefreshPortColors(Vector2Int cell)
        {
            if (!_portMarkers.TryGetValue(cell, out List<PortMarker> list)) return;

            NodeInstance inst = _grid.GetAt(cell);
            FlowKind outKind = BeltFlow.OutputKindOf(inst);

            foreach (PortMarker pm in list)
            {
                if (pm.sr == null) continue;
                bool outward = pm.io == PortIO.Output;
                Color c = FlowColor(outward ? outKind : pm.declared);
                if (!outward) c *= 0.55f; // 입력은 어둡게
                c.a = 1f;
                pm.sr.color = c;
            }
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
                _portMarkers.Remove(cell); // 자식이라 마커와 함께 파괴됐다 — 목록만 비운다
                if (_selected.HasValue && _selected.Value == cell) _selected = null;
            }
            else if (_grid.HasBelt(cell))
            {
                _grid.TryRemoveBelt(cell);
                if (_beltMarkers.TryGetValue(cell, out GameObject bm) && bm != null) Destroy(bm);
                _beltMarkers.Remove(cell);
                _beltArrows.Remove(cell);
                _beltFlows.Remove(cell);    // 무늬도 마커의 자식이라 함께 사라진다 — 목록만 비운다
                _beltWarnings.Remove(cell); // 아이콘 GameObject는 마커의 자식이라 위 Destroy로 함께 사라짐
            }
            else return;

            RefreshConnections();
            Debug.Log($"[MBI] 제거 @ 셀({cell.x},{cell.y}).");
        }

        // 조립 뷰에서만 노드 팔레트(우측 세로 버튼) — 선택으로 탭 배치 노드 변경 + 제거 모드.
        private void OnGUI()
        {
            _uiRects.Clear();
            if (!GameLayerController.BoardViewActive) return;
            KoreanFont.Apply(); // WebGL엔 시스템 폰트 폴백이 없다

            DrawTutorialGhost(); // 라벨보다 먼저 — 고스트는 배경이지 글자가 아니다
            DrawZoneLabels();    // 구역 이름표 — 칸 라벨보다 먼저(구역은 바탕이고 칸 내용이 위다)
            DrawCellLabels(); // 버튼보다 먼저 — 팔레트/모드 버튼이 라벨 위에 온다
            DrawBottleneckHint();
            DrawMiniMap();
            DrawModeButton();
            DrawZoom();

            if (palette == null || palette.Count == 0) return;

            var style = new GUIStyle(GUI.skin.button) { fontSize = 15 };
            const float w = 130f, h = 36f, pad = 6f;
            float x = Screen.width - w - 12f;
            // ⚠️ 변수 패널(우상단 12..262)과 겹치면 안 된다 — 실제로 겹쳐서 「노드 팔레트」 글자가
            // 패널 위에 얹혀 있었다. 그 아래에서 시작한다.
            float y0 = 300f;

            // 이동 모드에서는 팔레트를 흐리게 — 지금은 놓을 수 없다는 것을 버튼 상태로 알린다.
            GUI.enabled = _mode == BoardMode.Build;

            GUI.Label(new Rect(x, y0 - 26f, w, 24f), "노드 팔레트", new GUIStyle(GUI.skin.label) { fontSize = 15 });
            int i;
            for (i = 0; i < palette.Count; i++)
            {
                if (palette[i] == null) continue;
                var rect = new Rect(x, y0 + i * (h + pad), w, h);
                _uiRects.Add(rect);

                bool sel = !_removeMode && i == _selectedNode;
                if (GUI.Button(rect, (sel ? "● " : "") + palette[i].displayName, style))
                {
                    _selectedNode = i;
                    _removeMode = false;
                    _elementMode = null;
                }
            }

            // 벨트 요소(§5-4 L3). 직선·코너는 드래그가 만들고, 이 둘만 탭으로 놓는다 —
            // 방향이 여러 개라 드래그 경로로는 표현되지 않는다.
            float ey = y0 + i * (h + pad) + 10f;
            GUI.Label(new Rect(x, ey - 22f, w + 20f, 22f), "벨트 요소",
                new GUIStyle(GUI.skin.label) { fontSize = 14 });
            ey += 2f;

            foreach (BeltElementKind e in new[] { BeltElementKind.Merger, BeltElementKind.Sorter })
            {
                var eRect = new Rect(x, ey, w, h);
                _uiRects.Add(eRect);

                bool on = !_removeMode && _elementMode == e;

                // 강제 버튼(튜토리얼 기획서 2장) — 지금 놓아야 할 것을 빛나게 한다.
                // 고스트는 **자리**만 말하므로, 무엇을 놓을지 모르면 자리를 알아도 막힌다.
                Color prevCol = GUI.color;
                if (TutorialSignals.HighlightMerger && e == BeltElementKind.Merger && !on)
                    GUI.color = new Color(1f, 0.92f, 0.45f,
                        0.75f + 0.25f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f)));

                bool pressed = GUI.Button(eRect, (on ? "● " : "") + ElementLabel(e), style);
                GUI.color = prevCol;

                if (pressed)
                {
                    _elementMode = on ? (BeltElementKind?)null : e;
                    _removeMode = false;
                }
                ey += h + pad;
            }

            // 제거 토글.
            var rmRect = new Rect(x, ey + 4f, w, h);
            _uiRects.Add(rmRect);
            if (GUI.Button(rmRect, (_removeMode ? "● " : "") + "제거", style))
            {
                _removeMode = !_removeMode;
                if (_removeMode) _elementMode = null;
            }

            GUI.Label(new Rect(x, ey + h + 12f, w + 20f, 60f),
                _removeMode ? "제거 모드\n탭=노드/벨트 삭제" : "탭=노드 배치\n드래그=벨트");

            GUI.enabled = true;

            DrawRecipePanel();
        }

        /// <summary>
        /// 보드 위 글자 라벨. **색만으로는 안 갈린다** — 색각 이상도 있고, 회색조 캡처에서도
        /// 종류가 읽혀야 한다. 그래서 색과 글자를 같이 건다(UI 문서: 정보는 두 감각으로).
        ///
        /// 마커의 실제 월드 좌표를 쓴다 — 셀에서 다시 계산하면 스크롤 보정을 두 곳에서 하게 된다.
        /// </summary>
        private void DrawCellLabels()
        {
            Camera cam = boardCamera != null ? boardCamera : Camera.main;
            if (cam == null) return;

            var nodeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            var beltStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };

            // 「노는 중」은 노드 이름보다 작게 아래에 붙인다 — 이름을 밀어내면 무엇인지가 사라진다.
            var idleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };

            foreach (KeyValuePair<Vector2Int, GameObject> kv in _markers)
            {
                if (kv.Value == null) continue;
                DrawLabelAt(cam, kv.Value.transform.position, NodeLabel(_grid.GetAt(kv.Key)),
                    nodeStyle, Color.black, 118f);

                // 일감률 0 = 이 노드는 지금 아무것도 안 하고 있다(260831_V07 표시 규칙).
                // 초과분을 몰아서 0으로 두었으므로 **뺄 노드가 그대로 지목된다** —
                // 0.71씩 골고루 나눠 줬다면 여기에 쓸 말이 없다.
                if (IsIdle(kv.Key))
                    DrawLabelAt(cam, kv.Value.transform.position, "노는 중",
                        idleStyle, IdleLabelColor, 118f, 20f);
            }

            foreach (KeyValuePair<Vector2Int, GameObject> kv in _beltMarkers)
            {
                if (kv.Value == null) continue;
                BeltInstance belt = _grid.GetBeltAt(kv.Key);
                // 병합기·분류기는 **무엇인지가 먼저**다 — 직선 벨트와 형태가 같아 글자로만 갈린다.
                string label = belt != null && belt.Element == BeltElementKind.Merger ? "합"
                    : belt != null && belt.Element == BeltElementKind.Sorter ? "분"
                    : FlowLabel(BeltFlow.KindAt(_grid, kv.Key));
                if (label.Length == 0) continue; // 비어 있는 벨트는 색으로만 — 글자까지 깔면 시끄럽다
                DrawLabelAt(cam, kv.Value.transform.position, label, beltStyle, Color.black, 52f);
            }
        }

        /// <summary>
        /// 월드 좌표 → 화면 라벨 한 장. 화면 밖이나 카메라 뒤는 건너뛴다.
        /// <paramref name="yOffset"/>은 같은 칸에 둘째 줄을 붙일 때 쓴다(픽셀, 아래가 +).
        /// </summary>
        /// <summary>
        /// 구역 이름표 여덟 — 구역 왼윗모서리 안쪽에 붙인다
        /// (UI 아트 요청 문서(20) 10장 · `260905_W04` 4-5).
        ///
        /// **왜 코드가 그리는가.** 구역 경계는 격자 좌표에 정확히 맞아야 하는데 생성 도구는
        /// 픽셀 정확도를 못 낸다 — 117칸 중 어디까지가 `팔R`인지를 그림으로 맞출 수 없다.
        /// 그래서 아트 리소스가 아니라 코드 소관이다(배경 아트 요청 문서(23) 2장).
        ///
        /// **IMGUI로 그리는 이유.** 보드는 SpriteRenderer로 그리지만 월드 텍스트 수단이 없고,
        /// WebGL에는 시스템 폰트 폴백이 없어 한글이 통째로 사라진다(지침 §7). <see cref="OnGUI"/>가
        /// 이미 <c>KoreanFont.Apply()</c>로 폰트를 물려 두었으므로 여기 얹는 것이 안전하다.
        /// </summary>
        private void DrawZoneLabels()
        {
            if (config == null || !config.usePartLayout) return;
            Camera cam = boardCamera != null ? boardCamera : Camera.main;
            if (cam == null) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = ZoneLabelFontPx,
                alignment = TextAnchor.UpperLeft,
                fontStyle = FontStyle.Bold,
            };

            Vector2 o = _grid.Origin;
            float inset = ZoneLabelInsetPx * (config.cellSize / ZonePx);

            foreach (PartRect p in PartLayout.Parts)
            {
                string text = PartLayout.LabelOf(p.part);
                if (string.IsNullOrEmpty(text)) continue;

                // 구역의 왼윗모서리(격자는 좌하단 원점이라 y는 origin + size).
                var corner = new Vector3(
                    o.x + p.origin.x * config.cellSize + inset,
                    o.y + (p.origin.y + p.size.y) * config.cellSize - inset,
                    0f);

                Vector3 sp = cam.WorldToScreenPoint(corner);
                if (sp.z <= 0f) continue;
                float y = Screen.height - sp.y;
                if (sp.x < -120f || sp.x > Screen.width + 120f || y < -40f || y > Screen.height + 40f) continue;

                Color prev = GUI.color;
                GUI.color = ZoneLineColor; // 경계선과 같은 미색 — 한 표시의 두 부분이다
                GUI.Label(new Rect(sp.x, y, 120f, ZoneLabelFontPx + 6f), text, style);
                GUI.color = prev;
            }
        }

        private static void DrawLabelAt(Camera cam, Vector3 world, string text,
            GUIStyle style, Color color, float width, float yOffset = 0f)
        {
            if (string.IsNullOrEmpty(text)) return;

            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) return; // 카메라 뒤
            float y = Screen.height - sp.y + yOffset;
            if (sp.x < -width || sp.x > Screen.width + width || y < -20f || y > Screen.height + 20f) return;

            Color prev = GUI.color;
            GUI.color = color;
            GUI.Label(new Rect(sp.x - width * 0.5f, y - 12f, width, 24f), text, style);
            GUI.color = prev;
        }

        /// <summary>
        /// 선택한 노드의 조합표 패널(2026-08-27 레시피 선택형 · 260829_V02 착수 승인).
        ///
        /// **노드 하나는 조합표 하나를 돌린다.** 갈래를 늘리는 방법은 노드를 더 놓는 것이지
        /// 노드 하나를 넓히는 것이 아니므로, 후보 중 하나만 켜진다.
        /// 조합표가 탄약이면 탄종도 함께 고른다 — 출력이 **탄종별 노드 수**에서 나오기 때문이다.
        ///
        /// 판정은 NodeInstance가 한다(돌릴 수 없는 조합표는 거절된다). 여기서는 매핑만 한다.
        /// </summary>
        private void DrawRecipePanel()
        {
            if (!_selected.HasValue || _grid == null) return;

            NodeInstance inst = _grid.GetAt(_selected.Value);
            if (inst == null || inst.Definition == null) return;

            List<NodeRecipe> candidates = inst.Definition.recipes;
            if (candidates == null || candidates.Count == 0) return;

            var style = new GUIStyle(GUI.skin.button) { fontSize = 15 };
            var head = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            const float w = 160f, h = 32f, pad = 4f;
            const float x = 12f;
            // ⚠️ 전투 HUD가 y10~280을 쓰고 태그·합체 버튼이 y300에 있다 —
            // 실제로 겹쳐서 조합표 버튼이 물류 출력 글자 위에 얹혀 있었다. 그 아래에서 시작한다.
            float y = 380f;

            GUI.Label(new Rect(x, y - 26f, w + 80f, 24f), inst.Definition.displayName + " 조합표", head);

            RecipeKind current = inst.CurrentRecipe.kind;
            foreach (NodeRecipe r in candidates)
            {
                var rect = new Rect(x, y, w, h);
                _uiRects.Add(rect);

                // 돌릴 수 없는 후보도 **자리는 보여 준다** — 감추면 「왜 못 만드나」가 아니라
                // 「그런 게 있었나」가 된다. 착수 금지가 화면에서도 자리로 표현된다.
                GUI.enabled = r.IsRunnable;
                if (GUI.Button(rect, (r.kind == current ? "● " : "") + r.displayName, style)
                    && inst.SelectRecipe(r.kind))
                    RefreshConnections(); // 산출이 바뀌면 하류 벨트가 나르는 것도 바뀐다

                y += h + pad;
            }
            GUI.enabled = true;

            if (current != RecipeKind.Ammo) return;

            // 탄종 — 조합표만으로는 부족하다. 라인 생산량이 min(스펙, 탄종별 노드 수)이라
            // 「무엇을 몇 대 놓았는가」가 그대로 출력이 된다.
            y += 6f;
            GUI.Label(new Rect(x, y, w + 80f, 24f), "탄종", head);
            y += 26f;

            for (int k = 0; k < 3; k++)
            {
                var kind = (AmmoKind)k;
                var rect = new Rect(x + k * (64f + pad), y, 64f, h);
                _uiRects.Add(rect);

                if (GUI.Button(rect, (inst.AmmoKind == kind ? "● " : "") + AmmoLabel(kind), style))
                    inst.AmmoKind = kind; // 탄종은 흐르는 품목(탄약)을 바꾸지 않는다 — 라벨만 갈린다
            }
        }

        private static string AmmoLabel(AmmoKind kind)
        {
            switch (kind)
            {
                case AmmoKind.Pierce: return "관통";
                case AmmoKind.Split: return "분열";
                default: return "폭발";
            }
        }

        /// <summary>
        /// 병목 힌트 한 줄(260831_V02 §3 확정). **무엇을 하면 되는지**만 쓰고 정답은 말하지 않는다.
        ///
        /// 자리는 상단 중앙이다 — 좌상단은 전투 HUD, 우상단은 변수 패널이 이미 쓴다.
        /// 막힌 곳이 없으면 아무것도 그리지 않는다: 늘 떠 있는 줄은 읽히지 않는다.
        /// </summary>
        private void DrawBottleneckHint()
        {
            string hint = BottleneckHint.For(LogisticsOutputBridge.GlobalCause, _lastDiagnostics);
            if (hint.Length == 0) return;

            const float w = 540f, h = 32f;
            var rect = new Rect((Screen.width - w) * 0.5f, 12f, w, h);
            _uiRects.Add(rect);

            Color prev = GUI.color;
            GUI.color = HintColor;
            GUI.Box(rect, GUIContent.none);
            GUI.color = prev;

            GUI.Label(rect, hint, new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            });
        }

        // 모드 버튼 — 화면 우측 하단 1개(UI 문서 9-2).
        // **버튼이 표시를 겸한다.** 문구가 현재 모드를 그대로 나타내므로 별도 모드 표시를 두지 않는다.
        // 모드를 바꾸는 곳과 확인하는 곳이 같은 자리가 되고, 화면 요소도 하나 아낀다.
        private void DrawModeButton()
        {
            var style = new GUIStyle(GUI.skin.button) { fontSize = 16 };

            // ⚠️ **왼쪽 위로 옮긴다.** 종전에는 화면 바닥(height-78)에 고정돼 있었는데,
            // 팔레트는 y 300에서 아래로 자라므로 창 높이에 따라 **중간에서 만난다** —
            // 800×450 · 1024×600 · 1280×800 세 크기 전부에서 「제거」 버튼과 겹쳤다
            // (2026-09-02 실측). 팔레트 아래에 붙여도 팔레트가 화면보다 길면 다시 겹친다.
            //
            // 화면 바닥과 화면 위를 각각 기준으로 삼는 두 요소는 언젠가 반드시 만난다.
            // 그래서 같은 기준(왼쪽 위)을 쓰는 배율 줄 옆으로 보낸다.
            var rect = new Rect(12f, 300f, 140f, 46f);
            _uiRects.Add(rect);

            string label = _mode == BoardMode.Pan ? "이동 모드" : "조립 모드";

            // 강제 버튼(T-7) — **이동 모드일 때만** 빛난다. 바꾸고 나면 할 일이 끝났다.
            bool urge = TutorialSignals.HighlightBuildMode && _mode == BoardMode.Pan;
            Color prev = GUI.color;
            if (urge) GUI.color = new Color(1f, 0.92f, 0.45f);

            if (GUI.Button(rect, urge ? "조립 모드로 →" : label, style)) ToggleMode();

            GUI.color = prev;
        }

        // 미니맵 — 부유 요소 띠 좌측(UI 문서 2장). 실루엣 전체 + 현재 보고 있는 범위.
        // 보드가 화면 밖으로 나가는 것은 허용된 설계이므로, 지금 어디를 보는지는 이것이 알린다(9-3).
        // ---- 보드 배율 (영상 D구간 보드 클로즈업) ----
        //
        // 촬영에서 보드를 크게 잡아야 노드 색·벨트 품목 색·배선이 화면에서 읽힌다.
        // 카메라를 손으로 옮기지 않고 버튼으로 재현 가능하게 두는 이유는 **재테이크 때문이다** —
        // 테이크마다 배율이 다르면 편집에서 컷이 안 붙는다.

        /// <summary>보드 배율. 1이 기본이고 버튼이 단계로 움직인다.</summary>
        private float _zoom = 1f;

        /// <summary>씬이 준 원래 시야. 처음 볼 때 한 번 기억한다.</summary>
        private float _baseOrtho = -1f;

        private const float ZoomMin = 1f, ZoomMax = 2.5f, ZoomStep = 0.25f;

        private void DrawZoom()
        {
            var label = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            var style = new GUIStyle(GUI.skin.button) { fontSize = 16 };

            // ⚠️ **한 줄로 눕힌다.** 라벨을 버튼 위에 얹었더니 y 308이 되어 태그 버튼과 겹쳤다
            // (2026-09-02 브라우저 실측 — UI 겹침 네 번째). 전투 HUD가 y 290에서 끝나고
            // 전투 조작 버튼이 y 300에서 시작하는데, 그 조작 버튼은 이제 조립 화면에서
            // 그려지지 않으므로(StageRunner) 이 줄이 300을 통째로 쓴다.
            // 모드 버튼(12..152)의 오른쪽. 같은 줄에서 세로 가운데를 맞춘다.
            const float x = 164f, y = 308f, bw = 40f, bh = 30f, pad = 6f;

            var minus = new Rect(x, y, bw, bh);
            var plus = new Rect(x + bw + pad, y, bw, bh);

            GUI.Label(new Rect(x + (bw + pad) * 2f, y + 6f, 160f, 20f),
                $"보드 배율 ×{_zoom:0.00}", label);
            _uiRects.Add(minus);
            _uiRects.Add(plus);

            if (GUI.Button(minus, "−", style)) SetZoom(_zoom - ZoomStep);
            if (GUI.Button(plus, "+", style)) SetZoom(_zoom + ZoomStep);
        }

        private void SetZoom(float z) => _zoom = Mathf.Clamp(z, ZoomMin, ZoomMax);

        /// <summary>
        /// 배율을 카메라에 먹인다. **보드를 볼 때만** — 전투 화면으로 나가면 원래 시야로 돌린다.
        /// 안 돌리면 조립을 확대해 둔 채 나갔을 때 전투가 통째로 확대된 채 찍힌다.
        /// </summary>
        private void ApplyZoom()
        {
            Camera cam = boardCamera != null ? boardCamera : Camera.main;
            if (cam == null || !cam.orthographic) return;

            if (_baseOrtho < 0f) _baseOrtho = cam.orthographicSize;
            cam.orthographicSize = GameLayerController.BoardViewActive
                ? _baseOrtho / Mathf.Max(0.01f, _zoom)
                : _baseOrtho;
        }

        private void DrawMiniMap()
        {
            if (_pan == null || config == null) return;

            const float mapW = 96f;
            float mapH = mapW * config.rows / Mathf.Max(1, config.columns);
            var box = new Rect(12f, Screen.height - mapH - 32f, mapW, mapH);
            _uiRects.Add(box);

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

        /// <summary>
        /// 벨트 색 = **나르는 품목**. 비어 있으면 짙은 회색이라 「깔았는데 안 흐른다」가 먼저 보인다.
        /// 방향 화살표는 연결 여부(초록/노랑)를 그대로 쓴다 — 축이 둘이라 겹치지 않는다.
        /// </summary>
        private void RefreshBeltColors()
        {
            foreach (KeyValuePair<Vector2Int, GameObject> kv in _beltMarkers)
            {
                if (kv.Value == null) continue;
                var sr = kv.Value.GetComponent<SpriteRenderer>();
                if (sr == null) continue;

                Color c = FlowColor(BeltFlow.KindAt(_grid, kv.Key));
                c.a = BeltColor.a;
                sr.color = c;

                // 화살표는 **지금** 출력면을 가리켜야 한다 — 병합기·분류기는 방향이 다시 잡힌다.
                BeltInstance belt = _grid.GetBeltAt(kv.Key);
                if (belt == null || !_beltArrows.TryGetValue(kv.Key, out SpriteRenderer arrow)) continue;
                if (arrow == null) continue;

                Vector2 off = FaceOffset(belt.OutFace);
                arrow.transform.localPosition = new Vector3(off.x * 0.32f, off.y * 0.32f, 0f);
            }
        }

        /// <summary>격자 좌하단 코너 월드 좌표 = 보드 위치 중심 정렬(파생값).</summary>
        private static Vector2 ComputeOrigin(BoardConfig cfg, Vector3 boardPos)
        {
            return new Vector2(boardPos.x, boardPos.y)
                   - new Vector2(cfg.columns * cfg.cellSize, cfg.rows * cfg.cellSize) * 0.5f;
        }

        // 벨트 마커: 회색 셀 사각 + outFace 쪽 밝은 방향 표시(플레이스홀더).
        /// <summary>벨트 흐름 무늬. 화살표를 밀어 주는 쪽이다(셀 → 무늬).</summary>
        private readonly Dictionary<Vector2Int, BeltFlowAnimator> _beltFlows =
            new Dictionary<Vector2Int, BeltFlowAnimator>();

        private void SpawnBeltMarker(Vector2Int cell, PortFace outFace)
        {
            var m = new GameObject($"Belt_{cell.x}_{cell.y}");
            m.transform.SetParent(transform, false);
            m.transform.position = CellWorld(cell);
            m.transform.localScale = Vector3.one * (_grid.CellSize * 0.85f);
            var sr = m.AddComponent<SpriteRenderer>();
            sr.sprite = UnitSprite();
            sr.color = BeltColor;
            sr.sortingOrder = MarkerOrder;

            var arrow = new GameObject("dir");
            arrow.transform.SetParent(m.transform, false);
            Vector2 off = FaceOffset(outFace);
            arrow.transform.localPosition = new Vector3(off.x * 0.32f, off.y * 0.32f, 0f);
            arrow.transform.localScale = new Vector3(0.34f, 0.34f, 1f);
            var asr = arrow.AddComponent<SpriteRenderer>();
            asr.sprite = UnitSprite();
            asr.color = BeltArrowColor;
            // ⚠️ 보드는 **자기 지역 순서대로 그린다**(격자 배경 -3 · 셀선 -2 · 마커 0).
            // 여기에 SortingLayers.Tile(-20)을 쓰면 화살표가 벨트 몸통(0) 뒤로 들어가 안 보인다 —
            // 실제로 그래서 방향 표시가 화면에 없었다.
            asr.sortingOrder = BeltArrowOrder;

            // 흐름 무늬 — 입력 면은 격자에서 읽는다(직선·코너·병합기 다 같은 규칙).
            var flow = m.AddComponent<BeltFlowAnimator>();
            flow.arrow = asr;
            BeltInstance be = _grid.GetBeltAt(cell);
            PortFace inFace = be != null ? be.InFace : NodeConnectionRules.Opposite(outFace);
            flow.SetPath(BeltFlowAnimator.Offset(inFace), BeltFlowAnimator.Offset(outFace));
            _beltFlows[cell] = flow;

            // 끝단 미연결 경고(§5-4 ⑤): 셀 위쪽 모서리에 작은 표식. 기본 off — RefreshConnections가 켠다.
            var warn = new GameObject("warn");
            warn.transform.SetParent(m.transform, false);
            warn.transform.localPosition = new Vector3(0f, 0.30f, 0f);
            warn.transform.localScale = new Vector3(0.26f, 0.26f, 1f);
            var wsr = warn.AddComponent<SpriteRenderer>();
            wsr.sprite = UnitSprite();
            wsr.color = BeltWarningColor;
            wsr.sortingOrder = BeltWarningOrder; // 경고는 화살표보다도 위
            wsr.enabled = false;

            _beltArrows[cell] = asr;
            _beltWarnings[cell] = wsr;
            _beltMarkers[cell] = m;
        }

        // §5-4 L2: 배치 후 연결 그래프 재계산 → 벨트 방향 표시 색(연결=초록/미연결=노랑) + 끝단 경고(⑤).
        // 설치 확정 시점(설치·배치·제거)에만 호출된다 → 드래그 중에는 판정하지 않는다는 사양이 자동 충족.
        private void RefreshConnections()
        {
            // 순서가 중요하다: **면 → 품목 → 색**.
            // 면이 안 잡히면 링크가 안 서고, 링크가 안 서면 품목이 못 흐른다.
            BeltAutoOrient.Resolve(_grid);
            BeltFlow.Resolve(_grid);

            // 배치가 바뀌면 아이템 흐름의 링크도 다시 잡는다. **면·품목이 정해진 뒤**여야 한다 —
            // 먼저 부르면 아직 안 붙은 면으로 링크를 만든다.
            ItemFlow.Rebuild(_grid);

            RefreshBeltColors();
            foreach (Vector2Int cell in _portMarkers.Keys) RefreshPortColors(cell);

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

            // 흐름 무늬 — **이어져 있고 품목이 잡힌 벨트만** 흐른다.
            // 이어졌는데 품목이 None이면 배선만 있고 아무것도 안 지나가는 것이다.
            foreach (KeyValuePair<Vector2Int, BeltFlowAnimator> kv in _beltFlows)
            {
                if (kv.Value == null) continue;

                // ⚠️ **방향을 여기서 다시 잡는다.** 마커를 만들 때 잡아 둔 면은 곧 낡는다 —
                // 바로 위 BeltAutoOrient.Resolve가 이웃을 보고 면을 갈아 끼우기 때문이다.
                // 그대로 두면 무늬가 실제 흐름과 반대로 흐르는 벨트가 생긴다.
                BeltInstance b = _grid.GetBeltAt(kv.Key);
                if (b != null)
                    kv.Value.SetPath(BeltFlowAnimator.Offset(b.InFace),
                        BeltFlowAnimator.Offset(b.OutFace));

                kv.Value.Flowing = connected.Contains(kv.Key)
                    && BeltFlow.KindAt(_grid, kv.Key) != FlowKind.None;
            }

            // 판정은 전부 Core(BeltRouting) — 여기서는 켜고 끄기만 한다(§3 UI는 매핑만).
            // (아래) 끝단 미연결 경고.
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
