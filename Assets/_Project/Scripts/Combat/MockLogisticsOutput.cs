using MBI.Core;
using MBI.Data;

namespace MBI.Combat
{
    /// <summary>
    /// 물류 출력 ↔ 전투력 연동(임계경로 §5-6)의 브릿지. 이제 계산 경로가 LogisticsSimulation(§5-5)을 통과.
    ///
    /// baseOutput = RobotDefinition.weapons의 mock 생산율(pA)×히트당실피해(def0) 집계 = 대표 상태 145.
    /// 이를 흐름 시뮬에 넣어 병목·천장(≤origin×ceil=160)을 적용한다. 라이브 물류 네트워크(노드/벨트 배치)는
    /// 팔레트 UI 후 — 그때 실제 supply/draw/heat/belt 집계를 여기로 주입하면 배치가 출력에 반영된다.
    /// 교체는 이 클래스 한 곳(유일 진입점)에서 국소화.
    /// </summary>
    public static class MockLogisticsOutput
    {
        /// <summary>물류 출력(전투력). baseOutput(대표 145)을 시뮬 통과 + 천장 클램프.</summary>
        public static float CurrentOutput(RobotDefinition robot, float mountCoef, float moduleMult, float ceiling)
        {
            if (robot == null || robot.weapons == null) return 0f;

            float baseOutput = 0f;
            foreach (WeaponSpec w in robot.weapons)
                baseOutput += w.shotsPerSec * DamageFormula.PerHit(w.damagePerShot, mountCoef, moduleMult, 0f);

            // 라이브 네트워크 미구현(팔레트 UI 후) → 병목 없는 항등 입력으로 시뮬 통과 + 천장 클램프.
            LogisticsResult r = LogisticsSimulation.Compute(
                baseOutput,
                powerSupply: 1f, powerDraw: 1f,
                heatGenerate: 0f, heatDissipate: 0f, heatThreshold: 1f,
                beltCapacity: 1f, beltDemand: 0f,
                ceiling: ceiling);
            return r.output;
        }
    }
}
