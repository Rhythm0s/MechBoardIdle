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
    }
}
