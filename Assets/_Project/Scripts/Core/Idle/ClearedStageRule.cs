namespace MBI.Core
{
    /// <summary>
    /// **통과한 판은 다시 도전하지 않는다** (사용자 확정 #193 · 2026-10-19) + **최초 통과 보상 한 줄** (#194).
    ///
    /// · 통과 기록(`IdleSignals.ClearedStageIds`)에 있는 판 = 파밍은 그대로 들어가고(목록 · 바로가기)
    ///   그 판의 「도전」 단추만 「통과함」으로 흐려진다.
    /// · 결과 화면 — 승리면 [다시 도전] 을 숨기고, 실패면 그대로 둔다.
    /// · 바로가기 점프는 통과 기록을 안 남기므로 그 판은 도전할 수 있다(심사자 길 불변).
    /// · 튜토리얼 T3 는 S1 미통과라 무관하다.
    /// ⚠️ 저장 장부 신설 0 — 통과 기록 하나가 판정한다.
    /// </summary>
    public static class ClearedStageRule
    {
        /// <summary>도전 단추 글자 — 통과한 판이면 「통과함」(⚠️ 문구 = 플랜 지시 그대로).</summary>
        public const string ClearedLabel = "통과함";

        /// <summary>도전 단추 평상 글자.</summary>
        public const string ChallengeLabel = "도전";

        /// <summary>도전 단추가 눌리는가.</summary>
        public static bool CanChallenge(bool cleared) => !cleared;

        /// <summary>도전 단추 글자.</summary>
        public static string ButtonLabel(bool cleared) => cleared ? ClearedLabel : ChallengeLabel;

        /// <summary>결과 화면에 [다시 도전] 을 두는가 — 승리면 숨긴다(#193).</summary>
        public static bool ShowsRetry(bool won) => !won;

        /// <summary>
        /// #194 — 최초 통과 결과 화면 한 줄 「강화 재료 +N」(⚠️ 꼴 가정). 0 이면 안 쓴다(null).
        /// 재통과 재지급 없음 그대로 — 부르는 쪽이 「이번이 최초인가」를 넣는다.
        /// </summary>
        public static string RewardLine(bool firstClear, float reward) =>
            firstClear && reward > 0f ? "강화 재료 +" + UnityEngine.Mathf.RoundToInt(reward) : null;
    }
}
