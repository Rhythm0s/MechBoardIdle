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

        private const float RobotSize = 0.8f;

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

        /// <summary>적 표시 크기(장갑/보스 크게). 뷰·충돌 반경이 공유하는 단일 규칙.</summary>
        private static float EnemySize(float maxHp) => maxHp >= 1000f ? 1.1f : 0.4f;

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
            sr.sortingOrder = -10;               // 모든 엔티티 뒤
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

            _sim = new CombatSimulation(setup, spawns, tuning.arenaRadiusTbd, stage.challengeTime, tuning.spawnCadenceTbd);

            // 로봇 뷰(중앙, 파랑)
            _robotView = NewView("Robot");
            _robotView.Bind(_sim.Robot, new Color(0.3f, 0.6f, 1f), RobotSize, 10);

            _ready = true;
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

            bool running = _sim.Result == CombatResult.InProgress;

            if (running)
            {
                RefreshFireRate();
                // 창고 유입 = 라이브 군수 생산율. 재고가 마르면 발사가 멈춘다(탄약 소진 = 공격 정지).
                _sim.AmmoSupplyRate = LogisticsOutputBridge.AmmoProduce;
            }

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
                        desiredGap = tuning.autoPilotDesiredGapTbd,
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

            // 처치를 방치 런타임으로 흘린다. 가져가며 비우는 API라 같은 처치를 두 번 세지 않는다.
            IdleSignals.AddKills(_sim.ConsumeKills());

            // 이번 틱 사격 연출(탄선 + 피격). 실제 틱이 돈 경우만(종료 후 스테일 재생성 방지).
            if (running)
                foreach (ShotEvent s in _sim.ShotsThisTick)
                    SpawnShotFx(s);

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
                    view.Bind(e, col, size, 5);
                    _enemyViews[e] = view;
                }
                view.Sync();
            }

            _robotView.Sync();
        }

        private CombatEntityView NewView(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<CombatEntityView>();
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
            float ang = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;

            var tracer = new GameObject("Tracer");
            tracer.transform.SetParent(transform, false);
            tracer.transform.position = new Vector3(mid.x, mid.y, 0f);
            tracer.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            tracer.transform.localScale = new Vector3(Mathf.Max(dist, 0.01f), 0.06f, 1f);
            var tsr = tracer.AddComponent<SpriteRenderer>();
            tsr.sprite = PlaceholderSprite.White();
            tsr.color = c;
            tsr.sortingOrder = 20;
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
                bsr.sortingOrder = 8; // 적 위, 로봇 아래
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
                hsr.sortingOrder = 21;
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
            if (_robotView != null) Destroy(_robotView.gameObject);
            _robotView = null;
            Begin();
        }

        // ---- 최소 HUD (OnGUI, 디버그 표시) ----
        private void OnGUI()
        {
            if (!_ready) return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            var big = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold };

            GUILayout.BeginArea(new Rect(12, 10, 560, 270));
            GUILayout.Label($"스테이지 {stage.stageId}  ·  {stage.topic}", style);
            GUILayout.Label(OutputLine(), style);
            GUILayout.Label(AmmoLine(), style);
            GUILayout.Label($"저장고(군수 생산) {LogisticsOutputBridge.AmmoProduce:F1} 발/초", style);
            GUILayout.Label($"적 {_sim.Remaining}/{_sim.TotalEnemies}   로봇 HP {_sim.Robot.hp:F0}/{_sim.Robot.maxHp:F0}", style);
            GUILayout.Label($"경과 {_sim.Elapsed:F1}s / {stage.challengeTime:F0}s", style);
            GUILayout.Label("이동 WASD / 화살표 (카이팅)", style);
            GUILayout.EndArea();

            if (_sim.Result != CombatResult.InProgress)
            {
                GUILayout.BeginArea(new Rect(12, 290, 560, 160));
                GUILayout.Label(ResultText(), big);
                if (GUILayout.Button("다시 (Restart)", GUILayout.Width(160), GUILayout.Height(34)))
                    Restart();
                GUILayout.EndArea();
            }
        }

        /// <summary>물류 출력 이중표시(예상/실제/갭) + 전역 원인(전력/발열) 점멸(§L4-R #1·#5 변수패널 1차 표시자).</summary>
        private string OutputLine()
        {
            float exp = LogisticsOutputBridge.Expected;
            float act = LogisticsOutputBridge.Output;
            float gap = LogisticsOutputBridge.Gap;
            string line = $"물류 출력  예상 {exp:F0} · 실제 {act:F0} · 갭 {gap:F0}  /  요구 {ReqLabel()}{ReqBadge()}  ·  마운트계수 {_mountCoef:F2}";
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
