namespace MBI.Core
{
    /// <summary>
    /// 상단 칩에 찍는 **「전투력 n」** (2026-09-18 · 설계 지시 「= 지금 물류 출력 × 마운트계수
    /// 값 · 이름만」 · ⚠️ **가정 · 설계 역기입 자리**).
    ///
    /// ⚠️⚠️ **새 수를 만드는 것이 아니다.** 전투력은 이미 화면에 있던 **물류 출력**이고,
    /// 여기서는 **이름만** 바꿔 단다 — 설계가 「이름만」이라고 못 박은 자리가 그것이다.
    /// 판정에는 안 쓰인다.
    ///
    /// 🗑️ **폐기 — 「출력 × (1 + 적재율 × 계수)」**(2026-09-18 · 설계 검토 ③).
    ///
    /// ⚠️⚠️ 그것은 **지시에 없던 새 식**이었다. 계수 1 이면 마운트가 찰수록 전투력이
    /// **최대 두 배로 부풀어**, 촬영 화면의 「전투력」과 같은 화면의 **물류 출력이 서로 다른
    /// 수**가 된다 — 한 값이 두 얼굴을 갖는 꼴이고 지침 §7 이 막는 자리다.
    /// 지시는 「기존 물류 출력 × 마운트계수 · **이름만**」이었고, 마운트계수는
    /// **아직 값이 없는 칸**이지 1 로 채워 둘 칸이 아니었다.
    ///
    /// 📌 지금은 **계수 0 = 곱하지 않는다**가 기본이다 — 전투력은 물류 출력 그대로이며,
    /// 마운트계수가 설계에서 확정되면 그때 값이 선다(`combatPowerMountFactorTbd` · 판정 요청).
    ///
    /// 📌 지침 §7 — 이 수가 **두 곳에 살면 안 된다.** 화면 어디서도 직접 곱하지 않고
    /// 이 함수를 부른다.
    /// </summary>
    public static class CombatPower
    {
        /// <summary>
        /// 마운트 계수 기본값 — **0 = 안 곱한다**(2026-09-18 · 설계 검토 ③).
        /// 🗑️ 구 기본값 1 폐기. **미확정을 1 로 채우면 화면이 지어낸 수를 보인다.**
        /// </summary>
        public const float DefaultMountFactor = 0f;

        /// <summary>
        /// 적재율 0~1 을 받아 전투력을 낸다.
        /// ⚠️ 상한이 없는 마운트(Capacity 0)에서는 **적재율을 0 으로 본다** —
        /// 판정이 없는 자리에서 배수를 지어내면 화면이 거짓말을 한다.
        /// </summary>
        public static int Of(float logisticsOutput, float mountTotal, float mountCapacity,
                             float mountFactor = DefaultMountFactor)
        {
            if (logisticsOutput <= 0f) return 0;

            // 📌 **계수가 0 이면 출력 그대로다** — 「이름만」이 그 뜻이다.
            if (mountFactor <= 0f) return UnityEngine.Mathf.RoundToInt(logisticsOutput);

            float ratio = mountCapacity > 0f
                ? UnityEngine.Mathf.Clamp01(mountTotal / mountCapacity)
                : 0f;
            return UnityEngine.Mathf.RoundToInt(logisticsOutput * (1f + ratio * mountFactor));
        }
    }
}
