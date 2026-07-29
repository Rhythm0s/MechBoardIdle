using MBI.Core;
using MBI.Data;

namespace MBI.Combat
{
    /// <summary>
    /// 물류 출력 ↔ 전투력 연동(임계경로 §5-6)의 임시 브릿지.
    ///
    /// 벨트/시뮬(§5-4·5-5)이 아직 없으므로, 물류 출력(전투력)을 RobotDefinition.weapons에 mock으로
    /// 주입된 생산율(pA)에서 집계한다. 출력 = Σ pA × 히트당실피해(def0). 대표 상태 = 145 = s3Break.
    /// 실 물류 시뮬 완성 시 이 집계를 시뮬 산출로 교체(이 클래스가 유일 진입점이라 교체 국소화).
    /// </summary>
    public static class MockLogisticsOutput
    {
        /// <summary>명목 출력(def0 기준). 요구치(req)와 비교·표시용. 대표 상태 145.</summary>
        public static float CurrentOutput(RobotDefinition robot, float mountCoef, float moduleMult)
        {
            if (robot == null || robot.weapons == null) return 0f;
            float sum = 0f;
            foreach (WeaponSpec w in robot.weapons)
                sum += w.shotsPerSec * DamageFormula.PerHit(w.damagePerShot, mountCoef, moduleMult, 0f);
            return sum;
        }
    }
}
