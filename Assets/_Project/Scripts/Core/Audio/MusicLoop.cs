using UnityEngine;

namespace MBI.Core.Audio
{
    /// <summary>
    /// 곡이 다시 시작하는 자리를 **페이드 아웃 → 페이드 인**으로 넘긴다
    /// (2026-09-09 사용자 확정 · 사운드 문서 6장 「길이」).
    ///
    /// **왜 이음매를 안 맞추는가.** 문서가 요구한 것은 「이어 붙는 지점이 들리지 않게」이고,
    /// 그 방법으로 **파형을 정확히 물리는 것**과 **양 끝을 죽이는 것** 둘이 있다.
    /// 뒤의 것을 고른 근거는 **곡이 2분을 넘는다**는 것이다 — 176초·168초라 이음매가
    /// 3분에 한 번 오고, 그 한 번에 잠깐 여리게 지나가는 것은 들어도 거슬리지 않는다.
    /// 앞의 것은 자산 쪽에서 파형을 다시 잘라야 하는데 **얻는 것이 그만큼 크지 않다.**
    ///
    /// ⚠️ **소리가 사라지지 않는다.** 끝에서 0으로 내려간 뒤 **곧바로 처음부터 0에서 올라온다** —
    /// 중간에 무음 구간을 두지 않는다. 「페이드 아웃 하고 나서 페이드 인」이지
    /// 「페이드 아웃 하고 쉬었다가 페이드 인」이 아니다.
    /// </summary>
    public static class MusicLoop
    {
        /// <summary>
        /// 지금 이 순간의 음량 배수(0~1). 곡 볼륨에 **곱한다.**
        ///
        /// <paramref name="fadeSeconds"/>가 0 이하면 늘 1이다 — 페이드를 끄는 자리이며,
        /// 그때는 이음매가 파형 그대로 들린다.
        ///
        /// ⚠️ **페이드가 곡 절반보다 길면 줄여서 쓴다.** 안 줄이면 들어가는 페이드와
        /// 나가는 페이드가 겹쳐 **곡 한가운데가 가장 작아진다** — 짧은 자산을 넣었을 때
        /// 조용히 그렇게 되는 것을 막는다.
        /// </summary>
        public static float Envelope(float time, float length, float fadeSeconds)
        {
            if (length <= 0f || fadeSeconds <= 0f) return 1f;

            float fade = Mathf.Min(fadeSeconds, length * 0.5f);
            float t = Mathf.Clamp(time, 0f, length);

            if (t < fade) return t / fade;                       // 처음 — 올라온다
            if (t > length - fade) return (length - t) / fade;   // 끝 — 내려간다
            return 1f;
        }

        /// <summary>
        /// 지금 다시 틀어야 하는가. **끝났으면 참**이다.
        ///
        /// `AudioSource.loop`를 쓰지 않는 이유 — 그쪽은 파형을 바로 물려 버려서
        /// **페이드가 걸릴 자리가 없다.** 끝나는 것을 보고 다시 트는 쪽이라야
        /// 위 봉투가 양 끝에 걸린다.
        /// </summary>
        public static bool ShouldRestart(bool isPlaying, bool hasClip) => hasClip && !isPlaying;
    }
}
