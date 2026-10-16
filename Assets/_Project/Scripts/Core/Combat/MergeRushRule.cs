using UnityEngine;

namespace MBI.Core.Combat
{
    /// <summary>
    /// **도전 층 합체 연출 — 게이지 차오름 · 합체 원형 펄스** (사용자 확정 #160 · 2026-10-16).
    ///
    /// 물류 유지(#154)는 그대로 두고, 도전 층이 서면 합체 게이지가 **짧게 차올라** 만충이 되고,
    /// 만충이면 합체 원형이 **커졌다 작아졌다** 하며 누를 때까지 강조한다.
    /// ✅ **범위 = 모든 도전 층(S1~S6)** · 합체가 있는 판에서만(로봇 B 해금 #103 전엔 합체가 없어 연출도 없다)
    /// (사용자 22:1x 보충). 파밍 층은 90초 규칙 그대로 · 「한 판에 한 번」 그대로.
    ///
    /// ⚠️ 값(3초 · 1.0↔1.15 · 0.6초 주기)은 **가정**이다(플랜 읽기 · 사용자가 다르면 바꾼다).
    /// </summary>
    public static class MergeRushRule
    {
        /// <summary>
        /// 🗑️ **폐기 — 범위가 모든 도전 층이 됐다**(사용자 22:1x 보충). 처음 읽기(S6 만)를 남긴다. 읽는 곳 없음.
        /// </summary>
        public const string StageId = "S6";

        /// <summary>지금 값 → 만충까지 걸리는 시간(초) ⚠️ 가정.</summary>
        public const float RushSeconds = 3f;

        /// <summary>펄스 배율 범위 ⚠️ 가정.</summary>
        public const float PulseMin = 1f, PulseMax = 1.15f;

        /// <summary>펄스 한 주기(초) ⚠️ 가정.</summary>
        public const float PulsePeriod = 0.6f;

        /// <summary>
        /// 이 판·층에 거는가 — **도전** 층이고 **합체가 있는** 판(파밍 층·합체 없는 판은 아님).
        /// 튜토리얼 제외는 부르는 쪽이 건다.
        /// </summary>
        public static bool AppliesTo(bool challengeLayer, bool hasMerge) => challengeLayer && hasMerge;

        /// <summary>
        /// 차오름 중 게이지(초) — 시작 값에서 만충까지 곧게 오른다. 자연 충전이 더 높으면 그쪽을 둔다.
        /// </summary>
        public static float ChargeAt(float startSeconds, float elapsed, float naturalSeconds)
        {
            float t = RushSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / RushSeconds);
            float rush = Mathf.Lerp(Mathf.Clamp(startSeconds, 0f, MergeSystem.GaugeFullSeconds), MergeSystem.GaugeFullSeconds, t);
            return Mathf.Max(rush, naturalSeconds);
        }

        /// <summary>펄스 배율 — 시각(초)에 대해 1.0 ↔ 1.15 를 한 주기에 한 번 오간다(코사인 · 0초 = 1.0).</summary>
        public static float PulseScale(float time)
        {
            float phase = PulsePeriod <= 0f ? 0f : (time % PulsePeriod) / PulsePeriod;
            float k = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
            return Mathf.Lerp(PulseMin, PulseMax, k);
        }

        /// <summary>중심을 그대로 두고 배율만큼 키운 자리.</summary>
        public static Rect Scaled(Rect r, float scale)
        {
            float w = r.width * scale, h = r.height * scale;
            return new Rect(r.center.x - w * 0.5f, r.center.y - h * 0.5f, w, h);
        }
    }
}
