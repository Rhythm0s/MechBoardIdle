using System.Collections.Generic;
using MBI.Core.Audio;
using MBI.Data;
using UnityEngine;

namespace MBI.UI
{
    /// <summary>
    /// 효과음 재생기 (사운드 문서 7장 · 2026-09-09 신설).
    ///
    /// **자리를 미리 만들어 두고 돌려 쓴다.** 소리마다 `GameObject`를 새로 만들면
    /// 초당 다섯 발 나는 발사음에서 그것이 그대로 비용이 된다.
    ///
    /// ⚠️ **자산이 없으면 조용히 건너뛴다.** 자리표시 소리를 내지 않는다 —
    /// 없는 소리를 흰 잡음으로 대신하면 **그것이 그 사건의 소리인 줄 알게 되고**,
    /// 진짜 자산이 왔을 때 「소리가 바뀌었다」가 아니라 「소리가 이상해졌다」가 된다.
    /// 그림 쪽에서 색 사각을 폴백으로 남긴 것과 **반대 판단**이며, 근거가 다르다 —
    /// 색 사각은 자리를 보여 주지만 잡음은 아무것도 안 알린다.
    ///
    /// ⚠️ **소리가 판정을 바꾸지 않는다**(1장). 이 컴포넌트가 통째로 없어도 게임은 돈다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SfxPlayer : MonoBehaviour
    {
        [Tooltip("겹침 상한·볼륨·자산. 씬 생성기가 주입한다.")]
        [SerializeField] private AudioConfig config;

        private AudioSource[] _voices;
        private SoundOverlap _overlap;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

        /// <summary>지금 이 자리에서 도는 소리 — 끝나면 목록에서 뺀다.</summary>
        private string[] _slotId;
        private float[] _slotStarted;
        private float[] _slotEnds;

        /// <summary>겹침 규칙. 값은 SO가 주고 판정은 코어가 한다.</summary>
        public SoundOverlap Overlap => _overlap;

        private void Awake()
        {
            BuildClipTable();
            BuildVoices();
        }

        /// <summary>
        /// 이름 → 자산 표. **비어 있는 자리는 표에 안 넣는다** —
        /// 「이름은 있는데 소리가 없다」와 「이름이 없다」를 같은 자리에서 다루면
        /// 나중에 어느 쪽이었는지 못 가린다.
        /// </summary>
        private void BuildClipTable()
        {
            _clips.Clear();
            if (config == null || config.sfxClips == null) return;

            int n = Mathf.Min(SoundIds.All.Length, config.sfxClips.Length);
            for (int i = 0; i < n; i++)
            {
                AudioClip clip = config.sfxClips[i];
                if (clip == null) continue; // 아직 안 온 자산 — 조용히 건너뛴다
                _clips[SoundIds.All[i]] = clip;
            }
        }

        private void BuildVoices()
        {
            int count = config != null ? Mathf.Max(1, config.sfxVoiceCount) : 1;
            int limit = config != null ? Mathf.Max(1, config.overlapLimitPerSound) : 1;

            _voices = new AudioSource[count];
            _slotId = new string[count];
            _slotStarted = new float[count];
            _slotEnds = new float[count];

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"sfx_{i}");
                go.transform.SetParent(transform, false);
                AudioSource src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                // 2D로 둔다 — 이 게임의 소리는 「무슨 일이 났는가」를 알리는 것이지
                // 「어디서 났는가」가 아니다. 3D로 두면 화면 밖 사건이 작게 들린다.
                src.spatialBlend = 0f;
                _voices[i] = src;
            }

            _overlap = new SoundOverlap(limit, count);
        }

        private void Update()
        {
            RetireFinished();
        }

        /// <summary>끝난 자리를 겹침 목록에서 뺀다. 안 빼면 상한이 영영 차 있다.</summary>
        private void RetireFinished()
        {
            if (_voices == null) return;
            float now = Time.unscaledTime;
            for (int i = 0; i < _voices.Length; i++)
            {
                if (_slotId[i] == null || now < _slotEnds[i]) continue;
                _overlap.Retire(_slotId[i], _slotStarted[i]);
                _slotId[i] = null;
            }
        }

        /// <summary>
        /// 소리 하나를 낸다. **자산이 없으면 아무 일도 안 하고 false**를 준다 —
        /// 겹침 목록도 안 건드린다. 없는 소리가 자리를 먹으면 있는 소리가 밀린다.
        /// </summary>
        public bool Play(string id, SoundKind kind)
        {
            if (_overlap == null || string.IsNullOrEmpty(id)) return false;
            if (!_clips.TryGetValue(id, out AudioClip clip) || clip == null) return false;

            float now = Time.unscaledTime;
            SoundOverlap.Decision d = _overlap.Take(id, now);
            if (!d.play || d.slot < 0 || d.slot >= _voices.Length) return false;

            AudioSource src = _voices[d.slot];
            if (d.evicted && src.isPlaying) src.Stop(); // 가장 오래된 것을 끊는다(7장)

            src.clip = clip;
            src.volume = AudioMix.GainOf(config, kind);
            src.Play();

            _slotId[d.slot] = id;
            _slotStarted[d.slot] = now;
            _slotEnds[d.slot] = now + clip.length;
            return true;
        }

        /// <summary>스테이지를 다시 시작할 때 도는 것을 잊는다.</summary>
        public void StopAll()
        {
            if (_voices == null) return;
            for (int i = 0; i < _voices.Length; i++)
            {
                if (_voices[i] != null) _voices[i].Stop();
                _slotId[i] = null;
            }
            _overlap.Clear();
        }
    }
}
