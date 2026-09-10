using UnityEngine;

namespace MBI.Core.Audio
{
    /// <summary>
    /// 사용자가 고른 배경 음악 볼륨 (2026-09-10 사용자 확정).
    ///
    /// **SO 의 `musicVolume` 은 기본값이고, 여기 있는 것은 사람이 고친 값이다.**
    /// 둘을 가르는 이유 — SO 를 런타임에 고치면 에디터에서 **자산 파일이 바뀐다.**
    /// 고친 값이 리포에 들어가면 다음 사람이 남의 취향으로 게임을 켠다.
    ///
    /// ⚠️ **관계는 여전히 <see cref="AudioMix"/> 가 든다** — 음악이 효과음을 덮지 않는다는
    /// 규정은 기본값 사이의 관계이며, 사람이 음악을 올리는 것은 그 사람의 선택이다.
    /// </summary>
    public static class MusicVolume
    {
        /// <summary>기본 **30%** (사용자 확정 2026-09-10). SO 기본값도 같은 값이어야 한다.</summary>
        public const float Default = 0.30f;

        private static float _value = Default;

        /// <summary>지금 볼륨(0~1).</summary>
        public static float Value => _value;

        /// <summary>0~1 로 자른다 — 1 을 넘기면 음악이 효과음을 덮는다.</summary>
        public static float Clamp(float v) => Mathf.Clamp01(v);

        /// <summary>사람이 고른다. 자른 값이 실제로 들어간다.</summary>
        public static void Set(float v) => _value = Clamp(v);

        /// <summary>기본값으로 되돌린다.</summary>
        public static void Reset() => _value = Default;

        /// <summary>
        /// 화면에 적는 말 — **퍼센트 정수**다. 0.3 을 「0.3」으로 적으면 무엇의 0.3 인지 모른다.
        /// </summary>
        public static string Label(float v) => $"{Mathf.RoundToInt(Clamp(v) * 100f)}%";
    }
}
