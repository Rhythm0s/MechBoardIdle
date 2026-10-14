namespace MBI.Core
{
    /// <summary>
    /// **한 스테이지의 두 층** (2026-10-13 사용자 확정 #111 · 스테이지 기획서 1·2장).
    ///
    /// · <see cref="Farm"/> — 상주 파밍. 들어가면 **여기부터** 선다. 승패가 없고 N초마다
    ///   정원까지 보충된다. 고철은 파밍 장부에 쌓이고 보드는 자유로이 고친다.
    /// · <see cref="Challenge"/> — 120초 도전. 파밍 화면의 「도전」 버튼으로 들어간다.
    ///   요구치·제한시간·보스가 여기 있고, 통과해야 다음 판이 열린다.
    ///
    /// 📌 **같은 스테이지의 두 얼굴**이지 다른 스테이지가 아니다 — 보드도 로봇도 그대로다.
    /// 갈리는 것은 **무엇이 나오고 · 무엇으로 끝나고 · 번 것을 어느 장부에 적는가** 셋이다.
    /// </summary>
    public enum StageLayer
    {
        /// <summary>상주 파밍 — 승패 없음 · N초 보충 · 파밍 장부.</summary>
        Farm = 0,

        /// <summary>120초 도전 — 요구치·제한시간 · 도전 장부 · 통과 시 해금.</summary>
        Challenge = 1,
    }

    /// <summary>
    /// 지금 어느 층에 서 있는가 — **한 곳에서만 답한다**(지침 §7).
    ///
    /// ⚠️ 화면·시뮬·장부가 저마다 「지금 파밍인가」를 세면 한 곳만 고쳐지는 날이 온다.
    /// 들어가고 나오는 길은 러너가 쥐고, 나머지는 **여기를 읽기만** 한다.
    /// </summary>
    public static class StageLayerSignals
    {
        /// <summary>지금 층. 스테이지에 들어가면 <see cref="StageLayer.Farm"/> 부터다.</summary>
        public static StageLayer Current = StageLayer.Farm;

        /// <summary>파밍 층인가 — 읽는 쪽이 enum 을 다시 안 비교하게.</summary>
        public static bool IsFarming => Current == StageLayer.Farm;

        /// <summary>
        /// 지금 판이 **튜토리얼 전용 판**인가 (2026-10-14 · 사용자 확정 ④).
        ///
        /// ⚠️ 그 판에는 층이 없다 — 파밍·도전이 안 갈린다. 그래서 「파밍이 아니다」만 보고
        /// 보드를 잠그면 **벨트 한 칸을 까는 수업에서 잠긴다**(튜토리얼이 멈춘다).
        /// 러너가 매 프레임 적고, 보드는 <see cref="BoardEditRule"/> 를 통해 읽는다.
        /// </summary>
        public static bool IsTutorialStage;

        /// <summary>
        /// 파밍 장부에 쌓인 고철 — **도전 고철과 섞지 않는다**(사용자 확정 #111).
        /// 섞으면 「파밍으로 얼마를 버나」를 영영 못 재고, 오프라인 시급 기록이 거짓이 된다.
        /// </summary>
        public static double FarmScrap;

        /// <summary>도전 장부에 쌓인 고철.</summary>
        public static double ChallengeScrap;

        /// <summary>
        /// **지금 상주 중인 파밍 판**의 식별자 — 오프라인 정산이 쓸 「끈 시점의 판」이다
        /// (2026-10-13 · 순서 6).
        ///
        /// ⚠️⚠️ **이 칸이 비어 있어서 오프라인 정산이 기록을 못 쓰고 있었다.** 저장에
        /// `lastFarmStageId` 칸은 처음부터 있었는데 **쓰는 쪽이 없었다** — 늘 빈 글자라
        /// `BestFarmRate("")` 가 0 을 주고, 정산이 매번 **기본 시급**으로 떨어졌다
        /// (2026-10-13 추적). 파밍 바퀴가 실제로 서는 자리에서 여기에 적는다.
        ///
        /// ⚠️ **도전에 들어가도 지우지 않는다** — 도전은 잠깐 다녀오는 곳이고,
        /// 상주 판은 그대로다. 비우는 것은 <see cref="Reset"/> 뿐이다.
        ///
        /// ⚠️ **파밍 바퀴가 실제로 선 판만** 적는다. 표가 비어 바퀴가 안 서는 판을 적으면
        /// 꺼둔 동안 **한 번도 번 적 없는 판**으로 정산하게 된다.
        /// </summary>
        public static string FarmStageId = string.Empty;

        /// <summary>번 것을 지금 층의 장부에 적는다.</summary>
        public static void Credit(double scrap)
        {
            if (scrap <= 0d) return;
            if (IsFarming) FarmScrap += scrap;
            else ChallengeScrap += scrap;
        }

        /// <summary>처음 상태로 — 저장 초기화와 시험이 쓴다.</summary>
        public static void Reset()
        {
            Current = StageLayer.Farm;
            FarmScrap = 0d;
            ChallengeScrap = 0d;
            FarmStageId = string.Empty;
        }
    }
}
