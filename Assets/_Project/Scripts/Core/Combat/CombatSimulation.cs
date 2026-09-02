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
            public float droneInflowRate;       // 드론 몸체 유입(기/초)

            // 회피는 로봇마다 따로 든다 — 대기 보드의 부스터도 계속 돌아 추진제를 쌓는다.
            public readonly DodgeSystem dodge = new DodgeSystem();
            public float propellantSupplyRate;  // 부스터 유입(개/초)
            public float propellantCarry;       // 소수분 이월 — 15초에 1개라 한 틱에 1개가 안 나온다
        }

        private readonly RobotSide[] _sides;
        private int _active;

        /// <summary>지금 나가 있는 로봇의 상태 묶음.</summary>
        private RobotSide Act => _sides[_active];

        private readonly List<CombatEntity> _enemies = new List<CombatEntity>();
        private readonly List<EnemySpawn> _spawnQueue;
        private readonly Vector2[] _spawnPositions;
        private readonly List<ShotEvent> _shots = new List<ShotEvent>();

        private readonly float _arenaRadius;
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
        /// 규칙은 버스트와 같다: **최근접 1체** · 교대 프레임에 1회 ·
        /// **표적이 없으면 발동 보류**(false를 주면 마운트도 안 비워진다).
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

            CombatEntity target = NearestLivingEnemyWithin(side.body.position, side.setup.attackRange);
            if (target == null) return false; // 표적이 없으면 보류 — 재고는 만재로 남는다

            float avg = AverageDamagePerItem(side, target, loadedRounds);
            float damage = GrandEntrance.Damage(true, loadedRounds, avg);
            if (damage <= 0f) return false;

            target.hp -= damage;
            LastTagSkillDamage = damage;

            _shots.Add(new ShotEvent
            {
                from = side.body.position, to = target.position,
                kind = AmmoKind.Explosive, // 쏟아붓기 — 폭발 연출로 그린다
                killed = target.hp <= 0f, aoeRadius = 0f,
            });
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
            if (Tag == null || !Tag.TryManualTag()) return false;
            _active = Tag.ActiveIndex;
            return true;
        }

        /// <summary>이번 전투에서 드론이 낸 누적 피해(검산용).</summary>
        public float DroneDamageDealt { get; private set; }

        public CombatResult Result { get; private set; } = CombatResult.InProgress;
        public float Elapsed { get; private set; }
        public CombatEntity Robot => Act.body;
        public IReadOnlyList<CombatEntity> Enemies => _enemies;
        public IReadOnlyList<ShotEvent> ShotsThisTick => _shots;
        public int TotalEnemies => _spawnQueue.Count;
        public int Remaining => _enemies.Count;

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
                    position = FarmSpawnRule.RingPosition(i, batch.Count, _arenaRadius),
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
            _arenaRadius = arenaRadius;
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
            _spawnPositions = BuildSpawnPositions(_spawnQueue.Count, arenaRadius);

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
            if (s.mount == null || s.droneInflowRate <= 0f || dt <= 0f) return;
            s.mount.Load(MountItem.Drone, s.droneInflowRate * dt);
        }

        /// <summary>
        /// 추진제 유입 + 수동 플릭 소비. **적 공격 판정보다 먼저** 돈다:
        /// 수동이 먼저 발동해 있으면 자동은 재발동 금지에 걸려 그냥 지나가고,
        /// 그 결과 「수동이 이기고 추진제는 1개만」이 순서만으로 성립한다.
        /// </summary>
        private void DodgeTick(float dt)
        {
            for (int i = 0; i < _sides.Length; i++) _sides[i].dodge.Tick(dt);

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
            KillsThisTick = 0;
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
            if (s.ammoSupplyRate > 0f)
            {
                // 대기 로봇은 소비가 0이라 생산 전량이 쌓인다(조립 문서「소비까지의 흐름」).
                // 탄종 배정이 아직 물류에 없으므로 활성과 같은 과도 규칙을 쓴다.
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

        /// <summary>교대 판정 → 발동. 교대하면 활성 인덱스가 바뀐다.</summary>
        private void TagTick(float dt)
        {
            if (Tag == null) return;

            if (Tag.TickAuto(dt)) _active = Tag.ActiveIndex;
        }

        /// <summary>
        /// 창고 → 마운트 이송. 탄종별로 실을 수 있는 만큼만 실린다.
        /// **마운트가 만충 판정 주체**이므로(V03 §2) 이 이송이 태그 트리거를 만든다.
        /// </summary>
        private void RefillMount(RobotSide s)
        {
            if (s.mount == null || s.mount.SlotCount <= 0) return;

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
                Vector2 toRobot = Act.body.position - e.position;
                float dist = toRobot.magnitude;

                if (dist > e.attackRange)
                {
                    // 4방향 이동(구현 사양). 대각선은 두 축을 번갈아 낸다.
                    Vector2 next = GridMovement.Step(e.position, Act.body.position, e.moveSpeed * dt);

                    // 막히면 **멈춘다** — 통과하지도, 밀어내지도, 돌아가지도 않는다.
                    // 경로 탐색을 넣으면 장갑형 길막이 사라지므로 우회는 금지다.
                    if (!GridMovement.IsBlocked(next, e.radius, e, _enemies, Act.body))
                        e.position = next;

                    e.attackCooldown = 0f; // 접근 중엔 즉시 타격 준비
                }
                else
                {
                    e.attackCooldown -= dt;
                    if (e.attackCooldown <= 0f)
                    {
                        e.attackCooldown += Mathf.Max(0.0001f, e.attackInterval);

                        // 자동 회피는 **명중 판정에 들어오는 순간** 판정한다. 위협 반대 방향으로 뺀다.
                        // 이미 수동으로 피하고 있으면 재발동 금지에 걸려 추진제가 두 번 나가지 않는다.
                        Act.dodge.TryDodge(true, (Act.body.position - e.position).normalized,
                            false, Vector2.zero);

                        // ⚠️ 무적은 **판정식의 항이 아니다.** 계산에 진입하지 않고 통째로 건너뛴다 —
                        // 판정식이 max(1, …)라 「방어 무한대」로 표현하면 여전히 1이 꽂힌다.
                        if (Act.dodge.IsInvincible) continue;

                        Act.body.hp -= e.atk; // 로봇 방어 스탯 없음 — 받는 피해 = 몬스터 공격력(§9)
                    }
                }
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

            int launched = Act.bay.Launch(dt, Act.mount != null ? Act.mount.AmountOf(MountItem.Drone) : 0f);
            if (launched > 0 && Act.mount != null) Act.mount.TryConsume(MountItem.Drone, launched);

            for (int i = 0; i < launched; i++)
                Act.drones.Add(new DroneUnit(DroneStation(Act.drones.Count),
                    Act.setup.droneCharge, Act.setup.droneCharge, Act.setup.droneAttackRange));

            // 사격 — 표적은 본체와 같은 최근접 규칙이되 **기준점이 드론 자신**이라
            // 본체와 다른 적을 칠 수 있다(자동 전투 구현 사양).
            for (int i = Act.drones.Count - 1; i >= 0; i--)
            {
                DroneUnit d = Act.drones[i];
                CombatEntity target = NearestLivingEnemyWithin(d.Position, d.AttackRange);
                if (target == null) continue;

                float dealt = d.Fire();
                if (dealt <= 0f) continue;

                // 판정식을 다시 만들지 않는다 — 본체 사격과 같은 식을 탄다.
                float applied = DamageFormula.PerHit(dealt, Act.setup.mountCoef, Act.setup.moduleMult, target.def);
                target.hp -= applied;
                DroneDamageDealt += applied;

                _shots.Add(new ShotEvent
                {
                    from = d.Position, to = target.position,
                    kind = AmmoKind.Pierce, // 드론 = 단발 고밀도(관통형)
                    killed = target.hp <= 0f, aoeRadius = 0f,
                });

                // 충전량을 다 썼으면 소멸 — 슬롯은 즉시 빈다.
                if (!d.IsAlive)
                {
                    Act.drones.RemoveAt(i);
                    Act.bay.Retire();
                }
            }
        }

        /// <summary>드론 정박 위치(로봇 주변 고정 오프셋). 결정론 — 난수 0.</summary>
        private Vector2 DroneStation(int index)
        {
            int slots = Mathf.Max(1, Act.setup.droneSlots);
            float angle = Mathf.PI * 2f * (index % slots) / slots;
            float ring = Mathf.Max(0.35f, Act.setup.radius * 1.5f);
            return Act.body.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
        }

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
