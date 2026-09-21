namespace MBI.Core
{
    /// <summary>
    /// 노드 상태 표시(UI 문서「노드 상태 표시」· 260825_V02 §1 개정).
    ///
    /// **두 정보가 서로 다른 채널에 실린다:**
    ///   노드 종류 = **색상** — 아트 자체의 색이다. 코드는 칠하지 않는다.
    ///   산출률   = **밝기** — 스프라이트 틴트 곱셈 배율.
    ///
    /// 왜 채도가 빠졌는가: `SpriteRenderer.color`는 곱셈이라 어둡게만 할 수 있고
    /// **채도를 낮출 수 없다.** 색을 가진 아트에 회색을 곱하면 명도만 내려간다.
    /// 채도까지 축으로 쓰려면 셰이더가 필요해 배칭이 깨지므로, 채도를 빼고 밝기만 쓴다
    /// (2026-08-25 확정 — UI 문서에 반영됨).
    ///
    /// 어둡게 하면 사람 눈에는 탁해 보이므로, 「밝고 선명함 / 중간 / 어둡고 탁함」이라는
    /// 문서의 인상은 명도 조절만으로 대체로 재현된다.
    ///
    /// ⚠️ 종류별 배색을 코드가 정하지 않는 것이 이 개정의 핵심이다. 한 노드에 색 축이 둘 겹치면
    /// 빨간 노드가 「정지」인지 「군수 노드」인지 구분되지 않아 진단 체계가 무너진다.
    /// </summary>
    public static class NodeStatusTint
    {
        /// <summary>정상 — 원본 그대로.</summary>
        public const float Normal = 1.0f;
        /// <summary>감속·유휴 — 0 초과 1.0 미만.</summary>
        public const float Slow = 0.7f;
        /// <summary>정지 — 산출률 0.</summary>
        public const float Stopped = 0.4f;

        /// <summary>
        /// 산출률(actualRate ÷ targetRate) → 틴트 곱셈 배율.
        /// 3단계다 — 4번째 단계는 없다(모듈 과부하는 MVP 밖).
        /// </summary>
        public static float Of(float ratio)
        {
            if (ratio <= 0.0001f) return Stopped;  // 완전 정지
            if (ratio < 0.999f) return Slow;       // 깎여서 돌아감
            return Normal;                         // 설계대로
        }

        /// <summary>
        /// 되돌아가는 문턱 — **정상에서 감속으로 내려오려면 여기까지 떨어져야 한다.**
        /// ⚠️ 0.97 은 가정이다(설계 역기입 자리).
        /// </summary>
        public const float NormalExit = 0.97f;

        /// <summary>정지에서 감속으로 올라오는 문턱. ⚠️ 가정.</summary>
        public const float StoppedExit = 0.02f;

        /// <summary>
        /// **새 단계가 버텨야 하는 시간**(초) — 이것이 깜빡임을 막는 것이다.
        ///
        /// ⚠️⚠️ **값의 이력만으로는 못 막는다**(2026-09-21 · 내 첫 고침이 안 먹은 까닭).
        /// 활성 로봇 판은 만든 것을 전투가 곧바로 먹고, 도착이 **이산**이라 어떤 틱은
        /// 0 개다 — 산출률이 **0 과 1 사이를 통째로** 오간다. 그러면 문턱을 0.97 로 두든
        /// 0.5 로 두든 **어느 문턱이든 넘는다.**
        ///
        /// 📌 막아야 할 것은 **얼마나 떨어졌나**가 아니라 **얼마나 자주 바뀌나**다.
        /// ⚠️ 0.35 는 가정이다 — 짧으면 여전히 떨고, 길면 진짜 정지가 늦게 보인다.
        /// </summary>
        public const float DwellSeconds = 0.35f;

        /// <summary>
        /// **직전 단계를 보고 정한다** (2026-09-21 사용자 육안 — 「작동 중인 노드가 깜빡인다」).
        ///
        /// ⚠️⚠️ **문턱 하나로는 떨림을 못 막는다.** 설계대로 도는 노드도 산출률이
        /// **1.000 언저리에서 미세하게 떨린다** — 벨트 도착이 이산이라 틱마다 조금씩
        /// 다르다. <see cref="Of(float)"/> 는 0.999 에서 딱 잘리므로 그 떨림이
        /// **1.0 ↔ 0.7 밝기 튐**이 되어 화면에서 깜빡임으로 보였다.
        ///
        /// 📌 **이 리포에 같은 병을 고친 자리가 있다** — `DirectionHysteresis`
        /// (「대각에서 얼굴이 빠르게 뒤집힌다」). 올라가는 문턱과 내려오는 문턱을
        /// 벌려 두면 경계에서 떨어도 단계가 안 바뀐다.
        ///
        /// ⚠️ **올라가는 문턱은 그대로다**(0.999) — 느슨하게 하면 「깎여 도는데 정상」이
        /// 되어 진단이 거짓말을 한다. 벌리는 것은 **내려오는 쪽**뿐이다.
        /// </summary>
        public static float Of(float ratio, float previous)
        {
            if (ratio <= 0.0001f) return Stopped;              // 완전 정지는 문턱이 없다

            // 정지에서 올라올 때 — 아주 조금이라도 돌면 감속으로 올린다.
            if (previous <= Stopped + 0.0001f)
                return ratio >= StoppedExit ? Slow : Stopped;

            // 정상이었으면 **많이 떨어져야** 감속으로 내려온다.
            if (previous >= Normal - 0.0001f)
                return ratio >= NormalExit ? Normal : Slow;

            // 감속이었으면 올라가는 문턱은 종전 그대로다.
            return ratio >= 0.999f ? Normal : Slow;
        }
    }
}
