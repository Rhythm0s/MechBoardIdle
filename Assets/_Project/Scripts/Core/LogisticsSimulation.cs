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
        public float actual;     // **마운트에 실제로 도착한 것**(2026-09-05 · 종전에는 계산값)
        public float gap;        // expected − actual (총 손실)

        // 갭 발생원 분해 → gapPower+gapHeat+gapBelt == gap.
        public float gapPower;   // expected × (1−powerEff)
        public float gapHeat;    // expected × powerEff × (1−heatThrottle)
        public float gapBelt;    // afterHeat − actual — **관측된 나머지**(운송에서 잃은 몫)

        public float powerEfficiency;  // min(1, 공급/소비) — 전력 고정비 전용(강화 불가)
        public float heatThrottle;     // 발열 감쇠 배율
        public float beltThrottle;     // actual ÷ afterHeat — **역산값**(운송이 얼마나 살렸는가)

        public float multiple;    // expected / origin (명목 물리 배율 — 표시용, 판정 아님)
        public bool bottlenecked; // 어느 배율이라도 <1
    }

    /// <summary>
    /// 생산을 깎는 두 배율. 전력과 발열은 **생산 단계에 걸린다** — 노드가 덜 만든다.
    /// 그래서 이 둘은 도착량에 이미 반영되어 있고, 결과 조립에서 다시 곱하면 이중이 된다.
    /// </summary>
    public readonly struct ProductionThrottle
    {
        public readonly float power;   // min(1, 공급/소비)
        public readonly float heat;    // 임계 ÷ 순발열 (임계 이하면 1)

        /// <summary>노드에 실제로 걸리는 배율. <see cref="BoardItemTick.Step"/>에 넘긴다.</summary>
        public float Scale => power * heat;

        public ProductionThrottle(float power, float heat)
        {
            this.power = power;
            this.heat = heat;
        }
    }

    /// <summary>
    /// 물류 흐름 결과 조립(§5-5, 순수·결정론).
    ///
    /// ⚠️ **2026-09-05 전면 개정** (`260904_W04` 2-1 4번). 종전에는 여기가 출력을 **계산**했다:
    /// <c>actual = 명목 × 전력배율 × 발열배율 × 벨트배율</c>. 그 모델에는 두 가지 문제가 있었다.
    ///
    ///   1. **벨트가 근사였다.** 「필요 ≤ 용량이면 무손실」이라는 식은 라인의 모양을 안 본다.
    ///      실제로는 아이템이 칸을 지나고 칸마다 상한이 있으며 갈래에서 갈린다. 그래서
    ///      벨트를 잘 깔든 못 깔든 노드 수만 같으면 같은 수가 나왔다 —
    ///      최적화의 결과가 숫자에 안 보였다.
    ///   2. **전력이 두 번 걸릴 참이었다.** 2026-09-04에 전력 효율이 생산 단계로 들어가면서
    ///      (`260903_W02` 2-2) 도착량이 이미 전력을 반영하는데, 여기서 또 곱하면 제곱이 된다.
    ///
    /// 이제 <b>actual은 관측치</b>다. <see cref="MountDelivery"/>가 마운트를 통과한 것을 세고,
    /// 여기서는 그 수를 받아 갭을 분해하기만 한다. 전력·발열은 생산에 걸리고
    /// (<see cref="ProductionThrottle"/>), 벨트 손실은 **계산하지 않고 나머지로 남는다** —
    /// 근사식이 사라진 자리에 실제로 잃은 몫이 들어온다.
    ///
    /// 밸런스 규칙(§3·§9):
    ///   - 전력: 효율 = min(1, 공급/소비) — 고정비 전용, 성장으로 강화 불가(전력 긴장 영구화).
    ///   - 발열: (발생−냉각) > 임계면 임계/순발열로 감쇠.
    ///   - 물류 무개입: 별도 수치 보너스 없음 — 효율은 물리(처리율)에서만.
    ///   - 물류 천장: 담보는 보드 물리 상한(그리드·라인 수)이 진다. 여기서 규칙으로 막지 않는다(2026-08-21).
    /// </summary>
    public static class LogisticsSimulation
    {
        /// <summary>
        /// 생산에 걸리는 배율. **결과 조립보다 먼저** 불러 <see cref="BoardItemTick.Step"/>에
        /// 넘겨야 한다 — 그래야 같은 프레임의 도착량이 이 배율을 반영한다.
        /// </summary>
        public static ProductionThrottle Throttles(
            float powerSupply, float powerDraw,
            float heatGenerate, float heatDissipate, float heatThreshold)
        {
            float powerEff = powerDraw > 0f ? Mathf.Clamp01(powerSupply / powerDraw) : 1f;

            float netHeat = Mathf.Max(0f, heatGenerate - heatDissipate);
            float heatThrottle = (heatThreshold > 0f && netHeat > heatThreshold)
                ? heatThreshold / netHeat : 1f;

            return new ProductionThrottle(powerEff, heatThrottle);
        }

        /// <summary>
        /// 관측된 도착량으로 결과를 조립한다.
        ///
        /// <paramref name="baseOutput"/>은 병목이 하나도 없을 때의 명목(= expected)이고,
        /// <paramref name="observedActual"/>은 <see cref="MountDelivery"/>가 잰 실제 도착이다.
        ///
        /// **갭 분해**는 순차 귀속을 유지한다. 전력과 발열은 식이 있으므로 그대로 귀속하고,
        /// 남은 것이 전부 운송 몫이다:
        /// <code>
        ///   gapPower = expected × (1−전력)
        ///   gapHeat  = afterPower × (1−발열)
        ///   gapBelt  = afterHeat − actual        ← 계산이 아니라 나머지
        /// </code>
        /// 셋을 더하면 정확히 <c>expected − actual</c>이 된다.
        ///
        /// ⚠️ **gapBelt가 음수일 수 있다.** 쌓여 있던 것이 한꺼번에 빠지면 그 구간의 도착이
        /// 생산 능력보다 많다. 버그가 아니라 버퍼가 비워지는 중이라는 뜻이므로 0으로 덮지 않는다 —
        /// 덮으면 분해 합이 총갭과 안 맞아 변수 패널이 거짓말을 한다.
        /// </summary>
        public static LogisticsResult Compute(
            float baseOutput, ProductionThrottle throttle, float observedActual, float origin)
        {
            float expected = baseOutput;
            float afterPower = expected * throttle.power;
            float afterHeat = afterPower * throttle.heat;

            float actual = observedActual;
            float beltThrottle = afterHeat > 0f ? actual / afterHeat : 1f;

            float multiple = origin > 0f ? expected / origin : 0f;

            return new LogisticsResult
            {
                expected = expected,
                actual = actual,
                gap = expected - actual,
                gapPower = expected * (1f - throttle.power),
                gapHeat = afterPower * (1f - throttle.heat),
                gapBelt = afterHeat - actual,
                powerEfficiency = throttle.power,
                heatThrottle = throttle.heat,
                beltThrottle = beltThrottle,
                multiple = multiple,
                bottlenecked = throttle.power < 1f || throttle.heat < 1f || beltThrottle < 1f,
            };
        }
    }
}
