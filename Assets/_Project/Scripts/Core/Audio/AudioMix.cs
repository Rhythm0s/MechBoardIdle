using MBI.Data;

namespace MBI.Core.Audio
{
    /// <summary>
    /// 볼륨을 고르는 규칙 (사운드 문서 2장 · 2026-09-09 신설).
    ///
    /// **문서가 붙드는 것은 절대값이 아니라 관계 하나다** — 경고 &gt; 효과음 &gt; 조작음.
    /// 절대값은 기기와 재생 환경에 따라 달라져 박아 두면 곧 틀린 값이 되지만,
    /// **그 관계는 어느 환경에서도 유지돼야 한다.** 그래서 값은 SO가 들고 관계는 여기가 든다.
    /// </summary>
    public static class AudioMix
    {
        /// <summary>이 갈래의 볼륨. 설정이 없으면 0 — 모르는 값을 지어내지 않는다.</summary>
        public static float GainOf(AudioConfig config, SoundKind kind)
        {
            if (config == null) return 0f;
            switch (kind)
            {
                case SoundKind.Warning: return config.warningVolume;
                case SoundKind.Ui: return config.uiVolume;
                default: return config.effectVolume;
            }
        }

        /// <summary>
        /// 상대 순서가 지켜지는가 — **경고 &gt; 효과음 &gt; 조작음.**
        ///
        /// 이것이 이 파일의 존재 이유다. 점값은 귀가 정하지만 **순서가 뒤집히면
        /// 문서가 정한 것이 깨진다** — 병목 경고가 딸깍보다 작으면 화면을 안 보는
        /// 플레이어에게 닿지 않고, 그러면 사운드 문서 1장의 규정 자체가 무너진다.
        /// </summary>
        public static bool OrderHolds(AudioConfig config)
        {
            if (config == null) return false;
            return config.warningVolume > config.effectVolume
                && config.effectVolume > config.uiVolume;
        }

        /// <summary>
        /// 배경 음악 볼륨. **앞에 나서지 않는다** — 효과음이 상태를 알리는 통로이므로
        /// 음악이 그것을 덮으면 안 된다(6장 「성격」).
        /// </summary>
        public static float MusicGain(AudioConfig config) => config != null ? config.musicVolume : 0f;

        /// <summary>음악이 효과음을 안 덮는가. 6장 「성격」이 요구하는 관계다.</summary>
        public static bool MusicStaysBehind(AudioConfig config)
            => config != null && config.musicVolume < config.effectVolume;
    }
}
