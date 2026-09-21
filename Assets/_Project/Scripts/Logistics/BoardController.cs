using MBI.UI;
using System.Collections.Generic;
using MBI.Core;
using MBI.Core.Audio;
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
        [Tooltip("보드 아트 묶음(노드·벨트 부속·포트·품목). 비어 있으면 종전대로 색 사각으로 그린다.")]
        [SerializeField] private BoardArtSet art;
        [Tooltip("팔레트가 비었을 때 폴백 배치 노드.")]
        [SerializeField] private NodeDefinition placeTarget;
        [Tooltip("배치 가능한 노드 팔레트(조립 뷰에서 선택). 씬 생성기가 주입.")]
        [SerializeField] private List<NodeDefinition> palette = new List<NodeDefinition>();
        [Tooltip("모듈 팔레트 2종(M·R). 고른 뒤 놓인 노드를 탭하면 그 노드에 붙는다. 씬 생성기가 주입.")]
        [SerializeField] private List<ModuleDefinition> modulePalette = new List<ModuleDefinition>();
        [Tooltip("좌표 변환 카메라. 비우면 Camera.main.")]
        [SerializeField] private Camera boardCamera;
        [Tooltip("시작 배치(온보딩). 빈 보드로 시작하면 플레이어가 무엇을 해야 할지 알 수 없다 — 거의 완성된 라인을 주고 한 칸만 비워 둔다(튜토리얼 10장 '한쪽 팔만 비움'을 보드에 적용).")]
        [SerializeField] private List<InitialNode> initialLayout = new List<InitialNode>();
        [Tooltip("시작 배선. 노드만 있고 벨트가 없으면 연결성 게이트에 걸려 출력이 0이다.")]
        [SerializeField] private List<InitialBelt> initialBelts = new List<InitialBelt>();

        /// <summary>팔레트 버튼 안쪽 여백 — 그림이 테두리와 글자에 안 닿게.</summary>
        private const float PaletteThumbPad = 4f;

        private int _selectedNode; // 팔레트에서 선택된 노드 인덱스

        // 🗑️ **`_placeRotation` 폐기**(2026-09-18 사용자 리허설 ⑩) — 놓기 전 회전 버튼을
        //    걷으면서 들고 있을 것이 없어졌다. 회전은 놓은 뒤 팝오버가 든다.


        /// <summary>팔레트 가로 스크롤 자리. 열한 칸이 기준 캔버스 1440 을 넘어선다(2026-09-11).</summary>
        private Vector2 _paletteScroll;
        // 버튼 자리는 MBI.UI.UiBlockers가 모은다 — 다른 어셈블리가 그린 패널까지 함께 막기 위해서다.

        // ── 보드 둘 (2026-09-16 · 사용자 확정 · 플랜 §74-16 ①) ──────────────────
        //
        // **로봇마다 자기 판을 갖는다.** 실루엣은 같은 12×14 이고 마스크도 같지만,
        // 살아 있는 마운트 포트가 다르다(A 하나 · B 둘) — 주인은 `BoardGrid.Owner` 가 든다.
        //
        // 📌 **판은 둘, 그림은 하나다.** 둘 다 매 틱 돌지만(대기 로봇 보드도 채워야 한다),
        //    화면에 서는 마커는 **탭이 고른 쪽 하나**뿐이다. 마커 사전 아홉 벌을 둘로
        //    늘리는 대신 탭을 누를 때 **다시 짓는다** — 짓는 비용은 Awake 가 이미
        //    한 번 치르는 그 비용이고(칸 117), 사전이 둘이면 **같은 일을 하는 자리가
        //    아홉 쌍** 생긴다(지침 §7 이 제일 자주 깨지는 꼴).

        /// <summary>
        /// **시작 보드가 쓰는데 팔레트에는 없는 노드들** (2026-09-16 신설).
        ///
        /// 지금 여기 드는 것은 로봇 B 의 **복합 가공소** 하나다. 팔레트는 「플레이어가
        /// 놓을 수 있는 것」이고 이 주머니는 「판을 세울 때 필요한 자산」이라 뜻이 다르다 —
        /// 섞으면 시작 보드에 한 칸 늘 때마다 **놓을 수 있는 것이 조용히 늘어난다.**
        ///
        /// ⚠️ 채우는 것은 `GameSceneCreator` 다 — `StartingBoardB` 를 읽어 넣는다.
        /// </summary>
        public List<NodeDefinition> startingNodePool = new List<NodeDefinition>();

        /// <summary>판 둘. 차례는 <see cref="MountOwner"/> 값 그대로다(A 0 · B 1).</summary>
        private readonly BoardGrid[] _boards = new BoardGrid[2];
        private readonly BeltItemFlow[] _flows = { new BeltItemFlow(), new BeltItemFlow() };

        /// <summary>지금 **편집 중인** 판의 주인 — 조립 화면 로봇 탭이 정한다.</summary>
        private MountOwner _editing = MountOwner.RobotA;

        /// <summary>지금 편집 중인 판의 주인. 탭이 읽는다.</summary>
        public MountOwner Editing => _editing;

        private static int Index(MountOwner owner) => owner == MountOwner.RobotB ? 1 : 0;


        /// <summary>그 로봇의 판. Awake 전에는 <c>null</c>.</summary>
        public BoardGrid BoardOf(MountOwner owner) => _boards[Index(owner)];

        /// <summary>그 로봇 판의 벨트 흐름.</summary>
        public BeltItemFlow FlowOf(MountOwner owner) => _flows[Index(owner)];

        /// <summary>배치 상태 격자(§5-5 출력 집계용). Awake 후 유효.</summary>
        public BoardGrid Grid => _grid;

        /// <summary>
        /// 벨트 위 개별 아이템 (2026-09-04 배선 · `260904_W01` 6장).
        ///
        /// **보드가 소유한다.** 도는 것은 <see cref="LogisticsOutputProvider"/>이고 그리는 것은
        /// 벨트 흐름 애니메이션이라, 둘이 같은 객체를 봐야 화면과 숫자가 갈리지 않는다.
        ///
        /// ⚠️ **편집 중인 판의 것이다**(2026-09-16). 둘 다 도는 것은
        /// <see cref="LogisticsOutputProvider"/> 가 <see cref="FlowOf"/> 로 각각 부른다.
        /// </summary>
        public BeltItemFlow ItemFlow => _flows[Index(_editing)];

        /// <summary>
        /// 편집 중인 판. ⚠️ **이름을 안 바꿨다** — 이 파일 안 백 군데가 이것을 읽고 있고,
        /// 그 전부가 **편집 중인 판**을 뜻한다. 이름을 바꾸면 그 백 줄이 전부 diff 에 서서
        /// **무엇이 실제로 달라졌는지가 묻힌다.**
        /// </summary>
        private BoardGrid _grid => _boards[Index(_editing)];
        private InputAction _press;
        private readonly Dictionary<Vector2Int, GameObject> _markers = new Dictionary<Vector2Int, GameObject>();
        private readonly Dictionary<Vector2Int, Color> _nodeColors = new Dictionary<Vector2Int, Color>(); // 현재 상태색(선택 복원용)

        /// <summary>칸마다 모듈 기호 자리 둘. 자리는 늘 있고 켜짐만 바뀐다.</summary>
        private readonly Dictionary<Vector2Int, SpriteRenderer[]> _moduleSymbols =
            new Dictionary<Vector2Int, SpriteRenderer[]>();
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
        /// <summary>벨트 몸통 렌더러(마커의 자식). 돌아가는 것은 이것 하나다.</summary>
        private readonly Dictionary<Vector2Int, SpriteRenderer> _beltBodies =
            new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> _beltArrows = new Dictionary<Vector2Int, SpriteRenderer>();
        // 미연결 경고 아이콘(§5-4 ⑤). 마커의 자식이라 마커 파괴 시 함께 사라진다.
        private readonly Dictionary<Vector2Int, SpriteRenderer> _beltWarnings = new Dictionary<Vector2Int, SpriteRenderer>();

        /// <summary>노드 면에 붙은 입출력 표시. 마커의 자식이라 마커 파괴 시 함께 사라진다.</summary>
        private struct PortMarker
        {
            public SpriteRenderer sr;
            public PortIO io;
            public FlowKind declared; // 포트에 적힌 품목 — 출력은 조합표가 덮는다
            public bool hasArt;       // 그림이 붙은 포트는 품목색을 안 칠한다(색은 아트가 갖는다)
        }

        private readonly Dictionary<Vector2Int, List<PortMarker>> _portMarkers =
            new Dictionary<Vector2Int, List<PortMarker>>();

        /// <summary>마지막 진단. 병목 힌트가 「무엇부터」를 고르는 데 쓴다.</summary>
        private IReadOnlyList<NodeDiagnostic> _lastDiagnostics;

        // ⚠️ **폐기 — 제거는 모드가 아니라 제스처다**(2026-09-15 사용자 확정 · 육안 ⑧).
        // 벨트 위에서 끌면 지워지고, 노드가 섞이면 컨펌을 받는다. 필드는 남겨 두지 않는다 —
        // 직렬화되는 값이 아니라 런타임 상태였다.

        /// <summary>
        /// 지금 고른 팔레트 카테고리 (2026-09-15 · 하단 개편 ②).
        ///
        /// ⚠️ **「전체」에서 시작한다** — 처음 여는 사람에게 걸러진 목록을 주면
        /// 「나머지는 어디 갔나」가 먼저 생긴다.
        /// </summary>
        private PaletteCategory _tab = PaletteCategory.All;


        /// <summary>
        /// 그 칸이 **코어**인가 — 코어는 제거할 수 없다 (2026-09-15 사용자 확정 · 개편 ①).
        ///
        /// 코어가 없으면 아무것도 못 만들고, 만들 것이 없으니 다시 놓지도 못한다 —
        /// **되돌릴 수 없는 상태**가 되는 유일한 칸이라 규칙이 따로 있다.
        /// </summary>
        private bool IsCore(Vector2Int cell)
        {
            NodeInstance n = _grid != null ? _grid.GetAt(cell) : null;
            return n != null && n.Definition != null && n.Definition.type == NodeType.Core;
        }

        /// <summary>컨펌을 기다리는 제거 경로. null 이면 팝업이 안 뜬다(육안 ⑧).</summary>
        private List<Vector2Int> _pendingRemoval;

        /// <summary>
        /// 선택된 벨트 요소(병합기·분류기). 없으면 탭은 노드를 놓는다.
        ///
        /// 직선·코너는 **드래그**가 만들고 경로가 방향을 정한다. 병합기·분류기는 방향이 여러 개라
        /// 경로로 표현되지 않으므로 **탭**으로 놓고, 면은 이웃에서 다시 잡는다(BeltAutoOrient).
        /// </summary>
        private BeltElementKind? _elementMode;

        /// <summary>
        /// 고른 모듈. −1이면 모듈 모드가 아니다 (2026-09-09 신설).
        ///
        /// **조작은 「고르고 → 놓인 노드를 탭」이다.** 모듈은 보드에 놓는 것이 아니라
        /// **노드에 붙는 것**이라(범위 효과 폐기 · 2026-08-31) 빈 칸을 탭할 자리가 없다.
        /// </summary>
        private int _selectedModule = -1;

        private static readonly Color SelectedColor = new Color(0.98f, 0.85f, 0.30f, 1f);
        private static readonly Color BeltColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color BeltArrowColor = new Color(0.95f, 0.85f, 0.3f, 1f);       // 미연결(dangling)
        private static readonly Color BeltConnectedColor = new Color(0.35f, 0.9f, 0.4f, 1f);    // 자동연결됨
        private static readonly Color BeltWarningColor = new Color(0.98f, 0.45f, 0.25f, 1f);   // 끝단 미연결 경고
        private static readonly Color GridBgColor = new Color(0.11f, 0.13f, 0.17f, 0.55f);      // 설치 영역 배경
        private static readonly Color GridLineColor = new Color(0.40f, 0.85f, 0.60f, 0.35f);    // 셀 경계선
        private static readonly Color GridBorderColor = new Color(0.45f, 0.9f, 0.65f, 0.85f);   // 바깥 테두리
        private static readonly Color PanDimColor = new Color(0.03f, 0.05f, 0.08f, 0.55f);     // 이동 모드 흐림 막

        /// <summary>
        /// 튜토리얼 어둠막의 불투명도 — **가정 0.55** (2026-09-11 · 플랜 §68-5 ①).
        ///
        /// 이동 모드 흐림 막과 같은 값으로 뒀다. **UI 문서에 이 막의 절이 없다** —
        /// `UiPlate.Alpha` 와 같은 역기입 자리이며 값이 서면 여기 하나만 바뀐다.
        ///
        /// ⚠️ **바탕 판·이동 흐림과 곱해지는 자리가 있다** — 셋이 겹치면 두 번 세 번 어두워진다.
        /// 겹치는 범위는 설계가 UI 문서에 함께 적는다(`260911_W01` 3장).
        /// </summary>
        private static readonly Color TutorialDimColor = new Color(0.03f, 0.05f, 0.08f, 0.55f);

        /// <summary>고스트 칸 밖을 덮는 조각들. 한 칸에 하나다 — 구멍을 내려면 조각이어야 한다.</summary>
        private GameObject _tutorialDim;

        /// <summary>지금 막이 비켜 준 칸들. 바뀔 때만 다시 짓는다.</summary>
        private readonly HashSet<Vector2Int> _dimExempt = new HashSet<Vector2Int>();

        /// <summary>튜토리얼 고스트 — **월드 스프라이트**다(2026-09-11 · 스크롤을 따라간다).</summary>
        private GameObject _ghostView;
        private SpriteRenderer _ghostRenderer;
        private static readonly Color HintColor = new Color(0.98f, 0.72f, 0.25f, 0.92f);       // 병목 힌트 바탕(경고 톤)

        /// <summary>
        /// 상단 경고 띠 바탕 — **빨강**. 조립 층의 색 축이며 「못 쓴다」다(UI 문서 12-6).
        /// ⚠️ **정확한 색값은 문서에 없다** — 전력 100% 초과가 쓰는 빨강(`HudBars` UsageOver)과
        /// 같은 계열로 맞췄다. 셋이 같은 뜻이므로 색이 갈리면 뜻이 셋으로 읽힌다.
        /// </summary>
        private static readonly Color WarningBandColor = new Color(0.92f, 0.30f, 0.28f, 0.92f);

        /// <summary>띠 글자 — 바탕이 진해 흰 글자가 읽힌다.</summary>
        private static readonly Color WarningBandText  = new Color(1f, 0.97f, 0.95f);
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
        // ⚠️ **`FlowLabel` 을 걷었다** (2026-09-10 사용자 확정 · 리허설 1차).
        // 흐르는 품목을 한 글자로 적던 표였는데, **품목 그림 열둘이 배선되면서 자리표시가 끝났다.**
        // 대응(품목 → 그림)은 `BoardArtSet.ItemSprite` 가 들고 있다 — 표를 두 곳에 두지 않는다.


        /// <summary>
        /// 노드 라벨. 군수 노드는 **종류가 아니라 지금 만드는 것**을 적는다 —
        /// 넷 다 「군수」라고 적혀 있으면 보드에서 갈리지 않는다.
        /// </summary>
        private static string NodeLabel(NodeInstance inst)
        {
            if (inst == null || inst.Definition == null) return "";

            // ⚠️ **군수는 「군수」만 적는다**(2026-09-15 사용자 확정 · 하단 개편 ⑥).
            //
            // 종전에는 「군수:관통」처럼 **지금 만드는 것**까지 적었다. 그 정보는 이제
            // **출력 품목 아이콘**이 말하므로(개편 ⑦), 글자로 또 적으면 같은 말이 둘이 된다.
            // 구 분기(드론·추진·쉴드·탄종)는 폐기 표기로 남긴다.
            return inst.Definition.displayName;
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
        /// <summary>마운트 포트 마커들 — 재고 0일 때 빨갛게 점멸시키려고 들고 있는다.</summary>
        private readonly List<SpriteRenderer> _mountPortViews = new List<SpriteRenderer>();

        /// <summary>마운트 재고 0 — **빨강**(조립 층 색 축 · UI 문서 12-6 「못 쓴다」).</summary>
        private static readonly Color MountEmptyColor = new Color(0.92f, 0.30f, 0.28f, 1f);

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
        private void RefreshTutorialGhost(Vector2Int? cell)
        {
            // ⚠️ **월드 스프라이트다 — 종전에는 `OnGUI` 가 화면 좌표로 그렸다**
            // (2026-09-11 재육안 2 ③). 보드를 끌면 고스트가 **딸려 왔다**: 그리는 것이
            // 화면 사각이라 스크롤 중 칸에서 떨어져 보였다. **이름표와 같은 뿌리**이며
            // 「보드 위에 있는 것을 화면 좌표로 그렸다」가 그 뿌리다.
            //
            // 보드와 같은 층에 두면 칸에 고정되므로 **스크롤·배율을 공짜로 따라간다.**
            // 층은 타일 위 · 품목 아래(`ZoneLineOrder`) — 물건이 지나가는 것을 안 가린다.
            if (!cell.HasValue)
            {
                if (_ghostView != null) Destroy(_ghostView);
                _ghostView = null;
                _ghostRenderer = null;
                return;
            }

            if (_ghostView == null)
            {
                _ghostView = new GameObject("TutorialGhost");
                _ghostView.transform.SetParent(transform, false);
                _ghostRenderer = _ghostView.AddComponent<SpriteRenderer>();
                _ghostRenderer.sprite = UnitSprite();
                _ghostRenderer.sortingOrder = ZoneLineOrder;
            }

            _ghostView.transform.position = CellWorld(cell.Value);
            _ghostView.transform.localScale = Vector3.one * _grid.CellSize;

            // 깜빡인다 — 보드에 색이 많아 가만히 있으면 묻힌다.
            float pulse = 0.35f + 0.25f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f));
            _ghostRenderer.color = new Color(1f, 0.92f, 0.35f, pulse);
        }

        /// <summary>「노는 중」 글자색. 종류색·상태 밝기와 겹치지 않게 무채색에 가깝게 둔다.</summary>
        private static readonly Color IdleLabelColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        /// <summary>모듈 기호 색. 강조색(주황) — 노드 이름·「노는 중」과 다른 축이다.</summary>
        private static readonly Color ModuleSymbolColor = new Color(1f, 0.62f, 0.20f, 0.95f);

        /// <summary>이 노드에 붙은 모듈의 기호를 이어 붙인 것. 없으면 빈 문자열.</summary>
        /// <summary>
        /// 글자 기호 — **그림이 없는 모듈만.**
        ///
        /// `260909_W01` 5장이 「글자로 두지 않는다」로 정했고 그 근거는 **정지 상태에 0.4가
        /// 곱해지면 글자가 대비로 읽혀 가장 먼저 사라진다**는 것이다. 그래서 그림이 있으면
        /// 글자를 안 낸다 — 둘을 겹쳐 내면 같은 것을 두 번 말한다.
        ///
        /// ⚠️ **그림이 없을 때까지 지우지는 않는다.** 자산이 빠진 판에서 글자마저 없으면
        /// 모듈이 붙었는지 자체를 화면에서 알 수 없다. 이 갈래는 **자산이 오기 전의 다리**다.
        /// </summary>
        private string ModuleSymbols(NodeInstance node)
        {
            if (node == null || node.ModuleCount == 0) return string.Empty;
            string s = string.Empty;
            for (int i = 0; i < NodeInstance.ModuleSlots; i++)
            {
                ModuleDefinition m = node.ModuleAt(i);
                if (m == null) continue;
                if (art != null && art.ModuleSprite(m.kind) != null) continue; // 그림이 대신한다
                s += m.symbol;
            }
            return s;
        }

        /// <summary>이 칸의 노드가 놀고 있는가. 일감률이 아직 안 실렸으면 「논다」고 말하지 않는다.</summary>
        /// <summary>
        /// 노드 타일 안에 모듈 기호 자리 둘을 만든다 (`260909_W01` 5장).
        ///
        /// **자리는 늘 만들고 보이는 것만 켠다** — 모듈은 놀다가 붙으므로, 붙을 때마다
        /// 오브젝트를 만들면 프레임마다 쓰레기가 난다. 자리와 켜짐은 다른 것이다.
        ///
        /// ⚠️ **노드 마커의 자식이지만 색은 물려받지 않는다.** 부모가 산출률 밝기로
        /// 어두워져도 기호는 그대로다 — 모듈이 붙어 있는가는 노드가 도는가와 무관하다.
        /// 스프라이트 렌더러는 부모 색을 상속하지 않으므로 자식으로 두어도 안전하다.
        /// </summary>
        private void SpawnModuleSymbols(Vector2Int cell, Transform parent)
        {
            if (art == null) return;

            var slots = new SpriteRenderer[NodeInstance.ModuleSlots];
            for (int i = 0; i < slots.Length; i++)
            {
                var go = new GameObject($"ModuleSymbol_{i}");
                go.transform.SetParent(parent, false);

                Vector2 offset = ModuleSymbolLayout.SlotOffset(i);
                // ⚠️ **부모가 칸 크기로 늘어나 있다.** 노드 마커의 localScale이 이미 칸에
                // 맞춰져 있으므로 여기서 다시 칸 크기를 곱하면 두 번 곱해진다 —
                // 자리는 부모 기준의 **비율**로 준다.
                go.transform.localPosition = new Vector3(offset.x, offset.y, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = ModuleSymbolLayout.Tint;
                // 노드 그림 위 · 마커 층 안에서만 움직인다(지침 §7 ［08-29］ — 전역 층을 안 섞는다).
                sr.sortingOrder = MarkerOrder + 1;
                sr.enabled = false;
                slots[i] = sr;
            }
            _moduleSymbols[cell] = slots;
            RefreshModuleSymbols(cell);
        }

        /// <summary>
        /// 그 칸의 기호를 지금 상태에 맞춘다. **빈 칸은 안 그린다**(W01 5장) —
        /// 자리표시를 남기면 「붙일 수 있는 칸이 둘」이 아니라 「모듈이 둘」로 읽힌다.
        /// </summary>
        private void RefreshModuleSymbols(Vector2Int cell)
        {
            if (!_moduleSymbols.TryGetValue(cell, out SpriteRenderer[] slots)) return;

            NodeInstance node = _grid != null ? _grid.GetAt(cell) : null;
            for (int i = 0; i < slots.Length; i++)
            {
                SpriteRenderer sr = slots[i];
                if (sr == null) continue;

                ModuleDefinition m = node != null ? node.ModuleAt(i) : null;
                Sprite sprite = m != null && art != null ? art.ModuleSprite(m.kind) : null;

                // 그림이 없으면 **안 그린다.** 색 사각으로 대신하면 노드 안의 얼룩이 된다 —
                // 노드·품목과 달리 여기는 자리를 알려 주는 것이 아니라 무엇인지를 알려 주는 자리다.
                bool show = ModuleSymbolLayout.SlotIsVisible(m) && sprite != null;
                sr.enabled = show;
                if (!show) continue;

                sr.sprite = sprite;
                sr.transform.localScale =
                    Vector3.one * FitScale(sprite, ModuleSymbolLayout.SymbolSize);
            }
        }

        /// <summary>상태 표식의 한 변 = 칸의 몇 할인가. ⚠️ **가정 0.3** — 규격 문서에 절이 없다.</summary>
        private const float StatusIconCellFraction = 0.3f;

        // ── 마운트 표시 (2026-09-14 · 플랜 §71-41) ──────────────────────────────
        //
        // §72-5 사용자 확정으로 **가정 넷이 폐기됐다** — 1슬롯 = 보드 한 칸이다.

        /// <summary>모자라는 색 사각 — 보드 계열의 짙은 무채색. 노드색과 겹치지 않는다.</summary>
        private static readonly Color MountFallbackColor = new Color(0.38f, 0.40f, 0.43f, 0.95f);

        /// <summary>빈 슬롯 테두리 — 칸이 **몇 개인지**는 비어 있어도 보여야 한다.</summary>
        private static readonly Color MountSlotEdgeColor = new Color(0.62f, 0.60f, 0.55f, 0.90f);

        /// <summary>슬롯 테두리 굵기(칸 비율). 셀선과 같은 결의 가늘기다.</summary>
        private const float MountSlotEdgeWidth = 0.035f;

        /// <summary>
        /// 품목색 틴트의 불투명도. ⚠️ **가정 0.55** — 규격 문서에 절이 없다(설계 역기입 자리).
        ///
        /// 불투명하면 아래 마운트 그림이 통째로 가려 **무엇에 딸린 적재인지**가 사라지고,
        /// 너무 옅으면 **무슨 탄인지**가 안 읽힌다. 둘 사이다.
        /// </summary>
        private const float MountSlotTintAlpha = 0.55f;

        // ⚠️ **1슬롯 = 보드 한 칸**(2026-09-14 · §72-5 사용자 확정).
        // 첫 판은 마운트 그림 위에 **작은 그리드**를 얹고 칸 크기·틈·띄움을
        // 가정 넷으로 두었는데, 그 넷은 **폐기됐다** — 슬롯이 보드 칸과 같으면
        // **재는 자가 화면에 이미 있다**(옆 칸이 눈금이다).

        private readonly List<SpriteRenderer> _mountBodyViews = new List<SpriteRenderer>();
        private readonly List<Vector2Int> _mountBodyCells = new List<Vector2Int>();
        private readonly List<MountOwner> _mountBodyOwners = new List<MountOwner>();
        private readonly List<bool> _mountBodyHasArt = new List<bool>();

        /// <summary>쉴 때의 색 — **파츠 틴트**. 점멸이 끝나면 여기로 돌아온다(육안 6차 ④).</summary>
        private readonly List<Color> _mountBodyRest = new List<Color>();

        /// <summary>슬롯 칸 하나의 채움 막대 — 매 프레임 높이와 색이 바뀐다.</summary>
        private readonly List<SpriteRenderer> _mountSlotFills = new List<SpriteRenderer>();
        private readonly List<int> _mountSlotIndex = new List<int>();
        private readonly List<MountOwner> _mountSlotOwners = new List<MountOwner>();
        private readonly List<Vector2Int> _mountSlotCells = new List<Vector2Int>();
        private readonly List<SpriteRenderer> _mountSlotEdges = new List<SpriteRenderer>();

        /// <summary>슬롯마다 **무엇이 실렸는지** 그리는 아이콘 (§72-24 ③).</summary>
        private readonly List<SpriteRenderer> _mountSlotIcons = new List<SpriteRenderer>();

        /// <summary>테두리 하나하나의 임자 — **활성 로봇 것만 보인다**(§72-24 ②).</summary>
        private readonly List<MountOwner> _mountSlotEdgeOwners = new List<MountOwner>();


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
            // 그림이 붙은 칸은 **색을 아트가 갖는다** — 코드가 종류색을 곱하면 축이 둘이 된다.
            Color baseColor = NodeArtOf(cell) != null
                ? Color.white
                : inst != null && inst.Definition != null
                    ? NodeTypeColor(inst.Definition.type)
                    : NodeBaseColor;

            // ✅ **직전 단계를 보고 정한다**(2026-09-21 사용자 육안 — 노드 깜빡임).
            //    문턱 하나로는 1.000 언저리의 떨림이 그대로 밝기 튐이 된다.
            float prev = _nodeTints.TryGetValue(cell, out float had) ? had : NodeStatusTint.Normal;
            float tint = NodeStatusTint.Of(ratio, prev);
            _nodeTints[cell] = tint;

            Color c = baseColor * tint;
            c.a = baseColor.a; // 알파는 밝기 축이 아니다 — 곱하면 노드가 투명해진다
            return c;
        }

        /// <summary>노드 상태색 적용(§L4-R #5). 진단은 Provider(LogisticsDiagnostics)가 공급 — UI는 색 매핑만.
        /// 선택 중인 셀은 선택 하이라이트 유지.</summary>
        /// <summary>
        /// 칸마다의 **직전 밝기 단계** — 이력 문턱이 이것을 본다(2026-09-21).
        /// ⚠️ 색(<c>_nodeColors</c>)에서 되읽지 않는다: 아트 색이 곱해져 있어
        /// 나눠 되돌리면 자산마다 다른 수가 나온다.
        /// </summary>
        private readonly Dictionary<Vector2Int, float> _nodeTints =
            new Dictionary<Vector2Int, float>();

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
            //
            // ⚠️ **판 둘을 같은 자리에 겹쳐 세운다**(2026-09-16). 원점도 마스크도 같다 —
            // 다른 것은 **주인**뿐이고, 주인이 살아 있는 마운트 포트를 가른다.
            // 겹쳐 세우므로 좌표·스크롤·배경은 한 벌로 족하다.
            // 마스크는 판마다 따로 짓는다 — 한 벌을 나눠 쓰면 한쪽을 고칠 때 둘이 같이 바뀐다.
            _boards[Index(MountOwner.RobotA)] = new BoardGrid(config.columns, config.rows,
                config.cellSize, origin,
                config.usePartLayout ? PartLayout.BuildMask() : null, MountOwner.RobotA);
            _boards[Index(MountOwner.RobotB)] = new BoardGrid(config.columns, config.rows,
                config.cellSize, origin, config.usePartLayout ? PartLayout.BuildMask() : null,
                MountOwner.RobotB);

            _baseWorldPosition = transform.position;
            // ⚠️ **가로 여유가 없어졌다**(2026-09-14 · A 묶음도 격자 안으로 들어왔다).
            // A 는 팔R 아래 빈 칸(x1 · y0~3), B 는 머리 옆 빈 열(x3 · x8) —
            // **바깥으로 나가는 것이 하나도 없다.** 격자 폭 그대로 조인다.
            //
            // ⚠️ **세로는 더하지 않는다 — `config.rows` 가 이미 14 칸이다**(§72-6).
            // 마운트 전용 줄(y13)이 격자 **안**에 생겨서, 보드 높이가 그만큼 늘면
            // 스크롤 범위도 따라 늘어 맨 윗줄까지 닿는다. 구판은 13 칸이었다.
            _pan = new BoardPan(
                new Vector2(config.columns * config.cellSize, config.rows * config.cellSize),
                new Vector2(viewSizeCells.x * config.cellSize, viewSizeCells.y * config.cellSize));

            BuildGridVisual(); // §C-4 설치 가능 그리드 영역 표시(런타임).
            BuildDimOverlay(); // 이동 모드 표시(UI 문서 9-2)
            ApplyModeVisual();
            // 판 둘을 다 세운다 — **대기 로봇 판도 돈다**(사용자 확정). 그림은 그 뒤 한 번.
            ApplyInitialLayout(_boards[Index(MountOwner.RobotA)]);
            ApplyInitialLayout(_boards[Index(MountOwner.RobotB)]);

            // ⚠️⚠️ **판 둘의 벨트를 다 풀어 준다**(2026-09-21 사용자 관찰 —
            //    「B 탭을 열기 전까지 B 물류가 생산을 안 한다」).
            //
            // 🗑️ 구 거동 폐기 — 여기서는 노드만 놓고 **연결은 안 풀었다.**
            //    `RefreshConnections` 는 `_grid`(편집 중인 판) 하나만 보므로, B 는
            //    면·품목·링크가 **한 번도 안 잡힌 채** 서 있었다. 노드는 다 놓여 있는데
            //    벨트가 아무것도 안 나르니 **에러 없이 산출만 0** 이었다.
            //    탭을 누르면 그제야 `RespawnMarkersFromGrid` → `RefreshConnections` 가
            //    돌아 흐르기 시작한다 — 사용자가 본 그대로다.
            //
            // 📌 09-18 「대기 보드 드론 도착이 통째로 버려진다」와 **같은 뿌리의 다음 층**이다.
            //    그때는 도착을 안 실어 보냈고, 이번에는 **애초에 흐르지 않았다.**
            ResolveFlow(_boards[Index(MountOwner.RobotA)], _flows[Index(MountOwner.RobotA)]);
            ResolveFlow(_boards[Index(MountOwner.RobotB)], _flows[Index(MountOwner.RobotB)]);

            RespawnMarkersFromGrid();
        }

        // 시작 배치를 깐다. 배치 경로는 플레이어 조작과 동일(TryPlace + 마커) — 별도 경로를 만들지 않는다.
        /// <summary>
        /// 비워 둔 칸을 채우는 **운반로 벨트 한 칸**을 놓는다
        /// (2026-09-11 설계 확정 (가) · 구 「기초 군수 노드」 폐기(지금 이름 「기초 가공소」) · 그 앞은 병합기였다).
        ///
        /// 세 판 모두 같은 이유로 바뀌었다 — **놓기 전에 0 이 되는 자리**를 찾아온 것이다.
        /// 병합기는 합칠 갈래가 없어졌고, 기초 가공소는 나머지 세 줄이 흘러 0 이 안 됐다.
        /// 합류 뒤 외길의 벨트 한 칸이 그 조건을 만족한다.
        /// </summary>
        private void PlaceTutorialFill(BoardGrid grid)
        {
            // ⚠️ **튜토리얼은 A 만이다**(2026-09-16 사용자 확정) — B 판에 채우면
            //    배운 적 없는 칸이 메워져 있다.
            if (grid.Owner != MountOwner.RobotA) return;

            StartingBoard.Run run = StartingBoard.FillsEmptySlot;
            if (grid.HasBelt(run.cell) || grid.IsOccupied(run.cell)) return;

            grid.TryPlaceBelt(run.cell, run.inFace, run.outFace, FlowKind.None, out _);
        }

        // 시작 배치가 쓰는 노드 자산을 인스펙터 목록에서 찾는다. 시작 보드는 id로만 적고
        // 자산 참조를 갖지 않으므로(순수 데이터), 씬 쪽에서 이어 준다.
        private NodeDefinition FindStartingNode(string nodeId)
        {
            if (initialLayout != null)
                foreach (InitialNode item in initialLayout)
                    if (item.node != null && item.node.nodeId == nodeId) return item.node;

            if (palette != null)
                foreach (NodeDefinition d in palette)
                    if (d != null && d.nodeId == nodeId) return d;

            // ⚠️⚠️ **팔레트에 없는 노드도 시작 보드에 설 수 있다**
            //    (2026-09-16 · 플랜 브라우저 확인 ① — 「노드 'munix' 못 찾음」).
            //
            // 종전에는 찾는 곳이 **A 의 시작 배치**와 **팔레트** 둘뿐이었다. 그런데
            // B 의 시작 보드는 **복합 가공소(`munix`)** 를 쓰고 그것은 둘 중 어디에도 없다 —
            // 팔레트는 「플레이어가 놓을 수 있는 것」이라 뜻이 다르고, A 의 배치에는
            // 복합 가공소가 안 쓰인다. 그래서 B 판이 **한 칸 빈 채로** 섰다.
            //
            // 📌 **팔레트에 넣어 메우지 않는다.** 그러면 「놓을 수 있는 것」이 조용히
            //    늘어난다 — 그것은 값·기획 판정이지 이 결함의 고침이 아니다.
            //    자산을 대는 주머니를 따로 둔다.
            if (startingNodePool != null)
                foreach (NodeDefinition d in startingNodePool)
                    if (d != null && d.nodeId == nodeId) return d;

            return null;
        }

        /// <summary>
        /// 저장된 판을 세운다. 없거나 격자 크기가 다르면 안 세우고 <c>false</c>.
        ///
        /// ⚠️ **놓는 길은 시작 보드와 같다** — `TryPlace` · `TryPlaceBeltElement` 에
        /// 같은 마커 생성을 붙인다. 복원용 별도 경로를 만들면 **둘 중 한쪽만**
        /// 고쳐지는 버그가 생긴다(이 리포에서 여러 번 나온 모양이다).
        ///
        /// ⚠️ 못 놓은 것이 있어도 **통째로 버리지 않는다** — 자산 하나가 없어졌다고
        /// 나머지 판까지 날리는 편이 더 나쁘다. 다만 몇 개를 못 놓았는지는 남긴다.
        /// </summary>
        private bool TryRestoreSavedBoard(BoardGrid grid)
        {
            BoardStateV1 saved = IdleSignals.BoardStateOf(grid.Owner);
            if (!BoardStateCodec.Fits(saved, grid))
            {
                // ⚠️ **왜 버리는지 남긴다**(2026-09-16 · 육안 ⑦). 저장이 조용히 사라지면
                //    플레이어는 「내가 놓은 것이 없어졌다」만 겪고 이유를 못 본다.
                if (saved != null)
                {
                    Debug.LogWarning($"[MBI] 저장된 보드({grid.Owner})를 버린다 — "
                                     + BoardStateCodec.WhyNotFit(saved, grid)
                                     + ". 시작 보드로 시작한다(플레이어 배치도 함께 사라진다).");
                    IdleSignals.SetBoardState(grid.Owner, null);
                }
                return false;
            }

            // ⚠️ **마커는 안 짓는다**(2026-09-16). 그림은 `RespawnMarkersFromGrid` 한 곳이
            //    판을 읽어 짓는다 — 안 보이는 판에까지 마커를 세우지 않기 위해서다.
            int missed = BoardStateCodec.Restore(
                saved, grid, FindStartingNode, FindModuleById, NoMarker, NoBeltMarker);

            if (missed > 0)
                Debug.LogWarning($"[MBI] 저장된 보드({grid.Owner})에서 {missed} 개를 못 놓았다 — "
                                 + "자산 id 가 바뀜었거나 칸이 막혔다. 나머지는 그대로 섬.");

            return true;
        }

        /// <summary>
        /// **편집 중인 판을 읽어 마커를 통째로 다시 짓는다** (2026-09-16 신설 · 보드 둘).
        ///
        /// 📌 **그림을 짓는 자리는 여기 하나다.** 시작 배치 · 저장 복원 · 탭 전환이
        /// 전부 이 길로 온다 — 놓는 곳마다 마커를 붙이던 구 방식은 판이 둘이 되는 순간
        /// **안 보이는 판에도 마커를 세운다**(지침 §7).
        ///
        /// ⚠️ **한 판 통째로 다시 짓는 것이 비싸지 않다.** 유효 칸이 117 이고 Awake 가
        /// 이미 같은 일을 한 번 한다. 탭은 프레임마다 눌리지 않는다.
        /// </summary>
        private void RespawnMarkersFromGrid()
        {
            ClearMarkers();

            BoardGrid grid = _grid;
            if (grid == null) return;

            for (int x = 0; x < grid.Columns; x++)
            for (int y = 0; y < grid.Rows; y++)
            {
                var cell = new Vector2Int(x, y);
                if (grid.GetAt(cell) != null) { SpawnNodeMarker(cell); continue; }

                BeltInstance belt = grid.GetBeltAt(cell);
                if (belt == null) continue;
                // 출력면이 여럿일 수 있다(분류기) — 마커는 **첫 면**으로 세운다.
                // 그림의 배향은 `BeltArtOf` 가 벨트 자체를 보고 다시 정한다.
                PortFace outFace = belt.OutFaces != null && belt.OutFaces.Length > 0
                    ? belt.OutFaces[0] : PortFace.East;
                SpawnBeltMarker(cell, outFace);
            }

            RefreshConnections();
        }

        /// <summary>마커를 전부 지운다 — 탭을 옮기기 전에 부른다.</summary>
        private void ClearMarkers()
        {
            foreach (KeyValuePair<Vector2Int, GameObject> kv in _markers)
                if (kv.Value != null) Destroy(kv.Value);
            foreach (KeyValuePair<Vector2Int, GameObject> kv in _beltMarkers)
                if (kv.Value != null) Destroy(kv.Value);

            // ⚠️ **자식으로 달린 것은 목록만 비운다** — 포트 탭·모듈 기호·몸통·화살표·경고·무늬는
            //    전부 위 두 마커의 자식이라 부모와 함께 사라진다. 여기서 또 Destroy 하면
            //    이미 죽은 것을 건드린다.
            _markers.Clear();
            _nodeColors.Clear();
            _portMarkers.Clear();
            _moduleSymbols.Clear();
            _beltMarkers.Clear();
            _beltBodies.Clear();
            _beltArrows.Clear();
            _beltWarnings.Clear();
            _beltFlows.Clear();

            // 고른 칸은 판마다 다르다 — 옮기면 테두리를 내린다.
            _selected = null;
            if (_selectRing != null) _selectRing.SetActive(false);
        }

        /// <summary>
        /// **편집할 판을 고른다** — 조립 화면 로봇 탭이 부른다 (2026-09-16 · 사용자 확정).
        ///
        /// 판을 바꾸는 것은 **그림과 편집 대상**뿐이다 — 둘 다 매 틱 돌고 있고,
        /// 그 틱은 <see cref="LogisticsOutputProvider"/> 가 판별로 따로 돌린다.
        /// </summary>
        public void SetEditing(MountOwner owner)
        {
            if (_editing == owner) return;
            _editing = owner;
            RespawnMarkersFromGrid();
        }

        /// <summary>
        /// **로봇 B 의 시작 보드를 세운다** (2026-09-16 · 사용자 확정 · 플랜 §74-16 ②).
        ///
        /// A 와 달리 **완성본**이다 — 튜토리얼 대상이 아니라 비워 둘 칸이 없다.
        /// 자리는 <see cref="StartingBoardB"/> 가 든다(가정 · 설계 사후 역기입).
        ///
        /// ⚠️ 조합표를 **같이 넣는다.** 안 넣으면 가공 노드가 기본 조합표로 돌아
        /// 배터리 줄이 통째로 죽는다 — 에러 없이 도착만 0 이 된다.
        /// </summary>
        private void ApplyStartingBoardB(BoardGrid grid)
        {
            // ⚠️⚠️ **놓는 손은 `StartingBoardB.Apply` 하나다**(2026-09-18 정정).
            //
            // 🗑️ 구 코드 폐기 — 여기에 **같은 일을 하는 자리 둘째**가 있었다. 노드를 제가
            //    직접 놓느라 09-18 에 생긴 **회전 칸을 안 읽었고**, 그래서 화면에서는
            //    둘째 드론 줄의 노드 넷이 **안 돌린 채** 서서 「면이 다름」 경고가 떴다.
            //    하네스는 `Apply` 를 지나 0 칸으로 나왔으니, 둘이 **다른 판을 세우고 있었다.**
            //
            // 📌 알리는 일만 여기 남긴다 — `Apply` 는 조용히 건너뛰므로 무엇이 죽는지를 적는다.
            StartingBoardB.Apply(grid, FindStartingNode,
                onMissing: id => Debug.LogError(
                    $"[MBI] B 시작 보드: 노드 '{id}' 자산을 못 찾았다 — "
                    + "`startingNodePool` 에 없다(생성기를 다시 돌린다). "
                    + "그 칸이 비면 줄이 끊겨 **B 판이 아무것도 못 낸다.**"),
                onRecipeFail: (id, recipe) => Debug.LogWarning(
                    $"[MBI] B 시작 보드: {id} 가 조합표 {recipe} 를 못 받는다 — 기본값으로 선다."));
        }

        /// <summary>
        /// **편집 탭을 활성 로봇에 맞춘다** (2026-09-16 사용자 육안 · 플랜 §74-21 ②).
        ///
        /// 🗑️ **구 가정 「편집 축과 전투 축은 따로」 폐기.** 규칙으로는 깨끗했지만
        /// 써 보니 틀렸다 — B 로 교대해도 조립 화면은 A 판을 보여 주어
        /// **지금 싸우는 로봇의 줄을 못 봤다.**
        ///
        /// 맞추는 때는 **교대**와 **조립 진입** 둘뿐이다(규칙은 `BoardTabFollow`).
        /// 그 사이에 손으로 옮긴 탭은 **다음 교대까지 그대로 둔다** — 매 프레임 맞추면
        /// 손으로 B 를 열어 둔 채 A 로 싸울 수가 없다.
        /// </summary>
        private void FollowActiveRobotTab()
        {
            MountOwner active = SupplySignals.ActiveOwner;
            bool open = GameLayerController.BoardViewActive;

            if (BoardTabFollow.ShouldFollow(active, _lastSeenActiveOwner, open, _wasBoardOpen))
                SetEditing(active);

            _lastSeenActiveOwner = active;
            _wasBoardOpen = open;
        }

        /// <summary>직전 프레임에 본 활성 로봇 — 바뀌면 교대가 일어난 것이다.</summary>
        private MountOwner _lastSeenActiveOwner = MountOwner.RobotA;

        /// <summary>직전 프레임에 조립 화면이 열려 있었는가 — 닫힘 → 열림이 진입이다.</summary>
        private bool _wasBoardOpen;

        /// <summary>복원이 부르는 빈 통보 — 그림은 판을 읽어 따로 짓는다.</summary>
        private static void NoMarker(Vector2Int cell) { }
        private static void NoBeltMarker(Vector2Int cell, PortFace outFace) { }

        /// <summary>모듈 id 로 자산을 찾는다 — 복원이 쓴다. 없으면 null.</summary>
        private ModuleDefinition FindModuleById(string moduleId)
        {
            if (modulePalette == null || string.IsNullOrEmpty(moduleId)) return null;
            foreach (ModuleDefinition m in modulePalette)
                if (m != null && m.moduleId == moduleId) return m;
            return null;
        }

        /// <summary>
        /// 시작 배치(또는 저장된 판)를 **격자에만** 세운다 (2026-09-16 · 보드 둘로 갈리며 바뀜).
        ///
        /// 🗑️ **구 규칙 「놓으면서 마커도 같이 짓는다」는 폐기.** 판이 둘이 되면
        /// **안 보이는 판에도 마커를 짓게** 되고, 탭을 누를 때마다 지우고 다시 지어야 한다.
        /// 그림은 <see cref="RespawnMarkersFromGrid"/> 한 곳이 **판을 읽어** 짓는다 —
        /// 시작 배치든 저장 복원이든 탭 전환이든 **같은 길**이다(지침 §7).
        /// </summary>
        private void ApplyInitialLayout(BoardGrid grid)
        {
            // ⚠️⚠️ **저장된 판이 있으면 그것이 진실이다**(2026-09-16 · `Docs/board_save_design.md`).
            //
            // 종전엔 재입장할 때마다 **시작 보드를 다시 깔았다.** 플레이어가 늘린 판이
            // 통째로 사라졌고, 이 파일의 아래쪽 주석이 그것을 「진짜 구멍」이라고 적어 둔
            // 그 자리다. 이제 메운다.
            if (TryRestoreSavedBoard(grid)) return;

            // ⚠️⚠️ **씬에 적힌 시작 배치는 로봇 A 의 것이다.** B 의 시작 보드는 코드가 든다
            //    (`StartingBoardB`) — 씬 목록이 하나뿐이라 둘을 못 담는다.
            if (grid.Owner == MountOwner.RobotB) { ApplyStartingBoardB(grid); return; }

            // ⚠️⚠️ **A 도 코드의 배치를 쓴다**(2026-09-17 · 구 「씬의 `initialLayout` 을 깐다」 폐기).
            //
            // 종전에는 A 의 배치가 **씬 배열**에, B 의 것이 **코드**에 살았다. 이 파일의 옛
            // 주석이 그 자리를 「둘을 같이 고쳐야 할 때 한쪽을 빠뜨리기 쉽다」고 적어 두었고,
            // 09-17 에 **조합표가 자리에 들어오면서** 그것이 실제 위험이 됐다 —
            // 씬 배열에는 조합표 칸이 없어서 추진제 노드가 **표준탄으로 돌** 참이었다.
            // 그러면 부스터는 영영 빈 그릇이고 **회피가 한 번도 안 난다**(에러 없이).
            //
            // 📌 이제 배치는 `StartingBoard` 하나가 들고, 씬 배열은 **자산을 대는 주머니**로만
            //    남는다(`FindStartingNode` 가 거기서 노드를 찾는다).
            int placedCount = StartingBoard.Apply(grid, FindStartingNode);
            if (placedCount < StartingBoard.Nodes.Count)
                Debug.LogError($"[MBI] A 시작 보드: 노드 {StartingBoard.Nodes.Count} 중 "
                               + $"{placedCount} 만 섰다 — 자산을 못 찾았거나 칸이 막혔다. "
                               + "빠진 칸이 줄 가운데면 **그 줄이 통째로 안 흐른다.**");

            // 튜토리얼을 이미 끝냈으면 **비워 둔 칸이 채워진 채로 시작한다**(260902_W09 §1-1 안 A).
            //
            // 그러지 않으면 클리어한 사람이 다시 들어왔을 때 끊긴 보드를 보게 된다 —
            // 고스트도 안내도 없이 방금 배운 것을 다시 해야 하는 화면이다.
            //
            // ⚠️ 이것은 **근본 해결이 아니다.** 진짜 구멍은 보드 배치가 저장되지 않는 것이고,
            // 플레이어가 확장한 보드는 여전히 사라진다(불일치 목록 등재분).
            if (IdleSignals.TutorialCleared) PlaceTutorialFill(grid);
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

            // ⚠️ **분기보다 먼저 그린다**(2026-09-14 실측으로 고침).
            //
            // 넷이 `usePartLayout` 가지의 **`return` 뒤**에 있었다. 그 가지가 실제 보드이므로
            // **보드 바닥·마운트 포트·마운트 본체·적재 슬롯이 한 번도 안 그려졌다** —
            // 배선은 09-09 에 됐는데 화면에는 09-14 까지 없었고, **에러도 안 났다**(그릴
            // 것이 없으면 조용하다). 마운트 이름표가 안 보이던 것도 여기서 갈렸다:
            // `DrawMountLabels` 가 「본체가 없으면 안 그린다」로 먼저 빠져나갔기 때문이다.
            //
            // ⚠️ **가지와 무관한 것들이다** — 실루엣이 직사각이든 아니든 바닥과 마운트는
            // 같은 자리에 선다. 그래서 가지 **앞**이 제자리다.
            BuildBoardBackground(root.transform);
            BuildMountPorts(root.transform);
            BuildMountBodies(root.transform);
            BuildMountSlots(root.transform);

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
                    Color runColor = BoundaryColor(r, out bool outerRun);
                    float len = r.length * config.cellSize;
                    float ex = o.x + r.from.x * config.cellSize;
                    float ey = o.y + r.from.y * config.cellSize;
                    if (r.horizontal) SpawnDashedEdge(root.transform, ex + len * 0.5f, ey, len, true, runColor, outerRun);
                    else              SpawnDashedEdge(root.transform, ex, ey + len * 0.5f, len, false, runColor, outerRun);
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

        /// <summary>
        /// 보드 바닥 — **칸마다 한 장** (2026-09-09 배선).
        ///
        /// 캔버스가 192라 한 장이 정확히 한 칸이다(ArtSpec: 격자 한 칸 = 192px = 1 월드 유닛).
        /// 그래서 전투 배경처럼 나머지로 밀 것이 없다 — **격자와 그림이 이미 같은 눈금**이다.
        ///
        /// ⚠️ **임포트 설정을 안 건드린다.** 한 장을 Repeat 으로 늘리는 대신 복제해 깐다 —
        /// 그 설정은 `SpriteImportRules`가 `Art/` 전체에 한 규격으로 강제하고 있다.
        ///
        /// **실루엣 밖은 깔지 않는다.** 팔·다리 사이 빈칸까지 바닥을 두면 로봇 모양이 사라져
        /// 「파츠라는 제한 공간」이 화면에서 안 읽힌다(조립 문서 11장 · 유효 117칸).
        /// </summary>
        private void BuildBoardBackground(Transform parent)
        {
            if (art == null || art.boardBackground == null) return;

            float scale = FitScale(art.boardBackground, _grid.CellSize);
            for (int x = 0; x < _grid.Columns; x++)
            for (int y = 0; y < _grid.Rows; y++)
            {
                var cell = new Vector2Int(x, y);
                if (!_grid.IsInside(cell)) continue; // 실루엣 밖

                var go = new GameObject($"bg_{x}_{y}");
                go.transform.SetParent(parent, false);
                go.transform.position = CellWorld(cell);
                go.transform.localScale = Vector3.one * scale;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = art.boardBackground;
                sr.sortingOrder = BoardBackgroundOrder;

                // ⚠️ **칸마다 제 파츠 색으로 옅게 물들인다**(2026-09-11 · 플랜 §71-16 ⑧).
                // 종전에는 117칸이 전부 같은 바닥이라 **어느 칸이 어느 파츠인지**를
                // 점선이 끊기는 자리를 눈으로 좇아야만 알 수 있었다.
                // 색은 `SpriteRenderer.color` 로 **곱해지므로** 흰 쪽으로 당긴 값을 쓴다 —
                // 알파를 낮추면 투명해질 뿐 색이 안 섞인다(`PartPalette.FloorOf`).
                sr.color = PartPalette.FloorOf(PartLayout.PartAt(cell));
            }
        }

        /// <summary>
        /// 보드 바닥의 그리기 순서. 격자 배경(−3)보다 아래라 셀선·노드·품목이 전부 그 위에 남는다.
        ///
        /// ⚠️ **여기에 <c>SortingLayers.BackgroundFar</c>(−40)를 쓰지 않는다.** 보드는
        /// **자기 지역 순서**로 그린다(격자 배경 −3 · 셀선 −2 · 마커 0). 전역 정렬층을 섞으면
        /// 지침 §7 ［08-29］「전역 정렬층과 지역 그리기 순서 혼용」이 그대로 재현된다 —
        /// 그때 화살표가 벨트 뒤로 들어가 화면에서 사라졌다.
        /// </summary>
        private const int BoardBackgroundOrder = -4;

        /// <summary>
        /// 마운트 고정 포트 셋을 그린다(2026-09-09 신설 · <c>PartLayout.MountPorts</c>).
        ///
        /// **자리는 진작 데이터에 있었고 화면에만 없었다** — 보드의 산출이 전투로 넘어가는
        /// 곳인데 격자 위에 아무 표시가 없어, 왜 그 칸에서 라인이 끝나야 하는지가 안 보였다.
        /// 그림이 없으면 그리지 않는다 — 색 사각으로 대신하면 노드와 구분되지 않는다.
        /// </summary>
        private void BuildMountPorts(Transform parent)
        {
            // 다시 지을 때 묵은 렌더러가 남으면 죽은 것을 계속 칠하게 된다.
            _mountPortViews.Clear();
            if (art == null || art.mountPort == null) return;

            foreach (MountPort mp in PartLayout.MountPorts)
            {
                var go = new GameObject($"MountPort_{mp.owner}_{mp.face}");
                go.transform.SetParent(parent, false);
                go.transform.position = CellWorld(mp.cell);
                go.transform.localRotation = FaceRotation(PortFace.South, mp.face);
                go.transform.localScale = Vector3.one * FitScale(art.mountPort, _grid.CellSize);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = art.mountPort;
                // 셀선 위 · 노드 아래. 마운트 자리에 노드를 놓을 수 있고, 그때 노드가 위여야 한다.
                sr.sortingOrder = MarkerOrder - 1;
                _mountPortViews.Add(sr);
            }
        }

        /// <summary>
        /// **마운트 그리드 빨간 점멸** — 마운트 재고가 0일 때 (UI 문서 12-1·12-4 · `260909_W01` 3-1).
        ///
        /// **이것이 두 화면을 잇는 유일한 고리다.** 전투 화면은 결과를 내고 조립 화면은 원인을
        /// 내는데, 이 표시만 결과를 조립 쪽으로 끌고 온다 — 그래서 값이 있다(12-1).
        ///
        /// ⚠️ **마운트 칸 자체는 아직 화면에 없다.** 보드에 있는 것은 마운트 **포트 마커** 넷이며,
        /// 「그리드」라 부를 칸 표시가 코드에 없다. 그 마커가 마운트를 가리키는 유일한 자리라
        /// 거기에 걸었다 — **자산이 오면 옮길 자리**이고 판정 자리로 올린다.
        /// </summary>
        private void UpdateMountPortBlink()
        {
            // ⚠️ **점멸을 포트 마커에서 마운트 본체로 옮겼다**(2026-09-14 · §71-41 · UI 문서 12-1).
            // 종전에 마커에 걸려 있던 것은 **마운트 칸이 화면에 없었기 때문**이며,
            // 코드에도 「자산이 오면 옮길 자리」로 적혀 있었다. 이제 본체와 그리드가 있다.
            //
            // ⚠️ **포트 마커는 결합부로 남는다** — 흰색 고정이다. 결합부까지 같이 깜빡이면
            // 「탄약이 없다」와 「이 자리가 마운트다」가 한 신호로 뭉개진다.
            for (int i = 0; i < _mountPortViews.Count; i++)
            {
                SpriteRenderer pv = _mountPortViews[i];
                if (pv == null) continue;

                // ⚠️ **결합부도 활성 로봇 것만**(§72-24 ②) — 묶음이 사라졌는데 마커만 남으면
                // 「여기가 마운트인데 그림이 없다」로 읽힌다.
                bool show = i < PartLayout.MountPorts.Count
                            && ShowsMount(PartLayout.MountPorts[i].owner);
                if (pv.enabled != show) pv.enabled = show;
                if (show) pv.color = Color.white;
            }

            if (_mountBodyViews.Count == 0) return;

            // ⚠️ **점멸은 묶음 전체다**(§72-5) — 그림·슬롯 칸·이름표가 한 덩어리로 깜빡인다.
            bool on = MountBlinkOn;
            for (int i = 0; i < _mountBodyViews.Count; i++)
            {
                SpriteRenderer sr = _mountBodyViews[i];
                if (sr == null) continue;

                // ⚠️ **활성 로봇 것만 보인다**(§72-24 ②).
                bool show = ShowsMount(_mountBodyOwners[i]);
                if (sr.enabled != show) sr.enabled = show;
                if (!show) continue;

                // 그림이 없어 색 사각으로 선 것은 **제 색**으로 돌아간다 — 흰색으로 되돌리면
                // 폴백 사각이 흰 덩어리가 되어 노드와 구분되지 않는다.
                // ⚠️⚠️ **여기가 파츠 틴트를 매 프레임 지우고 있었다**
                // (2026-09-15 · 육안 6차 ④ — 「틴트 안 들어갔는데?」).
                //
                // 틴트는 `BuildMountBodies` 에서 **한 번** 넣었는데, 점멸을 되돌리는 이 줄이
                // **매 프레임 흰색으로 덮었다.** 한 프레임도 안 보였으니 「안 들어갔다」가 맞다.
                //
                // 📌 **짓는 곳과 되돌리는 곳이 다르면 되돌리는 쪽이 이긴다** — 짓는 쪽에만
                // 넣고 끝내면 안 된다. 쉴 때 색을 **들고 있다가** 그것으로 되돌린다.
                Color rest = _mountBodyHasArt[i] ? _mountBodyRest[i] : MountFallbackColor;
                sr.color = on ? MountEmptyColor : rest;
            }

            for (int i = 0; i < _mountSlotEdges.Count; i++)
            {
                SpriteRenderer edge = _mountSlotEdges[i];
                if (edge == null) continue;

                // ⚠️ **활성 로봇 것만 보인다**(§72-24 ②).
                bool show = i < _mountSlotEdgeOwners.Count && ShowsMount(_mountSlotEdgeOwners[i]);
                if (edge.enabled != show) edge.enabled = show;
                if (show) edge.color = on ? MountEmptyColor : MountSlotEdgeColor;
            }

            UpdateMountSlotFills();
        }

        /// <summary>
        /// 슬롯 칸의 **채움**을 매 프레임 맞춘다 (2026-09-14 · §72-5).
        ///
        /// ⚠️ **아래에서 위로 찬다.** 위에서 내려오면 「줄어드는 것」으로 읽힐다 —
        /// 슬롯 번호가 위로 쌓이는 것과 같은 방향이다.
        ///
        /// ⚠️ **지금 나선 로봇의 슬롯만 값이 있다**(`SupplySignals` 가 그것만 나른다).
        /// 대기 중인 로봇의 묶음은 **빈 칸로** 선다 — 0 으로 그리는 것이 아니라
        /// 「값이 없다」를 그대로 두는 것이다.
        /// </summary>
        private void UpdateMountSlotFills()
        {
            float cell = _grid != null ? _grid.CellSize : config.cellSize;

            for (int i = 0; i < _mountSlotFills.Count; i++)
            {
                SpriteRenderer sr = _mountSlotFills[i];
                if (sr == null) continue;

                // ⚠️⚠️ **기준이 「싸우는 로봇」에서 「보고 있는 판」으로 바뀌었다**
                //    (2026-09-16 · 로봇 탭이 생겼다 · `ShowsMount` 와 같은 뿌리).
                //
                // 옛 기준이면 B 판을 편집하는 내내 **B 칸이 전부 빈 칸으로** 보인다 —
                // A 가 싸우는 중이라 `ActiveOwner` 가 A 이기 때문이다. 실제로는 B 보드가
                // 돌며 B 마운트를 채우고 있는데 화면만 0 을 말하는 꼴이다.
                MountOwner shown = _mountSlotOwners[i];
                bool mine = shown == _editing;
                int slot = _mountSlotIndex[i];

                MountItem item = mine && slot < SupplySignals.MountSlotCountOf(shown)
                    ? SupplySignals.MountSlotItemOf(shown)[slot] : MountItem.None;

                float ratio = item == MountItem.None ? 0f
                    : MountDisplay.FillRatio(SupplySignals.MountSlotAmountOf(shown)[slot],
                                             SupplySignals.MountStackLimitOf(shown));

                // ⚠️ **무엇이 실렸는지는 그림이 말한다**(2026-09-14 · §72-24 ③).
                //
                // 틴트만 두면 **색을 외워야** 읽힌다. 아이콘을 깔면 색은 「얼마나 찼나」로,
                // 그림은 「무엇이 실렸나」로 역할이 갈린다 — 틴트는 그대로 얹힌다.
                //
                // ⚠️ **그림이 없으면 안 그린다** — 폴백 사각을 놓으면 「무엇인지 모를 것이
                // 실렸다」가 되어 빈 칸보다 나쁘다.
                if (i < _mountSlotIcons.Count && _mountSlotIcons[i] != null)
                {
                    SpriteRenderer ic = _mountSlotIcons[i];
                    Sprite want = item == MountItem.None || art == null
                        ? null : art.ItemSprite(MountItemMap.ToFlow(item));

                    bool showIcon = want != null;
                    if (ic.enabled != showIcon) ic.enabled = showIcon;
                    if (showIcon)
                    {
                        if (ic.sprite != want)
                        {
                            ic.sprite = want;
                            float u = FitScale(want, cell);
                            ic.transform.localScale = new Vector3(u, u, 1f);
                        }
                        ic.color = Color.white;
                    }
                }

                // ⚠️ **틴트다**(2026-09-14 · §72-13) — 칸을 색으로 덮는 것이 아니라
                // **그림 위에 품목색을 얹는다.** 빈 칸은 얹지 않아 그림만 남는다.
                if (ratio <= 0f) { if (sr.enabled) sr.enabled = false; continue; }
                if (!sr.enabled) sr.enabled = true;

                // 칸 밑변에 발을 맞추고 위로 늘린다 — 채움 비율은 그대로다.
                Vector3 c = CellWorld(_mountSlotCells[i]);
                float h = cell * ratio;
                sr.transform.position = new Vector3(c.x, c.y - cell * 0.5f + h * 0.5f, 0f);
                sr.transform.localScale = new Vector3(cell, h, 1f);

                // 색은 **벨트와 같은 표**에서 온다 — 같은 탄이 두 곳에서 다른 색이면 안 된다.
                // ⚠️ **반투명이다** — 불투명하면 아래 그림이 통째로 가려 마운트가 사라진다.
                Color tint = ItemColor(MountDisplay.FlowOf(item));
                tint.a = MountSlotTintAlpha;
                sr.color = tint;
            }
        }

        /// <summary>
        /// 지금 점멸 중인가 — **그림·그리드·이름표가 같은 박자를 쓴다**(§71-41).
        /// 세 곳에서 따로 재면 서로 다른 박자로 깜빡인다.
        /// </summary>
        /// <summary>
        /// 이 마운트를 조립 화면에 그리는가 — **지금 나선 로봇 것만 그린다**
        /// (2026-09-14 사용자 확정 · §72-24 ②).
        ///
        /// ⚠️ **왜 가리는가.** 적재 값은 `SupplySignals` 가 **활성 로봇 것만** 나른다.
        /// 대기 중인 로봇의 묶음은 그려도 **영영 빈 칸**이라, 화면에서는 「물류가 저기로는
        /// 안 간다」로 읽힌다 — 실제로는 **지금 안 보고 있을 뿐**이다. 없는 고장을
        /// 그림으로 지어내지 않으려면 **안 그리는 편**이 맞다.
        ///
        /// 그림·그리드·이름표·점멸이 **같은 판정 하나**를 읽는다. 따로 재면 셋 중 하나가
        /// 남아 어긋난다(오늘 점멸에서 이미 한 번 그랬다).
        /// </summary>
        /// <summary>
        /// 이 마운트를 **지금 보여 주는가** (2026-09-16 · 보드 로봇별 분리로 기준이 바뀌었다).
        ///
        /// 🗑️ **구 기준 「싸우는 로봇」(`SupplySignals.ActiveOwner`)은 폐기.**
        /// 조립 화면에 로봇 탭이 생기면서 **보는 판과 싸우는 로봇이 갈라졌다** —
        /// 옛 기준이면 B 판을 편집하는 내내 **A 의 마운트가 B 판 위에 떠 있다.**
        ///
        /// 📌 마운트는 **그 판의 것**이다(사용자 확정 — 「마운트 표시 = 그 로봇 것」).
        /// </summary>
        private bool ShowsMount(MountOwner owner) => owner == _editing;

        private static bool MountBlinkOn =>
            MountDisplay.Blinks(SupplySignals.HasCombat, SupplySignals.MountTotal)
            && HudMeters.BlinkOn(Time.unscaledTime);

        /// <summary>
        /// 마운트 **그림**을 슬롯 묶음 **네 칸 위에 세로로 늘려** 세운다
        /// (2026-09-14 · §72-13 사용자 확정 · 구 「별도 그림 칸」 폐기).
        ///
        /// **왜 바꿨나.** 그림 칸과 적재 칸이 따로 서니 **둘이 한 덩어리로 안 읽혔고**,
        /// 그림 칸 때문에 바깥 열이 하나 더 필요했다. 겹쳐 놓으면 「이 마운트에 이만큼
        /// 들었다」가 한눈에 서고 격자도 **한 칸만** 밖으로 나간다.
        ///
        /// ⚠️ **가로는 한 칸 · 세로는 네 칸이다** — 원본이 정사각이라 세로로 늘어난다.
        /// 규격이 「실루엣에 붙는 변 여백 0」이라 늘려도 결합부는 변에 붙어 있다.
        ///
        /// ⚠️ **그림이 없어도 그린다**(색 사각) — 없으면 슬롯 칸만 남아
        /// **무엇에 딸린 적재인지**가 사라진다.
        ///
        /// ⚠️ **슬롯 틴트보다 아래에 깐다** — 틴트가 그림 위에 곱해져야 한다.
        /// </summary>
        private void BuildMountBodies(Transform parent)
        {
            _mountBodyViews.Clear();
            _mountBodyCells.Clear();
            _mountBodyOwners.Clear();
            _mountBodyHasArt.Clear();
            _mountBodyRest.Clear();

            float cell = _grid.CellSize;
            int tall = MountDisplay.GroupHeightCells;

            foreach (MountPort mp in PartLayout.MountPorts)
            {
                // 묶음 맨 아랫 칸을 자리로 들고 있는다 — 이름표가 그 밑변을 쓴다.
                //
                // ⚠️⚠️ **슬롯 0번이 아니다**(2026-09-16 · 육안 9차 ⑥ 결함). A 는 위부터 차므로
                //    `SlotCell(...,0)` 이 묶음의 **맨 윗 칸**이다. 그것을 밑변으로 알고 위로
                //    반 묶음을 올리면 그림이 **세 칸 더 올라가** 격자 위쪽에 뜬다 —
                //    사용자가 본 「총 그림이 위에 작게 떠 있고 네 칸은 빈다」가 그것이다.
                Vector2Int bottom = MountDisplay.GroupBottomCell(mp.cell, mp.face, mp.owner);
                Sprite body = art != null ? art.MountBody(mp.owner) : null;

                var go = new GameObject($"MountBody_{mp.owner}_{mp.face}");
                go.transform.SetParent(parent, false);

                Vector3 c = CellWorld(bottom);
                go.transform.position = new Vector3(c.x, c.y + (tall - 1) * 0.5f * cell, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                if (body != null)
                {
                    sr.sprite = body;
                    float unit = FitScale(body, cell);
                    go.transform.localScale = new Vector3(unit, unit * tall, 1f);

                    // ⚠️ **파츠와 같은 틴트를 입힌다**(2026-09-15 사용자 확정 · 육안 5차 ④).
                    //
                    // 마운트는 실루엣 **안**에 앉는데 혼자 흰색이라 **바닥에서 떠 보였다.**
                    // 파츠 바닥은 이미 `PartPalette.FloorOf` 로 구역 색을 옅게 입고 있으므로,
                    // 마운트도 **자기가 앉은 파츠의 색**을 쓰면 같은 몸의 일부로 읽힌다.
                    //
                    // ⚠️ **같은 함수를 쓴다** — 여기서 색을 따로 만들면 파츠 틴트를 조정할 때
                    // 마운트만 옛 색으로 남는다(지침 §7).
                    sr.color = PartPalette.FloorOf(PartLayout.PartAt(bottom));
                    sr.flipX = MountDisplay.FlipX(mp.cell, mp.face, mp.owner);
                }
                else
                {
                    sr.sprite = UnitSprite();
                    go.transform.localScale = new Vector3(cell, cell * tall, 1f);
                    sr.color = MountFallbackColor;
                }

                sr.sortingOrder = MarkerOrder - 2;

                _mountBodyViews.Add(sr);
                _mountBodyCells.Add(bottom);
                _mountBodyOwners.Add(mp.owner);
                _mountBodyHasArt.Add(body != null);
                // 매 프레임 점멸을 되돌리는 쪽이 이 값을 쓴다 — 안 들고 있으면 흰색으로 덮인다.
                _mountBodyRest.Add(body != null
                    ? PartPalette.FloorOf(PartLayout.PartAt(bottom))
                    : MountFallbackColor);
            }
        }

        /// <summary>
        /// **적재 슬롯 묶음** — 1슬롯 = 보드 한 칸 (2026-09-14 · §72-5 사용자 확정 · UI 12-4).
        ///
        /// **왜 월드로 그리는가.** 슬롯이 보드 칸과 같은 크기이므로 **보드와 같은 층**에 두는 것이
        /// 맞다 — 그러면 스크롤·배율을 공짜로 따라간다. 화면 좌표로 그리면 한 번 잰 자리가
        /// **스크롤에서 떨어진다**(`Docs/HANDOFF.md` 함정).
        ///
        /// ⚠️ **B 는 왼쪽이 0~3 · 오른쪽이 4~7 이다** — 어깨가 둘인데 적재는 한 벌이라,
        /// **표시 순서를 나눈 것이지 적재를 나눈 것이 아니다**(§72-5). 합을 둘로 쪼개 그리면
        /// **없는 분배를 지어내는 것**이 된다.
        ///
        /// ⚠️ **빈 칸도 테두리로 그린다** — 칸이 **몇 개인지**는 비어 있어도 보여야
        /// 「얼마나 남았나」가 읽힌다.
        /// </summary>
        private void BuildMountSlots(Transform parent)
        {
            _mountSlotFills.Clear();
            _mountSlotIndex.Clear();
            _mountSlotOwners.Clear();
            _mountSlotCells.Clear();
            _mountSlotEdges.Clear();
            _mountSlotEdgeOwners.Clear();
            _mountSlotIcons.Clear();

            float cell = _grid.CellSize;
            float t = cell * MountSlotEdgeWidth;

            foreach (MountPort mp in PartLayout.MountPorts)
            {
                // ⚠️ **면이 아니라 자리로 가른다**(§72-6) — 왼쪽 묶음이 0~3 이다.
                int first = MountDisplay.FirstSlotOf(MountDisplay.SlotColumn(mp.cell, mp.face));

                for (int i = 0; i < MountDisplay.SlotsPerPort; i++)
                {
                    Vector2Int c = MountDisplay.SlotCell(mp.cell, mp.face, mp.owner, i);
                    Vector3 w = CellWorld(c);

                    // 테두리 네 변. 칸 하나를 **선으로만** 그린다 — 속을 칠하면 채움과 섞인다.
                    _mountSlotEdges.Add(SlotEdge(parent, w.x, w.y - cell * 0.5f, cell, t));
                    _mountSlotEdges.Add(SlotEdge(parent, w.x, w.y + cell * 0.5f, cell, t));
                    _mountSlotEdges.Add(SlotEdge(parent, w.x - cell * 0.5f, w.y, t, cell));
                    _mountSlotEdges.Add(SlotEdge(parent, w.x + cell * 0.5f, w.y, t, cell));
                    for (int e = 0; e < 4; e++) _mountSlotEdgeOwners.Add(mp.owner);

                    // ⚠️ **품목 아이콘 한 장**(2026-09-14 · §72-24 ③).
                    //
                    // 틴트만으로는 **색을 외워야** 무엇이 실렸는지 안다. 아이콘을 깔면
                    // 색은 「얼마나 찼나」로, 그림은 「무엇이 실렸나」로 **역할이 갈린다.**
                    // 틴트는 그대로 둔다 — 아이콘이 색을 대신하는 것이 아니다.
                    var icon = new GameObject($"MountIcon_{mp.owner}_{first + i}");
                    icon.transform.SetParent(parent, false);
                    icon.transform.position = w;
                    var iconSr = icon.AddComponent<SpriteRenderer>();
                    // ⚠️ **틴트 위로 올린다**(2026-09-15 사용자 확정 · 육안 6차 ⑤).
                    //
                    // 종전에는 틴트 아래였다 — 「얼마나 찼나」(색)를 위에 두고 「무엇이
                    // 실렸나」(그림)를 아래에 깔았는데, **차오를수록 그림이 묻혔다.**
                    // 무엇이 실렸는지는 언제나 읽혀야 하므로 그림이 위다.
                    // 이름표는 IMGUI 라 이보다 위에 있다 — 가리지 않는다.
                    iconSr.sortingOrder = MarkerOrder + 1;   // 틴트 위 · 이름표 아래
                    iconSr.enabled = false;                  // 실린 것이 없으면 안 그린다
                    _mountSlotIcons.Add(iconSr);

                    var go = new GameObject($"MountSlot_{mp.owner}_{first + i}");
                    go.transform.SetParent(parent, false);
                    go.transform.position = w;
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = UnitSprite();
                    // ⚠️ **아이콘보다 위** — 틴트는 얹히는 것이다
                    // (그림 MarkerOrder−2 < 아이콘 MarkerOrder−1 < 틴트 MarkerOrder).
                    sr.sortingOrder = MarkerOrder;
                    sr.enabled = false;   // 채움이 0이면 아예 안 그린다

                    _mountSlotFills.Add(sr);
                    _mountSlotIndex.Add(first + i);
                    _mountSlotOwners.Add(mp.owner);
                    _mountSlotCells.Add(c);
                }
            }
        }

        // 🗑️ **`DrawRotationArrow` 폐기**(2026-09-18 ⑩) — 그리던 버튼이 없어져
        //    부르는 곳이 0 건이 됐다. 남겨 두면 다음 사람이 「방향 표시가 있다」고 읽는다.


        /// <summary>슬롯 칸 테두리 한 변.</summary>
        private SpriteRenderer SlotEdge(Transform parent, float cx, float cy, float w, float h)
        {
            var g = new GameObject("slotEdge");
            g.transform.SetParent(parent, false);
            g.transform.position = new Vector3(cx, cy, 0f);
            g.transform.localScale = new Vector3(w, h, 1f);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = UnitSprite();
            sr.color = MountSlotEdgeColor;
            sr.sortingOrder = MarkerOrder - 2;   // 채움 아래
            return sr;
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

        /// <summary>
        /// 지금 보드를 **하네스가 읽는 꼴로** 찍는다 (2026-09-11 · 플랜 §71-14 ② · 개발 빌드 도구).
        ///
        /// **왜 있는가.** 리허설 결함 ① 은 사용자 화면에서만 나고 하네스에서는 안 난다.
        /// 재현하려면 **사용자가 놓은 배치 그대로**를 하네스에 먹여야 하는데, 지금까지는
        /// 스크린샷을 보고 사람이 좌표를 옮겨 적는 수밖에 없었다 — **한 칸만 틀려도 다른 보드를
        /// 재게 된다.** 오늘 하네스가 병합기를 직선 벨트로 놓고 게임과 다른 보드를 잰 것이
        /// 바로 그 사고였다.
        ///
        /// ⚠️ **병합기·분류기는 면을 전부 적는다.** 입력면이 여럿인 것이 병합기의 뜻이고,
        /// 첫 면만 적으면 옮겨 적은 쪽에서 다시 직선 벨트가 된다.
        /// </summary>
        private void FulfilBoardDump()
        {
            BoardDumpSignals.Requested = false;
            if (_grid == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# 보드 좌표 덤프 (개발 빌드 · " + System.DateTime.Now.ToString("HH:mm:ss") + ")");
            sb.AppendLine("# 노드: x y 종류 / 벨트: x y 입력면… > 출력면… [요소]");

            var nodes = new List<string>();
            var belts = new List<string>();
            for (int x = 0; x < _grid.Columns; x++)
            for (int y = 0; y < _grid.Rows; y++)
            {
                var cell = new Vector2Int(x, y);
                if (!_grid.IsInside(cell)) continue;

                NodeInstance n = _grid.GetAt(cell);
                if (n != null && n.Definition != null)
                    nodes.Add($"node {cell.x} {cell.y} {n.Definition.type}");

                BeltInstance b = _grid.GetBeltAt(cell);
                if (b == null) continue;
                belts.Add($"belt {cell.x} {cell.y} {Faces(b.InFaces)} > {Faces(b.OutFaces)} {b.Element}");
            }

            nodes.Sort(System.StringComparer.Ordinal);
            belts.Sort(System.StringComparer.Ordinal);
            sb.AppendLine($"# 노드 {nodes.Count} · 벨트 {belts.Count}");
            foreach (string line in nodes) sb.AppendLine(line);
            foreach (string line in belts) sb.AppendLine(line);

            // 재현에 필요한 값이 좌표만은 아니다 — 지금 무엇이 흐르고 있었는지도 같이 적는다.
            sb.AppendLine($"# 빈 칸 요청 {StartingBoard.EmptySlot.x} {StartingBoard.EmptySlot.y}" +
                          $" · 고스트 {(TutorialSignals.GhostCell.HasValue ? TutorialSignals.GhostCell.Value.ToString() : "없음")}" +
                          $" · 채워짐 {TutorialSignals.GhostCellFilled}");

            BoardDumpSignals.Latest = sb.ToString();
            BoardDumpSignals.Version++;

            // ⚠️ **클립보드가 본체다.** WebGL 에서 Debug.Log 는 브라우저 콘솔에만 남아
            // 사용자가 F12 를 열어야 보인다 — 붙여 넣을 수 있어야 하네스로 넘어간다.
            GUIUtility.systemCopyBuffer = BoardDumpSignals.Latest;
            Debug.Log(BoardDumpSignals.Latest);
        }

        private static string Faces(PortFace[] faces)
        {
            if (faces == null || faces.Length == 0) return "-";
            var parts = new string[faces.Length];
            for (int i = 0; i < faces.Length; i++) parts[i] = faces[i].ToString();
            return string.Join(",", parts);
        }

        /// <summary>
        /// 튜토리얼 어둠막의 **수명을 여기서만** 본다 (2026-09-11 결함 수정 · 플랜 §71-16 ⑤).
        ///
        /// ⚠️ **종전에는 이것이 `DrawTutorialGhost` 안에 있었고, 그 메서드는 맨 위에서
        /// `GhostCell == null` 이면 곧장 돌아갔다.** 그래서 **고스트가 사라지는 순간
        /// 막을 걷는 코드에 도달하지 못했다** — 채우고 나면 DIM 이 영영 남았다.
        /// 「채운 뒤 DIM 이 안 걷힌다」의 뿌리가 그 조기 반환이다.
        ///
        /// **수명은 그리기에 얹을 것이 아니다.** `OnGUI` 는 메뉴가 열려도, 전투 화면이어도
        /// 안 불리는데, 막은 월드에 놓인 스프라이트라 **안 불리는 동안에도 남아 있다.**
        /// 그래서 늘 도는 `Update` 로 옮긴다.
        ///
        /// ⚠️ `GhostCellFilled` 도 여기서 쓴다 — 그것을 정하는 것과 막을 걷는 것이
        /// **같은 판단**이라 떨어져 있으면 또 어긋난다.
        /// </summary>
        private void UpdateTutorialDim()
        {
            if (_grid == null) return;

            Vector2Int? target = TutorialSignals.GhostCell;
            if (!target.HasValue)
            {
                RefreshTutorialDim(null); // 튜토리얼이 끝났거나 꺼졌다 — 막을 걷는다
                RefreshTutorialGhost(null);
                return;
            }

            Vector2Int cell = target.Value;
            // ⚠️ **노드만 보면 안 된다** — 채우는 것이 벨트 요소인 국면이 있다(2026-09-01).
            bool filled = _grid.GetAt(cell) != null || _grid.GetBeltAt(cell) != null;
            TutorialSignals.GhostCellFilled = filled;

            // 채우면 막이 걷힌다 — 그것이 수업이 끝났다는 신호다.
            // ⚠️ **고스트도 같은 판단으로 함께 움직인다** — 둘을 떨어뜨려 두었다가
            // 막만 남던 것이 09-11 오전의 결함이었다(§71-16 ⑤).
            RefreshTutorialDim(filled ? null : (Vector2Int?)cell);
            RefreshTutorialGhost(filled ? null : (Vector2Int?)cell);
        }

        /// <summary>
        /// 고스트 칸 **밖**을 덮는 막을 다시 짓는다 (2026-09-11 · 플랜 §68-5 ①).
        ///
        /// ⚠️ **한 장으로는 구멍을 못 낸다.** 이동 모드 흐림(<see cref="BuildDimOverlay"/>)은
        /// 보드 전체를 사각 하나로 덮는데, 여기는 **비켜 줄 칸**이 있어야 하므로 **칸마다 조각**이다.
        ///
        /// 비켜 주는 것은 둘이다 — **고스트 칸**과 **거기 닿는 벨트 칸**.
        /// 벨트 끝을 같이 비켜 주지 않으면 「어디로 이어지는 자리인지」가 안 보인다.
        /// </summary>
        private void RefreshTutorialDim(Vector2Int? ghost)
        {
            var want = new HashSet<Vector2Int>();
            if (ghost.HasValue && _grid != null)
            {
                Vector2Int c = ghost.Value;
                want.Add(c);
                foreach (PortFace f in new[] { PortFace.North, PortFace.East, PortFace.South, PortFace.West })
                {
                    Vector2Int nb = c + BeltRouting.Delta(f);
                    if (_grid.GetBeltAt(nb) != null) want.Add(nb);
                }
            }

            // 바뀐 것이 없으면 손대지 않는다 — 매 프레임 다시 지으면 깜빡인다.
            if (_tutorialDim != null && want.SetEquals(_dimExempt)) return;

            if (_tutorialDim != null) Destroy(_tutorialDim);
            _tutorialDim = null;
            _dimExempt.Clear();
            foreach (Vector2Int c in want) _dimExempt.Add(c);

            if (!ghost.HasValue || _grid == null) return;

            _tutorialDim = new GameObject("TutorialDim");
            _tutorialDim.transform.SetParent(transform, false);

            for (int x = 0; x < _grid.Columns; x++)
            for (int y = 0; y < _grid.Rows; y++)
            {
                var cell = new Vector2Int(x, y);
                if (!_grid.IsInside(cell) || _dimExempt.Contains(cell)) continue;

                var go = new GameObject($"dim_{cell.x}_{cell.y}");
                go.transform.SetParent(_tutorialDim.transform, false);
                go.transform.position = CellWorld(cell);
                go.transform.localScale = Vector3.one * _grid.CellSize;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = UnitSprite();
                sr.color = TutorialDimColor;
                sr.sortingOrder = SortingLayers.Hud;
            }
        }

        // 중심(cx,cy)·크기(w,h)의 단색 사각 스프라이트 하나.
        // ── 구역 표시 배치 규격 (UI 아트 요청 문서(20) 10장 · `260905_W04` 4-5) ────────────
        // 값은 **아트 픽셀** 기준이고 한 칸이 192픽셀이다. 월드 단위로 쓰려면 cellSize를 곱한다 —
        // 칸 크기가 바뀌어도 화면에서 보이는 비율이 그대로 유지된다.
        private const float ZonePx = 192f;      // 격자 한 칸 = 192 아트 픽셀
        // ⚠️ **점선 → 실선 · 굵게**(2026-09-15 사용자 확정 · 육안 ⑦).
        //
        // 점선은 흙 배경 위에서 **끊긴 자국처럼** 보여 파츠 경계가 안 읽혔다.
        // 끊는 길이를 0 으로 두면 같은 코드가 실선을 그린다 — 그리는 길이만 남는다.
        // ⚠️ **값 셋 다 가정이다**(설계 역기입). 구 4 / 16 / 16 은 폐기 표기.
        private const float ZoneLineThickPx = 7f;   // 선 굵기 (구 4)
        private const float ZoneDashPx = 16f;       // 그리는 길이
        private const float ZoneGapPx = 0f;         // 띄우는 길이 — 0 = 실선 (구 16)
        private const float ZoneLabelInsetPx = 8f;  // 이름표를 구역 왼윗모서리 안쪽으로 들이는 양
        // 이름표 글자 높이 — **아트 픽셀**이다 (2026-09-07 · `260906_W05` 2-2).
        //
        // 구 값 24는 **화면 픽셀** 기준이었다. 단위를 아트 픽셀로 바꾸면 겉보기가
        // 창 크기를 따라가므로, 「몇 픽셀로 보이는가」는 해상도를 말하지 않으면 뜻이 없다.
        // 씬 카메라 시야가 8이고 칸이 1월드라 한 칸의 화면 세로 = 창 세로 ÷ 16이다.
        //
        //   창 세로   한 칸    24 아트 픽셀   96 아트 픽셀   무엇인가
        //     810     50.6px      6.3px         25.3px      아래 ⚠️ 참조
        //    2560    160.0px     20.0px         80.0px      문서 기준 해상도 (UI 아트 요청 문서(20) 7장)
        //     600     37.5px      4.7px         18.8px      웹빌드 기본 (ProjectSettings 960×600)
        //
        // ⚠️ **810은 어느 문서에도 없는 창이다.** 2026-09-07에 이 값을 고를 때 쓴 임시 기준이며,
        // 「96이 구 값 24의 겉보기와 같다」는 말은 **810에서만 참이다.** 2560에서는 3.3배가 된다.
        //
        // 96은 **격자 반 칸**(192의 절반)이라는 비율 선택이다. 확대하면 보드와 함께 커진다.
        // 반 칸이 맞는 비율인지는 아직 판정이 아니다 — `260907_V01` ❓로 올린다.
        // W05 2-2가 「안 읽히면 아트 픽셀 값을 올리고 역기입한다」로 조건을 걸어 둔 자리다.
        // ⚠️ 읽히는지의 최종 판정은 화면에서 사람이 한다 — 배치모드는 이 자리를 못 본다.
        private const int ZoneLabelFontPx = 96;


        /// <summary>
        /// 구역 표시의 미색. **선과 이름표가 같은 색이라는 것**이 배치 규격의 요구다 —
        /// 다른 색이면 둘이 다른 물건으로 읽힌다. 그래서 색조는 여기 하나뿐이고,
        /// 아래 둘은 <b>불투명도만</b> 다르게 가진다.
        /// </summary>
        private static readonly Color ZoneTint = new Color(0.96f, 0.94f, 0.86f, 1f);

        /// <summary>경계선 불투명도 40% — 밑의 타일을 가리지 않는다(배치 규격).</summary>
        private const float ZoneLineAlpha = 0.40f;

        /// <summary>
        /// 이름표 불투명도 **70%** (2026-09-06 확정 · `260906_W04` 2-7).
        /// 40%는 경계선에만 걸리는 값이었다 — 선은 얇아 흐려도 형태가 남지만 글자는 읽히지 않는다.
        /// </summary>
        private const float ZoneLabelAlpha = 0.70f;

        private static readonly Color ZoneLineColor = Alpha(ZoneTint, ZoneLineAlpha);

        /// <summary>이름표는 선과 같은 미색이되 더 진하다. 두 값이 **따로** 움직인다.</summary>
        private static readonly Color ZoneLabelColor = Alpha(ZoneTint, ZoneLabelAlpha);

        private static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

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
        private void SpawnDashedEdge(Transform parent, float cx, float cy, float length, bool horizontal,
            Color color, bool outer)
        {
            float unit = config.cellSize / ZonePx;
            // ⚠️ **바깥 경계는 굵다**(2026-09-11 재육안 2 ②) — 「여기까지가 로봇이다」를
            // 말하는 선이라 안쪽 변과 같은 굵기로는 덩어리의 테두리가 안 선다.
            float thickPx = outer ? ZoneLineThickPx * PartPalette.OuterLineThickness : ZoneLineThickPx;
            float thick = Mathf.Max(thickPx * unit, 0.01f);
            float period = (ZoneDashPx + ZoneGapPx) * unit;
            if (period <= 0f || length <= 0f) return;

            int count = Mathf.Max(1, Mathf.RoundToInt(length / period));
            float step = length / count;                 // 변 길이에 맞춘 실제 주기
            float dash = step * (ZoneDashPx / (ZoneDashPx + ZoneGapPx));
            float start = (horizontal ? cx : cy) - length * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float c = start + step * i + dash * 0.5f; // 조각 중심
                if (horizontal) SpawnQuad(parent, c, cy, dash, thick, color, ZoneLineOrder);
                else            SpawnQuad(parent, cx, c, thick, dash, color, ZoneLineOrder);
            }
        }

        /// <summary>
        /// 마운트 고정 포트에 「마운트」 이름표 (2026-09-11 · 플랜 §71-16 ②).
        ///
        /// **구역 이름표와 같은 규격**을 쓴다 — 같은 보드 위의 글자가 서로 다른 크기·색이면
        /// 하나는 꺼져 있는 것처럼 보인다(배율 라벨에서 이미 겪은 자리다).
        ///
        /// ⚠️ **글자는 포트 칸의 왼윗모서리가 아니라 칸 가운데 위**에 붙인다. 구역은 넓어
        /// 모서리에 붙여도 제 구역 안이지만, 마운트는 **한 칸**이라 모서리에 붙이면
        /// 옆 칸 위에 얹힌다.
        /// </summary>
        /// <summary>
        /// 노드 상태 표식 (2026-09-11 신설 · 플랜 §71-33 ③).
        ///
        /// **왜 신설하는가.** 상태는 지금 `NodeStatusTint` 의 **밝기**로만 말한다.
        /// 어두운 칸이 「정지」인지 「그늘진 그림」인지 옆 칸과 견줘야 알 수 있고,
        /// **전력 부족과 미연결은 밝기 축에 자리 자체가 없다.**
        /// 아이콘 다섯은 09-04 부터 `Art/UI/` 에 있었는데 **코드가 한 곳에서도 안 읽었다.**
        ///
        /// ⚠️ **화면 좌표로 굳히지 않는다** — 매 프레임 `WorldToScreenPoint` 로 다시 잰다.
        /// 보드 위에 그리는 것을 한 번 잰 화면 좌표로 그리면 **스크롤에서 떨어진다**
        /// (`Docs/HANDOFF.md` 함정). 마운트 이름표와 같은 규격이다.
        ///
        /// ⚠️ **판정은 `NodeStatusIcon` 이 한다** — 여기는 고르지 않고 그리기만 한다(§3).
        /// </summary>
        /// <summary>
        /// 고스트 칸 위에 **무엇을 놓는지와 어떻게 놓는지**를 쓴다
        /// (2026-09-15 사용자 확정 · 설계 사후 · W09 0908 「안내 문구를 넣지 않는다」를 뒤집는다).
        ///
        /// ⚠️⚠️ **왜 뒤집혔나.** 고스트는 「여기」만 말하고 「무엇을」과 「어떻게」는 말하지 않았다.
        /// 판을 처음 보는 사람에게 베이지 정사각 하나는 **누르라는 뜻으로도 안 읽힌다** —
        /// 팔레트에서 무엇을 골라야 하는지도 화면 어디에도 없었다.
        ///
        /// ⚠️ **문안은 가정이다**(설계 역기입 자리). 첫 플레이어 기준으로 짧게 두 줄만 쓴다.
        ///
        /// ⚠️ **화면 좌표로 굳히지 않는다** — 매 프레임 다시 잰다. 보드를 끌면 따라와야 한다
        /// (고스트 자신이 09-11 에 같은 이유로 월드 스프라이트가 됐다).
        /// </summary>
        private void DrawGhostHint(Camera cam, GUIStyle style, float fontScale)
        {
            Vector2Int? cell = TutorialSignals.GhostCell;
            if (!cell.HasValue) return;

            // ⚠️⚠️ **채운 뒤에도 한 번 더 말한다**(2026-09-16 사용자 확정 · 플랜 §74-17 ④).
            //
            // 튜토리얼이 가르치는 것은 **한 칸 터치**뿐이었다. 그런데 보드를 실제로 쓰려면
            // **끌어서 여러 칸을 깔고**, **놓인 칸에서 끌어 지우는** 두 손짓이 필요하다 —
            // 그 둘은 어디에서도 안 알려 주었고, 규칙은 이미 `OnPressEnd` 에 적혀 있었다.
            // 코드에만 있고 화면에 없는 규칙은 **없는 것과 같다.**
            //
            // 📌 **자리와 규격은 고스트 안내 그대로다** — 방금 채운 칸에 눈이 가 있으므로
            //    거기서 이어 말하는 것이 가장 짧다. 새 판을 만들지 않는다.
            // ⚠️⚠️ **「채워진 상태」가 아니라 「채우는 순간」이다**
            //    (2026-09-16 · 플랜 브라우저 확인 ④ — 「조립에 들어가자마자 떴다」).
            //
            // 종전 판은 `GhostCellFilled` 가 **참이기만 하면** 세었다. 그런데 그것은
            // **상태**라, 저장에서 돌아온 판은 **처음부터 참**이다 — 들어가자마자 안내가
            // 뜨고, 그때 플레이어는 방금 아무것도 안 했다.
            //
            // 📌 **상태와 사건은 다르다.** 가르치는 말은 **방금 한 일**에 붙어야 한다.
            //    그래서 거짓 → 참으로 **넘어가는 것을 본 때**만 시계를 놓는다.
            //    들어올 때 이미 참이면 그 넘어감을 못 봤으므로 **안 뜬다.**
            bool filled = TutorialSignals.GhostCellFilled;
            if (filled && !_ghostFilledSeen)
            {
                _ghostFilledSeen = true;
                // 처음 본 것이 **이미 채워진 판**이면 시계를 안 놓는다(= 영영 안 뜬다).
                if (_ghostEverEmpty) _ghostFilledAt = Time.unscaledTime;
            }
            if (!filled)
            {
                _ghostEverEmpty = true;   // 빈 것을 봤다 — 이제부터 채우면 그것은 사건이다
                _ghostFilledSeen = false;
                _ghostFilledAt = -1f;
            }
            if (filled)
            {
                if (_ghostFilledAt < 0f) return;
                if (Time.unscaledTime - _ghostFilledAt > BeltGestureHintSeconds) return;
            }

            // 칸의 **위**에 앉힌다 — 칸 안에 쓰면 고스트 색과 겹쳐 글자가 묻힌다.
            Vector3 c = CellWorld(cell.Value);
            Vector3 top = new Vector3(c.x, c.y + config.cellSize * 0.5f, 0f);
            Vector3 sp = cam.WorldToScreenPoint(top);
            if (sp.z <= 0f) return;

            // 칸 한 변이 화면에서 몇 픽셀인가 — 줌을 따라간다.
            Vector3 edge = cam.WorldToScreenPoint(top + new Vector3(config.cellSize, 0f, 0f));
            float cellPx = Mathf.Abs(edge.x - sp.x);
            if (cellPx < 24f) return;   // 너무 작으면 글자가 뭉갠다 — 안 그린다

            // ⚠️ **문안은 가정이다**(설계 역기입 자리). 지키는 것은 하나 —
            //    `OnPressEnd` 에 적힌 규칙을 **그대로** 옮긴다(지침 §7 · 말이 둘이 되면 안 된다):
            //    · 빈 칸에서 시작 → 설치(탭이든 드래그든)
            //    · 놓인 칸에서 시작한 **드래그** → 제거 (한 칸 탭은 제거가 아니다)
            string what = filled ? "끌면 여러 칸" : "벨트";
            string how = filled ? "놓인 칸에서 끌면 제거" : "빈 칸을 터치";

            var st = new GUIStyle(style)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,   // 재기 전에 잘리는 함정을 피한다
                fontStyle = FontStyle.Bold,
            };
            // ⚠️ 크기는 **두 변**에서 잡는다 — 칸 폭도 한계다(오늘 네 번 고친 병이다).
            int byCell = Mathf.RoundToInt(cellPx * 0.26f);
            st.fontSize = KoreanFont.Snap(Mathf.Max(10, Mathf.Min(byCell, Mathf.RoundToInt(28f * fontScale))));
            st.normal.textColor = Color.white;

            float lineH = st.fontSize * 1.35f;
            float w = cellPx * 2.4f;
            float x = sp.x - w * 0.5f;
            float y = Screen.height - sp.y - lineH * 2f - cellPx * 0.12f;

            var plate = new Rect(x, y, w, lineH * 2f);
            UiPlate.Draw(plate);
            UiBlockers.Add(plate);   // 판 밑의 칸이 눌리지 않게 — 안내가 설치를 막으면 본말전도다

            GUI.Label(new Rect(x, y, w, lineH), what, st);
            GUI.Label(new Rect(x, y + lineH, w, lineH), how, st);
        }

        /// <summary>
        /// 고스트 칸을 채운 시각. 음수면 아직 안 채웠다.
        /// ⚠️ **신호가 아니라 화면 쪽 값이다** — 「언제 채웠나」는 튜토리얼 상태가 아니라
        /// 이 안내가 얼마나 더 서 있을지를 정하는 값뿐이다.
        /// </summary>
        private float _ghostFilledAt = -1f;

        /// <summary>고스트 칸이 **비어 있는 것을 본 적이 있는가.** 저장 복원 판을 가른다.</summary>
        private bool _ghostEverEmpty;

        /// <summary>채워진 것을 이미 셌는가 — 매 프레임 시계를 다시 놓지 않게.</summary>
        private bool _ghostFilledSeen;

        /// <summary>
        /// 채운 뒤 손짓 안내를 몇 초 더 세워 둘 것인가.
        /// ⚠️ **가정 6초** — 문서에 안내 지속 절이 없다(설계 역기입 자리).
        /// 너무 짧으면 못 읽고, 안 지우면 판이 영영 칸 위에 얹혀 있다.
        /// </summary>
        private const float BeltGestureHintSeconds = 6f;

        /// <summary>
        /// **이웃은 있는데 면이 달라** 안 이어진 벨트 칸에 사유를 적는다
        /// (2026-09-16 사용자 확정 ⓒ · §74-12 A).
        ///
        /// ⚠️⚠️ 종전에는 아무 말도 안 했다. 코어 위에 가로로 끈 벨트가 **붙어 있는데 안
        /// 흐르고**, 왜 안 되는지가 어디에도 없었다(사용자 육안 09-16).
        ///
        /// 📌 「이웃이 없다」와 구분해서 적는다 — 앞은 아직 안 지은 것이고 뒤는 **지어 놓고
        /// 안 되는 것**이다. 같은 경고로 보이면 이미 이어 놓은 자리를 또 잇게 된다.
        ///
        /// ⚠️ 화면 좌표로 굳히지 않는다 — 매 프레임 다시 잰다(보드를 끌면 따라와야 한다).
        /// </summary>
        private void DrawFaceMismatchLabels(Camera cam, GUIStyle style, float fontScale)
        {
            if (_grid == null || cam == null) return;

            List<Vector2Int> cells = BeltRouting.FaceMismatchCells(_grid);
            if (cells.Count == 0) return;

            var st = new GUIStyle(style)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                fontStyle = FontStyle.Bold,
            };
            st.normal.textColor = MountEmptyColor;

            foreach (Vector2Int cell in cells)
            {
                Vector3 c = CellWorld(cell);
                Vector3 sp = cam.WorldToScreenPoint(c);
                if (sp.z <= 0f) continue;

                Vector3 edge = cam.WorldToScreenPoint(c + new Vector3(config.cellSize, 0f, 0f));
                float cellPx = Mathf.Abs(edge.x - sp.x);
                if (cellPx < 24f) continue;   // 너무 작으면 글자가 뭉갠다

                // ⚠️ 크기는 두 변에서 잡는다 — 칸 폭도 한계다.
                st.fontSize = KoreanFont.Snap(Mathf.Max(9,
                    Mathf.Min(Mathf.RoundToInt(cellPx * 0.22f), Mathf.RoundToInt(26f * fontScale))));

                float w = cellPx * 1.6f, h = st.fontSize * 1.4f;
                var r = new Rect(sp.x - w * 0.5f, Screen.height - sp.y - h * 0.5f, w, h);
                if (r.xMax < 0f || r.x > Screen.width || r.yMax < 0f || r.y > Screen.height) continue;

                UiPlate.Draw(r);
                GUI.Label(r, "면이 다름", st);
            }
        }

        private void DrawStatusIcons(Camera cam)
        {
            if (_lastDiagnostics == null || _lastDiagnostics.Count == 0) return;

            var dangling = new HashSet<Vector2Int>(BeltRouting.DanglingWarningCells(_grid));

            // 칸의 3할. ⚠️ **가정** — 규격 문서에 표식 크기 절이 없다(설계 역기입 자리).
            float sizeWorld = config.cellSize * StatusIconCellFraction;
            float half = config.cellSize * 0.5f;

            foreach (NodeDiagnostic d in _lastDiagnostics)
            {
                float ratio = d.targetRate > 0f ? d.actualRate / d.targetRate : 1f;
                NodeIcon icon = NodeStatusIcon.Of(
                    d.cause == ConstraintCause.Power, dangling.Contains(d.cell), ratio);
                if (icon == NodeIcon.None) continue;

                Texture2D tex = UiSkin.IconTexture(icon);
                if (tex == null) continue;   // 그림이 없으면 안 그린다 — 자리표시로 대신하지 않는다

                // 칸의 **오른윗모서리 안쪽**. 왼윗모서리는 마운트 이름표가 쓴다 —
                // 같은 자리에 둘을 얹으면 서로 가린다.
                Vector3 c = CellWorld(d.cell);
                Vector3 corner = new Vector3(c.x + half - sizeWorld * 0.5f,
                                             c.y + half - sizeWorld * 0.5f, 0f);
                Vector3 sp = cam.WorldToScreenPoint(corner);
                if (sp.z <= 0f) continue;

                // 화면에서의 한 변 — 월드 길이를 화면으로 옮겨 잰다(줌을 따라간다).
                Vector3 edge = cam.WorldToScreenPoint(corner + new Vector3(sizeWorld, 0f, 0f));
                float px = Mathf.Abs(edge.x - sp.x);
                if (px < 2f) continue;   // 너무 작으면 점이라 뜻이 안 선다

                float y = Screen.height - sp.y - px * 0.5f;
                float x = sp.x - px * 0.5f;
                if (x < -px || x > Screen.width || y < -px || y > Screen.height) continue;

                // ⚠️ **인셋 전투 자리에는 안 그린다** — 보드는 안 보이는데 표식만 뜬다.
                if (y < CombatInsetView.BottomPixels(Screen.height)) continue;

                GUI.DrawTexture(new Rect(x, y, px, px), tex, ScaleMode.ScaleToFit);
            }
        }

        /// <summary>
        /// 마운트 이름표 — **그림 아래**에 무엇을 싣는 마운트인지 적는다 (2026-09-14 · §71-41).
        ///
        /// ⚠️ **09-11 의 「칸 안에 넣는다」와 어긋나지 않는다.** 그때 밖으로 나가 잘린 것은
        /// **격자 한 칸** 위의 이름표였다. 마운트 본체는 **실루엓 바깥**에 서므로 그 아래가
        /// 비어 있고, 거기가 규격이 정한 자리다(가운데 정렬).
        /// </summary>
        private void DrawMountLabels(Camera cam, Vector2 o, GUIStyle style, float fontScale)
        {
            if (_mountBodyViews.Count == 0) return;

            // ⚠️ **점멸은 그림·그리드와 같은 판정이다**(`MountBlinkOn`). 두 번 재면
            // 글자와 그림이 서로 다른 박자로 깜빡인다.
            bool blink = MountBlinkOn;
            // ⚠️⚠️ **글자를 자르지 않는다**(2026-09-15 · 사용자 육안 4차 ⑧ — 「마운트」가 「마우」).
            //
            // 자리는 맞았고 **폭 측정이 틀렸다.** `CalcSize` 는 스타일의 폰트에 물어보는데,
            // 유니티 **동적 폰트는 아직 안 구운 크기의 글리프 폭을 모른다** — 줌을 바꾸면
            // 글자 크기가 사다리를 타고 새 단으로 올라가고, 그 단이 처음 쓰이는 프레임에는
            // 폭이 **작게** 나온다. 상자는 그 작은 값으로 잡히고 글자는 제 크기로 그려져
            // **뒤가 잘린다.** 「마우」가 그것이다.
            //
            // 📌 **같은 병의 다섯째 얼굴이다** — 아틀라스가 「마으ㅌ」·「판1」·「먹춤」·
            // 「하단 글자 소멸」로 왔고, 이번엔 **폭을 속이는 쪽**으로 왔다.
            //
            // **재는 것을 고치지 않고 자르기를 끈다.** 측정이 몇 픽셀 틀려도 글자는 온전히
            // 나오고, 가운데 정렬이라 좌우로 고르게 넘친다 — 이름표는 바탕이 없어 넘쳐도 안 겹친다.
            var mountStyle = new GUIStyle(style)
            {
                alignment = TextAnchor.UpperCenter,
                clipping = TextClipping.Overflow,
                wordWrap = false,
            };
            Color prev = GUI.color;
            GUI.color = blink ? MountEmptyColor : ZoneLabelColor;

            float boxH = mountStyle.fontSize + 6f * fontScale;

            foreach (MountPort mp in PartLayout.MountPorts)
            {
                // ⚠️ **활성 로봇 것만 적는다**(§72-24 ②) — 그림·그리드와 같은 판정이다.
                if (!ShowsMount(mp.owner)) continue;

                // ⚠️ **「마운트」 한 낱말로 줄였다**(2026-09-14 · 2차 스크린샷 1장).
                //
                // 종전에는 「마운트 · 탄약 적재」·「마운트 · 드론 적재」로 **로봇마다 다른 것을
                // 싣는다**는 것까지 글자에 담았다. 그런데 글자 높이는 **아트 픽셀**이라
                // (96 = 반 칸 · 2026-09-06 확정) 열 글자면 **다섯 칸 폭**이 되어, 한 칸짜리
                // 묶음 위에서 좌우로 통째로 삐져나왔다.
                //
                // **96 은 안 건드린다** — 확정값이고, 확대해도 이름표가 안 작아지는 근거다.
                // 줄일 것은 **글자 수**다. 무엇을 싣는지는 이름표가 아니라 **그림과 틴트**가
                // 이미 가른다(`BuildMountBodies` 의 스프라이트 · `MountDisplay.FlowOf`).
                //
                // ⚠️ **§71-41 의 구현 가정이라 재량으로 줄인다**(사용자 확인 09-14).
                // 문구가 문서로 서면 그때 이 한 줄이 바뀐다.
                const string text = "마운트";
                // 측정이 작게 나와도 넘쳐서 보이지만(위 `Overflow`), 상자가 너무 좁으면
                // 가운데가 어긋난다 — **글자 수 × 글자 크기**를 하한으로 둔다.
                float boxW = Mathf.Max(mountStyle.CalcSize(new GUIContent(text)).x,
                                       text.Length * mountStyle.fontSize);

                // **묶음 아래**(§72-5) — 묶음 맨 아랫 칸의 밑변이다.
                //
                // ⚠️ **B 왼쪽 이름표는 A 묶음의 맨 윗 칸과 맞닿은 줄에 앉는다** —
                // 두 묶음이 같은 열에서 위아래로 붙어 있어 빈 줄이 없기 때문이다.
                // 글자가 위에 얹히므로 읽히긴 하지만, 자리를 옮길지는 설계가 정한다.
                float half = config.cellSize * 0.5f;
                Vector3 c = CellWorld(MountDisplay.SlotCell(mp.cell, mp.face, mp.owner, 0));
                Vector3 under = new Vector3(c.x, c.y - half, 0f);
                Vector3 sp = cam.WorldToScreenPoint(under);
                if (sp.z <= 0f) continue;

                // ⚠️⚠️ **묶음 안쪽으로 들인다**(2026-09-21 사용자 육안 — 「마운트 이름표가
                //    아래 노드 이름판과 겹친다」).
                //
                // 🗑️ 구 자리(묶음 **밑변 아래**) 폐기. 노드 이름판은 타일 **위쪽 안쪽**에
                // 앉으므로, 마운트 아래 칸에 노드가 있으면 둘이 **정확히 같은 줄**을 쓴다 —
                // x3·x8 의 「변환기」가 그 자리였다.
                //
                // 📌 **아래 칸은 그 칸의 것이 쓴다.** 이름표가 제 묶음 안으로 들어오면
                //    「무엇에 붙은 이름인가」도 오히려 또렷해진다.
                float y = Screen.height - sp.y - boxH;
                float x = sp.x - boxW * 0.5f;
                if (x < -boxW || x > Screen.width || y < -boxH || y > Screen.height + boxH) continue;

                // ⚠️ **인셋 전투 자리에는 안 그린다** — 구역 이름표와 같은 이유다(보드는
                // 안 보이는데 글자만 뜼다).
                if (y < CombatInsetView.BottomPixels(Screen.height)) continue;

                GUI.Label(new Rect(x, y, boxW, boxH), text, mountStyle);
            }

            GUI.color = prev;
        }



        /// <summary>
        /// 경계 한 변의 색 — **그 변에 닿는 파츠**의 색 (2026-09-11 · 플랜 §71-16 ⑧).
        ///
        /// ⚠️ **두 파츠가 맞닿은 변은 미색으로 남긴다.** 어느 쪽 색을 주어도 한쪽이
        /// 다른 쪽 영역을 침범한 것처럼 읽히고, 점선은 **두 파츠가 공유하는 한 줄**이라
        /// 실제로 어느 한쪽 것이 아니다(그래서 파츠 루프 밖에서 한 번만 그린다).
        /// 실루엣 바깥면만 제 파츠 색을 갖는다 — 거기서는 안쪽 파츠가 하나뿐이다.
        ///
        /// ⚠️ **변 전체가 한 파츠에 닿을 때만** 그 색이다. 한 변이 여러 파츠를 지나면
        /// 중간에 색이 갈려 점선이 끊긴 것처럼 보이므로 미색으로 둔다.
        /// </summary>
        private static Color BoundaryColor(PartLayout.BoundaryRun r, out bool outer)
        {
            outer = false;
            RobotPart found = RobotPart.None;

            for (int i = 0; i < r.length; i++)
            {
                // 변의 양쪽 칸. 가로변이면 위·아래, 세로변이면 좌·우다.
                Vector2Int a = r.horizontal
                    ? new Vector2Int(r.from.x + i, r.from.y)
                    : new Vector2Int(r.from.x, r.from.y + i);
                Vector2Int b = r.horizontal
                    ? new Vector2Int(r.from.x + i, r.from.y - 1)
                    : new Vector2Int(r.from.x - 1, r.from.y + i);

                RobotPart pa = PartLayout.PartAt(a);
                RobotPart pb = PartLayout.PartAt(b);

                // 양쪽 다 파츠면 공유 변이다 — 미색.
                if (pa != RobotPart.None && pb != RobotPart.None) { outer = false; return PartPalette.Neutral; }

                RobotPart inner = pa != RobotPart.None ? pa : pb;
                if (inner == RobotPart.None) continue;      // 둘 다 실루엣 밖(있을 수 없지만 안전)
                if (found == RobotPart.None) found = inner;
                else if (found != inner) { outer = false; return PartPalette.Neutral; } // 한 변이 여러 파츠를 지난다
            }

            if (found == RobotPart.None) return ZoneLineColor;

            // 한쪽만 파츠인 변 = **실루엣 바깥**이다. 진하고 굵게.
            outer = true;
            return PartPalette.OuterLineOf(found);
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
            KeepBoardOnItsLayer();
        }

        /// <summary>지난 번에 본 계층 속 물건 수. 바뀜었을 때만 층을 다시 입힌다.</summary>
        private int _lastHierarchyCount = -1;

        /// <summary>
        /// 보드가 만든 것을 전부 **보드 층**에 둔다 (2026-09-16 · 육안 ②).
        ///
        /// ⚠️⚠️ **만드는 자리가 스물네 곳이다**(`new GameObject` 이 이 파일에만 스물네 번).
        /// 그 자리마다 층을 적으면 **빠뜨리는 자리가 생긴다** — 오늘까지 이 리포에서
        /// 반복된 결함이 대개 그 모양이었다. 그래서 **나중에 한 곳에서 거둔다.**
        ///
        /// 매 프레임 계층을 도는 것은 낭비라, `hierarchyCount`(손자까지 포함한 수)가
        /// 바뀜었을 때만 다시 입힌다 — 평소에는 정수 비교 하나다.
        /// </summary>

        private void KeepBoardOnItsLayer()
        {
            int count = transform.hierarchyCount;
            if (count == _lastHierarchyCount) return;
            _lastHierarchyCount = count;

            gameObject.layer = BoardLayer.Index;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = BoardLayer.Index;
        }

        private void Update()
        {
            if (BoardDumpSignals.Requested) FulfilBoardDump();

            FollowActiveRobotTab();

            // ⚠️ **게이트가 이것을 읽는다**(2026-09-11 · §71-19 ①). 국면은 신호만으로
            // 안 정해진다 — 이동 모드인 채로 고스트가 뜨면 할 수 있는 일은
            // 「조립 모드로 바꾸기」 하나뿐이다.
            TutorialSignals.BoardInBuildMode = _mode == BoardMode.Build;

            UpdateTutorialDim();

            UpdateMountPortBlink();

            TickZoomGesture(); // 핀치·휠 (2026-09-15 · 개편 ⑦ — 배율 버튼을 대신한다)
            ApplyZoom(); // 보드를 볼 때만 확대한다 — 나가면 원래 시야로 돌아간다

            // 촬영 복귀 요청 — 가져가며 내린다.
            if (TutorialSignals.ClearEmptySlotRequested)
            {
                TutorialSignals.ClearEmptySlotRequested = false;
                RemoveAt(StartingBoard.EmptySlot);
            }

            // 심사자 바로가기가 튜토리얼을 건너뛰고 들어왔다 — 그 칸을 채워 준다
            // (2026-09-15 사용자 확정 · 육안 ⑦). 안 채우면 운반로가 끊긴 채라 탄이 안 닿는다.
            if (TutorialSignals.FillEmptySlotRequested)
            {
                TutorialSignals.FillEmptySlotRequested = false;
                // 튜토리얼 칸은 **A 판**의 것이다 — 지금 B 를 보고 있어도 A 에 채운다.
                PlaceTutorialFill(_boards[Index(MountOwner.RobotA)]);
                if (_editing == MountOwner.RobotA) RespawnMarkersFromGrid();
                RefreshConnections();   // 벨트 한 칸이 라인을 잇는다 — 안 다시 풀면 안 흐른다
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

            // ⚠️ **무엇을 하는 손인지는 시작 칸과 칸 수가 정한다**(2026-09-15 사용자 확정 · 개편 ①).
            //
            // · 빈 칸에서 시작 → 벨트 설치(탭이든 드래그든)
            // · 노드·벨트에서 시작한 **드래그** → 제거
            // · **한 칸(탭)은 제거가 아니다** — 벨트 탭은 아무 일 없고 노드 탭은 팝오버다.
            //
            // ⚠️ **튜토리얼 동안에는 안 지운다** — 놓으라고 해 놓고 지울 수 있으면
            // 「지금 할 일」이 둘이 된다(구 「제거」 버튼이 걸고 있던 것과 같은 문).
            if (TutorialGate.Allows(TutorialGate.Control.Remove)
                && RemovalRules.IsRemovalDrag(_dragCells[0], _dragCells.Count,
                    _grid.IsOccupied, _grid.HasBelt, IsCore))
            {
                FinishRemovalDrag();
                _dragCells.Clear();
                return;
            }

            if (_dragCells.Count == 1)
            {
                Vector2Int cell = _dragCells[0];
                // 모듈을 고른 채 놓인 노드를 탭하면 **붙인다.** 못 붙이면(칸이 다 찼다)
                // 고르기로 떨어진다 — 탭이 아무 일도 안 하면 조작이 먹지 않은 것으로 읽힌다.
                if (_grid.IsOccupied(cell) && TryAttachSelectedModule(cell)) { }
                else if (_grid.IsOccupied(cell)) Select(cell);   // 다른 노드면 그 노드로 갈아탄다
                else if (_elementMode.HasValue) PlaceElement(cell, _elementMode.Value);
                // ⚠️ **벨트 탭은 아무 일도 안 한다**(2026-09-15 사용자 확정 · 개편 ①).
                // 지우려면 끌어야 한다 — 한 번 눌러 지워지면 실수가 곧 손실이다.
                else if (_grid.HasBelt(cell)) { if (_selected.HasValue) Deselect(); }
                else
                {
                    // ⚠️ **팝오버는 바깥을 누르면 닫힌다**(2026-09-15 사용자 확정 · 육안 ③).
                    //
                    // 종전에는 닫는 길이 **없었다** — 한 번 열면 다른 노드를 누를 때까지
                    // 화면 한쪽을 계속 가렸다. 빈 칸을 누르는 것은 「여기에 놓겠다」이기도 한데,
                    // **열려 있는 동안에는 닫기가 먼저다** — 팝오버가 덮은 자리를 잘못 눌러
                    // 엉뚱한 칸에 노드가 놓이는 것을 막는다.
                    if (_selected.HasValue) Deselect();
                    else Place(cell);
                }
            }
            else if (_dragCells.Count > 1)
            {
                LayBelts(_dragCells);
            }
            _dragCells.Clear();
        }

        /// <summary>
        /// 지우는 드래그가 끝났다 (2026-09-15 사용자 확정 · 육안 ⑧).
        ///
        /// **벨트만 지났으면 즉시 지운다.** 벨트는 한 칸이 곧 한 조각이고, 잘못 지워도
        /// 다시 끌면 되돌아온다 — 물어보는 값이 지우는 값보다 작다.
        ///
        /// **노드 칸을 하나라도 지났으면 묻는다.** 노드는 조합표·탄종·모듈·회전을 지고
        /// 있어서 **다시 끄는 것으로 안 돌아온다.** 그래서 한 번 더 손을 받는다.
        ///
        /// ⚠️ **묻고 나서는 경로 전체를 지운다** — 벨트만 지우고 노드를 남기면 손이
        /// 지나간 자리와 결과가 달라진다.
        /// </summary>
        private void FinishRemovalDrag()
        {
            if (_dragCells.Count == 0) return;

            // ⚠️ **코어는 못 지운다**(2026-09-15 사용자 확정) — 경로에서 빼고 나머지를 본다.
            List<Vector2Int> targets = RemovalRules.Removable(_dragCells, IsCore);
            if (targets.Count == 0) return;

            // 판정은 `RemovalRules` 가 한다 — 여기는 손을 받고 결과를 그릴 뿐이다(§3).
            if (!RemovalRules.NeedsConfirm(targets, _grid.IsOccupied))
            {
                foreach (Vector2Int c in targets) RemoveAt(c);
                return;
            }

            // 팝업이 뜨는 동안 경로를 들고 있어야 한다 — `_dragCells` 는 곧 비워진다.
            _pendingRemoval = targets;
        }

        /// <summary>
        /// 컨펌을 받은 뒤 경로 전체를 지운다. 노드가 섞여 있어 되돌릴 수 없는 삭제다.
        /// </summary>
        private void ApplyPendingRemoval()
        {
            if (_pendingRemoval == null) return;
            foreach (Vector2Int c in _pendingRemoval) RemoveAt(c);
            _pendingRemoval = null;
        }

        /// <summary>
        /// 「정말 삭제?」 — 노드가 섞인 제거 드래그에만 뜬다 (2026-09-15 · 육안 ⑧).
        ///
        /// ⚠️ **맨 뒤에 그린다** — IMGUI 는 뒤에 그리는 쪽이 위다. 앞에 그리면 팔레트가
        /// 이 팝업을 덮는다(09-10 에 오프라인 대화상자가 「게임 시작」을 덮은 그 자리).
        ///
        /// ⚠️ **뒤를 막는다** — 막을 깔지 않으면 팝업이 떠 있는 동안에도 보드가 눌린다.
        /// </summary>
        private void DrawRemovalConfirm()
        {
            if (_pendingRemoval == null) return;

            float sc = UiLayout.Scale(Screen.height);
            float w = 560f * sc, h = 300f * sc;
            var box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            // ⚠️ **뒤는 반투명 DIM 이다**(2026-09-15 육안 ② · 결함 수정).
            //
            // 종전에는 `UiSkin.DisabledTexture` 를 전체 화면에 깔았다. 그것은 **잠긴 버튼
            // 바탕**이라 불투명이고, 화면에서는 팝업이 아니라 **판이 화면을 통째로 덮은 것**으로
            // 보였다 — 무엇을 지우려는지가 안 보이면 물음에 답할 근거가 사라진다.
            var full = new Rect(0f, 0f, Screen.width, Screen.height);
            Color prevDim = GUI.color;
            GUI.color = RemovalDimColor;
            GUI.DrawTexture(full, Texture2D.whiteTexture);
            GUI.color = prevDim;

            UiBlockers.Add(full);
            UiSkin.DrawPlate(box);   // 9-슬라이스(육안 4차 ④)

            float pad = 24f * sc;
            var head = new GUIStyle(GUI.skin.label)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(9, Mathf.RoundToInt(34f * sc))),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            var body = new GUIStyle(GUI.skin.label)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(9, Mathf.RoundToInt(24f * sc))),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };

            int nodes = RemovalRules.NodeCount(_pendingRemoval, _grid.IsOccupied);

            GUI.Label(new Rect(box.x, box.y + pad, box.width, 48f * sc), "정말 삭제?", head);
            GUI.Label(new Rect(box.x + pad, box.y + pad + 56f * sc, box.width - pad * 2f, 90f * sc),
                "노드 " + nodes + "대를 포함해 " + _pendingRemoval.Count
                + "칸을 지운다. 노드에 붙인 조합표·모듈·방향은 돌아오지 않는다.", body);

            var btn = new GUIStyle(GUI.skin.button)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(9, Mathf.RoundToInt(26f * sc))),
            };
            float bw = (box.width - pad * 3f) * 0.5f, bh = 72f * sc;
            float by = box.yMax - pad - bh;

            if (UiSkin.Button(new Rect(box.x + pad, by, bw, bh), "취소", btn)) _pendingRemoval = null;
            if (UiSkin.Button(new Rect(box.x + pad * 2f + bw, by, bw, bh), "삭제", btn))
                ApplyPendingRemoval();
        }

        // 드래그 경로 → 벨트 세그먼트 설치(§5-4). 점유(노드/기존벨트) 셀은 건너뜀.
        private void LayBelts(List<Vector2Int> cells)
        {
            List<BeltSegmentSpec> segs = BeltPath.Build(cells);

            // 놓는 **그 순간에만** 양 끝을 이웃 노드에 맞춘다
            // (2026-09-16 사용자 확정 ⓑ+ⓒ · 플랜 §74-12 A).
            //
            // ⚠️ 놓은 뒤의 회전은 안 건드린다 — 계속 따라가게 하면 플레이어가 일부러
            //    돌려 둔 방향을 코드가 덮는다. 저장이 싣는 면도 그대로다.
            int snapped = BeltDropSnap.Snap(_grid, segs);

            int placed = 0;
            foreach (BeltSegmentSpec s in segs)
            {
                if (!_grid.TryPlaceBelt(s.cell, s.inFace, s.outFace, FlowKind.Material, out _)) continue;
                SpawnBeltMarker(s.cell, s.outFace);
                placed++;
            }
            RefreshConnections();
            Debug.Log($"[MBI] 벨트 설치: 드래그 {cells.Count}칸 → 세그먼트 {segs.Count}, "
                      + $"신규 배치 {placed}, 노드에 맞춘 끝 {snapped}.");
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
        /// 지금 포인터가 OnGUI 버튼 위에 있는가 — <see cref="UiBlockers"/> 참조.
        ///
        /// IMGUI 좌표는 **위에서 아래로** 재고 포인터는 아래에서 위로 재므로 y를 뒤집는다.
        /// </summary>
        private bool PointerOverUi()
        {
            if (Pointer.current == null) return false;
            Vector2 p = Pointer.current.position.ReadValue();
            var gui = new Vector2(p.x, Screen.height - p.y);

            return UiBlockers.Contains(gui);
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
        /// <summary>
        /// 표준탄이 벨트 위에서 **아무것도 안 그린다** — 임시 (2026-09-15 오후 · 사용자 지시).
        ///
        /// ⚠️⚠️ **표시만 끈다. 값도 논리도 안 건드린다.** 흐름·인계·병합기 「합」 글자는 그대로고,
        /// 다른 품목 그림도 그대로다. 표준탄 하나만 **안 보이게** 한다.
        ///
        /// 왜 — `ammo_standard.png` 가 벨트 위에서 **주황 정사각**으로 읽힌다.
        /// 실측: 실루엣 40×20 (캔버스 64 중 800px² · 열두 품목 중 가장 작다) · 외곽 채움 94% ·
        /// 색 덩이 열셋이 전부 주황-갈색. 크기 셈은 09-15 에 고쳤는데도 **그림 자체가**
        /// 그 크기에서 사각으로 보인다. 폴백 사각으로 떨어뜨리면 더 나빠지므로 둘 다 끈다.
        ///
        /// 📌 **되돌리는 법은 이 값을 `false` 로 두는 것 하나다.** 새 자산이 오면 그렇게 한다 —
        /// 「자산이 기준을 넘는가」를 코드가 재게 하지 않는다(재는 규칙을 또 하나 만들면
        /// 그것이 다음 결함이 된다). 사람이 보고 끈다.
        /// </summary>
        /// ✅ **되돌렸다(2026-09-16)** — 아트가 표준탄을 새로 냈다(`ed1e7c6` 「표준탄 세우기」).
        /// 실측: 실루엣 40x20(800px²) → **22x55(1089px²)** · 캔버스 64 중 span 0.86.
        /// 사용자 지시가 「**새 자산이 올 때까지** 안 그린다」였으므로 조건이 풀렸다.
        /// 다시 끄려면 이 값을 `true` 로 두는 것 하나다.
        private const bool HideStandardAmmoItemArt = false;

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
                    // 표준탄은 지금 안 그린다 — 위 `HideStandardAmmoItemArt` 참고.
                    // ⚠️ `used` 를 안 올린다 — 올리면 빈 자리가 풀에 생겨 다음 것이 밀린다.
                    if (HideStandardAmmoItemArt && items[i].kind == FlowKind.StandardAmmo) continue;

                    // 코너에서 꺾이는 경로는 `BeltItemPose`가 안다 — 여기서 다시 풀지 않는다.
                    Vector2 off = BeltItemPose.LocalOffset(
                        belt.InFace, belt.OutFace, items[i].progress);

                    SpriteRenderer sr = RentItemSprite(used++, out SpriteRenderer body);

                    // 품목 그림이 있으면 **테두리 두 겹을 쓰지 않는다** — 겹은 흰 사각으로
                    // 외곽선을 흉내 내려던 것이고, 그림에는 이미 외곽선이 있다.
                    Sprite itemArt = art != null ? art.ItemSprite(items[i].kind) : null;
                    if (itemArt != null)
                    {
                        sr.sprite = itemArt;
                        sr.color = Color.white;
                        // ⚠️ **캔버스가 아니라 그림에 맞춘다**(2026-09-15 · 육안 4차 ⑥ · 실측).
                        //
                        // 종전에는 `FitScale` 이 캔버스(64×64)를 목표 크기에 맞췄다. 그런데
                        // `ammo_standard` 는 그 64 안에 **40×20 만** 그려져 있어서,
                        // 캔버스를 0.26 칸에 맞추면 **보이는 것은 0.16×0.08 칸**이 됐다 —
                        // 한 칸 80픽셀에서 13×6픽셀, 곧 **주황 얼룩**이다.
                        // `core_energy` 는 54×52 로 캔버스를 거의 채워 같은 셈에도 멀쩡했고,
                        // **그래서 한 품목만 이상해 보였다.**
                        //
                        // ⚠️ 09-15 에 이 자리를 「그림이 맞으니 결함 아님」으로 닫았던 것이
                        // 틀렸다. 그림은 맞았고 **크기 셈이 틀렸다.**
                        float span = art.ItemContentSpan(items[i].kind);
                        sr.transform.localScale = Vector3.one
                            * FitScale(itemArt, _grid.CellSize * ItemDrawSize / span);
                        body.enabled = false;
                    }
                    else
                    {
                        sr.sprite = UnitSprite();
                        sr.color = ItemEdgeColor;
                        sr.transform.localScale = Vector3.one * (_grid.CellSize * ItemDrawSize);
                        body.enabled = true;
                        body.color = ItemColor(items[i].kind);
                    }

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
            if (!_grid.TryPlace(cell, node, out NodeInstance placedNode)) return;

            // 🗑️ **미리 돌려 두는 길 폐기**(2026-09-18 ⑩) — 팔레트 버튼이 없어졌다.
            //    놓은 뒤 팝오버의 「90도 돌리기」가 그 일을 한다.

            SpawnNodeMarker(cell);
            // 격자에 붙는 순간의 딸깍(사운드 문서 3장). **놓는 데 성공했을 때만** —
            // 실패한 탭에 소리가 붙으면 안 놓인 것이 놓인 것처럼 들린다.
            AudioSignals.Play(SoundIds.NodeSnap, SoundIds.KindOf(SoundIds.NodeSnap));
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

        /// <summary>
        /// 고른 모듈을 이 칸의 노드에 붙인다 (2026-09-09 신설 · MVP 문서 11장).
        ///
        /// **붙었을 때만 <c>true</c>다.** 칸이 다 찼거나 벨트 칸이면 false를 주고,
        /// 부르는 쪽은 종전대로 「고르기」로 떨어진다.
        /// </summary>
        private bool TryAttachSelectedModule(Vector2Int cell)
        {
            if (_selectedModule < 0 || modulePalette == null ||
                _selectedModule >= modulePalette.Count) return false;

            ModuleDefinition module = modulePalette[_selectedModule];
            NodeInstance node = _grid.GetAt(cell);
            if (module == null || node == null) return false;

            if (!node.TryAttachModule(module))
            {
                Debug.Log($"[MBI] 모듈 칸이 찼다 — {node.Definition.displayName} @ 셀({cell.x},{cell.y}) · 칸 {NodeInstance.ModuleSlots}개.");
                return false;
            }

            // 붙는 순간 전력 수요가 바뀐다 — 다시 안 돌리면 변수 패널이 옛 수요를 든 채로 남는다.
            RefreshConnections();
            RefreshModuleSymbols(cell);
            Debug.Log($"[MBI] 모듈 장착: {module.displayName} → {node.Definition.displayName} " +
                      $"@ 셀({cell.x},{cell.y}) · 산출 ×{node.ModuleOutputMultiplier:F2} · 부하 ×{node.ModulePowerLoadMultiplier:F2}.");
            return true;
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
            Sprite nodeArt = NodeArtOf(cell);
            marker.transform.localScale = nodeArt != null
                ? Vector3.one * FitScale(nodeArt, _grid.CellSize)
                : Vector3.one * (_grid.CellSize * ArtSpec.TileSize);
            var sr = marker.AddComponent<SpriteRenderer>();
            sr.sprite = nodeArt != null ? nodeArt : UnitSprite();
            sr.sortingOrder = MarkerOrder;
            Color c = SeverityColor(cell, 1f); // 초기 = 정상 밝기. Provider가 라이브 진단으로 갱신(§L4-R #5).
            sr.color = c;
            _markers[cell] = marker;
            _nodeColors[cell] = c;

            SpawnPortMarkers(cell, marker.transform);
            SpawnModuleSymbols(cell, marker.transform);
        }

        /// <summary>
        /// 그 칸의 마커를 **지우고 다시 짓는다** — 회전 뒤에 부른다 (2026-09-15 · §72-42).
        ///
        /// ⚠️ **포트 탭이 면에 붙어 있다.** 면이 돌았는데 마커를 그대로 두면
        /// **그림은 돌았는데 탭은 안 돈** 자리가 되어, 벨트를 어디 붙일지가 거짓으로 읽힌다.
        /// </summary>
        private void RebuildMarker(Vector2Int cell)
        {
            if (_markers.TryGetValue(cell, out GameObject old) && old != null) Destroy(old);
            _markers.Remove(cell);
            _portMarkers.Remove(cell);
            SpawnNodeMarker(cell);
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
            foreach (NodePort p in inst.Ports())
            {
                Vector2 off = FaceOffset(p.face);
                bool outward = p.io == PortIO.Output;

                var tab = new GameObject(outward ? $"out_{p.face}" : $"in_{p.face}");
                tab.transform.SetParent(parent, false);

                // ⚠️ **타일 전체 겹침을 걷었다**(2026-09-15 사용자 확정 · §72-51 · 보드 4-2 폐기).
                //
                // 포트 그림은 **한 칸을 통째로 쓰는 타일**이라, 면마다 한 장씩 얹으면
                // **노드 그림이 포트 타일 넷에 덮였다** — 무엇을 놓았는지가 안 보였다.
                // 필요한 것은 「어느 변으로 드나드는가」 하나이고 그건 **작은 커넥터**면 된다.
                //
                // **1단계는 코드 드로잉**이다(사용자 확정). 아트가 작은 자산 둘을 주면
                // 그때 `portArt` 갈래를 되살려 그림으로 바꾼다 — 지금은 타일을 안 쓴다.
                //
                // ⚠️ **크기는 칸의 약 1/6 — 가정이다**(설계 역기입). 입구는 변 **안쪽**에
                // 파이고, 출구는 변 **밖으로** 나간다 — 색을 못 가려도 **자리로** 갈린다.
                Sprite portArt = null;
                {
                    const float Thin = 0.10f;   // 변을 따라 얇은 쪽 (칸 대비)
                    const float Long = 0.22f;   // 변을 따라 긴 쪽
                    float dist = outward ? 0.56f : 0.38f;

                    tab.transform.localPosition = new Vector3(off.x * dist, off.y * dist, 0f);

                    // 면을 따라 납작하게 — 세로면이면 눕히고 가로면이면 세운다.
                    bool horizontal = Mathf.Abs(off.x) > 0.5f;
                    tab.transform.localScale = horizontal
                        ? new Vector3(Thin, Long, 1f)
                        : new Vector3(Long, Thin, 1f);
                }

                var sr = tab.AddComponent<SpriteRenderer>();
                sr.sprite = portArt != null ? portArt : UnitSprite();
                sr.sortingOrder = outward ? PortOutOrder : PortInOrder;
                list.Add(new PortMarker
                {
                    sr = sr, io = p.io, declared = p.kind, hasArt = portArt != null,
                });
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
                // 그림이 붙었으면 품목색을 안 칠한다 — 화살표 그림이 이미 방향을 말하고,
                // 그 위에 색을 곱하면 아트가 통째로 물든다. 밝기 축(입력은 어둡게)만 남긴다.
                Color c = pm.hasArt ? Color.white : FlowColor(outward ? outKind : pm.declared);
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
                if (_selected.HasValue && _selected.Value == cell)
                {
                    _selected = null;
                    // 노드를 지웠는데 테두리가 남으면 빈 칸이 골라져 있는 것으로 읽힌다.
                    if (_selectRing != null) _selectRing.SetActive(false);
                }
            }
            else if (_grid.HasBelt(cell))
            {
                _grid.TryRemoveBelt(cell);
                if (_beltMarkers.TryGetValue(cell, out GameObject bm) && bm != null) Destroy(bm);
                _beltMarkers.Remove(cell);
                _beltBodies.Remove(cell);
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
            // 메인 메뉴가 덮고 있으면 그리지 않는다 — IMGUI 는 뒤에 그리는 쪽이 위로 온다
            // (2026-09-10 · 실측: 오프라인 대화상자가 「게임 시작」 버튼을 덮었다).
            if (MainMenuGate.IsOpen) return;
            if (!GameLayerController.BoardViewActive) return;
            UiSkin.Apply(); // 껍데기 + 한글 폰트 — WebGL엔 시스템 폰트 폴백이 없다

            DrawSupplyWarningBand(); // 0차 — 다른 표시보다 먼저 그린다(UI 문서 12-4)
            // ⚠️ **차례를 뒤집었다**(2026-09-15 · 육안 ③ 겹침).
            //
            // 종전에는 구역 이름표를 **먼저**(=아래) 그렸다. 09-15 에 노드 이름판이
            // 되살아나면서(개편 ⑥) 구역 왼윗 칸의 노드 판이 **구역 이름표를 덮었다** —
            // 「팔R」 위에 에너지 노드 이름판이 얹혔다.
            //
            // 구역 이름은 **어느 파츠인가**라 노드 하나보다 큰 말이고, 겹치면 그쪽이
            // 살아남아야 한다. IMGUI 는 **뒤에 그리는 쪽이 위**다.
            // ⚠️⚠️ **보드 위 글자는 보드 띠 밖으로 나가면 안 된다**
            // (2026-09-15 · 사용자 육안 4차 ⑨ · §71-23 과 같은 뿌리).
            //
            // 이름판·품목 아이콘·「합」은 **월드 좌표를 화면으로 옮겨** 그린다. 그런데 IMGUI 는
            // 화면 전체가 도화지라, 보드를 끌어 칸이 띠 위로 올라가면 **글자가 전투 인셋 위에
            // 그려진다** — 카메라는 보드를 안 비추는데 글자만 떠 있는 꼴이다.
            //
            // 📌 **구역 이름표는 이미 따로 막아 두었다**(「인셋에 가린 구역 숨김」). 그때
            // 한 칸씩 막는 길을 골랐는데, **새로 그리는 것마다 같은 처리를 또 해야 했다** —
            // 이번 ⑨ 가 그 대가다. 그래서 여기서는 **그릇 하나로** 막는다.
            //
            // `BeginClip` 의 둘째 인자에 자리를 음수로 넣으면 **화면 좌표가 그대로 유지된다** —
            // 안쪽 코드는 지금처럼 화면 좌표로 계산하고, 잘리는 일만 그릇이 맡는다.
            Rect boardView = UiLayout.BandRect(UiLayout.Band.Board, Screen.width, Screen.height);
            GUI.BeginClip(boardView, -boardView.position, Vector2.zero, false);
            DrawCellLabels();    // 칸 라벨(노드 이름판·품목 아이콘)
            DrawZoneLabels();    // 구역 이름표 — 칸 라벨 **위**로 온다
            GUI.EndClip();

            DrawBottleneckHint();

            // ⚠️ **그릇을 먼저 깐다**(2026-09-11 재육안 2 ① 수정). 종전에는 이 판을
            // **팔레트 절에서** 그렸는데 그 절이 `DrawMiniMap`·`DrawModeButton` 보다
            // 뒤라, **판이 미니맵과 모드 버튼을 통째로 덮었다** — 모드 버튼이 「안 보인다」의
            // 원인이 그것이다(IMGUI 는 뒤에 그리는 쪽이 위로 온다).
            DrawFloatBandPlate();

            // ⚠️ **미니맵을 걷었다**(2026-09-15 사용자 확정 · 하단 개편 ①).
            // `DrawMiniMap` 자체는 폐기 표기로 남겨 둔다 — 되돌릴 때 다시 짓지 않게.
            // 🗑️ 구 주석 「그 자리는 튜토리얼 진행 두 줄이 쓴다」는 폐기 — 그 두 줄은
            //    **같은 날 보드 띠로 나갔다**(육안 7차 ②). 자리는 그 뒤로 비어 있었고,
            //    2026-09-16 부터 **로봇 탭 A·B** 가 쓴다.
            DrawRobotTabs();

            // ⚠️ **배율 막대를 걷었다**(2026-09-15 · 개편 ⑦) — 핀치·휠이 대신한다.
            DrawCategoryTabs();
            DrawModePlate();

            if (palette == null || palette.Count == 0) return;

            // ────────────────────────────────────────────────────────────────
            //  노드 팔레트 — **보드 위 세로 줄에서 부유 띠 가로 줄로** 옮겼다
            //  (2026-09-11 · 플랜 §68-4 (A) · §69 2번).
            //
            //  종전 자리는 `x = Screen.width - 142`, `y0 = 300`, 버튼 `130×36` 의 **날 픽셀**이었다.
            //  문제가 셋이었다 —
            //   ① 36px 는 문서 최소 150 의 4분의 1이다(리허설 1차 「눌러도 안 먹었다」).
            //   ② 보드 위에 떠 있어 `UiPlate` 와 튜토리얼 DIM 이 **겹치는 유일한 자리**였다.
            //   ③ 창이 작으면 아래가 잘려 「제거」와 조작 안내가 안 보였다(2026-09-09 실측).
            //  부유 띠로 내보내면 셋이 **한꺼번에** 없어진다.
            //
            //  ⚠️ **가로로 넘치면 스크롤한다.** 열한 칸 × 150 = 1650 이라 기준 캔버스 1440 을
            //  넘는다 — 줄여서 맞추면 ①로 돌아가므로 **크기를 지키고 스크롤을 준다.**
            //  합체·태그 원형과 자리를 다투지 않는다(그 둘은 전투 화면에만 그려진다).
            // ────────────────────────────────────────────────────────────────
            float sc = UiLayout.Scale(Screen.height);

            // ⚠️ **띠 전체에 그릇을 깐다**(2026-09-11 실측 · 플랜 §71-19 ②).
            //
            // 종전에는 팔레트 자리에만 판을 깔아, 미니맵·모드 버튼 칸에는 **보드가 그대로
            // 비쳐 보였다.** 사용자 눈에는 「캐릭터 그림」으로 보였는데 **실측하니 보드
            // 실루엣의 다리**였다 — 보드 카메라는 화면 전체를 그리고(조립 화면 세로 월드
            // −25 ~ −15) 부유 띠는 **격자 행 2.5~3.3** 위에 얹힌다. 그 행이 `다리L`·`다리R`
            // 구역(y 0~3)이다. 그림 출처는 `Art/Backgrounds/bg_board.png`(바닥 타일)와
            // 그 위의 파츠 색·셀선이다.
            //
            // ⚠️ **판은 임시 가림이다 — 구조 정리가 남아 있다.**
            // UI 문서 9-4 가 보드 뷰포트를 **768~2098** 로 이미 정해 두었으므로 카메라를
            // 그 띠로 자르는 것이 옳고, **이것은 판정거리가 아니라 구현 재량**이다
            // (조립 모드에서만 `Camera.rect` 를 조정하거나, 인셋처럼 두 번째 카메라를 둔다).
            // 그 카메라는 전투 화면도 쓰므로 **촬영 뒤에 손댄다** — 지금 바꾸면 전투
            // 프레이밍이 함께 움직인다. 촬영 전에는 판으로 충분하다.
            // (판 자체는 `DrawFloatBandPlate` 가 **이 절보다 먼저** 깐다.)
            Rect band = UiLayout.PaletteRect(Screen.width, Screen.height);

            float pad = 12f * sc;
            // ⚠️ **216 은 띠가 허락하는 최대다**(2026-09-15 사용자 확정 C안 · 육안 6차 ⑥).
            // 부유 띠 312 − 탭 줄 96 = 216. 띠보다 커질 수는 없으므로 아래 `Min` 은 남긴다.
            float side = Mathf.Min(UiLayout.PaletteButtonSize * sc, band.height - pad * 2f);

            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(9, Mathf.RoundToInt(side * 0.13f))),
                wordWrap = true,
                // ⚠️ **가로 가운데**(사용자 확정) — 그림 위 · 글자 아래로 묶어 세로도 가운데에 둔다.
                alignment = TextAnchor.LowerCenter,
                clipping = TextClipping.Overflow,
            };
            // 글자를 **아래로** 민다 — 위쪽은 노드 그림이 쓴다(`DrawPaletteThumb`).
            //
            // ⚠️ **아래 여백이 2 였다**(2026-09-15 · 육안 점검에서 잡았다). 버튼이 216 으로
            // 커지면서 글자가 **버튼 맨 밑변에 붙었고**, 그 밑변이 곧 부유 띠의 끝이라
            // 화면에서는 **액션바에 물려 아랫부분이 잘린 것처럼** 보였다.
            // 위를 조금 덜 밀고 아래를 띄워 **글자를 버튼 안쪽에** 앉힌다.
            int padBottom = Mathf.Max(4, Mathf.RoundToInt(side * 0.08f));
            style.padding = new RectOffset(2, 2, Mathf.RoundToInt(side * 0.50f), padBottom);

            // ⚠️ **칸 수를 세는 자리다 — 버튼을 더하면 여기도 더해야 한다**(2026-09-15 결함).
            //
            // `a27182a` 에서 「방향」 버튼을 넣고 **이 줄을 안 고쳤다.** 스크롤 내용 폭이
            // 한 칸 모자라 **맨 끝 「제거」에 손이 안 닿았다** — 화면에서는
            // 「S1 에서 벨트 제거가 안 된다」로 보였다(사용자 육안 09-15).
            //
            // 팔레트 n + **방향 1** + 병합기·분류기 2 + 모듈 m.
            // ⚠️ **「제거」 한 칸이 빠졌다**(2026-09-15 · 육안 ⑧ — 제거가 제스처가 됐다).
            //
            // ⚠️ **탭이 걸러낸 것만 센다**(2026-09-15 · 하단 개편 ②). 안 세면 「전력」 탭에서
            // 버튼 둘만 보이는데 스크롤 폭은 열넷짜리라 **빈 칸을 한참 끌게 된다.**
            int visibleNodes = 0;
            for (int i = 0; i < palette.Count; i++)
                if (palette[i] != null && PaletteCategories.Shows(_tab, palette[i])) visibleNodes++;

            int visibleModules = 0;
            if (modulePalette != null && PaletteCategories.ShowsModule(_tab))
                visibleModules = modulePalette.Count;

            int elementSlots = PaletteCategories.ShowsBeltElement(_tab) ? 2 : 0;

            // 「방향」은 어느 탭에서나 뜬다 — 놓기 전 방향은 종류와 무관한 손버릇이다.
            int slots = visibleNodes + 1 + elementSlots + visibleModules;
            var view = new Rect(band.x + pad, band.y + pad,
                band.width - pad * 2f, band.height - pad * 2f);

            // ⚠️ **한 줄에 여섯은 보인다**(2026-09-15 사용자 확정 · 「노드 리스트를 6개까지」).
            //
            // 종전에는 변의 상한이 **띠 높이 하나**였다(216). 그런데 창이 좁으면 216 이
            // 가로로 서너 개밖에 안 들어가고, 나머지는 **스크롤 밖**으로 밀린다 —
            // 무엇이 더 있는지조차 안 보인다. 가로에서도 상한을 잡아 **둘 중 작은 쪽**을 쓴다.
            //
            // 📌 **216 은 그대로 상한이다** — 넓은 창에서 버튼이 더 커지지는 않는다.
            const int WantVisible = 6;
            float byWidth = (view.width - pad * (WantVisible - 1)) / WantVisible;
            side = Mathf.Max(1f, Mathf.Min(side, byWidth));

            float step = side + pad;
            var content = new Rect(0f, 0f, slots * step, side);

            // ⚠️ **휠·트랙패드로도 밀린다**(2026-09-15 사용자 확정 · 「좌우로 스크롤 되지 않음」).
            //
            // 가로 스크롤바는 띠 아래쪽에 **몇 픽셀**로 깔려서 손가락으로도 마우스로도
            // 잡기 어렵다 — 있는데 못 쓰는 것은 **없는 것과 같다.**
            // IMGUI 스크롤뷰는 휠을 **세로로만** 먹으므로 여기서 직접 가로로 돌린다.
            //
            // ⚠️ **보드 확대·축소와 안 겹친다** — 저쪽은 보드 위에서만 듣고, 이 띠는
            // `UiBlockers` 에 들어 있어 보드가 아니다.
            if (Event.current != null && Event.current.type == EventType.ScrollWheel
                && view.Contains(Event.current.mousePosition))
            {
                float d = Event.current.delta.y + Event.current.delta.x;
                _paletteScroll.x = Mathf.Clamp(
                    _paletteScroll.x + d * step * 0.5f,
                    0f, Mathf.Max(0f, content.width - view.width));
                Event.current.Use();
            }

            // ✅ **이동 모드에서도 고를 수 있다**(2026-09-21 사용자 확정 ⑧).
            //
            // 🗑️ 구 규칙 「이동 모드면 팔레트를 잠근다」 폐기. 받은 말은 「모드와 무관하게
            //    **선택이 가능할 것**」이다 — 고르는 것과 놓는 것은 다른 일이다.
            //    고를 수 없으면 「무엇을 놓을지 정하고 모드를 바꾼다」가 막힌다.
            //
            // ⚠️ **놓는 것은 모드 규칙 그대로다**(가정) — 이동 모드에서 보드를 끌면
            //    여전히 스크롤이고 배치가 아니다(`OnPressStart` 의 `Mode` 갈래).
            //    받은 말이 「선택」까지라 거기까지만 연다.
            GUI.enabled = true;

            _paletteScroll = GUI.BeginScrollView(view, _paletteScroll, content, true, false);
            float bx = 0f;

            for (int i = 0; i < palette.Count; i++)
            {
                if (palette[i] == null) continue;   // 빈 칸은 자리도 안 먹는다(탭 폭 계산과 맞춘다)
                if (!PaletteCategories.Shows(_tab, palette[i])) continue;
                var rect = new Rect(bx, 0f, side, side);

                // ⚠️ **튜토리얼 동안에는 놓을 것 하나만 켠다**(2026-09-11 · 플랜 §68-5 ①).
                //
                // 고스트가 「여기」를 가리켜도 팔레트가 전부 켜져 있으면 **무엇을 놓으라는
                // 것인지**가 안 선다 — 기획서 2장·8장의 「강제」가 그 뜻이다.
                // 채우면 저절로 풀린다(고스트가 사라지면 이 조건도 거짓이 된다).
                // ⚠️ **국면이 허용하는 것만 켠다**(2026-09-11 · 플랜 §71-16 ④).
                // 종전에는 「고스트가 떠 있으면 기초 가공소만」이었는데, 그 앞 두 국면
                // (조립 진입 · 모드 바꾸기)에서는 팔레트가 **전부 켜져 있었다.**
                bool allowed = palette[i].type == NodeType.MunitionsBasic
                    ? TutorialGate.Allows(TutorialGate.Control.PaletteMunitions)
                    : TutorialGate.Allows(TutorialGate.Control.PaletteOther);
                bool wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && allowed;

                bool sel = i == _selectedNode;
                if (UiSkin.Button(rect, (sel ? "● " : "") + palette[i].displayName, style))
                {
                    _selectedNode = i;
                    _elementMode = null;
                    _selectedModule = -1;
                }
                DrawPaletteThumb(rect, palette[i]);

                // ⚠️ **어둠막은 버튼 뒤에 그린다**(2026-09-11 재육안 · IMGUI 는 뒤에 그리는
                // 쪽이 위로 온다). 앞에 그렸더니 **버튼이 제 바탕으로 덮어** 막이 한 번도
                // 안 보였다 — 화면에서는 팔레트가 전부 밝았다. 그림도 함께 덮는다.
                if (!allowed) UiSkin.DrawLockVeil(rect);

                GUI.enabled = wasEnabled;
                bx += step;
            }

            // 🗑️ **「방향 0/90/180/270」 버튼 폐기**(2026-09-18 사용자 리허설 ⑩ ·
            //    `a27182a` 3/3 되돌림). 09-15 의 근거는 「놓기 전에 방향을 정해 두면 손이
            //    덜 간다」였는데, 화면에서는 **탭마다 각도가 바뀌어** 무엇을 놓을지 고르는
            //    자리에 **상태가 하나 더** 생겼다 — 사용자가 「탭마다 노출된다」로 잡았다.
            //
            // 📌 **회전은 놓은 뒤 팝오버의 「90도 돌리기」만 남긴다** — 돌릴 일이 드물고,
            //    드문 일은 **늘 보이는 버튼**이 아니라 그 물건을 고른 뒤에 있으면 된다.

            // 벨트 요소(§5-4 L3). 직선·코너는 드래그가 만들고, 이 둘만 탭으로 놓는다 —
            // 방향이 여러 개라 드래그 경로로는 표현되지 않는다.
            bool elementAllowed = TutorialGate.Allows(TutorialGate.Control.BeltElement);
            foreach (BeltElementKind e in PaletteCategories.ShowsBeltElement(_tab)
                         ? new[] { BeltElementKind.Merger, BeltElementKind.Sorter }
                         : System.Array.Empty<BeltElementKind>())
            {
                var eRect = new Rect(bx, 0f, side, side);
                bool on = _elementMode == e;
                bool wasE = GUI.enabled;
                GUI.enabled = wasE && elementAllowed;

                // 강제 버튼(튜토리얼 기획서 2장) — 지금 놓아야 할 것을 빛나게 한다.
                // 고스트는 **자리**만 말하므로, 무엇을 놓을지 모르면 자리를 알아도 막힌다.
                Color prevCol = GUI.color;
                if (TutorialSignals.HighlightMerger && e == BeltElementKind.Merger && !on)
                    GUI.color = new Color(1f, 0.92f, 0.45f,
                        0.75f + 0.25f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f)));

                bool pressed = UiSkin.Button(eRect, (on ? "● " : "") + ElementLabel(e), style);

                // ⚠️ **그림을 얹는다**(2026-09-18 사용자 리허설 ⑨) — 노드 버튼은 그림이
                //    있는데 이 둘만 글자뿐이라, 팔레트에서 **무엇을 놓는지가 안 보였다.**
                //    글자는 남긴다(둘은 모양이 비슷해 이름이 갈라 준다).
                if (art != null)
                {
                    Sprite icon = art.BeltSprite(e);
                    if (icon != null)
                    {
                        var iconRect = new Rect(eRect.x + eRect.width * 0.22f,
                                                eRect.y + eRect.height * 0.08f,
                                                eRect.width * 0.56f, eRect.height * 0.56f);
                        GUI.DrawTexture(iconRect, icon.texture, ScaleMode.ScaleToFit, true);
                    }
                }
                GUI.color = prevCol;

                if (pressed)
                {
                    _elementMode = on ? (BeltElementKind?)null : e;
                    _selectedModule = -1;
                }
                if (!elementAllowed) UiSkin.DrawLockVeil(eRect);
                GUI.enabled = wasE;
                bx += step;
            }

            // 모듈 2종 (2026-09-09 · MVP 문서 11장). **놓는 것이 아니라 붙이는 것**이라
            // 고른 뒤 이미 놓인 노드를 탭한다 — 빈 칸을 탭할 자리가 없다(범위 효과 폐기).
            //
            // ⚠️ 종전의 「한 줄에 둘씩」은 세로 줄에서 아래가 잘리는 것을 막던 장치였다
            // (2026-09-09). 가로 줄에서는 잘릴 아래가 없어 **같은 크기로 나란히** 둔다.
            if (modulePalette != null && PaletteCategories.ShowsModule(_tab))
            {
                bool moduleAllowed = TutorialGate.Allows(TutorialGate.Control.Module);
                for (int m = 0; m < modulePalette.Count; m++)
                {
                    if (modulePalette[m] == null) continue;
                    var mRect = new Rect(bx, 0f, side, side);
                    bool on = _selectedModule == m;
                    bool wasM = GUI.enabled;
                    GUI.enabled = wasM && moduleAllowed;
                    if (UiSkin.Button(mRect, (on ? "●" : "") + modulePalette[m].displayName, style))
                    {
                        _selectedModule = on ? -1 : m;
                        _elementMode = null;
                    }
                    if (!moduleAllowed) UiSkin.DrawLockVeil(mRect);
                    GUI.enabled = wasM;
                    bx += step;
                }
            }

            // ⚠️ **「제거」 버튼을 걷었다**(2026-09-15 사용자 확정 · 육안 ⑧).
            //
            // 제거는 이제 **모드가 아니라 제스처**다 — 벨트 위에서 끌면 지워진다
            // (`OnPressStart` · `FinishRemovalDrag`). 모드 버튼이 있으면 같은 일을 하는
            // 길이 둘이 되고, 그 버튼은 팔레트 **맨 끝**이라 손이 안 닿기까지 했다
            // (09-15 육안 「벨트 제거가 안 된다」의 실제 원인).

            GUI.EndScrollView();
            GUI.enabled = true;

            // ⚠️ **구 규칙 문구를 걷었다**(2026-09-15 · 제거 규칙 개정 §73-15 ①).
            //
            // 「탭 = 노드 배치 · 드래그 = 벨트 · 벨트 위에서 끌면 제거」는 이제 틀렸다 —
            // 제거는 **노드·벨트를 안 가리고**, **탭은 제거가 아니다.**
            //
            // ⚠️ **자리도 틀렸다.** `band.y − 30` 은 **탭 줄과 겹치는 자리**였다(개편 ②로
            // 부유 띠 위쪽이 탭 줄이 됐다). 보드 띠 **왼쪽 아래**로 내린다 —
            // 모드 판이 오른쪽 아래를 쓰므로 반대 구석이다. ⚠️ 자리는 가정이다.
            // ⚠️⚠️ **자리를 `UiLayout` 으로 뺐다**(2026-09-21 사용자 육안 ⓐ — 「안내 줄이
            // 절반 가려진다」). 여기서 재던 `boardBand.yMax − 24 − 44` 는 **모드 막대
            // 속**이었다(막대 1932~2082 · 안내 줄 2030~2074). 자리가 코드 안에 흩어져
            // 있으면 **겹치는지를 눈으로만** 볼 수 있다 — 순수 함수로 빼면 시험이 본다
            // (`UiLayoutHintRectTests`).
            GUI.Label(UiLayout.BoardHintRect(Screen.width, Screen.height),
                _selectedModule >= 0 ? "모듈 — 놓인 노드를 탭하면 붙는다"
                : "빈 칸 = 벨트 · 노드 탭 = 조합표 · 놓인 것 위에서 끌면 제거",
                new GUIStyle(GUI.skin.label)
                {
                    // ⚠️ **스냅을 여기서 다시 하지 않는다** — 상자 높이가 이 크기를 보고
                    //    정해지므로 둘이 같은 곳에서 나와야 한다(육안 3차 ②).
                    fontSize = UiLayout.BoardHintFontSize(Screen.height),
                    normal = { textColor = NameTextColor },
                });

            DrawRecipePanel();

            // ⚠️ **모드 막대를 걷었다**(2026-09-15 · 하단 개편 ④). 자리는 보드 우측 하단
            // 판으로 옮겼고(`DrawModePlate`), 그 판은 팔레트보다 **먼저** 그린다 —
            // 보드 안이라 팔레트와 자리를 다투지 않으므로 맨 뒤일 이유가 없다.

            // ⚠️ **삭제 컨펌은 그보다 더 뒤다**(2026-09-15 · 육안 ⑧) — 물어보는 동안에는
            // 그 물음이 화면에서 가장 위여야 한다. 모드 막대까지 덮는다.
            DrawRemovalConfirm();
        }

        /// <summary>
        /// 부유 띠 그릇 — **띠 안의 어떤 것보다 먼저** (2026-09-11 재육안 2 ①).
        ///
        /// 판을 나중에 깔면 그 위에 그려야 할 것을 덮는다. 종전에 **모드 버튼이 화면에서
        /// 사라진** 자리이며, 좁은 창일수록 티가 났다(615×1085 에서 모드 칸 지름이 75px 이라
        /// 가려진 뒤에는 흔적도 안 남았다).
        /// </summary>
        private void DrawFloatBandPlate()
        {
            Rect band = UiLayout.BandRect(UiLayout.Band.FloatBand, Screen.width, Screen.height);
            UiBlockers.Add(band);
            UiSkin.DrawPlate(band);   // 9-슬라이스 — 통짜로 늘이면 모서리가 뭉개진다(육안 4차 ④)
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

            // 칸 하나가 화면에서 몇 픽셀인가 — 이름 판과 품목 아이콘이 이것을 따라간다.
            float cellPx = CellScreenPixels(cam);

            foreach (KeyValuePair<Vector2Int, GameObject> kv in _markers)
            {
                if (kv.Value == null) continue;
                // ⚠️ **이름 라벨을 되살렸다**(2026-09-15 사용자 확정 · 하단 개편 ⑥).
                //
                // 아침에 걷었던 것이다(§72-51 — 「그림이 이미 종류를 말한다」). 육안에서
                // **그림만으로는 열둘이 안 갈렸다** — 타일 아트가 같은 계열끼리 비슷하다.
                //
                // 되살리되 **종전과 다르게** 놓는다 —
                // · 타일 **위쪽**에 어두운 판을 깔고 그 위에 미색 글자(종전은 타일 한가운데
                //   검정 글자라 그림 위에 겹쳤다)
                // · 크기가 **배율을 따라간다**(종전은 17px 날 픽셀이라 줌을 넣으면 점이 됐다)
                // · 구역 이름표와 **같은 스냅**을 쓴다 — 안 쓰면 줌마다 새 글리프를 구워
                //   아틀라스가 차고 글자가 깨진다(09-15 「판1」의 그 병).
                //
                // ⚠️ **군수는 「군수」만 적는다**(사용자 확정). 종전 「군수:관통」은 탄종까지
                // 적었는데, 그것은 이제 **출력 품목 아이콘**이 말한다(개편 ⑦).
                NodeInstance nodeHere = _grid.GetAt(kv.Key);

                // ⚠️ **아이콘을 먼저, 이름판을 나중에**(2026-09-15 · 육안 ②).
                //
                // 코어는 **출력이 네 면**이라 북면 아이콘이 이름판 자리에 겹쳤다 —
                // 화면에서는 「코어」가 아이콘에 가려 안 읽혔다. IMGUI 는 뒤에 그리는 쪽이
                // 위이므로 **이름이 이긴다.** 무엇을 내는가는 나머지 세 면이 말한다.
                DrawOutputItemIcon(cam, kv.Value.transform.position, nodeHere, cellPx);
                DrawNodeNamePlate(cam, kv.Value.transform.position, NodeLabel(nodeHere), cellPx);

                // 일감률 0 = 이 노드는 지금 아무것도 안 하고 있다(260831_V07 표시 규칙).
                // 초과분을 몰아서 0으로 두었으므로 **뺄 노드가 그대로 지목된다** —
                // 0.71씩 골고루 나눠 줬다면 여기에 쓸 말이 없다.
                if (IsIdle(kv.Key))
                    DrawLabelAt(cam, kv.Value.transform.position, "노는 중",
                        idleStyle, IdleLabelColor, 118f, 20f);

                // ⚠️ **모듈 글자 자리표시를 걷었다**(2026-09-15 사용자 확정 · §72-51).
                //
                // 「그림이 오면 이 줄이 사라진다」고 적어 두었던 자리인데, **그림이 왔다**
                // (`SpawnModuleSymbols` 가 기호 둘을 스프라이트로 얹는다). 글자를 같이
                // 두면 그림 위에 글자가 겹쳐 **둘 다 안 읽힌다.**
            }

            foreach (KeyValuePair<Vector2Int, GameObject> kv in _beltMarkers)
            {
                if (kv.Value == null) continue;
                BeltInstance belt = _grid.GetBeltAt(kv.Key);
                // 병합기·분류기는 **무엇인지가 먼저**다 — 직선 벨트와 형태가 같아 글자로만 갈린다.
                //
                // ⚠️ **흐르는 품목의 한 글자는 안 적는다** (2026-09-10 사용자 확정 · 리허설 1차).
                // 「코」·「전」 같은 글자는 **품목 그림이 없던 시절의 자리표시**였는데, 품목 열둘이
                // 배선되면서 **그림이 흐르는 위에 글자가 겹쳤다.** 무엇이 흐르는지는 그림이 말한다.
                // 🗑️ **「합」·「분」 글자 폐기**(2026-09-18 사용자 리허설 ⑨ — 「병합기·분류기
                //    이미지가 안 보인다」). 타일 그림이 붙어 있는데 **검은 글자가 그 위를 덮고**
                //    있었다 — 09-10 에 품목 한 글자를 걷은 것과 **같은 자리**다:
                //    그림이 붙으면 글자는 자리표시였던 것이다.
                //    📌 무엇인지는 이제 **모양**이 말한다(직선·코너와 다른 그림이다).
                string label = string.Empty;
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

            // ⚠️ **스크롤을 더한다**(2026-09-15 · 육안 ③).
            //
            // `_grid.Origin` 만 쓰고 있었다 — 보드를 끌면 칸은 움직이는데 **구역 이름표만
            // 제자리에 남았다.** 다른 마커는 전부 `CellWorld`(= Origin + PanOffset)를 쓰는데
            // 여기만 빠져 있었다. 화면에서는 「팔」이 잘려 보였다.
            Vector2 o = _grid.Origin + PanOffset;
            float inset = ZoneLabelInsetPx * (config.cellSize / ZonePx);

            // 글자 높이도 **아트 픽셀**이다 (2026-09-06 확정 · `260906_W05` 2-2).
            // 보드 배율이 ×1.00에서 ×2.50까지 변하는데(DrawZoom) 글자만 화면 픽셀로 두면
            // 확대할수록 이름표가 상대적으로 작아져 경계선과 다른 물건으로 보인다.
            // 한 칸이 화면에서 몇 픽셀인지를 재서 곱한다 — 배율이 그 안에 이미 들어 있다.
            float cellScreenPx = Mathf.Abs(
                cam.WorldToScreenPoint(new Vector3(o.x + config.cellSize, o.y, 0f)).x
                - cam.WorldToScreenPoint(new Vector3(o.x, o.y, 0f)).x);
            if (cellScreenPx <= 0f) return;

            float fontScale = cellScreenPx / ZonePx;

            // ⚠️ **스냅을 빠뜨린 자리였다**(2026-09-15 육안 ④ · 09-14 §72-12 1 의 남은 하나).
            //
            // 「팔R」이 「판1」로, 「다리R」이 「다리ㅁ」으로 나왔다. **폰트 폴백이 아니다** —
            // NotoSansKR 에는 R·L·0·1 이 다 있다(cmap 으로 확인). 09-14 에 「마운트」가
            // 「마으ㅌ」로 나온 것과 **같은 병**이다: 유니티 동적 폰트는 **크기마다 글리프를
            // 따로 굽는데**, 여기 `fontScale` 은 `칸 화면 픽셀 / 96` 이라 **줌을 움직일 때마다
            // 새 크기가 생긴다.** 한글은 완성형이라 아틀라스가 금세 차고, 차면 굽지 못한
            // 글리프가 **엉뚱한 글리프로** 찍힌다.
            //
            // 09-14 에 `KoreanFont.Snap` 을 들여놓으면서 **계산값이 들어가는 이 한 곳을
            // 빠뜨렸다**(같은 파일 2478 줄은 쓰고 있었다).
            int fontPx = KoreanFont.Snap(
                Mathf.Max(1, Mathf.RoundToInt(ZoneLabelFontPx * fontScale)));

            // ⚠️ **Bold 를 걷었다**(2026-09-15 · 「다리L」 실측 갈래).
            //
            // 전투 HUD 에는 라틴·숫자가 **찍힌다**(「HP 946/1000」) — 그쪽은 **Regular** 다.
            // 구역 이름표만 Bold 였고 거기서만 라틴이 빠졌다. `NotoSansKR-Regular` 에는
            // **Bold 자형이 없어** 유니티가 굵기를 **합성**하는데, WebGL 에서 그 합성이
            // 라틴에 실패하는 것으로 보인다.
            //
            // ⚠️ **읽기는 안 나빠진다** — 이 글자는 크고(칸의 절반) 어두운 바탕 위 미색이다.
            // 굵기 없이도 구역 이름은 읽힌다.
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontPx,
                alignment = TextAnchor.UpperLeft,
            };

            // ⚠️ **폭을 글자에서 잰다**(2026-09-10 · 촬영 결함 b).
            // 종전에는 `120 * fontScale` 로 박혀 있었는데 **글자 하나가 이미 96 아트픽셀**이라
            // 세 글자 이름표(`팔R`·`다리L`)가 **한 글자에서 잘렸다.** 화면에는 「다」·「로」처럼
            // 거대한 한 글자만 남아, 리허설 1차에서 「보드 글자가 깨진다」로 올라온 그 자리다.
            //
            // **글자 크기 문제가 아니라 상자 문제였다** — 96 은 그대로 두고 상자만 글자에 맞춘다.
            float boxH = fontPx + 6f * fontScale;

            // ⚠️ **마운트에도 이름표를 붙인다**(2026-09-11 사용자 육안 · 플랜 §71-16 ②).
            //
            // 마운트 포트 그림은 진작 배선돼 있었는데(`BuildMountPorts`) **무엇인지 말하는
            // 글자가 없어** 그냥 또 하나의 마커로 보였다. 보드의 산출이 전투로 넘어가는
            // 유일한 자리이므로 「왜 라인이 여기서 끝나야 하는가」가 글자로 서야 한다.
            //
            // ⚠️ **UI 12-4 의 점멸과 한자리에 둔다** — 재고가 0 이면 그림이 빨갛게 깜빡이는데
            // 글자만 미색으로 남으면 **경고가 반쪽**이 된다. 같은 판정을 두 번 하지 않고
            // `UpdateMountPortBlink` 와 같은 규칙을 읽는다.
            DrawMountLabels(cam, o, style, fontScale);

            DrawStatusIcons(cam);

            DrawFaceMismatchLabels(cam, style, fontScale);

            DrawGhostHint(cam, style, fontScale);

            // ══════════ 구역 이름 = 파츠 **한가운데 큰 워터마크** ══════════
            //
            // ⚠️ **왼윗 작은 이름표를 걷었다**(2026-09-15 사용자 확정 · 육안 ⑦ · 파츠 구분 (다)).
            //
            // 작은 이름표는 **모서리에 붙어** 있어서 파츠가 겹쳐 보이는 화면에서 「이 이름이
            // 어느 덩어리 것인가」가 안 섰다 — 「팔R」이 옆 구역 노드 이름판과 겹치기까지 했다.
            // 한가운데 큰 글자는 **덩어리 자체를 물들이므로** 그 물음이 생기지 않는다.
            //
            // ⚠️ **둘 다 두지 않는다.** 같은 말을 두 번 하면 보드가 시끄러워진다.
            //
            // ⚠️ **글리프는 사다리 최대(44)로 굽고 화면에서 키운다.** 사다리를 늘리면
            // 아틀라스 예산을 넘는다(`KoreanFont.Ladder` 주석) — 워터마크는 반투명이라
            // 확대로 흐려지는 것이 문제가 안 된다.
            //
            // ⚠️ **값 둘은 가정이다**(설계 역기입) — 알파 0.16 · 파츠 짧은 변의 0.5.
            var mark = new GUIStyle(GUI.skin.label)
            {
                fontSize = KoreanFont.Ladder[KoreanFont.Ladder.Length - 1],
                alignment = TextAnchor.MiddleCenter,
            };

            foreach (PartRect p in PartLayout.Parts)
            {
                string text = PartLayout.LabelOf(p.part);
                if (string.IsNullOrEmpty(text)) continue;

                // 파츠의 화면 사각 — 네 귀퉁이 중 둘이면 정해진다.
                Vector3 lo = cam.WorldToScreenPoint(new Vector3(
                    o.x + p.origin.x * config.cellSize,
                    o.y + p.origin.y * config.cellSize, 0f));
                Vector3 hi = cam.WorldToScreenPoint(new Vector3(
                    o.x + (p.origin.x + p.size.x) * config.cellSize,
                    o.y + (p.origin.y + p.size.y) * config.cellSize, 0f));
                if (lo.z <= 0f || hi.z <= 0f) continue;

                var area = new Rect(Mathf.Min(lo.x, hi.x), Screen.height - Mathf.Max(lo.y, hi.y),
                    Mathf.Abs(hi.x - lo.x), Mathf.Abs(hi.y - lo.y));
                if (area.width < 8f || area.height < 8f) continue;

                // ⚠️ **인셋 전투 자리에는 안 그린다**(2026-09-10 · 리허설 2차 결함 2).
                //
                // ⚠️ **판정은 글자 한가운데로 한다**(2026-09-15 재실측). 파츠 사각의 아래끝으로
                // 재면 **보드와 인셋에 걸친 파츠**(머리·어깨)가 통과해 버리는데, 워터마크는
                // 한가운데 그려지므로 **글자만 흙 배경 위에 떠 있었다.**
                if (area.center.y < CombatInsetView.BottomPixels(Screen.height)) continue;

                // ⚠️ **0.5 → 0.30**(2026-09-15 재실측 · 가정). 절반으로 잡았더니 몸통 글자가
                // **노드를 덮고 파츠 경계를 넘었다** — 워터마크는 바탕이지 주인공이 아니다.
                float target = Mathf.Min(area.width / Mathf.Max(1, text.Length), area.height) * 0.30f;
                float zoom = Mathf.Max(0.1f, target / mark.fontSize);

                Color prevMark = GUI.color;
                Matrix4x4 prevMatrix = GUI.matrix;
                GUI.color = ZoneWatermarkColor;
                GUIUtility.ScaleAroundPivot(new Vector2(zoom, zoom), area.center);
                GUI.Label(area, text, mark);
                GUI.matrix = prevMatrix;
                GUI.color = prevMark;
            }
        }

        /// <summary>칸 하나가 화면에서 몇 픽셀인가. 배율·창 크기를 따라간다.</summary>
        private float CellScreenPixels(Camera cam)
        {
            if (cam == null || _grid == null) return 0f;
            Vector3 a = cam.WorldToScreenPoint(Vector3.zero);
            Vector3 b = cam.WorldToScreenPoint(new Vector3(_grid.CellSize, 0f, 0f));
            return Mathf.Abs(b.x - a.x);
        }

        /// <summary>
        /// 삭제 컨펌 뒤에 까는 DIM. ⚠️ **알파는 가정**(튜토리얼 어둠막 0.55 와 같은 눈금).
        /// 불투명하면 「팝업」이 아니라 「화면이 덮였다」가 된다(2026-09-15 육안 ②).
        /// </summary>
        /// <summary>
        /// 구역 워터마크 색 — ⚠️ **알파 0.16 은 가정**(2026-09-15 · 육안 ⑦).
        /// 덩어리를 물들이되 **그 위 노드·벨트를 안 가려야** 한다.
        /// </summary>
        private static readonly Color ZoneWatermarkColor = new Color(0.93f, 0.90f, 0.82f, 0.16f);

        private static readonly Color RemovalDimColor = new Color(0.02f, 0.03f, 0.05f, 0.55f);

        /// <summary>노드 이름 판 — 타일 **위쪽** 어두운 판 + 미색 글자. ⚠️ 알파·높이는 가정.</summary>
        private static readonly Color NamePlateColor = new Color(0.05f, 0.07f, 0.10f, 0.78f);
        private static readonly Color NameTextColor = new Color(0.93f, 0.90f, 0.82f);

        /// <summary>
        /// 노드 이름을 타일 **위쪽 띠**에 적는다 (2026-09-15 사용자 확정 · 하단 개편 ⑥).
        ///
        /// ⚠️ **한가운데가 아니라 위쪽이다.** 종전(§72-51 이전)은 타일 한가운데 검정
        /// 글자라 **그림 위에 겹쳤다** — 그래서 09-15 아침에 통째로 걷었던 것이다.
        /// 위쪽 띠로 올리고 판을 깔면 그림과 글자가 자리를 나눠 갖는다.
        ///
        /// ⚠️ **배율을 따라가고 스냅한다** — 구역 이름표와 같은 처리다. 날 픽셀로 두면
        /// 줌을 넣었을 때 점이 되고, 스냅을 빼면 줌마다 새 글리프를 구워 아틀라스가 찬다.
        /// </summary>
        /// <summary>
        /// 이름판이 쓸 수 있는 줄 수 — **둘**.
        ///
        /// ✅ **사용자 확정**(2026-09-16 저녁). ⚠️ 한때 갈렸던 자리다 — 받은 문구가
        /// 「**줄바꿈 최대 두 개**」였고 글자대로 읽으면 **세 줄**이라, 나는 **두 줄**로
        /// 구현해 놓고 「확정」이라고 적었다. 그것은 **해석**이었다.
        /// `260916_V02` 에 그 사실을 적어 물었고 **두 줄로 확정** 받았다 —
        /// 지금 이 줄은 확정을 옮긴 것이다.
        ///
        /// 📌 **해석을 확정으로 적지 않는다** — 그때는 물어야 한다.
        /// </summary>
        private const int MaxNamePlateLines = 2;

        private static void DrawNodeNamePlate(Camera cam, Vector3 world, string text, float cellPx)
        {
            if (string.IsNullOrEmpty(text) || cellPx < 24f) return;   // 너무 작으면 못 읽는다

            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) return;

            // ⚠️⚠️ **높이만 보고 크기를 정하면 이름이 칸 밖으로 넘친다**
            //    (2026-09-16 사용자 육안 — 「노드 라벨이 그리드를 넘어간다」).
            //
            // 종전 글자 크기는 `h * 0.76` 하나로 정해졌다 — **폭을 아예 안 봤다.**
            // 「복합 가공소」처럼 긴 이름은 한 칸 폭(0.92칸)을 넘어 **이웃 칸 위로** 삐져나왔다.
            // 오늘만 네 번째 같은 병이다(탭 줄 · 팔레트 · 원형 버튼 · 여기) —
            // **크기는 두 변에서 잡아야 한다.**
            float w = cellPx * 0.92f;
            float x = sp.x - w * 0.5f;

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                // Bold 없음 — 구역 이름표와 같은 이유(합성 굵기가 라틴을 놓친다).
                wordWrap = true,
            };

            // ⚠️⚠️ **끊을 자리를 우리가 준다**(2026-09-19 사용자 육안 ⑤ · 스크린샷 4).
            //
            // 두 줄에 드는 크기는 아래 사다리가 이미 골랐다 — **어디서 끊는지**가 문제였다.
            // IMGUI 의 `wordWrap` 은 한글을 **아무 데서나** 끊으므로 「기초 가공소」가
            // **「기초 가공 / 소」**로 나왔다(둘째 줄에 「소」 한 자). 뜻이 없는 자리다.
            // 띄어쓰기에서 끊으면 「기초 / 가공소」가 된다 — **글자는 그대로**고 자리만 옮긴다.
            //
            // ⚠️ **한 줄에 들면 안 끊는다** — 아래에서 한 번 더 본다. 여기서 못 박아 버리면
            //    줌을 넣어 넉넉해진 판에서도 늘 두 줄로 찍힌다(고치려던 것과 같은 병이다).
            string wrapped = LabelWrapRule.AtSpace(text);

            // 사다리를 큰 쪽부터 내려오며 **두 줄 안에 드는 첫 크기**를 고른다.
            // ✅ **최대 두 줄**(2026-09-16 사용자 확정) — 세 줄이면 타일이 글자로 덮인다.
            float lineH = cellPx * 0.26f;
            float maxH = lineH * MaxNamePlateLines;
            float h = lineH;

            // ⚠️ **사다리를 큰 쪽부터 내려온다** — 작은 쪽부터 올라가면 **맨 처음 드는
            //    가장 작은 크기**를 골라 늘 깨알같이 찍힌다. 우리가 원하는 것은
            //    **두 줄 안에 드는 가장 큰 크기**다.
            //    ⚠️ 사다리 밖 크기는 안 쓴다 — 동적 폰트 아틀라스가 크기마다 굽는다
            //    (`KoreanFont.Snap` 의 까닭 · 09-15 촬영 차단 결함).
            style.fontSize = KoreanFont.Ladder[0];
            for (int i = KoreanFont.Ladder.Length - 1; i >= 0; i--)
            {
                style.fontSize = KoreanFont.Ladder[i];
                float need = style.CalcHeight(new GUIContent(wrapped), w - 4f);
                if (need <= maxH) { h = Mathf.Max(lineH, need); break; }
                // 맨 아래까지 안 들면 **자르지 않고 두 줄 높이로 둔다** —
                // `GUILayout` 과 달리 `GUI.Label` 은 넘쳐도 안 지우고 그린다.
                if (i == 0) h = maxH;
            }

            // 고른 크기에서 **원래 글자가 한 줄에 들면 그것을 쓴다.** 끊어 둔 것은
            // 넘칠 때의 대비지, 두 줄로 만들려던 것이 아니다.
            if (style.CalcHeight(new GUIContent(text), w - 4f) <= lineH * 1.05f)
            {
                wrapped = text;
                h = lineH;
            }

            float y = Screen.height - sp.y - cellPx * 0.5f + cellPx * 0.04f;   // 타일 위쪽 안쪽
            if (x + w < 0f || x > Screen.width || y + h < 0f || y > Screen.height) return;

            var box = new Rect(x, y, w, h);

            // ⚠️⚠️ **인셋 경계에 걸치면 통째로 안 그린다**(2026-09-21 사용자 육안 ⓒ —
            //    「인셋 아래 경계에 이름판이 반쯤 가려진다」).
            //
            // 그릇(`BeginClip(boardView)`)이 이미 보드 띠 밖을 자르고 있고, 보드 띠 윗변은
            // **인셋 밑변과 같은 줄**이다(768 × h/2560 = 0.3h = `BottomPixels`). 그래서
            // 경계에 걸친 판은 **막히는 것이 아니라 반으로 잘렸다** — 「변환기」가 윗동강
            // 없이 인셋 밑변에 붙어 떠 있었다.
            //
            // ⚠️ **판정은 윗변으로 한다** — 구역 이름표는 한가운데로 재는데(`area.center.y`)
            // 그쪽은 사각이 커서 한 귀퉁이가 걸렸다고 통째로 숨기면 멀쩡한 이름이 사라진다.
            // 이름판은 **한 줄 높이**라 윗변이 걸리는 순간 이미 못 읽는다 — 같은 규칙을
            // 쓰면 반 토막이 그대로 남는다.
            if (box.y < CombatInsetView.BottomPixels(Screen.height)) return;

            Color prev = GUI.color;
            GUI.color = NamePlateColor;
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = NameTextColor;
            GUI.Label(box, wrapped, style);
            GUI.color = prev;
        }

        /// <summary>
        /// **지금 내는 품목** 아이콘을 출력 면 커넥터 옆에 (2026-09-15 사용자 확정 · 개편 ⑦).
        ///
        /// ⚠️ **이름 글자가 못 하는 말을 한다.** 이름은 이제 「군수」까지만 적으므로
        /// (개편 ⑥), **무엇을 내는 군수인가**는 이 그림이 유일한 답이다.
        ///
        /// ⚠️ **레시피가 바뀌면 따라 바뀐다** — 매 프레임 `CurrentRecipe` 에서 읽는다.
        /// 한 번 구워 두면 조합표를 바꿔도 옛 그림이 남는다.
        ///
        /// ⚠️ **출력이 없는 노드에는 안 뜬다** — 저장·부스터가 그렇다. 빈 그림으로
        /// 자리를 채우지 않는다.
        /// </summary>
        private void DrawOutputItemIcon(Camera cam, Vector3 world, NodeInstance inst, float cellPx)
        {
            if (inst == null || inst.Definition == null || cellPx < 24f) return;

            FlowKind kind = inst.CurrentRecipe.output;
            if (kind == FlowKind.None) return;

            Sprite sp = art != null ? art.ItemSprite(kind) : null;
            if (sp == null || sp.texture == null) return;   // 그림이 없으면 안 그린다(§10)

            // ⚠️⚠️ **칸 가운데에 둔다**(2026-09-16 사용자 육안 · 면 화살표와 겹쳤다).
            //
            // 🗑️ 구 자리는 **출력 면 쪽 0.30칸**이었다. 09-16 에 면 화살표가 같은 면에
            // 서면서 **둘이 겹쳐 둘 다 안 읽혔다** — 아이콘은 「무엇을 내는가」, 화살표는
            // 「어느 쪽으로 내는가」라 **다른 말인데 한 자리를 다퉜다.**
            //
            // 📌 **가운데면 면과 안 다툰다.** 어느 쪽으로 나가는지는 화살표와 포트 탭이
            // 이미 말하므로, 아이콘까지 면에 붙을 이유가 없다. 회전해도 자리가 안 변한다.
            Vector3 c = cam.WorldToScreenPoint(world);
            if (c.z <= 0f) return;

            float d = cellPx * 0.30f;
            float cx = c.x;
            float cy = Screen.height - c.y;

            // ⚠️⚠️ **여기가 「주황 정사각」의 진짜 자리였다**(2026-09-15 · 육안 5차 ③).
            //
            // 09-15 에 벨트 위 품목의 크기를 고치면서 **그림을 그리는 경로가 둘**이라는 것을
            // 놓쳤다. 벨트 위는 `SpriteRenderer` 로 그리고, **노드 출력 아이콘인 여기는
            // `GUI.DrawTextureWithTexCoords`** 로 그린다. 앞쪽만 고쳐 놓고 「고쳤다」고 적었다.
            //
            // 이 자리가 나빴던 까닭은 둘이다 —
            // ① **캔버스를 통째로 썼다.** `ammo_standard` 는 64 안에 40×20 만 그려져 있어서
            //    아이콘 상자의 대부분이 **빈 여백**이고 탄피는 가운데 몇 화소로 쪼그라들었다.
            // ② **정사각 상자에 밀어 넣었다.** 2:1 그림을 1:1 로 늘여 **탄피가 뭉개졌다.**
            // 둘이 겹쳐서 화면에는 **주황색 덩어리 하나**로 보였다.
            //
            // 이제 **그려진 네모만 잘라** 쓰고, 상자도 **그 비율대로** 잡는다.
            Rect uv = art.ItemContentRect(kind);
            float aspect = uv.height > 0.0001f ? uv.width / uv.height : 1f;
            float w = aspect >= 1f ? d : d * aspect;
            float h = aspect >= 1f ? d / aspect : d;
            GUI.DrawTextureWithTexCoords(new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h),
                sp.texture, uv);
        }

        /// <summary>
        /// 그 노드가 지금 내는 면. 없으면 동면(자산 기본)으로 둔다.
        ///
        /// 🗑️ **부르는 곳이 없어졌다**(2026-09-16 · 출력 아이콘이 칸 가운데로 갔다).
        /// 지우지 않고 남기는 것은 「출력 면이 어디인가」가 **다시 필요해질 물음**이기
        /// 때문이다 — 지웠다가 다시 짓느니 폐기 표기로 둔다.
        /// </summary>
        private static PortFace OutputFaceOf(NodeInstance inst)
        {
            IReadOnlyList<NodePort> ports = inst.Ports();
            for (int i = 0; i < ports.Count; i++)
                if (ports[i].io == PortIO.Output) return ports[i].face;
            return PortFace.East;
        }

        private static Vector2 FaceDirection(PortFace face)
        {
            switch (face)
            {
                case PortFace.North: return Vector2.up;
                case PortFace.South: return Vector2.down;
                case PortFace.West: return Vector2.left;
                default: return Vector2.right;
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

            // ⚠️ **조합표가 없어도 뜬다**(2026-09-15 사용자 확정 · 육안 ②).
            //
            // 종전에는 후보가 없으면 여기서 돌아섰다 — 코어·에너지·저장·부스터를 탭하면
            // **아무 일도 안 일어났다.** 이제 이 패널이 조합표만 고르는 곳이 아니라
            // **그 노드에 대해 아는 것을 말하는 곳**이라, 고를 것이 없어도 할 말이 있다.
            List<NodeRecipe> candidates = inst.Definition.recipes;
            int candidateCount = candidates != null ? candidates.Count : 0;

            Camera cam = boardCamera != null ? boardCamera : Camera.main;
            if (cam == null) return;

            // ⚠️ **날 픽셀 자리를 걷었다**(2026-09-15 사용자 확정 · §72-51).
            //
            // 종전은 `x 12 · y 380` 고정이라 고른 노드가 보드 어디에 있든 패널이 늘 화면
            // 왼쪽에 떴다 — **무엇을 고치는 중인지가 눈에서 멀었다.** 오늘 하단 넷이 전부
            // 「날 픽셀 자리가 문서 좌표 위에 남아 있었다」였고, 이것이 그 다섯째다.
            //
            // ⚠️ **배율·스크롤을 공짜로 따라간다** — 칸의 **월드 좌표**를 매 프레임 화면으로
            // 환산해 자리를 잡는다. 화면 좌표를 한 번 재어 들고 있으면 스크롤에서 떨어진다.
            Vector3 cw = CellWorld(_selected.Value);
            Vector3 sp = cam.WorldToScreenPoint(cw);
            if (sp.z <= 0f) return;                                  // 카메라 뒤 — 안 그린다
            var nodePos = new Vector2(sp.x, Screen.height - sp.y);   // GUI 는 y 가 뒤집혀 있다

            float cellPx = Mathf.Abs(
                cam.WorldToScreenPoint(cw + new Vector3(_grid.CellSize, 0f, 0f)).x - sp.x);

            float uiS = UiLayout.Scale(Screen.height);
            float w = UiLayout.RecipePopoverWidth * uiS;
            float h = UiLayout.RecipePopoverRow * uiS;
            float pad = UiLayout.RecipePopoverGap * uiS * 0.25f;
            float inner = UiLayout.RecipePopoverGap * uiS * 0.5f;

            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(10, Mathf.RoundToInt(h * 0.34f))),
            };
            var head = new GUIStyle(GUI.skin.label)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(10, Mathf.RoundToInt(h * 0.30f))),
                fontStyle = FontStyle.Bold,
            };

            var body = new GUIStyle(GUI.skin.label)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(10, Mathf.RoundToInt(h * 0.26f))),
                wordWrap = false,
            };

            // ⚠️ **높이를 줄 수에서 잰다** — 고정값을 박으면 조합표가 넷인 노드에서 잘린다
            // (오늘 HUD 280 이 그 자리였다).
            bool hasAmmo = inst.CurrentRecipe.kind == RecipeKind.Ammo;
            float row = h * 0.62f;                       // 읽기 전용 한 줄
            int moduleRows = inst.ModuleCount;
            float total = h                              // 회전 + 이름
                          + row * 2f                     // ① 상태 · 가동률
                          + row * 2f                     // ② 입력 → 출력
                          + row * 2f                     // ③ 전력 · 생산 시간
                          + (moduleRows > 0 ? row + (h * 0.8f) * moduleRows : 0f)   // 모듈 행
                          + (candidateCount > 0 ? row + (h + pad) * candidateCount : 0f)
                          + inner * 2f
                          + (hasAmmo ? h * 0.8f + h : 0f);

            Rect box = UiLayout.RecipePopoverRect(nodePos, cellPx, total,
                Screen.width, Screen.height);
            UiBlockers.Add(box);
            UiPlate.Draw(box);

            float x = box.x + inner;
            float y = box.y + inner;
            w -= inner * 2f;

            // ── 회전 (2026-09-15 사용자 확정 · §72-42) ──────────────────────────
            //
            // ⚠️ **왜 여기인가.** 노드를 고른 상태에서만 뜨는 패널이고, 조합표와 회전은
            // 둘 다 「고른 노드를 손본다」라 같은 자리에 있어야 한다.
            //
            // ⚠️ **누르면 배선을 다시 잡는다** — 면이 돌면 링크·품목·색이 전부 바뀐다.
            // 마커도 다시 짓는다(포트 탭이 면을 따라 붙어 있다).
            var rotRect = new Rect(x, y, w * 0.32f, h);
            // ⚠️ **화살표 기호를 걷었다**(2026-09-15 육안 · 글리프). 「↻」(U+21BB)가
            // 폰트에 없어 **두부(□)로 찍혔다** — 한글 폰트에 없는 기호는 안 쓴다
            // (09-02 에 「⚡」·「🔥」가 같은 이유로 걷혔다 · `VariablePanel` 주석).
            if (UiSkin.Button(rotRect, "90도 돌리기", style))
            {
                inst.Rotation = inst.Rotation + 1;
                RebuildMarker(_selected.Value);
                RefreshConnections();
            }

            GUI.Label(new Rect(x + w * 0.36f, y, w * 0.64f, h),
                $"{inst.Definition.displayName} · {inst.Rotation * 90}°", head);
            y += h + pad;

            // ══════════ ① 상태 · 가동률 (2026-09-15 사용자 확정 · 육안 ②) ══════════
            //
            // ⚠️ **왜 이 절이 먼저인가.** 「왜 안 도나」가 이 패널을 여는 가장 흔한 이유다.
            // 상태와 가동률이 맨 위에 없으면 조합표부터 의심하게 되는데, 실제 원인은
            // 대개 **전력이거나 안 이어진 것**이다(표식 둘이 그것을 가리킨다).
            //
            // ⚠️ **값을 여기서 만들지 않는다** — 진단은 물류가 이미 냈다(`_lastDiagnostics`),
            // 가동률은 `WorkloadRate` 가 냈다. 여기는 옮겨 적기만 한다(§3).
            float workload = LogisticsOutputBridge.Workload.Of(_selected.Value);
            GUI.Label(new Rect(x, y, w, row), "상태", head);
            y += row;
            GUI.Label(new Rect(x, y, w, row),
                $"{NodeStateText(_selected.Value)}  ·  가동률 {Mathf.Clamp01(workload) * 100f:F0}%", body);
            y += row;

            // ══════════ ② 입력 → 출력 ══════════
            //
            // ⚠️ **개/초까지 적는다.** 품목만 적으면 「무엇이 드나드는가」는 알아도
            // 「얼마나」를 모른다 — 줄을 설계할 때 필요한 것은 그 수다.
            NodeRecipe cur = inst.CurrentRecipe;
            GUI.Label(new Rect(x, y, w, row), "물품", head);
            y += row;
            DrawRecipeFlow(new Rect(x, y, w, row), inst, cur, body);
            y += row;

            // ══════════ ③ 전력 · 생산 시간 ══════════
            //
            // ⚠️ **전력은 대당 × 일감률이다.** 노는 노드는 전력을 안 먹는다(변수 패널의
            // 「노는 노드는 전력 0」과 같은 규칙) — 대당만 적으면 합이 화면과 안 맞는다.
            //
            // ⚠️ **생산 시간 = 필요 생산치 ÷ 생산력.** 생산력은 밸런스 SO 값(10)이고
            // 여기서 짓지 않는다. 필요 생산치가 0 이면 **미확정**이라 시간도 안 적는다.
            float powerEach = inst.Definition.resources.powerDraw * inst.ModulePowerLoadMultiplier;
            GUI.Label(new Rect(x, y, w, row), "전력 · 시간", head);
            y += row;
            GUI.Label(new Rect(x, y, w, row),
                $"전력 {powerEach * Mathf.Clamp01(workload):F1} (대당 {powerEach:F1})"
                + "  ·  " + ProductionTimeText(inst, cur), body);
            y += row;

            // ══════════ 붙은 모듈 ══════════
            //
            // ⚠️ **떼기가 여기 있어야 한다.** 붙이는 것은 팔레트에서 하는데 떼는 길이
            // 없었다 — 잘못 붙이면 **노드를 통째로 지우는 것 말고 방법이 없었다.**
            if (moduleRows > 0)
            {
                GUI.Label(new Rect(x, y, w, row), "붙은 모듈", head);
                y += row;

                float mh = h * 0.8f;
                for (int slot = 0; slot < NodeInstance.ModuleSlots; slot++)
                {
                    ModuleDefinition mod = inst.ModuleAt(slot);
                    if (mod == null) continue;

                    GUI.Label(new Rect(x, y, w * 0.74f, mh),
                        $"{mod.symbol} {mod.displayName}  ×{mod.outputMultiplier:F2}"
                        + $"  부하 ×{mod.powerLoadMultiplier:F1}", body);

                    if (UiSkin.Button(new Rect(x + w * 0.76f, y, w * 0.24f, mh), "떼기", style)
                        && inst.DetachModuleAt(slot))
                    {
                        RebuildMarker(_selected.Value);   // 기호 스프라이트가 노드에 얹혀 있다
                        RefreshConnections();             // 배수가 빠지면 하류 산출이 바뀐다
                    }
                    y += mh;
                }
            }

            // ══════════ ④ 조합표 후보 ══════════
            if (candidateCount == 0) return;

            GUI.Label(new Rect(x, y, w, row), "조합표", head);
            y += row;

            RecipeKind current = cur.kind;
            foreach (NodeRecipe r in candidates)
            {
                var rect = new Rect(x, y, w, h);

                // 돌릴 수 없는 후보도 **자리는 보여 준다** — 감추면 「왜 못 만드나」가 아니라
                // 「그런 게 있었나」가 된다. 착수 금지가 화면에서도 자리로 표현된다.
                GUI.enabled = r.IsRunnable;
                if (UiSkin.Button(rect, (r.kind == current ? "● " : "") + r.displayName, style)
                    && inst.SelectRecipe(r.kind))
                    RefreshConnections(); // 산출이 바뀌면 하류 벨트가 나르는 것도 바뀐다

                y += h + pad;
            }
            GUI.enabled = true;

            if (current != RecipeKind.Ammo) return;

            // 탄종 — 조합표만으로는 부족하다. 라인 생산량이 min(스펙, 탄종별 노드 수)이라
            // 「무엇을 몇 대 놓았는가」가 그대로 출력이 된다.
            GUI.Label(new Rect(x, y, w, h * 0.8f), "탄종", head);
            y += h * 0.8f;

            float bw = (w - pad * 2f) / 3f;
            for (int k = 0; k < 3; k++)
            {
                var kind = (AmmoKind)k;
                var rect = new Rect(x + k * (bw + pad), y, bw, h);

                if (UiSkin.Button(rect, (inst.AmmoKind == kind ? "● " : "") + AmmoLabel(kind), style))
                    inst.AmmoKind = kind; // 탄종은 흐르는 품목(탄약)을 바꾸지 않는다 — 라벨만 갈린다
            }
        }

        /// <summary>
        /// 그 칸 노드의 상태 한 마디 (2026-09-15 · 육안 ② 팝오버 ① 절).
        ///
        /// ⚠️ **판정은 물류가 이미 했다** — 여기는 진단을 문구로 옮길 뿐이다(§3).
        /// 문서가 정한 원인은 둘이다: 전력 부족 · 미연결. 그 둘이 아니면 도는 정도로만 말한다.
        /// </summary>
        private string NodeStateText(Vector2Int cell)
        {
            if (_lastDiagnostics != null)
                foreach (NodeDiagnostic d in _lastDiagnostics)
                {
                    if (d.cell != cell) continue;
                    if (d.cause == ConstraintCause.Power) return "전력 부족";

                    float ratio = d.targetRate > 0f ? d.actualRate / d.targetRate : 1f;
                    if (ratio <= 0.001f) return "멈춤";
                    if (ratio < 0.999f) return $"느림 ({ratio * 100f:F0}%)";
                    return "도는 중";
                }

            // 진단에 없는 칸 — 코어처럼 일감 계산 밖인 노드다. 없는 것을 지어내지 않는다.
            return "—";
        }

        /// <summary>
        /// 「무엇을 몇 개 먹어 무엇을 몇 개 내는가」 — **아이콘과 수로** 그린다
        /// (2026-09-15 사용자 확정 · 육안 ② 팝오버 ② 절).
        ///
        /// ⚠️ **글자가 아니라 그림이다.** 품목 이름표(`FlowLabel`)는 09-10 에 걷혔다 —
        /// 품목 그림 열둘이 배선되면서 자리표시가 끝났고, 대응표를 두 곳에 두지 않기로 했다.
        /// 여기서 이름을 다시 지으면 그 표가 되살아난다.
        ///
        /// ⚠️ **모듈 배수를 반영한다** — 붙은 모듈이 산출·투입을 곱하므로, 스펙만 적으면
        /// 모듈을 붙인 노드에서 화면과 실제가 갈린다.
        ///
        /// ⚠️ **그림이 없으면 그 자리를 비운다** — 자리표시 글자로 메우지 않는다(§10).
        /// </summary>
        private void DrawRecipeFlow(Rect rect, NodeInstance inst, NodeRecipe r, GUIStyle body)
        {
            float outRate = r.outputPerSec * inst.ModuleOutputMultiplier;
            float icon = rect.height;
            float cx = rect.x;

            if (r.inputs == null || r.inputs.Count == 0)
            {
                GUI.Label(new Rect(cx, rect.y, icon * 2.4f, rect.height), "원천", body);
                cx += icon * 2.4f;
            }
            else
            {
                for (int i = 0; i < r.inputs.Count; i++)
                {
                    RecipeInput inp = r.inputs[i];
                    if (i > 0)
                    {
                        GUI.Label(new Rect(cx, rect.y, icon * 0.7f, rect.height), "+", body);
                        cx += icon * 0.7f;
                    }
                    cx = DrawItemChip(cx, rect.y, icon, inp.kind,
                        inp.perOutput * outRate * inst.ModuleInputMultiplier, body);
                }
            }

            GUI.Label(new Rect(cx, rect.y, icon * 1.2f, rect.height), "→", body);
            cx += icon * 1.2f;

            if (r.output == FlowKind.None || outRate <= 0f)
                GUI.Label(new Rect(cx, rect.y, icon * 3f, rect.height), "산출 없음", body);
            else
                DrawItemChip(cx, rect.y, icon, r.output, outRate, body);
        }

        /// <summary>품목 그림 한 장 + 개/초. 다음 칸의 x 를 돌려준다.</summary>
        private float DrawItemChip(float x, float y, float icon, FlowKind kind, float rate,
            GUIStyle body)
        {
            Sprite sp = art != null ? art.ItemSprite(kind) : null;
            if (sp != null && sp.texture != null)
            {
                // 위 `DrawOutputItemIcon` 과 **같은 이유로** 그려진 네모만 잘라 쓴다 —
                // 조합표 칩도 캔버스를 통째로 늘이고 있었다(육안 5차 ③).
                Rect uv = art.ItemContentRect(kind);
                float aspect = uv.height > 0.0001f ? uv.width / uv.height : 1f;
                float w = aspect >= 1f ? icon : icon * aspect;
                float h = aspect >= 1f ? icon / aspect : icon;
                GUI.DrawTextureWithTexCoords(
                    new Rect(x + (icon - w) * 0.5f, y + (icon - h) * 0.5f, w, h), sp.texture, uv);
            }
            x += icon + 2f;

            var label = new GUIContent($"{rate:F2}/초");
            float lw = body.CalcSize(label).x;
            GUI.Label(new Rect(x, y, lw, icon), label, body);
            return x + lw + 6f;
        }

        /// <summary>
        /// 산출 하나에 걸리는 시간 (2026-09-15 · 육안 ② 팝오버 ③ 절).
        ///
        /// **필요 생산치 ÷ 노드 생산력**이다. 생산력은 밸런스 SO 값이고 여기서 짓지 않는다 —
        /// 자산이 없거나 필요 생산치가 0(미확정)이면 **안 적는다.**
        /// </summary>
        private static string ProductionTimeText(NodeInstance inst, NodeRecipe r)
        {
            BalanceConfig bal = inst.Definition != null ? inst.Definition.balanceRef : null;
            if (bal == null || r.requiredProduction <= 0f || bal.nodeProductionPower <= 0f)
                return "생산 시간 —";

            float seconds = r.requiredProduction / bal.nodeProductionPower;
            return $"생산 {seconds:F1}초/개";
        }

        private static string AmmoLabel(AmmoKind kind)
        {
            switch (kind)
            {
                case AmmoKind.Pierce: return "관통";
                case AmmoKind.Standard: return "표준";
                default: return "폭발";
            }
        }

        /// <summary>
        /// 병목 힌트 한 줄(260831_V02 §3 확정). **무엇을 하면 되는지**만 쓰고 정답은 말하지 않는다.
        ///
        /// 자리는 상단 중앙이다 — 좌상단은 전투 HUD, 우상단은 변수 패널이 이미 쓴다.
        /// 막힌 곳이 없으면 아무것도 그리지 않는다: 늘 떠 있는 줄은 읽히지 않는다.
        /// </summary>
        /// <summary>
        /// **상단 경고 띠** — 0차 표시 (UI 문서 12-2 · `260909_W01` 3-1).
        ///
        /// 자리와 참·거짓은 <see cref="SupplyStopRules"/>가 낸다. 여기는 픽셀만 그린다.
        ///
        /// **경고가 있을 때만 뜨고 없으면 자리를 안 먹는다** — 그래서 보드 뷰포트를 줄이지
        /// 않았다. 줄이면 경고가 없는 대부분의 시간에 빈 띠가 남는다(12-2).
        ///
        /// ⚠️ **화면 좌표다.** 보드를 스크롤해도 따라가지 않으므로 <c>UiBlockers</c>에 넣어
        /// 밑의 보드가 클릭을 먹지 않게 한다 — 안 넣으면 띠 뒤의 칸이 눌린다.
        ///
        /// 색은 **빨강**이다 — 조립 층의 색 축이며 「못 쓴다」를 뜻한다(12-6).
        /// 전투 화면의 주황과 다른 것은 **보드에 적이 없어 붉은색 자리가 비어 있기** 때문이다.
        /// </summary>
        private void DrawSupplyWarningBand()
        {
            // ⚠️⚠️ **자리가 통째로 옮겨 갔다**(2026-09-18 사용자 육안 · 시안 4 ②).
            //
            // 🗑️ **폐기 — 상단 띠**(768 부터 · 같은 날 오전 96 → 240 으로 넓혔던 그것).
            //    **인셋 바로 아래는 보드의 자리**인데 띠가 거기 앉아 **노드 이름판 위에
            //    「고철 6,406」이 겹쳐** 떴다(사용자 스크린샷 2). 자리를 넓힌 것이
            //    겹침을 키웠다 — 넓힐 자리가 아니라 **옮길 자리**였다.
            //
            // 📌 지금은 **하단 큰 버튼(「▲ 전투로」) 바로 위**다. 전투 화면도 **같은 자리**에
            //    같은 것을 보이므로(「▼ 조립」 위), 두 화면을 오가도 눈이 같은 데를 본다.
            //
            // ⚠️ **그리는 것은 `StatusStrip` 하나다** — 전투 화면과 이 화면이 같은 함수를
            //    부른다. 각각 그리면 같은 일을 하는 자리가 둘이 된다.
            Rect exit = UiLayout.ExitBoardRect(Screen.width, Screen.height);
            StatusStrip.Draw(StatusStrip.RectAbove(exit, Screen.width, Screen.height));
        }

        /// <summary>
        /// 팔레트 버튼 왼쪽에 그 노드의 **그림**을 얹는다 (2026-09-10 사용자 확정 · 리허설 1차).
        ///
        /// **글자를 그림으로 바꾸는 것이 아니라 글자 옆에 그림을 더한다** — 보드 위 노드가
        /// 무엇인지 이름표로는 안 읽히는데(구역 이름표는 잘리고 노드 그림에는 글자가 없다),
        /// **팔레트가 「이 그림 = 이 이름」을 한 줄에 보여 주면 보드에서 그림만 봐도 읽힌다.**
        ///
        /// ✅ **자산이 필요 없다** — `BoardArtSet`이 이미 든 노드 스프라이트를 그대로 쓴다.
        ///
        /// ⚠️ **UI 문서 팔레트 절에 이 규정이 없다.** 자리와 크기는 구현 가정이며,
        /// 버튼 높이에 맞춘 정사각으로 왼쪽 안쪽에 넣었다 — 글자와 안 겹치게 여백을 둔다.
        /// </summary>
        private void DrawPaletteThumb(Rect button, NodeDefinition def)
        {
            if (art == null || def == null) return;
            Sprite icon = art.NodeSprite(def.type);
            if (icon == null) return; // 그림이 없으면 글자만 — 색 사각으로 대신하지 않는다

            // ⚠️ **정사각 버튼에서는 위쪽 절반을 쓴다**(2026-09-11 · 팔레트 가로 개편).
            // 종전처럼 왼쪽을 통째로 채우면 그림이 버튼을 다 덮어 **글자가 그림 뒤로 간다** —
            // 세로 줄 시절에는 버튼이 납작해서(130×36) 왼쪽 정사각이 자연히 아이콘 칸이었다.
            float side = button.height * 0.52f;
            var box = new Rect(button.x + (button.width - side) * 0.5f,
                button.y + PaletteThumbPad, side, side);
            GUI.DrawTextureWithTexCoords(box, icon.texture, SpriteUv(icon), true);
        }

        /// <summary>스프라이트가 아틀라스 안 어디에 있는지 — 그 조각만 그린다.</summary>
        private static Rect SpriteUv(Sprite s)
        {
            Rect r = s.textureRect;
            return new Rect(r.x / s.texture.width, r.y / s.texture.height,
                r.width / s.texture.width, r.height / s.texture.height);
        }

        /// <summary>
        /// 🗑️ **폐기 — 화면 위쪽에 띄우던 병목 한 줄**(2026-09-18 사용자 육안 · 시안 4 ②).
        ///
        /// ⚠️⚠️ **같은 말을 하는 자리가 둘이 됐다.** 하단 띠(<see cref="MBI.UI.StatusStrip"/>)가
        /// 「미연결 — 나가는 곳이 없는 노드가 있다」를 이미 말하는데, 이 줄이 **화면 맨 위**에서
        /// 같은 것을 또 말했다 — 게다가 그 자리는 **인셋 전투 위**라 사용자 눈에는
        /// **캐릭터 옆에 뜬 경고**로 보였다. 물류의 말이 로봇 곁에 서 있던 셈이다.
        ///
        /// 📌 **규칙은 안 걷는다** — <see cref="BottleneckHint"/> 는 노드 팝오버와 시험이
        /// 그대로 쓴다. 걷은 것은 **이 자리에 그리던 일**뿐이다.
        /// </summary>
        private void DrawBottleneckHint() { }

        // 모드 버튼 — 화면 우측 하단 1개(UI 문서 9-2).
        // **버튼이 표시를 겸한다.** 문구가 현재 모드를 그대로 나타내므로 별도 모드 표시를 두지 않는다.
        // 모드를 바꾸는 곳과 확인하는 곳이 같은 자리가 되고, 화면 요소도 하나 아낀다.
        /// <summary>
        /// <summary>
        /// **로봇 탭 A·B** — 어느 판을 편집할지 고른다 (2026-09-16 · 사용자 확정 · §74-16 ①).
        ///
        /// ⚠️ **탭은 「누가 싸우는가」가 아니라 「무엇을 편집하는가」다.** 교대는
        /// 전투 화면의 태그 버튼이 한다 — 여기서 B 를 골라도 **A 가 계속 싸운다.**
        /// 둘을 한 버튼에 묶으면 「B 줄을 손보려다 전투 로봇이 바뀌는」 일이 생긴다.
        ///
        /// ⚠️ 마운트 그림도 이 탭을 따른다(`ShowsMount`) — 보고 있는 판의 마운트라야
        /// 「이 줄이 어디로 들어가는가」가 화면에서 이어진다.
        ///
        /// ⚠️ **튜토리얼 동안에는 잠근다.** 튜토리얼은 A 판만 가르치는데 B 로 옮겨 가면
        /// 안내가 가리키는 칸이 화면에 없다.
        /// </summary>
        private void DrawRobotTabs()
        {
            var owners = new[] { MountOwner.RobotA, MountOwner.RobotB };
            var labels = new[] { "로봇 A", "로봇 B" };

            // 탭 줄과 **같은 잠금**을 쓴다 — 팔레트가 잠긴 동안 여기만 살아 있으면
            // 「눌러도 아무 일 없는 버튼」이 둘 생긴다.
            bool allowed = TutorialGate.Allows(TutorialGate.Control.PaletteOther);
            bool was = GUI.enabled;
            GUI.enabled = was && allowed;

            for (int i = 0; i < owners.Length; i++)
            {
                Rect r = UiLayout.RobotTabRect(i == 1, Screen.width, Screen.height);
                if (r.width <= 0f || r.height <= 0f) continue;
                UiBlockers.Add(r);

                bool on = _editing == owners[i];
                var style = new GUIStyle(GUI.skin.button)
                {
                    fontSize = KoreanFont.Snap(Mathf.Max(9,
                        // 탭 줄과 같은 셈이다 — 높이만 보면 좁은 창에서 글자가 옆으로 잘린다.
                        Mathf.Min(Mathf.RoundToInt(r.height * 0.30f),
                                  Mathf.RoundToInt(r.width / 4.2f)))),
                    wordWrap = false,
                    clipping = TextClipping.Overflow,
                    fontStyle = on ? FontStyle.Bold : FontStyle.Normal,
                };

                Color prev = GUI.color;
                // 고른 쪽만 밝다 — 밑줄이 아니라 **밝기**로 가른다. 칸이 세로로 커서
                // 밑줄이 글자에서 멀어지면 무엇에 붙은 줄인지 안 읽힌다.
                if (!on) GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * 0.55f);
                bool hit = UiSkin.Button(r, labels[i], style);   // 클릭음은 UiSkin 이 낸다
                GUI.color = prev;

                // 🗑️ **임시 진단 걷음**(2026-09-16 · 자리가 닫혔다). 그 줄이 답한 것:
                //    심사자 S1 점프 뒤 「로봇 탭 → RobotB · 판 노드 9 · 벨트 8」 —
                //    **탭도 그림도 먹는다.** 튜토리얼 화면에서 안 눌린 것은 결함이 아니라
                //    잠금이다(튜토리얼은 A 판만 가르치므로 탭 줄과 같은 문에 걸려 있다).
                if (hit) SetEditing(owners[i]);
            }

            GUI.enabled = was;
        }

        /// <summary>
        /// 카테고리 탭 줄 — 부유 띠 위쪽 (2026-09-15 사용자 확정 · 하단 개편 ②).
        ///
        /// ⚠️ **탭은 무엇을 놓을지가 아니라 무엇을 볼지를 정한다.** 고른 노드는 탭을 바꿔도
        /// 안 풀린다 — 「전력」 탭에서 에너지를 고르고 「물류」로 옮겨 벨트를 보다가
        /// 다시 놓으려는데 고르기가 풀려 있으면 손이 한 번 더 간다.
        ///
        /// ⚠️ **글자 크기·탭 높이는 가정이다**(설계 역기입 목록).
        /// </summary>
        private void DrawCategoryTabs()
        {
            Rect row = UiLayout.CategoryTabRect(Screen.width, Screen.height);
            if (row.width <= 0f || row.height <= 0f) return;
            UiBlockers.Add(row);

            var order = PaletteCategories.Order;
            float pad = 6f * UiLayout.Scale(Screen.height);
            float w = (row.width - pad * (order.Length - 1)) / order.Length;

            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(9,
                    // ⚠️⚠️ **글자 크기를 높이만으로 정하면 안 된다**(2026-09-15 · 육안 6차 ⑥).
                    // 탭은 **여섯으로 나뉜 가로**가 더 좁다 — 높이는 넉넉한데 폭이 모자라
                    // 글자가 옆으로 잘렸다. 두 글자가 들어갈 폭에서 거꾸로 잡고 작은 쪽을 쓴다.
                    Mathf.Min(Mathf.RoundToInt(row.height * 0.36f),
                              Mathf.RoundToInt(w / 2.6f)))),
                wordWrap = false,
                // 측정이 몇 픽셀 틀려도 글자를 안 자른다 — 동적 폰트는 안 구운 크기의
                // 폭을 모른다(「마운트」→「마우」와 같은 병 · 육안 4차 ⑧).
                clipping = TextClipping.Overflow,
            };

            // 튜토리얼 동안에는 탭도 잠근다 — 팔레트가 잠겼는데 탭만 살아 있으면
            // 「눌러도 아무 일 없는 줄」이 하나 생긴다.
            bool allowed = TutorialGate.Allows(TutorialGate.Control.PaletteOther);
            bool was = GUI.enabled;
            GUI.enabled = was && allowed;

            for (int i = 0; i < order.Length; i++)
            {
                var rect = new Rect(row.x + i * (w + pad), row.y, w, row.height);
                bool on = _tab == order[i];
                // ⚠️ **고른 표시를 글자로 붙이지 않는다**(육안 6차 ⑥).
                // 「● 」는 **두 글자만큼 폭을 먹어** 고른 탭이 유독 더 잘렸다.
                // 표시는 아래 밑줄이 맡는다 — 폭을 안 먹는다.
                if (on)
                {
                    float th = Mathf.Max(2f, rect.height * 0.08f);
                    Color prevC = GUI.color;
                    GUI.color = UiSkin.Accent;
                    GUI.DrawTexture(new Rect(rect.x, rect.yMax - th, rect.width, th),
                        Texture2D.whiteTexture);
                    GUI.color = prevC;
                }

                if (UiSkin.Button(rect, PaletteCategories.LabelOf(order[i]), style))
                {
                    _tab = order[i];
                    _paletteScroll = Vector2.zero;   // 탭을 바꾸면 줄이 달라진다 — 앞에서 본다
                }
                if (!allowed) UiSkin.DrawLockVeil(rect);
            }

            GUI.enabled = was;
        }

        /// <summary>
        /// **지금 무슨 모드인가** 판 — 보드 우측 하단 (2026-09-15 · 하단 개편 ④).
        ///
        /// ⚠️ **가정이다 · 사용자 미답.** 구 중앙 600×150 막대(`DrawModeButton`)를 걷고
        /// 이 판이 그 일을 대신한다 — **판이 곧 전환 버튼이다.**
        ///
        /// ⚠️ **문구가 구 막대와 반대다.** 막대는 「무엇으로 바뀌는가」(「▼ 조립 모드로」)를
        /// 적었는데, 이 판은 **지금 무엇인가**를 적는다(「조립 모드」) — 사용자가 「현재 모드
        /// 표시」로 지정했기 때문이다. 눌러서 바뀐다는 것은 **화살표**가 말한다.
        /// </summary>
        private void DrawModePlate()
        {
            Rect rect = UiLayout.ModePlateRect(Screen.width, Screen.height);
            UiBlockers.Add(rect);

            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(10, Mathf.RoundToInt(rect.height * 0.30f))),
            };

            bool allowed = TutorialGate.Allows(TutorialGate.Control.ModeToggle);
            bool was = GUI.enabled;
            GUI.enabled = was && allowed;

            bool build = _mode == BoardMode.Build;
            // ⚠️ 「⇄」(U+21C4) 도 폰트에 없다 — 글자로 적는다(육안 · 글리프).
            string label = (build ? "조립 모드" : "이동 모드") + " (바꾸기)";

            // 강제 버튼(T-7) — 이동 모드일 때만 빛난다. 바꾸고 나면 할 일이 끝났다.
            bool urge = TutorialSignals.HighlightBuildMode && !build;
            Color prev = GUI.color;
            if (urge) GUI.color = new Color(1f, 0.92f, 0.45f);

            if (UiSkin.Button(rect, label, style)) ToggleMode();

            GUI.color = prev;
            if (!allowed) UiSkin.DrawLockVeil(rect);
            GUI.enabled = was;
        }

        /// <summary>
        /// ⚠️ **폐기 — 중앙 600×150 모드 막대**(2026-09-15 · 하단 개편 ④ · 가정).
        ///
        /// 자리는 이제 보드 우측 하단 판이다(<see cref="DrawModePlate"/>). 되돌릴 수 있게
        /// **한 메서드로 남긴다** — 부르는 곳은 없다. 사용자가 판을 물리면 이 줄 하나를
        /// 다시 부르면 된다.
        /// </summary>
        private void DrawModeButton()
        {
            var rect = UiLayout.ModeBarRect(Screen.width, Screen.height);
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(12, Mathf.RoundToInt(rect.height * 0.30f))),
            };

            // ⚠️ **왼쪽 위로 옮긴다.** 종전에는 화면 바닥(height-78)에 고정돼 있었는데,
            // 팔레트는 y 300에서 아래로 자라므로 창 높이에 따라 **중간에서 만난다** —
            // 800×450 · 1024×600 · 1280×800 세 크기 전부에서 「제거」 버튼과 겹쳤다
            // (2026-09-02 실측). 팔레트 아래에 붙여도 팔레트가 화면보다 길면 다시 겹친다.
            //
            // 화면 바닥과 화면 위를 각각 기준으로 삼는 두 요소는 언젠가 반드시 만난다.
            //
            // ⚠️ **이제 왼쪽 위도 아니다**(2026-09-11 · 플랜 §68-4 (A)). 문서 9-4 의
            // **부유 띠**가 「미니맵·모드」의 자리이며, 거기로 내보내면 상단 인셋 전투 위에
            // 깔던 `UiPlate` 도 함께 없어진다 — 판을 깔던 이유가 **자리가 틀렸기 때문**이었다.
            // ⚠️ **띠에서 나와 막대가 됐다**(2026-09-11 사용자 확정 · 플랜 §71-22 ②).
            //
            // 부유 띠 오른쪽 칸은 **좁은 창에서 너무 작아졌다** — 615×1085(배율 0.42)에서
            // 지름 **75px** 로 최소 150 의 절반에도 못 미쳤다. 이제 **화면 가운데 · 부유 띠
            // 바로 위**에 600×150 막대로 앉는다. **조립 진입 막대와 같은 문법**이고
            // 문구가 **상태 표시를 겸한다**(「▼ 조립 모드로」면 지금은 이동 모드다).
            UiBlockers.Add(rect);

            // ⚠️ **국면이 허용할 때만 눌린다**(2026-09-11 · 플랜 §71-16 ④).
            bool modeAllowed = TutorialGate.Allows(TutorialGate.Control.ModeToggle);
            bool wasMode = GUI.enabled;
            GUI.enabled = wasMode && modeAllowed;

            // ⚠️ **문구가 「무엇으로 바뀌는가」를 말한다** — 지금 모드를 적으면 버튼인지
            // 표시인지가 안 갈린다. 조립 진입 막대(「▼ 조립 (물류 보드)」)와 같은 문법이다.
            string label = _mode == BoardMode.Pan ? "▼ 조립 모드로" : "▲ 이동 모드로";

            // 강제 버튼(T-7) — **이동 모드일 때만** 빛난다. 바꾸고 나면 할 일이 끝났다.
            bool urge = TutorialSignals.HighlightBuildMode && _mode == BoardMode.Pan;
            Color prev = GUI.color;
            if (urge) GUI.color = new Color(1f, 0.92f, 0.45f);

            if (UiSkin.Button(rect, label, style)) ToggleMode();

            GUI.color = prev;
            if (!modeAllowed) UiSkin.DrawLockVeil(rect);
            GUI.enabled = wasMode;
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

        /// <summary>
        /// ⚠️ **폐기 — 배율 버튼·라벨을 걷었다**(2026-09-15 사용자 확정 · 개편 ⑦).
        ///
        /// 자리는 **핀치 줌(모바일)** 과 **마우스 휠(PC)** 이 대신한다(<see cref="TickZoomGesture"/>).
        /// 버튼 둘과 라벨 하나가 부유 띠 오른쪽을 늘 차지하고 있었는데, 배율은 **보는 동작**이라
        /// 손가락이 화면에서 바로 하는 편이 맞다 — 새티스팩토리·엔드필드가 그렇다.
        ///
        /// 메서드는 **부르는 곳 없이 남긴다** — 되살리려면 이 한 줄을 다시 부르면 된다.
        /// ⚠️ 범위 ×1.00~×2.50 은 UI 9-3 값 그대로고 여기서 안 건드린다.
        /// </summary>
        private void DrawZoom()
        {
        }

        private void SetZoom(float z) => _zoom = Mathf.Clamp(z, ZoomMin, ZoomMax);

        /// <summary>핀치 직전 두 손가락 거리. 0 이면 핀치 중이 아니다.</summary>
        private float _pinchStartDistance;

        /// <summary>핀치를 시작할 때의 배율 — 비율을 여기에 곱한다.</summary>
        private float _pinchStartZoom;

        /// <summary>
        /// **핀치 줌(모바일) · 마우스 휠(PC)** (2026-09-15 사용자 확정 · 개편 ⑦).
        ///
        /// ⚠️ **배율 버튼을 대신한다.** 배율은 **보는 동작**이라 손가락이 화면에서 바로
        /// 하는 편이 맞다 — 버튼 둘과 라벨 하나가 부유 띠 오른쪽을 늘 차지하고 있었다.
        ///
        /// ⚠️ **범위는 UI 9-3 값 그대로다**(×1.00~×2.50) — 여기서 안 만든다.
        ///
        /// ⚠️ **휠 한 칸당 배율은 가정이다**(설계 역기입). 핀치는 **두 손가락 거리의 비**라
        /// 값이 필요 없다 — 손이 벌린 만큼이 곧 배율이다.
        ///
        /// ⚠️ **조립 화면에서만 듣는다.** 전투 화면에서 휠을 굴려도 보드가 커지면
        /// 「무엇이 움직인 건가」가 된다.
        /// </summary>
        private void TickZoomGesture()
        {
            if (!GameLayerController.BoardViewActive) return;
            if (!TutorialGate.Allows(TutorialGate.Control.Zoom)) return;

            // ── 핀치 (모바일) ────────────────────────────────────────────────
            Touchscreen touch = Touchscreen.current;
            if (touch != null && touch.touches.Count >= 2)
            {
                UnityEngine.InputSystem.Controls.TouchControl a = touch.touches[0];
                UnityEngine.InputSystem.Controls.TouchControl b = touch.touches[1];
                bool bothDown = a.press.isPressed && b.press.isPressed;

                if (bothDown)
                {
                    float d = Vector2.Distance(a.position.ReadValue(), b.position.ReadValue());
                    if (_pinchStartDistance <= 0f)
                    {
                        _pinchStartDistance = d;
                        _pinchStartZoom = _zoom;
                    }
                    else if (_pinchStartDistance > 1f)
                    {
                        SetZoom(_pinchStartZoom * (d / _pinchStartDistance));
                    }
                    return;   // 핀치 중에는 휠을 안 본다
                }
            }
            _pinchStartDistance = 0f;

            // ── 휠 (PC) ─────────────────────────────────────────────────────
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            // ⚠️ **한 칸이 얼마인지는 기기마다 다르다** — 부호만 쓰고 크기는 고정 칸으로
            // 바꾼다. 안 그러면 트랙패드에서 한 번에 상한까지 튄다.
            SetZoom(_zoom + (scroll > 0f ? ZoomStep : -ZoomStep));
        }

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

            // ⚠️ **화면 바닥에서 부유 띠로 옮겼다**(2026-09-11 · 플랜 §68-4 (A) · 문서 9-4).
            // 바닥 고정이라 액션바(조립 진입 막대)와 변수 패널 띠가 자리를 잡는 순간 그 밑에
            // 깔린다 — 자리를 안 옮기면 막대 600×160 이 미니맵을 통째로 덮는다.
            //
            // ⚠️ **가로세로비를 지킨다.** 칸은 정사각이라 보드 형태가 왜곡되면 실루엣이 안 읽힌다.
            Rect slot = UiLayout.FloatBandSlot(right: false, Screen.width, Screen.height);
            float ratio = config.rows / Mathf.Max(1f, config.columns);
            float mapH = Mathf.Min(slot.height, slot.width * ratio);
            float mapW = mapH / Mathf.Max(0.001f, ratio);
            var box = new Rect(slot.x + (slot.width - mapW) * 0.5f,
                slot.y + (slot.height - mapH) * 0.5f, mapW, mapH);
            UiBlockers.Add(box);

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

        /// <summary>선택 테두리 하나. 고른 칸을 따라 옮겨 다닌다 — 칸마다 만들지 않는다.</summary>
        private GameObject _selectRing;

        /// <summary>
        /// 고른 칸 둘레에 테두리를 두른다. 굵기는 셀의 <b>비율</b>이라 확대해도 같게 보인다.
        /// </summary>
        private void ShowSelectionRing(Vector2Int cell)
        {
            float size = _grid.CellSize;
            float thick = size * 0.06f;

            if (_selectRing == null)
            {
                _selectRing = new GameObject("SelectRing");
                _selectRing.transform.SetParent(transform, false);
                // ⚠️ SpawnQuad를 안 쓴다 — 그쪽은 **월드 좌표**로 놓아서, 테두리를 옮기면
                // 네 변이 처음 자리와의 차이만큼 어긋난다. 여기는 국소 좌표라야 한다.
                float edge = (size - thick) * 0.5f;
                RingBar(new Vector3(0f, edge, 0f), size, thick);
                RingBar(new Vector3(0f, -edge, 0f), size, thick);
                RingBar(new Vector3(-edge, 0f, 0f), thick, size);
                RingBar(new Vector3(edge, 0f, 0f), thick, size);
            }

            _selectRing.SetActive(true);
            _selectRing.transform.position = CellWorld(cell);
        }

        /// <summary>테두리 한 변. 국소 좌표로 놓아 부모를 옮기면 함께 따라온다.</summary>
        private void RingBar(Vector3 localPosition, float w, float h)
        {
            var g = new GameObject("bar");
            g.transform.SetParent(_selectRing.transform, false);
            g.transform.localPosition = localPosition;
            g.transform.localScale = new Vector3(w, h, 1f);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = UnitSprite();
            sr.color = SelectedColor;
            // 노드 몸통(0)·포트(1·2)·벨트 위 물건(2)보다 위 — 테두리가 가려지면 뜻이 없다.
            sr.sortingOrder = BeltItemOrder + 2;
        }

        /// <summary>
        /// 고르기를 푼다 — 팝오버가 닫히고 테두리가 사라진다 (2026-09-15 · 육안 ③).
        /// </summary>
        private void Deselect()
        {
            if (!_selected.HasValue) return;

            if (_markers.TryGetValue(_selected.Value, out GameObject prev) && prev != null)
            {
                prev.GetComponent<SpriteRenderer>().color =
                    _nodeColors.TryGetValue(_selected.Value, out Color pc) ? pc : NodeBaseColor;
            }

            _selected = null;
            // 노드를 안 골랐는데 테두리가 남으면 빈 칸이 골라져 있는 것으로 읽힌다.
            if (_selectRing != null) _selectRing.SetActive(false);
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
            // 그림이 붙은 노드는 **덮어 칠하지 않는다** — 한 칸을 통째로 노란 사각으로 만들면
            // 무엇을 골랐는지는 보이지만 **고른 것이 무엇인지가 안 보인다**(2026-09-09 실측).
            // 대신 칸 둘레에 테두리를 두른다. 색 사각 시절에는 칠하는 것 말고 수단이 없었다.
            if (NodeArtOf(cell) == null && _markers.TryGetValue(cell, out GameObject cur) && cur != null)
                cur.GetComponent<SpriteRenderer>().color = SelectedColor;
            ShowSelectionRing(cell);

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
                if (!_beltBodies.TryGetValue(kv.Key, out SpriteRenderer sr) || sr == null) continue;

                // 그림이 붙은 벨트는 색을 안 칠한다 — 무엇이 흐르는지는 **벨트 위 물건**이 말하고,
                // 몸통에 품목색을 곱하면 아트가 통째로 물든다. 연결 여부는 화살표가 그대로 든다.
                if (sr.sprite != UnitSprite())
                {
                    sr.color = Color.white;
                }
                else
                {
                    Color c = FlowColor(BeltFlow.KindAt(_grid, kv.Key));
                    c.a = BeltColor.a;
                    sr.color = c;
                }

                // 화살표는 **지금** 출력면을 가리켜야 한다 — 병합기·분류기는 방향이 다시 잡힌다.
                BeltInstance belt = _grid.GetBeltAt(kv.Key);

                // 타일의 배향도 같은 이유로 다시 잡는다. 놓을 때 잡아 두면 **이웃이 붙은 뒤에
                // 방향이 바뀐 병합기·분류기가 옛 각도로 남는다** — 배치 순서가 화면에 남는 셈이다.
                Sprite reArt = BeltArtOf(belt, out Quaternion reRot);
                if (reArt != null) sr.transform.localRotation = reRot;

                if (belt == null || !_beltArrows.TryGetValue(kv.Key, out SpriteRenderer arrow)) continue;
                if (arrow == null) continue;

                Vector2 off = FaceOffset(belt.OutFace);
                arrow.transform.localPosition = new Vector3(off.x * 0.32f, off.y * 0.32f, 0f);
            }
        }

        /// <summary>직전에 이어져 있던 칸. 「연결이 성립한 순간」을 가르는 유일한 기준이다.</summary>
        private readonly HashSet<Vector2Int> _connectedSeen = new HashSet<Vector2Int>();

        /// <summary>첫 배선을 봤는가. 시작 보드가 깔리는 것은 플레이어가 이은 것이 아니다.</summary>
        private bool _connectionsSeeded;

        /// <summary>
        /// 새로 이어진 칸이 있으면 철컥 한 번 (사운드 문서 3장 · 2026-09-09 배선).
        ///
        /// **몇 칸이 늘었든 한 번만 낸다.** 드래그 한 번에 벨트가 여러 칸 깔리는데
        /// 칸마다 울리면 한 조작에 소리가 뭉텅 난다 — 사건은 「연결이 성립했다」 하나다.
        ///
        /// ⚠️ **시작 보드에는 안 낸다.** 온보딩이 깔아 주는 라인은 플레이어가 이은 것이
        /// 아니고, 그때 울리면 **게임을 켜자마자 조작음이 나서** 자기가 뭘 한 줄 알게 된다.
        ///
        /// ⚠️ **줄어드는 것은 세지 않는다.** 제거는 그 나름의 사건이고 문서에 소리가 없다 —
        /// 없는 소리를 지어 붙이지 않는다.
        /// </summary>
        private void ReportNewConnections(HashSet<Vector2Int> connected)
        {
            bool grew = false;
            foreach (Vector2Int cell in connected)
                if (!_connectedSeen.Contains(cell)) { grew = true; break; }

            _connectedSeen.Clear();
            foreach (Vector2Int cell in connected) _connectedSeen.Add(cell);

            if (!_connectionsSeeded) { _connectionsSeeded = true; return; }
            if (grew) AudioSignals.Play(SoundIds.BeltConnect, SoundIds.KindOf(SoundIds.BeltConnect));
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

            // ⚠️ **몸통을 자식으로 내렸다**(2026-09-09). 벨트 타일은 배향이 있어 돌려야 하는데
            // 마커 자체를 돌리면 **화살표와 경고 표식이 함께 돈다** — 그 둘은 면 오프셋으로
            // 자리를 잡으므로 보드 좌표계에 서 있어야 한다. 도는 것은 몸통 하나뿐이다.
            var bodyGo = new GameObject("tile");
            bodyGo.transform.SetParent(m.transform, false);

            BeltInstance instance = _grid.GetBeltAt(cell);
            Sprite beltArt = BeltArtOf(instance, out Quaternion beltRot);
            var sr = bodyGo.AddComponent<SpriteRenderer>();
            sr.sprite = beltArt != null ? beltArt : UnitSprite();
            sr.color = beltArt != null ? Color.white : BeltColor;
            sr.sortingOrder = MarkerOrder;
            if (beltArt != null)
            {
                bodyGo.transform.localRotation = beltRot;
                // 타일은 칸을 **가득** 채운다 — 0.85로 두면 칸 사이에 틈이 생겨 줄이 끊겨 보인다.
                float parentScale = Mathf.Max(0.0001f, _grid.CellSize * 0.85f);
                bodyGo.transform.localScale =
                    Vector3.one * (FitScale(beltArt, _grid.CellSize) / parentScale);
            }
            _beltBodies[cell] = sr;

            // ⚠️ **방향 네모는 그림이 붙은 벨트에서 걷었다**(2026-09-10 사용자 확정 · 플랜 §66-21 c).
            //
            // 이것은 화살표가 아니라 **흰 사각에 색을 칠한 자리표시**였다. 벨트 타일이 색 사각이던
            // 시절에는 그것이 유일한 방향 정보였는데, **그림이 붙은 지금은 타일의 방향선이 정보**다.
            // 자리표시가 그림 위에 얹혀 **칸마다 큰 초록 네모가 먼저 눈에 들었다**(촬영 D구간).
            //
            // ⚠️ **연결 여부 표시가 통째로 사라지지는 않는다** — 끝단 미연결 경고(`warn`)는 그대로다.
            // 그림이 없는 벨트(새 클론·아트 누락)에서는 여기가 유일한 방향 정보라 **그때만 남긴다.**
            //
            // ⚠️ **규정 문서가 없다.** §5-4 L2 가 「연결=초록 / 미연결=노랑」을 적었을 뿐
            // **모양을 규정한 문서는 없다** — 걷는 것을 V03 으로 통보한다.
            SpriteRenderer asr = null;
            if (beltArt == null)
            {
                var arrow = new GameObject("dir");
                arrow.transform.SetParent(m.transform, false);
                Vector2 off = FaceOffset(outFace);
                arrow.transform.localPosition = new Vector3(off.x * 0.32f, off.y * 0.32f, 0f);
                arrow.transform.localScale = new Vector3(0.34f, 0.34f, 1f);
                asr = arrow.AddComponent<SpriteRenderer>();
                asr.sprite = UnitSprite();
                asr.color = BeltArrowColor;
                // ⚠️ 보드는 **자기 지역 순서대로 그린다**(격자 배경 -3 · 셀선 -2 · 마커 0).
                // 여기에 SortingLayers.Tile(-20)을 쓰면 화살표가 벨트 몸통(0) 뒤로 들어가 안 보인다 —
                // 실제로 그래서 방향 표시가 화면에 없었다.
                asr.sortingOrder = BeltArrowOrder;
            }

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
        /// <summary>
        /// **어느 판이든** 면·품목·링크를 푼다 (2026-09-21).
        ///
        /// ⚠️ 색과 화살표는 여기 없다 — 그것은 **보고 있는 판**의 일이다.
        /// 여기서 푸는 셋은 **안 보는 판에도 필요하다**(그래야 벨트가 나른다).
        ///
        /// 📌 순서가 중요하다: **면 → 품목 → 링크.** 면이 안 잡히면 링크가 안 서고,
        ///    링크가 안 서면 품목이 못 흐른다.
        /// </summary>
        private static void ResolveFlow(BoardGrid grid, BeltItemFlow flow)
        {
            if (grid == null || flow == null) return;
            BeltAutoOrient.Resolve(grid);
            BeltFlow.Resolve(grid);
            flow.Rebuild(grid);
        }

        private void RefreshConnections()
        {
            // 푸는 셋은 **판을 안 가리는 일**이라 따로 빼 두었다(`ResolveFlow`) —
            // 시작할 때 판 둘에 다 써야 하기 때문이다(2026-09-21).
            ResolveFlow(_grid, ItemFlow);

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

            ReportNewConnections(connected);

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
            // ⚠️⚠️ **보드가 바뀌는 것을 여기서 한 번만 본다**(2026-09-16).
            //
            // 놓기·지우기·회전·조합표·모듈·병합기 — 전부 이 메서드를 거친다(부르는 곳 열한 곳).
            // 바꾸는 자리마다 저장을 붙이면 **빠뜨리는 자리가 생긴다** — 오늘까지
            // 이 리포에서 반복된 결함이 대개 그 모양이었다.
            //
            // 파일에 바로 쓰지는 않는다 — 신호에만 올려 두고 방치 런타임의 자동저장이
            // 가져간다. 한 프레임에 여러 번 바뀌어도 디스크는 한 번만 닿는다.
            IdleSignals.BoardState = BoardStateCodec.Capture(_grid);

            // **면 화살표는 배치가 바뀔 때만 다시 짓는다**(2026-09-16 · §74-3 #34).
            // 매 프레임 짓지 않는 까닭은 저장과 같다 — 바뀌는 자리가 전부 여기를 지난다.
            RebuildWiringArrows();
        }

        /// <summary>
        /// **아직 다 안 이어진 노드의 입출력 면에 화살표를 세운다**
        /// (2026-09-16 신설 · 사용자 확정 · 플랜 §74-3 #34).
        ///
        /// 📌 **판정은 `NodeWiringHints` 가 한다** — 여기서는 그리기만 한다.
        ///    「면이 이어졌는가」를 화면 쪽에서 다시 세면 답이 둘이 된다(지침 §7).
        ///
        /// ⚠️ **다 이어진 노드에는 안 그린다** — 이어지는 순간 사라진다.
        /// ⚠️ **작은 커넥터·「면이 다름」 경고와 함께 선다** — 셋이 말하는 것이 다르다:
        ///    커넥터는 「여기 면이 있다」, 경고는 「면이 안 맞는다」, 화살표는
        ///    **「이 노드는 아직 할 일이 남았고 방향은 이쪽이다」**.
        ///
        /// ⚠️ 크기·색은 **가정**이다(칸의 1/4 · 미색 · UI 문서 역기입 자리).
        /// </summary>
        private void RebuildWiringArrows()
        {
            foreach (GameObject go in _wiringArrows) if (go != null) Destroy(go);
            _wiringArrows.Clear();

            if (_grid == null) return;

            Sprite arrowArt = art != null ? art.portArrow : null;
            // ⚠️ **자산이 없으면 안 그린다** — 흰 사각으로 대신하면 「무엇을 가리키는지」가
            //    사라져 보드에 뜻 없는 네모만 늘어난다.
            if (arrowArt == null) return;

            float cell = _grid.CellSize;
            float size = cell * WiringArrowCellFraction;
            float scale = FitScale(arrowArt, size);

            foreach (PortHint h in NodeWiringHints.Collect(_grid))
            {
                Vector2 off = FaceOffset(h.face);

                var go = new GameObject($"arrow_{h.cell.x}_{h.cell.y}_{h.face}");
                go.transform.SetParent(transform, false);

                // 면의 **안쪽**에 앉힌다 — 칸 밖으로 내보내면 이웃 칸의 그림과 겹친다.
                Vector3 c = CellWorld(h.cell);
                go.transform.position = new Vector3(
                    c.x + off.x * cell * WiringArrowInset,
                    c.y + off.y * cell * WiringArrowInset, 0f);

                // ⚠️ **자산은 오른쪽을 본다** — 입구는 안쪽(면의 반대), 출구는 바깥을 본다.
                Vector2 dir = h.outward ? off : -off;
                go.transform.rotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                go.transform.localScale = Vector3.one * scale;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = arrowArt;
                sr.color = WiringArrowColor;
                // 노드 그림 **위**다 — 밑에 깔면 타일에 묻힌다.
                sr.sortingOrder = MarkerOrder + 2;
                // 층은 <c>KeepBoardOnItsLayer</c> 가 계층이 바뀐 것을 보고 통째로 입힌다.
                _wiringArrows.Add(go);
            }
        }

        /// <summary>면 화살표들. 배치가 바뀔 때 통째로 다시 짓는다.</summary>
        private readonly List<GameObject> _wiringArrows = new List<GameObject>();

        /// <summary>화살표 한 변 = 칸의 몇 분의 몇인가. ⚠️ **가정 0.25**(사용자 확정 문구).</summary>
        private const float WiringArrowCellFraction = 0.25f;

        /// <summary>칸 가운데에서 면 쪽으로 얼마나 미는가(칸 기준). ⚠️ 가정 — 면에 붙되 안 넘는다.</summary>
        private const float WiringArrowInset = 0.33f;

        /// <summary>화살표 색 — ⚠️ **가정 미색**(UI 문서에 절이 없다).</summary>
        private static readonly Color WiringArrowColor = new Color(0.97f, 0.95f, 0.86f, 0.92f);

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

        // ── 아트 배선 (2026-09-09 · 보드·품목 승인분) ──────────────────────────────────
        //
        // **규칙 하나: 그림이 있으면 코드가 색을 칠하지 않는다.** 종류는 그림이 말하고
        // 코드는 **밝기만 곱한다**(NodeStatusTint 주석 · UI 문서「노드 상태 표시」).
        // 색 사각은 사라지지 않고 **그림이 없을 때의 폴백**으로 남는다 — 아직 안 온 품목이 있고,
        // 그것이 보드를 통째로 비게 만들면 안 된다.
        //
        // ⚠️ **타일의 기준 배향은 파일을 열어서 읽은 것이다**(2026-09-09).
        //   포트 셋 · `merger` — 표시된 면이 **한 면**이고, 화면에서 그 면이
        //   포트 = 남쪽 / 병합기 = 동쪽에 있다. 분류기는 입력면이 **북쪽**이다.
        //   🗑️ 구 `belt_end` 는 이 줄에서 뺐다 — **끝단 자산 없음 · 폐기 09-06**.
        //   직선은 세로(남북)이고 코너는 **서–남**을 잇는다.
        // ⚠️ **이 배향이 맞는지는 화면이 판정한다** — 배치모드는 회전을 못 본다.

        /// <summary>면이 가리키는 방향의 각(도). 동쪽이 0이고 반시계로 돈다.</summary>
        private static float FaceAngle(PortFace face)
        {
            switch (face)
            {
                case PortFace.East: return 0f;
                case PortFace.North: return 90f;
                case PortFace.West: return 180f;
                default: return 270f; // South
            }
        }

        /// <summary>기준 면이 <paramref name="target"/>을 향하도록 타일을 돌리는 각.</summary>
        private static Quaternion FaceRotation(PortFace baseFace, PortFace target)
            => Quaternion.Euler(0f, 0f, FaceAngle(target) - FaceAngle(baseFace));

        /// <summary>이 칸 노드의 그림. 아트가 없거나 그 종류의 그림이 없으면 null.</summary>
        private Sprite NodeArtOf(Vector2Int cell)
        {
            if (art == null || _grid == null) return null;
            NodeInstance inst = _grid.GetAt(cell);
            return inst == null || inst.Definition == null ? null : art.NodeSprite(inst.Definition.type);
        }

        /// <summary>
        /// 벨트 칸의 그림과 그 배향.
        ///
        /// 🗑️ **끝단 자산 없음 · 폐기 09-06**(사용자 확정). 구 주석은 「끝단은 `belt_end` 가
        /// 따로 있다 — 이어지지 않은 칸을 직선으로 그리면 끊긴 것이 안 보인다」였는데,
        /// ⚠️ **그 그림을 그리는 코드는 한 줄도 없었다** — 자산 칸에 담기만 했다.
        /// 끊긴 것을 화면이 말하는 일은 **미연결 경고** 쪽이 진다.
        /// </summary>
        private Sprite BeltArtOf(BeltInstance belt, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (art == null || belt == null) return null;

            switch (belt.Element)
            {
                case BeltElementKind.Corner:
                    // 코너는 두 면을 잇는다. 기준은 서–남이고, 서쪽을 입력면에 맞추면 남쪽이 출력면에 간다.
                    rotation = CornerRotation(belt.InFace, belt.OutFace);
                    return art.beltCorner;
                case BeltElementKind.Merger:
                    rotation = FaceRotation(PortFace.East, belt.OutFace);
                    return art.merger;
                case BeltElementKind.Sorter:
                    rotation = FaceRotation(PortFace.North, belt.InFace);
                    return art.sorter;
                default:
                    // 직선의 기준은 세로다 — 가로로 흐르면 90도 돌린다. 흐르는 쪽은 화살표가 말한다.
                    bool horizontal = belt.OutFace == PortFace.East || belt.OutFace == PortFace.West;
                    rotation = Quaternion.Euler(0f, 0f, horizontal ? 90f : 0f);
                    return art.beltStraight;
            }
        }

        /// <summary>코너 타일의 각. 기준이 **서–남**이라 그 짝을 돌려 맞춘다.</summary>
        private static Quaternion CornerRotation(PortFace inFace, PortFace outFace)
        {
            // 네 짝밖에 없다. 표로 두면 어느 짝이 안 맞는지 화면에서 바로 지목된다.
            bool ws = Pair(inFace, outFace, PortFace.West, PortFace.South);
            bool se = Pair(inFace, outFace, PortFace.South, PortFace.East);
            bool en = Pair(inFace, outFace, PortFace.East, PortFace.North);
            if (ws) return Quaternion.identity;
            if (se) return Quaternion.Euler(0f, 0f, 90f);
            if (en) return Quaternion.Euler(0f, 0f, 180f);
            return Quaternion.Euler(0f, 0f, 270f); // 북–서
        }

        private static bool Pair(PortFace a, PortFace b, PortFace x, PortFace y)
            => (a == x && b == y) || (a == y && b == x);

        /// <summary>
        /// 그림을 셀 한 칸에 맞추는 배율. 스프라이트의 **실제 월드 크기를 재서** 나눈다 —
        /// 캔버스 규격을 상수로 다시 적으면 아트가 바뀔 때 조용히 어긋난다.
        /// </summary>
        private float FitScale(Sprite sprite, float targetWorldSize)
        {
            if (sprite == null) return targetWorldSize;
            float size = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            return size > 0.0001f ? targetWorldSize / size : targetWorldSize;
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
