using UnityEngine;

namespace MBI.Core
{
    /// <summary>물류 흐름 계산 결과 — 출력(전투력) + 각 병목 배율.</summary>
    public struct LogisticsResult
    {
        public float output;           // 전투력(요구치 대비), 천장 클램프 적용
        public float powerEfficiency;  // min(1, 공급/소비)
        public float heatThrottle;     // 발열 감쇠 배율
        public float beltThrottle;     // 벨트 용량 감쇠 배율
        public bool bottlenecked;      // 어느 하나라도 <1
    }

    /// <summary>
    /// 레이트 기반 물류 흐름 시뮬(§5-5, 순수·결정론). 노드/벨트 네트워크의 정상상태 처리율로 출력을 계산.
    ///
    /// 밸런스 규칙(§3·§9):
    ///   - 전력: 효율 = min(1, 공급/소비) — 고정비 전용, 성장으로 강화 불가(전력 긴장 영구화).
    ///   - 벨트: 필요 ≤ 용량이면 무손실, 초과 시 용량/필요로 감쇠.
    ///   - 발열: (발생−냉각) > 임계면 임계/순발열로 감쇠.
    ///   - 물류 무개입: 별도 수치 보너스 없음 — 효율은 물리(처리율)에서만.
    ///   - 물류 천장: 출력 ≤ origin×ceil(=160). 물류 단독으론 이 배율 초과 불가.
    /// baseOutput = 병목 없을 때의 명목 출력(대표 상태 = 관통1+분열1+폭발2 = 145).
    /// </summary>
    public static class LogisticsSimulation
    {
        public static LogisticsResult Compute(
            float baseOutput,
            float powerSupply, float powerDraw,
            float heatGenerate, float heatDissipate, float heatThreshold,
            float beltCapacity, float beltDemand,
            float ceiling)
        {
            float powerEff = powerDraw > 0f ? Mathf.Clamp01(powerSupply / powerDraw) : 1f;

            float netHeat = Mathf.Max(0f, heatGenerate - heatDissipate);
            float heatThrottle = (heatThreshold > 0f && netHeat > heatThreshold)
                ? heatThreshold / netHeat : 1f;

            float beltThrottle = (beltCapacity > 0f && beltDemand > beltCapacity)
                ? beltCapacity / beltDemand : 1f;

            float raw = baseOutput * powerEff * heatThrottle * beltThrottle;
            float output = ceiling > 0f ? Mathf.Min(raw, ceiling) : raw;

            return new LogisticsResult
            {
                output = output,
                powerEfficiency = powerEff,
                heatThrottle = heatThrottle,
                beltThrottle = beltThrottle,
                bottlenecked = powerEff < 1f || heatThrottle < 1f || beltThrottle < 1f,
            };
        }
    }
}
