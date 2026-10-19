namespace MBI.Core
{
    /// <summary>
    /// **로봇 B 는 언제 열리는가** (2026-10-13 신설 · §74-3 #84 → **2026-10-19 사용자 확정 #173 으로 개정**).
    ///
    /// ✅ **규정 — 골드로 해금한다.** S4 파밍부터 태그 원형 자리에 「로봇 B 해금 (골드 50)」 단추가 서고,
    /// 누르면 골드를 쓰고 열린다(저장 칸 `robotBUnlocked`). 그 전까지는 둘째 로봇이 없는 것과 같으므로
    /// **교대(태그)도 B 판 편집도 안 된다.**
    /// 🗑️ 구 규정 「S4 를 넘기면 열린다」(#84) 폐기 — **S4 통과만으로는 안 열린다.**
    ///
    /// ✅ **바로가기 S5 · S6 점프는 열린 것으로 본다**(#103 · 사용자 확정 2026-10-13 — #173 에서도 남김).
    /// 거기까지 뛴 심사자는 B 를 가진 것으로 친다. ⚠️ 이제 **바로가기 점프에만** 해당한다 —
    /// 자연 진행으로 S5 에 와도 산 적이 없으면 잠겨 있다.
    ///
    /// ⚠️⚠️ **왜 신호를 따로 두는가.** 이 답을 묻는 곳이 셋이다 — 조립 화면의 로봇 탭,
    /// 전투 화면의 태그 버튼, 자동 교대. 셋이 각자 세면 한 곳만 고쳐지는 날이 온다(지침 §7). **답은 여기 하나다.**
    /// </summary>
    public static class RobotBGate
    {
        /// <summary>해금 단추가 서기 시작하는 판(S4 파밍부터). ⚠️ 이 글자 하나가 규정이다.</summary>
        public const string UnlockStageId = "S4";

        /// <summary>골드로 해금했는가 — 저장 칸에서 방치 런타임이 넣는다(#173).</summary>
        public static bool Purchased;

        /// <summary>바로가기로 S5 · S6 에 뛰었는가 — 바로가기가 넣는다(#103 · 점프 전용).</summary>
        public static bool JumpUnlocked;

        /// <summary>지금 판의 번호(여섯에 안 들면 0). 러너가 넣는다 — 해금 단추 · 튜토리얼 막이 읽는다.</summary>
        public static int CurrentStageNumber;

        /// <summary>지금 로봇 B 가 열려 있는가.</summary>
        public static bool IsUnlocked => Unlocked(Purchased, JumpUnlocked);

        /// <summary>판정 한 줄 — 시험이 이것을 본다.</summary>
        public static bool Unlocked(bool purchased, bool jumpUnlocked) => purchased || jumpUnlocked;

        /// <summary>이 판 번호로 바로가기 점프하면 B 를 여는가 — S5 · S6(해금 판 다음부터 · #103).</summary>
        public static bool JumpOpens(int stageNumber) =>
            stageNumber > StageMapName.NumberOf(UnlockStageId);

        // ── 해금 단추 · 튜토리얼 막 (#173) ─────────────────────────────

        /// <summary>단추 글자 — ⚠️ 가정(#173). 값(골드 n)은 아이콘과 함께 따로 그린다.</summary>
        public const string ButtonLabel = "로봇 B 해금";

        /// <summary>S4 파밍 첫 진입 막 문구 — ⚠️ 가정(#173).</summary>
        public const string TutorialPrompt = "골드로 로봇 B를 해금해보세요";

        /// <summary>
        /// 태그 원형 자리에 해금 단추를 그리는가 — **S4 파밍부터 · 미해금**. 도전 층에서는 안 그린다
        /// (도전 중에 둘째 로봇이 생기면 들고 들어간 판이 바뀐다 · 구현 판단).
        /// </summary>
        public static bool ShowsUnlockButton(int stageNumber, bool isFarming, bool unlocked) =>
            !unlocked && isFarming && stageNumber >= StageMapName.NumberOf(UnlockStageId);

        /// <summary>단추가 눌리는가 — 골드가 값 이상.</summary>
        public static bool CanUnlock(double gold, double cost) => cost > 0d && gold >= cost - 1e-9;

        /// <summary>
        /// **S4 파밍 튜토리얼 막**을 까는가 — 튜토리얼 마침 · S4 · 파밍 · 미해금 · 골드 충분(#173).
        /// 골드가 모자라면 막을 안 깐다(단추만 흐림) — 못 누르는 단추로 화면을 잠그지 않는다.
        /// 단추를 누르면(해금) 저절로 꺼진다.
        /// </summary>
        public static bool TutorialActive(bool tutorialCleared, int stageNumber, bool isFarming,
                                          bool unlocked, double gold, double cost) =>
            tutorialCleared && isFarming && !unlocked
            && stageNumber == StageMapName.NumberOf(UnlockStageId)
            && CanUnlock(gold, cost);

        /// <summary>처음 상태로. 저장 초기화와 시험이 쓴다.</summary>
        public static void Reset()
        {
            Purchased = false;
            JumpUnlocked = false;
            CurrentStageNumber = 0;
        }
    }
}
