namespace MBI.Core
{
    /// <summary>
    /// **배지 카드 셋째 줄이 무엇을 말하는가** — 순수 규칙 (2026-10-13).
    ///
    /// 📌 **왜 규칙으로 뺐는가.** 이 줄은 **세 가지 다른 것**을 말한다 —
    /// 파밍이면 「언제 또 차는가」, 도전이면 「얼마 남았나」, 튜토리얼이면 **아무것도**다.
    /// 화면 쪽에서 `if` 둘을 겹쳐 쓰다가 셋째 경우를 빠뜨렸고, 그래서
    /// 튜토리얼 판에 **「남은 몬스터 0 · 남은 시간 166:39」**가 떴다(2026-10-13 실측).
    ///
    /// ⚠️⚠️ **166:39 는 지어낸 수가 아니다 — 9999 초다.** 튜토리얼 판
    /// (`Stage_S0.asset`)의 `challengeTime` 이 9999 이고, 그것이 「사실상 제한 없음」의
    /// 표식인데 화면이 그 수를 **제한시간으로 믿고** mm:ss 로 적었다.
    /// 수를 고치는 게 아니라 **그 줄을 안 그리는 것**이 맞다 —
    /// 튜토리얼 전용 판은 **요구치·보상·파밍·오프라인 넷 다 없다**(스테이지 기획서 3장).
    /// 없는 것에 시계를 달면 시계가 거짓말을 한다.
    /// </summary>
    public static class StageBadgeRule
    {
        /// <summary>배지 셋째 줄의 성격.</summary>
        public enum StatusKind
        {
            /// <summary>안 그린다 — 말할 것이 없는 판(튜토리얼 전용).</summary>
            None = 0,

            /// <summary>파밍 층 — 「리젠 mm:ss · 맵 n/정원」.</summary>
            Farm = 1,

            /// <summary>도전 층 — 「남은 몬스터 n · 남은 시간 mm:ss」.</summary>
            Challenge = 2,
        }

        /// <summary>
        /// 판 번호와 층으로 셋째 줄의 성격을 정한다.
        ///
        /// ⚠️ **번호가 0 이하면 여섯에 안 드는 판**이다(튜토리얼 전용 ·
        /// <see cref="StageMapName.NumberOf"/> 가 0 을 준다). 그 판에는 층이 없으므로
        /// `isFarming` 이 무엇이든 <see cref="StatusKind.None"/> 이다.
        /// </summary>
        public static StatusKind StatusOf(int stageNumber, bool isFarming)
        {
            if (stageNumber <= 0) return StatusKind.None;
            return isFarming ? StatusKind.Farm : StatusKind.Challenge;
        }

        /// <summary>
        /// 제한시간을 **시계로 적어도 되는 수**인가.
        ///
        /// ⚠️ 0 이하는 「제한 없음」이고, <paramref name="limitSeconds"/> 가
        /// <see cref="NoLimitSentinel"/> 이상이면 그것도 「제한 없음」의 표식이다 —
        /// 적으면 166:39 같은 뜻 없는 수가 뜬다.
        /// </summary>
        public static bool ClockIsMeaningful(float limitSeconds)
        {
            return limitSeconds > 0f && limitSeconds < NoLimitSentinel;
        }

        /// <summary>
        /// 「사실상 제한 없음」의 표식 — 자산이 9999 를 쓴다(`Stage_S0.asset`).
        ///
        /// ⚠️ 이 수는 **자산에서 읽은 것**이다(지어내지 않았다). 한 시간이 3600 초이므로
        /// 9999 는 두 시간 반이고, 도전 판의 제한시간 120 초와 견주면 비교가 안 된다 —
        /// 그래서 그 위를 전부 「없음」으로 본다. 경계를 3600 으로 둔 까닭은
        /// **한 판이 한 시간을 넘는 설계가 없다**는 것뿐이다(가정 · 설계 역기입 자리).
        /// </summary>
        public const float NoLimitSentinel = 3600f;
    }
}
