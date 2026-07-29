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

        private CombatSimulation _sim;
        private CombatEntityView _robotView;
        private readonly Dictionary<CombatEntity, CombatEntityView> _enemyViews =
            new Dictionary<CombatEntity, CombatEntityView>();

        private float _output;      // 물류 출력(전투력) 표시값
        private float _mountCoef;
        private bool _ready;

        private const float RobotSize = 0.8f;

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
            Begin();
        }

        /// <summary>시뮬·뷰 구성(최초 및 재시작 공용).</summary>
        private void Begin()
        {
            // 마운트계수: 물류(S1~S3)=base, 그 외(강화/태그/버스트)=enhanced(1.45).
            _mountCoef = stage.powerModel == StagePowerModel.Logistics
                ? robot.mountCoef : robot.enhancedMountCoef;
            _output = MockLogisticsOutput.CurrentOutput(robot, _mountCoef, robot.moduleMult);

            // 발사 모델: 관통(싱글)→분열(멀티샷)→폭발(AoE) 한 발씩 로테이션.
            List<AllocatedShot> shots = ShotAllocator.RoundRobin(robot.weapons);

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
                shots = shots,
            };

            List<EnemySpawn> spawns = BuildSpawns();

            _sim = new CombatSimulation(setup, spawns, tuning.arenaRadiusTbd, stage.challengeTime, tuning.spawnCadenceTbd);

            // 로봇 뷰(중앙, 파랑)
            _robotView = NewView("Robot");
            _robotView.Bind(_sim.Robot, new Color(0.3f, 0.6f, 1f), RobotSize, 10);

            _ready = true;
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

            // 플레이어 이동(WASD/화살표) — 카이팅. 진행 중에만.
            if (running)
            {
                Vector2 mv = MoveInput();
                if (mv != Vector2.zero)
                {
                    Vector2 pos = _sim.Robot.position + mv.normalized * tuning.robotMoveSpeedTbd * Time.deltaTime;
                    float maxR = tuning.arenaRadiusTbd;
                    if (pos.magnitude > maxR) pos = pos.normalized * maxR; // 아레나 밖 이탈 방지
                    _sim.Robot.position = pos;
                }
            }

            _sim.Tick(Time.deltaTime);

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
        private void Restart()
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

            GUILayout.BeginArea(new Rect(12, 10, 560, 220));
            GUILayout.Label($"스테이지 {stage.stageId}  ·  {stage.topic}", style);
            GUILayout.Label($"물류 출력(전투력) {_output:F0}  /  요구 {ReqLabel()}  ·  마운트계수 {_mountCoef:F2}", style);
            GUILayout.Label($"적 {_sim.Remaining}/{_sim.TotalEnemies}   로봇 HP {_sim.Robot.hp:F0}/{_sim.Robot.maxHp:F0}", style);
            GUILayout.Label($"경과 {_sim.Elapsed:F1}s / {stage.challengeTime:F0}s", style);
            GUILayout.Label("이동 WASD / 화살표 (카이팅)", style);
            GUILayout.EndArea();

            if (_sim.Result != CombatResult.InProgress)
            {
                GUILayout.BeginArea(new Rect(12, 240, 560, 160));
                GUILayout.Label(ResultText(), big);
                if (GUILayout.Button("다시 (Restart)", GUILayout.Width(160), GUILayout.Height(34)))
                    Restart();
                GUILayout.EndArea();
            }
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
