using MBI.Data;

namespace MBI.Core
{
    /// <summary>
    /// **편집 탭이 언제 활성 로봇을 따라가는가** (2026-09-16 · 사용자 육안 · 플랜 §74-21 ②).
    ///
    /// 🗑️ **구 가정 「편집 축과 전투 축은 따로」 폐기.** 09-16 오후에 내가 둘을 갈라
    /// 두었다 — 탭은 「무엇을 편집하는가」, 태그는 「누가 싸우는가」. 규칙으로는 깨끗한데
    /// **써 보니 틀렸다**: 로봇 B 로 교대해도 조립 화면은 A 판을 보여 주고, 플레이어는
    /// **지금 싸우는 로봇의 줄을 못 본다.** 사용자 판단으로 뒤집는다.
    ///
    /// 📌 **따라가되 손을 이기지 않는다.** 손으로 탭을 옮기면 그대로 두고,
    /// **다음 교대**나 **다음 조립 진입**에서 다시 활성 로봇으로 맞춘다 —
    /// 매 프레임 맞추면 손으로 B 를 열어 둔 채 A 로 싸울 수가 없다.
    /// </summary>
    public static class BoardTabFollow
    {
        /// <summary>
        /// 지금 편집 탭을 활성 로봇으로 맞춰야 하는가.
        ///
        /// 맞추는 때는 **둘뿐**이다 —
        /// · **교대가 일어났다**(활성 로봇이 직전에 본 것과 다르다)
        /// · **조립 화면에 막 들어왔다**(닫힘 → 열림)
        ///
        /// 그 밖에는 거짓이다. 특히 **이미 열려 있는 동안에는 안 맞춘다** —
        /// 그 사이가 손으로 탭을 고르는 시간이다.
        /// </summary>
        public static bool ShouldFollow(MountOwner active, MountOwner lastSeenActive,
            bool boardOpen, bool wasBoardOpen)
        {
            if (active != lastSeenActive) return true;       // 교대가 일어났다
            return boardOpen && !wasBoardOpen;               // 막 들어왔다
        }
    }
}
