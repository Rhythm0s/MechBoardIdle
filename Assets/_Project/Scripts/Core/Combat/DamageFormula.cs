using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 판정식(07 1장, CLAUDE.md §9): 실피해(히트당) = max(1, 발당피해 × 마운트계수 × 모듈배율 − 방어).
    /// 난수 0. 방어는 히트당 뺄셈. 로봇 방어 스탯 없음(받는 피해 = 몬스터 공격력).
    /// 순수 함수 — Unity 없이 EditMode에서 검증 가능.
    /// </summary>
    public static class DamageFormula
    {
        /// <summary>히트당 실피해. 하한 1(방어가 아무리 커도 최소 1 관통).</summary>
        public static float PerHit(float damagePerShot, float mountCoef, float moduleMult, float def)
        {
            return Mathf.Max(1f, damagePerShot * mountCoef * moduleMult - def);
        }
    }
}
