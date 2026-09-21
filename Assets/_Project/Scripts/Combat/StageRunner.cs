using MBI.UI;
using System.Collections.Generic;
using MBI.Core;
using MBI.Core.Combat;
using MBI.Core.Anim;
using MBI.Core.Audio;
using MBI.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MBI.Combat
{
    /// <summary>
    /// 실시간 탑뷰 전투 런타임(§5-6·7). StageDefinition·RobotDefinition·CombatTuning을 받아
    /// CombatSimulation(순수 코어)을 구동하고 플레이스홀더 뷰로 렌더한다.
    ///
    /// 전투력(물류 출력)은 MockLogisticsOutput 브릿지가 공급(벨트/시뮬 미구현 → mock 대표 상태 145).
    /// HP·이동/사거리·스폰은 CombatTuning의 TBD placeholder(⚠️ chat+Notion 확정 필요).
    /// </summary>
    public sealed partial class StageRunner : MonoBehaviour
    {
        [Header("데이터(생성기 산출 SO)")]
        public RobotDefinition robot;
        [Tooltip("태그 상대(로봇 B, 드론 운용기). 비우면 로봇 한 대로 돈다 — 태그·합체가 없는 기존 경로.")]
        public RobotDefinition robotB;
        [Tooltip("합체체. 합체 지속 20초 동안 로봇 A·B 스프라이트를 대신한다(15-3 7장). 비우면 원래 로봇이 그대로 선다.")]
        public RobotDefinition robotFusion;
        public StageDefinition stage;
        public CombatTuning tuning;
        [Tooltip("적 카탈로그(atk 조회용). Enemy_infantry/artillery/armor/boss.")]
        public List<EnemyDefinition> enemyCatalog = new List<EnemyDefinition>();
        [Tooltip("자동 조종(방치). 수동 입력이 들어오면 잠시 양보한다.")]
        public bool autoPilot = true;

        /// <summary>전투 시뮬(자동 전투 컨트롤러가 결과·처치를 읽는다). Begin 전에는 null.</summary>
        public CombatSimulation Sim => _sim;

        /// <summary>현재 전투 결과. 시뮬이 없으면 진행 중으로 본다.</summary>
        public CombatResult CurrentResult => _sim != null ? _sim.Result : CombatResult.InProgress;

        /// <summary>현재 스테이지 SO.</summary>
        public StageDefinition CurrentStage => stage;

        private CombatSimulation _sim;
        private CombatEntityView _robotView;
        private readonly Dictionary<CombatEntity, CombatEntityView> _enemyViews =
            new Dictionary<CombatEntity, CombatEntityView>();

        private float _output;      // 물류 출력(전투력) 표시값
        private float _nominalOutput;                                   // 만공급 시 출력(라이브 스케일의 분모)
        private float _lastScale = 1f;                                  // 마지막으로 반영한 물류 배율

        // 재배분 판정 (2026-09-15). 배율 하나만 보던 동안 라인이 0 줄로 굳었다 — `FireRateGate`.
        private readonly FireRateGate _fireGate = new FireRateGate();

        // 🗑️ **폐기 — HUD 접기**(2026-09-18 · 시안 3). 접을 **글자 블록 자체가 없어졌다.**
        //    조립 화면에서는 전투 HUD 를 한 줄도 안 그리고, 전투 화면에서는 글자가 아니라
        //    칩·배지·막대라 접을 것이 없다. 기억하던 `PlayerPrefs` 키도 함께 버린다.

        private readonly List<AmmoLine> _lineBuffer = new List<AmmoLine>(); // 재배분 버퍼(프레임당 할당 0)
        private const float ScaleEpsilon = 0.001f;                      // 이만큼 변해야 재배분
        private float _manualHoldUntil;                                 // 이 시각까지는 수동 우선(자동 정지)
        private float _mountCoef;
        private bool _ready;
        private float _lastRobotHp; // 피격 점멸 트리거 — HP가 줄어든 프레임을 잡는다
        private readonly MergeCutscene _cutscene = new MergeCutscene(); // 합체 3초 연출(시간표는 코어가 쥔다)
        private int _viewedRobotIndex;  // 뷰가 지금 그리고 있는 로봇 — 교대하면 다시 묶는다
        private bool _viewedMerged;     // 뷰가 지금 합체체를 그리고 있는가 — 합체 시작·종료에 다시 묶는다
        private bool _pointerDown;      // 플릭 인식: 누른 상태인가
        private Vector2 _pointerStart;  // 누른 지점(스크린 픽셀)
        private float _pointerDownTime;
        private readonly Dictionary<DroneUnit, SpriteRenderer> _droneViews =
            new Dictionary<DroneUnit, SpriteRenderer>();
        private GUIStyle _hudSmall;     // 막대 옆 숫자
        private GUIStyle _hudSegment;   // 막대 칸 안 이름

        /// <summary>지금 나가 있는 로봇의 SO. 태그하면 바뀐다 — 스프라이트·색이 여기서 온다.</summary>
        private RobotDefinition ActiveRobotDef
        {
            get
            {
                // 합체 지속 20초 동안 합체체가 로봇 A·B 스프라이트를 대신한다
                // (합체 로봇 아트 요청 문서(15-3) 7장 · 260907_W01 3-1).
                if (IsMerged && robotFusion != null) return robotFusion;
                return _sim != null && _sim.ActiveRobotIndex == 1 && robotB != null ? robotB : robot;
            }
        }

        /// <summary>합체 중인가. 뷰 교체의 유일한 조건이다.</summary>
        private bool IsMerged => _sim != null && _sim.Merge != null && _sim.Merge.IsActive;

        // 로봇 두 대를 색으로 구분한다(아트가 들어오면 스프라이트가 이깁니다).
        private static readonly Color RobotAColor = new Color(0.3f, 0.6f, 1f);
        private static readonly Color RobotBColor = new Color(0.45f, 0.9f, 0.55f);

        // 크기는 아트 캔버스가 결정한다(ArtSpec, PPU 192 — V02 §4). 플레이스홀더도 실물과 같은 자리를
        // 차지하게 해서 스프라이트 교체 때 레이아웃이 흔들리지 않게 한다.
        private static float RobotSize => ArtSpec.RobotSize; // 256px → 1.333칸

        // 태그 진입이 시작되는 자리. 화면 우측 밖이면 되므로 아레나 반경보다 넉넉히 잡는다.
        private const float TagEntryOffsetX = 8f;

        private static Sprite _circleSprite;

        /// <summary>단위 원반 스프라이트(중심 옅은 채움 + 가장자리 밝은 링). 스케일로 아레나 지름 반영.</summary>
        private static Sprite CircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;
            const int n = 128;
            float r = n * 0.5f - 1f;      // 반경(px)
            float ring = 3f;              // 테두리 링 두께(px)
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var c = new Vector2(n * 0.5f, n * 0.5f);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float a;
                if (dist > r) a = 0f;                       // 밖 = 투명
                else if (dist > r - ring) a = 0.85f;        // 가장자리 링 = 진하게
                else a = 0.07f;                             // 내부 = 옅은 채움
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return _circleSprite;
        }

        /// <summary>
        /// 적 표시 크기(보스 크게). **뷰·그림자·HP 바·충돌 반경이 공유하는 단일 규칙**이라
        /// 여기 하나만 고치면 넷이 같이 따라간다.
        ///
        /// 아트 캔버스 규격에서 온다 — 몬스터 128px(0.667칸) ·
        /// 보스는 <see cref="ArtSpec.BossViewSize"/>(벌 256px × 배율 2 = 2.667칸).
        ///
        /// ⚠️ **구 서술 폐기** — 「보스 512px」이라 적혀 있었다. 승인본은 **256** 이고
        /// 코드가 2 배로 키운다(2026-09-10 사용자 확정). 숫자(2.667칸)는 우연히 같지만
        /// **나오는 길이 다르다** — 512 벌이 오는 날 배율은 1 로 돌아간다.
        /// </summary>
        /// <summary>몸 크기 규칙은 <see cref="StageSpawnFactory"/> 가 들고 있다 — 값이 두 곳에 살면 안 된다.</summary>
        private static float EnemySize(float maxHp) => StageSpawnFactory.EnemySize(maxHp);

        /// <summary>
        /// 표시명 → 화면 배율. **값은 SO 에서 온다**(<see cref="EnemyDefinition.viewScale"/>) —
        /// 코드가 배율을 직접 들고 있지 않게 하려는 것이다(지침 §3).
        ///
        /// ⚠️ **시뮬에 넣지 않는다.** 배율은 화면의 일이고 <c>CombatSimulation</c> 은 순수 규칙이다.
        /// 스폰을 만들 때 여기에 적어 두었다가 뷰를 만들 때 쓴다.
        /// </summary>
        private readonly Dictionary<string, int> _viewScaleByLabel = new Dictionary<string, int>();

        /// <summary>표시명 → 그림. 뷰가 받는 것은 <c>CombatEntity</c> 뿐이라 정의를 여기서 찾는다.</summary>
        private readonly Dictionary<string, EnemyDefinition> _artByLabel =
            new Dictionary<string, EnemyDefinition>();

        private void Start()
        {
            if (robot == null || stage == null || tuning == null)
            {
                Debug.LogError("[MBI] StageRunner 참조 누락(robot/stage/tuning) — 'MBI/Create Combat Scene'로 씬 생성 필요.");
                enabled = false;
                return;
            }
            BuildBackground(); // 바닥 그림 — 원반보다 아래(-40). 스테이지가 바뀌면 다시 깐다.

            // ⚠️ **메인 메뉴가 떠 있으면 여기서 시작하지 않는다**(2026-09-10 · 플랜 §66-10).
            // 깔아 두는 것(배경·경계)은 미리 해 둔다 — 메뉴 뒤에 보이는 화면이기 때문이다.
            // **시뮬과 음악 국면만 미룬다.** 메뉴가 없는 씬에서는 빗장이 없어 곧바로 시작한다.
            TryBeginWhenAllowed();
        }

        /// <summary>
        /// 시작해도 되면 시작한다. **한 번만 돈다** — 빗장이 처음 한 번만 참을 낸다.
        /// </summary>
        private bool TryBeginWhenAllowed()
        {
            if (!MainMenuGate.TryStart()) return false;

            PushMusicPhase(); // 어느 곡을 틀지 — 판정은 코어가 하고 여기서는 신호만 쓴다.
            Begin();
            return true;
        }

        /// <summary>
        /// 배경 음악의 국면을 신호에 써 둔다 (2026-09-09 · 사운드 문서 6장).
        ///
        /// **판정은 `MusicPhaseRule` 한 자리에만 있다** — 배경 그림의 보스 판정과 같은
        /// `reqType == Budget`을 쓰며, 여기서 스테이지 id를 다시 비교하지 않는다.
        ///
        /// ⚠️ **재생기를 직접 부르지 않는다.** 러너가 `AudioSource`를 알면 격리 전투 씬처럼
        /// 재생기가 없는 자리에서 참조가 빈다 — 신호는 받는 쪽이 없어도 성립한다.
        /// </summary>
        private void PushMusicPhase()
        {
            if (stage == null) return;
            AudioSignals.Phase = MusicPhaseRule.Of(stage.reqType);
        }

        // ── 전투 배경 (2026-09-09 배선) ────────────────────────────────────────────
        //
        // **임포트 설정을 안 건드리고 코드로 격자 복제해 깐다.** 한 장을 Repeat 으로 늘리려면
        // 임포터의 Wrap 을 바꿔야 하는데, 그 설정은 `SpriteImportRules`가 `Art/` 전체에
        // 한 규격으로 강제하고 있다 — 배경 하나 때문에 손대면 **다른 스프라이트가 조용히
        // 따라 바뀐다**(지침 §7 ［08-31］ 「에디터에서만 보면 안 드러나는 결함」과 같은 종류다).
        //
        // 까는 넓이는 **시야 + 여유 한 장**이다. 여유가 없으면 아래의 오프셋이 한 장만큼
        // 밀 때 가장자리에 빈 줄이 생긴다.

        private Transform _bgRoot;
        private Sprite _bgSprite;      // 지금 깔린 그림 — 보스 스테이지로 바뀌면 다시 깐다
        private Vector2 _bgTile;       // 한 장의 월드 크기
        private Vector2 _bgViewport;   // 깔 때 본 시야 — 창이 바뀌면 다시 깐다

        /// <summary>
        /// 이 스테이지의 바닥 그림. **보스 배경은 S6에서만**이며, 그 판정은
        /// 이미 있는 <see cref="StageReqType.Budget"/>(「예산식(보스 HP) — S6」)을 그대로 쓴다.
        /// 새 상수를 만들지 않는다 — 스테이지 id 문자열을 여기서 다시 비교하면
        /// 「S6이 무엇인가」에 답이 둘이 된다.
        /// </summary>
        private Sprite BackgroundForStage()
        {
            if (tuning == null) return null;
            bool boss = stage != null && stage.reqType == StageReqType.Budget;
            Sprite pick = boss ? tuning.bossBackgroundSprite : tuning.combatBackgroundSprite;
            // 보스 그림이 아직 없으면 일반 배경으로 내려앉는다 — 바닥이 통째로 사라지는 것보다 낫다.
            return pick != null ? pick : tuning.combatBackgroundSprite;
        }

        private void BuildBackground()
        {
            Sprite bg = BackgroundForStage();
            if (bg == null)
            {
                if (_bgRoot != null) Destroy(_bgRoot.gameObject);
                _bgRoot = null;
                _bgSprite = null;
                return;
            }

            Camera cam = Camera.main;
            float halfH = cam != null && cam.orthographic ? cam.orthographicSize : 5f;
            // ⚠️ **조립 화면 상단 인셋까지 덮는다**(2026-09-10 · 플랜 §66-21 d 실측).
            // 인셋 카메라는 세로가 30% 뿐이라 **가로세로비가 주 카메라의 3.3배**다 —
            // 주 카메라만 보고 깔면 인셋 좌우에 검은 여백이 남는다.
            float halfW = cam != null && cam.orthographic
                ? CombatInsetView.BackgroundHalfWidth(halfH, cam.aspect)
                : halfH;

            if (_bgRoot != null) Destroy(_bgRoot.gameObject);

            var root = new GameObject("Background");
            root.transform.SetParent(transform, false);
            _bgRoot = root.transform;
            _bgSprite = bg;
            _bgViewport = new Vector2(halfW, halfH);

            // 한 장의 월드 크기는 **재서 쓴다.** 캔버스를 상수로 다시 적으면 아트가 바뀔 때
            // 조용히 어긋난다(보드 아트 배선의 `FitScale`과 같은 이유).
            _bgTile = bg.bounds.size;
            if (_bgTile.x <= 0.0001f || _bgTile.y <= 0.0001f) return;

            // 시야를 덮는 장수 + 여유 — 셈은 `BackgroundTiling` 이 한다(시험이 닿는 자리).
            int cols = BackgroundTiling.Count(halfW, _bgTile.x);
            int rows = BackgroundTiling.Count(halfH, _bgTile.y);

            for (int x = 0; x < cols; x++)
            for (int y = 0; y < rows; y++)
            {
                var go = new GameObject($"bg_{x}_{y}");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(
                    (x - (cols - 1) * 0.5f) * _bgTile.x,
                    (y - (rows - 1) * 0.5f) * _bgTile.y, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = bg;
                // **아레나 원반보다 아래다.** 원반은 「여기까지 움직일 수 있다」는 경계라
                // 바닥에 묻히면 뜻이 사라진다.
                sr.sortingOrder = SortingLayers.BackgroundFar;
            }
        }

        /// <summary>
        /// 바닥을 로봇 위치의 나머지만큼 민다 — **바닥이 로봇 아래로 흐르는 것처럼 보인다.**
        ///
        /// 카메라가 고정이라 로봇만 움직이면 바닥이 정지 화면처럼 남는데, 그러면
        /// 「움직이고 있다」가 화면에서 지워진다. 한 장 폭의 나머지만 쓰므로 **타일 수가
        /// 안 늘고**, 격자가 자기 자신과 이어져 끝이 안 보인다.
        /// </summary>
        private void UpdateBackgroundOffset()
        {
            if (_bgRoot == null) return;

            // ⚠️ **카메라를 기준으로 깐다**(2026-09-15 · 육안 ⑥ 카메라 추적).
            //
            // 종전에는 로봇 위치의 나머지만 썼다 — 카메라가 고정이었으므로 그것으로 충분했다.
            // 카메라가 로봇을 따라가게 된 지금은 **바닥도 카메라를 따라가야** 한다. 안 그러면
            // 로봇이 멀리 걸어간 만큼 바닥이 화면 밖으로 빠져 **검은 바닥**이 드러난다.
            //
            // 카메라 중심을 **타일 격자에 스냅한 자리**에 둔다 — 한 장 폭의 나머지만 쓰므로
            // 타일 수가 안 늘고, 격자가 자기 자신과 이어져 끝이 안 보인다.
            //
            // ⚠️⚠️ **기준은 「지금 전투를 비추는 카메라」다**(2026-09-16 · 육안 10차 ② 결함).
            //
            // 09-15 에 「로봇 기준」으로 되돌린 까닭은 옳았다 — 조립 화면의 전투는
            // **상단 인셋**(`combatInsetCam`)이고 그때 주 카메라는 **보드**를 비추므로,
            // 주 카메라만 보고 깔면 바닥이 보드 쪽으로 가 인셋이 검어진다.
            //
            // 그런데 「로봇 기준이면 lerp 차이는 타일 한 장 안에서 흡수된다」는 **틀렸다.**
            // 주 카메라는 로봇을 lerp 로 따라가므로 **늘 뒤처져 있고**, 아래로 걸을 때는
            // 로봇보다 **위**에 남는다. 그 뒤처진 만큼 카메라 윗변이 격자 윗변을 넘어선다 —
            // 사용자가 본 「아래로 이동할 때 상단 배경이 짧게 사라진다」가 그것이다.
            //
            // 📌 두 화면이 서로 다른 카메라를 쓰므로 **화면에 따라 기준도 갈린다** —
            //    · 전투 화면: 주 카메라가 전투를 비춘다 → **주 카메라 자리**(뒤처짐 그대로 반영).
            //    · 조립 화면: 인셋이 전투를 비추고 그것은 `CombatFocus`(=로봇) 에 **딱** 붙는다
            //      → **로봇 자리**. 주 카메라를 쓰면 09-15 의 검은 인셋이 그대로 돌아온다.
            // 거리 가정을 새로 적지 않는다 — **비추는 자리를 그대로 쓴다**(지침 §7).
            Vector2 robot = _sim != null && _sim.Robot != null ? _sim.Robot.position : Vector2.zero;
            Vector2 p = robot;
            if (!GameViewSignals.BoardViewActive)
            {
                Camera main = Camera.main;
                if (main != null) p = main.transform.position;
            }

            Vector2 origin = BackgroundTiling.SnapOrigin(p, _bgTile);
            _bgRoot.position = new Vector3(origin.x, origin.y, _bgRoot.position.z);
        }

        /// <summary>깔아 둔 전제가 바뀌었는가 — 스테이지(보스)와 창 크기 둘뿐이다.</summary>
        private bool BackgroundNeedsRebuild()
        {
            if (_bgRoot == null) return BackgroundForStage() != null;
            if (BackgroundForStage() != _bgSprite) return true;

            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic) return false;
            float halfH = cam.orthographicSize;
            // 깔 때와 **같은 셈**을 쓴다 — 다르면 매 프레임 「모자란다」로 읽혀 계속 다시 깐다.
            float halfW = CombatInsetView.BackgroundHalfWidth(halfH, cam.aspect);
            // 창이 커지면 덮던 넓이가 모자란다. 줄어드는 쪽은 남는 것이라 다시 깔지 않는다.
            return halfW > _bgViewport.x + 0.001f || halfH > _bgViewport.y + 0.001f;
        }

        /// <summary>
        /// ⚠️ **아레나 원반은 폐기됐다** (2026-09-11 사용자 확정 · 플랜 §71-28 1).
        ///
        /// 반경 `arenaRadiusTbd` 의 청록 원반 + 테두리 링을 바닥에 깔던 자리다.
        /// 그 원은 **이동 클램프의 그림**이었고, 클램프가 없어지면서 **가리킬 것이 사라졌다** —
        /// 경계가 없는데 경계선만 남으면 화면이 거짓말을 한다.
        ///
        /// 전장은 이제 **로봇을 따라다니는 판**이고, 적은 <see cref="SpawnRingRule"/> 이 내는
        /// **화면 밖 링**에서 걸어 들어온다. 그 링은 **안 그린다** — 보이면 안 되는 자리다.
        /// </summary>
        private void SetSpawnRingFromCamera()
        {
            if (_sim == null) return;

            // ⚠️ **시뮬은 카메라가 없다** — 밖에서 재서 넣는다(`SetVisibleBounds` 와 같은 자리).
            // 값이 `260911_W03` 으로 오면 그것을 쓰고, 그 전에는 **화면 대각선 반 + 한 칸**을
            // 가정으로 쓴다(`SpawnRingRule.RadiusFromView`).
            // ⚠️ **띠가 먼저다**(2026-09-15 사용자 확정 · §72-40) — 반경 하나는 그 뒤다.
            // 적이 min~max 사이 아무 거리에나 나야 가까운 것은 제자리에서 쏘고
            // 먼 것에는 걸어간다. 링 하나로는 **전부 같은 거리**라 둘 중 하나만 일어난다.
            float bandMin = tuning != null ? tuning.spawnRingMinTbd : 0f;
            float bandMax = tuning != null ? tuning.spawnRingMaxTbd : 0f;
            if (bandMin > 0f && bandMax > 0f) { _sim.SetSpawnBand(bandMin, bandMax); return; }

            float fromSo = tuning != null ? tuning.spawnRingRadiusTbd : 0f;
            if (fromSo > 0f) { _sim.SetSpawnRing(fromSo); return; }

            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic) return;

            float h = cam.orthographicSize * 2f;
            float w = h * cam.aspect;
            _sim.SetSpawnRing(SpawnRingRule.RadiusFromView(w, h, SpawnRingMarginCells));
        }

        /// <summary>링이 화면 밖으로 나가는 여유 — **한 칸**(아트 규격 PPU 192 = 1 월드 유닛).</summary>
        private const float SpawnRingMarginCells = 1f;

        /// <summary>시뮬·뷰 구성(최초 및 재시작 공용).</summary>
        // ---- 창고 승계 (260902_W08 §1) ----
        //
        // **스테이지 전환은 재고에 손대지 않는다.** 그래서 창고를 시뮬이 아니라 러너가 든다 —
        // 시뮬은 판마다 새로 만들어지지만 이 둘은 살아남아 그대로 다음 판으로 넘어간다.
        //
        // 새 세션의 자연 상태는 **빈 창고**다. 스테이지 0이 0에서 시작하는 것은 예외 규칙이
        // 아니라 그 자연 상태이고, 그 뒤로는 물류가 이어 붙인다.
        private AmmoInventory _storeA;
        private AmmoInventory _storeB;
        private MountLoad _mountA;
        private MountLoad _mountB;

        /// <summary>
        /// 창고와 마운트를 버린다 — **촬영용 저장 초기화 전용**(260902_W09 §1-2).
        /// 다음 <c>Begin</c>이 빈 것을 새로 만든다. 정상 플레이에는 이 경로가 없다.
        /// </summary>
        /// <summary>
        /// 지금 살아 있는 적을 모두 없앤다 — **개발 빌드 전용 촬영·리허설 도구**
        /// (2026-09-10 사용자 확정 · 플랜 §67 결함 5).
        ///
        /// **왜 필요한가.** 보스 사망 연출을 확인하려면 보스를 죽여야 하는데, 물류가
        /// 안 돌면 탄이 없어 **영원히 못 죽인다.** 그러면 「사망 벌이 없다」와
        /// 「죽이지 못했다」가 구분되지 않는다 — 실제로 리허설 2차에서 그렇게 읽혔다.
        ///
        /// ⚠️ **승패 판정을 건너뛰지 않는다.** 체력을 0으로 만들 뿐이고, 죽는 것도
        /// 이기는 것도 시뮬이 평소대로 처리한다 — 그래야 사망 연출이 **실제 경로로** 돈다.
        /// </summary>
        public int KillAllEnemies()
        {
            if (_sim == null) return 0;

            int n = 0;
            System.Collections.Generic.IReadOnlyList<CombatEntity> list = _sim.Enemies;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null || list[i].hp <= 0f) continue;
                list[i].hp = 0f;
                n++;
            }
            return n;
        }

        public void ResetCarry()
        {
            _storeA = null;
            _storeB = null;
            _mountA = null;
            _mountB = null;
        }

        private AmmoInventory StoreA(float capacity) =>
            _storeA ?? (_storeA = new AmmoInventory(capacity));

        private AmmoInventory StoreB(float capacity) =>
            _storeB ?? (_storeB = new AmmoInventory(capacity));

        // ⚠️ **마운트도 함께 든다.** 창고만 이으면 전환 때 재고가 사라진다 —
        // 이송에 속도 제한이 없어 창고는 대개 비어 있고 물건은 마운트에 앉아 있기 때문이다.
        private MountLoad MountA(float stack) =>
            _mountA ?? (_mountA = new MountLoad(MountLoad.SlotsRobotA, MountLoad.StandardStacks(stack)));

        // ⚠️ **B 의 드론 스택만 절반이다**(2026-09-18 사용자 확정 · 보고 ⑫⑬) —
        //    8 × 10 = 80 이라 한 판에 만충이 한 번도 안 서던 자리다.
        private MountLoad MountB(float stack) =>
            _mountB ?? (_mountB = new MountLoad(MountLoad.SlotsRobotB,
                MountLoad.StandardStacks(stack, stack * DroneStackFactor)));

        /// <summary>드론 스택 비 — **자산이 원천**이다(`params.mountDroneStackFactor`).</summary>
        private float DroneStackFactor
        {
            get
            {
                BalanceConfig bal = robot != null ? robot.balanceRef : null;
                return bal != null && bal.mountDroneStackFactor > 0f
                    ? bal.mountDroneStackFactor : MountLoad.DroneStackFactorFallback;
            }
        }

        private void Begin()
        {
            // 마운트계수: 물류(S1~S3)=base, 그 외(강화/태그/버스트)=enhanced(1.45).
            _mountCoef = stage.powerModel == StagePowerModel.Logistics
                ? robot.mountCoef : robot.enhancedMountCoef;
            float origin = robot.balanceRef != null ? robot.balanceRef.origin : 100f;
            float ammoCapacity = robot.balanceRef != null ? robot.balanceRef.storeCapacity : 40f;
            // 브릿지 게시 단위 = 물류 단위(마운트계수 미적용). 마운트계수는 판정식 내부 항이라 전투가 곱한다.
            // 초기값(격리 전투 씬). Game.unity는 Provider가 라이브로 덮어쓴다.
            LogisticsOutputBridge.Result = MockLogisticsOutput.Simulate(robot, 1f, robot.moduleMult, origin);
            _output = LogisticsOutputBridge.Output;

            // 발사 배분(§L4-R #4): 물류 생산율(pA) 기반 고효율 우선, 소비 상한 = robot.consumptionCap(capA=6).
            // 명목 출력 = 물류 단위(마운트계수 1) — 라이브 스케일의 분모이므로 Begin에서 1회만 구한다.
            _nominalOutput = RobotOutput.Nominal(robot.weapons, 1f, robot.moduleMult);
            _lastScale = 1f;
            _fireGate.Reset(1f);
            ShotAllocator.AllocateRates(robot.weapons, robot.consumptionCap,
                SupplySignals.ArrivalRateOf, SupplySignals.MountStockOf, _lineBuffer);

            // 로봇 A — 다발형. 화력이 탄약 라인에서 나온다.
            var setup = new RobotSetup
            {
                hp = tuning.robotHp,
                mountCoef = _mountCoef,
                moduleMult = robot.moduleMult,
                attackRange = tuning.robotAttackRangeTbd,
                radius = RobotSize * 0.5f,
                multiShotCount = tuning.multiShotCountTbd,
                aoeRadius = tuning.aoeRadiusTbd,
                aoeSplashFactor = tuning.aoeSplashFactorTbd,
                lines = new List<AmmoLine>(_lineBuffer),
                // 재고는 단일 층(마운트 적재 = 창고 비축). 용량은 확정치 40(balance store).
                // **창고는 러너가 들고 있다** — 스테이지를 넘어도 그대로다(260902_W08 §1).
                ammoCapacity = ammoCapacity,
                ammoStore = StoreA(ammoCapacity),
            };

            List<EnemySpawn> spawns = BuildSpawns();

            // 태그 상대가 있으면 로봇 두 대로 돈다. 마운트는 비대칭(A 4슬롯 / B 8슬롯)이고
            // **스택 상한 10을 넘긴다**(260901_V03 확정). 적재량은 그 파생값이다 —
            // A 4×10 = 40 · B 8×10 = 80.
            //
            // ⚠️ 종전에는 상한을 안 넘겼다. 스택이 미확정이라 발명을 피한 것인데, 그 결과
            // `StackLimitOf`가 전부 0 → `IsFull`이 영영 false → **태그 스킬이 한 번도 발동하지
            // 않았다.** 배선은 있는데 조건이 서지 않는 상태였다.
            float stack = robot.balanceRef != null ? robot.balanceRef.mountStackLimit : 10f;
            setup.mount = MountA(stack); // 단일 로봇 경로는 setup으로 받는다
            _sim = robotB != null
                ? new CombatSimulation(setup, BuildRobotBSetup(),
                    MountA(stack), MountB(stack),
                    spawns, tuning.arenaRadiusTbd, stage.challengeTime, tuning.spawnCadenceTbd)
                : new CombatSimulation(setup, spawns,
                    tuning.arenaRadiusTbd, stage.challengeTime, tuning.spawnCadenceTbd);

            // ⚠️ **웨이브 스폰**(2026-09-18 · 가정 · 설계 판정 자리). 간격 0 이면
            //    `묶음 × spawnCadence` 로 끌어와 **평균 마리/초가 종전과 같다** —
            //    화면의 리젠 타이머는 이 모델이 있어야 셀 것이 생긴다.
            _sim.SetWave(tuning.waveSizeTbd,
                WaveSpawnRule.Interval(tuning.waveIntervalSecondsTbd,
                                       tuning.waveSizeTbd, tuning.spawnCadenceTbd));

            // 로봇 뷰(중앙). 아트가 있으면 그것을, 없으면 색 플레이스홀더로 폴백한다(교체 지점 §8).
            _robotView = NewView("Robot");
            BindRobotView();

            _cutscene.Reset(); // 지난 판의 숫자가 남아 있으면 안 된다
            _ready = true;
        }

        /// <summary>
        /// 로봇 B — 드론 운용기. **본체 무기가 없다**: 화력은 전부 사출한 드론에서 나온다
        /// (RobotDefinition의 weapons가 비어 있는 것이 그 표현이다).
        /// 드론 확정치(슬롯 3 · 방출률 1.0 · 충전량 100)는 BalanceConfig 미러에서 온다.
        /// </summary>
        private RobotSetup BuildRobotBSetup()
        {
            BalanceConfig bal = robotB.balanceRef != null ? robotB.balanceRef : robot.balanceRef;

            return new RobotSetup
            {
                hp = tuning.robotHp,
                mountCoef = stage.powerModel == StagePowerModel.Logistics
                    ? robotB.mountCoef : robotB.enhancedMountCoef,
                moduleMult = robotB.moduleMult,
                attackRange = tuning.robotAttackRangeTbd,
                radius = RobotSize * 0.5f,
                // 드론 2종(누적형·광역형) 구분은 광역 반경이 미확정이라 보류다 —
                // 단일 표적으로 두고, 확정되면 여기서 갈린다.
                multiShotCount = 1,
                aoeRadius = 0f,
                aoeSplashFactor = 0f,
                lines = new List<AmmoLine>(),
                // 탄약 라인이 없으니 창고도 쓰지 않는다. 용량만 남겨 HUD가 0/40을 그린다.
                ammoCapacity = bal != null ? bal.storeCapacity : 40f,
                ammoStore = StoreB(bal != null ? bal.storeCapacity : 40f),
                droneSlots = bal != null ? bal.droneSlots : 3,
                droneReleaseRate = bal != null ? bal.droneReleaseRate : 1f,
                droneCharge = bal != null ? bal.droneCharge : 100f,
                // ⚠️ **기당 피해는 충전량의 몫이다**(2026-09-16 사용자 확정 · 가정 1/10).
                //    총 피해와 수명은 안 바뀐다 — 나눠 쓰는 횟수만 바뀐다.
                //    값 자체를 여기 안 적는다: 충전량도 몫도 SO 에서 온다(§3).
                droneDamagePerHit = (bal != null ? bal.droneCharge : 100f)
                                    * (tuning != null ? tuning.droneDamageFractionTbd : 0.1f),
                droneAttackRange = tuning.robotAttackRangeTbd, // 본체와 동일(C-3 확정)
                // 광역 판정 반경 — **사거리와 다른 칸**이다(2026-09-18 · 260918_W01 3장).
                // ⚠️ 잠정 점값이라 자산이 든다. 0 이면 시뮬이 구 거동(사거리)으로 떨어진다.
                droneAoeJudgeRadius = bal != null ? bal.droneAoeJudgeRadius : 0f,
                // 광역형 표적당 피해 비 — 0 이면 구 거동(둘이 같은 피해)이다.
                droneAoeDamageFactor = bal != null ? bal.droneAoeDamageFactor : 0f,
                mountStackLimit = bal != null ? bal.mountStackLimit : 10f,
            };
        }

        /// <summary>
        /// 뷰를 지금 나가 있는 로봇에 묶는다. 교대하면 **엔티티도 스프라이트도 바뀌므로**
        /// 다시 묶지 않으면 B가 싸우는데 화면에는 A가 서 있게 된다.
        /// </summary>
        /// <summary>
        /// **쏘는 동안 로봇이 쏘는 쪽을 보게 한다** (2026-09-15 사용자 확정 · 육안 4차 ①).
        ///
        /// 얼굴은 종전에 **이동 축**만 따랐다. 자동 조종은 사거리 안에 적이 있으면 제자리에서
        /// 쏘므로, 서 있는 동안 **마지막으로 걸었던 쪽**을 그대로 보고 탄만 옆으로 나갔다.
        ///
        /// ⚠️ **수동이 표적을 이긴다**(구현 가정 · 되돌릴 수 있다 · 설계 사후 판정).
        /// 손으로 몰 때도 표적을 보게 하면 **가는 쪽을 못 본다** — 조종감이 먼저다.
        ///
        /// ⚠️ **탄이 나간 틱에만 방향이 온다.** 사격 간격이 1초면 그 사이 틱에는 사건이 없어서,
        /// 매 틱 새로 읽으면 **얼굴이 깜빡인다.** 그래서 마지막 조준을 잠깐 붙들어 둔다 —
        /// 값이 아니라 **끊김**을 막는 장치다.
        /// </summary>
        private void UpdateRobotFacing(bool manualActive)
        {
            if (_robotView == null || _sim == null) return;

            // 🗑️ **무적 깜빡임 배선 폐기**(2026-09-18 사용자 리허설 ④ — 「버그처럼 보인다」).
            //    같은 날 오전에 넣었다가 저녁에 걷었다. 값 둘은 자산에 폐기 표기로 남겼다.

            // ⚠️ **회피로 밀리는 동안에는 가는 쪽을 본다 — 선 자세로**
            //    (2026-09-18 사용자 확정). 표적도 수동 입력도 이것보다 뒤다:
            //    밀리는 0.167초 동안 얼굴이 표적을 향하면 **옆걸음으로 미끄러지는 것**처럼
            //    보여 「튕겼다」가 안 읽힌다.
            _robotView.ForceIdlePose = _sim.DodgeMotionActive;
            if (_sim.DodgeMotionActive)
            {
                Vector2 pushed = _sim.DodgeMotionDirection;
                if (pushed.sqrMagnitude > 1e-6f)
                {
                    _robotView.FacingOverride = pushed;
                    return;
                }
            }

            if (manualActive)
            {
                _robotView.FacingOverride = null;   // 조종 중에는 가는 쪽을 본다
                return;
            }

            // ⚠️⚠️ **근거는 발사가 아니라 표적이다**(2026-09-15 사용자 확정 · 육안 8차 ①).
            //
            // 종전: `ShotsThisTick` — 쏘 때만 조준이 생겼다. 탄약 0 이면 사거리 안에
            // 적을 두고도 영영 등을 돌렸다(09-15 진단 줄 「마지막 조준 없음」).
            // 지금: 사거리 안 최근접 적을 고른 순간부터 그쪽을 본다 — 발사와 무관.
            Vector2? aim = _sim.AimDirection;
            if (aim.HasValue)
            {
                _lastAim = aim.Value;
                _lastAimAt = Time.time;
            }

            _robotView.FacingOverride =
                Time.time - _lastAimAt <= AimHoldSeconds ? _lastAim : (Vector2?)null;
        }

        /// <summary>
        /// **표적이 사라진 뒤** 마지막 조준을 붙들어 두는 시간(2026-09-15 사용자 확정 · 뜻이 바뀌었다).
        ///
        /// ⚠️ 종전 뜻은 「마지막 **발사** 뒤 유지」였다. 근거가 발사에서 표적으로 옮겨졌으므로
        /// 이 값이 세는 것도 바뀐다 — 사거리 안에 적이 있는 동안에는 계속 갱신되고,
        /// 적이 죽거나 사거리를 벗어난 **뒤부터** 이 시간이 흐른다.
        /// 그래야 적 하나를 죽인 직후 얼굴이 홱 돌아가지 않는다.
        /// </summary>
        private const float AimHoldSeconds = 1.5f;

        private Vector2 _lastAim;
        private float _lastAimAt = -999f;

        private void BindRobotView()
        {
            if (_robotView == null || _sim == null) return;

            RobotDefinition def = ActiveRobotDef;
            _robotView.Bind(_sim.Robot, _sim.ActiveRobotIndex == 1 ? RobotBColor : RobotAColor,
                RobotSize, SortingLayers.Actor, def != null ? def.sprite : null,
                def != null ? def.animClips : null);

            _viewedRobotIndex = _sim.ActiveRobotIndex;
            _viewedMerged = IsMerged;
            _lastRobotHp = _sim.Robot.hp; // 교대 프레임을 피격으로 오인해 점멸하지 않게 한다
        }

        /// <summary>
        /// 태그 진입 — 새 로봇이 화면 우측 밖에서 자리로 들어온다(`260907_W01` 2-3 · 사용자 확정).
        ///
        /// <b>길이를 태그 클립의 실제 초와 같게 둔다</b> — 클립이 이동보다 먼저 끝나면
        /// 마지막 프레임으로 굳은 채 미끄러져 들어온다(W01 확인 1). <b>가정이며 되돌릴 수 있다.</b>
        /// 0.75초 자체가 잠정이다 — 지속 시간의 소관은 전투 시스템 문서「태그 규칙」이고
        /// 15-1 5-1이 그쪽으로 넘겨 두었다(W01 9장 2).
        /// </summary>
        /// <summary>
        /// 태그 진입 이동. <b>클립보다 꼬리 칸만큼 일찍 끝난다</b>(`260907_W02` 2-3 사용자 확정) —
        /// 도착 뒤에 남은 칸에서 반동과 수렴이 제자리에서 보인다. 확정된 것은 값이 아니라
        /// 「발사보다 뒤 · 클립보다 먼저」라는 순서이고, 꼬리 칸 수는 <see cref="CombatTuning"/>에 있다.
        ///
        /// ⚠️ <b>「발사보다 뒤」는 새 태그 벌을 전제한다</b> — W02 2-4가 「첫 프레임부터 발사 자세」로
        /// 다시 뽑으라 했으므로 첫 칸이 발사이고 조건이 저절로 선다. 옛 로봇 A 태그 벌은 발사가
        /// 뒤쪽 칸이라 이 전제가 서지 않는데, 그 벌은 2-4로 교체된다.
        /// </summary>
        private void PlayTagEntrance()
        {
            if (_robotView == null) return;
            float clip = tuning != null ? tuning.animTagInSeconds : 0.75f;
            // 꼬리 칸은 **지금 들어오는 로봇**의 값이다(2026-09-09 · 캐릭터 15 7-7).
            // 튜닝의 값은 로봇이 안 정했을 때의 폴백으로만 남는다.
            int trail = tuning != null ? tuning.animTagEntryTrailCells : 3;
            RobotDefinition entering = ActiveRobotDef;
            if (entering != null) trail = entering.TrailCellsOr(trail);
            float cell = tuning != null ? tuning.animCellSeconds : 1f / AnimSchedule.CellsPerSecond;
            _robotView.PlayTagIn(AnimSchedule.TagEntrySeconds(clip, trail, cell), TagEntryOffsetX);
        }

        /// <summary>
        /// 카메라가 지금 비추는 사각형을 재서 시뮬에 넣는다 — **태그 스킬 광역이 여기 든 적만 친다**
        /// (2026-09-08 · <c>260908_W05</c> 2-2).
        ///
        /// ⚠️ **값을 짓지 않는다.** 창 크기와 비율이 바뀌면 사각형도 바뀌므로 틱마다 다시 잰다.
        /// 직교 카메라의 <c>orthographicSize</c>는 **세로 절반**이라 가로는 비율을 곱한다.
        /// 카메라가 없으면 넣지 않는다 — 그때 시뮬은 살아 있는 적 전부를 친다.
        /// </summary>
        private void PushVisibleBounds()
        {
            if (_sim == null) return;
            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic) return;

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 c = cam.transform.position;
            _sim.SetVisibleBounds(new Rect(c.x - halfW, c.y - halfH, halfW * 2f, halfH * 2f));
        }

        /// <summary>
        /// 태그 스킬 연출을 건다 — **들어오는 로봇이 누구냐로 갈린다**
        /// (2026-09-08 · <c>260908_W04</c> 2-2·2-3).
        ///
        /// **B 태그 인 = 레이저 한 줄기**가 맵을 한 바퀴 · **A 태그 인 = 120도 부채꼴 탄환비**.
        /// 값 아홉은 전부 <see cref="CombatTuning"/>에 있다 — 여기서 숫자를 만들지 않는다.
        ///
        /// ⚠️ **탄환 스프라이트는 아직 없다.** <c>vfx_tagbullet</c>이 생성되기 전까지
        /// 자리표시(흰 사각)로 그린다 — 아트가 내면 그 자리만 바꾼다.
        ///
        /// ⚠️ **피해와 무관하다.** 연출이 있든 없든 판정은 시뮬이 이미 끝냈다(전투 문서 10-1).
        /// </summary>
        /// <summary>
        /// **개발 빌드에서만** 태그 스킬의 수를 화면에 적는다 (2026-09-18 사용자 리허설 ⑫).
        ///
        /// 📌 나눔이 실제로 걸리는지를 **사람이 화면에서** 보려면 수가 보여야 한다 —
        /// 지금까지는 하네스 로그에만 있었다.
        ///
        /// ⚠️ **배포 빌드에는 안 나온다**(`Debug.isDebugBuild`) — 심사자가 보는 화면에
        /// 진단 글자가 뜨면 그것이 게임의 일부로 읽힌다.
        /// </summary>
        private void NoteTagSkillNumbers()
        {
            if (!Debug.isDebugBuild) return;
            if (_sim.LastTagSkillDamage <= 0f) return;

            int n = Mathf.Max(1, _sim.LastTagSkillTargetCount);
            _tagSkillNote = $"태그 스킬 — 표적 {n} · 총 피해 {_sim.LastTagSkillDamage:F0}"
                            + $" · 한 체 몫 {_sim.LastTagSkillDamage / n:F0}";
            _tagSkillNoteUntil = Time.time + TagSkillNoteSeconds;
        }

        /// <summary>진단 줄이 화면에 머무는 시간(초). ⚠️ 가정 — 개발 빌드 전용이라 값이 아니다.</summary>
        private const float TagSkillNoteSeconds = 3f;

        private string _tagSkillNote;
        private float _tagSkillNoteUntil;

        private void PlayTagSkillEffect()
        {
            if (_sim == null) return;
            // 마운트가 만재가 아니어서 스킬이 안 터졌으면 연출도 없다 — 사건 종속(10-1).
            if (_sim.LastTagSkillDamage <= 0f) return;

            bool bEntering = _sim.ActiveRobotIndex == 1;
            Color c = bEntering ? RobotBColor : RobotAColor;

            // ⚠️⚠️ **화면 사각형을 그대로 받는다** (2026-09-18 사용자 육안 · 「소용돌이 말고
            //    화면 전체」). 종전에는 로봇 자리에서 길이를 재어 **원점에서 뻗는 그림**을
            //    그렸는데, 줄기가 몇이든 원점이 있으면 **소용돌이로 읽힌다.**
            //    화면은 여기서 짓지 않는다 — 카메라에서 재고, 판정이 쓰는 범위와 같은 사각형이다.
            Rect screen = VisibleRect();

            if (bEntering)
            {
                // 섬광은 자산이 없다 — 흰 사각을 화면 크기로 늘여 그리는 것이 곧 완성형이다.
                TagSkillEffect.PlayFlash(transform, screen, tuning, PlaceholderSprite.White(), c);
            }
            else
            {
                // 탄환은 아트 자산이 있다. 아직 안 들어왔으면 자리표시로 폴백한다.
                Sprite bullet = tuning != null && tuning.tagBulletSprite != null
                    ? tuning.tagBulletSprite : PlaceholderSprite.White();
                bool real = bullet != PlaceholderSprite.White();
                // 자산에는 색이 이미 실려 있다 — 흰 사각일 때만 색을 입힌다.
                Color tint = real ? Color.white : c;
                TagSkillEffect.PlayBulletRain(transform, screen, tuning, bullet, tint, real);
            }
        }

        /// <summary>
        /// 지금 화면이 덮는 사각형(월드). **판정이 쓰는 것과 같은 수**다 —
        /// 러너가 매 틱 <c>SetVisibleBounds</c> 로 넣어 주는 그 사각형을 여기서도 만든다.
        ///
        /// ⚠️ 카메라가 없거나 원근이면 옛 값(투기장 반경)으로 떨어진다 — 수를 지어내지 않는다.
        /// </summary>
        private Rect VisibleRect()
        {
            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic)
            {
                float r = tuning != null ? tuning.arenaRadiusTbd : 6f;
                return new Rect(-r, -r, r * 2f, r * 2f);
            }

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 c = cam.transform.position;
            return new Rect(c.x - halfW, c.y - halfH, halfW * 2f, halfH * 2f);
        }

        // 🗑️ **`TagSkillAimDegrees` 폐기**(2026-09-18 사용자 육안) — 연출이 화면 전체를
        //    덮으면서 「어느 쪽으로 나가는가」를 재는 자리가 없어졌다. 부르는 곳이 0건이라
        //    남겨 두면 다음 사람이 「겨냥이 있다」고 읽는다.

        /// <summary>마지막으로 본 회피 횟수 — 늘어난 프레임이 곧 회피 발동 순간이다.</summary>
        private int _seenDodges;

        /// <summary>탄약 소진 표시 하나. 지속 상태라 매번 만들지 않고 껐다 켠다.</summary>
        private SpriteRenderer _ammoOutView;

        /// <summary>
        /// 설치된 VFX 배선 셋 — 드론 사출 · 회피 · 드론 소멸 (2026-09-08 · <c>260908_W06</c> 6장).
        ///
        /// **사건 자리는 전부 시뮬에 이미 있었다.** 여기서 만드는 것은 그림뿐이고
        /// 판정은 건드리지 않는다(전투 시스템 문서 10-1 「연출은 새로운 사건을 만들지 않는다」).
        ///
        /// ⚠️ **`vfx_ammoout`은 빠져 있다.** 「공급이 끊겨 공격이 멈췄다」는 **상태**이고
        /// 연출 문서가 「공급이 돌아올 때까지 점멸한다」로 적었는데, 코드에는 그 상태가 없다 —
        /// 있는 것은 「이번 한 발이 안 나갔다」뿐이다. **없는 사건을 지어 넣지 않는다.**
        ///
        /// ⚠️ 자산이 안 들어와 있으면 **아무것도 안 그린다.** 자리표시(흰 사각)로 대신하지 않는다 —
        /// 이 셋은 「무엇인지」가 그림에만 있어 흰 사각으로는 뜻이 안 선다.
        /// </summary>
        private void PlayInstalledVfx()
        {
            if (_sim == null || tuning == null) return;

            float life = Mathf.Max(0.02f, tuning.vfxOneShotSeconds);

            // ⚠️ **소리는 그림과 따로 건다.** 그림이 없어도 사출은 일어났고, 사운드 문서 1장이
            // 「화면을 안 볼 때도 닿는 통로」로 규정했다 — 그림 유무에 소리를 매달면 그 규정이 깨진다.
            for (int i = 0; i < _sim.DroneLaunchesThisTick.Count; i++)
                AudioSignals.Play(SoundIds.DroneLaunch, SoundIds.KindOf(SoundIds.DroneLaunch));

            // ⚠️ **사출 이펙트는 절반 크기다**(2026-09-16 사용자 육안 4차 ③ · 가정 0.5 · SO).
            //    드론(64px)보다 이펙트가 커서 기체가 그림에 묻혔다.
            if (tuning.droneLaunchSprite != null)
                foreach (Vector2 p in _sim.DroneLaunchesThisTick)
                    SpawnOneShot(tuning.droneLaunchSprite, p, life, tuning.droneLaunchScaleTbd);

            if (tuning.droneExpireSprite != null)
                foreach (Vector2 p in _sim.DroneExpiriesThisTick)
                    SpawnOneShot(tuning.droneExpireSprite, p, life);

            // 회피는 **시작 순간에만 1회**다(연출 4장). 시뮬이 세는 누적 횟수가 늘어난
            // 프레임이 그 순간이며, 그 값 하나로 자동·수동을 가리지 않는다 — 둘 다 회피다.
            DodgeSystem dodge = _sim.Dodge;
            if (dodge == null) { _seenDodges = 0; return; }

            // ⚠️ **분사는 제 지속을 쓴다**(2026-09-17 · `260917_W06` 3장) — 다른 한 방 그림과
            //    같은 `life` 를 쓰면 **무적만큼도 안 남아** 발동해도 안 보인다.
            //    사용자 보고 「부스터가 발생하지 않음」의 정체가 그 자리였다.
            if (dodge.TotalDodges > _seenDodges && _sim.Robot != null)
            {
                // ③ 끝점 한 방 — **그림 자산이 있는 유일한 회피 연출**이라 그대로 둔다.
                // ⚠️ **알파가 빠지며 사라진다**(2026-09-18 사용자 육안 ② — 「부스트도 동일하게」).
                //    잔상·줄기가 옅어져 걷히는데 분사만 툭 없어지면 **둘이 따로 논다.**
                if (tuning.boosterSprite != null)
                    SpawnOneShot(tuning.boosterSprite, _sim.Robot.position,
                        Mathf.Max(life, tuning.dodgeVfxSeconds), fade: true);

                // ①② **잔상과 줄기**(2026-09-18 사용자 확정 · 참고 이미지).
                //
                // ⚠️⚠️ **시작점을 여기서 잰다.** 회피는 이미 끝난 뒤에 이 줄을 지나므로
                //    지금 자리는 **끝점**이다 — 시작점은 거기서 **회피 방향으로 거리만큼
                //    되짚어** 낸다. 값을 지어내지 않고 `DodgeMotion` 의 거리를 그대로 쓴다.
                //
                // ⚠️ 방향은 **회피 방향**이다(사용자 지시) — 바라보는 쪽이 아니다.
                Vector2 to = _sim.Robot.position;
                Vector2 dir = _sim.DodgeMotionDirection;
                BalanceConfig dodgeBal = robot != null ? robot.balanceRef : null;
                float dist = dodgeBal != null && dodgeBal.dodgeMoveDistance > 0f
                    ? dodgeBal.dodgeMoveDistance : DodgeMotion.DefaultDistance;
                Vector2 from = dir.sqrMagnitude > 0f ? to - dir.normalized * dist : to;

                DodgeTrail.Play(transform, from, to,
                    _robotView != null ? _robotView.BodySprite : null,
                    _robotView != null && _robotView.BodyFlipX,
                    _robotView != null ? _robotView.BodyScale : Vector3.one,
                    tuning);
            }

            _seenDodges = dodge.TotalDodges;
        }

        /// <summary>
        /// 로봇 머리 위 **쉴드 막대**에 비율을 건다 — HP 바 바로 위 한 칸이다.
        /// 최대치가 0 이면 음수를 줘 **막대를 지운다**(쉴드 줄이 없는 판).
        /// </summary>
        private void SyncShieldBar()
        {
            if (_robotView == null || _sim == null) return;

            float max = _sim.ShieldMax, now = _sim.Shield.Value;
            if (IsMerged && _sim.HasTagPartner)
            {
                max += _sim.StandbyShieldMax;
                now += _sim.StandbyShield.Value;
            }

            _robotView.SetShieldRatio(max > 0f ? now / max : -1f);
        }

        /// <summary>
        /// 탄약 소진 표시(`vfx_ammoout`) — **마운트가 통째로 빈 동안** 계속 떠 있는다.
        ///
        /// ⚠️ **이 조건은 가정이다**(2026-09-09 · `❓`7-1 (가)). 연출 문서가 이 자산을
        /// 「공급이 돌아올 때까지」의 **지속 상태**로 적었는데, 코드에는 「이번 한 발이 안 나갔다」는
        /// 순간 사실밖에 없었다. 셋 중 (가)를 골랐다 — 한 탄종만 빈 것은 다른 탄종이 계속
        /// 나가므로 「소진」이 아니고, 창고까지 비었는지는 화면에 보이는 것과 한 단계 떨어져 있다.
        /// 설계가 (나)·(다)로 답하면 **이 한 줄만 바뀐다** — 뜨는 조건이고 판정식에 안 닿는다.
        ///
        /// 다른 넷과 달리 <see cref="SpawnOneShot"/>을 안 쓴다. 0.2초짜리를 매 프레임 새로
        /// 만들면 같은 그림이 깜빡이며 쌓이고, 무엇보다 **지속 상태가 순간 연출로 바뀐다.**
        /// 하나를 만들어 두고 껐다 켠다.
        /// </summary>
        private void UpdateAmmoOutView()
        {
            if (_sim == null || tuning == null || tuning.ammoOutSprite == null) return;

            MountLoad mount = _sim.ActiveMount;
            bool empty = _sim.Result == CombatResult.InProgress && mount != null && mount.Total <= 0f;

            if (empty && _ammoOutView == null)
            {
                var go = new GameObject("VfxAmmoOut");
                go.transform.SetParent(transform, false);
                _ammoOutView = go.AddComponent<SpriteRenderer>();
                _ammoOutView.sprite = tuning.ammoOutSprite;
                _ammoOutView.sortingOrder = SortingLayers.EffectOver;
            }
            if (_ammoOutView == null) return;

            _ammoOutView.enabled = empty;
            if (empty && _sim.Robot != null)
            {
                // **캐릭터 하단** — 바닥 그림자와 같은 발밑이다(`260909_W01` 3-1 · UI 문서 12-1).
                // 종전에는 로봇 한가운데였고, 그러면 몸통을 덮어 「무엇이 멈췄는지」보다
                // 「무언가 가려졌다」가 먼저 읽힌다.
                //
                // ⚠️ **크기를 줄인다**(2026-09-10 사용자 확정 · 촬영 전 임시).
                // 이 그림은 256 캔버스에 실루엣 212px 이라 **로봇(220px)만 하다.** 그대로 두면
                // 「무엇이 멈췄는지」보다 「무언가 가려졌다」가 먼저 읽힌다.
                // 배율은 조율 SO 의 **가정치**이고 코어가 상한(절반 이하)으로 자른다.
                float scale = EffectTiming.AmmoOutScale(tuning.ammoOutScaleAssumed);
                _ammoOutView.transform.localScale = new Vector3(scale, scale, 1f);

                // ⚠️ **그림자 발밑보다 더 내려간다.** `ShadowFootOffset` 자리는 아직 실루엣 안이라
                // 거기 두면 아이콘 위쪽이 다리를 덮는다 — 여기서는 몸통 밖으로 통째로 내린다.
                //
                // ⚠️ **그림자보다 위다.** 이 뷰는 `EffectOver`(10)이고 그림자는 `EffectUnder`(-10)이라
                // 층이 이미 갈려 있다 — 새 상수를 만들지 않는다.
                float foot = EffectTiming.AmmoOutFootOffset(RobotSize, RobotSize * scale);
                _ammoOutView.transform.position =
                    new Vector3(_sim.Robot.position.x, _sim.Robot.position.y + foot, 0f);
            }
        }

        /// <summary>
        /// 조립 화면이 볼 값을 코어에 놓는다 (UI 문서 12장 · `260909_W01` 3-1).
        ///
        /// **여기서 판정하지 않는다** — 0인지 아닌지는 <see cref="SupplyStopRules"/>가 가른다.
        /// 다리에서 나누면 두 화면이 서로 다른 기준을 갖게 된다.
        /// </summary>
        private void PublishSupplySignals()
        {
            // ⚠️ **카메라가 비출 자리를 같이 낸다**(2026-09-15 사용자 확정 · 육안 ⑥ · UI 9-5).
            // 레이어 컨트롤러가 이것을 읽어 주 카메라와 인셋을 함께 옮긴다.
            GameViewSignals.HasCombatFocus = _sim != null && _sim.Robot != null;
            if (GameViewSignals.HasCombatFocus) GameViewSignals.CombatFocus = _sim.Robot.position;

            if (_sim == null) { SupplySignals.Reset(); return; }

            SupplySignals.HasCombat = true;
            MountOwner active =
                _sim.ActiveRobotIndex == 1 ? MountOwner.RobotB : MountOwner.RobotA;
            SupplySignals.ActiveOwner = active;

            // ⚠️⚠️ **대기 로봇 것도 같이 싣는다**(2026-09-16 사용자 확정 · 플랜 §74-16 ③).
            //
            // 종전에는 **나선 로봇 하나**만 실었다. 그러면 「지금 교대하면 쏠 것이 있는가」를
            // **물어볼 자리 자체가 없다** — 전투 HUD 의 대기 로봇 적재 표시도, 조립 화면의
            // B 마운트 칸도 그 값을 필요로 한다.
            //
            // 📌 `Tag` 가 둘을 다 들고 있다 — 여기서 새로 짓지 않는다(지침 §7).
            //    태그가 없는 판(합체 등)이면 나선 쪽 하나만 실린다.
            PublishMount(active, _sim.ActiveMount, _sim.AmmoStock);

            MountOwner standby = active == MountOwner.RobotB ? MountOwner.RobotA : MountOwner.RobotB;
            MountLoad standbyMount = _sim.Tag != null ? _sim.Tag.StandbyMount : null;
            // ⚠️ 창고는 **나선 쪽 것만 잰다** — 대기 로봇의 창고 재고를 시뮬이 안 들고 있다.
            //    0 을 지어 넣지 않고 **건드리지 않는다**(옛 값이 남는 편이 거짓말보다 낫다).
            if (standbyMount != null) PublishMount(standby, standbyMount, null);
        }

        /// <summary>
        /// 한 로봇의 마운트 상태를 신호에 싣는다 (2026-09-16 · 로봇별로 갈리며 떼어냈다).
        ///
        /// ⚠️ **슬롯 상태도 함께 넘긴다**(2026-09-14 · §71-41 · UI 문서 12-4).
        /// 합만 넘기면 조립 화면은 「얼마나 찼나」까지만 말할 수 있고
        /// **무엇이 몇 칸에 있나**는 못 그린다.
        /// </summary>
        /// <param name="stock">창고 재고. <c>null</c> 이면 **안 건드린다**(모르는 값이다).</param>
        private static void PublishMount(MountOwner owner, MountLoad mount, float? stock)
        {
            SupplySignals.SetMountTotal(owner, mount != null ? mount.Total : 0f);
            if (stock.HasValue) SupplySignals.SetStorageStock(owner, stock.Value);

            int slots = mount != null ? mount.SlotCount : 0;
            SupplySignals.EnsureSlots(owner, slots);
            MountItem[] items = SupplySignals.MountSlotItemOf(owner);
            float[] amounts = SupplySignals.MountSlotAmountOf(owner);
            for (int i = 0; i < slots; i++)
            {
                items[i] = mount.ItemAt(i);
                amounts[i] = mount.AmountAt(i);
            }

            // 분모는 **품목과 무관하게 같다**(표준 스택 10) — `StandardStacks` 가 그렇게 짓는다.
            SupplySignals.SetMountStackLimit(owner,
                mount != null ? mount.StackLimitOf(MountItem.Standard) : 0f);
        }

        /// <summary>한 번 그려지고 사라지는 이펙트 한 장. 반복 없음(연출 2장 「공통 생성 규칙」).</summary>
        /// <summary>
        /// 날아오는 적 포탄을 그린다 (2026-09-11 · §71-33 ②).
        ///
        /// ⚠️ **한 발짜리 오브젝트를 매 프레임 만들지 않는다** — 투사체는 사건이 아니라
        /// **상태**라 사는 내내 프레임마다 새로 만들면 쓰레기가 쌓인다.
        /// 목록 길이에 맞춰 **늘려 두고 껐다 켠다.**
        ///
        /// ⚠️ **자리표시 흰 사각이다** — 적 포탄 그림은 아직 없다. 태그 탄환(`vfx_tagbullet`)을
        /// 빌려 쓰지 않는다: 그건 **로봇이 쏘는 것**이라, 같은 그림이면 화면에서
        /// 「내가 쏜 것」과 「나에게 오는 것」이 구분되지 않는다.
        /// </summary>
        /// <summary>
        /// 적 포탄 그림 — **이 스테이지가 쓰는 것 하나** (2026-09-16 · 자리 준비).
        ///
        /// ⚠️⚠️ **시뮬의 포탄은 누가 쐈는지를 안 들고 있다.** 그래서 병종별로 못 고른다 —
        /// 지금은 **포격 계열 중 그림이 걸린 첫 정의**의 것을 쓴다(⚠️ 가정).
        /// 병종마다 다른 포탄이 필요해지면 그때 `EnemyProjectile` 이 정체를 들어야 하고,
        /// **그것은 이 한 줄보다 큰 일**이라 값이 설 때 하기로 미룬다.
        ///
        /// 📌 아트 요청은 **무채색 한 장**이다 — 색은 코드가 틴트한다.
        /// </summary>
        private Sprite EnemyProjectileSprite()
        {
            if (enemyCatalog == null) return null;
            foreach (EnemyDefinition d in enemyCatalog)
                if (d != null && d.projectileSprite != null) return d.projectileSprite;
            return null;
        }

        private void SyncEnemyProjectileViews()
        {
            var live = _sim.EnemyProjectiles;

            while (_projectileViews.Count < live.Count)
            {
                var go = new GameObject($"EnemyProjectile{_projectileViews.Count}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                // ⚠️ **자산이 있으면 그것을 쓴다**(2026-09-16 · 사용자 육안 「흰 사각」).
                //    없으면 종전대로 흰 사각으로 떨어진다 — 안 보이는 것보다 낫다.
                Sprite shell = EnemyProjectileSprite();
                sr.sprite = shell != null ? shell : PlaceholderSprite.White();
                sr.sortingOrder = SortingLayers.EffectOver;
                // ⚠️ **자산은 무채색이다 — 색은 코드가 입힌다**(2026-09-16 아트 규약).
                //    자리표시(흰 사각)일 때도 같은 색이라 **틴트를 갈래로 안 나눈다.**
                sr.color = EnemyShellTint;
                // ⚠️ **크기는 가정**이고 값은 자산이 든다(`enemyProjectileViewUnitsTbd`).
                //    연출 문서에 적 포탄 절이 없어 확정이 아니다 — 2026-09-18 에 사용자가
                //    「안 보인다」로 키웠다(0.25 → 0.5).
                go.transform.localScale = Vector3.one * ProjectileViewUnits;
                _projectileViews.Add(sr);
            }

            for (int i = 0; i < _projectileViews.Count; i++)
            {
                SpriteRenderer sr = _projectileViews[i];
                bool on = i < live.Count;
                if (sr.gameObject.activeSelf != on) sr.gameObject.SetActive(on);
                if (!on) continue;

                sr.transform.position = live[i].position;
                // ⚠️ **자산은 오른쪽을 본다**(아트 규약) — 날아가는 쪽으로 돌린다.
                //    방향이 없으면(속도 0) 돌리지 않는다 — 0 으로 나누지 않는다.
                Vector2 dir = live[i].direction;
                if (dir.sqrMagnitude > 1e-6f)
                    sr.transform.rotation = Quaternion.Euler(0f, 0f,
                        Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }
        }

        /// <param name="fade">
        /// 참이면 **알파가 빠지며** 사라진다(2026-09-18 사용자 육안 ② — 「부스트도 동일하게」).
        ///
        /// ⚠️ 거짓이면 종전대로 **시간이 되면 툭 없어진다.** 한 방 그림 대부분은 제 안에
        /// 사라짐이 그려져 있어 밖에서 또 옅게 하면 두 번 사라진다 — 그래서 **기본은 거짓**이고
        /// 부르는 쪽이 고른다.
        ///
        /// ⚠️ 옅게 하는 일은 <see cref="FadeOutAndDestroy"/> 가 든다 — **같은 일을 하는 자리를
        /// 또 만들지 않는다**(태그 퇴장에 쓰려고 지어 두고 부르는 곳이 없던 것을 여기서 쓴다).
        /// </param>
        private void SpawnOneShot(Sprite sprite, Vector2 position, float seconds,
                                  float scale = 1f, bool fade = false)
        {
            var go = new GameObject("Vfx");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(position.x, position.y, 0f);
            // ⚠️ 1 이면 **자산 제 크기**다 — 배율을 지어내지 않는다(2026-09-16).
            if (!Mathf.Approximately(scale, 1f) && scale > 0f)
                go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = SortingLayers.EffectOver;

            if (fade) go.AddComponent<FadeOutAndDestroy>().Begin(sr, seconds);
            else Destroy(go, seconds);
        }

        /// <summary>
        /// 라이브 물류 → 발사율(§5-6 D2). 코어 명제가 코드에서 성립하는 지점이다:
        /// 보드에서 노드를 빼면 브릿지 출력이 떨어지고, 그만큼 발사율이 줄어 전투가 실제로 약해진다.
        ///
        /// 배율이 의미 있게 변했을 때만 재배분한다(매 프레임 재할당은 낭비).
        /// 전투를 재시작하지 않고 라인만 갈아끼운다 — 연속성 원칙(조립 중에도 전투는 안 멈춘다).
        /// </summary>
        private void RefreshFireRate()
        {
            if (_nominalOutput <= 0f) return;

            float scale = LogisticsOutputBridge.Output / _nominalOutput;
            if (scale < 0f) scale = 0f;

            // ⚠️ **게이트가 배분식과 따로 놀고 있었다**(2026-09-15 실측 · `FireDeadlockProbe`).
            // 판정은 `FireRateGate` 가 한다 — 거기 주석에 원인과 실측이 있다.
            if (!_fireGate.ShouldReallocate(scale, SupplySignals.ArrivalRateOf,
                    SupplySignals.MountStockOf, ScaleEpsilon)) return;

            _lastScale = scale;

            // ⚠️ **재고가 있으면 스펙대로, 비면 공급이 상한**(2026-09-15 사용자 확정 · (가)).
            ShotAllocator.AllocateRates(robot.weapons, robot.consumptionCap,
                SupplySignals.ArrivalRateOf, SupplySignals.MountStockOf, _lineBuffer);
            _sim.SetFireLines(_lineBuffer);
            _output = LogisticsOutputBridge.Output;
        }

        /// <summary>
        /// 앞의 것이 0 이 아니면 그것을 쓴다. **0 은 「안 정했다」는 뜻**이지 0 이라는 값이 아니다.
        /// </summary>
        /// <summary>
        /// 적 포탄의 한 변(월드 유닛). ⚠️ 가정 — 연출 문서에 절이 없다.
        /// 🗑️ 구 `const 0.25f` 폐기(2026-09-18) — **값이 코드에 살면 손댈 때마다 코드를 고친다.**
        /// 자산 칸을 못 읽는 판을 위한 기본값만 여기 남는다.
        /// </summary>
        private float ProjectileViewUnits =>
            tuning != null && tuning.enemyProjectileViewUnitsTbd > 0f
                ? tuning.enemyProjectileViewUnitsTbd : 0.5f;

        /// <summary>
        /// 적 포탄 색 — ⚠️ **가정**(연출 문서에 적 포탄 절이 없다 · 2026-09-16).
        /// 자산이 무채색으로 들어와 코드가 입힌다. 적 쪽 색 축(주황 계열)을 따른다.
        /// </summary>
        private static readonly Color EnemyShellTint = new Color(1f, 0.72f, 0.38f, 1f);

        private readonly List<SpriteRenderer> _projectileViews = new List<SpriteRenderer>();

        /// <summary>
        /// 이 스테이지의 적 목록. **짓는 규칙은 <see cref="StageSpawnFactory"/> 한 곳에 있다**
        /// (2026-09-16 · S1 하네스가 같은 판을 재려면 같은 함수를 불러야 한다).
        ///
        /// ⚠️ 여기서는 **화면의 값만** 받아 둔다 — 배율과 그림은 시뮬에 안 들어간다.
        /// </summary>
        private List<EnemySpawn> BuildSpawns()
            => StageSpawnFactory.Build(stage, enemyCatalog, tuning, (label, def) =>
            {
                // 배율은 없거나 0 이면 1 로 본다 — 낡은 자산이 적을 통째로 사라지게 하면 안 된다.
                _viewScaleByLabel[label] = def != null && def.viewScale > 0 ? def.viewScale : 1;
                _artByLabel[label] = def;
            });

        private void Update()
        {
            // 메뉴가 닫히는 프레임에 시작한다. 그 전에는 시뮬이 없으므로 아래로 안 내려간다.
            if (!_ready && !TryBeginWhenAllowed()) return;
            if (!_ready) return;

            // ⚠️⚠️ **메뉴가 떠 있는 동안에는 전투를 세운다**(2026-09-18 사용자 확정 ·
            //    「메인 메뉴로」). 종전에는 메뉴를 다시 열어도 **뒤에서 판이 계속 돌았다** —
            //    화면은 메뉴인데 적은 다가오고 HP 는 깎인다. 메뉴를 보고 돌아왔더니
            //    져 있는 일이 나는 자리다.
            //
            // ⚠️ **시작 깃발은 안 내린다** — 되돌아온 메뉴는 「다시 시작」이 아니라
            //    「보고 있는 중」이다(`MainMenuGate.TryStart` 주석).
            // ✅ **설정 패널도 세운다**(2026-09-21 사용자 확정 ③).
            //    🗑️ 구 규칙 「설정은 얹기만 한다 — 안 세운다」 폐기. 09-18 에 그렇게 정한
            //    까닭은 **메뉴 빗장을 쓰면 HUD 가 통째로 사라졌기 때문**인데, 그것은
            //    `OnGUI` 억제의 문제였지 **시간**의 문제가 아니었다. 그리기는 그대로 두고
            //    **판만 세운다** — 아래 `OnGUI` 는 `SettingsGate` 를 안 본다.
            //
            //    ⚠️ 세우는 것은 **판**이고 시작 깃발이 아니다 — 닫으면 이어서 돈다.
            if (MainMenuGate.IsOpen || SettingsGate.IsOpen) return;

            // 결과가 나도 끝까지 튼다 — 버스트로 마지막 적이 죽으면 연출이 그 프레임에 끊긴다.
            _cutscene.Tick(Time.deltaTime);

            bool running = _sim.Result == CombatResult.InProgress;

            if (running)
            {
                RefreshFireRate();
                // 창고 유입 = 라이브 군수 생산율. 재고가 마르면 발사가 멈춘다(탄약 소진 = 공격 정지).
                _sim.AmmoSupplyRate = LogisticsOutputBridge.AmmoProduce;
                // 드론 몸체·추진제도 같은 보드에서 온다. 사출대·부스터가 각각 받아 화력과 생존이 된다.
                // 🗑️ **구 길 폐기** — 기초 가공소 **생산량**으로 마운트를 채우던 자리.
                //    문서는 「고정 포트에 도착한 것은 곧바로 마운트 적재」다(조립 7-3-1).
                //    값 자체는 남긴다 — 「보드가 무엇을 만들었나」를 그리는 데 쓴다.
                _sim.DroneInflowRate = LogisticsOutputBridge.DroneProduce;
                // **도착한 것이 곧 적재다** — 종별로 쌓는다(2026-09-16).
                _sim.StackDroneArrivalRate = LogisticsOutputBridge.StackDroneArrivalRate;
                _sim.AoeDroneArrivalRate = LogisticsOutputBridge.AoeDroneArrivalRate;
                _sim.PropellantSupplyRate = LogisticsOutputBridge.PropellantProduce;
                // 회피 스택 상한은 부스터 대수의 파생값이다 — 노드를 뽑으면 그 자리에서 줄어든다.
                _sim.BoosterCount = LogisticsOutputBridge.BoosterCount;

                // ✅ **두 번째 보드가 생겨 그 줄이 붙었다** (2026-09-17 · `260917_W03` 7-2 #1).
                //    🗑️ 구 주석 「대기 로봇의 유입은 주입하지 않는다 — 보드가 한 장뿐이라
                //    같은 값을 양쪽에 넣으면 한 장이 두 배를 생산한다」는 폐기.
                //    보드는 09-16 에 로봇마다 하나가 됐고, 대기 보드의 값은 **제 판에서** 온다.
                //    ⚠️ 이것이 없으면 합체 중 「두 보드의 회피 스택을 모두 쓴다」가
                //    대기 그릇 0 칸이라 화면에서 한 번도 성립하지 않는다.
                _sim.StandbyBoosterCount = LogisticsOutputBridge.StandbyBoosterCount;
                _sim.StandbyPropellantSupplyRate = LogisticsOutputBridge.StandbyPropellantProduce;

                // ⚠️⚠️ **대기 보드의 드론 도착**(2026-09-18 사용자 보고 ⑪). 이 두 줄이 없어
                //    A 로 싸우는 동안 B 의 마운트가 영영 비어 있었다 — **만충이 안 서니
                //    태그 스킬도 안 나갔다.** 추진제·부스터가 09-17 에 얻은 길을 드론도 얻는다.
                _sim.StandbyStackDroneArrivalRate = LogisticsOutputBridge.StandbyStackDroneArrivalRate;
                _sim.StandbyAoeDroneArrivalRate = LogisticsOutputBridge.StandbyAoeDroneArrivalRate;

                // 합체 지속 중에는 두 보드 모든 노드의 산출량이 ×2 다(`260917_W03` 7-2 #2).
                // ⚠️ **매 틱 다시 쓴다** — 켤 때만 쓰면 합체가 끝나도 공장이 영영 두 배다.
                BalanceConfig bal = robot != null ? robot.balanceRef : null;

                // 회피 스택 계수는 **자산이 원천**이다(2026-09-17 · `260917_W04` 2장).
                // ⚠️ 매 틱 넣는 까닭 — 자산을 인스펙터에서 고치면 그 자리에서 반영돼야 한다.
                if (bal != null && bal.dodgeStacksPerBooster > 0)
                    DodgeSystem.StacksPerBooster = bal.dodgeStacksPerBooster;

                // 회피 이동 값 둘 — **자산이 원천**이다(⚠️ 설계 가정 · `260917_W07` 3장).
                //    매 틱 넣는 까닭은 계수와 같다 — 인스펙터에서 고치면 그 자리에서 반영돼야 한다.
                if (bal != null)
                {
                    _sim.DodgeMoveDistance = bal.dodgeMoveDistance;
                    _sim.DodgeMoveSeconds = bal.dodgeMoveSeconds;
                }
                MergeSignals.OutputMultiplier = IsMerged && bal != null
                    ? Mathf.Max(1f, bal.mergeOutputMult)
                    : MergeSignals.None;

                // ── 쉴드 (2026-09-17 · `260917_W05` 4장) ──────────────────────────
                // ⚠️ **기본 최대치 0 이면 이 층은 없는 것과 같다** — 값이 정해지기 전까지
                //    배포 거동은 지금 그대로다. 켜는 것은 `balance_v4.json` 이 한다.
                if (bal != null)
                {
                    // ⚠️ **그릇도 보드가 정한다**(2026-09-17 사용자 확정) — 노드를 뽑으면
                    //    그 자리에서 줄어든다. 🗑️ 구 `bal.shieldMax`(로봇 고정) 폐기.
                    _sim.ShieldMax = ShieldSystem.MaxFrom(
                        LogisticsOutputBridge.ShieldNodeCount, bal.shieldMaxPerNode);
                    _sim.StandbyShieldMax = ShieldSystem.MaxFrom(
                        LogisticsOutputBridge.StandbyShieldNodeCount, bal.shieldMaxPerNode);

                    _sim.ShieldChargeRate = ShieldCharge(bal, _sim.ShieldMax,
                        LogisticsOutputBridge.ShieldMaterialProduce,
                        LogisticsOutputBridge.ShieldNodeCount);
                    // ⚠️ **대기 보드도 채운다** — 그래야 「다친 로봇을 빼서 회복」이 성립한다.
                    _sim.StandbyShieldChargeRate = ShieldCharge(bal, _sim.StandbyShieldMax,
                        LogisticsOutputBridge.StandbyShieldMaterialProduce,
                        LogisticsOutputBridge.StandbyShieldNodeCount);
                }
            }

            // 수동 회피(화면 플릭). 이동 명령이 아니라 **즉시 회피**라 이동 처리와 섞지 않는다.
            if (running) PollFlick();

            // PC 단축키 — 플릭과 **나란히** 산다(2026-09-16 · `260915_W01` 판정 5).
            if (running) PollDodgeKey();

            // 이동: 수동 입력이 있으면 수동이 우선, 없으면 유예 후 자동 조종이 맡는다.
            // 영상 시나리오의 수동 카이팅 연출과 방치 진행이 한 빌드에서 공존해야 하므로 둘 다 살린다.
            if (running)
            {
                Vector2 mv = MoveInput();
                if (mv != Vector2.zero) _manualHoldUntil = Time.time + tuning.manualOverrideGraceTbd;

                UpdateRobotFacing(mv != Vector2.zero || Time.time < _manualHoldUntil);

                // ⚠️⚠️ **회피로 밀리는 동안은 자동 조종이 양보한다**(2026-09-17 · `260917_W07` 3장).
                //    안 그러면 같은 틱에 자동 조종이 제 자리를 써 버려 **밀린 것이 지워진다** —
                //    시뮬은 밀었는데 화면은 안 움직이는, 가장 찾기 어려운 꼴이 된다.
                if (mv == Vector2.zero && autoPilot && Time.time >= _manualHoldUntil
                    && !_sim.DodgeMotionActive)
                {
                    var ctx = new AutoPilotContext
                    {
                        robotPos = _sim.Robot.position,
                        enemies = _sim.Enemies,
                        arenaRadius = tuning.arenaRadiusTbd,
                        // 사거리 안이면 제자리 사격 — 카이팅 폐기(2026-08-26 판정)로
                        // desiredGap(후퇴 개시 거리)은 더 이상 쓰지 않는다.
                        attackRange = tuning.robotAttackRangeTbd,
                        moveSpeed = tuning.robotMoveSpeedTbd,
                        // 사거리 안이 이 수를 넘으면 제자리 — 이하면 가장 가까운 무리로 간다
                        // (2026-09-15 사용자 확정 · 가정 3).
                        holdWhenMoreThan = tuning.autoPilotHoldWhenMoreThanTbd,
                        dt = Time.deltaTime,
                    };
                    _sim.Robot.position = AutoPilotPolicy.NextPosition(ctx);
                }
                else if (mv != Vector2.zero)
                {
                    // ⚠️⚠️ **아레나 클램프가 수동 경로에만 남아 있었다**
                    // (2026-09-15 · 사용자 육안 8차 ② — 「이동 범위에 제한이 걸린다」).
                    //
                    // 09-11 에 전장이 **로봇을 따라다니는 판**이 되면서 아레나 원이 없어졌고
                    // `AutoPilotPolicy` 에서는 그때 걷었다(그 주석에 근거가 있다). 그런데
                    // **손으로 모는 이 줄은 안 걷었다** — `arenaRadiusTbd`(6) 원 안에 갇힌다.
                    // 자동으로 걸으면 안 막히고 **손으로 몰 때만 막히니** 더 헷갈린다.
                    //
                    // 📌 **같은 규칙을 두 경로가 따로 구현했고 한쪽만 고쳤다.** 오늘 내내 나온 모양이다 —
                    // 규칙이 두 벌이면 **고칠 때도 두 벌을 고쳐야 한다**는 것을 아무도 안 알려 준다.
                    _sim.Robot.position += mv.normalized * tuning.robotMoveSpeedTbd * Time.deltaTime;
                }
            }

            // 「화면 안」을 카메라에서 **재서** 넣는다 — 상수로 짓지 않는다(260908_W05 2-2).
            // 시뮬은 순수 계산이라 카메라가 없고, 아는 경계는 아레나 반지름 하나뿐인데
            // 그것은 **스폰 링(화면 바깥)**이라 「화면 안」을 대신할 수 없다.
            PushVisibleBounds();

            // 스폰 링도 같은 이유로 밖에서 넣는다 — **화면 밖 가장자리**다(§71-28 2).
            SetSpawnRingFromCamera();

            // 바닥 — 스테이지나 창이 바뀌었으면 다시 깔고, 아니면 밀기만 한다.
            if (BackgroundNeedsRebuild()) BuildBackground();
            UpdateBackgroundOffset();

            // 태그 스킬이 터지는 자리도 밖에서 넣는다 — **진입 클립이 다 도는 초**다
            // (260908_W06 2장 · (가) 0.75초). 새 상수를 만들지 않고 이미 있는 클립 초를 그대로 쓴다.
            _sim.SetTagSkillDelay(tuning != null ? tuning.animTagInSeconds : 0.75f);
            // 곁눈질 방향을 붙드는 시간 — 값은 조율 SO 가 든다(⚠️ 가정 · §74-12 B).
            _sim.SetSideStepHold(tuning != null ? tuning.enemySideStepHoldTbd : 0f);
            _sim.SetSideDetourCells(tuning != null ? tuning.enemySideDetourCellsTbd : 0f);
            _sim.SetSideDetourRecoverSeconds(
                tuning != null ? tuning.enemySideDetourRecoverSecondsTbd : 0f);

            // ⚠️ **매 프레임 넣는다**(2026-09-16 · 사용자 확정 §74-16 ③).
            //    설정은 `PlayerPrefs` 에 살고 버튼이 그것을 바꾸므로, 한 번만 넣으면
            //    켜고 끈 것이 이번 판에 안 먹는다. 캐지 않는 까닭도 같다(지침 §7).
            _sim.AutoTagEnabled = TagAutoMode.Enabled;

            // 처치한 자리에 재화를 떨어뜨린다(2026-09-18 설계 지시 ①).
            // ⚠️ **적립이 아니라 그림이다** — 규칙은 방치 런타임 한 곳에 있다.
            TickDrops();

            // ⚠️ **드론 이동 값 넷은 가정이다**(`CombatTuning` 의 `drone*Tbd`) —
            //    코드가 숫자를 들지 않게 SO 에서 매 프레임 넣는다(§3).
            if (tuning != null)
            {
                _sim.DroneOrbitRadius = tuning.droneOrbitRadiusTbd;
                _sim.DroneOrbitSpeed = tuning.droneOrbitSpeedTbd;
                _sim.DroneFlySpeed = tuning.droneFlySpeedTbd;
                _sim.DroneAttachDistance = tuning.droneAttachDistanceTbd;
                // ⚠️ **누적형 활동 범위**(2026-09-18 사용자 확정) — 0 이면 드론 사거리를 쓴다.
                _sim.DroneLeashRadius = tuning.droneLeashRadiusTbd;
                // ⚠️ **한 발을 쪼개 쏘기**(2026-09-18 사용자 확정 — 세 발 · 대미지 1/2).
                _sim.ShotsPerRound = tuning.shotsPerRound;
                _sim.ShotDamageFactor = tuning.shotDamageFactor;
                _sim.DroneHitInterval = tuning.droneHitIntervalTbd;
            }
            // 종을 가르는 몫 — 보드가 정한다.
            _sim.AoeDroneShare = LogisticsOutputBridge.AoeDroneShare;

            _sim.Tick(Time.deltaTime);

            // ⚠️ **못 닿는 적을 로봇 쪽 링으로 되돌린다**(§71-28 2). 이동 클램프를 걷어 내면
            // 로봇이 반대로 달릴 때 **영영 못 닿는 적**이 생기고, 그 적은 살아 있어
            // 스테이지가 안 끝나는데 화면에도 없다 — **왜 안 끝나는지가 안 보인다.**
            // 지우지 않고 되돌리므로 **개체 수는 안 변한다.**
            _sim.RespawnUnreachable();

            // 교대했으면 뷰를 새 로봇에 다시 묶는다 — 안 하면 B가 싸우는데 A가 서 있다.
            if (_sim.ActiveRobotIndex != _viewedRobotIndex || IsMerged != _viewedMerged)
            {
                // ⚠️ **유령을 안 남긴다** (2026-09-10 사용자 확정 · 리허설 1차).
                // 물러나는 로봇을 페이드로 지우던 것을 **폐기**했다 — 새 로봇이 들어오는 내내
                // 옛 로봇이 같이 서 있어 **둘이 겹쳐 보였다.** 뷰는 하나뿐이라 다시 묶으면
                // 그 순간 이전 그림이 사라진다. 그것이 지금 원하는 것이다.
                // ⚠️ `260907_W01` 2-3의 「페이드 아웃으로 지운다」는 **사용자가 09-10에 폐기**했다.
                bool tagSwitch = _sim.ActiveRobotIndex != _viewedRobotIndex && IsMerged == _viewedMerged;
                BindRobotView();
                if (tagSwitch) PlayTagEntrance();
            }

            // 태그 스킬 연출은 **터진 틱**에 나간다 — 태그 인과 동시가 아니다
            // (260908_W06 2장 · 진입 클립이 다 돈 0.75초 뒤). 뷰를 다시 묶는 위 블록과
            // 떼어 놓은 이유가 그것이다 — 교대 프레임에는 아직 안 터졌다.
            if (_sim.TagSkillResolvedThisTick)
            {
                PlayTagSkillEffect();
                NoteTagSkillNumbers();
            }

            PlayInstalledVfx();
            // 쉴드 막대 — 매 프레임 비율을 다시 건다(HP 바와 같은 결).
            SyncShieldBar();
            SyncEnemyProjectileViews();

            // 날아가는 탄환은 **연출 전용**이라 시뮬 배속이 아니라 실제 시간으로 간다 —
            // 피해는 이미 들어갔고 그림만 뒤따르기 때문이다(§72-24 ④).
            TickFlyingShots(Time.deltaTime);

            TickFiredRates(Time.deltaTime);
            UpdateAmmoOutView();   // 지속 상태 — 한 번 만들고 껐다 켠다(`260909_W01` 3장이 (가)를 확정)
            PublishSupplySignals();

            // ⚠️⚠️ **전투 그림은 전투 층에 둔다**(2026-09-21 사용자 육안 ③).
            //    배경·적·탄·드론·연출·드롭이 전부 이 트랜스폼 아래에 달리므로
            //    **뿌리에서 한 번** 훑는다 — 낳는 자리마다 적으면 새로 낳는 것마다 빠뜨린다.
            CombatLayer.Apply(transform);

            // 처치를 방치 런타임으로 흘린다. 가져가며 비우는 API라 같은 처치를 두 번 세지 않는다.
            IdleSignals.AddKills(_sim.ConsumeKills());

            // 이번 틱 사격 연출(탄선 + 피격). 실제 틱이 돈 경우만(종료 후 스테일 재생성 방지).
            if (running)
                foreach (ShotEvent s in _sim.ShotsThisTick)
                {
                    SpawnShotFx(s);
                    // 발사 반동 — 스프라이트를 늘리지 않고 로봇 전체를 표적 반대로 밀었다 복귀(V01 §3).
                    if (_robotView != null) _robotView.Recoil(s.to - s.from);
                    // 소리는 **탄선과 같은 사건**에 붙는다 — 소리를 위해 새 사건을 만들지 않는다
                    // (사운드 문서 개요 「발동 사건 목록을 갖지 않는다」).
                    AudioSignals.Play(SoundIds.FireA, SoundIds.KindOf(SoundIds.FireA));
                }

            // 피격 점멸 — 로봇 HP가 줄어든 프레임에 한 번. 세기는 일정하다(V01 §3):
            // 로봇에 방어력 항목이 없어 받는 피해가 몬스터 공격력 그대로이므로,
            // 세기로 정도를 표현하면 없는 정보를 지어내는 것이 된다.
            if (running && _sim.Robot != null)
            {
                if (_sim.Robot.hp < _lastRobotHp - 0.0001f)
                {
                    if (_robotView != null) _robotView.FlashHit();
                    // ⚠️ 점멸은 뷰가 있어야 하지만 **소리는 뷰와 무관하다** — 사운드 문서 1장이
                    // 「화면을 안 볼 때도 닿는 통로」로 규정했으니 뷰가 없다고 안 낼 이유가 없다.
                    AudioSignals.Play(SoundIds.Hit, SoundIds.KindOf(SoundIds.Hit));
                }
                _lastRobotHp = _sim.Robot.hp;
            }

            // 스폰/사망에 따른 적 뷰 수명 관리
            var live = new HashSet<CombatEntity>(_sim.Enemies);
            // despawn(사망) 정리
            var toRemove = new List<CombatEntity>();
            foreach (KeyValuePair<CombatEntity, CombatEntityView> kv in _enemyViews)
                if (!live.Contains(kv.Key)) { if (kv.Value != null) Destroy(kv.Value.gameObject); toRemove.Add(kv.Key); }
            foreach (CombatEntity e in toRemove) _enemyViews.Remove(e);

            // 신규 스폰 뷰 생성 + 동기화
            foreach (CombatEntity e in _sim.Enemies)
            {
                if (!_enemyViews.TryGetValue(e, out CombatEntityView view))
                {
                    bool big = e.maxHp >= 1000f; // 장갑/보스 크게
                    float size = EnemySize(e.maxHp);
                    Color col = big ? new Color(0.9f, 0.35f, 0.2f) : new Color(0.9f, 0.3f, 0.3f);
                    view = NewView($"Enemy_{e.label}");
                    _viewScaleByLabel.TryGetValue(e.label, out int viewScale);
                    _artByLabel.TryGetValue(e.label, out EnemyDefinition art);
                    view.Bind(e, col, size, SortingLayers.Actor,
                        art != null ? art.sprite : null,
                        art != null ? art.animClips : null,
                        viewScale > 0 ? viewScale : 1);
                    _enemyViews[e] = view;
                }
                view.Sync();
            }

            SyncDroneViews();
            _robotView.Sync();
        }

        /// <summary>
        /// 사출된 드론의 뷰. 드론은 CombatEntity가 아니라(HP도 방어도 없다 — 충전량이 곧 수명이다)
        /// 위치만 따라가는 얇은 스프라이트로 그린다.
        /// </summary>
        private void SyncDroneViews()
        {
            IReadOnlyList<DroneUnit> drones = _sim.Drones;

            // 소멸분 정리 — 충전량을 다 쓴 드론은 목록에서 빠진다.
            var live = new HashSet<DroneUnit>(drones);
            var gone = new List<DroneUnit>();
            foreach (KeyValuePair<DroneUnit, SpriteRenderer> kv in _droneViews)
                if (!live.Contains(kv.Key))
                {
                    if (kv.Value != null) Destroy(kv.Value.gameObject);
                    gone.Add(kv.Key);
                }
            foreach (DroneUnit d in gone) _droneViews.Remove(d);

            foreach (DroneUnit d in drones)
            {
                if (!_droneViews.TryGetValue(d, out SpriteRenderer sr))
                {
                    var go = new GameObject("Drone");
                    go.transform.SetParent(transform, false);
                    sr = go.AddComponent<SpriteRenderer>();
                    // ⚠️ **종마다 다른 그림이다**(2026-09-16) — 자산은 둘 다 있었는데
                    //    (`drone_n` 누적형 · `drone_w` 광역형) **한 장만 걸려 있었다.**
                    Sprite art = robotB == null ? null
                        : (d.Kind == DroneKind.Aoe && robotB.droneAoeSprite != null
                            ? robotB.droneAoeSprite
                            : robotB.droneSprite);
                    sr.sprite = art != null ? art : PlaceholderSprite.SoftDisc();
                    sr.color = art != null ? Color.white : new Color(0.6f, 0.95f, 0.7f);

                    // ⚠️⚠️ **액터보다 두 칸 위에 그린다**(2026-09-18 사용자 리허설 ① —
                    //    「드론이 공격할 때 사라진다」). 종전에는 적·로봇과 **같은 층 0** 이라
                    //    누적형이 적에게 **붙는 순간 그 뒤로 숨었고**, 광역형도 로봇 뒤를 돌 때
                    //    가려졌다 — 같은 층 안에서는 어느 쪽이 위인지 정해지지 않는다.
                    //    📌 ±1~9 는 **같은 층 안의 미세 조정** 몫이다(`SortingLayers` 주석).
                    sr.sortingOrder = SortingLayers.Actor + 2;
                    // 크기는 아트 캔버스가 정한다(드론 64px). 아트가 이미 그 크기면 스케일 1이다.
                    if (art == null) go.transform.localScale = new Vector3(ArtSpec.DroneSize, ArtSpec.DroneSize, 1f);
                    _droneViews[d] = sr;
                }
                if (sr != null) sr.transform.position = new Vector3(d.Position.x, d.Position.y, 0f);
            }
        }

        private CombatEntityView NewView(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<CombatEntityView>();
        }

        /// <summary>
        /// 화면 플릭 → 수동 회피. 누른 지점에서 뗀 지점까지의 방향으로 피한다.
        ///
        /// 짧고 빠른 것만 플릭으로 본다 — 오래 끈 것은 드래그(보드 조작)라 회피가 아니다.
        /// 문턱 두 값은 원천 미규정이라 CombatTuning의 TBD를 읽는다.
        /// </summary>
        private void PollFlick()
        {
            Pointer p = Pointer.current;
            if (p == null) return;

            bool pressed = p.press.isPressed;
            Vector2 pos = p.position.ReadValue();

            if (pressed && !_pointerDown)
            {
                _pointerDown = true;
                _pointerStart = pos;
                _pointerDownTime = Time.unscaledTime;
                return;
            }

            if (pressed || !_pointerDown) return;

            _pointerDown = false;
            Vector2 delta = pos - _pointerStart;
            if (Time.unscaledTime - _pointerDownTime > tuning.flickMaxSecondsTbd) return;
            if (delta.magnitude < tuning.flickMinPixelsTbd) return;

            // 방향만 넘긴다. 발동 여부(추진제 유무·재발동 금지)는 시뮬이 판정한다.
            _sim.RequestDodge(delta.normalized);
        }

        // ---- 입력(InputSystem, 프로젝트 컨벤션) ----
        /// <summary>
        /// 회피 단축키. ⚠️ **키는 가정이다** — 문서에 키 배치 절이 없다(설계 역기입 자리).
        /// 이동이 WASD/화살표라 손가락이 그 위에 있고, 스페이스는 **둘 다 닿는** 자리다.
        /// </summary>
        private const Key DodgeKey = Key.Space;

        /// <summary>
        /// 키로 한 번 피한다 — **촬영본에 입력이 남게** 하려는 것이 이 기능의 이유다.
        ///
        /// ⚠️ 누른 프레임에만 반응한다(`wasPressedThisFrame`) — 누르고 있는 동안
        /// 매 프레임 피하면 **스택이 한 순간에 비운다.**
        ///
        /// ⚠️ 방향을 여기서 정하지 않는다 — `DodgeShortcut` 이 정한다(§3).
        /// 피할 근거가 없으면 `null` 이고, 그때는 **스택을 안 쓴다.**
        /// </summary>
        private void PollDodgeKey()
        {
            Keyboard k = Keyboard.current;
            if (k == null || _sim == null) return;
            if (!k[DodgeKey].wasPressedThisFrame) return;

            Vector2? dir = DodgeShortcut.Direction(MoveInput(), _sim.AimDirection);
            if (dir.HasValue) _sim.RequestDodge(dir.Value);
        }

        private static Vector2 MoveInput()
        {
            Keyboard k = Keyboard.current;
            if (k == null) return Vector2.zero;
            Vector2 d = Vector2.zero;
            if (k.wKey.isPressed || k.upArrowKey.isPressed) d.y += 1f;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) d.y -= 1f;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) d.x -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) d.x += 1f;
            return d;
        }

        // ---- 사격 연출: 날아가는 탄환 + 명중 플래시 ----

        /// <summary>
        /// 탄환이 나는 속도(유닛/초). ⚠️ **가정이다** — 연출 문서에 로봇 탄속 절이 없다.
        ///
        /// 적 포탄이 6(<see cref="EnemyAttackRule"/> · 이것도 가정)이라 **그 넷 배**로 둔다.
        /// 근거는 값이 아니라 **구분**이다 — 내가 쏜 것이 나에게 오는 것보다 눈에 띄게
        /// 빨라야 두 방향이 한 화면에서 갈린다. 값이 서면 여기 하나만 바뀐다.
        /// </summary>
        private const float ShotBulletSpeedTbd = 24f;

        /// <summary>탄환 그림이 없을 때 쓰는 사각의 한 변(유닛). ⚠️ 가정.</summary>
        private const float ShotBulletUnits = 0.22f;

        /// <summary>날아가는 중인 탄환 하나. **판정은 이미 끝났다** — 그림만 뒤따라간다.</summary>
        private struct FlyingShot
        {
            public Transform view;
            public Vector2 from;
            public Vector2 to;
            public float elapsed;
            public float duration;
            public ShotEvent shot;
        }

        private readonly List<FlyingShot> _flying = new List<FlyingShot>();

        /// <summary>
        /// 사격 연출 — **탄선을 탄환으로 바꿨다**(2026-09-14 사용자 확정 · §72-24 ④).
        ///
        /// 종전에는 로봇에서 적까지 **흰 사각을 늘여** 0.05초 띄웠다. 그러면 발사와 명중이
        /// **한 프레임에 같이** 일어나 「무엇이 날아가서 맞혔다」가 안 읽힌다 —
        /// 화면에서는 적이 그냥 줄어드는 것으로 보였다.
        ///
        /// ⚠️⚠️ **피해 시점은 안 옮긴다.** 시뮬은 이 이벤트가 날 때 **이미 피해를 넣었다.**
        /// 그림만 뒤따라간다 — 연출이 판정을 끌고 다니면 **맞기 전에 죽거나 죽은 뒤에 맞는**
        /// 자리가 생기고, 그건 밸런스가 아니라 규칙이 흔들리는 것이다.
        /// </summary>
        private void SpawnShotFx(ShotEvent s)
        {
            Color c = TracerColor(s.kind);

            Vector2 from = s.from, to = s.to;
            float dist = Vector2.Distance(from, to);
            // 이펙트는 1방향만 그리고 회전은 코드가 준다(V01 §C) — 회전 적용 지점은 ArtSpec 하나다.
            float ang = ArtSpec.EffectRotationDegrees(to - from);

            Sprite bullet = tuning != null ? tuning.tagBulletSprite : null;

            var go = new GameObject("ShotBullet");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(from.x, from.y, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            var bsr = go.AddComponent<SpriteRenderer>();
            if (bullet != null)
            {
                bsr.sprite = bullet;
                bsr.color = c;
            }
            else
            {
                // 그림이 없으면 **작은 사각**이다 — 종전처럼 길게 늘이지 않는다.
                bsr.sprite = PlaceholderSprite.White();
                bsr.color = c;
                go.transform.localScale = new Vector3(ShotBulletUnits, ShotBulletUnits, 1f);
            }
            bsr.sortingOrder = SortingLayers.EffectOver;

            _flying.Add(new FlyingShot
            {
                view = go.transform,
                from = from,
                to = to,
                elapsed = 0f,
                // 아주 가까우면 한 프레임에 끝나 안 보인다 — 바닥을 둔다(가정).
                duration = Mathf.Max(0.03f, dist / ShotBulletSpeedTbd),
                shot = s,
            });
        }

        /// <summary>날아가는 탄환을 옮기고, 닿으면 **명중 플래시**로 바꾼다.</summary>
        private void TickFlyingShots(float dt)
        {
            // 역순 — 지우면서 돈다.
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                FlyingShot f = _flying[i];
                f.elapsed += dt;

                if (f.elapsed < f.duration)
                {
                    if (f.view != null)
                        f.view.position = Vector2.Lerp(f.from, f.to, f.elapsed / f.duration);
                    _flying[i] = f;
                    continue;
                }

                if (f.view != null) Destroy(f.view.gameObject);
                _flying.RemoveAt(i);
                SpawnHitFx(f.shot);
            }
        }

        /// <summary>
        /// 명중 플래시 — **코드 드로잉**이다(자산 없음 · §72-24 ④).
        ///
        /// ⚠️ **폭발탄은 크게.** 발당 50 이라 관통 20 · 표준 10 과 **다섯 배까지** 차이가
        /// 나는데 같은 크기로 터지면 화면에서 **무엇이 센지가 안 보인다.**
        /// </summary>
        /// <summary>
        /// 이 탄종의 피격 VFX 칸들. 없으면 <c>null</c> — 그때는 코드 플래시로 떨어진다.
        ///
        /// ⚠️ **고르는 자리를 하나로 둔다** — 탄종마다 if 를 흩어 두면 하나만 고쳐진다.
        /// </summary>
        private Sprite[] HitFramesOf(AmmoKind kind)
        {
            if (tuning == null) return null;
            Sprite[] f;
            switch (kind)
            {
                case AmmoKind.Pierce: f = tuning.hitPierceFrames; break;
                case AmmoKind.Standard: f = tuning.hitStandardFrames; break;
                case AmmoKind.Explosive: f = tuning.hitExplosiveFrames; break;
                default: return null;
            }
            return f != null && f.Length > 0 ? f : null;
        }

        /// <summary>
        /// 피격 VFX 한 벌의 한 변(월드 유닛). ⚠️ **가정** — 연출 문서에 크기 절이 없다.
        ///
        /// 근거는 값이 아니라 **비**다: 종전 코드 플래시가 쓰던 0.28(격파 0.6)에 맞춘다.
        /// 그림이 대신 들어서는 것이므로 화면에서 차지하는 자리가 갑자기 달라지면 안 된다.
        /// 폭발탄이 한 배 반인 것도 그대로 둔다(§72-24 ④).
        /// </summary>
        private static float HitVfxSize(ShotEvent s)
        {
            float size = s.killed ? 0.6f : 0.28f;
            if (s.kind == AmmoKind.Explosive) size *= 1.5f;
            return size * HitVfxSpriteMultiplier;
        }

        /// <summary>
        /// 그림이 코드 사각보다 여백을 품고 있어 곱하는 배수. ⚠️ **가정 · 되돌릴 수 있다.**
        /// </summary>
        private const float HitVfxSpriteMultiplier = 3f;

        private void SpawnHitFx(ShotEvent s)
        {
            Color c = TracerColor(s.kind);

            Vector2 to = s.to;

            // ⚠️⚠️ **자산이 있으면 그림으로, 없으면 지금 코드 그리기로**(2026-09-16 · §74-7 ②).
            //
            // 폴백을 남기는 이유는 둘이다 — ① 아트가 빠진 판에서도 「맞았다」가 보여야 하고,
            // ② 격리 전투 씬처럼 튜닝이 안 꽂힌 경로가 아직 있다.
            //
            // ⚠️ **틴트를 건다** — 탄선 색과 같은 색이라야 「무엇이 맞혔나」가 한 눈에 갈린다.
            //    격파는 종전처럼 밝은 노랑이다.
            Sprite[] frames = HitFramesOf(s.kind);
            if (frames != null && s.aoeRadius <= 0f)
            {
                float size = HitVfxSize(s);
                var go = new GameObject("HitVfx");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(to.x, to.y, 0f);
                go.transform.localScale = new Vector3(size, size, 1f);

                Color tint = s.killed ? new Color(1f, 0.92f, 0.55f) : c;
                go.AddComponent<HitVfxPlayer>().Play(
                    frames, tint, EffectTiming.HitFlashDuration, SortingLayers.EffectOver + 1);
                return;
            }

            // ── 광역형 드론의 범위 타격 (2026-09-17 · `260917_W08` 5-5) ──
            //
            // ⚠️ **폭발탄 칸(`aoeRadius`)과 다른 칸을 본다** — 09-16 에 사거리를 그 칸에
            //    넣었다가 한 변 18.4 짜리 주황 사각이 화면 절반을 덮었다.
            // 📌 그리는 반경은 **판정이 실제로 쓴 수**다(`aoeJudgeRadius`).
            if (s.aoeJudgeRadius > 0f)
            {
                // ⚠️⚠️ **스위치가 먼저다.** 2026-09-18 에 아트가 들어오면서 그림이 채워졌는데,
                //    구 순서(그림이 있으면 그린다)로 두면 **스위치를 안 거치고 켜졌다** —
                //    크기를 정할 판정 반경 값이 아직 없어 반경 9.2 짜리 고리가 떴을 것이다.
                Sprite ring = !tuning.droneAoeRingOn ? null
                    : tuning.droneAoeSprite != null ? tuning.droneAoeSprite : PlaceholderSprite.Ring();

                if (ring != null)
                {
                    float dia = s.aoeJudgeRadius * 2f;
                    var aoe = new GameObject("DroneAoe");
                    aoe.transform.SetParent(transform, false);
                    aoe.transform.position = new Vector3(to.x, to.y, 0f);
                    aoe.transform.localScale = new Vector3(dia, dia, 1f);
                    var asr = aoe.AddComponent<SpriteRenderer>();
                    asr.sprite = ring;
                    // 바닥에 퍼지는 것이라 **액터보다 아래**다 — 그림자와 같은 층 결.
                    asr.color = new Color(0.55f, 0.8f, 1f, 0.55f);
                    asr.sortingOrder = SortingLayers.EffectUnder;
                    Destroy(aoe, EffectTiming.HitFlashDuration);
                }
            }

            if (s.aoeRadius > 0f)
            {
                // AoE 폭발 광역 원(플레이스홀더 반투명 주황 사각). 스플래시 범위 시각화.
                float d = s.aoeRadius * 2f;
                var boom = new GameObject("Boom");
                boom.transform.SetParent(transform, false);
                boom.transform.position = new Vector3(to.x, to.y, 0f);
                boom.transform.localScale = new Vector3(d, d, 1f);
                var bsr2 = boom.AddComponent<SpriteRenderer>();
                bsr2.sprite = PlaceholderSprite.White();
                bsr2.color = new Color(1f, 0.5f, 0.15f, 0.35f);
                bsr2.sortingOrder = SortingLayers.EffectOver - 1; // 폭발 — 탄환보다 아래
                Destroy(boom, 0.14f);
            }
            else
            {
                // 단일/멀티샷 피격 플래시(격파 시 크고 밝게).
                // ⚠️ **폭발탄은 한 배 반**(§72-24 ④) — 발당 50 이 10·20 과 같은 크기로
                // 터지면 어느 것이 센지가 화면에서 안 갈린다. 값은 가정이다.
                float fs = s.killed ? 0.6f : 0.28f;
                if (s.kind == AmmoKind.Explosive) fs *= 1.5f;
                var flash = new GameObject("Hit");
                flash.transform.SetParent(transform, false);
                flash.transform.position = new Vector3(to.x, to.y, 0f);
                flash.transform.localScale = new Vector3(fs, fs, 1f);
                var hsr = flash.AddComponent<SpriteRenderer>();
                hsr.sprite = PlaceholderSprite.White();
                hsr.color = s.killed ? new Color(1f, 0.92f, 0.55f) : c;
                hsr.sortingOrder = SortingLayers.EffectOver + 1; // 피격 플래시 — 탄선 위
                Destroy(flash, s.killed ? 0.12f : 0.06f);
            }
        }

        private static Color TracerColor(AmmoKind kind)
        {
            switch (kind)
            {
                case AmmoKind.Pierce: return new Color(1f, 0.92f, 0.35f);   // 관통 = 노랑
                case AmmoKind.Standard: return new Color(0.4f, 0.9f, 1f);      // 표준 = 시안
                case AmmoKind.Explosive: return new Color(1f, 0.55f, 0.2f); // 폭발 = 주황
                default: return Color.white;
            }
        }

        /// <summary>씬 리로드 없이 전투를 재시작(뷰 정리 후 재구성). Build Settings 비의존.</summary>
        /// <summary>
        /// 스테이지를 갈아끼우고 재시작한다(자동 전투 진행용). 씬 리로드 없음 — 연속성 원칙.
        /// null이면 무시한다(생성기 미실행 등으로 SO가 비어 있을 때 조용히 죽지 않게).
        /// </summary>
        public void LoadStage(StageDefinition next)
        {
            if (next == null) return;
            stage = next;
            // ⚠️ **스테이지가 갈릴 때도 국면을 다시 쓴다.** `Start`에서만 쓰면 S6로 들어가도
            // 전투 곡이 그대로 돈다 — 「보스전은 국면 전환」이 화면에서만 일어나고 귀에서는 안 난다.
            PushMusicPhase();
            Restart();
        }

        /// <summary>뷰만 정리하고 전투를 다시 구성한다. 씬 리로드 없음.</summary>
        public void Restart()
        {
            _ready = false;
            foreach (KeyValuePair<CombatEntity, CombatEntityView> kv in _enemyViews)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _enemyViews.Clear();
            // 드론 뷰도 함께 정리한다 — 안 지우면 재시작마다 유령 드론이 화면에 쌓인다.
            foreach (KeyValuePair<DroneUnit, SpriteRenderer> kv in _droneViews)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _droneViews.Clear();
            // 날아가던 탄환도 지운다 — 안 지우면 재시작 뒤 **없던 명중 플래시**가
            // 옛 자리에서 터진다(드론 유령과 같은 종류다 · §72-24 ④).
            for (int i = 0; i < _flying.Count; i++)
                if (_flying[i].view != null) Destroy(_flying[i].view.gameObject);
            _flying.Clear();
            if (_robotView != null) Destroy(_robotView.gameObject);
            _robotView = null;
            _viewedRobotIndex = 0;
            _viewedMerged = false;

            // ⚠️⚠️ **발사 누적의 기준점을 같이 지운다** (2026-09-17 · 사용자 육안 · 실측 `-198.8`).
            //
            // `_firedMark` 는 **앞 판의 누적 발사 수**다. 새 시뮬은 0 부터 다시 세는데
            // 기준점만 남아 있으면 첫 창에서 **(0 − 199) ÷ 1초 = -199 발/초**가 찍힌다.
            // 화면에 「표준 **-198.8** 발/초」로 떴고, 사용자가 S1 을 200발로 이기고
            // S2 에 들어간 직후의 수가 정확히 그것이다.
            //
            // 📌 **지우는 자리가 둘로 갈려 있던 것**이다 — 시뮬은 새로 나는데 그 시뮬을
            //    재는 창은 안 지워졌다. 「같이 나는 것은 같이 지운다」.
            for (int i = 0; i < _firedMark.Length; i++) { _firedMark[i] = 0; _firedRate[i] = 0f; }
            _firedWindow = 0f;
            _seenDodges = 0;
            Begin();
        }

        // ---- 최소 HUD (OnGUI, 디버그 표시) ----
        private void OnGUI()
        {
            // 메인 메뉴가 덮고 있으면 그리지 않는다 — IMGUI 는 뒤에 그리는 쪽이 위로 온다
            // (2026-09-10 · 실측: 오프라인 대화상자가 「게임 시작」 버튼을 덮었다).
            if (MainMenuGate.IsOpen) return;
            if (!_ready) return;
            UiSkin.Apply(); // 껍데기 + 한글 폰트 — WebGL엔 시스템 폰트 폴백이 없다

            // ⚠️⚠️ **HUD 를 시안 3 으로 다시 짰다**(2026-09-18 사용자 확정 · 플랜 §85-8).
            //
            // 🗑️ **폐기 — 좌상단 글자 블록 전부**(줄 열둘 · 헤더 분리 11f2ef9 포함).
            //    그 꼴은 **줄이 늘 때마다 잘릴 자리를 다시 재야 하는** 구조였다:
            //    09-14 에 세 줄을 잃었고, 09-15 에 방 재는 법을 고쳤고, 09-18 오전에
            //    헤더로 갈랐고, 그날 오후에 통째로 폐기됐다. **네 번 고친 자리는 꼴이 틀린 것이다.**
            //
            // 📌 지금 화면이 말하는 법 —
            //    · **칩 줄**(썸네일·닉네임·전투력 / 골드·소리·설정)
            //    · **배지 줄**(스테이지·목표 / 남은 몬스터·리젠)
            //    · **로봇 몸에 붙은 막대**(HP·보호막·회피) — 글자로 안 적는다
            //    · **우하단 마일스톤 카드**
            //    · 수치 아홉 줄은 **개발 빌드 전용 「i」 패널** 안으로
            //
            // ⚠️ **그리는 것은 여기서 부르기만 한다** — 몸은 `StageRunner.Hud.cs` 다.
            float hudScale = UiLayout.Scale(Screen.height);
            int hudPx = KoreanFont.Snap(Mathf.Max(11, Mathf.RoundToInt(28f * hudScale)));

            var style = new GUIStyle(GUI.skin.label) { fontSize = hudPx };
            style.normal.textColor = Color.white;
            var big = new GUIStyle(GUI.skin.label)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(16, Mathf.RoundToInt(68f * hudScale))),
                fontStyle = FontStyle.Bold,
            };
            big.normal.textColor = Color.white;

            // 막대 옆·막대 안 글자(탄약 막대가 쓴다). 막대 높이가 14라 16이면 칸 밖으로 넘친다.
            if (_hudSmall == null)
            {
                _hudSmall = new GUIStyle(GUI.skin.label) { fontSize = hudPx };
                _hudSmall.normal.textColor = Color.white;
                _hudSegment = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleCenter,
                };
            }
            else
            {
                _hudSmall.fontSize = hudPx;
            }

            // ⚠️⚠️ **조립 화면에서는 전투 HUD 를 한 줄도 안 그린다**(2026-09-18).
            //
            // 상단 인셋 위는 이제 **보드 쪽이 쓴다**(고철 수치 + 물류·조립 문제 목록).
            // 전투 칩 줄을 거기 같이 얹으면 두 화면의 머리가 겹친다 —
            // 09-11 에 「띠 다섯과 절대 좌표는 다른 화면의 수」라고 갈라 둔 그 경계다.
            //
            // 🗑️ 함께 폐기 — **접기 버튼**(09-15 육안 ③). 접을 글자 블록 자체가 없어졌다.
            if (GameViewSignals.BoardViewActive)
            {
                DrawMergeCutscene();   // 전체 화면 연출은 층이 달라 여기서도 나온다
                return;
            }

            DrawChipBar();
            DrawStageBadge();
            DrawRobotGauges();
            DrawPops();
            DrawMilestoneCard();
            DrawDevPanel(style);

            // 고철 + 물류 문제 요약 — **하단 큰 버튼 바로 위**(2026-09-18 사용자 육안 · 시안 4 ②).
            // ⚠️ 조립 화면과 **같은 자리 · 같은 함수**다. 경고가 캐릭터 옆에 뜨던 것은 폐기 —
            //    「나가는 곳이 없다」는 물류의 말이지 로봇의 말이 아니다.
            StatusStrip.Draw(StatusStrip.RectAbove(
                UiLayout.EnterBoardRect(Screen.width, Screen.height), Screen.width, Screen.height));

            if (_sim.Result != CombatResult.InProgress)
            {
                // 결과 — 화면 한가운데 아래. ⚠️ 자리는 가정이다(설계 역기입).
                float w = Mathf.Min(560f * hudScale, Screen.width - 24f);
                var box = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.42f,
                                   w, 160f * hudScale);
                UiPlate.Draw(box);
                UiBlockers.Add(box);
                GUILayout.BeginArea(box);
                GUILayout.Label(ResultText(), big);
                if (UiSkin.ButtonLayout("다시 (Restart)",
                        GUILayout.Width(200f * hudScale), GUILayout.Height(50f * hudScale)))
                    Restart();
                GUILayout.EndArea();
            }
            else
            {
                TagMergeButtons(style);
            }

            // IMGUI는 나중에 그린 것이 위에 온다 — 연출은 결과 화면 위에도 덮여야 한다.
            DrawMergeCutscene();
        }

        /// <summary>
        /// 합체 3초 연출(260831_V07 「3초 최소본」). **판정은 없다** — <see cref="MergeCutscene"/>이
        /// 계산한 값을 화면에 옮길 뿐이다.
        ///
        /// 최소본이 보여야 할 둘: **화면이 바뀐다**(전면 암전)와 **수치가 바뀐다**(화력 카운트업).
        /// 암전을 1.0으로 채우지 않는 이유는 그 뒤에서 전투가 계속 돌기 때문이다 —
        /// 합체 화력으로 적이 녹는 장면이 연출에 가려지면 보여 줄 것이 사라진다.
        /// </summary>
        /// <summary>
        /// 🗑️ **폐기 — 얼굴 진단을 따로 그리던 자리**(2026-09-18 · 시안 3).
        /// 진단 줄은 이제 **개발 빌드 전용 「i」 패널** 안의 한 줄이다
        /// (<c>StageRunner.Hud.cs</c> 의 <c>FaceDiagnosticLine</c>). 판을 따로 깔고
        /// 자리를 따로 재던 일이 통째로 없어졌다.
        /// </summary>

        private void DrawMergeCutscene()
        {
            if (!_cutscene.IsPlaying) return;

            Color prev = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, _cutscene.Dim);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

            // 글자도 암전과 같이 붙었다 뗀다 — 배경만 걷히고 글씨가 남으면 겉돈다.
            float a = _cutscene.Dim / MergeCutscene.MaxDim;

            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 64, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            var line = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30, alignment = TextAnchor.MiddleCenter,
            };

            float w = Screen.width;
            float y = Screen.height * 0.5f - 130f;

            GUI.color = new Color(1f, 0.86f, 0.35f, a); // 합체 = 금색. 태그(청)와 눈으로 갈린다
            GUI.Label(new Rect(0f, y, w, 90f), "합　체", title);

            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(0f, y + 96f, w, 44f),
                $"화력 {_cutscene.OutputBefore:F0}  →  {_cutscene.OutputNow:F0}", line);

            // 표적이 없어 안 터졌으면 줄 자체를 안 그린다 — 「버스트 0」은 거짓말이다.
            if (_cutscene.BurstDamage > 0f)
            {
                GUI.color = new Color(1f, 0.55f, 0.35f, a);
                GUI.Label(new Rect(0f, y + 144f, w, 44f),
                    $"버스트 {_cutscene.BurstDamage:F0}", line);
            }

            GUI.color = prev;
        }

        /// <summary>
        /// 태그·합체 조작. **판정은 시뮬이 한다** — 여기서는 누를 수 있는지만 물어보고 결과를 그린다.
        /// </summary>
        private void TagMergeButtons(GUIStyle style)
        {
            if (_sim.Tag == null) return;

            // ⚠️ **날 픽셀 자리를 걷었다**(2026-09-11 · 플랜 §68-4 (A)).
            // 문서는 이 둘을 **원형 200**(기준 캔버스)으로 정했고 자리는 **부유 띠 오른쪽**이다.
            // 종전 `(12, 300, 560, 120)` 은 좌상단 HUD 바로 아래라 배율 라벨과 겹쳤고
            // (`260902_W09` §5-3 이 그 자리를 이미 한 번 옮겼다) 34px 높이는 최소 150 을 밑돈다.
            Rect tagRect = UiLayout.RoundButtonRect(0, Screen.width, Screen.height);
            Rect mergeRect = UiLayout.RoundButtonRect(1, Screen.width, Screen.height);
            // ⚠️ **원형 그림이 있으면 그것을 쓴다**(2026-09-14 · `ui_round` 설치).
            // 없으면 `RoundStyle` 이 받은 것을 그대로 돌려줘 **네모 버튼으로 떨어진다** —
            // 누를 자리가 안 보이는 것보다 낫다.
            var round = UiSkin.RoundStyle(GUI.skin.button);
            round.fontSize = KoreanFont.Snap(Mathf.Max(10, Mathf.RoundToInt(tagRect.height * 0.15f)));
            round.wordWrap = true;   // 원형 안이라 한 줄로는 안 들어간다

            // ⚠️ **글자가 쓸 수 있는 자리는 사각이 아니라 원 안의 정사각이다**
            // (2026-09-14 · 2차 스크린샷 2장).
            //
            // 사각 그대로 두었더니 「합체 게이지 15%」가 **원 테두리를 넘어 좌우로 삐져나왔다.**
            // 지름 D 인 원에 드는 정사각의 한 변은 **D / √2**(≈ 0.707 D)이므로, 남는
            // (D − D/√2) / 2 를 사방 여백으로 준다 — **지어낸 값이 아니라 기하다.**
            int inset = Mathf.RoundToInt(tagRect.height * (1f - 0.70710678f) * 0.5f);
            round.padding = new RectOffset(inset, inset, inset, inset);
            UiBlockers.Add(tagRect);
            UiBlockers.Add(mergeRect);

            // 태그 — 쿨다운 중이거나 합체로 잠겨 있으면 비활성. 누르면 시뮬이 활성 인덱스까지 맞춘다.
            GUI.enabled = _sim.Tag.Tag.CanTag;

            // ⚠️⚠️ **길게 누르면 자동 교대가 켜진다**(2026-09-16 사용자 확정 · §74-16 ③).
            //    버튼을 그리기 **전에** 눌린 시간을 잰다 — 그래야 길게 누른 프레임에
            //    짧은 눌림(교대)이 같이 나가지 않는다.
            bool longPressed = TrackTagLongPress(tagRect);

            // ⚠️ **태그 스킬이 나갈 수 있으면 버튼을 밝힌다**(2026-09-16 사용자 확정 · §74-21 ④).
            //    대기 마운트가 만충일 때만이며, 판정은 `TagSystem.SkillReady` 하나가 낸다 —
            //    다른 잣대를 쓰면 **빛나는데 안 나가는** 버튼이 생긴다.
            //    ⚠️ 밝기 값은 **가정**이다(UI 12장 역기입 자리).
            bool skillReady = TagSystem.SkillReady(
                _sim.Tag.Tag.CanTag, _sim.Tag.StandbyMount != null && _sim.Tag.StandbyMount.IsFull);
            // ⚠️ **밝기만으로는 사용자 눈에 안 보였다**(2026-09-16 육안 2차 ③) —
            //    원형 버튼은 이미 밝은 판이라 1.45배가 묻힌다. **테두리를 같이 두른다.**
            if (skillReady) DrawTagSkillRing(tagRect);

            // ⚠️ **적재 링**(2026-09-18 설계 지시 · 「태그 원형(적재 링 40)」).
            //    대기 마운트가 얼마나 찼는지를 **원형 둘레**로 보인다 — 만충이 태그 스킬의
            //    조건이라, 「얼마나 남았나」가 버튼 자리에서 읽혀야 누를 때를 안다.
            //    ⚠️ 상한이 없으면(0) 안 그린다 — 만충 판정 자체가 없는 자리다.
            //
            // ✅ **쿨다운도 같은 원형 둘레에 얹는다**(2026-09-21 사용자 확정 ⑦).
            //    🗑️ 구 표시(원형 안 「쿨다운 2.6s」 글자)는 **버튼 글자로만** 남는다 —
            //    네모 판을 따로 띄우지 않는다.
            //
            // ⚠️ **두 링이 한 둘레를 나눠 쓴다.** 같은 자리에 겹쳐 그리면 둘 다 안 읽힌다 —
            //    적재는 **바깥 띠**, 쿨다운은 그 **안쪽 띠**다. 굵기로 자리를 가른다.
            //    ⚠️ 굵기 비(0.6 / 0.6)와 색은 **가정**이다(UI 역기입 자리).
            float ringScale = UiLayout.Scale(Screen.height);
            float loadT = TagSkillRingThickness * 0.6f * ringScale;

            MountLoad standby = _sim.Tag.StandbyMount;
            if (standby != null && standby.Capacity > 0f)
                DrawLoadRing(tagRect, standby.Total / standby.Capacity,
                    new Color(0.45f, 0.85f, 1f, 0.9f), loadT);

            // 쿨다운 — **남은 만큼**이 아니라 **돌아온 만큼**이 찬다. 「얼마나 기다려야
            // 하나」보다 「언제 눌러도 되나」가 버튼에서 읽어야 할 것이다.
            float cdLeft = _sim.Tag.Tag.CooldownRemaining;
            if (cdLeft > 0f && TagSystem.CooldownSeconds > 0f)
            {
                float ready = 1f - Mathf.Clamp01(cdLeft / TagSystem.CooldownSeconds);
                DrawLoadRing(Shrink(tagRect, loadT), ready,
                    new Color(1f, 0.78f, 0.35f, 0.95f), loadT);
            }

            Color tagPrev = GUI.color;
            if (skillReady) GUI.color = TagSkillReadyTint;

            bool tagHit = UiSkin.Button(tagRect, TagButtonLabel(), round);
            GUI.color = tagPrev;

            if (tagHit && !longPressed && _sim.TryManualTag())
            {
                // ⚠️ **여기서 뷰를 바로 다시 묶는다**(2026-09-14 · 「교대가 한 박자 느리다」).
                //
                // 유니티는 한 프레임에 `Update` 를 먼저 돌고 `OnGUI` 를 나중에 돈다.
                // 뷰 재바인드는 `Update` 에 있으므로, 버튼(OnGUI)으로 교대하면 그 프레임에는
                // **시뮬만 바뀌고 그림은 그대로**이고 **다음 프레임**에야 따라온다 —
                // 딱 한 박자다. 누른 사람에게는 버튼이 씹힌 것처럼 보인다.
                //
                // ⚠️ **자동 태그는 이 문제가 없다** — `TagTick` 이 `Update` 안에서 돌아
                // 같은 프레임의 재바인드 블록에 걸린다. 그래서 **수동만** 늦었다.
                BindRobotView();
                PlayTagEntrance();
            }

            // 합체 — 게이지가 차야 눌린다. 스테이지당 1회라 쓰고 나면 영영 비활성이다.
            GUI.enabled = _sim.Merge != null && _sim.Merge.IsReady;
            if (UiSkin.Button(mergeRect, MergeButtonLabel(), round) && _sim.TryMerge())
            {
                // 발동에 **성공했을 때만** 튼다. 실패한 버튼에 연출이 붙으면 안 된 일이 된 것처럼 보인다.
                _cutscene.Play(_sim.LastMergeSnapshot, _sim.LastBurstDamage);
                // 합체와 버스트는 **한 순간에 나는 두 사건**이다(MVP 4장 「동시에 일어나는 두 사건」).
                // 소리도 둘이며, 사운드 문서 4장이 **음색으로** 가르라고 정했다.
                AudioSignals.Play(SoundIds.Fusion, SoundIds.KindOf(SoundIds.Fusion));
                if (_sim.LastBurstDamage > 0f)
                    AudioSignals.Play(SoundIds.Burst, SoundIds.KindOf(SoundIds.Burst));
            }

            GUI.enabled = true;

            DrawTagAutoToggle(tagRect);
        }

        /// <summary>
        /// 태그 버튼을 **길게 눌렀는가** (2026-09-16 · 사용자 확정 「탭 = 교대 · 길게 = 자동」).
        ///
        /// ⚠️ **IMGUI 에는 길게 누르기가 없다** — 눌린 순간과 뗀 순간만 온다.
        /// 그래서 누른 시각을 들고 있다가 **뗄 때** 얼마나 지났는지로 가른다.
        ///
        /// ⚠️ **길게 누른 프레임에는 교대가 안 나가야 한다.** 둘 다 나가면
        /// 「자동을 켜려다 교대까지 해 버리는」 손이 된다 — 부르는 쪽이 이 값으로 막는다.
        ///
        /// 🗑️ **구 규칙 「켜기만 한다」 폐기**(2026-09-16 사용자 육안 · 플랜 §74-21 ①).
        /// 내가 「길게 눌러 켜고 길게 눌러 끄면 지금 어느 쪽인지 모른 채 누르게 된다」고
        /// 적었는데, **써 보니 그 반대였다** — 켠 뒤 다시 길게 눌러도 안 꺼지니
        /// **끄는 길이 없는 것처럼** 느껴진다. 지금 상태는 **위 토글 버튼이 글자로 말한다.**
        ///
        /// 📌 **켬↔끔 한 손짓이다.** 위 토글과 **같은 값**을 뒤집는다 —
        /// 둘이 다른 값을 만지면 화면이 두 답을 갖는다(지침 §7).
        /// </summary>
        private bool TrackTagLongPress(Rect tagRect)
        {
            Event e = Event.current;
            if (e == null) return false;

            if (e.type == EventType.MouseDown && tagRect.Contains(e.mousePosition))
            {
                _tagPressAt = Time.unscaledTime;
                return false;
            }

            if (e.type != EventType.MouseUp || _tagPressAt < 0f) return false;

            float held = Time.unscaledTime - _tagPressAt;
            _tagPressAt = -1f;

            // 뗀 자리가 버튼 밖이면 **아무 일도 없다** — 끌어서 취소하는 길을 남긴다.
            if (!tagRect.Contains(e.mousePosition)) return false;
            if (held < TagAutoMode.LongPressSeconds) return false;

            TagAutoMode.Toggle();   // 뒤집는 자리는 한 곳이다
            AudioSignals.Play(SoundIds.UiClick, SoundIds.KindOf(SoundIds.UiClick));
            return true;
        }

        /// <summary>태그 버튼을 누르기 시작한 시각. 음수면 안 눌린 상태다.</summary>
        private float _tagPressAt = -1f;

        /// <summary>
        /// **자동 교대 토글 + 안내 한 줄** — 태그 원형 **위에** 붙는다
        /// (2026-09-16 · 사용자 확정 · 자리는 가정 · UI 역기입 자리).
        ///
        /// ⚠️ **끄는 길이 여기 하나뿐이다.** 길게 누르기는 **켜기만** 하므로,
        /// 이 토글이 없으면 한 번 켠 자동을 못 끈다.
        ///
        /// ⚠️ **합체 중에는 안 그린다** — 태그 자체가 잠겨 있어 켜 봐야 아무 일도 없다.
        /// </summary>
        private void DrawTagAutoToggle(Rect tagRect)
        {
            // ⚠️ **원형 바로 아래**(2026-09-18 사용자 확정 · 시안 3 — 「태그 원형 → 바로 아래
            //    「자동」 토글 → 합체 원형」). 🗑️ 구 자리(원형 **위**) 폐기.
            //    자리는 `UiLayout` 한 곳이 낸다 — 여기서 재면 원형이 움직일 때 또 어긋난다.
            Rect toggle = UiLayout.TagAutoToggleRect(Screen.width, Screen.height);
            float h = toggle.height;
            if (toggle.y < 0f || toggle.yMax > Screen.height) return;
            UiBlockers.Add(toggle);

            bool on = TagAutoMode.Enabled;
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(9,
                    Mathf.Min(Mathf.RoundToInt(h * 0.42f),
                              Mathf.RoundToInt(toggle.width / 5.5f)))),
                wordWrap = false,
                clipping = TextClipping.Overflow,
                fontStyle = on ? FontStyle.Bold : FontStyle.Normal,
            };

            Color prev = GUI.color;
            if (!on) GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * 0.55f);
            // ✅ **「Auto」**(2026-09-19 사용자 확정 ②). 🗑️ 구 「자동 교대 켜짐/꺼짐」 폐기 —
            //    원형 바로 아래 좁은 판에 여섯 자가 들어가느라 판이 원형보다 넓어졌다.
            //
            // ⚠️⚠️ **켜짐·꺼짐을 글자가 말하지 않는다.** 이제 그것을 지는 것은 위의
            //    **굵기와 밝기**(꺼지면 알파 0.55) 둘뿐이다 — 글자는 같은 「Auto」다.
            //    ⚠️ 이 둘로 갈리는지는 **육안이 판정할 일**이다(가정).
            bool hit = UiSkin.Button(toggle, "Auto", style);
            GUI.color = prev;
            if (hit) TagAutoMode.Toggle();   // 길게 누르기와 **같은 값**을 뒤집는다

            // 🗑️🗑️ **안내 줄 폐기**(2026-09-19 사용자 확정 ④ — 「탭 = 교대, 길게 자동 킴/끔 제거」).
            //
            // 09-19 오전에 이 줄을 두 줄로 나눠 화면 안에 들여놨는데, **들어오고 나니
            // 아래 합체 원형을 덮었다**(사용자 스크린샷 5 — 「길게 = 자동 켬/끔」 위에
            // 「합체 / 3%」가 겹쳐 찍혔다). 자리를 또 다투게 하는 대신 **줄을 걷는다.**
            //
            // 📌 조작법은 이제 **「Auto」 토글 자체**가 말한다 — 눌러서 켜고 끄는 것이
            //    보이므로, 같은 말을 글자로 또 적을 자리가 아니다.
            // ⚠️ `TagAutoMode.Hint` **상수는 지운 게 아니라 폐기 표기**로 남는다 —
            //    되살릴 자리가 생기면 문구가 거기 그대로 있어야 한다.
        }

        /// <summary>
        /// 태그 스킬이 나갈 수 있을 때 버튼에 얹는 밝기. ⚠️ **가정**(UI 12장 역기입 자리).
        ///
        /// 색을 바꾸지 않고 **밝기만** 올린다 — 색을 바꾸면 그 색이 무엇을 뜻하는지
        /// 또 하나 외워야 하고, 조립 층의 색 축(빨강 = 못 쓴다)과 섞인다.
        /// </summary>
        private static readonly Color TagSkillReadyTint = new Color(1.7f, 1.7f, 1.2f, 1f);

        /// <summary>강조 테두리 색·굵기 — ⚠️ **가정**(UI 12장 역기입 자리).</summary>
        private static readonly Color TagSkillRingColor = new Color(1f, 0.82f, 0.25f, 0.95f);

        /// <summary>테두리 굵기(기준 캔버스 px). 얇으면 12.2mm 에서 사라진다.</summary>
        private const float TagSkillRingThickness = 10f;

        /// <summary>
        /// 태그 버튼 둘레에 **강조 테두리**를 두른다 (2026-09-16 육안 2차 ③).
        ///
        /// ⚠️ **사각 테두리다** — 버튼은 원형 그림이지만 IMGUI 로 원을 그리려면 그림이
        /// 하나 더 있어야 한다. 없는 자산을 지어내지 않고, **버튼 바깥을 둘러싸는 네 변**으로
        /// 둔다(안쪽이 비어 있어 원형 그림을 가리지 않는다).
        ///
        /// 📌 색을 **하나만** 쓴다 — 조립 층의 빨강(못 쓴다)과 겹치지 않는 노랑 계열이고,
        /// 뜻은 「지금 누르면 더 좋다」 하나다.
        /// </summary>
        private void DrawTagSkillRing(Rect r)
        {
            float t = Mathf.Max(2f, TagSkillRingThickness * UiLayout.Scale(Screen.height));
            var outer = new Rect(r.x - t, r.y - t, r.width + t * 2f, r.height + t * 2f);

            HudBars.Fill(new Rect(outer.x, outer.y, outer.width, t), TagSkillRingColor);
            HudBars.Fill(new Rect(outer.x, outer.yMax - t, outer.width, t), TagSkillRingColor);
            HudBars.Fill(new Rect(outer.x, outer.y + t, t, outer.height - t * 2f), TagSkillRingColor);
            HudBars.Fill(new Rect(outer.xMax - t, outer.y + t, t, outer.height - t * 2f), TagSkillRingColor);
        }

        /// <summary>
        /// 태그 원형의 글자. **줄을 우리가 나눈다** (2026-09-19 사용자 육안 ⑤).
        ///
        /// ⚠️⚠️ **원 안에 드는 글자는 지름이 아니라 그 안의 정사각이 받는다** — 지름 D 의
        /// 0.707 배다(<see cref="TagMergeButtons"/> 의 <c>inset</c>). 거기에 「태그 — 교대」를
        /// 한 줄로 넣으면 넘치고, IMGUI 의 <c>wordWrap</c> 은 한글을 **아무 데서나** 끊어
        /// 화면에 **「태그 — 교 / 대」**로 나왔다(육안 ⑤ · 스크린샷 1·3).
        ///
        /// 그래서 **마디마다 끊어 준다** — 첫 줄은 무엇인가, 둘째 줄은 어떤 상태인가다.
        /// 🗑️ **구 한 줄 문구 폐기**(「태그 — 교대」·「태그 (쿨다운 1.2s)」…).
        ///
        /// ✅ **평상시는 「태그」 한 낱말**(2026-09-19 사용자 확정 ① — 「태그 교대 → 태그」).
        /// 🗑️ 「태그 / 교대」 폐기 — 두 줄로 끊어 넣고 보니 **둘째 줄이 하는 말이 없었다**
        /// (버튼이 하는 일은 하나뿐이라 「교대」가 「태그」를 다시 말할 뿐이다).
        /// **평상시가 아닐 때만** 둘째 줄이 선다 — 잠금 · 쿨다운 · 스킬 준비.
        /// </summary>
        private string TagButtonLabel()
        {
            if (_sim.Tag.Locked) return "태그\n잠금";
            float cd = _sim.Tag.Tag.CooldownRemaining;
            if (cd > 0f) return $"쿨다운\n{cd:F1}s";

            // 밝기만으로는 **왜** 밝은지가 안 읽힌다 — 한 마디를 붙인다(문구 가정).
            return TagSystem.SkillReady(true,
                _sim.Tag.StandbyMount != null && _sim.Tag.StandbyMount.IsFull)
                ? "태그\n스킬"
                : "태그";
        }

        /// <summary>합체 원형의 글자. 끊는 까닭은 <see cref="TagButtonLabel"/> 과 같다.</summary>
        private string MergeButtonLabel()
        {
            if (_sim.Merge == null) return "합체\n없음";
            if (_sim.Merge.IsActive) return $"합체\n{_sim.Merge.RemainingSeconds:F1}s";
            if (_sim.Merge.UsedThisStage) return "합체\n사용함";
            return _sim.Merge.IsReady
                ? "합체\n발동"
                : $"합체\n{_sim.Merge.ChargeRatio * 100f:F0}%";
        }

        /// <summary>
        /// 회피 재고. 추진제가 곧 회피 횟수이므로 남은 개수를 상한과 함께 보여 준다.
        /// 무적 중에는 그 사실을 따로 표시한다 — 피해가 0으로 뜨는 이유가 보여야 한다.
        /// </summary>
        /// <summary>
        /// ⚠️ **추진제 유입과 누적 회피를 같이 찍는다** (2026-09-17 · 사용자 육안 ①).
        ///
        /// 「부스터가 발생하지 않음」이라는 보고가 왔는데, 화면에 있던 것은 **남은 스택** 하나라
        /// **「한 번도 안 났다」와 「나는 즉시 써서 늘 0 이다」가 같아 보였다.**
        /// 둘을 가르는 것은 **누적 횟수**이고, 왜 안 차는지를 말하는 것은 **유입**이다.
        /// </summary>
        /// <summary>쉴드 충전률 — 셈은 `ShieldSystem.ChargeFrom` 하나가 쥔다(§7).</summary>
        private static float ShieldCharge(BalanceConfig bal, float max,
                                          float materialProduce, int nodeCount)
            => bal == null ? 0f
             : ShieldSystem.ChargeFrom(max, bal.shieldChargeRatioPerSec,
                                       materialProduce, nodeCount, bal.shieldMaterialPerSec);

        private string DodgeLine()
        {
            DodgeSystem d = _sim.Dodge;
            // 상한을 부스터 대수와 함께 보여 준다 — 「노드를 더 놓으면 칸이 는다」가 화면에서 읽혀야 한다.
            string core = $"회피 {d.Stacks}/{d.Capacity} (부스터 {d.BoosterCount}대)";

            // 누적 · 유입 — 「안 나는 것」과 「나자마자 쓰는 것」을 가른다.
            core += $" · 누적 {d.TotalDodges}회 · 추진제 {_sim.PropellantSupplyRate:F3}/초";
            if (_sim.PropellantSupplyRate <= 0f) core += " ⚠️보드가 추진제를 안 만든다";

            return d.IsInvincible ? core + "  [무적]" : core;
        }

        /// <summary>
        /// 회피 눈금 바(UI 문서 11-3). **길이는 고정이고 눈금 수가 상한을 따른다** —
        /// 부스터 한 대에 두 칸이라, 노드를 놓으면 칸이 늘어나는 것이 그림으로 보인다.
        ///
        /// 상한이 열을 넘으면 눈금은 열에서 멈추고 옆에 `10+`가 붙는다. 실제로 그 위쪽이
        /// 차는 일은 스테이지 제한 시간 안에서는 거의 없다(<see cref="HudMeters.TickCount"/> 주석).
        /// </summary>
        /// <summary>
        /// HUD 의 쉴드 칸 — **HP 바로 뒤에 붙인다**(2026-09-17 · `260917_W07` 4장 4번).
        ///
        /// ⚠️⚠️ **규격이 문서에 없었다.** UI 문서 · UI 아트 요청 문서를 찾아보니 쉴드 게이지
        /// 규격이 없다(회피 스택만 「HP 바 인접」으로 서 있다). 설계 지시대로
        /// **HP 와 같은 규칙**(현재/최대 · 같은 줄 · 바로 옆)으로 넣었다. **구현 가정 · 역기입 자리.**
        ///
        /// ⚠️ **합체 중에는 두 보드의 게이지를 합산해 보인다**(사용자 확정 · `260917_W06` 5장) —
        /// 쓰는 규칙이 합산인데 화면이 한쪽만 보이면 **남은 것보다 더 버티는** 꼴이 된다.
        ///
        /// 쉴드가 없는 판(최대치 0)에서는 **빈 문자열**이다 — 없는 층을 0/0 으로 그리면
        /// 고장으로 읽힌다.
        /// </summary>
        private string ShieldLine()
        {
            float max = _sim.ShieldMax, now = _sim.Shield.Value;
            if (IsMerged && _sim.HasTagPartner)
            {
                max += _sim.StandbyShieldMax;
                now += _sim.StandbyShield.Value;
            }
            if (max <= 0f) return string.Empty;

            string merged = IsMerged && _sim.HasTagPartner ? "(합산)" : string.Empty;
            return $"   쉴드{merged} {now:F0}/{max:F0}";
        }

        // 🗑️ **폐기 — `DrawDodgeTicks`**(2026-09-18 · 시안 3). `GUILayout` 세로 블록
        //    안에서만 쓰이던 자리다. 회피 눈금은 이제 **로봇 몸 밑**에 직접 그린다
        //    (<c>StageRunner.Hud.cs</c> 의 <c>DrawRobotGauges</c>).

        /// <summary>
        /// 탄약 줄(UI 문서 3-3) — **저장 노드 재고**를 막대 하나로 나눠 그린다.
        ///
        /// ⚠️ 위의 <see cref="AmmoLine"/>은 **초당 소비**를 적는다. 둘은 다른 것을 잰다 —
        /// 하나는 지금 있는 발수이고 하나는 빠져나가는 속도다. 같은 줄에 섞지 않는다.
        ///
        /// 색은 <see cref="TracerColor"/>를 그대로 쓴다. 화면에 날아가는 탄선과 창고 칸이
        /// 다른 색이면 「저 노란 것이 다 떨어졌다」가 안 읽힌다.
        /// </summary>
        // 🗑️ **폐기 — `HudTextHeight`**(2026-09-18 · 시안 3). 줄 높이를 더해 방을 재던
        //    셈이다. **글자 블록이 없어졌으니 잴 것이 없다** — 09-14 에 세 줄을 잃고 들인
        //    장치였고, 그 병 자체를 꼴을 바꿔 없앴다.

        private void DrawAmmoBar()
        {
            _ammoSegments.Clear();
            for (int i = 0; i < AmmoKinds.Length; i++)
            {
                AmmoKind k = AmmoKinds[i];
                _ammoSegments.Add(new HudBars.Segment
                {
                    label = AmmoLabelOf(k),
                    value = _sim.AmmoStockOf(k),
                    color = TracerColor(k),
                });
            }
            GUILayout.BeginHorizontal();
            // 0인 탄종도 칸을 유지한다 — 숨기면 「안 만들고 있다」와 「다 썼다」가 같은 화면이 된다
            // (2026-09-09 사용자 확정 · UI 문서 3-3).
            HudBars.Segments(HudBars.Row(300f), _ammoSegments, _sim.AmmoCapacity, _hudSegment,
                keepEmpty: true);
            // ⚠️ **「창고」다**(2026-09-14 정정) — 마운트 적재와 **다른 층**이다.
            // 「재고」로 적어 두니 「마운트 적재 30」과 나란히 놓였을 때 **모순으로 읽혔다.**
            GUILayout.Label($"창고 {_sim.AmmoStock:F0}/{_sim.AmmoCapacity:F0}", _hudSmall);
            GUILayout.EndHorizontal();
        }

        private static readonly AmmoKind[] AmmoKinds =
            { AmmoKind.Pierce, AmmoKind.Standard, AmmoKind.Explosive };

        private readonly List<HudBars.Segment> _ammoSegments = new List<HudBars.Segment>(3);

        private static string AmmoLabelOf(AmmoKind kind)
        {
            switch (kind)
            {
                case AmmoKind.Pierce: return "관통";
                case AmmoKind.Standard: return "표준";
                default: return "폭발";
            }
        }

        /// <summary>
        /// 태그 상태 한 줄. 만충 판정 주체는 **마운트**다(창고가 아니다).
        /// ⚠️ 탄약·드론 스택 상한이 미확정이라 마운트 만충이 성립하지 않는다 —
        /// 지금 자동 교대를 여는 것은 「활성 소진 + 대기에 남음」 쪽뿐이다.
        /// </summary>
        private string TagLine()
        {
            string who = _sim.ActiveRobotIndex == 1 ? "로봇B(드론)" : "로봇A(탄약)";
            MountLoad act = _sim.Tag.ActiveMount;
            MountLoad standby = _sim.Tag.StandbyMount;
            // 상한이 하나도 없으면 Capacity가 0이다 — 이때는 「채우는 중」이 아니라 **판정 자체가 없다**.
            // 한 칸이 얼마든 받아 나머지 칸이 안 열리므로 만충이 영영 서지 않는다.
            string fullness = standby.Capacity <= 0f
                // ⚠️ 괄호를 뗐다(260901_V02 판정 3). TBD는 문서에서 하는 말이지
                // 심사자가 볼 화면에서 하는 말이 아니다. 스택이 확정되면 이 갈래 자체가 사라진다.
                ? "만충 판정 대기"
                : (standby.IsFull ? "만충" : "채우는 중");
            // ⚠️ **진단 — ④ 「B 물류가 태그 전엔 안 찬다」를 가르는 줄**(2026-09-21).
            //    배선을 다 읽었는데 셋 중 어디서 끊기는지는 **게임이 돌아야** 갈린다.
            //    도착률이 0 이면 판/브릿지 쪽, 0 이 아닌데 적재가 안 늘면 칸 쪽이다.
            //    ⚠️ 답이 오면 이 줄은 걷는다 — 심사자 화면에 남길 글이 아니다.
            string standbyFeed =
                $"   ·   대기 도착 {LogisticsOutputBridge.StandbyStackDroneArrivalRate:F2}"
                + $"/{LogisticsOutputBridge.StandbyAoeDroneArrivalRate:F2}/초"
                + $"   ·   대기 칸 {standby.Total:F1}/{standby.Capacity:F0}";

            return $"출전 {who}   ·   마운트 적재 {act.Total:F0}   ·   대기 마운트 {standby.Total:F0} ({fullness})"
                   + MountItemBreakdown(standby)
                   + $"   ·   드론 {_sim.Drones.Count}기"
                   + standbyFeed;
        }

        /// <summary>
        /// 마운트에 **무엇이** 실려 있는가 — 품목 칸 (2026-09-16 · 사용자 확정 · §74-16 ③).
        ///
        /// ⚠️⚠️ **합만으로는 교대를 못 정한다.** 「대기 마운트 40」은 가득이라는 말일 뿐,
        /// 그것이 **쓸 수 있는 탄인지**는 말하지 않는다 — B 는 드론을 쓰는데 표준탄이
        /// 40 실려 있으면 합은 만충이지만 교대해도 못 쏜다(⑤ 결함이 그 자리다).
        ///
        /// ⚠️ **빈 칸은 안 적는다** — 0 을 늘어놓으면 줄만 길어지고 읽히지 않는다.
        /// 아무것도 없으면 통째로 빈 문자열이라 줄이 그대로 짧아진다.
        ///
        /// ⚠️ **글자다 — 칸 그림이 아니다**(가정 · UI 역기입 자리). 전투 HUD 는 글자 줄로
        /// 서 있고, 여기만 그림을 넣으면 줄 높이가 혼자 달라진다.
        /// </summary>
        private static string MountItemBreakdown(MountLoad mount)
        {
            if (mount == null || mount.Total <= 0f) return "";

            var sb = new System.Text.StringBuilder();
            foreach (MountItem item in MountItemsShown)
            {
                float amount = mount.AmountOf(item);
                if (amount <= 0f) continue;
                sb.Append(sb.Length == 0 ? " [" : " · ");
                sb.Append($"{MountItemLabel(item)} {amount:F0}");
            }
            if (sb.Length > 0) sb.Append(']');
            return sb.ToString();
        }

        /// <summary>줄에 적는 품목과 그 차례. ⚠️ 차례는 가정이다(탄 셋 → 드론).</summary>
        private static readonly MountItem[] MountItemsShown =
        {
            MountItem.Pierce, MountItem.Standard, MountItem.Explosive, MountItem.Drone,
        };

        private static string MountItemLabel(MountItem item)
        {
            switch (item)
            {
                case MountItem.Pierce: return "관통";
                case MountItem.Standard: return "표준";
                case MountItem.Explosive: return "폭발";
                case MountItem.Drone: return "드론";
                default: return item.ToString();
            }
        }

        /// <summary>물류 출력 이중표시(예상/실제/갭) + 전역 원인(전력/발열) 점멸(§L4-R #1·#5 변수패널 1차 표시자).</summary>
        private string OutputLine()
        {
            float exp = LogisticsOutputBridge.Expected;
            float act = LogisticsOutputBridge.Output;
            float gap = LogisticsOutputBridge.Gap;
            // ⚠️ **요구치가 없는 스테이지에서는 요구치를 말하지 않는다.** 스테이지 0은 전투가 없어
            // req가 0인데, 그대로 그리면 화면이 「요구 0 [충족 80]」이라고 한다 —
            // 없는 것을 말하고 판정까지 내리는 것이다(2026-09-01 브라우저 실측, A구간에 찍힌다).
            string req = HasRequirement ? $"  /  요구 {ReqLabel()}{ReqBadge()}" : "";
            // ⚠️ **「물류 실제」다**(2026-09-14 정정) — 이 값은 **물류 산출률**이고
            // 하네스의 「완료 출력」(마운트 도착 수 환산)과 **정의가 다르다.**
            // 그냥 「실제」로 두니 둘을 같은 자를 잰 값으로 읽게 됐다.
            string line = $"물류 출력  예상 {exp:F0} · 물류 실제 {act:F0} · 갭 {gap:F0}{req}  ·  마운트계수 {_mountCoef:F2}";
            string badge = CauseBadge();
            return badge.Length > 0 ? line + "   " + badge : line;
        }

        /// <summary>전역 병목 원인 배지 — Power→Heat 우선, 점멸(변수패널 1차 표시자). 아이콘 에셋 전 텍스트 placeholder.</summary>
        private static string CauseBadge()
        {
            if (!Blink()) return "";
            switch (LogisticsOutputBridge.GlobalCause)
            {
                case ConstraintCause.Power: return "[전력 부족]";
                case ConstraintCause.Heat: return "[발열 초과]";
                default: return "";
            }
        }

        private static bool Blink() => ((int)(Time.unscaledTime * 2.5f) & 1) == 0;

        /// <summary>
        /// 탄약 표시(§C-2) — 마운트 용량(종당) + **지금 실제로 쏘는 발사율**(발/초).
        ///
        /// ⚠️ **2026-09-14 정정.** 여기서 `robot.weapons[].shotsPerSec` 를 읽고 있었다 —
        /// 그것은 **자산의 정적 스펙**이고, 실제 발사는 `ShotAllocator` 가 물류 배율을 곱해
        /// 만든 **라인 값**(`_lineBuffer`)이다. 주석은 「현재 물류 공급율」이라고 적혀 있는데
        /// 화면에는 물류와 무관한 수가 떠 있었다 — **읽는 사람이 없는 이상을 본다.**
        /// (「표준 1발/초인데 시작 보드가 네 줄이면 4여야 한다」가 여기서 나왔다.)
        ///
        /// ⚠️ **규칙은 안 바꿨다.** 발사율이 공급율에 묶여 있다는 것은 그대로이고,
        /// 화면이 **그 묶인 값**을 보이게 된 것뿐이다.
        /// </summary>
        // ── 실제로 쏜 발수 (2026-09-15 사용자 확정 ㉮) ───────────────────────
        //
        // ⚠️ **배분을 찍고 있었다.** `_lineBuffer` 는 `ShotAllocator` 가 낸 **상한**이고,
        // 마운트에 그 탄이 없으면 `ConsumeRound` 가 실패해 한 발도 안 나간다 —
        // 표준탄만 오는 시작 보드에서 「관통 0.2 · 폭발 0.5 발/초」로 찍혔고 **거짓이었다.**
        // 이제 시뮬의 **누적 발사 수**를 창으로 나눠 **난 발**을 찍는다.
        private readonly int[] _firedMark = new int[3];
        private readonly float[] _firedRate = new float[3];
        private float _firedWindow;

        /// <summary>발사율 창(초). ⚠️ 가정 — 짧으면 숫자가 튀고 길면 굼뜨다.</summary>
        private const float FiredSampleSeconds = 1f;

        private void TickFiredRates(float dt)
        {
            if (_sim == null) return;

            _firedWindow += dt;
            if (_firedWindow < FiredSampleSeconds) return;

            for (int i = 0; i < _firedRate.Length; i++)
            {
                int now = _sim.FiredOf((AmmoKind)i);
                _firedRate[i] = (now - _firedMark[i]) / _firedWindow;
                _firedMark[i] = now;
            }
            _firedWindow = 0f;
        }

        private string AmmoLine()
        {
            int cap = Mathf.RoundToInt(robot.consumptionCap); // capA — RobotDefinition 단일 소스(§3)
            return $"탄약 마운트(용량 {cap}/종)  관통 {_firedRate[(int)AmmoKind.Pierce]:F1} · " +
                   $"표준 {_firedRate[(int)AmmoKind.Standard]:F1} · " +
                   $"폭발 {_firedRate[(int)AmmoKind.Explosive]:F1} 발/초 (실제)";
        }

        /// <summary>
        /// 화면에 뜨는 스테이지 이름 — **튜토리얼에는 번호를 붙이지 않는다**(260902_W09 §2).
        ///
        /// 이름에 0이 남아 있으면 스테이지 1~6과 같은 줄에 선 것으로 읽히고,
        /// 그러면 「요구치 없음 · 보상 없음 · 선택 불가 · 실패 없음」이 전부 예외로 보인다.
        /// 종류가 다른 것이지 순번이 앞선 것이 아니다.
        ///
        /// ⚠️ 코드 식별자(<c>stageId</c> = "S0")는 그대로다 — 저장의 클리어 목록 키이기도 하고,
        /// 게이트 2가 9월 4일이다. 읽는 사람이 있는 곳만 바꾼다.
        /// </summary>
        private string StageTitle() =>
            stage.stageId == IdleSignals.TutorialId
                ? "튜토리얼 전용 스테이지"
                : $"스테이지 {stage.stageId}";

        /// <summary>요구치가 있는 스테이지인가. 전투가 없는 튜토리얼에는 없다.</summary>
        private bool HasRequirement =>
            stage != null && (stage.reqType != StageReqType.Fixed || stage.req > 0f);

        private string ReqLabel()
        {
            switch (stage.reqType)
            {
                case StageReqType.Fixed: return $"{stage.req:F0}";
                case StageReqType.Band: return $"[{stage.reqBand.x:F0},{stage.reqBand.y:F0}]";
                default: return stage.reqType.ToString();
            }
        }

        /// <summary>
        /// 요구치 대비 배지(§5-6 F). 판정은 StageRequirement(Core)가 하고 여기서는 문구만 붙인다.
        /// 전투력 = 브릿지 출력(물류 단위) × 마운트계수 — 마운트계수는 판정식 내부 항이라 전투 측에서 곱한다.
        /// 승패에는 관여하지 않는다(문서가 정한 통과 조건은 전원처치/보스형).
        /// </summary>
        private string ReqBadge()
        {
            float power = LogisticsOutputBridge.Output * _mountCoef;

            // 🗑️ 구 꼴 「[부족 18]」 폐기 (2026-09-17 · 사용자 육안) — 괄호 안 수가
            //    「모자란 양」으로 읽혔고, 반올림까지 겹쳐 「요구 18 · 부족 18」이 떴다.
            //    문구는 `StageRequirement.Badge` 하나가 낸다(시험이 그것을 본다).
            return StageRequirement.Badge(stage.reqType, stage.req, stage.reqBand, power);
        }

        private string ResultText()
        {
            switch (_sim.Result)
            {
                case CombatResult.Win: return "승리 — 적 전멸";
                case CombatResult.LoseDead: return "패배 — 로봇 파괴";
                case CombatResult.LoseTimeout: return "패배 — 시간 초과";
                default: return "";
            }
        }
    }
}
