namespace MBI.Core
{
    /// <summary>
    /// **마운트 강화 최소판** (2026-10-16 사용자 확정 #150).
    ///
    /// 단추 하나 — 「강화 (재료 전부 투입)」. 재료가 s4Cost(ENHANCE_DATA · 100) 이상이면 눌리고,
    /// 누르면 재료를 쓰고 저장에 표식을 남긴다. 표식이 서면 마운트계수가 ×1.45(enhancedMountCoef)다.
    ///
    /// ⚠️⚠️ 🗑️ **구 규칙 「S4 부터는 저절로 ×1.45」(powerModel 조건) 폐기.** 재료를 안 넣어도
    ///    계수가 켜져 있어 강화 재료가 쓸 데가 없었다(#149 대조 보고 · `TrySpendEnhMaterial` 호출 0).
    ///    이제 계수는 **표식 하나**가 정한다 — 판 축(powerModel)은 계수를 안 정한다.
    ///
    /// ⚠️ 되돌리기 없음 · 「처음부터」로만 지워진다(#141 갈래 — 새 저장이 표식 없이 선다).
    /// ⚠️ 튜토리얼 T6(이중 표시 · CTA · 강제 버튼)은 안 짓는다 — 기획서 T6 전체는 뒤다.
    /// </summary>
    public static class MountEnhanceRule
    {
        /// <summary>단추 글자 — ⚠️ 가정(#150 · 사용자가 바꿀 수 있다).</summary>
        public const string ButtonLabel = "강화 (재료 전부 투입)";

        /// <summary>켜진 뒤 글자 — ⚠️ 가정(#150). 배율은 자산 값을 받아 적는다(1.45 를 글자에 박지 않는다).</summary>
        public static string AppliedLabel(float enhancedCoef) => $"강화 ×{enhancedCoef:0.##} 적용";

        /// <summary>단추가 눌리는가 — 아직 안 켰고 · 비용이 있고 · 재료가 비용 이상.</summary>
        public static bool CanEnhance(bool enhanced, double material, double cost) =>
            !enhanced && cost > 0d && material >= cost;

        /// <summary>
        /// **바로가기 점프로 강화 표식을 켜는 판**(사용자 확정 #162 · 2026-10-16) — S4 부터.
        /// #156(마운트 만충)과 같은 결 · 심사자용 · 「그 판에 자연 진행으로 온 상태」 흉내.
        /// 재료는 안 건드린다 · S1~S3 점프는 안 켠다(켜진 표식을 끄지도 않는다) · 「처음부터」면 꺼진다.
        /// </summary>
        public const int JumpEnhanceFromStage = 4;

        /// <summary>이 판 번호로 점프하면 강화 표식을 켜는가.</summary>
        public static bool JumpTurnsOn(int stageNumber) => stageNumber >= JumpEnhanceFromStage;

        /// <summary>마운트계수 — **표식 ? 강화 : 기본**. 러너와 하네스가 같은 문을 지난다.</summary>
        public static float MountCoef(bool enhanced, float baseCoef, float enhancedCoef) =>
            enhanced ? enhancedCoef : baseCoef;
    }
}
