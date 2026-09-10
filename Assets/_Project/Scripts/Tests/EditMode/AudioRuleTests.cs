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

        // ── 루프 이음새 (2026-09-09 사용자 확정 · 사운드 문서 6장) ────────────────

        [Test]
        public void LoopFade_RisesAtTheStartAndFallsAtTheEnd()
        {
            const float length = 176f, fade = 2f;

            Assert.AreEqual(0f, MusicLoop.Envelope(0f, length, fade), 1e-4f, "처음은 0에서 올라온다");
            Assert.AreEqual(0.5f, MusicLoop.Envelope(1f, length, fade), 1e-4f);
            Assert.AreEqual(1f, MusicLoop.Envelope(fade, length, fade), 1e-4f);
            Assert.AreEqual(1f, MusicLoop.Envelope(length * 0.5f, length, fade), 1e-4f,
                "가운데는 그대로 — 곡 전체를 여리게 만드는 것이 아니다");
            Assert.AreEqual(0.5f, MusicLoop.Envelope(length - 1f, length, fade), 1e-4f);
            Assert.AreEqual(0f, MusicLoop.Envelope(length, length, fade), 1e-4f, "끝은 0으로 내려간다");
        }

        [Test]
        public void LoopFade_OffMeansNoChange()
        {
            // 0이면 파형 그대로 이어진다 — 페이드를 끄는 자리다.
            Assert.AreEqual(1f, MusicLoop.Envelope(0f, 176f, 0f), 1e-4f);
            Assert.AreEqual(1f, MusicLoop.Envelope(176f, 176f, 0f), 1e-4f);
        }

        [Test]
        public void LoopFade_LongerThanHalfTheClip_DoesNotDipTheMiddle()
        {
            // ⚠️ 안 줄이면 들어가는 페이드와 나가는 페이드가 겹쳐 **한가운데가 가장 작아진다.**
            // 짧은 자산을 넣었을 때 조용히 그렇게 되는 것을 막는다.
            const float length = 4f;
            float mid = MusicLoop.Envelope(length * 0.5f, length, 10f);

            Assert.AreEqual(1f, mid, 1e-4f, "가운데는 늘 최대여야 한다");
        }

        [Test]
        public void LoopRestarts_OnlyWhenTheClipHasStopped()
        {
            // `AudioSource.loop`를 안 쓰는 이유가 여기 있다 — 끝나는 것을 봐야 페이드가 걸린다.
            Assert.IsTrue(MusicLoop.ShouldRestart(isPlaying: false, hasClip: true));
            Assert.IsFalse(MusicLoop.ShouldRestart(isPlaying: true, hasClip: true), "돌고 있으면 안 건드린다");
            Assert.IsFalse(MusicLoop.ShouldRestart(isPlaying: false, hasClip: false), "곡이 없으면 틀 것이 없다");
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

        // ---- 배경 음악 볼륨 — 사람이 고른다 (2026-09-10 사용자 확정) ----

        /// <summary>
        /// **기본은 30%다.** SO 기본값과 코어 상수가 **같은 값**이어야 한다 —
        /// 갈리면 씬에서 켤 때와 코드가 기대하는 것이 달라진다.
        /// </summary>
        [Test]
        public void MusicVolume_DefaultsTo30Percent_AndMatchesTheAsset()
        {
            Assert.AreEqual(0.30f, MusicVolume.Default, 1e-6f, "코어 기본값");

            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioConfig>(
                "Assets/_Project/ScriptableObjects/AudioConfig.asset");
            Assert.NotNull(config, "AudioConfig 자산");
            Assert.AreEqual(MusicVolume.Default, config.musicVolume, 1e-6f,
                "SO 기본값이 코어 상수와 같다");
        }

        /// <summary>
        /// **시험판은 꺼 두고 연다**(사용자 확정 2026-09-10). 배포판 기본 30% 는 그대로다 —
        /// 두 값은 서로 다른 것을 가리키므로 <c>Default</c> 를 0 으로 내리지 않는다.
        ///
        /// ⚠️ 시험이 도는 자리(에디터)는 <c>Debug.isDebugBuild</c> 가 참이라
        /// **여기서 기대할 수 있는 것은 0 이다.** 배포판 쪽은 상수로 붙든다.
        /// </summary>
        [Test]
        public void MusicVolume_StartsSilentInTestBuilds_ButKeepsTheReleaseDefault()
        {
            Assert.AreEqual(0.30f, MusicVolume.Default, 1e-6f, "배포판 기본값은 안 바뀐다");

            Assert.IsTrue(UnityEngine.Debug.isDebugBuild, "시험은 개발 쪽에서 돈다");
            Assert.AreEqual(0f, MusicVolume.StartupDefault, 1e-6f, "시험판은 꺼진 채로 연다");

            // 끄는 것이지 없애는 것이 아니다 — 올리면 그대로 들어간다.
            MusicVolume.Set(0.5f);
            Assert.AreEqual(0.5f, MusicVolume.Value, 1e-6f);
            MusicVolume.Reset();
        }

        /// <summary>
        /// **소리 버튼이 변수 패널과 겹치지 않는다** (2026-09-10 · 실제로 겹쳐 있었다).
        ///
        /// 겹치면 뒤에 그리는 쪽이 클릭을 먹어 **버튼이 그려지기만 하고 안 눌린다.**
        /// 화면 없이 잴 수 있는 것은 **자리**뿐이라 자리만 잰다 — 눌리는지는 사람이 본다.
        /// </summary>
        [Test]
        public void SoundButton_DoesNotOverlapTheVariablePanel()
        {
            const float margin = 12f;
            const float w = 1440f;

            // 변수 패널 — VariablePanel.OnGUI 와 같은 셈.
            var variablePanel = new Rect(w - 300f - margin, margin, 300f, 250f);
            // 소리 버튼 — AudioOptionsPanel.OnGUI 와 같은 셈.
            var soundButton = new Rect(w - 96f - margin, 250f + margin * 2f, 96f, 32f);

            Assert.IsFalse(variablePanel.Overlaps(soundButton),
                $"겹친다 — 변수 {variablePanel} · 소리 {soundButton}");
            Assert.Greater(soundButton.y, variablePanel.yMax, "소리 버튼은 변수 패널 아래다");
        }

        /// <summary>사람이 올려도 1을 안 넘고 내려도 0 아래로 안 간다.</summary>
        [Test]
        public void MusicVolume_IsClampedToZeroOne()
        {
            MusicVolume.Set(5f);
            Assert.AreEqual(1f, MusicVolume.Value, 1e-6f);

            MusicVolume.Set(-2f);
            Assert.AreEqual(0f, MusicVolume.Value, 1e-6f);

            MusicVolume.Reset();
            Assert.AreEqual(MusicVolume.Default, MusicVolume.Value, 1e-6f);
        }

        /// <summary>화면에 적는 말은 **퍼센트 정수**다 — 0.3을 「0.3」으로 적으면 무엇의 0.3인지 모른다.</summary>
        [Test]
        public void MusicVolume_LabelIsAWholePercent()
        {
            Assert.AreEqual("30%", MusicVolume.Label(0.30f));
            Assert.AreEqual("0%", MusicVolume.Label(0f));
            Assert.AreEqual("100%", MusicVolume.Label(1f));
        }

        /// <summary>
        /// **크로스페이드가 50% 넓어졌다** (2026-09-10 사용자 확정 · 1.5 → 2.25).
        /// ⚠️ **루프 이음새(2초)는 안 건드렸다** — 「브금 전환 시」가 가리킨 것은 국면 전환이다.
        /// </summary>
        [Test]
        public void Crossfade_IsFiftyPercentWider_ButLoopFadeIsUntouched()
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioConfig>(
                "Assets/_Project/ScriptableObjects/AudioConfig.asset");
            Assert.NotNull(config);

            Assert.AreEqual(2.25f, config.musicCrossfadeSeconds, 1e-6f, "1.5 × 1.5");
            Assert.AreEqual(2f, config.musicLoopFadeSeconds, 1e-6f, "루프 이음새는 그대로");
        }

    }
}
