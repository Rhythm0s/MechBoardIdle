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
    /// ✅ **단, 최소 안내 하나는 있다**(사용자 확정 #182 → **#188 S4 → S3** · 2026-10-19) — S3 파밍 · 재료와 고철이
    ///    다 차면 막 두 장(전투 「▼ 조립」 → 보드 A 마운트 칸 (1,4)) · 강화하면 풀린다(<see cref="TutorialActive"/>).
    ///    🗑️ 「B 해금 뒤」 조건 — #188 로 걷음(S3 에는 B 가 없다 · B 해금은 S4 #189).
    /// </summary>
    public static class MountEnhanceRule
    {
        /// <summary>단추 글자 — ⚠️ 가정(#150 · 사용자가 바꿀 수 있다).</summary>
        public const string ButtonLabel = "강화 (재료 전부 투입)";   // 🗑️ #199 — 화면은 `ButtonLabelWithCost` · 비용 50 만 쓰는데 「전부」가 거짓이었다

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
        public const int JumpEnhanceFromStage = 4;   // #198(2026-10-19) — S3 점프는 자동 안 켬(재료 63 지급 → 강화 막에서 직접 누름) · 🗑️ #188 의 3.

        /// <summary>이 판 번호로 점프하면 강화 표식을 켜는가.</summary>
        public static bool JumpTurnsOn(int stageNumber) => stageNumber >= JumpEnhanceFromStage;

        // ── #182 강화 최소 안내(2026-10-19 사용자 확정 · T3 · 로봇 B 해금과 같은 꼴) · #188 S4 → S3 ──────────

        /// <summary>안내가 서는 판 — **S3**(#188 · 🗑️ 구 S4). 주제 「마운트 강화」(STAGE_DATA 3행).</summary>
        public const string TutorialStageId = "S3";

        /// <summary>전투 화면 문구 — ⚠️ 가정(#182 · 사용자가 바꿀 수 있다).</summary>
        public const string CombatPrompt = "조립에서 마운트를 탭해 강화해보세요";

        /// <summary>보드 화면 마운트 칸 위 안내 — ⚠️ 가정(#182).</summary>
        public const string BoardHint = "마운트를 탭한 뒤 강화를 누르세요";

        /// <summary>
        /// **안내가 서는가** — S3 파밍(#188) · 아직 강화 안 함 · **재료와 고철이 둘 다 참**(🗑️ B 해금 조건).
        /// ✅ #198 — 「튜토리얼 마침」 조건 걷음(인자는 남김 · 안 씀): 바로가기 S3 점프도 막이 선다. S3 파밍은 튜토리얼을 마치거나 점프해야만 닿는다.
        /// 모자라면 막을 안 띄운다(갇힘 방지 · 골드 부족 때 해금 막과 같은 결). 저장 장부 신설 0 —
        /// 끝은 강화 표식이 판정한다. 바로가기 S4+ 점프는 #162 로 표식이 켜져 있어 안 선다.
        /// </summary>
        public static bool TutorialActive(bool tutorialCleared, int stageNumber, bool isFarming,
                                          bool enhanced, double material, double cost, double scrap, double scrapCost) =>
            isFarming
            && stageNumber == StageMapName.NumberOf(TutorialStageId)
            && CanEnhance(enhanced, material, cost, scrap, scrapCost);

        // ── #195 로봇 B 마운트 강화(2026-10-19 사용자 확정) ──────────

        /// <summary>B 강화 안내가 서는 판 — S6 파밍(주제 「자유 플레이 · 보스」 · 안내는 이것 하나).</summary>
        public const string TutorialStageIdB = "S6";

        /// <summary>바로가기 점프로 B 표식을 켜는 판 — S6 부터(A · B 둘 다 자동).</summary>
        public const int JumpEnhanceBFromStage = 7;   // #198 — 🗑️ 구 6(#195 「S6 점프 = A·B 자동」) · B 는 점프에서 자동 안 켬(재료 지급 → B 막에서 직접)

        /// <summary>이 판 번호로 점프하면 B 강화 표식을 켜는가.</summary>
        public static bool JumpTurnsOnB(int stageNumber) => stageNumber >= JumpEnhanceBFromStage;

        /// <summary>전투 화면 문구(B) — ⚠️ 가정(#195).</summary>
        public const string CombatPromptB = "조립에서 로봇 B 마운트를 탭해 강화해보세요";

        /// <summary>보드 화면 안내(B) — ⚠️ 가정(#195 · 플랜 문안).</summary>
        public const string BoardHintB = "로봇 B 마운트를 탭한 뒤 강화를 누르세요";

        /// <summary>
        /// B 안내가 서는가 — 튜토리얼 마침 · S6 파밍 · B 해금 · B 미강화 · 재료와 고철 둘 다(모자라면 막 없음).
        /// </summary>
        public static bool TutorialActiveB(bool tutorialCleared, int stageNumber, bool isFarming, bool robotBUnlocked,
                                           bool enhancedB, double material, double cost, double scrap, double scrapCost) =>
            isFarming && robotBUnlocked   // #198 — 튜토리얼 마침 조건 걷음(A 와 같은 까닭)
            && stageNumber == StageMapName.NumberOf(TutorialStageIdB)
            && CanEnhance(enhancedB, material, cost, scrap, scrapCost);

        /// <summary>
        /// **#198 — 바로가기 점프 재료**: 그 판 앞 판들의 최초 통과 보상 합에서 이미 강화한(또는 점프가 자동 강화한) 마운트 몫을 뺀 값.
        /// 지갑을 이 값**까지만** 채운다(더하지 않는다 → 거듭 점프해도 중복 지급 없음 · 장부 신설 0). S3 = 63 · S4·S5 = 50 · S6 = 50(B 몫).
        /// </summary>
        public static double JumpMaterialTarget(double rewardsBefore, double cost, bool aEnhanced, bool bEnhanced) =>
            System.Math.Max(0d, rewardsBefore - (aEnhanced ? cost : 0d) - (bEnhanced ? cost : 0d));

        // ── #199 팝오버 레벨 · 배율 줄 + 단추 글자 ──────────
        /// <summary>「강화 Lv 1 · ×1.00」 / 「강화 Lv 2 · ×1.45」 — ⚠️ 꼴 가정(#199).</summary>
        public static string LevelLine(bool enhanced, float enhancedCoef) =>
            enhanced ? $"Lv 2 · 피해량 ×{enhancedCoef:0.00}" : "Lv 1 · 피해량 ×1.00";   // #202 — 🗑️ 「강화 Lv 2 · ×1.45」

        /// <summary>단추 글자 — 「강화 (재료 50 · 고철 300)」 / 강화 뒤 「강화 완료」(흐림) ⚠️ 가정(#199 · 🗑️ 「강화 (재료 전부 투입)」).</summary>
        public static string ButtonLabelWithCost(bool enhanced, double cost, double scrapCost) =>
            enhanced ? "강화 완료"
                     : scrapCost > 0d ? $"강화 (재료 {cost:0} · 고철 {scrapCost:0})" : $"강화 (재료 {cost:0})";

        /// <summary>마운트계수 — **표식 ? 강화 : 기본**. 러너와 하네스가 같은 문을 지난다.</summary>
        public static float MountCoef(bool enhanced, float baseCoef, float enhancedCoef) =>
            enhanced ? enhancedCoef : baseCoef;
    }
}
