namespace MBI.Core
{
    /// <summary>
    /// **로봇 B 는 언제 열리는가** (2026-10-13 신설 · 사용자 확정 · §74-3 #84).
    ///
    /// 규정은 하나다 — **S4 를 넘기면 열린다.** 그 전까지는 둘째 로봇이 없는 것과 같으므로
    /// **교대(태그)도 B 판 편집도 안 된다.**
    ///
    /// ⚠️⚠️ **왜 신호를 따로 두는가.** 이 답을 묻는 곳이 셋이다 — 조립 화면의 로봇 탭,
    /// 전투 화면의 태그 버튼, 자동 교대. 셋이 각자 「S4 를 깼나」를 세면 한 곳만 고쳐지는
    /// 날이 온다(이 리포에서 되풀이된 결함 종류다 · 지침 §7). **답은 여기 하나다.**
    ///
    /// 📌 **두 가지가 답을 만든다.**
    /// · <see cref="S4Cleared"/> — 저장에 남은 클리어 기록. 방치 런타임이 넣는다.
    /// · <see cref="CurrentStageNumber"/> — 지금 서 있는 판의 번호. 러너가 넣는다.
    ///
    /// ⚠️ **심사자가 S5 로 뛰면 열린 것으로 본다**(⚠️ 가정 · 설계 역기입 자리).
    /// 바로가기로 S5·S6 에 들어갔는데 로봇이 하나뿐이면 **그 판이 성립하지 않는다** —
    /// 거기까지 간 사람은 B 를 가진 것으로 치는 편이 판을 보여 주는 쪽에 맞다.
    /// 규정이 서면 <see cref="Unlocked"/> 한 줄만 바뀐다.
    /// </summary>
    public static class RobotBGate
    {
        /// <summary>B 를 여는 판. ⚠️ 이 글자 하나가 규정이다 — 숫자를 다른 곳에 안 적는다.</summary>
        public const string UnlockStageId = "S4";

        /// <summary>저장에 S4 클리어가 남아 있는가. 방치 런타임이 넣는다.</summary>
        public static bool S4Cleared;

        /// <summary>지금 판의 번호(여섯에 안 들면 0). 러너가 넣는다.</summary>
        public static int CurrentStageNumber;

        /// <summary>지금 로봇 B 가 열려 있는가.</summary>
        public static bool IsUnlocked => Unlocked(S4Cleared, CurrentStageNumber);

        /// <summary>
        /// 판정 한 줄 — 시험이 이것을 본다.
        ///
        /// ⚠️ **번호를 여기서 안 적는다** — <see cref="UnlockStageId"/> 를 읽어 센다.
        /// 「4」를 따로 적으면 여는 판이 바뀔 때 한쪽만 고쳐진다.
        /// </summary>
        public static bool Unlocked(bool s4Cleared, int currentStageNumber) =>
            s4Cleared || currentStageNumber > StageMapName.NumberOf(UnlockStageId);

        /// <summary>처음 상태로. 저장 초기화와 시험이 쓴다.</summary>
        public static void Reset()
        {
            S4Cleared = false;
            CurrentStageNumber = 0;
        }
    }
}
