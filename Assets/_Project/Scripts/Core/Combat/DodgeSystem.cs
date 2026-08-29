using UnityEngine;

namespace MBI.Core
{
    /// <summary>회피가 어떻게 발동했는가. 겹치면 수동이 이긴다.</summary>
    public enum DodgeTrigger
    {
        None = 0,
        /// <summary>자동 — 적 공격이 명중 판정에 들어오는 순간.</summary>
        Auto,
        /// <summary>수동 — 화면 플릭. **이동 명령이 아니라 즉시 회피**다.</summary>
        Manual,
    }

    /// <summary>
    /// 회피(전투 시스템 문서 11-9장 · 07 문서「생존 체계」, 2026-08-29 신설).
    /// 순수 로직 — 씬 비의존이라 EditMode로 검증된다.
    ///
    /// 확정치: **무적 0.167초** · 추진제 1개 = 회피 1회 · **회피 스택 상한 = 부스터 대수 × 2**.
    ///
    /// 상한은 상수가 아니라 **대수의 파생값**이다(260829_V02 §① — 구 「상한 3」 폐기).
    /// 상수로 두면 부스터 1대와 3대가 같아져 「더 놓으면 강해진다」가 수치로 무너진다.
    /// 밸런스 문서「물류 천장 재검증」의 **천장은 생산 비용이 만든다**가 여기에도 적용된다.
    ///
    /// 그릇만 키워도 안 세지는 이유: **채우는 속도는 군수 노드가 정한다**(15초에 1개).
    /// 부스터는 담는 그릇이라 여섯 칸을 채우려면 90초가 든다.
    ///
    /// ⚠️ 「추진제 아이템 최대 스택 3」과 **다른 축**이다. 추진제는 물건이라 마운트에 쌓이고,
    /// 회피 스택은 부스터가 그 물건을 먹어 채우는 형체 없는 게이지다. 숫자가 3 근처라 섞이기 쉽다.
    ///
    /// 규칙:
    ///   - 자동 기본 + 수동 오버라이드(태그와 같은 문법)
    ///   - 수동 입력은 **화면 플릭**이고 드래그한 방향으로 피한다 — 걷는 명령이 아니다
    ///   - **회피 중에도 사격은 계속한다**
    ///   - 회피가 진행 중이면 재발동하지 않는다. **종료 모션이 끝나야** 다음이 나간다
    ///   - 자동과 수동이 겹쳐도 **추진제는 1개만** 나간다
    ///
    /// ⚠️ **회피는 판정식의 항이 아니다.** 무적 구간에서는 피해 계산에 **진입하지 않는다** —
    /// 판정식이 max(1, …) 구조라 어떤 값을 넣어도 최소 1이 들어가므로,
    /// 무적을 「방어력 무한대」로 표현하면 여전히 1이 꽂힌다.
    ///
    /// ⚠️ **무적은 시간으로 센다.** 프레임을 세면 30fps 기기에서 무적이 두 배가 된다.
    /// </summary>
    public sealed class DodgeSystem
    {
        /// <summary>무적 지속(초). 확정치 0.167.</summary>
        public const float InvincibleSeconds = 0.167f;

        /// <summary>부스터 노드 1대가 드는 회피 스택. 확정치 2(260829_V02).</summary>
        public const int StacksPerBooster = 2;

        /// <summary>
        /// 종료 모션 딜레이(초) — **미확정**. 무적과 **별개 값**이다:
        /// 합치면 무적을 늘리려다 연사 속도까지 바뀐다.
        /// 0 = 미측정 센티넬. 확정되면 SO로 승격한다(검증 대장 이월분).
        /// </summary>
        public const float RecoveryDelayTbd = 0f;

        private float _invincibleLeft;
        private float _recoveryLeft;
        private int _boosterCount;

        /// <summary>
        /// 보드에 놓인 부스터 대수. 상한의 원천이라 줄이면 넘치는 스택이 잘린다 —
        /// 노드를 뽑았는데 회피가 그대로 남으면 보드가 결과를 못 바꾸는 것이 된다.
        /// </summary>
        public int BoosterCount
        {
            get => _boosterCount;
            set
            {
                _boosterCount = Mathf.Max(0, value);
                if (Stacks > Capacity) Stacks = Capacity;
            }
        }

        /// <summary>회피 스택 상한 = 부스터 대수 × 2. 부스터가 없으면 0 — 회피 자체가 없다.</summary>
        public int Capacity => _boosterCount * StacksPerBooster;

        /// <summary>보유 회피 스택(회피 가능 횟수). 부스터가 추진제를 먹어 채운다.</summary>
        public int Stacks { get; private set; }

        /// <summary>무적 구간인가. **이 동안 피해 계산에 진입하지 않는다.**</summary>
        public bool IsInvincible => _invincibleLeft > 0f;

        /// <summary>회피 동작(무적 + 종료 모션) 중인가. 이 동안 재발동하지 않는다.</summary>
        public bool IsDodging => _invincibleLeft > 0f || _recoveryLeft > 0f;

        /// <summary>지금 회피할 수 있는가 — 진행 중이 아니고 추진제가 있다.</summary>
        public bool CanDodge => !IsDodging && Stacks > 0;

        /// <summary>마지막 회피가 어떻게 발동했는가(연출용).</summary>
        public DodgeTrigger LastTrigger { get; private set; }

        /// <summary>마지막 회피 방향(수동이면 플릭 방향, 자동이면 위협 반대). 연출용.</summary>
        public Vector2 LastDirection { get; private set; }

        /// <summary>이 전투에서 회피한 횟수(진단용).</summary>
        public int TotalDodges { get; private set; }

        /// <summary>
        /// 부스터가 추진제를 먹어 회피 스택을 채운다. **대수 × 2를 넘겨 쌓이지 않는다** —
        /// 넘치는 분은 버려지고, 그래서 부스터를 더 놓는 것이 회피를 늘리는 유일한 방법이다.
        /// 실제로 들어간 개수를 돌려준다.
        /// </summary>
        public int AddStacks(int count)
        {
            if (count <= 0) return 0;

            int room = Capacity - Stacks;
            int taken = Mathf.Min(room, count);
            Stacks += taken;
            return taken;
        }

        /// <summary>시간 경과. **프레임이 아니라 시간으로 센다.**</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            if (_invincibleLeft > 0f) _invincibleLeft = Mathf.Max(0f, _invincibleLeft - dt);
            else if (_recoveryLeft > 0f) _recoveryLeft = Mathf.Max(0f, _recoveryLeft - dt);
        }

        /// <summary>
        /// 이번 프레임의 회피 판정 → 발동까지. 발동했으면 true.
        ///
        /// **수동이 자동을 이긴다.** 둘이 겹쳐도 회피는 한 번이고 추진제도 1개만 나간다 —
        /// 두 경로가 각각 소비하면 플릭 한 번에 재고가 두 개 빠진다.
        /// </summary>
        public bool TryDodge(bool autoTriggered, Vector2 autoDirection,
            bool manualFlick, Vector2 flickDirection)
        {
            if (!manualFlick && !autoTriggered) return false;
            if (!CanDodge) return false;

            // 수동 우선 — 겹쳐도 한 번, 추진제도 하나.
            LastTrigger = manualFlick ? DodgeTrigger.Manual : DodgeTrigger.Auto;
            LastDirection = manualFlick ? flickDirection : autoDirection;

            Stacks--;
            TotalDodges++;
            _invincibleLeft = InvincibleSeconds;
            _recoveryLeft = RecoveryDelayTbd;
            return true;
        }

        /// <summary>스테이지 시작·재시작 초기화. 추진제는 보드가 다시 공급한다.</summary>
        public void Reset()
        {
            _invincibleLeft = 0f;
            _recoveryLeft = 0f;
            Stacks = 0; // 부스터 대수는 보드가 정하는 것이라 여기서 지우지 않는다
            TotalDodges = 0;
            LastTrigger = DodgeTrigger.None;
            LastDirection = Vector2.zero;
        }
    }
}
