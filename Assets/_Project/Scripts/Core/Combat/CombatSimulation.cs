using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>전투 판정 상태.</summary>
    public enum CombatResult
    {
        InProgress,
        Win,          // 적 전멸(전원 스폰 후)
        LoseDead,     // 로봇 HP 0
        LoseTimeout   // 도전 제한시간(120초) 초과
    }

    /// <summary>이번 Tick에 발생한 사격 연출 1건(러너가 탄선·피격·폭발 연출).</summary>
    public struct ShotEvent
    {
        public Vector2 from;    // 로봇 위치
        public Vector2 to;      // 착탄점(단일/멀티샷=표적, AoE=직격 몬스터)
        public AmmoKind kind;   // 탄종(색 구분)
        public bool killed;     // 이 연출 대상이 격파됐는가
        public float aoeRadius; // >0 → 착탄점에 폭발 광역 원(반경). 0 → 단일 탄선/플래시.
    }

    /// <summary>로봇 초기 설정(순수 값 — 시뮬은 SO를 모른다, 테스트 용이).</summary>
    public struct RobotSetup
    {
        public float hp;
        public float mountCoef;   // 스테이지 powerModel에 따라 러너가 base/enhanced 선택
        public float moduleMult;
        public float attackRange;
        public float radius;      // 충돌 반경(분리). 0이면 분리 없음.
        public int multiShotCount;    // 멀티샷(분열) 표적 수(TBD). 1이면 단일.
        public float aoeRadius;       // AoE(폭발) 스플래시 반경(TBD). 0이면 직격만.
        public float aoeSplashFactor; // AoE 스플래시 데미지 배율(TBD). 1이면 풀 데미지.
        public List<AllocatedShot> shots; // 1초분 사격(ShotAllocator 산출)
    }

    /// <summary>적 스폰 스펙(순수 값). 위치는 시뮬이 결정론적으로 배치.</summary>
    public struct EnemySpawn
    {
        public string label;
        public float hp;
        public float def;
        public float atk;
        public float moveSpeed;
        public float attackRange;
        public float attackInterval;
        public float radius;      // 충돌 반경(분리). 0이면 분리 없음.
    }

    /// <summary>
    /// 실시간 탑뷰 전투 시뮬(순수 C#·결정론적·난수 0). CLAUDE.md §5-6·7.
    ///
    /// 로봇은 원점(0,0), 적은 아레나 경계 원주에 균등 각도로 스폰(결정론적)되어 로봇으로 접근.
    /// 매 Tick(dt): 스폰 → 적 이동/공격 → 로봇 사격(판정식) → 사망 정리 → 승/패/타임아웃 판정.
    /// 고정 dt로 호출하면 완전 재현(EditMode 검증 가능). 러너(MBI.Combat)가 SO→값 주입 후 구동.
    /// </summary>
    public sealed class CombatSimulation
    {
        private readonly CombatEntity _robot;
        private readonly List<CombatEntity> _enemies = new List<CombatEntity>();
        private readonly List<EnemySpawn> _spawnQueue;
        private readonly Vector2[] _spawnPositions;
        private readonly List<ShotEvent> _shots = new List<ShotEvent>();

        private readonly RobotSetup _robotSetup;
        private readonly float _arenaRadius;
        private readonly float _challengeTime;
        private readonly float _spawnCadence;

        private int _spawnedCount;
        private float _fireTimer;
        private int _shotIndex;
        private readonly float _fireInterval; // 1 / 초당 발수

        public CombatResult Result { get; private set; } = CombatResult.InProgress;
        public float Elapsed { get; private set; }
        public CombatEntity Robot => _robot;
        public IReadOnlyList<CombatEntity> Enemies => _enemies;
        public IReadOnlyList<ShotEvent> ShotsThisTick => _shots;
        public int TotalEnemies => _spawnQueue.Count;
        public int Remaining => _enemies.Count;

        public CombatSimulation(RobotSetup robot, IReadOnlyList<EnemySpawn> spawns,
            float arenaRadius, float challengeTime, float spawnCadence)
        {
            _robotSetup = robot;
            _arenaRadius = arenaRadius;
            _challengeTime = challengeTime;
            _spawnCadence = spawnCadence;

            _robot = new CombatEntity
            {
                faction = Faction.Robot,
                label = "로봇",
                position = Vector2.zero,
                hp = robot.hp,
                maxHp = robot.hp,
                def = 0f,
                radius = robot.radius,
            };

            _spawnQueue = new List<EnemySpawn>(spawns ?? new List<EnemySpawn>());
            _spawnPositions = BuildSpawnPositions(_spawnQueue.Count, arenaRadius);

            int shotsPerSec = robot.shots != null ? robot.shots.Count : 0;
            _fireInterval = shotsPerSec > 0 ? 1f / shotsPerSec : float.PositiveInfinity;
        }

        /// <summary>경계 원주에 균등 각도로 배치(결정론적, 난수 0).</summary>
        private static Vector2[] BuildSpawnPositions(int count, float radius)
        {
            var pos = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float angle = count > 0 ? (2f * Mathf.PI * i) / count : 0f;
                pos[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return pos;
        }

        public void Tick(float dt)
        {
            if (Result != CombatResult.InProgress || dt <= 0f) return;

            _shots.Clear();
            Elapsed += dt;

            SpawnDue();
            MoveAndAttackEnemies(dt);
            ResolveSeparation();
            RobotFire(dt);
            CleanupDead();
            Evaluate();
        }

        // 스폰 시각 = index * spawnCadence. cadence<=0 이면 전원 t=0.
        private void SpawnDue()
        {
            while (_spawnedCount < _spawnQueue.Count)
            {
                float spawnAt = _spawnCadence > 0f ? _spawnedCount * _spawnCadence : 0f;
                if (Elapsed < spawnAt) break;

                EnemySpawn s = _spawnQueue[_spawnedCount];
                _enemies.Add(new CombatEntity
                {
                    faction = Faction.Enemy,
                    label = s.label,
                    position = _spawnPositions[_spawnedCount],
                    hp = s.hp,
                    maxHp = s.hp,
                    def = s.def,
                    atk = s.atk,
                    moveSpeed = s.moveSpeed,
                    attackRange = s.attackRange,
                    attackInterval = s.attackInterval,
                    attackCooldown = 0f, // 사거리 진입 즉시 첫 타
                    radius = s.radius,
                });
                _spawnedCount++;
            }
        }

        private void MoveAndAttackEnemies(float dt)
        {
            foreach (CombatEntity e in _enemies)
            {
                if (!e.IsAlive) continue;
                Vector2 toRobot = _robot.position - e.position;
                float dist = toRobot.magnitude;

                if (dist > e.attackRange)
                {
                    float step = e.moveSpeed * dt;
                    if (step >= dist) e.position = _robot.position; // 오버슛 방지
                    else e.position += toRobot / dist * step;
                    e.attackCooldown = 0f; // 접근 중엔 즉시 타격 준비
                }
                else
                {
                    e.attackCooldown -= dt;
                    if (e.attackCooldown <= 0f)
                    {
                        _robot.hp -= e.atk; // 로봇 방어 스탯 없음 — 받는 피해 = 몬스터 공격력(§9)
                        e.attackCooldown += Mathf.Max(0.0001f, e.attackInterval);
                    }
                }
            }
        }

        /// <summary>
        /// 반경 기반 겹침 해소(결정론적). 적-적은 대칭으로, 적-로봇은 적만 밀어낸다(로봇=플레이어 조작).
        /// 리스트 순서 고정 → 재현 가능. radius 0(테스트 기본)이면 아무 것도 안 함.
        /// </summary>
        private void ResolveSeparation()
        {
            // 적-적
            for (int i = 0; i < _enemies.Count; i++)
            {
                CombatEntity a = _enemies[i];
                if (!a.IsAlive) continue;
                for (int j = i + 1; j < _enemies.Count; j++)
                {
                    CombatEntity b = _enemies[j];
                    if (!b.IsAlive) continue;
                    float min = a.radius + b.radius;
                    if (min <= 0f) continue;
                    Vector2 delta = b.position - a.position;
                    float d = delta.magnitude;
                    if (d >= min) continue;
                    if (d > 1e-5f)
                    {
                        float push = (min - d) * 0.5f;
                        Vector2 n = delta / d;
                        a.position -= n * push;
                        b.position += n * push;
                    }
                    else
                    {
                        // 완전 동일 좌표: 인덱스 순 고정 축으로 분리(결정론).
                        float push = min * 0.5f;
                        a.position -= new Vector2(push, 0f);
                        b.position += new Vector2(push, 0f);
                    }
                }
            }

            // 적-로봇 (적만 경계 밖으로)
            foreach (CombatEntity e in _enemies)
            {
                if (!e.IsAlive) continue;
                float min = e.radius + _robot.radius;
                if (min <= 0f) continue;
                Vector2 delta = e.position - _robot.position;
                float d = delta.magnitude;
                if (d >= min) continue;
                e.position = d > 1e-5f
                    ? _robot.position + delta / d * min
                    : _robot.position + new Vector2(min, 0f);
            }
        }

        private void RobotFire(float dt)
        {
            if (_robotSetup.shots == null || _robotSetup.shots.Count == 0) return;

            // 사거리 내 살아있는 적이 있을 때만 사격(공백 후 버스트 방지 위해 타겟 있을 때만 누적).
            CombatEntity target = NearestLivingEnemyInRange();
            if (target == null) return;

            _fireTimer += dt;
            while (_fireTimer >= _fireInterval)
            {
                _fireTimer -= _fireInterval;

                target = NearestLivingEnemyInRange();
                if (target == null) break;

                AllocatedShot shot = _robotSetup.shots[_shotIndex % _robotSetup.shots.Count];
                _shotIndex++;

                // 탄종 히트 패턴(단일/멀티샷/AoE) 해석 → 각 표적에 판정식(발당피해×배율) 적용.
                List<HitTarget> hits = HitResolver.Resolve(shot.kind, target, _enemies,
                    _robotSetup.multiShotCount, _robotSetup.aoeRadius, _robotSetup.aoeSplashFactor);

                foreach (HitTarget h in hits)
                {
                    float dmg = DamageFormula.PerHit(shot.damagePerShot * h.damageFactor,
                        _robotSetup.mountCoef, _robotSetup.moduleMult, h.entity.def);
                    h.entity.hp -= dmg;
                }

                // 연출: AoE = 착탄점 탄선 1발 + 폭발 광역 원(스플래시엔 개별 빔 없음).
                //        멀티샷/단일 = 표적별 탄선.
                if (shot.kind == AmmoKind.Explosive)
                {
                    _shots.Add(new ShotEvent
                    {
                        from = _robot.position,
                        to = target.position,
                        kind = shot.kind,
                        killed = !target.IsAlive,
                        aoeRadius = _robotSetup.aoeRadius,
                    });
                }
                else
                {
                    foreach (HitTarget h in hits)
                        _shots.Add(new ShotEvent
                        {
                            from = _robot.position,
                            to = h.entity.position,
                            kind = shot.kind,
                            killed = !h.entity.IsAlive,
                            aoeRadius = 0f,
                        });
                }
            }
        }

        private CombatEntity NearestLivingEnemyInRange()
        {
            CombatEntity best = null;
            float bestSqr = _robotSetup.attackRange * _robotSetup.attackRange;
            foreach (CombatEntity e in _enemies)
            {
                if (!e.IsAlive) continue;
                float sqr = (e.position - _robot.position).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    // 가장 가까운 적 우선. bestSqr을 갱신하며 최근접 추적.
                    bestSqr = sqr;
                    best = e;
                }
            }
            return best;
        }

        private void CleanupDead()
        {
            for (int i = _enemies.Count - 1; i >= 0; i--)
                if (!_enemies[i].IsAlive) _enemies.RemoveAt(i);
        }

        private void Evaluate()
        {
            if (_robot.hp <= 0f)
            {
                _robot.hp = 0f;
                Result = CombatResult.LoseDead;
                return;
            }
            if (_spawnedCount >= _spawnQueue.Count && _enemies.Count == 0)
            {
                Result = CombatResult.Win;
                return;
            }
            if (Elapsed >= _challengeTime)
            {
                Result = CombatResult.LoseTimeout;
            }
        }
    }
}
