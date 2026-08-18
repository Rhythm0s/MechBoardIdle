using MBI.Core;
using MBI.Data;
using UnityEngine;

namespace MBI.Combat
{
    /// <summary>
    /// 격리 전투 씬(Combat.unity) 전용 물류 소스(§5-6 D2).
    ///
    /// 통합 씬에서는 LogisticsOutputProvider가 라이브 보드를 읽어 브릿지에 쓴다. 격리 씬에는 보드가
    /// 없으므로 이 컴포넌트가 같은 자리를 채운다 — 덕분에 StageRunner는 **두 씬 모두에서 브릿지만
    /// 읽으면 된다.** 씬을 구분하는 플래그 분기가 필요 없어지고, 단위(물류 단위 = 마운트계수 미적용)도
    /// 한 곳에서 지켜진다.
    ///
    /// 값은 병목 없는 항등 입력이라 대표 출력에서 움직이지 않는다(격리 = 전투 메커니즘만 보는 씬).
    /// </summary>
    public sealed class MockLogisticsSource : MonoBehaviour
    {
        [Tooltip("로봇. 명목 출력·원점·천장의 원천. 씬 생성기가 주입.")]
        public RobotDefinition robot;

        private void Awake() => LogisticsOutputBridge.Reset();

        private void Update()
        {
            if (robot == null) return;

            float origin = robot.balanceRef != null ? robot.balanceRef.origin : 100f;
            float ceilMult = robot.balanceRef != null ? robot.balanceRef.ceil : 1.6f;

            LogisticsOutputBridge.Result = MockLogisticsOutput.Simulate(robot, 1f, robot.moduleMult, origin, ceilMult);
            LogisticsOutputBridge.AmmoProduce = robot.consumptionCap; // 격리 씬 = 수요만큼 공급된다고 본다
            LogisticsOutputBridge.GlobalCause = ConstraintCause.None;
        }
    }
}
