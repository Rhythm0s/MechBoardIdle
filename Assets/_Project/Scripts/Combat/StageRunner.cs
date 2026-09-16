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
    public sealed class StageRunner : MonoBehaviour
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

        /// <summary>
        /// 조립 화면에서 HUD 글자 블록을 접었는가 (2026-09-15 사용자 확정 · 육안 ③).
        ///
        /// ⚠️ **사람마다 다른 선택이라 기억한다** — 보드를 넓게 보고 싶은 사람과 수치를
        /// 계속 보고 싶은 사람이 갈린다. 매번 다시 접게 하면 그 선택이 조작이 된다.
        ///
        /// ⚠️ **전투 화면은 안 접는다** — 거기서는 이 글자가 화면의 본문이다.
        /// </summary>
        private static bool HudFolded
        {
            get => PlayerPrefs.GetInt(HudFoldedKey, 0) != 0;
            set => PlayerPrefs.SetInt(HudFoldedKey, value ? 1 : 0);
        }

        private const string HudFoldedKey = "mbi.hud.folded";
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

        private MountLoad MountB(float stack) =>
            _mountB ?? (_mountB = new MountLoad(MountLoad.SlotsRobotB, MountLoad.StandardStacks(stack)));

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
                hp = tuning.robotHpTbd,
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
                hp = tuning.robotHpTbd,
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
        private void PlayTagSkillEffect()
        {
            if (_sim == null) return;
            // 마운트가 만재가 아니어서 스킬이 안 터졌으면 연출도 없다 — 사건 종속(10-1).
            if (_sim.LastTagSkillDamage <= 0f) return;

            float radius = tuning != null ? tuning.arenaRadiusTbd : 6f;
            Vector2 origin = _sim.Robot != null ? _sim.Robot.position : Vector2.zero;
            bool bEntering = _sim.ActiveRobotIndex == 1;
            Color c = bEntering ? RobotBColor : RobotAColor;

            if (bEntering)
            {
                // 레이저는 자산이 없다 — 흰 사각을 늘여 그리는 것이 곧 완성형이다(연출 3-1).
                TagSkillEffect.PlayLaser(transform, origin, radius, tuning, PlaceholderSprite.White(), c);
            }
            else
            {
                // 탄환은 아트 자산이 있다. 아직 안 들어왔으면 자리표시로 폴백한다.
                Sprite bullet = tuning != null && tuning.tagBulletSprite != null
                    ? tuning.tagBulletSprite : PlaceholderSprite.White();
                bool real = bullet != PlaceholderSprite.White();
                // 자산에는 색이 이미 실려 있다 — 흰 사각일 때만 색을 입힌다.
                Color tint = real ? Color.white : c;
                TagSkillEffect.PlayBulletRain(transform, origin, radius, tuning, bullet, tint, real);
            }
        }

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

            if (tuning.droneLaunchSprite != null)
                foreach (Vector2 p in _sim.DroneLaunchesThisTick)
                    SpawnOneShot(tuning.droneLaunchSprite, p, life);

            if (tuning.droneExpireSprite != null)
                foreach (Vector2 p in _sim.DroneExpiriesThisTick)
                    SpawnOneShot(tuning.droneExpireSprite, p, life);

            // 회피는 **시작 순간에만 1회**다(연출 4장). 시뮬이 세는 누적 횟수가 늘어난
            // 프레임이 그 순간이며, 그 값 하나로 자동·수동을 가리지 않는다 — 둘 다 회피다.
            DodgeSystem dodge = _sim.Dodge;
            if (dodge == null) { _seenDodges = 0; return; }

            if (dodge.TotalDodges > _seenDodges && tuning.boosterSprite != null &&
                _sim.Robot != null)
                SpawnOneShot(tuning.boosterSprite, _sim.Robot.position, life);

            _seenDodges = dodge.TotalDodges;
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
                // ⚠️ **크기는 가정 0.25 유닛**(격자 한 칸의 1/4). 연출 문서에 적 포탄 절이 없다.
                go.transform.localScale = Vector3.one * ProjectileViewUnits;
                _projectileViews.Add(sr);
            }

            for (int i = 0; i < _projectileViews.Count; i++)
            {
                SpriteRenderer sr = _projectileViews[i];
                bool on = i < live.Count;
                if (sr.gameObject.activeSelf != on) sr.gameObject.SetActive(on);
                if (on) sr.transform.position = live[i].position;
            }
        }

        private void SpawnOneShot(Sprite sprite, Vector2 position, float seconds)
        {
            var go = new GameObject("Vfx");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(position.x, position.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = SortingLayers.EffectOver;
            Destroy(go, seconds);
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
        /// <summary>적 포탄 자리표시의 한 변(월드 유닛). ⚠️ 가정 — 연출 문서에 절이 없다.</summary>
        private const float ProjectileViewUnits = 0.25f;

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

            // 결과가 나도 끝까지 튼다 — 버스트로 마지막 적이 죽으면 연출이 그 프레임에 끊긴다.
            _cutscene.Tick(Time.deltaTime);

            bool running = _sim.Result == CombatResult.InProgress;

            if (running)
            {
                RefreshFireRate();
                // 창고 유입 = 라이브 군수 생산율. 재고가 마르면 발사가 멈춘다(탄약 소진 = 공격 정지).
                _sim.AmmoSupplyRate = LogisticsOutputBridge.AmmoProduce;
                // 드론 몸체·추진제도 같은 보드에서 온다. 사출대·부스터가 각각 받아 화력과 생존이 된다.
                _sim.DroneInflowRate = LogisticsOutputBridge.DroneProduce;
                _sim.PropellantSupplyRate = LogisticsOutputBridge.PropellantProduce;
                // 회피 스택 상한은 부스터 대수의 파생값이다 — 노드를 뽑으면 그 자리에서 줄어든다.
                _sim.BoosterCount = LogisticsOutputBridge.BoosterCount;
                // ⚠️ 대기 로봇의 유입은 주입하지 않는다. 보드는 로봇의 몸이라 로봇마다 하나인데
                // 지금 씬에는 보드가 한 장뿐이다 — 같은 값을 양쪽에 넣으면 보드 한 장이
                // 두 배를 생산하게 된다. 두 번째 보드가 생기면 여기 한 줄이 붙는다.
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

                if (mv == Vector2.zero && autoPilot && Time.time >= _manualHoldUntil)
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

            // ⚠️ **매 프레임 넣는다**(2026-09-16 · 사용자 확정 §74-16 ③).
            //    설정은 `PlayerPrefs` 에 살고 버튼이 그것을 바꾸므로, 한 번만 넣으면
            //    켜고 끈 것이 이번 판에 안 먹는다. 캐지 않는 까닭도 같다(지침 §7).
            _sim.AutoTagEnabled = TagAutoMode.Enabled;

            // ⚠️ **드론 이동 값 넷은 가정이다**(`CombatTuning` 의 `drone*Tbd`) —
            //    코드가 숫자를 들지 않게 SO 에서 매 프레임 넣는다(§3).
            if (tuning != null)
            {
                _sim.DroneOrbitRadius = tuning.droneOrbitRadiusTbd;
                _sim.DroneOrbitSpeed = tuning.droneOrbitSpeedTbd;
                _sim.DroneFlySpeed = tuning.droneFlySpeedTbd;
                _sim.DroneAttachDistance = tuning.droneAttachDistanceTbd;
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
            if (_sim.TagSkillResolvedThisTick) PlayTagSkillEffect();

            PlayInstalledVfx();
            SyncEnemyProjectileViews();

            // 날아가는 탄환은 **연출 전용**이라 시뮬 배속이 아니라 실제 시간으로 간다 —
            // 피해는 이미 들어갔고 그림만 뒤따르기 때문이다(§72-24 ④).
            TickFlyingShots(Time.deltaTime);

            TickFiredRates(Time.deltaTime);
            UpdateAmmoOutView();   // 지속 상태 — 한 번 만들고 껐다 켠다(`260909_W01` 3장이 (가)를 확정)
            PublishSupplySignals();

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
                    sr.sortingOrder = SortingLayers.Actor;
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

            // ⚠️ **기본 skin 의 회색을 안 쓴다**(2026-09-15 · 육안 ⓐ 대응 · 09-10 과 같은 처방).
            //
            // 전투 화면에서는 이 글자 밑에 **판을 안 깐다**(조립 화면만 깐다). 그래서 회색
            // 글자가 **흙바닥 위**에 놓이는데, 09-15 에 카메라가 로봇을 따라가게 되면서
            // 밑에 오는 타일이 달라졌다 — 플랜 세션 육안에서 **HUD 가 통째로 안 보였다.**
            //
            // ⚠️ **원인을 못 좁혔다.** 회색 대비가 맞는지, 다른 것이 가린 것인지 가르지
            // 못했다 — 다만 흰색은 **어느 쪽이든 나빠지지 않는다**(같은 줄의 다른 글자가
            // 이미 흰색이라 색이 섞여 있던 것도 함께 풀린다).
            // 글자 크기도 창을 따라가게 한다 — 16 은 날 픽셀이라 큰 창에서 점이 된다.
            float hudScale = UiLayout.Scale(Screen.height);
            int hudPx = KoreanFont.Snap(Mathf.Max(11, Mathf.RoundToInt(32f * hudScale)));

            var style = new GUIStyle(GUI.skin.label) { fontSize = hudPx };
            style.normal.textColor = Color.white;
            var big = new GUIStyle(GUI.skin.label)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(16, Mathf.RoundToInt(68f * hudScale))),
                fontStyle = FontStyle.Bold,
            };
            big.normal.textColor = Color.white;
            // 막대 옆·막대 안 글자. 막대 높이가 14라 16으로 두면 칸 밖으로 넘친다.
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

            // ⚠️ **조립 화면에서는 이 글자가 전투 그림 위에 얹힌다**(2026-09-10 · 플랜 §66-35 ②).
            // 상단 인셋을 켠 대가라, 글자가 있는 자리만 어둡게 깔아 대비를 되돌린다.
            // **블록 단위로만** 깐다 — 위쪽을 통째로 덮으면 전투를 그린 뜻이 사라진다.
            // ⚠️ **날 픽셀 자리를 전투 띠 안으로 가둔다**(2026-09-14 · §72-12 1).
            // `(12, 10, 560, 280)` 은 **창 크기와 무관한 고정 자리**라, 창이 작으면
            // 그 사각이 **전투 인셋과 아래 띠를 통째로 가린다.**
            //
            // ⚠️ **비례로 줄이지 않는다** — 그러면 작은 화면에서 글자가 못 읽게 된다.
            //
            // ⚠️ **세로 280 은 폐기**(2026-09-14 오후 · 2차 스크린샷). 가로 560 만 남았다 —
            // 아래에서 보듯 높이는 이제 **글자에서 잰다.** 「큰 화면에서는 종전 그대로
            // 560×280」이던 종전 문장도 함께 폐기한다.
            Rect combatBand = UiLayout.BandRect(UiLayout.Band.Combat, Screen.width, Screen.height);

            // ⚠️ **줄을 먼저 짓고 높이를 거기서 잰다**(2026-09-14 · 2차 스크린샷 두 장).
            //
            // 종전 높이는 `280` **날 픽셀 고정**이었다. 그 사이 줄이 열하나로 늘고
            // 태그 줄이 길어 **두 줄로 접히면서** 밑이 넘쳤고, 화면에서는
            // **「경과」·「고철」·「이동 WASD」 세 줄이 통째로 사라졌다** — 2차 두 장 모두 그렇다.
            //
            // 줄 수는 로봇B 유무·줄바꿈에 따라 변하므로 **고정값을 다시 박을 수 없다** —
            // 스타일에 물어 `CalcHeight` 로 재고 막대 둘을 더한다.
            // ⚠️ **첫 줄이 「지금 무엇을 해야 하는가」를 말한다**
            // (2026-09-15 사용자 확정 · 육안 6차 ③ — 「할 일이 없으면 할 일을 만들어야지,
            //  그래서 목표가 뭐냐」).
            //
            // 종전 첫 줄은 **「스테이지 1 · 벨트 연결(온보딩)」** 이었다. 어디에 있는지는
            // 말하지만 **무엇을 하면 끝나는지는 안 말한다.** 튜토리얼은 두 줄로 그것을
            // 말하는데(「끊긴 자리를 잇는다」·「마운트가 가득 찬다」), 튜토리얼을 벗어나면
            // 그 안내가 **통째로 사라졌다** — 그래서 할 일이 없어 보인다.
            //
            // ⚠️ **값은 지어내지 않는다** — 주제와 요구치 둘 다 `balance_v4.json` 의
            // `stages[].topic` · `stages[].req` 다. 요구치가 없는 스테이지(튜토리얼)에서는
            // 그 꼬리를 안 붙인다 — 0 을 「넘기라」고 적으면 거짓이 된다.
            // 문안 자리는 설계 역기입 대상이다.
            string lineTitle = HasRequirement
                ? $"{StageTitle()} 목표: {stage.topic}  ·  출력 {stage.req:F0} 넘기기"
                : $"{StageTitle()}  ·  {stage.topic}";
            string lineOutput = OutputLine();
            string lineAmmo = AmmoLine();
            string lineStore = $"저장고(군수 생산) {LogisticsOutputBridge.AmmoProduce:F1} 발/초";
            string lineEnemy = $"적 {_sim.Remaining}/{_sim.TotalEnemies}   로봇 HP {_sim.Robot.hp:F0}/{_sim.Robot.maxHp:F0}" +
                               $"   {DodgeLine()}";
            string lineTag = robotB != null ? TagLine() : null;
            string lineElapsed = $"경과 {_sim.Elapsed:F1}s / {stage.challengeTime:F0}s";
            string lineWallet = $"고철 {IdleSignals.WalletScrap:N0}   ·   강화재료 {IdleSignals.WalletEnhMaterial:N0}";
            const string lineHelp = "이동 WASD / 화살표   ·   회피 = 스페이스 / 화면 플릭";

            // ⚠️ **진단 한 줄**(2026-09-15 · 육안 8차 ① — 「쏘는 쪽을 안 본다」).
            //
            // 코드는 맞아 보이는데 화면에서는 안 돈다. **어느 단에서 끊기는지 눈으로 못 가른다** —
            // 덮어쓰기가 안 걸린 것인지(수동으로 읽힘 · 조준이 안 옴 · 1.5초가 지났다),
            // 걸렸는데 벌이 안 바뀐 것인지. 그래서 **그 값을 그대로 찍는다.**
            //
            // 📌 오늘 튜토리얼 게이트도 이 방법으로 갈랐다 — **재는 것으로 못 잡는 자리**는
            // 화면이 답하게 한다. 자리가 잡히면 이 줄은 걷는다.
            string lineFace = _robotView == null ? "얼굴 — 뷰 없음"
                : $"얼굴 {(_robotView.FacingOverride.HasValue ? "조준" : "이동")}"
                  + $" · 마지막 조준 {(_lastAimAt > 0f ? (Time.time - _lastAimAt).ToString("F1") + "초 전" : "없음")}"
                  + $" · 수동유예 {Mathf.Max(0f, _manualHoldUntil - Time.time):F1}s";

            // ⚠️ **날 픽셀 폭 560 을 걷었다**(2026-09-15 · 육안 ②).
            //
            // 글자만 배율을 먹이고 **자리는 안 고쳤더니**, 720×1280 세로 창에서 HUD 가
            // **화면 왼쪽 밖으로 잘렸다** — 「(부」·「마운」만 보였다.
            //
            // 전투 화면은 **레이어 1**(절대 좌표)이다. 상태창이 x40 에서 시작하므로
            // (`UiLayout.StatusPanelOrigin`) 그 자리를 쓰고, 폭은 기준 캔버스 **760**
            // (상태창이 화면 절반을 넘지 않는 선 · ⚠️ 가정)으로 잡아 배율을 곱한다.
            float hudLeft = UiLayout.StatusPanelOrigin.x * hudScale;
            float hudW = Mathf.Min(760f * hudScale, Screen.width - hudLeft * 2f);

            // ⚠️⚠️ **글자 크기를 높이로만 정하고 있었다**(2026-09-15 · 사용자 육안 7차).
            //
            // `hudPx` 는 `32 × Scale(화면 높이)` 다. **좁고 긴 창**에서는 높이가 커서 글자가
            // 커지는데 **폭은 창을 따라 좁아진다** — 그래서 「창고 0/40」이 한 글자씩
            // 세로로 쌓였다. 큰 글자가 좁은 칸에 들어가면 줄바꿈이 **글자 단위**가 된다.
            //
            // 📌 **같은 병을 오늘 세 번째 고친다** — 카테고리 탭(가로가 좁다) ·
            // 마운트 이름표(폭 측정) · 여기. **크기는 두 변에서 잡아야 한다.**
            //
            // 가장 긴 줄이 한 줄에 들어갈 만큼으로 상한을 둔다 — 넘치면 줄바꿈이 나되
            // **글자 단위로는 안 쪼개진다.**
            {
                int byWidth = Mathf.RoundToInt(hudW / 22f);   // 대략 22 글자가 한 줄
                int capped = Mathf.Max(11, Mathf.Min(hudPx, byWidth));
                if (capped < hudPx)
                {
                    hudPx = KoreanFont.Snap(capped);
                    style.fontSize = hudPx;
                    _hudSmall.fontSize = hudPx;
                }
            }
            float need = HudTextHeight(style, hudW - 10f,
                             lineTitle, lineOutput, lineAmmo, lineStore, lineEnemy, lineTag,
                             lineElapsed, lineWallet, lineHelp)
                         + (HudBars.BarHeight + 4f) * 2f   // 탄약 막대 · 회피 눈금
                         + 10f;                            // 아랫변 여백

            // ⚠️⚠️ **띠가 가두는 것은 조립 화면에서뿐이다.** 띠 다섯은 **레이어 2** 의 수이고
            // 전투 화면은 **레이어 1**(절대 좌표)이라 768 과 견줄 것이 아니다 — `UiLayout` 의
            // 주석이 「두 수는 다른 화면의 수」라고 이미 적어 둔 그 자리다. 전투 화면까지
            // 768 로 가두면 **아래 줄을 잃을 이유가 없는데 잃는다.**
            // ⚠️⚠️ **방을 잴 때 시작점을 빼야 한다**(2026-09-15 · 육안 점검에서 잡았다).
            //
            // 종전에는 조립 화면에서 `combatBand.height - 20` 을 방으로 썼다. 그런데 글자는
            // 띠 맨 위가 아니라 **`InfoBarHeight` 만큼 내려와서** 시작한다 — 그 높이를
            // 안 뺐으니 **방을 그만큼 넘겨 잡았다.** 결과로 HUD 가 전투 띠를 **넘어 보드
            // 위까지** 내려왔고, 화면에서는 경고 띠와 노드가 그 위를 덮어 **글자가 잘린
            // 것처럼** 보였다(실제로는 잘린 게 아니라 **가려진** 것이다).
            //
            // 📌 **띠 안에 가두는 것이 목적이면 띠의 아랫변에서 재야 한다** — 높이에서
            // 빼는 것과 아랫변에서 재는 것은 시작점이 0 일 때만 같다.
            float hudTop = combatBand.y + UiLayout.InfoBarHeight * hudScale;
            float room = GameViewSignals.BoardViewActive
                ? Mathf.Max(0f, combatBand.yMax - hudTop - 10f)
                : Mathf.Max(0f, Screen.height - hudTop - 20f);

            // ⚠️ **방이 모자라면 글자를 줄인다 — 줄을 버리지 않는다.**
            //
            // `GUILayout.BeginArea` 는 넘치는 것을 **말없이 자른다.** 방만 맞추고 끝내면
            // 아래 줄들이 **에러 없이 사라진다** — 09-14 에 「경과·고철·이동 WASD」 세 줄이
            // 통째로 없어졌던 그 모양이다. 줄 수는 화면이 정할 것이 아니므로 크기를 줄인다.
            if (need > room && room > 0f)
            {
                int shrunk = Mathf.Max(9, Mathf.RoundToInt(style.fontSize * room / need));
                if (shrunk < style.fontSize)
                {
                    style.fontSize = KoreanFont.Snap(shrunk);
                    need = HudTextHeight(style, hudW - 10f,
                               lineTitle, lineOutput, lineAmmo, lineStore, lineEnemy, lineTag,
                               lineElapsed, lineWallet, lineHelp)
                           + (HudBars.BarHeight + 4f) * 2f + 10f;
                }
            }

            // ⚠️ **화면 안으로 가둔다**(2026-09-15 · 육안 ②).
            //
            // 자리를 띠 체계로 옮기고도 **여전히 위아래가 잘렸다.** 어느 셈이 넘치는지
            // 아직 못 짚었으므로, **결과를 화면 안으로 클램프**한다 — 어느 DPR 에서도
            // 잘리지 않게 하는 것이 먼저다. 넘치는 셈을 찾으면 이 클램프는 아무 일도 안 한다.
            float hudH = Mathf.Min(need, room);
            float hudY = hudTop;
            if (hudY + hudH > Screen.height) hudY = Mathf.Max(0f, Screen.height - hudH);
            if (hudH > Screen.height) hudH = Screen.height;

            var hud = new Rect(hudLeft, hudY, hudW, hudH);

            // ⚠️ **조립 화면에서는 이 글자 블록을 접을 수 있다**(2026-09-15 사용자 확정 · 육안 ③).
            //
            // 조립 중에 이 열한 줄은 **전투 그림 위에 얹혀** 보드로 가는 눈길을 가로챈다.
            // 접으면 **글자 블록만** 사라진다 — 인셋 전투 그림과 경고 띠는 그대로다.
            // 전투 화면에서는 접지 않는다: 거기서는 이 글자가 화면의 본문이다.
            //
            // ⚠️ **가정이다** — 문서에 접기 절이 없다(설계 역기입 자리). 버튼 크기·자리는
            // 눌리는 최소(44)를 기준으로 잡았다.
            // ⚠️⚠️ **진단 줄은 HUD 밖으로 내보낸다**(2026-09-15 오후 · 사용자 「안 보인다」).
            //
            // 종전엔 HUD 글자 블록의 **마지막 줄**이었다 — 그것이 이 화면에서
            // 가장 약한 자리다. `GUILayout.BeginArea` 는 넘치는 것을 **말없이 자르고**,
            // 잘리는 것은 항상 마지막 줄이다. 게다가 접기·판 깔기가 전부 이 블록에 걸려
            // 「안 보인다」의 갈래가 넷이나 된다.
            //
            // 📌 **진단물은 진단하려는 것과 같은 배에 타면 안 된다.** 그래서 따로 놓는다 —
            // 접기와 무관 · HUD 높이 셈과 무관 · 둘 화면 모두 · 항상.
            //
            // 자리는 상단 `InfoBarHeight`(120) 띄다 — **아무도 안 쓰는 빈 자리**임을
            // 전수 검색으로 확인했다(`InfoBarHeight` 를 읽는 곳은 HUD 시작점 하나뿐).
            // 그래서 HUD 첫 줄보다 **앞**에 오고, 아무것도 밀어내지 않는다.
            //
            // ⚠️ **임시물이다** — 얼굴 방향의 원인이 잡히면 이 메서드를 통째로 걷는다.
            DrawFacingDiagnostic(combatBand, hudScale, hudLeft, hudW, lineFace);

            bool foldable = GameViewSignals.BoardViewActive;
            if (foldable)
            {
                float bs = 44f;
                var foldRect = new Rect(hud.x + hud.width - bs - 4f, hud.y, bs, bs);
                UiBlockers.Add(foldRect);
                if (UiSkin.Button(foldRect, HudFolded ? "▼" : "▲")) HudFolded = !HudFolded;
            }

            if (foldable && HudFolded)
            {
                // ⚠️ **합체 연출은 접어도 나온다** — 전체 화면 연출이라 HUD 와 층이 다르다.
                DrawMergeCutscene();
                return;
            }

            if (GameViewSignals.BoardViewActive) UiPlate.Draw(hud);

            GUILayout.BeginArea(hud);
            GUILayout.Label(lineTitle, style);
            GUILayout.Label(lineOutput, style);
            GUILayout.Label(lineAmmo, style);
            // 탄약 줄 — **저장 노드 재고를 막대 하나로**(UI 문서 3-3). 재고가 0인 탄종은 칸이 없다.
            DrawAmmoBar();
            GUILayout.Label(lineStore, style);
            // 회피 스택은 HP 바로 옆에 붙인다 — 「몇 대 더 버티는가」를 같은 눈길에서 읽게 한다.
            GUILayout.Label(lineEnemy, style);
            // 회피 눈금 바 — 숫자 옆에 붙인 줄을 그림으로 한 번 더 준다(UI 문서 11-3).
            DrawDodgeTicks();
            if (lineTag != null) GUILayout.Label(lineTag, style);
            GUILayout.Label(lineElapsed, style);
            // 재화는 방치 런타임이 게시한 값을 그대로 읽는다(IdleSignals). 여기서 계산하지 않는다 —
            // 적립 규칙은 방치 런타임 한 곳에만 산다. 화면에 새 패널을 놓을 자리가 없어
            // 이 상태 칸에 붙였다. 방치 씬이 없는 격리 전투 씬에서는 둘 다 0으로 뜬다.
            GUILayout.Label(lineWallet, style);
            GUILayout.Label(lineHelp, style);
            GUILayout.EndArea();

            // ⚠️ **전투 조작 버튼은 조립 화면에서 그리지 않는다**(260902_W09 §5-3).
            //
            // 좌측 y 300~460은 전투가 쓰는 자리인데, 조립 화면에서도 그대로 떠서
            // 보드 쪽 UI가 놓일 자리가 없었다. 배율 라벨이 태그 버튼과 겹친 것이 그 증상이다.
            // 자리를 또 옮기는 대신 **화면마다 무엇을 그릴지**를 정한다 —
            // 조립 중에 태그·합체·다시를 누를 이유가 없다(기능은 그대로 남는다).
            //
            // 물류 출력 줄(위 HUD)은 남긴다. 재설계하면서 수치가 따라 움직이는 것을
            // 보는 것이 조립 화면의 내용물이기 때문이다.
            if (!GameViewSignals.BoardViewActive)
            {
                if (_sim.Result != CombatResult.InProgress)
                {
                    GUILayout.BeginArea(new Rect(
                        12f, hud.yMax + 10f,
                        Mathf.Min(560f, Screen.width - 24f),
                        Mathf.Min(160f, Mathf.Max(0f, combatBand.yMax - hud.yMax - 20f))));
                    GUILayout.Label(ResultText(), big);
                    if (UiSkin.ButtonLayout("다시 (Restart)", GUILayout.Width(160), GUILayout.Height(34)))
                        Restart();
                    GUILayout.EndArea();
                }
                else
                {
                    TagMergeButtons(style);
                }
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
        /// 얼굴 방향 진단 한 줄을 **따로** 그린다(2026-09-15 오후).
        ///
        /// ⚠️ 판을 깔고 흰 글자로 쓴다 — 전투 화면에서는 밑이 **흙바닥**이라
        /// 판 없이는 같은 색에 무힌다. 이미 같은 이유로 HUD 글자가 한 번 사라졌다.
        ///
        /// ⚠️ 크기는 **두 변**에서 잡는다 — 높이로만 정하면 좁고 긴 창에서 글자가
        /// 한 자씩 쌓인다(오늘 네 번째 같은 병이다).
        /// </summary>
        private void DrawFacingDiagnostic(Rect combatBand, float scale, float left, float width,
            string text, string second = null)
        {
            if (string.IsNullOrEmpty(text)) return;

            float pad = 4f * scale;
            float h = Mathf.Max(16f, UiLayout.InfoBarHeight * scale - pad * 2f);
            var r = new Rect(left, combatBand.y + pad, width, h);
            if (r.yMax > Screen.height) r.y = Mathf.Max(0f, Screen.height - r.height);

            UiPlate.Draw(r);

            int byHeight = Mathf.RoundToInt(h * 0.55f);
            int byWidth = Mathf.RoundToInt(width / 26f);   // 이 줄은 대략 26 글자다
            var st = new GUIStyle(GUI.skin.label)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(10, Mathf.Min(byHeight, byWidth))),
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Overflow,   // 재기 전에 재면 줄어드는 함정을 피한다
            };
            st.normal.textColor = Color.white;

            if (string.IsNullOrEmpty(second))
            {
                GUI.Label(new Rect(r.x + pad, r.y, r.width - pad * 2f, r.height), text, st);
                return;
            }

            // 두 줄이면 판을 반씩 나눈다 — 글자를 겹치면 둘 다 못 읽는다.
            float half = r.height * 0.5f;
            GUI.Label(new Rect(r.x + pad, r.y, r.width - pad * 2f, half), text, st);
            GUI.Label(new Rect(r.x + pad, r.y + half, r.width - pad * 2f, half), second, st);
        }

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
            float s = UiLayout.Scale(Screen.height);
            float h = 56f * s;
            float gap = 8f * s;

            // 원형 **위**. 띠 밖으로 나가면 안 그린다 — 화면 밖 버튼은 눌 수 없다.
            var toggle = new Rect(tagRect.x, tagRect.y - gap - h, tagRect.width, h);
            if (toggle.y < 0f) return;
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
            bool hit = UiSkin.Button(toggle, on ? "자동 교대 켜짐" : "자동 교대 꺼짐", style);
            GUI.color = prev;
            if (hit) TagAutoMode.Toggle();   // 길게 누르기와 **같은 값**을 뒤집는다

            // 안내 한 줄 — **문구는 사용자 확정 그대로**다(`TagAutoMode.Hint`).
            var hint = new Rect(toggle.x, toggle.y - h * 0.62f, toggle.width, h * 0.58f);
            if (hint.y < 0f) return;
            GUI.Label(hint, TagAutoMode.Hint, new GUIStyle(GUI.skin.label)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(8,
                    Mathf.Min(Mathf.RoundToInt(h * 0.30f),
                              Mathf.RoundToInt(hint.width / 8.5f)))),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow,
            });
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

        private string TagButtonLabel()
        {
            if (_sim.Tag.Locked) return "태그 (합체 중 잠금)";
            float cd = _sim.Tag.Tag.CooldownRemaining;
            if (cd > 0f) return $"태그 (쿨다운 {cd:F1}s)";

            // 밝기만으로는 **왜** 밝은지가 안 읽힌다 — 한 마디를 붙인다(문구 가정).
            return TagSystem.SkillReady(true,
                _sim.Tag.StandbyMount != null && _sim.Tag.StandbyMount.IsFull)
                ? "태그 — 교대 (스킬)"
                : "태그 — 교대";
        }

        private string MergeButtonLabel()
        {
            if (_sim.Merge == null) return "합체 (없음)";
            if (_sim.Merge.IsActive) return $"합체 진행 {_sim.Merge.RemainingSeconds:F1}s";
            if (_sim.Merge.UsedThisStage) return "합체 (이 스테이지 사용 완료)";
            return _sim.Merge.IsReady
                ? "합체 — 발동"
                : $"합체 게이지 {_sim.Merge.ChargeRatio * 100f:F0}%";
        }

        /// <summary>
        /// 회피 재고. 추진제가 곧 회피 횟수이므로 남은 개수를 상한과 함께 보여 준다.
        /// 무적 중에는 그 사실을 따로 표시한다 — 피해가 0으로 뜨는 이유가 보여야 한다.
        /// </summary>
        private string DodgeLine()
        {
            DodgeSystem d = _sim.Dodge;
            // 상한을 부스터 대수와 함께 보여 준다 — 「노드를 더 놓으면 칸이 는다」가 화면에서 읽혀야 한다.
            string core = $"회피 {d.Stacks}/{d.Capacity} (부스터 {d.BoosterCount}대)";
            return d.IsInvincible ? core + "  [무적]" : core;
        }

        /// <summary>
        /// 회피 눈금 바(UI 문서 11-3). **길이는 고정이고 눈금 수가 상한을 따른다** —
        /// 부스터 한 대에 두 칸이라, 노드를 놓으면 칸이 늘어나는 것이 그림으로 보인다.
        ///
        /// 상한이 열을 넘으면 눈금은 열에서 멈추고 옆에 `10+`가 붙는다. 실제로 그 위쪽이
        /// 차는 일은 스테이지 제한 시간 안에서는 거의 없다(<see cref="HudMeters.TickCount"/> 주석).
        /// </summary>
        private void DrawDodgeTicks()
        {
            DodgeSystem d = _sim.Dodge;
            GUILayout.BeginHorizontal();
            HudBars.Ticks(HudBars.Row(200f), d.Stacks, d.Capacity, d.IsInvincible);
            string tag = HudMeters.OverflowTag(d.Capacity);
            if (tag.Length > 0) GUILayout.Label(tag, _hudSmall);
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 탄약 줄(UI 문서 3-3) — **저장 노드 재고**를 막대 하나로 나눠 그린다.
        ///
        /// ⚠️ 위의 <see cref="AmmoLine"/>은 **초당 소비**를 적는다. 둘은 다른 것을 잰다 —
        /// 하나는 지금 있는 발수이고 하나는 빠져나가는 속도다. 같은 줄에 섞지 않는다.
        ///
        /// 색은 <see cref="TracerColor"/>를 그대로 쓴다. 화면에 날아가는 탄선과 창고 칸이
        /// 다른 색이면 「저 노란 것이 다 떨어졌다」가 안 읽힌다.
        /// </summary>
        /// <summary>
        /// HUD 글자 줄들의 **실제 높이 합**(2026-09-14 · 2차 스크린샷).
        ///
        /// ⚠️ **줄 수가 아니라 높이를 더한다.** 태그 줄처럼 긴 줄은 폭에 따라
        /// **두 줄로 접히므로**, 「줄 × 20px」로 재면 그만큼 모자라고 모자라면 밑줄이 잘린다.
        /// `null` 은 안 그리는 줄이라 건너뛴다(로봇B 가 없으면 태그 줄이 없다).
        /// </summary>
        private static float HudTextHeight(GUIStyle style, float width, params string[] lines)
        {
            float total = 0f;
            foreach (string line in lines)
            {
                if (line == null) continue;
                total += style.CalcHeight(new GUIContent(line), width);
            }
            return total;
        }


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
            return $"출전 {who}   ·   마운트 적재 {act.Total:F0}   ·   대기 마운트 {standby.Total:F0} ({fullness})"
                   + MountItemBreakdown(standby)
                   + $"   ·   드론 {_sim.Drones.Count}기";
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
            switch (StageRequirement.Evaluate(stage.reqType, stage.req, stage.reqBand, power))
            {
                case ReqStatus.Below: return $"  [부족 {power:F0}]";
                case ReqStatus.Met: return $"  [충족 {power:F0}]";
                case ReqStatus.AboveBand: return $"  [밴드 초과 {power:F0}]";
                default: return "";
            }
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
