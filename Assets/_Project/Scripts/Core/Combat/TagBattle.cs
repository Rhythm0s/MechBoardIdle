using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// A↔B 교대 진행(전투 시스템 문서 2-1장). 순수 조정자 — 두 로봇의 마운트와 태그 상태만 들고
    /// 「지금 누가 나가 있는가」를 정한다. 사격·피격은 시뮬이 하고 여기는 교대만 본다.
    ///
    /// **태그와 태그 스킬은 조건이 다르다**(2026-08-27 정정):
    ///   - 태그(교대)   → **스택 수치와 무관하게 진행된다.** 소진 트리거는 「비었는가」만 보므로
    ///                     상한을 몰라도 판정된다. 만충 트리거는 스택이 들어오면 함께 켜진다.
    ///   - 태그 스킬     → **마운트 만충**이 조건이라 스택 수치가 필요하다.
    /// 둘을 한 조건으로 묶으면 스택이 없다는 이유로 교대까지 멈춘다 — 그러면 안 된다.
    ///
    /// 대기 로봇의 공장도 계속 돈다(전투 문서 1장). 그 산출이 대기 마운트에 쌓여
    /// 태그 인 순간 비축 화력이 되고, 그것이 저장 노드의 존재 이유다.
    /// </summary>
    public sealed class TagBattle
    {
        private readonly MountLoad[] _mounts;
        private readonly TagSystem _tag = new TagSystem();

        public TagBattle(MountLoad robotA, MountLoad robotB)
        {
            _mounts = new[] { robotA, robotB };
            ActiveIndex = 0;
        }

        /// <summary>지금 나가 있는 로봇(0 = A, 1 = B).</summary>
        public int ActiveIndex { get; private set; }
        public int StandbyIndex => 1 - ActiveIndex;

        public MountLoad ActiveMount => _mounts[ActiveIndex];
        public MountLoad StandbyMount => _mounts[StandbyIndex];

        public TagSystem Tag => _tag;

        /// <summary>합체 중 태그 잠금(전투 문서 4장 상위 잠금).</summary>
        public bool Locked
        {
            get => _tag.Locked;
            set => _tag.Locked = value;
        }

        /// <summary>마지막 태그에서 태그 스킬이 나갔는가(연출·피해 계산 트리거).</summary>
        /// <summary>
        /// 태그 스킬 타격. 시뮬이 꽂아 준다 — 인자는 마운트 적재량, 반환은 **실제로 때렸는가**.
        ///
        /// **재고를 비우는 것과 피해를 주는 것은 한 동작이다**(260831_V09 확정).
        /// 이 함수가 false를 주면 재고도 안 비운다. 표적이 없으면 발동을 보류하고
        /// 마운트는 만재를 유지하다가, 적이 나타나면 그때 터진다.
        ///
        /// ⚠️ 꽂아 주지 않으면 스킬은 **안 나간다.** 그 편이 맞다 — 때릴 대상을 아는 쪽이
        /// 없으면 표적 없이 발동한 것과 같고, 그것이 재고만 태우던 결함이었다.
        /// </summary>
        public System.Func<float, bool> SkillStrike;

        public bool LastTagFiredSkill { get; private set; }

        /// <summary>마지막 태그 스킬이 소진한 적재량. 피해 계산의 입력.</summary>
        public float LastTagSkillDrained { get; private set; }

        // ── 태그 스킬은 진입 클립이 다 돈 뒤에 터진다 (260908_W06 2장 · (가) 0.75초) ──────
        //
        // **갈라 둔 것 둘.** 태그 인 틱에 확정되는 것은 **발동 여부와 발수**이고,
        // **표적 판정 · 피해 · 마운트 비움**은 클립이 끝난 자리로 미룬다.
        //
        // 왜 발수를 먼저 잡는가 — 발동 조건은 「들어오는 로봇의 마운트가 만충」이고
        // 그 만충은 **태그 인 시점의 사실**이다(전투 시스템 문서「태그 시스템」).
        // 기다리는 동안 창고가 마운트를 더 채워도 그것으로 때리면 조건과 결과가 어긋난다.
        //
        // 왜 표적은 미루는가 — 연출이 닿는 데까지가 판정이 닿는 데까지여야 하고
        // (260908_W05 2-2), 연출은 클립이 끝난 뒤에 나간다. **그때의 화면으로 판정한다.**
        //
        // ⚠️ **보류 규칙은 그대로다** — 그때 화면 안에 표적이 하나도 없으면
        // 타격이 성립하지 않고 **마운트도 안 비워진다**(재고는 만재로 남는다).
        private float _pendingRounds;
        private int _pendingIndex;

        /// <summary>진입 클립이 끝나기를 기다리는 태그 스킬이 있는가.</summary>
        public bool HasPendingSkill => _pendingRounds > 0f;

        /// <summary>기다리던 발수 — 태그 인 틱에 잡힌 값이다(진단용).</summary>
        public float PendingSkillRounds => _pendingRounds;

        /// <summary>
        /// 기다리던 태그 스킬을 **지금** 터뜨린다 — 진입 클립이 끝난 자리에서 시뮬이 부른다.
        /// 시간을 재는 쪽은 시뮬이다(여기는 dt를 모른다).
        /// </summary>
        /// <returns>타격이 성립했으면 참. 표적이 없어 보류됐으면 거짓이며 마운트는 그대로다.</returns>
        public bool ResolvePendingSkill()
        {
            if (_pendingRounds <= 0f) return false;

            float loaded = _pendingRounds;
            _pendingRounds = 0f;

            if (SkillStrike == null || !SkillStrike(loaded)) return false;

            LastTagSkillDrained = _mounts[_pendingIndex].DrainAll();
            LastTagFiredSkill = true;
            return true;
        }

        /// <summary>기다리던 스킬을 버린다(전투 종료·리셋 등).</summary>
        public void CancelPendingSkill() => _pendingRounds = 0f;

        /// <summary>
        /// **실패 조건 — A·B 동시 고갈 → 공격 정지**(밸런스 「태그 시스템 수치」).
        /// 한쪽만 비면 태그로 넘어가면 되지만 둘 다 비면 갈 곳이 없다.
        /// </summary>
        public bool BothDepleted => _mounts[0].IsEmpty && _mounts[1].IsEmpty;

        public void Tick(float dt) => _tag.Tick(dt);

        /// <summary>
        /// 자동 태그 판정 → 발동까지. 발동했으면 true.
        ///
        /// 만충 트리거는 대기 로봇의 마운트를, 소진 트리거는 활성 로봇의 마운트를 본다 —
        /// 「대기가 다 찼으니 화려하게 등장」과 「활성이 말랐으니 어쩔 수 없이 교대」는 다른 사건이다.
        /// </summary>
        public bool TickAuto(float dt)
        {
            Tick(dt);

            // 소진 트리거는 **갈 곳이 있을 때만** 성립한다. 대기도 비었으면 교대해 봐야 똑같고,
            // 그 헛교대가 5초 쿨다운을 먹어 정작 대기가 찼을 때 못 나가게 만든다.
            // 둘 다 빈 것은 교대 사유가 아니라 **실패 조건**이다(BothDepleted).
            bool canFallBack = !StandbyMount.IsEmpty;

            TagEntry reason = TagSystem.EvaluateAuto(
                standbyMountFull: StandbyMount.IsFull,
                activeDepleted: ActiveMount.IsEmpty && canFallBack);

            return Execute(reason);
        }

        /// <summary>수동 태그(보스전 등 컨트롤 콘텐츠). 쿨다운·잠금은 그대로 적용된다.</summary>
        public bool TryManualTag() => Execute(TagEntry.Manual);

        private bool Execute(TagEntry reason)
        {
            // ⚠️ 실패 판정이 플래그를 지우지 않게 **성공한 뒤에** 초기화한다.
            // 「마지막 태그에서 스킬이 나갔는가」는 연출·피해 계산이 읽는 값인데,
            // 매 틱 리셋하면 교대 다음 프레임에 사라져 그 프레임을 놓친 쪽은 영영 못 읽는다.
            if (!_tag.TryTag(reason)) return false;

            LastTagFiredSkill = false;
            LastTagSkillDrained = 0f;

            // 태그 스킬 — **들어오는 쪽(대기 로봇)의 마운트**가 만충일 때 나간다.
            // 만재 등장이 보상형인 이유가 이것이고, 소진 트리거에는 붙지 않는다.
            bool skill = TagSystem.HasTagSkill(reason, StandbyMount.IsFull);

            ActiveIndex = StandbyIndex; // 교대

            if (skill)
            {
                // 마운트 재고 **전량**을 소진하는 공격 1회. 저장 노드는 남는다 —
                // 마운트가 빈 동안 화력이 죽고 벨트가 다시 채우며, 물류가 좋을수록 그 공백이 짧다.
                //
                // ⚠️ **비우기와 때리기는 한 동작이다.** 타격이 성립하지 않으면 비우지도 않는다 —
                // 종전에는 비우기만 있어 「대가만 치르고 아무 일도 안 일어나는」 순손실이었다.
                //
                // ⚠️ **여기서 때리지 않는다** (2026-09-08 · 260908_W06 2장). 발수만 잡아 두고
                // 진입 클립이 다 돈 뒤에 <see cref="ResolvePendingSkill"/>이 때린다.
                // 구 거동은 태그 인과 **동시(0초)**였다.
                float loaded = ActiveMount.Total;
                if (loaded > 0f)
                {
                    _pendingRounds = loaded;
                    _pendingIndex = ActiveIndex;
                }
            }
            return true;
        }

        /// <summary>태그 스킬 피해. 식은 GrandEntrance 하나를 쓴다(합체 경로와 공용).</summary>
        public float TagSkillDamage(float avgDamagePerShot) =>
            GrandEntrance.Damage(LastTagFiredSkill, LastTagSkillDrained, avgDamagePerShot);

        public void Reset()
        {
            ActiveIndex = 0;
            _tag.Reset();
            LastTagFiredSkill = false;
            LastTagSkillDrained = 0f;
            _pendingRounds = 0f;
        }
    }
}
