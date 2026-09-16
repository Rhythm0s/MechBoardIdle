using System.Collections.Generic;
using MBI.Data;

namespace MBI.Core
{
    /// <summary>
    /// 보드가 만드는 **탄약 줄 목록**을 짓는 유일한 자리 (2026-09-16 · 사용자 결정 §74-6 ②).
    ///
    /// ⚠️⚠️ **왜 빼냈나.** 출력은 이 줄들에서 나오는데(`AmmoLineProduction.TotalOutput`),
    /// 짓는 코드가 `LogisticsOutputProvider`(MonoBehaviour) 안에 private 으로 있었다.
    /// 그래서 **EditMode 하네스가 출력을 못 쟀다** — S1 요구치 18 을 넘기는지 확인할 방법이
    /// 화면 하나뿐이었다.
    ///
    /// 📌 **재는 쪽과 노는 쪽이 같은 함수를 부른다.** 하네스가 제 식으로 다시 짜면
    /// 「하네스는 통과인데 화면은 아니다」가 생긴다 — 09-15 에 이미 겪은 모양이다.
    ///
    /// ⚠️ **값을 만들지 않는다** — 스펙은 `BalanceConfig.LineSpecOf`, 피해는 무기 스펙,
    /// 노드 수는 보드 집계에서 온다. 여기는 셋을 묶기만 한다.
    /// </summary>
    public static class MunitionsLineFactory
    {
        /// <summary>
        /// 군수 노드 한 대가 내는 양. 밸런스가 정하고, 없으면 1 로 본다.
        ///
        /// ⚠️ 폴백 1 은 **값이 아니라 안전장치**다 — 자산이 없을 때 출력이 0 이 되어
        /// 「보드가 아무것도 안 만든다」로 잘못 읽히는 것을 막는다.
        /// </summary>
        public static float PerNodeRate(RobotDefinition robot)
            => robot != null && robot.balanceRef != null ? robot.balanceRef.muniPerNode : 1f;

        /// <summary>
        /// 이 로봇·이 보드의 탄약 줄들을 <paramref name="into"/> 에 채운다(먼저 비운다).
        ///
        /// ⚠️ **스펙이 0 인 탄종은 줄을 안 만든다** — 라인 스펙이 없다는 것은 그 탄종을
        /// 이 밸런스가 안 쓴다는 뜻이고, 0 짜리 줄을 넣으면 합계에 0 이 섞여 평균이 흐려진다.
        /// </summary>
        public static void Build(RobotDefinition robot, NetworkAggregate agg, List<MunitionsLine> into)
        {
            if (into == null) return;
            into.Clear();
            if (robot == null || robot.weapons == null) return;

            BalanceConfig bal = robot.balanceRef;

            for (int i = 0; i < robot.weapons.Count; i++)
            {
                WeaponSpec w = robot.weapons[i];
                float spec = bal != null ? bal.LineSpecOf(w.kind) : 0f;
                if (spec <= 0f) continue;

                into.Add(new MunitionsLine(w.kind, spec, w.damagePerShot, agg.MuniCountOf(w.kind)));
            }
        }

        /// <summary>
        /// 이 보드의 **명목 출력**(배율·운송 전). 부르는 쪽이 줄 목록을 들고 있지 않아도 되게 묶어 둔다.
        /// </summary>
        public static float BaseOutput(RobotDefinition robot, NetworkAggregate agg,
            List<MunitionsLine> scratch)
        {
            Build(robot, agg, scratch);
            return AmmoLineProduction.TotalOutput(scratch, PerNodeRate(robot));
        }
    }
}
