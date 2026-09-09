using System.Collections.Generic;
using MBI.Core.Audio;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 소리 규칙 셋 — 겹침 · 국면 · 무음 (2026-09-09 · 사운드 문서 2·6·7장).
    ///
    /// **재생기가 아니라 규칙을 시험한다.** `AudioSource`는 배치모드가 못 보지만
    /// 「몇 개가 겹치는가 · 어느 곡인가 · 소리가 없어도 도는가」는 화면 없이 볼 수 있다 —
    /// 그것을 재생기 안에 두면 **테스트가 못 보는 자리**가 된다(지침 §7).
    /// </summary>
    public sealed class AudioRuleTests
    {
        // ── 겹침 (사운드 문서 2장 · 7장 · 9장 S-1) ────────────────────────────────

        [Test]
        public void SameSound_StopsAtTheLimit_AndTakesTheOldest()
        {
            var overlap = new SoundOverlap(limitPerSound: 2, slotCount: 8);

            SoundOverlap.Decision a = overlap.Take("sfx_hit", 0f);
            SoundOverlap.Decision b = overlap.Take("sfx_hit", 1f);
            Assert.IsTrue(a.play && b.play);
            Assert.IsFalse(a.evicted || b.evicted, "상한 안에서는 아무것도 안 끊는다");
            Assert.AreEqual(2, overlap.CountOf("sfx_hit"));

            SoundOverlap.Decision c = overlap.Take("sfx_hit", 2f);
            Assert.IsTrue(c.play, "넘쳐도 새 소리는 난다 — 지금 난 일을 알리는 것이 소리의 일이다");
            Assert.IsTrue(c.evicted, "가장 오래된 것을 끊는다(7장)");
            Assert.AreEqual(a.slot, c.slot, "끊은 그 자리를 새 소리가 쓴다");
            Assert.AreEqual(2, overlap.CountOf("sfx_hit"), "상한을 넘어서 늘지 않는다");
        }

        [Test]
        public void TheLimitIsPerSound_NotAcrossSounds()
        {
            // 상한이 「이 소리의 상한」이라야 발사음이 찼다고 피격음이 막히지 않는다.
            var overlap = new SoundOverlap(limitPerSound: 1, slotCount: 8);

            Assert.IsTrue(overlap.Take("sfx_fire_a", 0f).play);
            SoundOverlap.Decision hit = overlap.Take("sfx_hit", 1f);

            Assert.IsTrue(hit.play);
            Assert.IsFalse(hit.evicted, "다른 소리가 찼다고 끊을 이유가 없다");
            Assert.AreEqual(1, overlap.CountOf("sfx_fire_a"));
            Assert.AreEqual(1, overlap.CountOf("sfx_hit"));
        }

        [Test]
        public void WhenSlotsRunOut_TheOldestAnywhereIsTaken()
        {
            var overlap = new SoundOverlap(limitPerSound: 9, slotCount: 2);

            overlap.Take("sfx_fire_a", 0f);
            overlap.Take("sfx_hit", 1f);
            SoundOverlap.Decision third = overlap.Take("sfx_burst", 2f);

            Assert.IsTrue(third.play && third.evicted);
            Assert.AreEqual(2, overlap.ActiveCount, "자리 수를 넘겨 늘지 않는다");
            Assert.AreEqual(0, overlap.CountOf("sfx_fire_a"), "가장 오래된 것이 끊겼다");
        }

        [Test]
        public void ZeroLimit_MeansSilence_NotUnlimited()
        {
            // ⚠️ 0을 무제한으로 읽으면 **값을 안 채운 SO가 상한 없는 재생기**가 된다.
            var overlap = new SoundOverlap(limitPerSound: 0, slotCount: 8);
            Assert.IsFalse(overlap.Take("sfx_hit", 0f).play);
            Assert.AreEqual(0, overlap.ActiveCount);
        }

        [Test]
        public void RetiredSounds_FreeTheirSlot()
        {
            var overlap = new SoundOverlap(limitPerSound: 1, slotCount: 4);
            overlap.Take("sfx_hit", 0f);
            Assert.AreEqual(1, overlap.CountOf("sfx_hit"));

            overlap.Retire("sfx_hit", 0f);

            Assert.AreEqual(0, overlap.CountOf("sfx_hit"), "끝난 소리를 안 빼면 상한이 영영 차 있다");
            Assert.IsFalse(overlap.Take("sfx_hit", 1f).evicted, "빈 자리가 다시 열린다");
        }

        // ── 국면 (사운드 문서 6장) ────────────────────────────────────────────────

        [Test]
        public void BossMusic_IsS6Only_AndReusesTheExistingJudgment()
        {
            Assert.AreEqual(MusicPhase.Boss, MusicPhaseRule.Of(StageReqType.Budget),
                "「예산식(보스 HP) — S6」이 보스 판정이다");
            Assert.AreEqual(MusicPhase.Battle, MusicPhaseRule.Of(StageReqType.Fixed));
            Assert.AreEqual(MusicPhase.Battle, MusicPhaseRule.Of(StageReqType.Band));
            Assert.AreEqual(MusicPhase.Battle, MusicPhaseRule.Of(StageReqType.Formula),
                "S5는 태그 필연이지 보스전이 아니다");
        }

        [Test]
        public void MusicSwaps_OnlyWhenThePhaseChanges()
        {
            // 화면 전환은 여기 안 들어온다 — 조립 화면으로 들어가도 국면은 그대로다(6장).
            Assert.IsFalse(MusicPhaseRule.NeedsSwap(MusicPhase.Battle, MusicPhase.Battle),
                "같은 국면에서 곡을 갈면 화면을 오갈 때마다 음악이 끊긴다");
            Assert.IsTrue(MusicPhaseRule.NeedsSwap(MusicPhase.Battle, MusicPhase.Boss));
            Assert.IsTrue(MusicPhaseRule.NeedsSwap(MusicPhase.Boss, MusicPhase.Battle));
        }

        // ── 무음 (사운드 문서 7장 「모든 소리가 빠져도 게임이 정상 동작해야 한다」) ──

        [Test]
        public void CuesSurvive_WithNoPlayerListening()
        {
            AudioSignals.Reset();

            AudioSignals.Play(SoundIds.Hit, SoundKind.Effect);
            AudioSignals.Play(SoundIds.FireA, SoundKind.Effect);
            Assert.AreEqual(2, AudioSignals.PendingCount);

            var drained = new List<AudioSignals.Cue>();
            AudioSignals.Drain(drained);

            Assert.AreEqual(2, drained.Count);
            Assert.AreEqual(0, AudioSignals.PendingCount, "비운 뒤에는 남지 않는다");
        }

        [Test]
        public void PendingCues_DoNotGrowForever()
        {
            // ⚠️ 재생기가 없는 씬에서도 넣는 쪽은 돈다 — 상한이 없으면 목록이 영영 자란다.
            AudioSignals.Reset();
            for (int i = 0; i < AudioSignals.MaxPending * 3; i++)
                AudioSignals.Play(SoundIds.FireA, SoundKind.Effect);

            Assert.AreEqual(AudioSignals.MaxPending, AudioSignals.PendingCount);
            AudioSignals.Reset();
        }

        [Test]
        public void EmptyIdIsIgnored()
        {
            AudioSignals.Reset();
            AudioSignals.Play(null, SoundKind.Effect);
            AudioSignals.Play(string.Empty, SoundKind.Effect);
            Assert.AreEqual(0, AudioSignals.PendingCount);
        }

        [Test]
        public void NoConfig_MeansNoSound_NotADefaultGuess()
        {
            // 설정이 없으면 볼륨은 0이다 — 모르는 값을 지어내지 않는다.
            Assert.AreEqual(0f, AudioMix.GainOf(null, SoundKind.Warning), 1e-6f);
            Assert.AreEqual(0f, AudioMix.MusicGain(null), 1e-6f);
            Assert.IsFalse(AudioMix.OrderHolds(null));
        }

        // ── 볼륨 상대 순서 (사운드 문서 2장) ──────────────────────────────────────

        [Test]
        public void VolumeOrder_HoldsInTheShippedConfig()
        {
            // **문서가 붙드는 것은 점값이 아니라 이 관계다** — 경고 > 효과음 > 조작음.
            // 병목 경고가 딸깍보다 작으면 화면을 안 보는 플레이어에게 안 닿고,
            // 그러면 사운드 문서 1장의 규정 자체가 무너진다.
            var config = ScriptableObject.CreateInstance<AudioConfig>();

            Assert.IsTrue(AudioMix.OrderHolds(config),
                "기본값이 이미 순서를 지켜야 한다 — 값을 고치다 뒤집으면 여기가 먼저 빨개진다");
            Assert.IsTrue(AudioMix.MusicStaysBehind(config),
                "음악이 효과음을 덮으면 상태를 알리는 통로가 막힌다(6장 「성격」)");
        }

        [Test]
        public void VolumeOrder_BreaksWhenWarningIsQuieter()
        {
            var config = ScriptableObject.CreateInstance<AudioConfig>();
            config.warningVolume = 0.1f;

            Assert.IsFalse(AudioMix.OrderHolds(config), "뒤집힌 것을 통과로 적지 않는다");
        }

        // ── 소리 이름 (사운드 문서 10장) ──────────────────────────────────────────

        [Test]
        public void EightSounds_AndNoInventedNames()
        {
            Assert.AreEqual(8, SoundIds.All.Length,
                "영상에 나오는 일곱 + 조건부 하나 — sfx_tagskill과 선택 등급 다섯은 범위 밖이다");
            CollectionAssert.AllItemsAreUnique(SoundIds.All);
            foreach (string id in SoundIds.All)
                Assert.IsTrue(id.StartsWith("sfx_"), $"{id} — 자산 파일명 그대로 쓴다(지침 §8)");
        }

        [Test]
        public void BottleneckIsAWarning_AndBoardTapsAreQuietest()
        {
            Assert.AreEqual(SoundKind.Warning, SoundIds.KindOf(SoundIds.Bottleneck));
            Assert.AreEqual(SoundKind.Ui, SoundIds.KindOf(SoundIds.NodeSnap));
            Assert.AreEqual(SoundKind.Ui, SoundIds.KindOf(SoundIds.BeltConnect));
            Assert.AreEqual(SoundKind.Effect, SoundIds.KindOf(SoundIds.FireA));
        }
    }
}
