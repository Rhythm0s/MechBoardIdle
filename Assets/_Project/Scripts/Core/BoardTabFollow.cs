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

        /// <summary>
        /// ✅ **#205 ① — 튜토리얼 막 동안 탭 고정**(CID_204 · 플랜 판단 ⚠️ · 2026-10-19).
        /// 막이 선 동안에는 교대 · 손 어느 쪽도 탭을 못 옮긴다 — 대상 판(S5 폭발탄 · S3 강화 = A · S6 B 강화 = B)에 묶는다.
        /// 8133 실측: Auto 교대마다 B 판으로 따라가 안내가 꺼지고 B 판 탭이 보통 조작(모듈 붙음 · 고철 −30)으로 샜다.
        /// 막이 걷히면 null → 종전 따라가기(<see cref="ShouldFollow"/>) 그대로. Auto 교대 자체는 안 건드린다.
        /// </summary>
        public static MountOwner? TutorialPin(bool explosiveActive, bool enhanceA, bool enhanceB)
        {
            if (explosiveActive || enhanceA) return MountOwner.RobotA;
            if (enhanceB) return MountOwner.RobotB;
            return null;
        }
    }
}
