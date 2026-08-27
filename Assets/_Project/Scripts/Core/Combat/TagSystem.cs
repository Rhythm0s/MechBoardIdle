using UnityEngine;

namespace MBI.Core
{
    /// <summary>태그 인이 일어난 이유. 어느 쪽이냐에 따라 등장의 격이 다르다.</summary>
    public enum TagEntry
    {
        /// <summary>발동 없음.</summary>
        None = 0,
        /// <summary>주 트리거 — 대기 로봇 비축 만충. **만재 등장**(보상형), 등장 특공을 동반한다.</summary>
        Full,
        /// <summary>보조 트리거 — 활성 로봇 소진. **약한 등장**(동기화 실패의 물리적 처벌).</summary>
        Depleted,
        /// <summary>수동 태그 — 보스전 등 컨트롤 콘텐츠 대비.</summary>
        Manual,
    }

    /// <summary>
    /// 태그(교대) 시스템(전투 시스템 문서 2-1장 · 밸런스 문서「태그 시스템 수치」).
    ///
    /// 설계 의도는 **구문법 — 축적 → 만재 등장**이다. 태그 주기 자체가 플레이어의 설계물이 된다:
    /// 대기 보드의 생산 속도와 저장 용량이 곧 주기이므로, 임의 쿨다운으로 리듬을 만들지 않는다.
    /// 5초 쿨다운은 리듬 장치가 아니라 **수동 태그 진동 방지 안전장치**다.
    ///
    /// 순수 로직 — 씬·시뮬 비의존이라 EditMode로 검증된다. 실제 로봇 교체 배선은 호출자가 한다.
    /// </summary>
    public sealed class TagSystem
    {
        /// <summary>태그 후 전역 쿨다운(초). 확정치 5 — 수동 태그 진동 방지.</summary>
        public const float CooldownSeconds = 5f;

        /// <summary>
        /// 교대 공백(초) — **미확정**. 밸런스에는 교대 계수 0.96만 있고 초 단위 값이 원천에 없다.
        /// 0 = 미측정 센티넬. 확정되면 여기가 아니라 SO로 승격한다.
        /// </summary>
        public const float SwitchGapTbd = 0f;

        private float _cooldown;

        /// <summary>남은 쿨다운(초).</summary>
        public float CooldownRemaining => _cooldown;

        /// <summary>지금 태그할 수 있는가. 쿨다운 중이거나 잠겼으면 불가.</summary>
        public bool CanTag => _cooldown <= 0f && !Locked;

        /// <summary>
        /// 상위 잠금 — **합체 중에는 태그가 불가**하다(전투 문서 4장). 두 공장은 계속 돌아
        /// 비축이 쌓이고, 합체가 끝나면 필드 로봇이 만재로 복귀한다.
        /// </summary>
        public bool Locked { get; set; }

        /// <summary>누적 태그 횟수(진단·연출용).</summary>
        public int TotalTags { get; private set; }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (_cooldown > 0f) _cooldown = Mathf.Max(0f, _cooldown - dt);
        }

        /// <summary>
        /// 자동 트리거 판정. **만충이 주, 소진이 보조**이므로 둘이 동시에 성립하면 만충이 이긴다 —
        /// 같은 순간에 약한 등장을 고를 이유가 없다.
        /// 쿨다운·잠금은 여기서 보지 않는다(판정과 발동을 분리해야 「조건은 됐는데 쿨다운이라 못 나감」이 표현된다).
        /// </summary>
        /// <param name="standbyMountFull">대기 로봇의 **마운트**가 만충인가(V03 §2 — 창고가 아니다).</param>
        /// <param name="activeDepleted">활성 로봇의 마운트가 소진됐는가.</param>
        public static TagEntry EvaluateAuto(bool standbyMountFull, bool activeDepleted)
        {
            if (standbyMountFull) return TagEntry.Full;
            if (activeDepleted) return TagEntry.Depleted;
            return TagEntry.None;
        }

        /// <summary>
        /// 태그 실행. 쿨다운 중·잠금 중·사유 없음이면 아무 일도 일어나지 않는다.
        /// 성공하면 쿨다운이 걸린다.
        /// </summary>
        public bool TryTag(TagEntry reason)
        {
            if (reason == TagEntry.None || !CanTag) return false;

            _cooldown = CooldownSeconds;
            TotalTags++;
            return true;
        }

        /// <summary>재시작 등에서 상태를 되돌린다.</summary>
        public void Reset()
        {
            _cooldown = 0f;
            Locked = false;
            TotalTags = 0;
        }

        // ---- 태그 스킬 (구 「등장 특공」/「과부하 특공」 — 260827_V03 §4 개명) ----

        /// <summary>
        /// 이 태그에서 **태그 스킬**이 발동하는가.
        ///
        /// 조건은 **마운트 만충**이다(V03 §2·§4). 구 「창고 100%」는 폐기됐다 —
        /// 2026-08-27 개정으로 만충 판정 주체가 저장 노드에서 마운트로 옮겨졌다.
        /// 효과는 **마운트 재고 전량을 소진하는 공격 1회**이고, 저장 노드는 남는다.
        ///
        /// 만재 등장에서만 나간다 — 소진 트리거는 활성 로봇이 마른 것이지
        /// 대기 로봇의 마운트가 찼다는 뜻이 아니다.
        ///
        /// ⚠️ **피해 계산식은 여기 없다.** 태그 전용이 아니라 합체 발동에서도 같은 식으로
        /// 나가므로 식은 GrandEntrance에 독립으로 두었다. 여기는 **태그 경로의 발동 조건**만 본다.
        /// </summary>
        public static bool HasTagSkill(TagEntry reason, bool mountFull) =>
            mountFull && (reason == TagEntry.Full || reason == TagEntry.Manual);
    }
}
