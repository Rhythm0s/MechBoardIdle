namespace MBI.Core
{
    /// <summary>
    /// 우하단 **마일스톤 카드** (2026-09-18 설계 지시 ④ · 쿠키런 크럼블 문법).
    ///
    /// 담는 것은 **튜토리얼 목표 둘**이다 — 「끊긴 자리를 잇는다」·「마운트가 가득 찬다」.
    /// 판정은 <see cref="Stage0Goal"/> 하나가 이미 갖고 있으므로 **여기서 다시 세지 않는다**
    /// (지침 §7). 이 파일이 아는 것은 **보상을 받았는가** 하나뿐이다.
    ///
    /// ⚠️ **보상액은 가정이다** — 문서에 마일스톤 보상 절이 없다(설계 역기입 자리).
    /// ⚠️ **한 번만 받는다.** 받은 뒤에는 버튼이 사라지고 카드는 「받음」으로 남는다 —
    /// 되풀이 지급은 닫힌 곡선을 무너뜨린다(`IdleRuntime.CreditSignals` 의 최초 클리어와 같은 이유).
    /// </summary>
    public sealed class MilestoneCard
    {
        /// <summary>⚠️ 가정 — 달성 보상 골드.</summary>
        public const int DefaultGoldReward = 20;

        /// <summary>⚠️ 가정 — 달성 보상 고철.</summary>
        public const int DefaultScrapReward = 50;

        /// <summary>
        /// 목표 ② 옆 「 (지금/가득)」 (2026-10-15 사용자 확정 #145 · 꼴은 구현 가정).
        /// 걸쇠가 걸리면 가득 수로 멈춘다. 용량이 0 이면 빈 글.
        /// </summary>
        public static string MountProgressText(float now, float cap, bool filled)
        {
            if (cap <= 0f) return string.Empty;
            int c = UnityEngine.Mathf.RoundToInt(cap);
            int n = filled ? c : UnityEngine.Mathf.Clamp(UnityEngine.Mathf.FloorToInt(now), 0, c);
            return " (" + n + "/" + c + ")";
        }

        /// <summary>보상을 이미 받았는가.</summary>
        public bool Claimed { get; private set; }

        /// <summary>지금 받을 수 있는가 — **달성했고 아직 안 받았을 때만.**</summary>
        public bool CanClaim(bool complete) => complete && !Claimed;

        /// <summary>
        /// 받는다. 못 받을 상태면 <c>false</c> 이고 **아무것도 안 바뀐다** —
        /// 부르는 쪽이 조건을 두 번 검사하지 않아도 되게 한다.
        /// </summary>
        public bool TryClaim(bool complete)
        {
            if (!CanClaim(complete)) return false;
            Claimed = true;
            return true;
        }

        public void Reset() => Claimed = false;
    }
}
