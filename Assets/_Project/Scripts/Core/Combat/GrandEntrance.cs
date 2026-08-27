namespace MBI.Core
{
    /// <summary>
    /// 태그 스킬(구 「등장 특공」/「과부하 특공」 — 260827_V03 §4 개명). 밸런스 문서「태그 시스템 수치」· 전투 시스템 문서 2-1장 · 9-1장).
    ///
    /// **발동 경로가 둘이다.** 태그 전용이 아니다 —
    ///   ① 태그 인 시점 — 대기 로봇 **마운트 만충**
    ///   ② 합체 발동 시점 — **마운트 만충**
    /// 구 「창고 100%」는 폐기됐다 — 판정 주체가 저장 노드에서 마운트로 옮겨졌다(V03 §2).
    /// ②는 신규 메커니즘이 아니라 본 항의 두 번째 발동 경로이며 **식이 동일하다**.
    /// 그래서 이 식은 TagSystem 안이 아니라 여기 독립으로 있다 — 합체 쪽이 같은 함수를 부른다.
    ///
    /// 지위는 요구치 **예산 밖 마진 항**이다. 물류가 빠르면 만재가 자주 차서 발동 빈도가
    /// 오를 뿐 계수가 얹히지는 않으므로, 물류 무개입 원칙과 정합하고 요구치 곡선을 건드리지 않는다.
    /// </summary>
    public static class GrandEntrance
    {
        /// <summary>
        /// 특공 피해. **만재 단일 기준이며 부분 발동이 없다** — 99%에서 99%만큼 나가지 않는다.
        /// 조건을 맞추거나 못 맞추거나다.
        ///
        /// 식 확정: **적재 발수 × 강화 평균 발당피해** (대표 40 × 52.6 ≈ 2,103 — params tagspec).
        /// </summary>
        public static float Damage(bool mountFull, float loadedRounds, float avgDamagePerShot)
        {
            if (!mountFull || loadedRounds <= 0f || avgDamagePerShot <= 0f) return 0f;
            return loadedRounds * avgDamagePerShot;
        }
    }
}
