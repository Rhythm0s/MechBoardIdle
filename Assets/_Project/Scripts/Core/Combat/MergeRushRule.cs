using UnityEngine;

namespace MBI.Core.Combat
{
    /// <summary>
    /// **S6 합체 연출 — 게이지 차오름 · 합체 원형 펄스** (사용자 확정 #160 · 2026-10-16).
    ///
    /// 물류 유지(#154)는 그대로 두고, S6 도전 층이 서면 합체 게이지가 **짧게 차올라** 만충이 되고,
    /// 만충이면 합체 원형이 **커졌다 작아졌다** 하며 누를 때까지 강조한다.
    /// 다른 판은 지금대로(게이지 90초 · 파밍 이어 감).
    ///
    /// ⚠️ 값(3초 · 1.0↔1.15 · 0.6초 주기)은 **가정**이다(플랜 읽기 · 사용자가 다르면 바꾼다).
    /// </summary>
    public static class MergeRushRule
    {
        /// <summary>차오름을 거는 판 — S6(보스 판 · 「합체 필연」).</summary>
        public const string StageId = "S6";

        /// <summary>지금 값 → 만충까지 걸리는 시간(초) ⚠️ 가정.</summary>
        public const float RushSeconds = 3f;

        /// <summary>펄스 배율 범위 ⚠️ 가정.</summary>
        public const float PulseMin = 1f, PulseMax = 1.15f;

        /// <summary>펄스 한 주기(초) ⚠️ 가정.</summary>
        public const float PulsePeriod = 0.6f;

        /// <summary>이 판·층에 거는가 — S6 **도전** 층만(파밍 층·튜토리얼은 아님).</summary>
        public static bool AppliesTo(string stageId, bool challengeLayer) =>
            challengeLayer && stageId == StageId;

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
