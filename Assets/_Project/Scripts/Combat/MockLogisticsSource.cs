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

        [Tooltip("군수 노드 SO. 드론 몸체·추진제 산출 속도를 조합표에서 읽는다 — 여기 숫자를 적지 않는다.")]
        public NodeDefinition munitionsNode;

        private void Awake() => LogisticsOutputBridge.Reset();

        private void Update()
        {
            if (robot == null) return;

            float origin = robot.balanceRef != null ? robot.balanceRef.origin : 100f;

            LogisticsOutputBridge.Result = MockLogisticsOutput.Simulate(robot, 1f, robot.moduleMult, origin);
            LogisticsOutputBridge.AmmoProduce = robot.consumptionCap; // 격리 씬 = 수요만큼 공급된다고 본다
            LogisticsOutputBridge.DroneProduce = RecipeRate(RecipeKind.DroneBody);
            LogisticsOutputBridge.PropellantProduce = RecipeRate(RecipeKind.Propellant);
            LogisticsOutputBridge.GlobalCause = ConstraintCause.None;
        }

        /// <summary>
        /// 군수 노드가 그 조합표를 **한 개 돌렸을 때**의 산출(개/초). 노드가 없으면 0이다.
        ///
        /// 격리 씬은 「군수 노드 한 대가 붙어 있다」로 본다 — 여기서 노드 수를 늘리면
        /// 보드 없이 밸런스를 만지게 되므로 배수를 두지 않는다.
        /// </summary>
        private float RecipeRate(RecipeKind kind)
        {
            if (munitionsNode == null || munitionsNode.recipes == null) return 0f;

            foreach (NodeRecipe r in munitionsNode.recipes)
                if (r.kind == kind) return r.IsRunnable ? r.outputPerSec : 0f;

            return 0f;
        }
    }
}
