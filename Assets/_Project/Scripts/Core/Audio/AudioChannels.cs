using UnityEngine;

namespace MBI.Core.Audio
{
    /// <summary>
    /// 사람이 고르는 볼륨 **채널 셋** (2026-09-11 사용자 확정 · 사운드 문서 7장 · 플랜 §71-16 ③).
    ///
    /// **왜 셋인가.** 사운드 문서 7장이 채널을 셋으로 정했고(배경 · 효과음 · 조작음),
    /// 그 셋은 **사람이 서로 다른 이유로 줄이는 것**이다 — 음악은 취향, 효과음은 피로,
    /// 조작음은 딸깍이 거슬릴 때. 슬라이더 하나로 묶으면 셋 중 하나가 거슬려도
    /// **나머지 둘까지 함께 잃는다.**
    ///
    /// ⚠️ **관계는 여전히 <see cref="AudioMix"/> 가 든다.** 「경고 &gt; 효과음 &gt; 조작음」은
    /// **기본값 사이의 관계**이고, 여기 있는 것은 그 위에 곱해지는 **사람의 선택**이다.
    /// 둘을 섞으면 사람이 효과음을 올린 순간 경고보다 커진다.
    ///
    /// ⚠️ **배경은 <see cref="MusicVolume"/> 이 그대로 든다** — 값을 두 곳에 두면
    /// 한쪽만 바뀌는 날이 온다. 여기서는 **저장과 읽기만** 거들고 값은 저쪽에 맡긴다.
    /// </summary>
    public static class AudioChannels
    {
        /// <summary>사람이 만지는 채널 셋.</summary>
        public enum Channel
        {
            /// <summary>배경 음악.</summary>
            Music = 0,
            /// <summary>효과음 — 게임이 내는 소리. **경고도 여기 든다**(아래 주석).</summary>
            Effect = 1,
            /// <summary>조작음 — 조립 화면의 딸깍.</summary>
            Ui = 2,
        }

        /// <summary>
        /// 효과음·조작음 기본값. ⚠️ **가정 100%** — 사운드 문서에 사람 기본값의 절이 없다
        /// (배경 30% 만 2026-09-10 에 확정됐다). 설계 역기입 자리.
        ///
        /// 100% 로 둔 근거: 채널 슬라이더는 **줄이라고 있는 것**이고, 기본을 낮춰 두면
        /// 「소리가 작다」를 슬라이더에서 찾아야 한다 — 기본값이 규정된 믹스(`AudioMix`)를
        /// 그대로 들려주는 자리가 100% 다.
        /// </summary>
        public const float EffectDefault = 1f;

        /// <summary>조작음 기본값. ⚠️ **가정 100%** — <see cref="EffectDefault"/> 와 같은 근거.</summary>
        public const float UiDefault = 1f;

        private const string KeyPrefix = "mbi.volume.";

        private static float _effect = EffectDefault;
        private static float _ui = UiDefault;

        /// <summary>
        /// 소리 갈래가 어느 채널에 드는가.
        ///
        /// ⚠️ **경고는 효과음 채널이다.** 채널이 셋뿐이라 넷째를 만들 수 없고, 경고는
        /// 「게임이 내는 소리」쪽이다. 경고가 더 크게 들려야 하는 것은 `AudioMix` 의
        /// 기본값이 이미 지키고 있으므로, 사람이 효과음을 줄이면 경고도 **같은 비율로**
        /// 줄어 관계가 보존된다.
        /// </summary>
        public static Channel ChannelOf(SoundKind kind) =>
            kind == SoundKind.Ui ? Channel.Ui : Channel.Effect;

        /// <summary>지금 값(0~1).</summary>
        public static float Value(Channel channel)
        {
            switch (channel)
            {
                case Channel.Music: return MusicVolume.Value;
                case Channel.Ui: return _ui;
                default: return _effect;
            }
        }

        /// <summary>그 갈래의 소리에 곱할 값.</summary>
        public static float Value(SoundKind kind) => Value(ChannelOf(kind));

        /// <summary>사람이 고른다. 0~1 로 자른다.</summary>
        public static void Set(Channel channel, float v)
        {
            v = Mathf.Clamp01(v);
            switch (channel)
            {
                case Channel.Music: MusicVolume.Set(v); break;
                case Channel.Ui: _ui = v; break;
                default: _effect = v; break;
            }
        }

        /// <summary>기본값.</summary>
        public static float DefaultOf(Channel channel)
        {
            switch (channel)
            {
                case Channel.Music: return MusicVolume.StartupDefault;
                case Channel.Ui: return UiDefault;
                default: return EffectDefault;
            }
        }

        /// <summary>화면에 적는 말 — 퍼센트 정수.</summary>
        public static string Label(Channel channel)
        {
            switch (channel)
            {
                case Channel.Music: return "배경 음악";
                case Channel.Ui: return "조작음";
                default: return "효과음";
            }
        }

        /// <summary>저장 키. **채널마다 따로 둔다** — 한 덩어리로 묶으면 하나만 고쳐도 셋이 다 쓰인다.</summary>
        public static string KeyOf(Channel channel) => KeyPrefix + channel.ToString().ToLowerInvariant();

        /// <summary>기기에 남긴다.</summary>
        public static void Save(Channel channel)
        {
            PlayerPrefs.SetFloat(KeyOf(channel), Value(channel));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 기기에서 읽는다. **없으면 기본값 그대로** — 첫 실행은 정상 경로이지 예외가 아니다.
        /// </summary>
        public static void Load()
        {
            foreach (Channel c in new[] { Channel.Music, Channel.Effect, Channel.Ui })
            {
                string key = KeyOf(c);
                Set(c, PlayerPrefs.HasKey(key) ? PlayerPrefs.GetFloat(key) : DefaultOf(c));
            }
        }

        /// <summary>도메인 리로드 비활성 시 이전 Play 의 값이 남는 것을 막는다.</summary>
        public static void Reset()
        {
            _effect = EffectDefault;
            _ui = UiDefault;
        }
    }
}
