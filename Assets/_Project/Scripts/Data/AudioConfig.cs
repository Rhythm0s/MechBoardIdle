using UnityEngine;

namespace MBI.Data
{
    /// <summary>
    /// 소리 값 묶음(SO · 2026-09-09 신설). **소리에 관한 숫자는 전부 여기 산다**(§3).
    ///
    /// ⚠️ **여기 있는 값 넷은 아직 확정이 아니다.** 사운드 문서 9장이 그렇게 두었다 —
    /// S-1(겹침 상한)은 「구현 후 실제 화면에서 듣고 정한다」, S-2(볼륨 배치)는
    /// 「자산이 나온 뒤 같은 화면에서 대조」다. **잠정값을 넣어 두고 화면에서 고친다** —
    /// 값이 없으면 재생기가 아예 안 돌아 들어 볼 수조차 없다.
    ///
    /// **문서가 붙드는 것은 절대값이 아니라 관계다** — 경고 &gt; 효과음 &gt; 조작음.
    /// 그 관계는 코드가 시험으로 지키고(`AudioMixTests`), 점값은 귀가 정한다.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "MBI/Audio Config", order = 5)]
    public sealed class AudioConfig : ScriptableObject
    {
        [Header("겹침 (사운드 문서 9장 S-1 — 미확정)")]
        [Tooltip("같은 소리가 동시에 몇 개까지 나는가. ⚠️ 잠정값 — 적이 가장 많은 스테이지에서 듣고 정한다.")]
        [Min(1)] public int overlapLimitPerSound = 3;
        [Tooltip("효과음 재생 자리 수(소리 종류 무관 전체 상한). 넘치면 가장 오래된 것을 끊는다.")]
        [Min(1)] public int sfxVoiceCount = 12;
        [Tooltip("이 값들이 확정인가. Tbd = 화면에서 듣고 정할 자리(§7 「미검증을 통과로 적지 않는다」).")]
        public ConfirmState overlapConfirm = ConfirmState.Tbd;

        [Header("볼륨 — 상대 순서만 (사운드 문서 2장 · 9장 S-2 미확정)")]
        [Tooltip("경고음. **가장 커야 한다** — 화면을 안 봐도 닿아야 하는 소리다.")]
        [Range(0f, 1f)] public float warningVolume = 1.00f;
        [Tooltip("효과음. 게임이 내는 나머지.")]
        [Range(0f, 1f)] public float effectVolume = 0.75f;
        [Tooltip("조작음. **가장 작아야 한다** — 플레이어가 스스로 낸 소리다.")]
        [Range(0f, 1f)] public float uiVolume = 0.50f;
        [Tooltip("배경 음악. 앞에 나서지 않는다 — 효과음이 상태를 알리는 통로라 덮으면 안 된다(6장).")]
        [Range(0f, 1f)] public float musicVolume = 0.30f;
        [Tooltip("볼륨 배치가 확정인가. Tbd = 자산이 나온 뒤 같은 화면에서 대조할 자리.")]
        public ConfirmState volumeConfirm = ConfirmState.Tbd;

        [Header("배경 음악")]
        [Tooltip("국면이 바뀔 때 곡을 겹쳐 넘기는 초. ⚠️ 미확정 — 화면에서 듣고 정한다. 0이면 즉시 갈아탄다.")]
        [Min(0f)] public float musicCrossfadeSeconds = 2.25f;
        [Tooltip("크로스페이드 초가 확정인가.")]
        public ConfirmState crossfadeConfirm = ConfirmState.Tbd;

        [Tooltip("곡이 다시 시작하는 자리에서 여리게 지나가는 초 (2026-09-09 사용자 확정 — 페이드 아웃 → 페이드 인). 0이면 페이드 없이 파형 그대로 이어진다.")]
        [Min(0f)] public float musicLoopFadeSeconds = 2f;
        [Tooltip("루프 페이드 초가 확정인가. 방식은 사용자가 정했고 점값은 아직이다.")]
        public ConfirmState loopFadeConfirm = ConfirmState.Tbd;

        [Header("자산 (경로는 생성기에만 · §8)")]
        [Tooltip("전투 국면 배경 음악. 비면 아무 곡도 안 튼다 — 자리표시 소리를 만들지 않는다.")]
        public AudioClip musicBattle;
        [Tooltip("보스전 국면 배경 음악. 비면 전투 곡을 이어 쓴다.")]
        public AudioClip musicBoss;

        [Tooltip("효과음 여덟. 이름 순서는 SoundIds.All과 같다. 없는 것은 비워 둔다 — 조용히 건너뛴다.")]
        public AudioClip[] sfxClips = new AudioClip[0];
    }
}
