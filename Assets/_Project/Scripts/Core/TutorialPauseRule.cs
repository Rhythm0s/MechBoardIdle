namespace MBI.Core
{
    /// <summary>
    /// **튜토리얼 막이 선 동안 전투 일시 정지** (사용자 확정 #203 · 2026-10-19 · 「S1 · S3 · S6 로 결정」).
    ///
    /// 막이 떠 있는 동안 Auto 태그 · 적 · 리젠 시계가 돌아 「강제로 태그가 된다」는 육안. 세 막만 세운다 —
    /// S1 T3(「도전」 구멍 · 안내 창까지) · S3 A 강화 막 · S6 B 강화 막(전투 화면 막 + 보드 안내 동안).
    /// 🗑️ 「S2 T4 · S4 해금 · S4 합체(도전 층) · S5 폭발탄은 **안 멈춘다**(사용자 범위)」 — #207 확장(23:4x)으로 뒤집힘.
    ///
    /// ✅ **#207 확장 — 규칙 통일**(사용자 확정 23:4x 「권고안 ⓐ로 · 규칙 통일」): 파밍 층 튜토리얼 국면 **전부** —
    /// S1 T3 · S2 T4(막 + 보드 고스트) · S3 강화 · S4 해금 막 · S5 폭발탄(알림 · 막 + 보드 걸음 ①~⑤ 끝까지) · S6 B 강화 —
    /// 막 또는 보드 안내가 살아 있는 동안 정지 + 스폰 보류(첫 묶음은 국면이 끝난 뒤 #206 ×1.3 띠 · `FarmFirstSpawnRule`).
    /// 도전 층 S4 합체 막 = **정지만**(전원 스폰 뒤라 보류 무의미 · 남은 시간도 멈춤 · 합체 누르면 재개).
    /// ⚠️ 멈추는 것은 **전투 시뮬**(적 · 발사 · 태그 · 게이지 · 리젠 · 도전 시계)이고 물류 보드 틱은 계속 돈다(⚠️ 플랜 가정).
    /// 막 판단(신호)은 계속 돈다 — 안 돌면 강화해도 막이 안 꺼진다.
    /// </summary>
    public static class TutorialPauseRule
    {
        /// <summary>#207 확장 — 국면 신호 묶음. 하나라도 참이면 정지.</summary>
        public static bool ShouldPause(bool t3Challenge, bool t4Module, bool unlockRobotB, bool enhanceA,
                                       bool explosive, bool enhanceB, bool mergePrompt) =>
            t3Challenge || t4Module || unlockRobotB || enhanceA || explosive || enhanceB || mergePrompt;

        /// <summary>🗑️ #203 세 신호 판 — 시험 호환으로 남김(부르는 곳 = 시험만).</summary>
        public static bool ShouldPause(bool t3Challenge, bool enhanceA, bool enhanceB) =>
            ShouldPause(t3Challenge, false, false, enhanceA, false, enhanceB, false);
    }
}
