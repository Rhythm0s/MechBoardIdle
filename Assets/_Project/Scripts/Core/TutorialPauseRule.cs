namespace MBI.Core
{
    /// <summary>
    /// **튜토리얼 막이 선 동안 전투 일시 정지** (사용자 확정 #203 · 2026-10-19 · 「S1 · S3 · S6 로 결정」).
    ///
    /// 막이 떠 있는 동안 Auto 태그 · 적 · 리젠 시계가 돌아 「강제로 태그가 된다」는 육안. 세 막만 세운다 —
    /// S1 T3(「도전」 구멍 · 안내 창까지) · S3 A 강화 막 · S6 B 강화 막(전투 화면 막 + 보드 안내 동안).
    /// S2 T4 · S4 해금 · S4 합체(도전 층) · S5 폭발탄은 **안 멈춘다**(사용자 범위).
    /// ⚠️ 멈추는 것은 **전투 시뮬**(적 · 발사 · 태그 · 게이지 · 리젠)이고 물류 보드 틱은 계속 돈다(⚠️ 플랜 가정).
    /// 막 판단(신호)은 계속 돈다 — 안 돌면 강화해도 막이 안 꺼진다.
    /// </summary>
    public static class TutorialPauseRule
    {
        public static bool ShouldPause(bool t3Challenge, bool enhanceA, bool enhanceB) =>
            t3Challenge || enhanceA || enhanceB;
    }
}
