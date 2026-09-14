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
        /// <summary>
        /// 배경 기본값 — **15%** (2026-09-14 사용자 지시).
        ///
        /// 09-10 의 30% 를 대신한다. 셋(배경·효과음·조작음)을 **같은 값**으로 맞춘 것이며,
        /// 그래서 이 값만 따로 움직이지 않는다.
        /// </summary>
        public const float Default = 0.15f;

        /// <summary>
        /// **시험판에서는 꺼 두고 연다** (사용자 확정 2026-09-10).
        ///
        /// 리허설과 촬영 준비는 **같은 곡을 몇 번이고 다시 듣는 자리**라 곡이 방해가 된다.
        /// <see cref="Default"/> 를 0 으로 내리지 않는 이유는 **배포판의 기본값이 30% 라는
        /// 확정이 살아 있기 때문**이다 — 두 값은 서로 다른 것을 가리킨다.
        ///
        /// ⚠️ **끄는 것이지 없애는 것이 아니다.** 슬라이더는 그대로라 올리면 들린다.
        /// 한 번 올리면 그 값이 기기에 남아 다음에도 그 값으로 열린다.
        /// </summary>
        /// ⚠️ **09-10 의 「시험판은 0 으로 연다」는 폐기했다**(2026-09-14 사용자 지시).
        /// 그 규칙은 기본값이 30% 라 리허설에서 곡이 방해가 된다는 근거였는데,
        /// **셋을 다 15% 로 맞추라는 지시가 그 근거를 없앴다** — 15% 는 방해가 되는 크기가
        /// 아니고, 시험판만 0 이면 **지금 만지는 빌드에서 지시한 값이 안 보인다.**
        public static float StartupDefault => Default;

        private static float _value = Default;

        /// <summary>지금 볼륨(0~1).</summary>
        public static float Value => _value;

        /// <summary>0~1 로 자른다 — 1 을 넘기면 음악이 효과음을 덮는다.</summary>
        public static float Clamp(float v) => Mathf.Clamp01(v);

        /// <summary>사람이 고른다. 자른 값이 실제로 들어간다.</summary>
        public static void Set(float v) => _value = Clamp(v);

        /// <summary>기본값으로 되돌린다.</summary>
        public static void Reset()
        {
            _value = Default;
            PreviewRequested = false;
        }

        /// <summary>
        /// **사람이 볼륨을 만졌다** — 미리듣기를 열어도 된다는 표시
        /// (2026-09-10 · 플랜 §67-3 결함 ③).
        ///
        /// 웹에서는 **누르기 전까지 오디오가 잠겨 있어서**, 부팅 때 건 재생은 소리 없이
        /// 흘러가고 나중에 볼륨만 올려도 살아나지 않는다. 슬라이더를 만진 그 순간이
        /// **누른 순간**이므로, 그때 재생을 새로 걸 수 있게 여기 남긴다.
        ///
        /// ⚠️ **재생기가 한 번 쓰고 내린다.** 계속 서 있으면 곡이 끝날 때마다 미리듣기가
        /// 다시 열려 메뉴에서 곡이 되살아난다.
        /// </summary>
        public static bool PreviewRequested;

        /// <summary>
        /// 화면에 적는 말 — **퍼센트 정수**다. 0.3 을 「0.3」으로 적으면 무엇의 0.3 인지 모른다.
        /// </summary>
        public static string Label(float v) => $"{Mathf.RoundToInt(Clamp(v) * 100f)}%";
    }
}
