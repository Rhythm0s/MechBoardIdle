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
        /// 파밍 장부에 쌓인 고철 — **도전 고철과 섞지 않는다**(사용자 확정 #111).
        /// 섞으면 「파밍으로 얼마를 버나」를 영영 못 재고, 오프라인 시급 기록이 거짓이 된다.
        /// </summary>
        public static double FarmScrap;

        /// <summary>도전 장부에 쌓인 고철.</summary>
        public static double ChallengeScrap;

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
        }
    }
}
