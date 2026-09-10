using System.Collections.Generic;
using MBI.Core;
using MBI.Core.Audio;
using MBI.Data;
using UnityEngine;

namespace MBI.UI
{
    /// <summary>
    /// 소리 전체를 맡는 하나 (사운드 문서 7장 · 2026-09-09 신설).
    ///
    /// **채널 셋을 만들고**(배경 음악 · 효과음 · 조작음), 배경 음악의 **국면**을 지키고,
    /// <see cref="AudioSignals"/>에 쌓인 사건을 프레임마다 비워 <see cref="SfxPlayer"/>에 넘긴다.
    ///
    /// **왜 여기 있는가.** 전투와 물류는 서로를 참조하지 않는데 둘 다 소리를 낸다.
    /// `MBI.UI`는 셋이 함께 참조하는 유일한 자리이며, 사건은 코어의 중립 채널로 들어온다 —
    /// 넣는 쪽은 `AudioSource`를 안 본다.
    ///
    /// ⚠️ **화면이 바뀌어도 음악을 안 끊는다**(6장). 조립 화면으로 들어가는 것은
    /// **화면 전환**이고 곡이 갈리는 것은 **국면 전환**이라, 이 컴포넌트는 화면을 아예 안 본다.
    /// UI 문서 1장의 연속성 원칙이 「음악이 끊기면 두 화면이 서로 다른 장소처럼 느껴진다」로
    /// 그 근거를 든다.
    ///
    /// ⚠️ **씬을 넘어도 산다.** 스테이지가 바뀔 때마다 새로 만들면 그 순간 음악이 끊긴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioDirector : MonoBehaviour
    {
        [Tooltip("소리 값 묶음. 씬 생성기가 주입한다. 비면 아무 소리도 안 난다.")]
        [SerializeField] private AudioConfig config;

        [Tooltip("효과음 재생기. 없으면 효과음 사건을 비우기만 하고 버린다.")]
        [SerializeField] private SfxPlayer sfx;

        /// <summary>지금 도는 곡. 국면이 바뀔 때만 갈린다.</summary>
        public MusicPhase Phase { get; private set; } = MusicPhase.Battle;

        /// <summary>이 판에 한 번이라도 곡을 튼 적이 있는가. 첫 재생과 국면 전환을 가른다.</summary>
        private bool _musicStarted;

        /// <summary>
        /// 지금 **곡이 있어야 하는가**. `SwapMusic(null)`로 껐을 때 거짓이 된다.
        /// ⚠️ 이것이 없으면 「꺼 둔 곡」과 「끝난 곡」이 구분되지 않아 껐는데 다시 돈다.
        /// </summary>
        private bool _musicWanted;

        /// <summary>
        /// 곡을 튼 뒤 흐른 시간(초). **`AudioSource.time`을 안 쓴다.**
        ///
        /// ⚠️ 2026-09-10 리허설에서 **BGM이 아예 안 났다.** 봉투가 `src.time`으로 페이드를
        /// 만드는데 그 값이 안 오르면 `t / fade`가 **0에 박혀 볼륨이 영영 0**이 된다.
        /// 스트리밍 클립은 플랫폼에 따라 `time`이 다르게 도므로 **우리가 직접 센다** —
        /// 이 값은 어디서나 같은 속도로 오른다.
        /// </summary>
        private float _headA, _headB;

        // 크로스페이드는 소스 둘을 겹쳐 쓴다 — 하나는 줄고 하나는 는다.
        private AudioSource _musicA;
        private AudioSource _musicB;
        private bool _useA = true;
        private float _fadeLeft;

        private readonly List<AudioSignals.Cue> _drain = new List<AudioSignals.Cue>();

        private static AudioDirector _instance;

        private void Awake()
        {
            // 하나만 산다. 씬을 다시 열어 둘이 되면 곡이 겹쳐 두 배로 들린다.
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (sfx == null) sfx = GetComponentInChildren<SfxPlayer>();
            BuildMusicChannel();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void BuildMusicChannel()
        {
            _musicA = MakeMusicSource("music_a");
            _musicB = MakeMusicSource("music_b");
        }

        private AudioSource MakeMusicSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            AudioSource src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            // ⚠️ **`loop`를 안 쓴다** (2026-09-09 사용자 확정). 그쪽은 파형을 바로 물려 버려
            // **페이드가 걸릴 자리가 없다** — 끝나는 것을 보고 다시 트는 쪽이라야
            // 양 끝에 봉투가 걸린다(`MusicLoop`). 반복 자체는 그대로다(6장).
            src.loop = false;
            src.spatialBlend = 0f;
            src.volume = 0f;
            return src;
        }

        /// <summary>
        /// 국면을 따라간다. **러너가 신호에 써 두고 여기서 읽는다** —
        /// 판정은 코어(`MusicPhaseRule`)가 하고 여기서는 곡을 갈지 말지만 정한다.
        ///
        /// **국면이 그대로면 아무 일도 안 한다.** 그것이 「화면 전환에 안 끊긴다」의 실체다 —
        /// 조립 화면으로 들어가도 이 함수가 부를 것을 못 찾는다.
        /// </summary>
        private void FollowPhase()
        {
            MusicPhase wanted = AudioSignals.Phase;
            if (_musicStarted && !MusicPhaseRule.NeedsSwap(Phase, wanted)) return;

            // ⚠️ **부팅하자마자 걸지 않는다**(2026-09-10 · 플랜 §67-3 결함 ③).
            // 웹은 **사람이 무언가를 누르기 전까지 오디오가 잠겨** 있어서, 그때 건 재생은
            // 소리 없이 흘러가고 **나중에 볼륨을 올려도 살아나지 않는다.**
            // 「게임 시작」이나 볼륨 슬라이더를 누른 뒤에 건다.
            if (!MusicStartRule.ShouldStart(MainMenuGate.IsOpen, MusicVolume.PreviewRequested)) return;

            // 미리듣기 표시는 **한 번 쓰고 내린다** — 계속 서 있으면 메뉴에서 곡이 되살아난다.
            MusicVolume.PreviewRequested = false;

            Phase = wanted;
            SwapMusic(ClipFor(wanted));
        }

        /// <summary>
        /// 이 국면의 곡. **보스 곡이 없으면 전투 곡을 이어 쓴다** —
        /// 곡이 통째로 사라지는 것보다 낫고, 배경 그림에서 내린 판단과 같은 결이다.
        /// </summary>
        private AudioClip ClipFor(MusicPhase phase)
        {
            if (config == null) return null;
            if (phase == MusicPhase.Boss && config.musicBoss != null) return config.musicBoss;
            return config.musicBattle;
        }

        private void SwapMusic(AudioClip clip)
        {
            if (_musicA == null || _musicB == null) return;
            _musicStarted = true;

            AudioSource next = _useA ? _musicB : _musicA;
            _useA = !_useA;

            _musicWanted = clip != null;

            if (clip == null)
            {
                // 곡이 없으면 지금 것을 끄기만 한다 — 자리표시 소리를 만들지 않는다.
                _fadeLeft = 0f;
                if (_musicA != null) _musicA.Stop();
                if (_musicB != null) _musicB.Stop();
                return;
            }

            next.clip = clip;
            next.volume = 0f;
            next.Play();

            next.time = 0f; // 새 곡은 처음부터 — 루프 봉투가 0에서 올라온다
            SetHead(next, 0f);
            _fadeLeft = config != null ? Mathf.Max(0f, config.musicCrossfadeSeconds) : 0f;
            if (_fadeLeft <= 0f) ApplyFade(1f); // 0이면 즉시 갈아탄다
        }

        private void Update()
        {
            FollowPhase();
            AdvanceHeads();
            RestartFinishedMusic();
            TickFade();
            DrainCues();
        }

        /// <summary>도는 소스의 머리를 민다. **우리 시계이므로 플랫폼을 안 탄다.**</summary>
        private void AdvanceHeads()
        {
            float dt = Time.unscaledDeltaTime;
            if (_musicA != null && _musicA.isPlaying) _headA += dt;
            if (_musicB != null && _musicB.isPlaying) _headB += dt;
        }

        private void SetHead(AudioSource src, float value)
        {
            if (src == _musicA) _headA = value;
            else if (src == _musicB) _headB = value;
        }

        private float HeadOf(AudioSource src) =>
            src == _musicA ? _headA : src == _musicB ? _headB : 0f;

        /// <summary>
        /// 끝난 곡을 처음부터 다시 튼다 (2026-09-09 사용자 확정 · 사운드 문서 6장).
        ///
        /// **끝에서 0으로 내려간 뒤 곧바로 처음부터 0에서 올라온다** — 사이에 무음을 두지 않는다.
        /// 봉투는 <see cref="MusicLoop.Envelope"/>가 만들고 여기서는 **다시 트는 일만** 한다.
        /// </summary>
        private void RestartFinishedMusic()
        {
            // ⚠️ **지금 국면의 곡만 되돌린다.** 물러난 소스까지 보면 지난 국면 곡이 조용히 다시 돈다.
            if (!_musicWanted) return;
            Restart(Current());
        }

        private void Restart(AudioSource src)
        {
            if (src == null || src.clip == null) return;
            if (!MusicLoop.ShouldRestart(src.isPlaying, true)) return;

            src.time = 0f;
            SetHead(src, 0f);   // 봉투도 처음부터 — 안 되돌리면 새 곡이 꼬리 페이드로 시작한다
            src.Play();
        }

        /// <summary>겹쳐 넘기는 구간. 초는 SO가 들고(미확정) 여기서는 비율만 만든다.</summary>
        private void TickFade()
        {
            if (_fadeLeft <= 0f)
            {
                ApplyFade(1f);
                if (Previous().isPlaying && Previous().volume <= 0f) Previous().Stop();
                return;
            }

            float total = config != null ? Mathf.Max(0.0001f, config.musicCrossfadeSeconds) : 0.0001f;
            _fadeLeft = Mathf.Max(0f, _fadeLeft - Time.unscaledDeltaTime);
            ApplyFade(1f - _fadeLeft / total);
        }

        /// <summary>
        /// 두 소스의 음량을 다시 쓴다.
        ///
        /// **봉투가 둘 겹친다** — 국면을 넘기는 크로스페이드(<paramref name="t"/>)와
        /// 곡이 다시 시작하는 자리의 루프 페이드다. 서로 다른 것을 재므로 **곱한다** —
        /// 국면을 넘기는 중에 이음매가 오면 둘 다 걸리는 것이 맞다.
        /// </summary>
        private void ApplyFade(float t)
        {
            // **사람이 고른 볼륨이 기준이다** — SO 값은 기본값이고 여기서는 지금 값을 쓴다.
            float target = MusicVolume.Value;

            // ⚠️ **선형으로 넘기면 가운데가 꺼진다.** 두 곡을 t 와 1−t 로 섞으면 중간에서
            // 합이 √2 만큼 모자라 **볼륨이 한 번 파인다.** 등파워 곡선(사인·코사인)은
            // 제곱의 합이 1 이라 넘기는 내내 크기가 유지된다 — 2026-09-10 사용자 확정
            // 「볼륨 낮추는 구간이 부드럽게 넘어가도록」이 가리키는 자리다.
            float k = Mathf.Clamp01(t);
            float inGain = Mathf.Sin(k * Mathf.PI * 0.5f);
            float outGain = Mathf.Cos(k * Mathf.PI * 0.5f);

            Current().volume = target * inGain * LoopEnvelopeOf(Current());
            Previous().volume = target * outGain * LoopEnvelopeOf(Previous());
        }

        /// <summary>이 소스의 루프 봉투. 판정은 코어가 하고 여기서는 값만 읽어 넘긴다.</summary>
        private float LoopEnvelopeOf(AudioSource src)
        {
            if (src == null || src.clip == null) return 1f;
            float fade = config != null ? config.musicLoopFadeSeconds : 0f;
            return MusicLoop.Envelope(HeadOf(src), src.clip.length, fade);
        }

        private AudioSource Current() => _useA ? _musicA : _musicB;
        private AudioSource Previous() => _useA ? _musicB : _musicA;

        /// <summary>
        /// 쌓인 사건을 비우고 재생기에 넘긴다.
        ///
        /// ⚠️ **재생기가 없어도 비운다.** 안 비우면 목록이 자라기만 하고, 나중에
        /// 재생기가 붙는 순간 **지나간 소리가 한꺼번에 터진다.**
        /// </summary>
        private void DrainCues()
        {
            _drain.Clear();
            AudioSignals.Drain(_drain);
            if (sfx == null) return;

            for (int i = 0; i < _drain.Count; i++)
                sfx.Play(_drain[i].id, _drain[i].kind);
        }

        /// <summary>스테이지를 다시 시작할 때 효과음만 끊는다. **음악은 안 끊는다.**</summary>
        public void StopSfx()
        {
            if (sfx != null) sfx.StopAll();
        }
    }
}
