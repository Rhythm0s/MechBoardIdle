using MBI.Core;
using MBI.Data;

namespace MBI.Combat
{
    /// <summary>
    /// 물류 출력 ↔ 전투력 연동(임계경로 §5-6)의 브릿지. 이제 계산 경로가 LogisticsSimulation(§5-5)을 통과.
    ///
    /// baseOutput = RobotDefinition.weapons의 mock 생산율(pA)×히트당실피해(def0) 집계 = 대표 상태 145.
    /// 이를 흐름 시뮬에 넣어 병목을 적용한다. 천장 판정은 없다 — 145는 돌파선이지 상한이 아니다(2026-08-21).
    /// 라이브 물류 네트워크(노드/벨트 배치)는
    /// 팔레트 UI 후 — 그때 실제 supply/draw/heat/belt 집계를 여기로 주입하면 배치가 출력에 반영된다.
    /// 교체는 이 클래스 한 곳(유일 진입점)에서 국소화.
    /// </summary>
    public static class MockLogisticsOutput
    {
        /// <summary>
        /// 격리 씬용 물류 산출. 대표 출력을 병목 없는 항등 입력으로 시뮬에 통과시킨다(actual==expected).
        /// 브릿지 게시 단위에 맞추려면 mountCoef=1(물류 단위)로 부른다 — 마운트계수는 전투 측 항이다.
        /// </summary>
        public static LogisticsResult Simulate(RobotDefinition robot, float mountCoef, float moduleMult, float origin)
        {
            if (robot == null || robot.weapons == null) return default;

            float baseOutput = RobotOutput.Nominal(robot.weapons, mountCoef, moduleMult);

            // 라이브 네트워크가 없는 격리 전투 씬이다 — 보드도 벨트도 없으므로 **잴 도착이 없다.**
            // 병목 없는 배율에 「명목만큼 도착했다」를 넣어 actual == expected == baseOutput으로 만든다.
            // 2026-09-05에 actual이 관측치가 되면서, 여기서는 그 관측을 대신 적어 주는 자리가 됐다.
            return LogisticsSimulation.Compute(
                baseOutput,
                new ProductionThrottle(1f, 1f),
                observedActual: baseOutput,
                origin: origin);
        }
    }
}
