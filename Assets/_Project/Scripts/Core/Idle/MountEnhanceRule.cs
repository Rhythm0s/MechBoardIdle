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
    /// ✅ **단, 최소 안내 하나는 있다**(사용자 확정 #182 · 2026-10-19) — S4 파밍 · B 해금 뒤 · 재료와 고철이
    ///    다 차면 막 두 장(전투 「▼ 조립」 → 보드 A 마운트 칸 (1,4)) · 강화하면 풀린다(<see cref="TutorialActive"/>).
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
        /// **재료 + 고철 둘 다**(사용자 확정 #172 · 2026-10-19) — 재료 조건 위에 고철 조건을 얹는다.
        /// 설정 판 단추 · 마운트 팝오버 · 방치 런타임(실제 지출)이 **이 한 줄**을 지난다.
        /// 고철 비용 0 이면 종전(재료만)과 같다.
        /// </summary>
        public static bool CanEnhance(bool enhanced, double material, double cost, double scrap, double scrapCost) =>
            CanEnhance(enhanced, material, cost) && (scrapCost <= 0d || scrap >= scrapCost - 1e-9);

        /// <summary>
        /// **바로가기 점프로 강화 표식을 켜는 판**(사용자 확정 #162 · 2026-10-16) — S4 부터.
        /// #156(마운트 만충)과 같은 결 · 심사자용 · 「그 판에 자연 진행으로 온 상태」 흉내.
        /// 재료는 안 건드린다 · S1~S3 점프는 안 켠다(켜진 표식을 끄지도 않는다) · 「처음부터」면 꺼진다.
        /// </summary>
        public const int JumpEnhanceFromStage = 4;

        /// <summary>이 판 번호로 점프하면 강화 표식을 켜는가.</summary>
        public static bool JumpTurnsOn(int stageNumber) => stageNumber >= JumpEnhanceFromStage;

        // ── #182 S4 강화 최소 안내(2026-10-19 사용자 확정 · T3 · 로봇 B 해금과 같은 꼴) ──────────

        /// <summary>안내가 서는 판 — S4(로봇 B 해금과 같은 판 · 해금 뒤에 선다).</summary>
        public const string TutorialStageId = "S4";

        /// <summary>전투 화면 문구 — ⚠️ 가정(#182 · 사용자가 바꿀 수 있다).</summary>
        public const string CombatPrompt = "조립에서 마운트를 탭해 강화해보세요";

        /// <summary>보드 화면 마운트 칸 위 안내 — ⚠️ 가정(#182).</summary>
        public const string BoardHint = "마운트를 탭한 뒤 강화를 누르세요";

        /// <summary>
        /// **안내가 서는가** — 튜토리얼 마침 · S4 파밍 · 로봇 B 해금됨 · 아직 강화 안 함 · **재료와 고철이 둘 다 참**.
        /// 모자라면 막을 안 띄운다(갇힘 방지 · 골드 부족 때 해금 막과 같은 결). 저장 장부 신설 0 —
        /// 끝은 강화 표식이 판정한다. 바로가기 S4+ 점프는 #162 로 표식이 켜져 있어 안 선다.
        /// </summary>
        public static bool TutorialActive(bool tutorialCleared, int stageNumber, bool isFarming, bool robotBUnlocked,
                                          bool enhanced, double material, double cost, double scrap, double scrapCost) =>
            tutorialCleared && isFarming && robotBUnlocked
            && stageNumber == StageMapName.NumberOf(TutorialStageId)
            && CanEnhance(enhanced, material, cost, scrap, scrapCost);

        /// <summary>마운트계수 — **표식 ? 강화 : 기본**. 러너와 하네스가 같은 문을 지난다.</summary>
        public static float MountCoef(bool enhanced, float baseCoef, float enhancedCoef) =>
            enhanced ? enhancedCoef : baseCoef;
    }
}
