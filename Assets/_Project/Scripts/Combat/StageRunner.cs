using MBI.UI;
using System.Collections.Generic;
using MBI.Core;
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
        private readonly List<AmmoLine> _lineBuffer = new List<AmmoLine>(); // 재배분 버퍼(프레임당 할당 0)
        private const float ScaleEpsilon = 0.001f;                      // 이만큼 변해야 재배분
        private float _manualHoldUntil;                                 // 이 시각까지는 수동 우선(자동 정지)
        private float _mountCoef;
        private bool _ready;
        private float _lastRobotHp; // 피격 점멸 트리거 — HP가 줄어든 프레임을 잡는다
        private readonly MergeCutscene _cutscene = new MergeCutscene(); // 합체 3초 연출(시간표는 코어가 쥔다)
        private int _viewedRobotIndex;  // 뷰가 지금 그리고 있는 로봇 — 교대하면 다시 묶는다
        private bool _pointerDown;      // 플릭 인식: 누른 상태인가
        private Vector2 _pointerStart;  // 누른 지점(스크린 픽셀)
        private float _pointerDownTime;
        private readonly Dictionary<DroneUnit, SpriteRenderer> _droneViews =
            new Dictionary<DroneUnit, SpriteRenderer>();

        /// <summary>지금 나가 있는 로봇의 SO. 태그하면 바뀐다 — 스프라이트·색이 여기서 온다.</summary>
        private RobotDefinition ActiveRobotDef =>
            _sim != null && _sim.ActiveRobotIndex == 1 && robotB != null ? robotB : robot;

        // 로봇 두 대를 색으로 구분한다(아트가 들어오면 스프라이트가 이깁니다).
        private static readonly Color RobotAColor = new Color(0.3f, 0.6f, 1f);
        private static readonly Color RobotBColor = new Color(0.45f, 0.9f, 0.55f);

        // 크기는 아트 캔버스가 결정한다(ArtSpec, PPU 192 — V02 §4). 플레이스홀더도 실물과 같은 자리를
        // 차지하게 해서 스프라이트 교체 때 레이아웃이 흔들리지 않게 한다.
        private static float RobotSize => ArtSpec.RobotSize; // 256px → 1.333칸

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
        /// 적 표시 크기(보스 크게). 뷰·충돌 반경이 공유하는 단일 규칙.
        /// 아트 캔버스 규격에서 온다 — 보스 512px(2.667칸) / 몬스터 128px(0.667칸).
        /// </summary>
        private static float EnemySize(float maxHp) => maxHp >= 1000f ? ArtSpec.LargeSize : ArtSpec.MonsterSize;

        private void Start()
        {
            if (robot == null || stage == null || tuning == null)
            {
                Debug.LogError("[MBI] StageRunner 참조 누락(robot/stage/tuning) — 'MBI/Create Combat Scene'로 씬 생성 필요.");
                enabled = false;
                return;
            }
            BuildArena(); // 이동 가능 범위 경계(§C-1) — 상수라 최초 1회만.
            Begin();
        }

        /// <summary>이동 가능 아레나 경계 시각화(§C-1): 반경 arenaRadiusTbd 원반 + 테두리 링. 최초 1회.</summary>
        private void BuildArena()
        {
            var go = new GameObject("ArenaBounds");
            go.transform.SetParent(transform, false);
            go.transform.position = Vector3.zero;
            float d = tuning.arenaRadiusTbd * 2f;
            go.transform.localScale = new Vector3(d, d, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CircleSprite();          // 텍스처가 옅은 채움 + 밝은 테두리 링을 함께 담음
            sr.color = new Color(0.35f, 0.75f, 1f); // 청록 톤(알파는 텍스처)
            sr.sortingOrder = SortingLayers.Background; // 아레나 바닥
        }

        /// <summary>시뮬·뷰 구성(최초 및 재시작 공용).</summary>
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
            ShotAllocator.AllocateRates(robot.weapons, robot.consumptionCap, _lastScale, _lineBuffer);

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
                // ⚠️ 전투 시작 재고는 원천 미규정 — 만재로 둔다(보고 대상).
                ammoCapacity = ammoCapacity,
                ammoInitialStock = ammoCapacity,
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
            _sim = robotB != null
                ? new CombatSimulation(setup, BuildRobotBSetup(),
                    new MountLoad(MountLoad.SlotsRobotA, MountLoad.StandardStacks(stack)),
                    new MountLoad(MountLoad.SlotsRobotB, MountLoad.StandardStacks(stack)),
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
                ammoInitialStock = 0f,
                droneSlots = bal != null ? bal.droneSlots : 3,
                droneReleaseRate = bal != null ? bal.droneReleaseRate : 1f,
                droneCharge = bal != null ? bal.droneCharge : 100f,
                droneAttackRange = tuning.robotAttackRangeTbd, // 본체와 동일(C-3 확정)
                mountStackLimit = bal != null ? bal.mountStackLimit : 10f,
            };
        }

        /// <summary>
        /// 뷰를 지금 나가 있는 로봇에 묶는다. 교대하면 **엔티티도 스프라이트도 바뀌므로**
        /// 다시 묶지 않으면 B가 싸우는데 화면에는 A가 서 있게 된다.
        /// </summary>
        private void BindRobotView()
        {
            if (_robotView == null || _sim == null) return;

            RobotDefinition def = ActiveRobotDef;
            _robotView.Bind(_sim.Robot, _sim.ActiveRobotIndex == 1 ? RobotBColor : RobotAColor,
                RobotSize, SortingLayers.Actor, def != null ? def.sprite : null);

            _viewedRobotIndex = _sim.ActiveRobotIndex;
            _lastRobotHp = _sim.Robot.hp; // 교대 프레임을 피격으로 오인해 점멸하지 않게 한다
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
            if (Mathf.Abs(scale - _lastScale) < ScaleEpsilon) return;

            _lastScale = scale;
            ShotAllocator.AllocateRates(robot.weapons, robot.consumptionCap, scale, _lineBuffer);
            _sim.SetFireLines(_lineBuffer);
            _output = LogisticsOutputBridge.Output;
        }

        private List<EnemySpawn> BuildSpawns()
        {
            var byKey = new Dictionary<string, EnemyDefinition>();
            foreach (EnemyDefinition e in enemyCatalog)
                if (e != null && !string.IsNullOrEmpty(e.enemyKey)) byKey[e.enemyKey] = e;

            var spawns = new List<EnemySpawn>();
            foreach (StageComposition c in stage.composition)
            {
                byKey.TryGetValue(c.enemyKey, out EnemyDefinition def);
                float atk = def != null ? def.atk : 0f;
                string label = def != null ? def.displayName : c.enemyKey;
                for (int i = 0; i < c.count; i++)
                {
                    spawns.Add(new EnemySpawn
                    {
                        label = label,
                        hp = c.hp,
                        def = c.def,
                        atk = atk,
                        moveSpeed = tuning.enemyMoveSpeedTbd,
                        attackRange = tuning.enemyAttackRangeTbd,
                        attackInterval = tuning.enemyAttackIntervalTbd,
                        radius = EnemySize(c.hp) * 0.5f,
                    });
                }
            }
            return spawns;
        }

        private void Update()
        {
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

            // 이동: 수동 입력이 있으면 수동이 우선, 없으면 유예 후 자동 조종이 맡는다.
            // 영상 시나리오의 수동 카이팅 연출과 방치 진행이 한 빌드에서 공존해야 하므로 둘 다 살린다.
            if (running)
            {
                Vector2 mv = MoveInput();
                if (mv != Vector2.zero) _manualHoldUntil = Time.time + tuning.manualOverrideGraceTbd;

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
                        dt = Time.deltaTime,
                    };
                    _sim.Robot.position = AutoPilotPolicy.NextPosition(ctx);
                }
                else if (mv != Vector2.zero)
                {
                    Vector2 pos = _sim.Robot.position + mv.normalized * tuning.robotMoveSpeedTbd * Time.deltaTime;
                    float maxR = tuning.arenaRadiusTbd;
                    if (pos.magnitude > maxR) pos = pos.normalized * maxR; // 아레나 밖 이탈 방지
                    _sim.Robot.position = pos;
                }
            }

            _sim.Tick(Time.deltaTime);

            // 교대했으면 뷰를 새 로봇에 다시 묶는다 — 안 하면 B가 싸우는데 A가 서 있다.
            if (_sim.ActiveRobotIndex != _viewedRobotIndex) BindRobotView();

            // 처치를 방치 런타임으로 흘린다. 가져가며 비우는 API라 같은 처치를 두 번 세지 않는다.
            IdleSignals.AddKills(_sim.ConsumeKills());

            // 이번 틱 사격 연출(탄선 + 피격). 실제 틱이 돈 경우만(종료 후 스테일 재생성 방지).
            if (running)
                foreach (ShotEvent s in _sim.ShotsThisTick)
                {
                    SpawnShotFx(s);
                    // 발사 반동 — 스프라이트를 늘리지 않고 로봇 전체를 표적 반대로 밀었다 복귀(V01 §3).
                    if (_robotView != null) _robotView.Recoil(s.to - s.from);
                }

            // 피격 점멸 — 로봇 HP가 줄어든 프레임에 한 번. 세기는 일정하다(V01 §3):
            // 로봇에 방어력 항목이 없어 받는 피해가 몬스터 공격력 그대로이므로,
            // 세기로 정도를 표현하면 없는 정보를 지어내는 것이 된다.
            if (running && _sim.Robot != null)
            {
                if (_sim.Robot.hp < _lastRobotHp - 0.0001f && _robotView != null) _robotView.FlashHit();
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
                    view.Bind(e, col, size, SortingLayers.Actor);
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
                    Sprite art = robotB != null ? robotB.droneSprite : null;
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

        // ---- 사격 연출: 탄선(빔) + 피격 플래시 (플레이스홀더, 자동 소멸) ----
        private void SpawnShotFx(ShotEvent s)
        {
            Color c = TracerColor(s.kind);

            // 탄선 = 흰 사각을 로봇→적 방향으로 늘려 회전.
            Vector2 from = s.from, to = s.to;
            Vector2 mid = (from + to) * 0.5f;
            float dist = Vector2.Distance(from, to);
            // 이펙트는 1방향만 그리고 회전은 코드가 준다(V01 §C) — 회전 적용 지점은 ArtSpec 하나다.
            float ang = ArtSpec.EffectRotationDegrees(to - from);

            var tracer = new GameObject("Tracer");
            tracer.transform.SetParent(transform, false);
            tracer.transform.position = new Vector3(mid.x, mid.y, 0f);
            tracer.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            tracer.transform.localScale = new Vector3(Mathf.Max(dist, 0.01f), 0.06f, 1f);
            var tsr = tracer.AddComponent<SpriteRenderer>();
            tsr.sprite = PlaceholderSprite.White();
            tsr.color = c;
            tsr.sortingOrder = SortingLayers.EffectOver; // 탄선
            Destroy(tracer, 0.05f);

            if (s.aoeRadius > 0f)
            {
                // AoE 폭발 광역 원(플레이스홀더 반투명 주황 사각). 스플래시 범위 시각화.
                float d = s.aoeRadius * 2f;
                var boom = new GameObject("Boom");
                boom.transform.SetParent(transform, false);
                boom.transform.position = new Vector3(to.x, to.y, 0f);
                boom.transform.localScale = new Vector3(d, d, 1f);
                var bsr = boom.AddComponent<SpriteRenderer>();
                bsr.sprite = PlaceholderSprite.White();
                bsr.color = new Color(1f, 0.5f, 0.15f, 0.35f);
                bsr.sortingOrder = SortingLayers.EffectOver - 1; // 폭발 — 탄선보다 아래
                Destroy(boom, 0.14f);
            }
            else
            {
                // 단일/멀티샷 피격 플래시(격파 시 크고 밝게).
                float fs = s.killed ? 0.6f : 0.28f;
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
                case AmmoKind.Split: return new Color(0.4f, 0.9f, 1f);      // 분열 = 시안
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
            if (_robotView != null) Destroy(_robotView.gameObject);
            _robotView = null;
            _viewedRobotIndex = 0;
            Begin();
        }

        // ---- 최소 HUD (OnGUI, 디버그 표시) ----
        private void OnGUI()
        {
            if (!_ready) return;
            KoreanFont.Apply(); // WebGL엔 시스템 폰트 폴백이 없다 — 안 물리면 한글이 통째로 사라진다

            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            var big = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold };

            GUILayout.BeginArea(new Rect(12, 10, 560, 280));
            GUILayout.Label($"스테이지 {stage.stageId}  ·  {stage.topic}", style);
            GUILayout.Label(OutputLine(), style);
            GUILayout.Label(AmmoLine(), style);
            GUILayout.Label($"저장고(군수 생산) {LogisticsOutputBridge.AmmoProduce:F1} 발/초", style);
            // 회피 스택은 HP 바로 옆에 붙인다 — 「몇 대 더 버티는가」를 같은 눈길에서 읽게 한다.
            GUILayout.Label($"적 {_sim.Remaining}/{_sim.TotalEnemies}   로봇 HP {_sim.Robot.hp:F0}/{_sim.Robot.maxHp:F0}" +
                            $"   {DodgeLine()}", style);
            if (robotB != null) GUILayout.Label(TagLine(), style);
            GUILayout.Label($"경과 {_sim.Elapsed:F1}s / {stage.challengeTime:F0}s", style);
            GUILayout.Label("이동 WASD / 화살표   ·   회피 = 화면 플릭", style);
            GUILayout.EndArea();

            if (_sim.Result != CombatResult.InProgress)
            {
                GUILayout.BeginArea(new Rect(12, 300, 560, 160));
                GUILayout.Label(ResultText(), big);
                if (GUILayout.Button("다시 (Restart)", GUILayout.Width(160), GUILayout.Height(34)))
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

            GUILayout.BeginArea(new Rect(12, 300, 560, 120));
            GUILayout.BeginHorizontal();

            // 태그 — 쿨다운 중이거나 합체로 잠겨 있으면 비활성. 누르면 시뮬이 활성 인덱스까지 맞춘다.
            GUI.enabled = _sim.Tag.Tag.CanTag;
            if (GUILayout.Button(TagButtonLabel(), GUILayout.Width(210), GUILayout.Height(34)))
                _sim.TryManualTag();

            // 합체 — 게이지가 차야 눌린다. 스테이지당 1회라 쓰고 나면 영영 비활성이다.
            GUI.enabled = _sim.Merge != null && _sim.Merge.IsReady;
            if (GUILayout.Button(MergeButtonLabel(), GUILayout.Width(210), GUILayout.Height(34)) && _sim.TryMerge())
            {
                // 발동에 **성공했을 때만** 튼다. 실패한 버튼에 연출이 붙으면 안 된 일이 된 것처럼 보인다.
                _cutscene.Play(_sim.LastMergeSnapshot, _sim.LastBurstDamage);
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private string TagButtonLabel()
        {
            if (_sim.Tag.Locked) return "태그 (합체 중 잠금)";
            float cd = _sim.Tag.Tag.CooldownRemaining;
            return cd > 0f ? $"태그 (쿨다운 {cd:F1}s)" : "태그 — 교대";
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
            return $"출전 {who}   ·   마운트 적재 {act.Total:F0}   ·   대기 마운트 {standby.Total:F0} ({fullness})" +
                   $"   ·   드론 {_sim.Drones.Count}기";
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
            string line = $"물류 출력  예상 {exp:F0} · 실제 {act:F0} · 갭 {gap:F0}{req}  ·  마운트계수 {_mountCoef:F2}";
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

        /// <summary>탄약 표시(§C-2): 마운트 용량(종당) + 탄종별 현재 물류 공급율(발/초). 재고 변동은 물류연동 #1 이후.</summary>
        private string AmmoLine()
        {
            float pierce = 0f, split = 0f, expl = 0f;
            if (robot.weapons != null)
                foreach (WeaponSpec w in robot.weapons)
                {
                    switch (w.kind)
                    {
                        case AmmoKind.Pierce: pierce += w.shotsPerSec; break;
                        case AmmoKind.Split: split += w.shotsPerSec; break;
                        case AmmoKind.Explosive: expl += w.shotsPerSec; break;
                    }
                }
            int cap = Mathf.RoundToInt(robot.consumptionCap); // capA — RobotDefinition 단일 소스(§3, CombatTuning 중복 정리)
            return $"탄약 마운트(용량 {cap}/종)  관통 {pierce:F0} · 분열 {split:F0} · 폭발 {expl:F0} 발/초";
        }

        /// <summary>요구치가 있는 스테이지인가. 전투가 없는 스테이지 0은 없다.</summary>
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
