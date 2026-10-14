namespace MBI.Core
{
    /// <summary>
    /// **방향 묻는 판이 떠 있을 때 누름 하나를 누가 받는가** (#93 · 2026-10-14 사용자 육안).
    ///
    /// ⚠️⚠️ **이 규칙이 없어서 패드가 안 눌렸다.** 패드는 자기 사각형을 UI 가림막에
    /// 등록한다(그 아래 칸에 노드가 놓이는 것을 막으려고). 그런데 보드는 누름을 받을 때
    /// 「UI 위면 무시」를 **물음 분기보다 먼저** 보고 있었다 — 그래서 **패드 안을 누르면
    /// 전부 버려지고**, 패드 **밖**을 누른 것만 물음에 닿아 「취소」가 되었다.
    /// 화면에서는 「눌러도 아무 일이 없고, 엉뚱한 데를 누르면 노드가 사라진다」로 보였다.
    ///
    /// 셋을 가린다 —
    ///   · 패드 **안** → 물음에 답한다. UI 가림막보다 **앞**이다(패드가 곧 그 UI다).
    ///   · 패드 밖이고 **다른 UI 위**(팔레트·나가기…) → 그 UI 것이다. 물음은 그대로 둔다.
    ///   · 패드 밖이고 빈 자리 → 물음에 답한다(= 바깥 누름 → 설치 취소).
    /// </summary>
    public enum DirectionAskPress
    {
        /// <summary>물음이 안 떠 있다 — 보드가 평소처럼 받는다.</summary>
        NotAsking,

        /// <summary>물음에 답한다.</summary>
        Answer,

        /// <summary>다른 UI 것이다 — 물음은 건드리지 않는다.</summary>
        OtherUi,
    }

    public static class DirectionAskInput
    {
        /// <param name="asking">방향을 묻는 중인가.</param>
        /// <param name="insidePad">누른 자리가 **패드 사각형 안**인가.</param>
        /// <param name="overUi">누른 자리가 UI 가림막에 걸리는가(패드 자신도 걸린다).</param>
        public static DirectionAskPress Route(bool asking, bool insidePad, bool overUi)
        {
            if (!asking) return DirectionAskPress.NotAsking;
            if (insidePad) return DirectionAskPress.Answer;
            if (overUi) return DirectionAskPress.OtherUi;
            return DirectionAskPress.Answer;
        }
    }
}
