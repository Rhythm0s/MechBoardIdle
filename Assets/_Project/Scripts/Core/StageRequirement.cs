using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>요구치 대비 상태. 표시용 — 승패를 정하지 않는다.</summary>
    public enum ReqStatus
    {
        NotApplicable, // 스칼라 요구치가 없는 스테이지(S5 공식형·S6 예산형)
        Below,         // 요구치 미달
        Met,           // 충족
        AboveBand,     // 밴드 상단 초과(S4)
    }

    /// <summary>
    /// 스테이지 요구치 판정(§5-6, 순수). balance_v4 stages[].reqType/req/reqBand를 그대로 읽는다.
    ///
    /// ⚠️ 이건 **척도지 관문이 아니다.** 문서가 정한 통과 조건은 "S1~S5 허들 전원처치 / S6 보스형"이고,
    /// 요구치는 그 물리가 성립하는지 보는 검증선이다. 그래서 여기 결과는 HUD 배지에만 쓰고
    /// CombatSimulation.Evaluate(승패)에는 넣지 않는다 — 넣으면 판정이 이중화되어 §9 예산식과 드리프트한다.
    ///
    /// S5(formula)·S6(budget)은 비교할 스칼라 req가 원천에 아예 없다 → NotApplicable.
    /// 없는 규칙을 여기서 만들지 않는다(§3 구현이 시스템을 발명하지 않는다).
    /// </summary>
    public static class StageRequirement
    {
        public static ReqStatus Evaluate(StageReqType type, float req, Vector2 band, float power)
        {
            switch (type)
            {
                case StageReqType.Fixed:
                    return power >= req ? ReqStatus.Met : ReqStatus.Below;

                case StageReqType.Band:
                    if (power < band.x) return ReqStatus.Below;
                    return power <= band.y ? ReqStatus.Met : ReqStatus.AboveBand;

                default:
                    return ReqStatus.NotApplicable; // Formula(S5) · Budget(S6)
            }
        }
    }
}
