namespace MBI.Core
{
    /// <summary>
    /// **도전 진입 시 마운트 A 만재**(사용자 확정 #223 · 2026-10-20 · 「S4 · S5 · S6 전부 · 다시 도전도」 · ⓐ 플랜 판단 = A 만 · B 그대로).
    /// 🗑️ 첫 판 「A·B 둘 다」 — B 가득 + 즉시 합체에서 S5 · S6 시간 초과.
    /// S1~S3 는 제외(「지금 실린 물류를 그대로 가지고」 그대로). 채우는 길은 바로가기 #156 의 A 몫과 같다(`StageRunner.FillMountAForChallenge`).
    /// 합체 게이지는 안 건드린다(#160 3초 만충이 따로 있다).
    /// </summary>
    public static class ChallengeFillRule
    {
        public const int FirstStage = 4;
        public const int LastStage = 6;

        public static bool FillsOnEntry(int stageNumber) => stageNumber >= FirstStage && stageNumber <= LastStage;
    }
}
