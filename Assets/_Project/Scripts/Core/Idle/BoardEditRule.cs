namespace MBI.Core
{
    /// <summary>
    /// **보드를 고칠 수 있는 층인가** (2026-10-14 사용자 확정 ④
    /// 「도전 스테이지에서 보드 안 잠기는데?」 · 「들어가되 못 고친다」).
    ///
    /// 📌 **두 층의 일이 다르다.** 파밍은 **고치는 층**이고 도전은 **시험하는 층**이다.
    /// 도전 중에 벨트를 다시 깔 수 있으면 「무엇으로 이겼는가」가 흐려진다 — 진 판을
    /// 돌리는 중에 고쳐서 이기면 그 판이 무엇을 재는 판인지 알 수 없다.
    ///
    /// ⚠️ **보는 것은 막지 않는다**(사용자 확정 「들어가되 못 고친다」). 도전 중에도
    /// 보드에 들어가 흐름을 구경할 수 있다 — 막는 것은 **바꾸는 손**뿐이다.
    ///
    /// ⚠️ **튜토리얼 판은 예외다.** 그 판에는 층이 없고(파밍·도전이 안 갈린다),
    /// 거기서 못 고치면 **튜토리얼 자체가 안 돌아간다**(벨트 한 칸을 까는 수업이다).
    /// </summary>
    public static class BoardEditRule
    {
        /// <param name="isFarming">지금 파밍 층인가(<see cref="StageLayerSignals.IsFarming"/>).</param>
        /// <param name="isTutorialStage">지금 판이 튜토리얼 전용 판인가.</param>
        public static bool CanEdit(bool isFarming, bool isTutorialStage)
        {
            if (isTutorialStage) return true;
            return isFarming;
        }

        /// <summary>
        /// 도전 층에서 모드 단추가 대신 적는 글 — **사용자 확정 원문 그대로**(2026-10-15 #143). 고치지 않는다.
        /// </summary>
        public const string ChallengeModeLockedLabel = "도전 중 변경 불가";

        /// <summary>
        /// **모드 단추(조립 ↔ 이동)를 누를 수 있는가** (2026-10-15 사용자 확정 #143).
        ///
        /// 📌 위 <see cref="CanEdit"/> 의 화면 표현이다 — 도전 층에서는 바꾸는 손이 다 막혀 있는데
        ///    모드 단추만 살아 「조립 모드」로 바꿔도 아무것도 안 놓였다. 답은 같은 규칙이 낸다.
        /// </summary>
        public static bool CanToggleMode(bool isFarming, bool isTutorialStage) =>
            CanEdit(isFarming, isTutorialStage);
    }
}
