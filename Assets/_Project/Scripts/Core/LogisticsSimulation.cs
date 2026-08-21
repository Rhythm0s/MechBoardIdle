using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 물류 흐름 계산 결과(§5-5, L4-R 개정) — 이중값 출력 + 병목 배율 + 갭 발생원 분해.
    ///
    /// expected = 위상+노드레이트 명목(병목 미적용) / actual = 병목 효율 곱 / gap = expected−actual(총 손실).
    /// 클램프 없음: 물류 천장(×ceil)은 물리의 *결과*지 시스템 캡이 아니다(밸런스 "시스템 개입 안 함").
    /// actual은 병목상 항상 expected 이하라 별도 클램프가 필요 없다.
    ///
    /// ⚠️ 2026-08-21: 145 기준 overCeiling 판정을 **제거**했다. 145는 천장이 아니라 S3 돌파 실측선이고,
    /// 물류 단독 상한 160은 탄종 조합의 이론 최대치(폭발만 채우면 50×6=300)를 규칙으로 막는 값이 아니다.
    /// 천장 담보는 **보드 물리 상한(그리드 크기·라인 수)** 이 지며 그 설계는 검증 대장 이월 항목이다 —
    /// 물리가 막아야 할 것을 규칙이 막던 구조였다. `multiple`은 표시용으로 남긴다.
    /// </summary>
    public struct LogisticsResult
    {
        public float expected;   // 명목(병목 미적용)
        public float actual;     // expected × powerEff × heatThrottle × beltThrottle
        public float gap;        // expected − actual (총 손실)

        // 갭 발생원 분해(곱셈 병목 순차 귀속 → gapPower+gapHeat+gapBelt == gap, telescoping).
        public float gapPower;   // expected × (1−powerEff)
        public float gapHeat;    // expected × powerEff × (1−heatThrottle)
        public float gapBelt;    // expected × powerEff × heatThrottle × (1−beltThrottle)

        public float powerEfficiency;  // min(1, 공급/소비) — 전력 고정비 전용(강화 불가)
        public float heatThrottle;     // 발열 감쇠 배율
        public float beltThrottle;     // 벨트 용량 감쇠 배율

        public float multiple;    // expected / origin (명목 물리 배율 — 표시용, 판정 아님)
        public bool bottlenecked; // 어느 배율이라도 <1
    }

    /// <summary>
    /// 레이트 기반 물류 흐름 시뮬(§5-5, 순수·결정론). 노드/벨트 네트워크의 정상상태 처리율로 출력을 계산.
    ///
    /// 밸런스 규칙(§3·§9):
    ///   - 전력: 효율 = min(1, 공급/소비) — 고정비 전용, 성장으로 강화 불가(전력 긴장 영구화).
    ///   - 벨트: 필요 ≤ 용량이면 무손실, 초과 시 용량/필요로 감쇠.
    ///   - 발열: (발생−냉각) > 임계면 임계/순발열로 감쇠.
    ///   - 물류 무개입: 별도 수치 보너스 없음 — 효율은 물리(처리율)에서만.
    ///   - 물류 천장: 담보는 보드 물리 상한(그리드·라인 수)이 진다. 여기서 규칙으로 막지 않는다(2026-08-21).
    /// baseOutput = 병목 없을 때의 명목 출력(대표 상태 = 관통1+분열1+폭발2 = 145).
    /// </summary>
    public static class LogisticsSimulation
    {
        public static LogisticsResult Compute(
            float baseOutput,
            float powerSupply, float powerDraw,
            float heatGenerate, float heatDissipate, float heatThreshold,
            float beltCapacity, float beltDemand,
            float origin)
        {
            float powerEff = powerDraw > 0f ? Mathf.Clamp01(powerSupply / powerDraw) : 1f;

            float netHeat = Mathf.Max(0f, heatGenerate - heatDissipate);
            float heatThrottle = (heatThreshold > 0f && netHeat > heatThreshold)
                ? heatThreshold / netHeat : 1f;

            float beltThrottle = (beltCapacity > 0f && beltDemand > beltCapacity)
                ? beltCapacity / beltDemand : 1f;

            // 순차 귀속(telescoping): 각 단계가 직전까지 살아남은 흐름에 자신의 손실을 적용.
            float expected = baseOutput;
            float afterPower = expected * powerEff;
            float afterHeat = afterPower * heatThrottle;
            float actual = afterHeat * beltThrottle;

            float multiple = origin > 0f ? expected / origin : 0f;

            return new LogisticsResult
            {
                expected = expected,
                actual = actual,
                gap = expected - actual,
                gapPower = expected * (1f - powerEff),
                gapHeat = afterPower * (1f - heatThrottle),
                gapBelt = afterHeat * (1f - beltThrottle),
                powerEfficiency = powerEff,
                heatThrottle = heatThrottle,
                beltThrottle = beltThrottle,
                multiple = multiple,
                bottlenecked = powerEff < 1f || heatThrottle < 1f || beltThrottle < 1f,
            };
        }
    }
}
