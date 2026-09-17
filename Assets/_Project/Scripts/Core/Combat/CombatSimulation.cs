using System.Collections.Generic;
using MBI.Core.Combat;
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
        public int multiShotCount;    // 멀티샷(표준) 표적 수(TBD). 1이면 단일.
        public float aoeRadius;       // AoE(폭발) 스플래시 반경(TBD). 0이면 직격만.
        public float aoeSplashFactor; // AoE 스플래시 데미지 배율(TBD). 1이면 풀 데미지.
        public List<AmmoLine> lines; // 탄종별 발사 라인(ShotAllocator.AllocateRates 산출)
        public float ammoCapacity;   // 재고 용량(발). 확정치 40 — 재고는 단일 층

        /// <summary>
        /// 창고 본체. **러너가 들고 있어 스테이지를 넘어도 그대로 이어진다**(260902_W08 §1 확정).
        /// null이면 빈 창고를 새로 만든다 — 새 저장의 자연 상태가 0이다.
        ///
        /// ⚠️ 종전에는 <c>ammoInitialStock</c>으로 **전투가 열릴 때마다 재고를 만들어 줬다**(만재 40).
        /// 원천에 없는 값을 러너가 고른 것이었고, 그 탓에 생산이 0인 스테이지 0에서도
        /// 마운트가 놓기 전에 이미 차 있어 「쌓인다」가 한 번도 안 보였다.
        /// 스테이지를 여는 순간 40발을 주는 것도 0으로 비우는 것도 시스템의 개입이다 —
        /// 공장은 스테이지를 넘어도 계속 돌고 있다(지침 §3).
        /// </summary>
        public AmmoInventory ammoStore;

        /// <summary>
        /// 마운트 본체. 창고와 같은 이유로 **러너가 들고 있다**(260902_W08 §1).
        /// null이면 새로 만든다. 생성자에 마운트를 직접 넘기면 그쪽이 이긴다(태그 경로).
        ///
        /// ⚠️ 창고만 이어서는 모자란다. 창고 → 마운트 이송에 속도 제한이 없어 재고가 곧바로
        /// 마운트로 옮겨 앉으므로, 전환 시점의 창고는 대개 비어 있다. 마운트를 안 이으면
        /// **스테이지를 넘는 순간 재고가 통째로 사라진다.**
        /// </summary>
        public MountLoad mount;

        // ---- 드론(로봇 B) ----
        // 실효 방출량 = min(유입, 슬롯 × 방출률). 유입은 보드의 「드론 몸체」 조합표가 만든다.
        public int droneSlots;          // params slot = 3 (강화 비대상 상수)

        /// <summary>
        /// 드론 **한 방**의 피해. 0 이면 <c>droneCharge</c> 전량 — **구 거동**이다.
        ///
        /// ⚠️⚠️ **여기가 갈리지 않으면 「붙어서 충전량을 다 쓸 때까지 때린다」가 안 보인다**
        /// (2026-09-16 · 사용자 확정 §74-21 을 구현하다 드러났다).
        /// 지금 값(params dB 100 = 충전량 100)이면 **한 방에 전량이 나가** 드론이
        /// 붙는 그 틱에 사라진다 — 붙어 있는 시간이 0 이다.
        ///
        /// 📌 **값을 안 지어낸다.** 기본값 0 은 구 거동 그대로이고, 기당 피해를
        ///    충전량보다 작게 잡을지는 **밸런스 판정**이다(설계 몫).
        /// </summary>
        public float droneDamagePerHit;
        public float droneReleaseRate;  // params r = 1.0 (기/초/슬롯)
        public float droneCharge;       // params dB = 100. **1기 = 1회 타격 = 충전량 전량**

        /// <summary>
        /// 마운트 품목 스택 상한(260901_V03 확정 10). 0이면 상한 없음 —
        /// 그때는 <c>IsFull</c>이 서지 않아 태그 스킬이 발동하지 않는다.
        /// </summary>
        public float mountStackLimit;
        public float droneAttackRange;  // 본체와 동일하게 둔다(C-3 확정)
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
        /// <summary>투사체 속도(유닛/초). 0 = 즉발(현행). 포격만 >0 이다(§71-33 ②).</summary>
        public float projectileSpeed;
    }

    /// <summary>
    /// 적이 쏜 **날아가는 것** (2026-09-11 · §71-33 ② 공격 패턴 (가)).
    ///
    /// ⚠️ **조준점은 쏜 순간의 로봇 자리로 고정한다** — 따라오지 않는다. 그래야 포격이
    /// 「피할 수 있는 공격」이 되고, 안 움직이면 맞는다. 유도로 만들면 사거리를 벌린 의미가 없다.
    /// </summary>
    public struct EnemyProjectile
    {
        public Vector2 position;
        public Vector2 aim;       // 쏜 순간의 로봇 자리. 여기까지 가면 사라진다.
        public Vector2 direction; // 단위 방향(고정)
        public float speed;
        public float atk;
        public float remaining;   // 조준점까지 남은 거리(유닛)
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
        /// <summary>
        /// 로봇 한 대분 상태. 태그(A↔B 교대)가 들어오면서 이 묶음이 둘이 된다 —
        /// **대기 로봇도 자기 공장·창고·마운트를 그대로 갖는다**(전투 문서 1장: 대기 로봇의 공장도
        /// 가동을 유지하고, 그 산출이 태그 인 순간 비축 화력이 된다).
        ///
        /// 클래스로 둔 이유: setup이 struct라 배열에서 값으로 꺼내면 복사본이 되고,
        /// SetFireLines의 라인 교체가 원본에 안 닿는다. 클래스 필드면 그 자리에서 바뀐다.
        /// </summary>
        private sealed class RobotSide
        {
            public RobotSetup setup;
            public CombatEntity body;
            public AmmoInventory ammo;          // 창고(저장 노드)
            public MountLoad mount;             // 마운트 적재 — 만충 판정 주체(V03 §2)
            public DroneBay bay;
            public readonly List<DroneUnit> drones = new List<DroneUnit>();
            public float[] lineTimers = new float[0];
            public float ammoSupplyRate;        // 창고 유입(발/초)
            /// <remarks>
            /// 🗑️ **폐기 — 읽는 곳이 없다**(2026-09-16). 기초 군수 **생산량**으로 마운트를
            /// 채우던 임시 길이었다. 지금은 **마운트에 닿은 기 수**가 채운다(아래 둘).
            /// 자리를 남기는 것은 밖에서 넣는 `DroneInflowRate` 가 아직 있기 때문이다 —
            /// 그 값은 「보드가 무엇을 만들었나」를 그리는 데 쓰인다.
            /// </remarks>
            public float droneInflowRate;       // 드론 몸체 유입(기/초) — 폐기

            public float stackDroneArrivalRate; // 누적형이 마운트에 닿는 비율(기/초)
            public float aoeDroneArrivalRate;   // 광역형이 마운트에 닿는 비율(기/초)

            // 회피는 로봇마다 따로 든다 — 대기 보드의 부스터도 계속 돌아 추진제를 쌓는다.
            public readonly DodgeSystem dodge = new DodgeSystem();

            /// <summary>생존 세 층의 가운데 — 이 로봇의 보드가 채운다(2026-09-17).</summary>
            public readonly ShieldSystem shield = new ShieldSystem();
            public float propellantSupplyRate;  // 부스터 유입(개/초)
            public float propellantCarry;       // 소수분 이월 — 15초에 1개라 한 틱에 1개가 안 나온다
        }

        private readonly RobotSide[] _sides;
        private int _active;

        /// <summary>지금 나가 있는 로봇의 상태 묶음.</summary>
        private RobotSide Act => _sides[_active];

        private readonly List<CombatEntity> _enemies = new List<CombatEntity>();

        /// <summary>태그 스킬이 한 번에 치는 표적 모음 — 광역이라 틱마다 다시 담는다.</summary>
        private readonly List<CombatEntity> _tagSkillTargets = new List<CombatEntity>();

        /// <summary>
        /// **화면 안**의 범위 — 태그 스킬 광역이 여기 든 적만 친다
        /// (2026-09-08 · <c>260908_W05</c> 2-2).
        ///
        /// ⚠️ **시뮬은 이 값을 스스로 못 만든다.** 여기는 순수 계산이라 카메라가 없고,
        /// 아는 경계는 <c>_arenaRadius</c>(이동 경계 · 스폰 링) 하나뿐인데
        /// **스폰 링은 화면 바깥 가장자리**라 그것으로 「화면 안」을 대신하면 뜻이 뒤집힌다.
        /// 그래서 **밖에서 재서 넣는다** — <see cref="SetVisibleBounds"/>.
        ///
        /// 안 넣으면 <c>null</c>이고 그때는 **살아 있는 적 전부**를 친다(종전 동작).
        /// </summary>
        private Rect? _visibleBounds;

        /// <summary>
        /// 스폰 링 반경을 넣는다 — 러너가 **카메라에서 재서** 준다(2026-09-11 · §71-28 2).
        ///
        /// ⚠️ **0 이하는 무시한다** — 카메라를 아직 못 잰 프레임에 0 이 들어오면
        /// 적이 **로봇 위에 겹쳐 스폰된다.**
        /// </summary>
        public void SetSpawnRing(float radius)
        {
            if (radius > 0f) SetSpawnBand(radius, radius);
        }

        /// <summary>
        /// **스폰 띠** — 적은 로봇 중심 <paramref name="min"/>~<paramref name="max"/> 사이
        /// 아무 거리에나 난다 (2026-09-15 사용자 확정 · §72-40).
        ///
        /// ⚠️ **왜 띠인가.** 반경 하나였을 때는 **모든 적이 같은 거리에서** 났고,
        /// 그 거리가 사거리보다 짧으면 로봇이 **한 걸음도 안 걸었다**(§72-38 실측 —
        /// 사거리를 9.2 로 줄여도 S1 클리어가 95.9초로 **똑같았다**). 띠로 두면
        /// 가까이 난 것은 제자리에서 쏘고 **멀리 난 것에는 걸어간다.**
        ///
        /// ⚠️ **min &gt; max 면 뒤집어 받는다** — 값이 아직 미정이라 잘못 들어올 수 있고,
        /// 뒤집힌 채로 두면 `Random.Range` 가 조용히 이상한 거리를 낸다.
        ///
        /// ⚠️ **0 이하는 무시한다** — 카메라를 아직 못 잰 프레임에 0 이 들어오면
        /// 적이 **로봇 위에 겹쳐 스폰된다.**
        /// </summary>
        /// <summary>
        /// 곁눈질 방향을 붙드는 시간(초). ⚠️ **가정**이고 값은 `CombatTuning` 에 있다 —
        /// 시뮬은 값을 만들지 않고 받는다(2026-09-16 · §74-12 B).
        /// </summary>
        public void SetSideStepHold(float seconds) => _sideStepHold = Mathf.Max(0f, seconds);

        private float _sideStepHold;

        public void SetSpawnBand(float min, float max)
        {
            if (min <= 0f || max <= 0f) return;
            if (min > max) { float t = min; min = max; max = t; }
            _spawnRingMin = min;
            _spawnRingMax = max;
        }

        /// <summary>
        /// 이번 스폰의 거리 — 띠 안에서 **결정론적으로** 고른다.
        ///
        /// ⚠️ **`UnityEngine.Random` 을 안 쓴다.** 시뮬은 결정론이라야 하네스·시험이 같은 값을
        /// 두 번 낸다(`AutoPilotPolicy` 주석 「난수 0」과 같은 이유). 스폰 차례를 씨앗으로 쓴다.
        ///
        /// 띠가 한 점이면(min == max) 종전과 같은 링이다 — **값이 오기 전까지 거동이 안 바뀐다.**
        /// </summary>
        private float SpawnDistance(int index)
        {
            if (_spawnRingMax <= _spawnRingMin) return _spawnRingMin;

            // 황금비 계단 — 이웃한 차례가 몰리지 않게 띠를 고르게 훑는다.
            float t = (index * 0.6180339887f) % 1f;
            return Mathf.Lerp(_spawnRingMin, _spawnRingMax, t);
        }

        /// <summary>디스폰(되돌림)이 기준으로 삼는 거리 — **띠의 바깥**이다.</summary>
        public float SpawnRingMax => _spawnRingMax;

        /// <summary>
        /// **너무 멀어진 적을 로봇 쪽 링으로 되돌린다** (2026-09-11 · §71-28 2).
        ///
        /// ⚠️ **지우지 않는다 — 개체 수가 안 변한다.** 지우면 난이도가 조용히 낮아진다.
        /// HP 도 유지한다(가정) — 때려 놓은 것이 되살아나면 플레이어가 한 일이 사라진다.
        ///
        /// 되돌린 개수를 낸다(진단·시험용).
        /// </summary>
        /// <summary>지금 쓰는 스폰 띠의 **안쪽**. 러너가 카메라에서 재서 넣는다(시험·진단용).</summary>
        public float SpawnRingRadius => _spawnRingMin;

        public int RespawnUnreachable(float seconds = OffscreenRespawnRule.UnreachableSeconds)
        {
            Vector2 robot = RobotPosition;
            int moved = 0;

            for (int i = 0; i < _enemies.Count; i++)
            {
                CombatEntity e = _enemies[i];
                if (e == null || e.hp <= 0f) continue;

                float d = (e.position - robot).magnitude;
                if (!OffscreenRespawnRule.NeedsRespawn(d, e.moveSpeed, seconds)) continue;

                // ⚠️ **제 방향을 지킨다** — 링 위 아무 자리로 옮기면 화면에서 순간이동으로
                // 읽힌다. 로봇에서 그 적을 향한 방향 그대로 당겨 온다.
                Vector2 dir = d > 1e-4f ? (e.position - robot) / d : Vector2.right;
                // ⚠️ **되돌리는 자리는 띠의 바깥**이다(§72-40) — 안쪽으로 당겨 오면
                // 멀어져 되돌아온 적이 **가장 가까운 적**이 되어 표적이 뒤바뀐다.
                e.position = robot + dir * _spawnRingMax;

                // ⚠️ **HP 를 리셋한다**(2026-09-14 · `260911_W03` 1-1 설계 확정).
                // 09-11 에는 「때려 놓은 것이 사라지면 플레이어가 한 일이 사라진다」를 들어
                // **HP 유지**로 두었는데, 설계가 뒤집었다 — 근거는 **같은 개체라는 보장이 없다**는 것이다.
                // 로봇 기준으로 멀어져 되돌아온 것은 화면 밖에서 새로 걸어 들어오는 것과
                // 구분되지 않으므로, 반피짜리가 영문 없이 서 있는 편이 더 이상하다.
                //
                // ⚠️ **개체 수는 그대로다** — 리셋은 지우는 것이 아니다. 통과 조건은 09-11 그대로다.
                e.hp = e.maxHp;
                moved++;
            }

            return moved;
        }

        /// <summary>
        /// 화면 안의 범위를 넣는다 — 러너가 **카메라에서 재서** 준다(상수로 짓지 않는다).
        /// 창 크기·비율이 바뀌면 다시 넣어야 하므로 러너가 틱마다 갱신한다.
        /// </summary>
        public void SetVisibleBounds(Rect bounds) => _visibleBounds = bounds;

        /// <summary>화면 범위를 지운다 — 다시 「살아 있는 적 전부」로 돌아간다(테스트용).</summary>
        public void ClearVisibleBounds() => _visibleBounds = null;

        /// <summary>그 적이 지금 화면 안에 들어와 있는가. 범위가 없으면 전부 참이다.</summary>
        private bool IsOnScreen(CombatEntity e) =>
            !_visibleBounds.HasValue || _visibleBounds.Value.Contains(e.position);

        /// <summary>
        /// 태그 인부터 태그 스킬이 터지기까지의 초 — **진입 클립이 다 도는 시간**이다
        /// (2026-09-08 확정 · <c>260908_W06</c> 2장 · UI 문서「연출 표현 규칙」 10-3).
        ///
        /// ⚠️ **새 상수가 아니다.** 러너가 <c>CombatTuning.animTagInSeconds</c>를 그대로 넣어 준다 —
        /// <see cref="SetVisibleBounds"/>와 같은 길이며, 시뮬이 값을 스스로 짓지 않는다.
        ///
        /// ⚠️ **코드 이동이 멎는 자리가 아니다.** 이동은 꼬리 칸만큼 먼저 끝나지만
        /// (로봇 A 아트 요청 문서 5-1), 꼬리 칸은 「화면을 보고 조정할 값」이라
        /// 거기에 시점을 걸면 **꼬리 칸을 고칠 때마다 스킬이 같이 움직인다**(W06 2장 넷째 근거).
        ///
        /// 0이면 종전대로 **태그 인과 동시**에 터진다(테스트 기본값).
        /// </summary>
        private float _tagSkillDelaySeconds;

        private float _tagSkillWait;

        /// <summary>태그 스킬이 터지기까지의 초를 넣는다 — 러너가 튜닝의 클립 초를 그대로 준다.</summary>
        public void SetTagSkillDelay(float seconds) => _tagSkillDelaySeconds = Mathf.Max(0f, seconds);

        /// <summary>이번 틱에 태그 스킬이 실제로 터졌는가 — 연출이 이 프레임에 나간다.</summary>
        public bool TagSkillResolvedThisTick { get; private set; }

        // ── 연출이 읽는 사건 자리 (2026-09-08 · 260908_W06 6장 · VFX 배선) ──────────────
        //
        // ⚠️ **판정에 손대지 않는다.** 이미 일어난 일의 자리만 밖으로 낸다
        // (전투 시스템 문서 10-1 「연출은 새로운 사건을 만들지 않는다」).
        // 사격·피격은 종전대로 `ShotsThisTick`이 나른다 — 여기는 그것이 못 나르는 둘이다.
        private readonly List<Vector2> _droneLaunches = new List<Vector2>();
        private readonly List<Vector2> _droneExpiries = new List<Vector2>();

        /// <summary>이번 틱에 드론이 사출된 자리들 — 연출 `vfx_dronelaunch`가 여기 붙는다.</summary>
        public IReadOnlyList<Vector2> DroneLaunchesThisTick => _droneLaunches;

        /// <summary>이번 틱에 드론이 충전량을 다 쓰고 사라진 자리들 — `vfx_droneexpire`.</summary>
        public IReadOnlyList<Vector2> DroneExpiriesThisTick => _droneExpiries;
        private readonly List<EnemySpawn> _spawnQueue;

        private readonly List<ShotEvent> _shots = new List<ShotEvent>();

        /// <summary>
        /// 적이 나타나는 **링 반경** — 로봇 기준이다(2026-09-11 · §71-28 2).
        ///
        /// ⚠️ **구 이름 `_arenaRadius` 폐기.** 이동 경계이자 스폰 링이던 값인데,
        /// 클램프가 없어지면서 **스폰 링 하나**만 남았다. 이름이 둘을 겸하면
        /// 「화면 안」을 이것으로 대신하는 실수가 다시 난다(140행 주석과 같은 자리).
        ///
        /// ⚠️ **읽기 전용이 아니다** — 창 크기가 바뀌면 러너가 다시 넣는다.
        /// </summary>
        private float _spawnRingMin;
        private float _spawnRingMax;
        private readonly float _challengeTime;
        private readonly float _spawnCadence;

        private int _spawnedCount;
        // 라인별 발사 누산기는 로봇마다 따로 든다 — 교대해도 위상이 보존돼야 한다.
        private const float FireEpsilon = 1e-4f; // float 누적 오차로 발사를 흘리지 않기 위한 허용오차
        // 추진제 이월도 같은 뿌리의 오차를 탄다 — 0.9999에서 한 개를 흘리면 회피가 영영 안 찬다.
        private const float PropellantEpsilon = 1e-4f;

        private bool _pendingFlick;
        private Vector2 _pendingFlickDirection;

        /// <summary>창고로 들어오는 생산율(발/초, 전 탄종 합). 러너가 라이브 물류에서 매 프레임 주입한다.</summary>
        public float AmmoSupplyRate
        {
            get => Act.ammoSupplyRate;
            set => Act.ammoSupplyRate = value;
        }

        /// <summary>
        /// **마운트에 닿는 드론**(기/초) — 종별 (2026-09-16 · 조립 문서 7-3-1).
        ///
        /// 📌 생산량이 아니라 **도착률**이다. 만든 것이 벨트를 타고 고정 포트에 닿아야
        /// 적재가 된다 — 그것이 문서가 정한 재고 세 층의 마지막 층이다.
        /// </summary>
        public float StackDroneArrivalRate
        {
            get => Act.stackDroneArrivalRate;
            set => Act.stackDroneArrivalRate = value;
        }

        public float AoeDroneArrivalRate
        {
            get => Act.aoeDroneArrivalRate;
            set => Act.aoeDroneArrivalRate = value;
        }

        /// <summary>대기 로봇의 것 — 대기 보드도 계속 돌아 적재가 쌓인다.</summary>
        public float StandbyStackDroneArrivalRate
        {
            get => Standby.stackDroneArrivalRate;
            set => Standby.stackDroneArrivalRate = value;
        }

        public float StandbyAoeDroneArrivalRate
        {
            get => Standby.aoeDroneArrivalRate;
            set => Standby.aoeDroneArrivalRate = value;
        }

        /// <summary>대기 로봇의 창고 유입. 대기 보드도 계속 돌아 비축이 쌓인다(전투 문서 1장).</summary>
        public float StandbyAmmoSupplyRate
        {
            get => Standby.ammoSupplyRate;
            set => Standby.ammoSupplyRate = value;
        }

        /// <summary>재고 잔량(전 탄종 합)·적재율(HUD용). 창고는 만충 판정 주체가 아니다(V03 §2).</summary>
        public float AmmoStock => Act.ammo.Total;
        public float AmmoFillRatio => Act.ammo.FillRatio;

        /// <summary>탄종별 잔량(발). 탄종별 창고 표시·진단용.</summary>
        public float AmmoStockOf(AmmoKind kind) => Act.ammo.StockOf(kind);

        /// <summary>
        /// 창고 총량 상한(발). **탄종이 나눠 쓰는 한 칸**이라, 탄약 줄이 빈 꼬리를 그리려면
        /// 이 값이 있어야 한다 — 없으면 있는 것끼리의 비율만 남아 「얼마나 찼는가」가 사라진다.
        /// </summary>
        public float AmmoCapacity => Act.ammo.Capacity;

        // ---- 드론(로봇 B) ----

        /// <summary>드론 몸체 유입(기/초). 러너가 보드 산출에서 매 프레임 주입한다.</summary>
        public float DroneInflowRate
        {
            get => Act.droneInflowRate;
            set => Act.droneInflowRate = value;
        }

        /// <summary>
        /// 대기 로봇의 드론 몸체 유입. 대기 보드도 돌아 **대기 로봇의 마운트에 드론이 쌓인다** —
        /// 그것이 「활성 소진 → 대기 복귀」 태그 트리거의 전제다.
        /// </summary>
        public float StandbyDroneInflowRate
        {
            get => Standby.droneInflowRate;
            set => Standby.droneInflowRate = value;
        }

        /// <summary>필드에 나가 있는 드론.</summary>
        public IReadOnlyList<DroneUnit> Drones => Act.drones;

        /// <summary>드론 사출대(진단·HUD용).</summary>
        public DroneBay Drones_Bay => Act.bay;

        // ---- 회피(부스터 노드) ----

        /// <summary>나가 있는 로봇의 회피. HUD는 HP 바 옆에 이 스택을 그린다.</summary>
        public DodgeSystem Dodge => Act.dodge;

        /// <summary>대기 로봇의 회피 그릇 — **합체 중에만 쓰인다**(`260917_W03` 7-2 #1).</summary>
        public DodgeSystem StandbyDodge => Standby.dodge;

        /// <summary>싸우는 로봇의 쉴드.</summary>
        public ShieldSystem Shield => Act.shield;

        /// <summary>대기 로봇의 쉴드 — **합체 중에는 합산해 쓴다**(`260917_W06` 5장).</summary>
        public ShieldSystem StandbyShield => Standby.shield;

        /// <summary>
        /// 싸우는 보드의 쉴드 최대치.
        ///
        /// 🗑️ **구 거동 폐기 — 2026-09-17.** 종전에는 여기 넣으면 **두 진영에 같이 걸렸다**
        /// (최대치가 로봇의 것이었으므로 A · B 가 같은 값). 이제 최대치는
        /// **보드의 쉴드 발생 노드 수**가 정하므로(`ShieldSystem.MaxFrom`) **판마다 다르다** —
        /// 한쪽에 건 값을 반대쪽에도 걸면 **쉴드 줄이 없는 보드가 그릇을 얻는다.**
        /// 대기 보드 쪽은 `StandbyShieldMax` 로 따로 건다.
        /// </summary>
        public float ShieldMax
        {
            get => Act.shield.Max;
            set => Act.shield.Max = value;
        }

        /// <summary>대기 보드의 쉴드 최대치 — **제 판의 노드 수**에서 온다.</summary>
        public float StandbyShieldMax
        {
            get => Standby.shield.Max;
            set => Standby.shield.Max = value;
        }

        /// <summary>싸우는 보드가 채우는 속도(쉴드/초).</summary>
        public float ShieldChargeRate
        {
            get => Act.shield.ChargeRate;
            set => Act.shield.ChargeRate = value;
        }

        /// <summary>
        /// 대기 보드가 채우는 속도. ⚠️ **대기 보드도 계속 채운다**
        /// (「다친 로봇을 빼서 쉴드 회복」 · 플레이어블 로봇 기획서「태그 접점」).
        /// </summary>
        public float StandbyShieldChargeRate
        {
            get => Standby.shield.ChargeRate;
            set => Standby.shield.ChargeRate = value;
        }

        /// <summary>
        /// 쉴드가 막은 피해 합(진단용) — 로봇이 둘이면 두 쪽을 더한다.
        ///
        /// ⚠️⚠️ **로봇이 하나면 `Standby` 가 `Act` 와 같은 객체다**(`_sides` 가 한 칸이라
        /// `Standby` 가 제자리를 가리킨다). 무턱대고 더하면 **막은 양이 두 배로 세진다** —
        /// 실제로 그릇 50 짜리 쉴드가 100 을 막았다고 나왔다.
        /// </summary>
        public float ShieldAbsorbed =>
            HasTagPartner ? Act.shield.Absorbed + Standby.shield.Absorbed : Act.shield.Absorbed;

        /// <summary>
        /// **합체 중에는 두 보드의 회피 스택을 모두 쓴다** (2026-09-17 사용자 결정 ·
        /// `260917_W03` 7-2 #1 · 전투 시스템 문서「회피」 합체체 행).
        ///
        /// 📌 **어느 쪽부터 빼는가는 구현 판단이다 — 활성 쪽을 먼저 쓴다.**
        /// 근거는 **눈에 보이는 것**이다. 화면의 회피 게이지는 활성 로봇의 것이므로,
        /// 대기 쪽부터 빼면 플레이어가 **줄어드는 것을 못 보면서** 스택이 사라진다.
        /// (값이 아니라 읽히는 방식의 문제라 설계 역기입 자리로 올린다.)
        ///
        /// ⚠️ **한 번에 하나만 나간다.** 활성 쪽이 실제로 발동했으면 거기서 끝이다 —
        /// 「자동·수동이 겹쳐도 추진제는 1개」와 같은 규칙이고, 그래서 **떨어지면**
        /// 대기 쪽을 본다. 둘 다 부르면 한 대 맞고 추진제가 둘 나간다.
        ///
        /// ⚠️⚠️ **무적도 두 쪽을 봐야 한다.** 대기 쪽이 피했는데 `Act.dodge.IsInvincible`
        /// 만 보면 **피하고도 맞는다** — 무적은 진영이 아니라 **몸**에 걸리는 것이다.
        /// </summary>
        private bool MergedNow => Merge != null && Merge.IsActive;

        /// <summary>
        /// 쉴드가 먹고 **남은 피해**를 돌려준다 — 그것이 HP 로 간다 (2026-09-17 · `260917_W05` 4-1).
        ///
        /// 📌 **넘치는 규칙 — 한 방이 남은 쉴드보다 크면 넘친 만큼 HP 로 간다**(구현 판단).
        /// 「1 이라도 남았으면 통째로 막는다」로 두면 **쉴드 1 = 무적 1회**가 되어
        /// 회피와 같은 일을 하는 층이 둘 생긴다.
        ///
        /// 📌 **합체 중에는 두 보드 게이지를 합산해 쓴다**(사용자 확정 · `260917_W06` 5장).
        /// **활성 쪽부터 깎는다** — 회피 스택의 「활성 쪽 먼저」와 **같은 모양**이라
        /// 화면에서 읽는 규칙이 하나로 유지된다.
        ///
        /// ⚠️ **합체가 끝난 뒤 남은 게이지를 어떻게 돌려주는가는 설계가 안 정했다.**
        /// 지금 구현은 **각자 제 게이지를 그대로 들고 나간다** — 합산은 「쓸 때」만이고
        /// 옮겨 담지 않으므로 종료 시점에 따로 할 일이 없다. 가장 적게 지어내는 쪽이다.
        /// </summary>
        private float AbsorbWithShield(float damage)
        {
            float left = Act.shield.Absorb(damage);
            if (left <= 0f || !MergedNow) return left;

            return Standby.shield.Absorb(left);
        }

        /// <summary>지금 무적인가 — 합체 중이면 **어느 쪽이든** 무적이면 무적이다.</summary>
        private bool BodyIsInvincible =>
            Act.dodge.IsInvincible || (MergedNow && Standby.dodge.IsInvincible);

        /// <summary>
        /// 피격 판정에서의 회피 한 번. 활성 쪽을 먼저, 못 하면(합체 중에만) 대기 쪽을 본다.
        /// </summary>
        private void TryBodyDodge(Vector2 autoDirection)
        {
            if (Act.dodge.TryDodge(true, autoDirection, false, Vector2.zero)) return;
            if (!MergedNow) return;

            // 활성 쪽이 **진행 중**이어도 여기 온다 — 그때는 이미 무적이라 대기 쪽이
            // 발동해도 추진제만 축난다. 진행 중이 아닐 때만 대기 쪽을 쓴다.
            if (Act.dodge.IsDodging) return;
            Standby.dodge.TryDodge(true, autoDirection, false, Vector2.zero);
        }

        /// <summary>
        /// **맞은 횟수 — 회피로 무효가 된 것도 센다** (2026-09-17 · `260917_W03` 2-2).
        ///
        /// 📌 무효가 된 것을 빼면 「회피가 몇 번 필요한가」의 **분모가 사라진다.**
        /// 재는 쪽이 알고 싶은 것은 「몇 대 맞을 뻔했나」이지 「몇 대 맞았나」가 아니다.
        /// </summary>
        public int HitsTaken { get; private set; }

        /// <summary>
        /// 실제로 **HP 에서** 깎인 피해 합 — 쉴드가 먹은 몫은 여기 안 든다(`ShieldAbsorbed` 가 센다).
        /// 🗑️ 구 주석 「쉴드는 시뮬에 없다 — HP 하나다」 폐기(2026-09-17 · 사용자 「보호막 되살릴 것」).
        /// </summary>
        public float DamageTaken { get; private set; }

        /// <summary>무적 구간이라 **계산에 들어가지도 않은** 피해 합 — 회피가 더한 값이다.</summary>
        public float DamageAvoided { get; private set; }

        /// <summary>
        /// 추진제 유입(개/초). 러너가 부스터 노드 산출에서 매 프레임 주입한다.
        /// ⚠️ 탄약 유입과 마찬가지로 **설정 시점의 활성 로봇**을 가리킨다 —
        /// 같은 틱에 교대가 끼면 그 값은 새로 나온 로봇의 것이 된다.
        /// </summary>
        public float PropellantSupplyRate
        {
            get => Act.propellantSupplyRate;
            set => Act.propellantSupplyRate = value;
        }

        /// <summary>대기 로봇의 추진제 유입. 대기 보드도 돈다(전투 문서 1장).</summary>
        public float StandbyPropellantSupplyRate
        {
            get => Standby.propellantSupplyRate;
            set => Standby.propellantSupplyRate = value;
        }

        /// <summary>
        /// 보드에 놓인 부스터 대수. **회피 스택 상한이 여기서 나온다**(대수 × 2) —
        /// 상한이 상수가 아니므로 노드를 더 놓는 것이 회피를 늘리는 유일한 방법이다.
        /// </summary>
        public int BoosterCount
        {
            get => Act.dodge.BoosterCount;
            set => Act.dodge.BoosterCount = value;
        }

        /// <summary>대기 로봇 보드의 부스터 대수.</summary>
        public int StandbyBoosterCount
        {
            get => Standby.dodge.BoosterCount;
            set => Standby.dodge.BoosterCount = value;
        }

        /// <summary>
        /// 수동 회피 입력(화면 플릭). **이동 명령이 아니라 즉시 회피**라 다음 틱에 즉시 소비된다.
        /// 자동과 겹쳐도 추진제는 1개만 나간다 — 수동이 먼저 처리돼 자동이 재발동을 못 한다.
        /// </summary>
        public void RequestDodge(Vector2 flickDirection)
        {
            _pendingFlick = true;
            _pendingFlickDirection = flickDirection;
        }

        // ---- 태그(A↔B 교대) ----

        private RobotSide Standby => _sides[_sides.Length > 1 ? 1 - _active : _active];

        /// <summary>로봇이 둘인가. 하나면 태그가 없다(격리 전투·기존 테스트 경로).</summary>
        public bool HasTagPartner => _sides.Length > 1;

        /// <summary>지금 나가 있는 로봇(0 = A, 1 = B).</summary>
        public int ActiveRobotIndex => _active;

        /// <summary>교대 조정자. 로봇이 하나면 null.</summary>
        public TagBattle Tag { get; private set; }

        /// <summary>활성 로봇의 마운트 — 만충·소진 판정의 주체(V03 §2).</summary>
        public MountLoad ActiveMount => Act.mount;

        /// <summary>
        /// 합체·버스트. 로봇이 하나면 null — 합칠 상대가 없다.
        /// 합체 중에는 두 로봇이 모두 쏘고 태그가 잠긴다.
        /// </summary>
        public MergeSystem Merge { get; private set; }

        /// <summary>
        /// 합체를 시도한다(플레이어 트리거). 게이지 만충이고 스테이지당 1회 미사용일 때만 성공.
        /// 성공하면 태그가 잠긴다 — 합체 중 교대는 불가다(전투 문서 4장).
        /// </summary>
        public bool TryMerge()
        {
            if (Merge == null || !Merge.TryActivate()) return false;
            if (Tag != null) Tag.Locked = true;

            // 발동 순간에 일어나는 것은 **버스트 하나**다 — 태그 스킬은 부르지 않는다(2026-08-29 확정).
            FireBurst();
            return true;
        }

        /// <summary>직전 버스트가 낸 피해(진단·연출용). 아직 안 터졌으면 0.</summary>
        public float LastBurstDamage { get; private set; }

        /// <summary>합체 발동 순간의 두 로봇 합산 초당 실피해. 연출이 「전 → 후」의 **전**으로 쓴다.</summary>
        public float LastMergeSnapshot { get; private set; }

        /// <summary>직전 태그 스킬이 낸 피해(진단·연출용). 안 나갔으면 0.</summary>
        public float LastTagSkillDamage { get; private set; }

        /// <summary>
        /// 태그 스킬 타격 — 만재 등장이 쏟아내는 **1회 공격**(260831_V09 확정).
        ///
        /// ✅ **판정 범위는 광역이다** — 화면 안의 살아 있는 적 **전부**를 친다
        /// (2026-09-08 신설 · <c>260908_W04</c> 2-1). 평상시 사격의 최근접 단일 규칙
        /// (전투 시스템 문서 11-4)은 태그 스킬에 걸리지 않는다.
        /// 구 규칙은 「최근접 1체」였다.
        ///
        /// **사거리를 안 본다.** 문안이 「화면 안의 적 전부」이고, 같은 문서 11-2가
        /// 「화면이 곧 전장이다」로 정했다 — 살아 있는 적이 곧 화면 안의 적이다.
        ///
        /// 교대 프레임에 1회 · **표적이 하나도 없으면 발동 보류**
        /// (false를 주면 마운트도 안 비워진다).
        ///
        /// 피해 = 적재량 × 평균 발당피해(<see cref="GrandEntrance.Damage"/> 확정식).
        /// 평균은 **마운트에 실린 것들로 가중**한다 — 실린 물건 하나가 타격 하나이고,
        /// 그 물건의 발당피해를 판정식에 태우는 것은 드론이 자기 충전량으로 때리는 것과 같은 규칙이다.
        /// </summary>
        private bool TagSkillStrike(float loadedRounds)
        {
            if (loadedRounds <= 0f || Tag == null) return false;

            // ⚠️ **Act가 아니라 들어오는 로봇이다.** TagBattle은 이 시점에 이미 교대를 끝냈지만
            // 시뮬의 _active는 TickAuto가 돌아온 뒤에 갱신된다. Act를 쓰면 **나가는 로봇의**
            // 마운트로 평균을 내서 피해가 어긋난다 — 실제로 200이 나올 자리에 100이 나왔다.
            RobotSide side = _sides[Tag.ActiveIndex];

            // 광역 — **화면 안에 들어와 있는** 적을 먼저 모은다. 하나도 없으면 보류다.
            // ⚠️ 「살아 있는 적 전부」가 아니다(2026-09-08 정정 · 260908_W05 2-2) —
            // 스폰 지점이 화면 바깥이라 **걸어 들어오는 중인 적**이 늘 있고,
            // 그것까지 치면 **보이지 않는 곳의 적이 죽는다.** 연출이 닿는 데까지가 판정이 닿는 데까지다.
            _tagSkillTargets.Clear();
            foreach (CombatEntity e in _enemies)
                if (e.IsAlive && IsOnScreen(e)) _tagSkillTargets.Add(e);
            if (_tagSkillTargets.Count == 0) return false; // 재고는 만재로 남는다

            // ⚠️ **가정 하나 — 「전부에 같은 피해」로 둔다** (260908_V05 판정 요청).
            // W04 2-1 문안이 「적 전부를 친다」까지만 정하고 나누는지를 안 정했다.
            // 판정식·공식은 그대로이며 표적 수만 늘어난다. 나누는 쪽으로 답이 오면
            // 아래 한 줄(피해를 표적 수로 나눔)만 넣으면 된다 — 되돌릴 수 있는 크기다.
            float dealt = 0f;
            bool anyHit = false;
            foreach (CombatEntity target in _tagSkillTargets)
            {
                float avg = AverageDamagePerItem(side, target, loadedRounds);
                float damage = GrandEntrance.Damage(true, loadedRounds, avg);
                if (damage <= 0f) continue;

                target.hp -= damage;
                dealt += damage;
                anyHit = true;

                _shots.Add(new ShotEvent
                {
                    from = side.body.position, to = target.position,
                    kind = AmmoKind.Explosive, // 쏟아붓기 — 폭발 연출로 그린다
                    killed = target.hp <= 0f, aoeRadius = 0f,
                });
            }

            if (!anyHit) return false;
            LastTagSkillDamage = dealt;
            return true;
        }

        /// <summary>
        /// 마운트에 실린 것들의 **가중 평균 발당 실피해.** 판정식을 다시 만들지 않는다.
        ///
        /// 드론은 <c>droneCharge</c>가 곧 1회 타격이라 그 값을 발당피해로 쓴다 —
        /// 드론 사격이 이미 같은 식을 탄다(<c>DroneTick</c>).
        /// </summary>
        private static float AverageDamagePerItem(RobotSide side, CombatEntity target, float loadedRounds)
        {
            if (loadedRounds <= 0f || side.mount == null) return 0f;

            float sum = side.mount.AmountOf(MountItem.Drone) *
                        DamageFormula.PerHit(side.setup.droneCharge,
                            side.setup.mountCoef, side.setup.moduleMult, target.def);

            List<AmmoLine> lines = side.setup.lines;
            if (lines != null)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    AmmoLine l = lines[i];
                    sum += side.mount.AmountOf(MountItemMap.From(l.kind)) *
                           DamageFormula.PerHit(l.damagePerShot,
                               side.setup.mountCoef, side.setup.moduleMult, target.def);
                }
            }

            return sum / loadedRounds;
        }

        /// <summary>
        /// 버스트 — 합체 발동 순간의 **순간 필살 1회**(밸런스 5장, 예산 밖 마진 항).
        ///
        /// 스냅샷 = 그 순간 **두 로봇이 합쳐 내는 초당 실피해**이고, 거기에 300%를 곱한다.
        /// 「실피해」인 이유는 합체 배율과 같다 — 배율을 방어 빼기 전에 곱하면
        /// 「(A 화력 + B 화력) × 배수」와 값이 달라진다(260829_V01 확정).
        ///
        /// ⚠️ 드론은 스냅샷에 넣지 않는다. 드론은 초당 화력이 아니라 **재고를 태워 쓰는** 축이라
        /// 「지금 내고 있는 화력」에 섞으면 남은 재고까지 한 번에 계상된다.
        /// </summary>
        private void FireBurst()
        {
            LastBurstDamage = 0f;

            CombatEntity target = NearestLivingEnemyInRange();

            // ⚠️ 스냅샷은 **표적 유무와 무관하게** 잡는다. 연출이 「화력 50 → 90」을 띄우는 근거가
            // 이 값인데, 마침 사거리에 적이 없었다는 이유로 0이 되면 화면이 빈다.
            float snapshot = 0f;
            for (int i = 0; i < _sides.Length; i++) snapshot += SideOutputAgainst(_sides[i], target);
            LastMergeSnapshot = snapshot;

            if (target == null) return; // 때릴 것이 없으면 터뜨리지 않는다 — 허공에 버리지 않는다
            if (snapshot <= 0f) return;

            float damage = MergeSystem.BurstDamage(snapshot);
            target.hp -= damage;
            LastBurstDamage = damage;

            _shots.Add(new ShotEvent
            {
                from = Act.body.position, to = target.position,
                kind = AmmoKind.Explosive, // 순간 필살 — 폭발 연출로 그린다
                killed = target.hp <= 0f, aoeRadius = 0f,
            });
        }

        /// <summary>그 표적에 대해 이 로봇이 내는 **초당 실피해**. 판정식을 다시 만들지 않는다.</summary>
        private static float SideOutputAgainst(RobotSide side, CombatEntity target)
        {
            List<AmmoLine> lines = side.setup.lines;
            if (lines == null) return 0f;

            float sum = 0f;
            for (int i = 0; i < lines.Count; i++)
            {
                AmmoLine l = lines[i];
                if (l.shotsPerSec <= 0f) continue;
                sum += l.shotsPerSec *
                       DamageFormula.PerHit(l.damagePerShot, side.setup.mountCoef, side.setup.moduleMult,
                           target != null ? target.def : 0f);
            }
            return sum;
        }

        /// <summary>
        /// 수동 태그(HUD 버튼). 성공하면 **이번 틱부터** 새 로봇이 싸운다.
        ///
        /// ⚠️ <c>Tag.TryManualTag()</c>를 밖에서 직접 부르면 안 된다 — 조정자의 활성 인덱스만 바뀌고
        /// 시뮬의 <c>_active</c>가 그대로 남아 두 쪽이 어긋난다. 동기화는 여기 한 곳에서만 한다.
        /// </summary>
        public bool TryManualTag()
        {
            if (Tag == null) return false;

            Vector2 where = RobotPosition;
            if (!Tag.TryManualTag()) return false;
            _active = Tag.ActiveIndex;
            PlaceIncomingRobot(where);
            return true;
        }

        /// <summary>
        /// **들어오는 로봇을 나가는 로봇이 서 있던 자리에 세운다**
        /// (2026-09-16 · 사용자 보고 ⑤ — 「합체 뒤 태그 시 로봇 스폰 위치 다름」).
        ///
        /// ⚠️⚠️ **몸은 둘이고, 움직이는 것은 나선 쪽 하나뿐이다.** 대기 로봇의 몸은
        /// 전투가 시작된 자리(원점)에 그대로 서 있다 — 자동 조종이 `Act.body` 만 옮기기
        /// 때문이다. 그 상태로 교대하면 로봇이 **원점으로 순간이동**한다.
        ///
        /// 사용자가 합체 뒤에 알아챈 것은 우연이 아니다 — 합체 전에는 대개 로봇이
        /// 아직 원점 근처에 있어 차이가 안 보이고, 한 판을 걸어 다닌 뒤에야 벌어진다.
        ///
        /// 📌 **교대는 자리를 바꾸는 일이 아니다.** 같은 자리에서 선수만 바뀐다 —
        /// 진입 연출(`PlayTagIn`)이 오른쪽에서 미끄러져 들어오는 것은 **그림**이고,
        /// 그 그림도 끝나면 이 자리로 수렴한다.
        ///
        /// ⚠️ **나가는 로봇은 안 옮긴다.** 다음 교대 때 이 함수가 다시 세우므로,
        /// 두 몸을 늘 붙여 두면 같은 일을 두 곳에서 하게 된다.
        /// </summary>
        private void PlaceIncomingRobot(Vector2 where)
        {
            if (Act.body != null) Act.body.position = where;
        }

        /// <summary>이번 전투에서 드론이 낸 누적 피해(검산용).</summary>
        public float DroneDamageDealt { get; private set; }

        public CombatResult Result { get; private set; } = CombatResult.InProgress;
        public float Elapsed { get; private set; }
        public CombatEntity Robot => Act.body;

        /// <summary>
        /// 로봇의 지금 자리 — **링과 재스폰이 기준으로 삼는 점**(2026-09-11 · §71-28).
        /// 몸이 없으면 원점이다(태그 전환 사이 한 프레임 같은 자리).
        /// </summary>
        public Vector2 RobotPosition => Robot != null ? Robot.position : Vector2.zero;
        public IReadOnlyList<CombatEntity> Enemies => _enemies;
        /// <summary>
        /// 지금 겸누는 방향 — 사거리 안 최근접 적까지의 방향이다. 표적이 없으면 `null`.
        ///
        /// ⚠️⚠️ **얼굴은 발사가 아니라 표적을 따른다**(2026-09-15 사용자 확정 · 육안 8차 ①).
        ///
        /// 종전엔 `ShotsThisTick` 이 유일한 근거였다. 그러면 **한 발도 안 쏘 동안에는
        /// 영영 안 돌아본다** — 탄약이 0 이면 사거리 안에 적을 두고도 딱 등을 돌린다.
        /// 09-15 진단 줄이 그것을 그대로 찍었다 — 「얼굴 이동 · 마지막 조준 없음」(S2 9.8초).
        ///
        /// 📌 **표적 고르는 규칙은 여기 한 곳에만 산다** — `NearestLivingEnemyInRange` 를
        /// 그대로 부른다. 밖에서 「최근접 적」을 다시 고르면 쏘는 놀과 보는 놀이 갈라진다
        /// (지침 §7 — 「한 값이 두 곳에 살면 답이 둘이 된다」).
        /// </summary>
        public Vector2? AimDirection
        {
            get
            {
                CombatEntity target = NearestLivingEnemyInRange();
                if (target == null || Robot == null) return null;
                Vector2 d = target.position - Robot.position;
                return d.sqrMagnitude > 1e-6f ? d : (Vector2?)null;
            }
        }

        public IReadOnlyList<ShotEvent> ShotsThisTick => _shots;

        /// <summary>
        /// 지금 날고 있는 적 투사체들. **한 틱짜리 사건이 아니라 상태다** —
        /// `ShotsThisTick` 과 달리 매 틱 비우지 않는다(맞거나 지나칠 때까지 산다).
        /// </summary>
        public IReadOnlyList<EnemyProjectile> EnemyProjectiles => _enemyProjectiles;
        public int TotalEnemies => _spawnQueue.Count;
        public int Remaining => _enemies.Count;

        // ── 실제로 나간 발 (2026-09-15 사용자 확정 ㉮) ───────────────────────
        //
        // ⚠️ **배분과 발사는 다르다.** `ShotAllocator` 가 낸 줄은 **쏠 수 있는 상한**이고,
        // 마운트에 그 탄이 없으면 `ConsumeRound` 가 실패해 **그 틱에 안 나간다.**
        // HUD 가 배분을 찍고 있어서 **한 발도 안 나가는 탄종이 「0.5 발/초」로 보였다.**
        private readonly int[] _firedCount = new int[3];

        /// <summary>그 탄종으로 **실제로 나간** 누적 발수.</summary>
        public int FiredOf(AmmoKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < _firedCount.Length ? _firedCount[i] : 0;
        }

        /// <summary>이번 Tick에 죽은 수(관찰용). 적립에 쓸 때는 <see cref="ConsumeKills"/>로 가져간다.</summary>
        public int KillsThisTick { get; private set; }

        /// <summary>
        /// 이번 틱 처치 수를 **가져가며 0으로 비운다.** 고철 적립은 반드시 이 경로로만 읽는다.
        ///
        /// 왜 그냥 읽으면 안 되는가: 전투가 끝나면 Tick이 즉시 반환하므로 KillsThisTick이 마지막 값에
        /// 그대로 멈춰 있다. 매 프레임 그 값을 더하면 승리 화면에서 고철이 무한히 불어난다.
        /// 가져가며 비우면 두 번 읽어도 두 번 세지 않는다.
        /// </summary>
        public int ConsumeKills()
        {
            int k = KillsThisTick;
            KillsThisTick = 0;
            return k;
        }

        /// <summary>이 전투에서 누적 처치 수.</summary>
        public int TotalKills { get; private set; }

        /// <summary>
        /// 상주 파밍 층 여부. true면 승리·시간초과 판정을 하지 않는다(로봇 파괴만 남는다).
        /// 스폰은 <see cref="FarmSpawner"/>가 밖에서 몰고 <see cref="SpawnBatch"/>로 넣는다 —
        /// 도전 층의 유한 큐 스포너(SpawnDue)와 **한 엔진 안에서도 경로는 분리**된다.
        /// </summary>
        public bool Endless { get; set; }

        /// <summary>
        /// 적 배치를 즉시 투입한다(상주 파밍 보충용). 아레나 경계에 균등 배치 — 결정론, 난수 0.
        /// 도전 층은 이 경로를 쓰지 않는다.
        /// </summary>
        public void SpawnBatch(IReadOnlyList<EnemySpawn> batch)
        {
            if (batch == null) return;
            for (int i = 0; i < batch.Count; i++)
            {
                EnemySpawn s = batch[i];
                _enemies.Add(new CombatEntity
                {
                    faction = Faction.Enemy,
                    label = s.label,
                    // ⚠️ **로봇 기준이다**(2026-09-11) — 원점 기준이면 로봇이 움직인 만큼
                    // 적이 **화면 안에서 튀어나온다.**
                    position = SpawnRingRule.Position(RobotPosition, i, batch.Count, SpawnDistance(i)),
                    hp = s.hp,
                    maxHp = s.hp,
                    def = s.def,
                    atk = s.atk,
                    moveSpeed = s.moveSpeed,
                    attackRange = s.attackRange,
                    attackInterval = s.attackInterval,
                    radius = s.radius,
                });
            }
        }

        /// <summary>로봇 한 대(격리 전투·태그 없음).</summary>
        public CombatSimulation(RobotSetup robot, IReadOnlyList<EnemySpawn> spawns,
            float arenaRadius, float challengeTime, float spawnCadence)
            : this(new[] { robot }, null, spawns, arenaRadius, challengeTime, spawnCadence)
        {
        }

        /// <summary>
        /// 로봇 두 대(A↔B 태그). 대기 로봇도 자기 공장·창고·마운트를 그대로 갖고 계속 돈다 —
        /// 그 산출이 태그 인 순간 비축 화력이 되고, 그것이 저장 노드의 존재 이유다.
        /// </summary>
        public CombatSimulation(RobotSetup robotA, RobotSetup robotB,
            MountLoad mountA, MountLoad mountB, IReadOnlyList<EnemySpawn> spawns,
            float arenaRadius, float challengeTime, float spawnCadence)
            : this(new[] { robotA, robotB }, new[] { mountA, mountB },
                   spawns, arenaRadius, challengeTime, spawnCadence)
        {
        }

        private CombatSimulation(RobotSetup[] setups, MountLoad[] mounts,
            IReadOnlyList<EnemySpawn> spawns, float arenaRadius, float challengeTime, float spawnCadence)
        {
            // ⚠️ **값이 오기 전까지는 띠가 한 점이다** — 종전 링과 같다(§72-40).
            // `spawnRingMinTbd`·`spawnRingMaxTbd` 가 서면 러너가 `SetSpawnBand` 로 넣는다.
            _spawnRingMin = arenaRadius;
            _spawnRingMax = arenaRadius;
            _challengeTime = challengeTime;
            _spawnCadence = spawnCadence;

            _sides = new RobotSide[setups.Length];
            for (int i = 0; i < setups.Length; i++)
            {
                RobotSetup r = setups[i];
                var side = new RobotSide
                {
                    setup = r,
                    body = new CombatEntity
                    {
                        faction = Faction.Robot,
                        label = setups.Length > 1 ? (i == 0 ? "로봇A" : "로봇B") : "로봇",
                        position = Vector2.zero,
                        hp = r.hp,
                        maxHp = r.hp,
                        def = 0f,
                        radius = r.radius,
                    },
                    ammo = r.ammoStore ?? new AmmoInventory(r.ammoCapacity),
                    bay = new DroneBay(r.droneSlots, r.droneReleaseRate, r.droneCharge),
                    // 마운트를 안 줘도 **자기 마운트는 갖는다.** 마운트는 로봇의 장비이지
                    // 태그의 부속이 아니다 — 슬롯 0을 주던 동안 단일 로봇 시뮬은 드론을
                    // 실을 데가 없어 사출이 통째로 막혔다(드론 재고가 마운트로 옮겨진 뒤 드러남).
                    // 슬롯 수는 setup에서 나온다: 드론을 모는 쪽이 로봇 B다.
                    mount = mounts != null && i < mounts.Length && mounts[i] != null
                        ? mounts[i]
                        : r.mount
                        ?? new MountLoad(r.droneSlots > 0 ? MountLoad.SlotsRobotB : MountLoad.SlotsRobotA,
                            r.mountStackLimit > 0f ? MountLoad.StandardStacks(r.mountStackLimit) : null),
                };
                side.lineTimers = new float[r.lines != null ? r.lines.Count : 0];
                _sides[i] = side;
            }

            _spawnQueue = new List<EnemySpawn>(spawns ?? new List<EnemySpawn>());
            // ⚠️ **자리를 미리 굳히지 않는다**(2026-09-11 · §71-28 2). 종전에는 생성자에서
            // 원점 기준 절대 좌표를 다 만들어 두었는데, 링이 **로봇을 따라다니게** 되면서
            // 스폰 시점의 로봇 자리를 알아야 한다. 방향만 규칙이 내고 자리는 그때 만든다.

            // ⚠️ **여기서 재고를 만들지 않는다**(260902_W08 §1). 창고는 러너가 들고 있고
            // 시뮬은 빌려 쓸 뿐이다 — 스테이지 전환이 재고에 손대지 않는 것이 그 뜻이다.

            if (_sides.Length > 1)
            {
                Tag = new TagBattle(_sides[0].mount, _sides[1].mount);
                // 때릴 대상을 아는 쪽은 시뮬뿐이다. 이걸 안 꽂으면 스킬이 안 나간다(의도된 기본값).
                Tag.SkillStrike = TagSkillStrike;
                Merge = new MergeSystem(); // 합칠 상대가 있을 때만 존재한다
            }
        }

        // ── 탄종별 배분 (§1 배선 전 과도 규칙) ────────────────────────────────
        // 창고로 들어오는 생산은 본래 **어느 군수 노드가 어느 탄종을 만드는가**로 갈린다
        // (260824_V02 §1: 노드 1개 = 1발/초, 라인 가동률 = min(1, 보유 노드 ÷ 필요 노드)).
        // 그 배정이 아직 물류 쪽에 없으므로 그때까지는 **라인 수요 비율**로 나눈다.
        // 수요 비율 = 소비 비율이므로 단일 풀이던 종전 거동을 그대로 재현한다(회귀 없음).
        // §1 배선이 들어오면 이 두 메서드는 노드 배정 기반으로 교체된다.

        /// <summary>라인 i가 전체 수요에서 차지하는 몫(0~1).</summary>
        private float DemandShare(int lineIndex) => DemandShareOf(Act, lineIndex);

        private float DemandShareOf(RobotSide s, int lineIndex)
        {
            List<AmmoLine> lines = s.setup.lines;
            if (lines == null || lineIndex < 0 || lineIndex >= lines.Count) return 0f;

            float total = 0f;
            for (int i = 0; i < lines.Count; i++) total += Mathf.Max(0f, lines[i].shotsPerSec);
            if (total <= 0f) return 0f;

            return Mathf.Max(0f, lines[lineIndex].shotsPerSec) / total;
        }

        /// <summary>군수 → 창고 유입을 탄종별로 넣는다. 용량은 셋이 나눠 쓴다(잠식).</summary>
        private void ProduceAmmo(float dt) => ProduceAmmoInto(Act, dt);

        private void ProduceAmmoInto(RobotSide s, float dt)
        {
            if (s.ammoSupplyRate <= 0f) return;

            List<AmmoLine> lines = s.setup.lines;
            if (lines == null || lines.Count == 0) return;

            for (int i = 0; i < lines.Count; i++)
                s.ammo.Produce(lines[i].kind, dt, s.ammoSupplyRate * DemandShareOf(s, i));
        }

        // ── 회피(부스터 노드) ────────────────────────────────────────────────
        // 군수 노드가 추진제를 만들고, 부스터가 그것을 먹어 회피 스택을 채운다. 추진제 1개 = 회피 1회.
        // 상한은 **부스터 대수 × 2**라 회피를 늘리는 방법은 부스터를 더 놓는 것뿐이다.
        // 그릇만 키워도 안 세진다 — 채우는 속도는 군수 노드가 정한다(15초에 1개).

        /// <summary>
        /// 드론 몸체 유입 → 마운트 적재(260829_V03 §판정②: 사출대는 재고 층이 아니다).
        /// 마운트가 없으면(격리 단일 전투) 아무 데도 안 쌓인다 — 그때는 드론도 안 나간다.
        /// </summary>
        private static void LoadDronesInto(RobotSide s, float dt)
        {
            if (s.mount == null || dt <= 0f) return;

            // ⚠️⚠️ **도착한 것이 곧 적재다**(2026-09-16 · 조립 문서 7-3-1 · 사용자 확정).
            //
            // 🗑️ **구 길 폐기** — `droneInflowRate`(= 기초 군수 **생산량**)로 채우던 것.
            // 문서는 「고정 포트에 도착한 것은 곧바로 마운트 적재」인데 드론에는 그 길이
            // 없었다. 그래서 **복합 군수가 만들어 벨트로 보낸 드론은 끝에서 사라지고**,
            // 재고는 **만들지도 않은 수**(부품 생산량)가 채웠다.
            //
            // 📌 **종별로 쌓는다** — 무엇이 실렸는지 모르면 교대해도 못 쏜다(⑤-2 와 같은 뿌리).
            if (s.stackDroneArrivalRate > 0f)
                s.mount.Load(MountItem.Drone, s.stackDroneArrivalRate * dt);
            if (s.aoeDroneArrivalRate > 0f)
                s.mount.Load(MountItem.DroneAoe, s.aoeDroneArrivalRate * dt);
        }

        /// <summary>
        /// 추진제 유입 + 수동 플릭 소비. **적 공격 판정보다 먼저** 돈다:
        /// 수동이 먼저 발동해 있으면 자동은 재발동 금지에 걸려 그냥 지나가고,
        /// 그 결과 「수동이 이기고 추진제는 1개만」이 순서만으로 성립한다.
        /// </summary>
        private void DodgeTick(float dt)
        {
            for (int i = 0; i < _sides.Length; i++) _sides[i].dodge.Tick(dt);

            // ⚠️ **쉴드는 두 진영이 다 찬다**(2026-09-17 · `260917_W05` 4-1 귀속).
            //    대기 보드도 계속 채우는 것이 「다친 로봇을 빼서 회복」의 근거다.
            for (int i = 0; i < _sides.Length; i++) _sides[i].shield.Tick(dt);

            ProducePropellantInto(Act, dt);

            if (!_pendingFlick) return;
            Act.dodge.TryDodge(false, Vector2.zero, true, _pendingFlickDirection);
            _pendingFlick = false;
        }

        /// <summary>
        /// 부스터 → 추진제. 만충이면 이월분을 **버린다** — 상한 위에 남겨 두면
        /// 회피를 쓴 직후 쌓아 둔 소수분이 한꺼번에 터져 상한이 사실상 없어진다.
        /// </summary>
        private void ProducePropellantInto(RobotSide s, float dt)
        {
            if (s.propellantSupplyRate <= 0f) return;

            s.propellantCarry += s.propellantSupplyRate * dt;
            while (s.propellantCarry >= 1f - PropellantEpsilon)
            {
                if (s.dodge.AddStacks(1) == 0) { s.propellantCarry = 0f; return; }
                s.propellantCarry -= 1f;
            }
        }

        /// <summary>
        /// 발사 라인 교체(§5-6 D2). 물류 출력이 변하면 전투를 재시작하지 않고 이것만 갈아끼운다
        /// (연속성 원칙 — 조립 중에도 전투는 멈추지 않는다).
        ///
        /// ⚠️ 누산기(Act.lineTimers)는 **보존한다.** 매 프레임 호출될 수 있는데 여기서 0으로 되돌리면
        /// 누산이 1.0에 영영 도달하지 못해 영구 무발사가 된다.
        /// </summary>
        public void SetFireLines(IReadOnlyList<AmmoLine> lines)
        {
            if (Act.setup.lines == null) Act.setup.lines = new List<AmmoLine>();
            Act.setup.lines.Clear();
            if (lines != null)
                for (int i = 0; i < lines.Count; i++) Act.setup.lines.Add(lines[i]);

            ResizeLineTimers(Act.setup.lines.Count);
        }

        // 라인 수가 변해도 기존 위상을 최대한 유지한다(길이가 줄면 잘리고, 늘면 0에서 시작).
        private void ResizeLineTimers(int count)
        {
            if (Act.lineTimers.Length == count) return;
            var next = new float[count];
            int keep = Act.lineTimers.Length < count ? Act.lineTimers.Length : count;
            for (int i = 0; i < keep; i++) next[i] = Act.lineTimers[i];
            Act.lineTimers = next;
        }

        /// <summary>경계 원주에 균등 각도로 배치(결정론적, 난수 0).</summary>
        public void Tick(float dt)
        {
            if (Result != CombatResult.InProgress || dt <= 0f) return;

            _shots.Clear();
            _droneLaunches.Clear();
            _droneExpiries.Clear();
            KillsThisTick = 0;
            TagSkillResolvedThisTick = false;
            Elapsed += dt;

            ProduceAmmo(dt);   // 군수 → 창고 유입(총량 캡 초과분은 버려진다)
            // 창고 → 마운트. ⚠️ 태그 여부와 **무관하게** 돈다 — 마운트는 로봇의 장비라
            // 로봇이 하나여도 거기서 쏜다. 이걸 TagTick 안에 두었더니 단일 로봇 시뮬은
            // 마운트가 영영 비어 발사가 통째로 멈췄다(마운트 슬롯이 0이던 동안 가려져 있었다).
            RefillMount(Act);
            StandbyTick(dt);   // 대기 로봇의 공장도 계속 돈다 — 그 산출이 태그 인 순간 비축 화력이 된다
            MergeTick(dt);     // 게이지 충전·지속 소모. 합체가 끝나면 태그 잠금이 풀린다
            TagTick(dt);       // 교대 판정. 교대가 일어나면 이번 틱부터 새 로봇이 싸운다

            DodgeTick(dt);     // 추진제 유입 + 수동 플릭 소비. **적 공격 판정보다 앞선다**

            SpawnDue();
            MoveAndAttackEnemies(dt);
            // ⚠️ **쏜 뒤에 옮긴다.** 이번 틱에 낳은 포탄도 한 걸음 나아간다 —
            // 적 발 밑에 한 프레임 멈춰 있으면 화면에서 「튀어나오는」 것으로 보인다.
            MoveEnemyProjectiles(dt);
            // ResolveSeparation(밀어내기) 폐기 — 구현 사양이 "밀어내지 않음 · 막히면 멈춤"으로 확정됐다.
            // 겹침은 이동 시점에 IsBlocked로 막으므로 사후 보정이 필요 없다.
            RobotFire(dt);
            DroneTick(dt);
            CleanupDead();
            Evaluate();
        }

        // ── 태그(A↔B 교대) ─────────────────────────────────────────────────
        // 로봇이 하나면 아무 일도 하지 않는다 — 격리 전투와 기존 경로가 그대로 돈다.

        /// <summary>
        /// 대기 로봇의 공장 가동. **대기 중에도 창고가 차고 마운트가 채워진다**(전투 문서 1장).
        /// 이것이 없으면 태그 인 순간 빈손으로 나와 「축적 → 만재 등장」이라는 설계가 성립하지 않는다.
        /// </summary>
        private void StandbyTick(float dt)
        {
            if (!HasTagPartner) return;

            RobotSide s = Standby;

            // 🗑️⚠️ **구 「과도 규칙」 폐기**(2026-09-16 · 사용자 보고 ⑤ · 플랜 §74-17 ⑤).
            //
            // 구 주석: 「탄종 배정이 아직 물류에 없으므로 활성과 같은 과도 규칙을 쓴다.」
            // 그 임시 규칙이 **로봇 B 창고에 탄약을 쌓았고**, 그것이 `RefillMount` 를 타고
            // **B 마운트에 표준탄으로 실렸다** — 문서는 B 마운트를 **드론만**으로 정한다.
            // 사용자 스크린샷이 그 자리를 잡았다.
            //
            // 📌 **전제가 사라졌다.** 2026-09-16 에 보드가 로봇별로 갈리면서
            //    탄종 배정이 물류에 생겼다 — A 판이 탄약을, B 판이 드론을 만든다.
            //    임시 규칙을 떠받치던 「아직 없다」가 없어졌으므로 규칙도 걷는다.
            if (s.ammoSupplyRate > 0f && !UsesDroneMount(s))
            {
                // 대기 로봇은 소비가 0이라 생산 전량이 쌓인다(조립 문서「소비까지의 흐름」).
                ProduceAmmoInto(s, dt);
            }

            // 대기 보드의 부스터도 돈다 — 태그 인 순간 회피 스택도 함께 나온다.
            ProducePropellantInto(s, dt);

            // 대기 로봇의 드론도 마운트에 쌓인다. **이것이 로봇 B의 태그 조건을 연다** —
            // 드론이 마운트로 안 들어가던 동안 B의 마운트는 영구히 비어 있어
            // 「활성 소진 → 대기 복귀」 트리거가 켜질 수 없었다.
            LoadDronesInto(s, dt);

            // 창고 → 마운트. 벨트가 실어 오는 것이라 자리가 없으면 창고에 남는다.
            RefillMount(s);
        }

        /// <summary>
        /// 합체 게이지 충전과 지속 소모. **전투 수행 중에만** 찬다 —
        /// 여기서 Tick이 도는 것 자체가 전투가 진행 중이라는 뜻이다.
        /// 합체가 끝나면 태그 잠금을 푼다(전투 문서 4장: 종료 후 필드 로봇 만재 복귀).
        /// </summary>
        private void MergeTick(float dt)
        {
            if (Merge == null) return;

            bool wasActive = Merge.IsActive;
            Merge.Tick(dt, inCombat: true);

            if (wasActive && !Merge.IsActive && Tag != null) Tag.Locked = false;
        }

        /// <summary>
        /// **자동 교대를 켤 것인가** (2026-09-16 · 사용자 확정 · `MBI.Core.TagAutoMode`).
        ///
        /// ⚠️ **여기 기본값은 참이다** — 이 클래스의 구 거동이고, 하네스와 시험이
        /// 그 판을 잰다. **게임의 기본값은 꺼짐**이며 그것은 `TagAutoMode` 가 들고
        /// 러너가 매 프레임 넣는다. 둘은 다른 물음이다 —
        /// 「시뮬이 무엇을 할 수 있는가」와 「플레이어가 무엇을 골랐는가」.
        /// </summary>
        public bool AutoTagEnabled { get; set; } = true;

        /// <summary>교대 판정 → 발동. 교대하면 활성 인덱스가 바뀐다.</summary>
        private void TagTick(float dt)
        {
            if (Tag == null) return;

            // ⚠️ **교대 전 자리를 먼저 잡아 둔다** — 교대한 뒤에는 `RobotPosition` 이
            //    이미 들어온 로봇(원점)을 가리킨다(2026-09-16 · 사용자 보고 ⑤).
            Vector2 where = RobotPosition;
            bool tagged = Tag.TickAuto(dt, AutoTagEnabled);
            if (tagged)
            {
                _active = Tag.ActiveIndex;
                PlaceIncomingRobot(where);
            }

            // 태그 스킬은 **진입 클립이 다 돈 뒤에** 터진다 (260908_W06 2장 · (가) 0.75초).
            // 교대한 틱에 시계를 0으로 놓고, 지연이 0이면 그 틱에 바로 터진다(종전 거동).
            if (tagged) _tagSkillWait = 0f;

            if (!Tag.HasPendingSkill) return;

            _tagSkillWait += dt;
            if (_tagSkillWait < _tagSkillDelaySeconds) return;

            // ⚠️ 표적이 하나도 없으면 여기서도 **보류**다 — 마운트는 만재로 남는다.
            TagSkillResolvedThisTick = Tag.ResolvePendingSkill();
        }

        /// <summary>
        /// **이 로봇은 드론을 모는가** (2026-09-16 · 사용자 보고 ⑤).
        ///
        /// 📌 **판정을 새로 만들지 않는다** — 마운트 슬롯 수를 고를 때 이미 같은 물음을
        /// 같은 방법으로 묻고 있다(`r.droneSlots > 0 ? SlotsRobotB : SlotsRobotA`).
        /// 여기서 다른 잣대를 쓰면 **슬롯은 B 인데 적재 규칙은 A** 인 로봇이 생긴다
        /// (지침 §7 — 한 값이 두 곳에 살면 답이 둘이 된다).
        /// </summary>
        private static bool UsesDroneMount(RobotSide s) => s.setup.droneSlots > 0;

        /// <summary>
        /// 창고 → 마운트 이송. 탄종별로 실을 수 있는 만큼만 실린다.
        /// **마운트가 만충 판정 주체**이므로(V03 §2) 이 이송이 태그 트리거를 만든다.
        /// </summary>
        private void RefillMount(RobotSide s)
        {
            if (s.mount == null || s.mount.SlotCount <= 0) return;

            // ⚠️⚠️ **드론을 모는 로봇의 마운트에는 탄약이 안 실린다**
            //    (2026-09-16 · 사용자 보고 ⑤ — 「B 마운트에 표준탄이 실림」).
            //
            // 실리면 **드론이 들어갈 칸을 탄약이 차지한다** — B 는 그 탄을 못 쏘므로
            // 마운트는 「만충」인데 **교대해도 아무것도 안 나가는** 상태가 된다.
            // 대기 마운트 합만 보던 태그 트리거가 그 거짓 만충에 걸리던 자리이기도 하다.
            if (UsesDroneMount(s)) return;

            for (int k = 0; k < 3; k++)
            {
                var kind = (AmmoKind)k;
                float have = s.ammo.StockOf(kind);
                if (have <= 0f) continue;

                MountItem item = MountItemMap.From(kind);
                float loaded = s.mount.Load(item, have);
                if (loaded > 0f) s.ammo.TryConsume(kind, loaded);
            }
        }

        // 스폰 시각 = index * spawnCadence. cadence<=0 이면 전원 t=0.
        private readonly List<EnemyProjectile> _enemyProjectiles = new List<EnemyProjectile>();

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
                    position = SpawnRingRule.Position(
                        RobotPosition, _spawnedCount, _spawnQueue.Count, SpawnDistance(_spawnedCount)),
                    hp = s.hp,
                    maxHp = s.hp,
                    def = s.def,
                    atk = s.atk,
                    moveSpeed = s.moveSpeed,
                    attackRange = s.attackRange,
                    attackInterval = s.attackInterval,
                    attackCooldown = 0f, // 사거리 진입 즉시 첫 타
                    radius = s.radius,
                    projectileSpeed = s.projectileSpeed,
                });
                _spawnedCount++;
            }
        }

        private void MoveAndAttackEnemies(float dt)
        {
            foreach (CombatEntity e in _enemies)
            {
                if (!e.IsAlive) continue;
                Vector2 toRobot = Act.body.position - e.position;
                float dist = toRobot.magnitude;

                // ⚠️ **사거리는 표면 사이 거리다**(2026-09-15 · 육안 ⑤ 결함 · 실측).
                //
                // 종전에는 **중심 사이 거리**를 사거리와 그대로 견줬다. 그런데 적은
                // `IsBlocked` 때문에 **반경 합까지만** 다가갈 수 있다 —
                // 로봇 0.666 + 보병 0.334 = **1.000**, 그리고 보병 사거리도 **1.000**이다.
                // 둘이 정확히 같아서 적은 늘 「조금 먼」 쪽에 서고 **영원히 이동 분기에
                // 머물렀다.** 실측: 로봇이 가만히 있으면 30초 동안 **로봇 HP 가 한 번도 안 깎였고**,
                // 걸으면 거리가 흔들려 그때만 맞았다(육안 ⑤ 「가까이 와도 공격 안 함」).
                //
                // **「사거리 1」은 「표면에서 한 칸 떨어져 때린다」는 뜻**이지 「중심이 1 이내」가
                // 아니다. 중심으로 재면 **큰 적일수록 사거리가 짧아진다** — 보스는 반경이 커서
                // 더 못 때리게 된다. 그 이상함이 이 결함의 뿌리다.
                //
                // ⚠️ **모든 적의 유효 사거리가 반경 합만큼 늘어난다**(보병은 1.0 → 2.0).
                // 이것은 밸런스에 닿는 변화이고 **설계 역기입 대상**이다 — 다만 「접촉했는데
                // 못 때린다」는 어떤 값으로도 정당화되지 않으므로 결함으로 고친다.
                float reach = e.attackRange + Act.body.radius + e.radius;

                if (dist > reach)
                {
                    // 4방향 이동(구현 사양). 대각선은 두 축을 번갈아 낸다.
                    //
                    // ⚠️⚠️ **주 축이 막히면 부 축으로 한 칸 본다**(2026-09-16 사용자 확정 · §74-12 B).
                    //
                    // 🗑️ 구 주석 「통과하지도, 밀어내지도, **돌아가지도** 않는다 · 경로 탐색을
                    //    넣으면 장갑형 길막이 사라지므로 우회는 금지」는 폐기됐다.
                    //    그 규칙은 장갑형 **하나**가 길을 막는 그림이었는데, 보병 120 기에서는
                    //    저희끼리 막아 **97%가 굳었다**(실측 81/83 · 사거리 안 1).
                    //
                    // ⚠️ 경로 탐색이 아니다 — **한 칸짜리 곁눈질**이고 둘 다 막히면 그대로 선다.
                    //    통과·밀어내기는 여전히 없다.
                    Vector2? next = GridMovement.StepOrSide(
                        e.position, Act.body.position, e.moveSpeed * dt, e.radius, e, _enemies, Act.body,
                        ref e.sideStepHold, ref e.sideStepDir, _sideStepHold, dt);
                    if (next.HasValue) e.position = next.Value;

                    e.attackCooldown = 0f; // 접근 중엔 즉시 타격 준비
                }
                else
                {
                    e.attackCooldown -= dt;
                    if (e.attackCooldown <= 0f)
                    {
                        e.attackCooldown += Mathf.Max(0.0001f, e.attackInterval);

                        // ⚠️ **투사체는 여기서 피해를 주지 않는다**(2026-09-11 · §71-33 ②).
                        // 날아가는 것 하나를 낳고 끝낸다 — 회피도 무적도 **맞는 순간**에 본다.
                        // 쏘는 순간에 판정하면 「피했는데 맞았다」가 생긴다.
                        if (e.projectileSpeed > 0f) { LaunchProjectile(e); continue; }

                        // 자동 회피는 **명중 판정에 들어오는 순간** 판정한다. 위협 반대 방향으로 뺀다.
                        // 이미 수동으로 피하고 있으면 재발동 금지에 걸려 추진제가 두 번 나가지 않는다.
                        TryBodyDodge((Act.body.position - e.position).normalized);

                        // ⚠️ 무적은 **판정식의 항이 아니다.** 계산에 진입하지 않고 통째로 건너뛴다 —
                        // 판정식이 max(1, …)라 「방어 무한대」로 표현하면 여전히 1이 꽂힌다.
                        HitsTaken++;
                        if (BodyIsInvincible) { DamageAvoided += e.atk; continue; }

                        // 회피 → **쉴드** → HP. 쉴드가 먹고 남은 것만 HP 로 간다.
                        float toBody = AbsorbWithShield(e.atk);
                        if (toBody <= 0f) continue;

                        DamageTaken += toBody;
                        Act.body.hp -= toBody; // 로봇 방어 스탯 없음 — 받는 피해 = 몬스터 공격력(§9)
                    }
                }
            }
        }

        /// <summary>
        /// 쏜 순간의 로봇 자리를 겨눠 한 발 낳는다. **유도가 아니다** — 방향과 사거리가 여기서 굳는다.
        /// </summary>
        private void LaunchProjectile(CombatEntity e)
        {
            Vector2 aim = Act.body.position;
            Vector2 d = aim - e.position;
            float dist = d.magnitude;
            // 겹쳐 있으면 방향이 안 선다 — 그때는 즉발로 떨어뜨린다(0 나눗셈 자리).
            if (dist <= 0.0001f) { Act.body.hp -= e.atk; return; }

            _enemyProjectiles.Add(new EnemyProjectile
            {
                position = e.position,
                aim = aim,
                direction = d / dist,
                speed = e.projectileSpeed,
                atk = e.atk,
                remaining = dist,
            });
        }

        /// <summary>
        /// 날아가는 것들을 옮기고 **접촉**을 본다 (명중 = 접촉 · 사용자 확정).
        ///
        /// ⚠️ **역순으로 지운다.** 앞에서부터 지우면 뒤 원소가 당겨져 **한 발씩 건너뛴다** —
        /// 화면에서는 「가끔 안 맞는 포탄」으로 보이고 원인이 안 읽힌다.
        /// </summary>
        private void MoveEnemyProjectiles(float dt)
        {
            // 로봇 반경이 0 인 시험 설정이 있다 — 그때 접촉이 영영 안 서면 포탄이 지나가 버린다.
            float contact = Mathf.Max(Act.body.radius, 0.25f);

            for (int i = _enemyProjectiles.Count - 1; i >= 0; i--)
            {
                EnemyProjectile p = _enemyProjectiles[i];
                float step = p.speed * dt;
                p.position += p.direction * step;
                p.remaining -= step;

                if (EnemyAttackRule.Hits(p.position, Act.body.position, contact))
                {
                    _enemyProjectiles.RemoveAt(i);

                    // 회피·무적은 **여기서** 본다. 위협 방향은 포탄이 온 쪽이다.
                    TryBodyDodge(-p.direction);

                    HitsTaken++;
                    if (BodyIsInvincible) { DamageAvoided += p.atk; continue; }

                    float toBody = AbsorbWithShield(p.atk);
                    if (toBody <= 0f) continue;

                    DamageTaken += toBody;
                    Act.body.hp -= toBody;
                    continue;
                }

                // 조준점을 지나쳤다 — 피한 것이다. 유도가 아니라서 여기서 끝난다.
                if (p.remaining <= 0f) { _enemyProjectiles.RemoveAt(i); continue; }

                _enemyProjectiles[i] = p;
            }
        }

        // ── 드론(로봇 B) ─────────────────────────────────────────────────────
        // 유입 = 생산이고, 실효 방출량 = min(유입, 슬롯 × 방출률)이다.
        // **1기 = 1회 타격 = 충전량 전량** — 등가선이 그렇게 맞는다:
        //   초당 1기(pB) × 기당 100(dB) = DPS 100. 나눠 쏘면 등가선을 벗어난다.
        // 「단발 고밀도(관통형)」라는 밸런스 표현이 이 구조를 가리킨다.
        private void DroneTick(float dt)
        {
            if (Act.bay == null) return;

            // 유입은 **마운트로** 들어간다 — 드론은 로봇 B의 탄약이고, 탄약이 있는 곳은 마운트다.
            LoadDronesInto(Act, dt);

            // ⚠️ **둘을 합쳐 셈한다** — 어느 종이든 슬롯 하나를 쓴다.
            float stock = Act.mount != null
                ? Act.mount.AmountOf(MountItem.Drone) + Act.mount.AmountOf(MountItem.DroneAoe)
                : 0f;
            int launched = Act.bay.Launch(dt, stock);

            for (int i = 0; i < launched; i++)
            {
                // ⚠️ **사출구에서 난다**(15-2 9장 Unity 반영 규격) — 로봇 자리다.
                //    나온 뒤 어디로 가는가는 **종이 정한다**(아래 `MoveDrones`).
                // ⚠️⚠️ **종은 이제 재고가 정한다**(2026-09-16 · 조립 문서 7-3-1).
                //
                // 🗑️ 구 길은 `_droneKind.Next(AoeDroneShare)` — **생산 비율**로 골랐다.
                // 그때는 마운트가 종을 구분 못 해 달리 방법이 없었다. 이제 **실제로
                // 실려 있는 것**에서 꺼내므로 비율을 따로 들 이유가 없다.
                //
                // 📌 **난수 없음** — 많이 남은 쪽을 먼저 쓰고, 같으면 누적형이다.
                //    그러면 둘이 섞여 들어와도 한쪽만 쌓이지 않는다.
                float haveStack = Act.mount != null ? Act.mount.AmountOf(MountItem.Drone) : 0f;
                float haveAoe = Act.mount != null ? Act.mount.AmountOf(MountItem.DroneAoe) : 0f;
                DroneKind kind = haveAoe > haveStack ? DroneKind.Aoe : DroneKind.Stack;

                MountItem item = kind == DroneKind.Aoe ? MountItem.DroneAoe : MountItem.Drone;
                if (Act.mount != null && !Act.mount.TryConsume(item, 1f))
                {
                    // 셈과 재고가 어긋났다 — 슬롯을 돌려주고 이 기체는 안 낸다.
                    Act.bay.Retire();
                    continue;
                }

                // 광역형은 궤도 각을 **고르게 흩어** 시작한다 — 전부 0 에서 나면
                // 여러 기가 한 점에 겹쳐 한 기처럼 보인다. 난수가 아니라 **차례**다.
                int slots = Mathf.Max(1, Act.setup.droneSlots);
                float angle = Mathf.PI * 2f * (_droneSpawned % slots) / slots;
                _droneSpawned++;

                float perHit = Act.setup.droneDamagePerHit > 0f
                    ? Act.setup.droneDamagePerHit
                    : Act.setup.droneCharge;   // 0 = 전량(구 거동)
                Act.drones.Add(new DroneUnit(Act.body.position,
                    Act.setup.droneCharge, perHit, Act.setup.droneAttackRange,
                    kind, angle));
                // 사출 연출이 붙는 자리 — 판정은 위에서 이미 끝났다.
                _droneLaunches.Add(Act.body.position);
            }

            MoveDrones(dt);

            // 사격 — 표적은 본체와 같은 최근접 규칙이되 **기준점이 드론 자신**이라
            // 본체와 다른 적을 칠 수 있다(자동 전투 구현 사양).
            for (int i = Act.drones.Count - 1; i >= 0; i--)
            {
                DroneUnit d = Act.drones[i];

                // ⚠️⚠️ **누적형은 붙은 적만 친다**(2026-09-16 사용자 확정).
                //    최근접을 다시 고르면 붙어 있는 뜻이 사라진다 — 옆에 더 가까운 적이
                //    지나가는 순간 표적이 갈아타 「붙어서 다 쓴다」가 성립하지 않는다.
                CombatEntity target = d.Kind == DroneKind.Stack
                    ? (d.Attached ? d.Target as CombatEntity : null)
                    : NearestLivingEnemyWithin(d.Position, d.AttackRange);
                if (target == null || !target.IsAlive) continue;

                // ⚠️ **타격 간격**(2026-09-16 사용자 육안 3차). 갓 사출된 기체는
                //    시계가 0 이라 **바로 한 방** 나가고, 그 뒤부터 간격이 걸린다.
                if (d.HitCooldown > 0f) continue;

                float dealt = d.Fire();
                if (dealt <= 0f) continue;
                d.HitCooldown = DroneHitInterval;

                // 판정식을 다시 만들지 않는다 — 본체 사격과 같은 식을 탄다.
                float applied = DamageFormula.PerHit(dealt, Act.setup.mountCoef, Act.setup.moduleMult, target.def);
                target.hp -= applied;
                DroneDamageDealt += applied;

                // ⚠️ **광역형은 주위 적 전부를 친다**(사용자 확정). 한 대상 피해를
                //    그대로 다른 적에게도 얹는다 — 감쇠는 값이라 안 지어낸다.
                if (d.Kind == DroneKind.Aoe)
                    foreach (CombatEntity other in _enemies)
                    {
                        if (other == target || !other.IsAlive) continue;
                        if ((other.position - d.Position).sqrMagnitude > d.AttackRange * d.AttackRange) continue;
                        float more = DamageFormula.PerHit(dealt, Act.setup.mountCoef,
                            Act.setup.moduleMult, other.def);
                        other.hp -= more;
                        DroneDamageDealt += more;
                    }

                _shots.Add(new ShotEvent
                {
                    from = d.Position, to = target.position,
                    kind = AmmoKind.Pierce, // 드론 = 단발 고밀도(관통형)
                    killed = target.hp <= 0f,
                    // ⚠️⚠️ **여기에 사거리를 넣으면 안 된다**(2026-09-16 · 사용자 육안 5차).
                    //
                    // 🗑️ 구 줄은 <c>aoeRadius = d.AttackRange</c> 였다. 그 칸은 **폭발탄의
                    // 스플래시 그림**을 위한 것이고, 화면이 그것을 **지름 = 반경 × 2** 인
                    // 반투명 사각으로 그린다 — 사거리 9.2 를 넣었더니 **한 변 18.4 유닛짜리
                    // 주황 사각**이 화면 절반을 덮었다.
                    //
                    // 📌 **광역형의 광역 타격에는 그림이 없다.** 없는 자산을 있는 칸에
                    // 밀어 넣지 않는다 — 연출이 필요하면 그때 제 자산으로 낸다.
                    aoeRadius = 0f,
                });

                // 충전량을 다 썼으면 소멸 — 슬롯은 즉시 빈다.
                if (!d.IsAlive)
                {
                    _droneExpiries.Add(d.Position); // 소멸 연출이 붙는 자리
                    Act.drones.RemoveAt(i);
                    Act.bay.Retire();
                }
            }
        }

        /// <summary>
        /// **드론을 옮긴다 — 종이 규칙을 가른다**
        /// (2026-09-16 사용자 확정 · 플랜 §74-21 · §74-3 #30).
        ///
        /// 🗑️ **구 `DroneStation()` 정박 폐기.** 사출되는 순간 로봇 둘레 고정 오프셋에
        /// 놓고 **그 뒤 아무도 위치를 안 바꿨다** — 로봇이 걸어가면 드론만 뒤에 남았다.
        /// 문서에 이동 규칙이 없어 구현이 고른 가정이었고, 사용자가 화면을 보고 뒤집었다.
        ///
        /// · **광역형** — 로봇 주변 궤도. **로봇이 걸어도 따라온다**(자리를 각으로 들고
        ///   매 틱 로봇 자리에서 다시 낸다 — 그래야 뒤처지지 않는다).
        /// · **누적형** — 사거리 안 한 적에게 **날아가 붙는다.** 붙으면 그 적을 따라다니며
        ///   충전량을 다 쓸 때까지 때리고, **그 적이 죽으면 다음 적으로 옮긴다**(가정).
        ///
        /// ⚠️ **값 넷은 전부 가정이다**(`CombatTuning` 의 `drone*Tbd` · 설계 역기입 자리).
        /// ⚠️ **난수 0** — 궤도 각도 표적 고르기도 차례와 거리로만 정한다.
        /// </summary>
        private void MoveDrones(float dt)
        {
            if (dt <= 0f) return;
            Vector2 robot = Act.body != null ? Act.body.position : Vector2.zero;

            foreach (DroneUnit d in Act.drones)
            {
                // ⚠️ **시계는 이동과 함께 돈다** — 사격 단계보다 먼저 줄어야
                //    간격이 정확히 이 값이 된다. 사격 뒤에 줄이면 한 틱씩 늘어난다.
                if (d.HitCooldown > 0f) d.HitCooldown = Mathf.Max(0f, d.HitCooldown - dt);

                if (d.Kind == DroneKind.Aoe)
                {
                    d.OrbitAngle += DroneOrbitSpeed * dt;
                    d.Position = robot + new Vector2(Mathf.Cos(d.OrbitAngle),
                        Mathf.Sin(d.OrbitAngle)) * DroneOrbitRadius;
                    continue;
                }

                // ── 누적형 ────────────────────────────────────────────────
                var target = d.Target as CombatEntity;
                if (target == null || !target.IsAlive)
                {
                    // ⚠️ **표적은 드론 자신을 기준으로 고른다** — 로봇 기준으로 고르면
                    //    멀리 날아간 드론이 등 뒤의 적으로 되돌아온다.
                    target = NearestLivingEnemyWithin(d.Position, d.AttackRange);
                    d.Target = target;
                    d.Attached = false;
                    if (target == null) continue;   // 칠 것이 없으면 제자리에 뜬다
                }

                Vector2 to = target.position - d.Position;
                float dist = to.magnitude;
                if (dist <= DroneAttachDistance)
                {
                    d.Attached = true;
                    d.Position = target.position;   // 붙었다 — 적을 따라다닌다
                    continue;
                }

                d.Attached = false;
                float step = Mathf.Min(DroneFlySpeed * dt, dist);
                d.Position += to / Mathf.Max(dist, 1e-5f) * step;
            }
        }

        /// <summary>광역형 궤도 반경 — ⚠️ 가정. 러너가 `CombatTuning` 에서 넣는다.</summary>
        public float DroneOrbitRadius { get; set; } = 1.6f;

        /// <summary>광역형 각속도(라디안/초) — ⚠️ 가정.</summary>
        public float DroneOrbitSpeed { get; set; } = 1.6f;

        /// <summary>누적형 비행 속도 — ⚠️ 가정.</summary>
        public float DroneFlySpeed { get; set; } = 6f;

        /// <summary>누적형이 붙었다고 보는 거리 — ⚠️ 가정.</summary>
        public float DroneAttachDistance { get; set; } = 0.35f;

        /// <summary>
        /// 드론 **타격 간격**(초) — ⚠️ 가정 0.5 (2026-09-16 사용자 육안 3차).
        ///
        /// 기당 피해가 충전량의 1/10 이므로 이 간격이면 **한 기가 약 5초** 붙어 있다.
        /// ⚠️ **누적형·광역형 같은 간격**이다 — 두 종을 가르는 것은 이동과 표적이다.
        /// </summary>
        public float DroneHitInterval { get; set; } = 0.5f;

        /// <summary>
        /// **광역형이 차지하는 몫**(0~1) — 보드의 복합 군수가 무엇을 돌리는가.
        ///
        /// 📌 종을 가르는 것은 전투가 아니라 **보드**다. 0 이면 전부 누적형이다
        /// (지금 시작 보드 B 가 누적형 조합표만 돌리므로 그 값이다).
        /// </summary>
        public float AoeDroneShare { get; set; }

        private readonly DroneKindPicker _droneKind = new DroneKindPicker();

        /// <summary>여태 사출한 수 — 궤도 각을 고르게 흩는 데만 쓴다(난수 아님).</summary>
        private int _droneSpawned;

        /// <summary>주어진 기준점에서 사거리 안 최근접 생존 적. 동률은 먼저 등장한 쪽.</summary>
        private CombatEntity NearestLivingEnemyWithin(Vector2 origin, float range)
        {
            CombatEntity best = null;
            float bestSqr = range * range;
            foreach (CombatEntity e in _enemies)
            {
                if (!e.IsAlive) continue;
                float sqr = (e.position - origin).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = e; }
            }
            return best;
        }

        private void RobotFire(float dt)
        {
            // 합체 중에는 **두 로봇이 모두 쏘고**, 각 발에 합체 배율이 곱해진다.
            // 그래야 「합체 화력 = (A 화력 + B 화력) × 1.8」이 DPS 수준에서 정확히 성립한다
            // (밸런스 5-2). 배율을 발사율에 얹으면 발사 리듬이 바뀌어 같은 값이 안 나온다.
            if (Merge != null && Merge.IsActive)
            {
                for (int i = 0; i < _sides.Length; i++)
                    FireSide(_sides[i], dt, MergeSystem.MergeMultiplier);
                return;
            }

            FireSide(Act, dt, 1f);
        }

        private void FireSide(RobotSide side, float dt, float damageMultiplier)
        {
            List<AmmoLine> lines = side.setup.lines;
            if (lines == null || lines.Count == 0) return;

            // 사거리 내 살아있는 적이 있을 때만 사격(공백 후 버스트 방지 위해 타겟 있을 때만 누적).
            CombatEntity target = NearestLivingEnemyInRange();
            if (target == null) return;

            // 라인마다 제 주기로 발사. 순회 순서 고정 = 결정론 유지(난수 0).
            for (int li = 0; li < lines.Count && li < side.lineTimers.Length; li++)
            {
                AmmoLine shot = lines[li];
                if (shot.shotsPerSec <= 0f) continue;

                side.lineTimers[li] += shot.shotsPerSec * dt;
                // 허용오차: dt를 잘게 더하면 1발/초가 정확히 1.0이 아니라 0.9999…로 끝나 그 발이 다음 틱으로 밀린다.
                // 잔여가 이월되므로 장기 발사율은 맞지만, 초 경계에서 한 발이 늦어 "1초 피해 = 명목 출력"(§5-6 계약)이
                // 딱 떨어지지 않는다. 계약을 경계에서도 성립시키기 위한 허용오차다.
                while (side.lineTimers[li] >= 1f - FireEpsilon)
                {
                    side.lineTimers[li] -= 1f;

                    target = NearestLivingEnemyInRange();
                    if (target == null) { side.lineTimers[li] = 0f; break; }

                    // 탄약 소진 = 공격 정지(밸런스 확정 원칙). **그 탄종의** 재고가 없으면 그 발은 나가지 않는다
                    // — 다른 탄종이 쌓여 있어도 대신 쏘지 않는다.
                    //
                    // 소비하는 곳은 **마운트**다(V03 §2). 흐름이 군수 → 창고 → 벨트 → 마운트 → 소비이므로
                    // 실제 탄약이 있는 곳은 마운트이고, 창고에서 빼면 이송분이 이중으로 사라진다.
                    // 마운트가 없는 구성(격리 전투·단일 로봇)은 창고에서 바로 쓴다.
                    if (!ConsumeRound(side, shot.kind)) { side.lineTimers[li] = 0f; break; }

                    // **실제로 나간 발**만 센다 — 배분과 발사는 다르다(2026-09-15).
                    int fi = (int)shot.kind;
                    if (fi >= 0 && fi < _firedCount.Length) _firedCount[fi]++;

                    FireOne(side, shot, target, damageMultiplier);
                }
            }
        }

        // 한 발 처리: 히트 패턴 해석 → 판정식 적용 → 연출 이벤트.
        /// <summary>한 발 소비. 마운트가 있으면 마운트에서, 없으면 창고에서 뺀다.</summary>
        private static bool ConsumeRound(RobotSide side, AmmoKind kind)
        {
            if (side.mount != null && side.mount.SlotCount > 0)
                return side.mount.TryConsume(MountItemMap.From(kind), 1f);

            return side.ammo.TryConsume(kind, 1f);
        }

        private void FireOne(RobotSide side, AmmoLine shot, CombatEntity target, float damageMultiplier)
        {
            // 탄종 히트 패턴(단일/멀티샷/AoE) 해석 → 각 표적에 판정식(발당피해×배율) 적용.
            List<HitTarget> hits = HitResolver.Resolve(shot.kind, target, _enemies,
                side.setup.multiShotCount, side.setup.aoeRadius, side.setup.aoeSplashFactor);

            foreach (HitTarget h in hits)
            {
                // 합체 배율은 **판정식 결과에** 곱한다. 발당피해에 곱하면 방어를 빼기 전에 커져
                // 「(A 화력 + B 화력) × 배율」과 값이 달라진다 — 화력은 방어 반영 후 값이다.
                float dmg = DamageFormula.PerHit(shot.damagePerShot * h.damageFactor,
                    side.setup.mountCoef, side.setup.moduleMult, h.entity.def) * damageMultiplier;
                h.entity.hp -= dmg;
            }

            // 연출: 실제 스플래시가 있는 폭발(드론 광역형)만 착탄점 탄선 1발 + 폭발 광역 원.
            //        스플래시 0(로봇A 폭발=단일)·멀티샷·단일 = 표적별 탄선/플래시.
            if (shot.kind == AmmoKind.Explosive && Act.setup.aoeSplashFactor > 0f)
            {
                _shots.Add(new ShotEvent
                {
                    from = Act.body.position,
                    to = target.position,
                    kind = shot.kind,
                    killed = !target.IsAlive,
                    aoeRadius = Act.setup.aoeRadius,
                });
            }
            else
            {
                foreach (HitTarget h in hits)
                    _shots.Add(new ShotEvent
                    {
                        from = Act.body.position,
                        to = h.entity.position,
                        kind = shot.kind,
                        killed = !h.entity.IsAlive,
                        aoeRadius = 0f,
                    });
            }
        }

        private CombatEntity NearestLivingEnemyInRange()
        {
            CombatEntity best = null;
            float bestSqr = Act.setup.attackRange * Act.setup.attackRange;
            foreach (CombatEntity e in _enemies)
            {
                if (!e.IsAlive) continue;
                float sqr = (e.position - Act.body.position).sqrMagnitude;
                // 엄격 부등호 — 동률이면 **먼저 등장한 쪽**이 이긴다(구현 사양 확정).
                // <= 로 두면 나중에 스폰된 쪽이 표적을 빼앗아 표적이 계속 흔들린다.
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = e;
                }
            }
            return best;
        }

        // 처치 집계는 **여기 한 곳**에서만 늘린다(§5-7 고철 적립의 유일한 입력).
        // 데미지를 준 지점에서 세면 AoE 한 발이 여러 번 카운트되어 수입이 부풀려진다 —
        // 제거는 개체당 정확히 한 번뿐이므로 이 자리가 중복이 구조적으로 불가능한 지점이다.
        private void CleanupDead()
        {
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                if (_enemies[i].IsAlive) continue;
                _enemies.RemoveAt(i);
                KillsThisTick++;
                TotalKills++;
            }
        }

        private void Evaluate()
        {
            if (Act.body.hp <= 0f)
            {
                Act.body.hp = 0f;
                Result = CombatResult.LoseDead;
                return;
            }
            // 상주 파밍 층은 끝나지 않는다 — 전멸(승리)도 제한시간(패배)도 적용하지 않는다.
            // 스포너가 계속 보충하므로 "전원 스폰 후 전멸"이 성립할 수 없고, 도전 층의 120초 제한도
            // 파밍에는 없다(스테이지 기획서「이층 구조」). 로봇 파괴만 남는다.
            if (Endless) return;

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
