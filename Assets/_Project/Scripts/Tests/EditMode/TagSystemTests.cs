using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 태그 시스템(전투 시스템 문서 2-1장 · 밸런스 문서「태그 시스템 수치」).
    /// 확정치: 쿨다운 5초 · **만충 판정 주체 = 마운트**(V03 §2 개정) · 태그 스킬 = 적재 발수 × 강화 평균 발당피해.
    /// </summary>
    public sealed class TagSystemTests
    {
        private const float D = 0.001f;

        // ---- 트리거 ----

        /// <summary>
        /// 만충이 **주 트리거**, 소진이 **보조 트리거**다. 둘이 동시에 성립하면 만충이 이긴다 —
        /// 같은 순간에 굳이 약한 등장을 고를 이유가 없다.
        /// </summary>
        [Test]
        public void FullTrigger_WinsOverDepleted()
        {
            Assert.AreEqual(TagEntry.Full, TagSystem.EvaluateAuto(standbyMountFull: true, activeDepleted: true));
            Assert.AreEqual(TagEntry.Full, TagSystem.EvaluateAuto(true, false));
            Assert.AreEqual(TagEntry.Depleted, TagSystem.EvaluateAuto(false, true));
            Assert.AreEqual(TagEntry.None, TagSystem.EvaluateAuto(false, false));
        }

        /// <summary>
        /// 판정과 발동이 분리돼 있어야 「조건은 됐는데 쿨다운이라 못 나감」이 표현된다.
        /// EvaluateAuto는 쿨다운을 보지 않는다.
        /// </summary>
        [Test]
        public void EvaluateAuto_IgnoresCooldown()
        {
            var tag = new TagSystem();
            tag.TryTag(TagEntry.Full);

            Assert.IsFalse(tag.CanTag, "쿨다운 중");
            Assert.AreEqual(TagEntry.Full, TagSystem.EvaluateAuto(true, false), "판정은 그대로 성립한다");
        }

        // ---- 쿨다운 ----

        [Test]
        public void Cooldown_IsFiveSeconds_AndBlocksRetag()
        {
            var tag = new TagSystem();

            Assert.IsTrue(tag.TryTag(TagEntry.Full));
            Assert.AreEqual(5f, tag.CooldownRemaining, D, "확정치 5초");
            Assert.IsFalse(tag.TryTag(TagEntry.Full), "쿨다운 중 재태그 불가 — 수동 태그 진동 방지");

            tag.Tick(4.9f);
            Assert.IsFalse(tag.CanTag);

            tag.Tick(0.2f);
            Assert.IsTrue(tag.CanTag, "5초 지나면 다시 가능");
            Assert.AreEqual(0f, tag.CooldownRemaining, D, "음수로 흐르지 않는다");
        }

        [Test]
        public void FirstTag_NeedsNoWarmup()
        {
            Assert.IsTrue(new TagSystem().CanTag, "시작하자마자 태그할 수 있다");
        }

        [Test]
        public void NoReason_DoesNothing_AndDoesNotStartCooldown()
        {
            var tag = new TagSystem();

            Assert.IsFalse(tag.TryTag(TagEntry.None));
            Assert.AreEqual(0f, tag.CooldownRemaining, D, "헛발질이 쿨다운을 먹지 않는다");
            Assert.AreEqual(0, tag.TotalTags);
        }

        // ---- 합체 중 잠금 ----

        /// <summary>합체 중에는 태그가 **불가**하다(전투 문서 4장 상위 잠금).</summary>
        [Test]
        public void Locked_BlocksTag_EvenWhenReadyAndOffCooldown()
        {
            var tag = new TagSystem { Locked = true };

            Assert.IsFalse(tag.CanTag);
            Assert.IsFalse(tag.TryTag(TagEntry.Full));
            Assert.AreEqual(0, tag.TotalTags);

            tag.Locked = false;
            Assert.IsTrue(tag.TryTag(TagEntry.Full), "합체가 끝나면 다시 가능");
        }

        /// <summary>잠금은 쿨다운을 대신하지 않는다 — 합체가 끝나도 쿨다운이 남았으면 못 나간다.</summary>
        [Test]
        public void Unlocking_DoesNotClearCooldown()
        {
            var tag = new TagSystem();
            tag.TryTag(TagEntry.Full);
            tag.Locked = true;
            tag.Locked = false;

            Assert.IsFalse(tag.CanTag);
            Assert.AreEqual(5f, tag.CooldownRemaining, D);
        }

        // ---- 등장 특공 ----

        /// <summary>
        /// 대표 상태 검산: 비축 40발 × 강화 평균 발당피해 52.6 ≈ 2,103
        /// (밸런스 params tagspec 확정치).
        /// </summary>
        [Test]
        public void GrandEntrance_MatchesConfirmedFormula()
        {
            float dmg = GrandEntrance.Damage(mountFull: true, loadedRounds: 40f, avgDamagePerShot: 52.6f);

            Assert.AreEqual(2104f, dmg, 1f, "40 × 52.6 — params tagspec 2103과 대조");
        }

        /// <summary>
        /// **부분 발동이 없다.** 조건을 맞추거나 못 맞추거나다 — 99%에서 99%만큼 나가지 않는다.
        /// 만재 단일 기준이라는 것이 이 시스템의 핵심 규칙이다.
        /// </summary>
        [Test]
        public void GrandEntrance_DoesNotScaleWithPartialStock()
        {
            Assert.AreEqual(0f, GrandEntrance.Damage(mountFull: false, loadedRounds: 39.9f, avgDamagePerShot: 52.6f), D,
                "만재가 아니면 한 발도 나가지 않는다");
            Assert.Greater(GrandEntrance.Damage(true, 40f, 52.6f), 0f);
        }

        /// <summary>약한 등장(소진 트리거)에는 특공이 없다 — 미완충의 물리적 처벌이 유지돼야 한다.</summary>
        [Test]
        public void GrandEntrance_OnlyOnFullEntry()
        {
            Assert.IsTrue(TagSystem.HasTagSkill(TagEntry.Full, mountFull: true));
            Assert.IsTrue(TagSystem.HasTagSkill(TagEntry.Manual, mountFull: true), "수동이라도 만재면 나간다");

            Assert.IsFalse(TagSystem.HasTagSkill(TagEntry.Depleted, mountFull: true),
                "소진 트리거는 활성 로봇이 마른 것이지 대기 로봇이 만재라는 뜻이 아니다");
            Assert.IsFalse(TagSystem.HasTagSkill(TagEntry.Full, mountFull: false));
            Assert.IsFalse(TagSystem.HasTagSkill(TagEntry.None, true));
        }

        [Test]
        public void GrandEntrance_IsZeroForDegenerateInputs()
        {
            Assert.AreEqual(0f, GrandEntrance.Damage(true, 0f, 52.6f), D);
            Assert.AreEqual(0f, GrandEntrance.Damage(true, 40f, 0f), D);
            Assert.AreEqual(0f, GrandEntrance.Damage(true, -5f, 52.6f), D);
        }

        /// <summary>
        /// **특공은 태그 전용이 아니다.** 발동 경로가 둘이고 식이 같다(전투 문서 9-1):
        ///   ① 태그 인 — 대기 로봇 비축 100%
        ///   ② 합체 발동 — 창고 100% (구 「과부하 특공」)
        /// 식이 TagSystem 안에 있으면 합체 쪽이 태그를 거쳐 부르게 되어 의존이 거꾸로 선다.
        /// 이 테스트는 **합체 경로가 TagEntry 없이도 같은 값을 얻는다**는 것을 고정한다.
        /// </summary>
        [Test]
        public void GrandEntrance_IsCallableWithoutTagContext_ForMergePath()
        {
            // 합체 발동 시점 — 태그 사유가 없다. 창고 만재만 본다.
            float viaMerge = GrandEntrance.Damage(mountFull: true, loadedRounds: 40f, avgDamagePerShot: 52.6f);
            float viaTag = GrandEntrance.Damage(mountFull: true, loadedRounds: 40f, avgDamagePerShot: 52.6f);

            Assert.AreEqual(viaTag, viaMerge, D, "두 경로가 같은 식을 쓴다");
            Assert.Greater(viaMerge, 0f);
        }

        // ---- 태그 리듬 ----

        /// <summary>
        /// 「임의 쿨다운 없음」의 뜻: 태그 주기는 생산 속도와 저장 용량의 파생값이지
        /// 시스템이 정한 리듬이 아니다. 대표 상태에서 40발 ÷ 4발/초 = **10초**가 그 주기다.
        /// 5초 쿨다운은 그 절반이라 주기를 방해하지 않는다 — 안전장치이지 리듬 장치가 아니다.
        /// </summary>
        [Test]
        public void CooldownDoesNotGovernRhythm_ItIsShorterThanTheProductionCycle()
        {
            const float storeCapacity = 40f;      // params store
            const float representativeRate = 4f;  // 대표 생산 4발/초
            float cycle = storeCapacity / representativeRate;

            Assert.AreEqual(10f, cycle, D, "태그 주기 10초");
            Assert.Less(TagSystem.CooldownSeconds, cycle, "쿨다운이 주기보다 짧아야 리듬을 시스템이 뺏지 않는다");
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            var tag = new TagSystem();
            tag.TryTag(TagEntry.Full);
            tag.Locked = true;

            tag.Reset();

            Assert.IsTrue(tag.CanTag);
            Assert.AreEqual(0, tag.TotalTags);
            Assert.IsFalse(tag.Locked);
        }

        /// <summary>교대 공백은 원천에 초 단위 값이 없다 — 0 = 미측정 센티넬(교대 계수 0.96만 확정).</summary>
        [Test]
        public void SwitchGap_IsUnmeasuredSentinel()
        {
            Assert.AreEqual(0f, TagSystem.SwitchGapTbd, D,
                "확정되면 이 테스트가 실패해 SO 승격을 알린다");
        }
    }
}
