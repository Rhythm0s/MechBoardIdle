namespace MBI.Core
{
    /// <summary>
    /// **T3 의 어둠막과 안내 문구 — 언제 같이 뜨는가** (2026-10-14 사용자 확정 #130).
    ///
    /// 사용자 육안이 「파밍에서 물류 보드 버튼이 Disable — 버그」로 읽었다. 잠금은 설계대로였다
    /// (#115 ⓒ · 「도전」만 열고 「조립」·「스테이지」를 닫는다). 그런데 화면이 **잠긴 까닭을
    /// 말하지 않았다** — 단추가 흐리기만 했다. 그래서 잠금 범위는 그대로 두고
    /// **「도전」 하나만 남기고 덮는 막**과 **문구 한 줄**을 더한다.
    ///
    /// ⚠️⚠️ **막과 문구는 한 몸이다.** 둘을 따로 켜고 끄면 「어두운데 무엇을 하라는지 모른다」
    /// 또는 「하라고 하는데 어디가 빛나는지 모른다」가 된다. 그래서 둘 다 <see cref="ShouldShow"/>
    /// 하나만 본다.
    ///
    /// ⚠️ **끝 조건·잠금 범위는 안 바꾼다.** 이 규칙은 <see cref="ChallengeTutorialRule"/> 이
    /// 켠 T3 를 **받아서 보여 줄지만** 가린다 — T3 가 꺼지면(S1 통과) 막도 문구도 같이 사라진다.
    /// </summary>
    public static class ChallengeTutorialOverlayRule
    {
        /// <summary>안내 문구 — **사용자 확정 글자 그대로**(#130). 고치지 않는다.</summary>
        public const string Prompt = "도전 버튼을 눌러 도전해보세요";

        /// <param name="t3Active">T3 국면이 서 있는가(<c>TutorialSignals.HighlightChallengeButton</c>).</param>
        /// <param name="boardViewOpen">조립 화면이 열려 있는가.</param>
        /// <param name="settingsOpen">설정 패널이 열려 있는가.</param>
        /// <param name="menuOpen">메인 메뉴가 덮고 있는가.</param>
        public static bool ShouldShow(bool t3Active, bool boardViewOpen, bool settingsOpen, bool menuOpen)
        {
            if (!t3Active) return false;

            // ⚠️ **조립 화면에서는 안 깐다**(가정 — 사용자 확정 「▲전투로 예외는 그대로 보이되
            //    DIM 위인지 아래인지는 네 판단」). 「도전」 단추는 **전투 화면에만** 그려진다 —
            //    조립 화면에 막을 깔면 남길 구멍이 없어 **화면이 통째로 어두워지고**, 그 위의
            //    「▲전투로」(T3 에서도 열린 유일한 길)까지 어둠에 묻힌다. 막을 안 까는 쪽이
            //    「▲전투로는 언제나 보인다」를 가장 단순하게 지킨다.
            if (boardViewOpen) return false;

            // 설정·메뉴는 **제 막을 따로 깐다**(설정 = 판 둘레 어둠 · 메뉴 = 전체 배경).
            // 그 위에 이 막이 또 덮이면 두 겹으로 어두워지고, 무엇보다 **설정 판이 흐려져 못 읽는다.**
            if (settingsOpen || menuOpen) return false;

            return true;
        }
    }
}
