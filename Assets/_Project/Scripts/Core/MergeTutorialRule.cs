namespace MBI.Core
{
    /// <summary>
    /// **S4 합체 강제 버튼** (사용자 확정 #189 · 2026-10-19 · 기획서 수준 전체판).
    ///
    /// S4 첫 도전에서 합체 게이지가 차는 순간(#160 3초) 막 + 합체 원형만 밝음 + 문구.
    /// 누르면(합체 발동) 저절로 풀린다 — 게이지가 「준비」가 아니게 되기 때문이다.
    /// ⚠️ 저장 장부 신설 0 — 「한 번」은 조건이 든다: S4 미통과 · B 해금 · 게이지 만충 · 이 판 합체 미사용.
    /// T3 · 해금 막과 같은 꼴 — 매 프레임 다시 판단하고(전투 러너가 신호로 씀), 「▲ 전투로」는 예외.
    /// </summary>
    public static class MergeTutorialRule
    {
        /// <summary>안내가 서는 판 — S4(주제 「로봇 B 해금 · 합체」 · STAGE_DATA 4행).</summary>
        public const string StageId = "S4";

        /// <summary>문구 — ⚠️ 가정(#189 · 사용자가 바꿀 수 있다).</summary>
        public const string Prompt = "합체 버튼을 눌러 합체해보세요";

        /// <summary>
        /// 막이 서는가 — 튜토리얼 마침 · S4 **도전 층** · S4 미통과 · 로봇 B 해금 · 합체 준비(만충 · 미사용 · 진행 중 아님).
        /// </summary>
        public static bool IsActive(bool tutorialCleared, int stageNumber, bool isFarming,
                                    bool stageCleared, bool robotBUnlocked, bool mergeReady) =>
            tutorialCleared && !isFarming && !stageCleared && robotBUnlocked && mergeReady
            && stageNumber == StageMapName.NumberOf(StageId);
    }
}
