namespace MBI.Core
{
    /// <summary>
    /// 상단 칩에 찍는 **「전투력 n」** (2026-09-18 · 설계 지시 「= 지금 물류 출력 × 마운트계수
    /// 값 · 이름만」 · ⚠️ **가정 · 설계 역기입 자리**).
    ///
    /// ⚠️⚠️ **새 수를 만드는 것이 아니다.** 전투력은 이미 화면에 있던 **물류 출력**이고,
    /// 여기서는 거기에 **지금 마운트가 얼마나 찼는가**를 한 번 곱해 이름만 바꿔 단다 —
    /// 설계가 「이름만」이라고 못 박은 자리가 그것이다. 판정에는 안 쓰인다.
    ///
    /// ⚠️ **계수는 가정이다.** 마운트가 비면 출력 그대로(×1), 가득이면 ×(1+계수)이며
    /// 계수 기본값은 1 이다 — 문서에 전투력 식이 없다. 확정되면 여기 한 줄만 바뀐다.
    ///
    /// 📌 지침 §7 — 이 수가 **두 곳에 살면 안 된다.** 화면 어디서도 직접 곱하지 않고
    /// 이 함수를 부른다.
    /// </summary>
    public static class CombatPower
    {
        /// <summary>마운트 계수 기본값 — ⚠️ 가정.</summary>
        public const float DefaultMountFactor = 1f;

        /// <summary>
        /// 적재율 0~1 을 받아 전투력을 낸다.
        /// ⚠️ 상한이 없는 마운트(Capacity 0)에서는 **적재율을 0 으로 본다** —
        /// 판정이 없는 자리에서 배수를 지어내면 화면이 거짓말을 한다.
        /// </summary>
        public static int Of(float logisticsOutput, float mountTotal, float mountCapacity,
                             float mountFactor = DefaultMountFactor)
        {
            if (logisticsOutput <= 0f) return 0;
            float ratio = mountCapacity > 0f
                ? UnityEngine.Mathf.Clamp01(mountTotal / mountCapacity)
                : 0f;
            if (mountFactor < 0f) mountFactor = 0f;
            return UnityEngine.Mathf.RoundToInt(logisticsOutput * (1f + ratio * mountFactor));
        }
    }
}
