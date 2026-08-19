using System.Collections.Generic;
using MBI.Data;

namespace MBI.Core
{
    /// <summary>
    /// 출력(전투력) 산출의 단일 원천(§5-6). 순수 함수 — EditMode 검증 가능.
    ///
    /// 출력 = Σ(초당 발사수 × 히트당 실피해). 히트당 실피해는 판정식(플레이어블 로봇 기획서「판정식」)을 그대로 쓴다 —
    /// <see cref="DamageFormula.PerHit"/> 재사용이며 여기서 다시 계산하지 않는다(§3 한 파일=한 책임).
    ///
    /// ⚠️ 이 클래스가 생긴 이유: 대표 출력 145가 브릿지 기본값·Provider 인스펙터 값·Mock 계산의
    /// 세 곳에 각자 존재해 서로 어긋날 수 있었다. 유일한 원천은 RobotDefinition.weapons(= balance_v4의
    /// pA/dA를 CombatAssetGenerator가 주입)이므로, 145를 알아야 하는 쪽은 전부 여기를 호출한다.
    ///
    /// 마운트계수는 판정식 내부 항(= 전투 측)이다. 물류 단위 값이 필요하면 mountCoef=1로 부른다.
    /// </summary>
    public static class RobotOutput
    {
        /// <summary>명목 출력: 무기 스펙의 발사율이 전부 공급된다고 볼 때의 출력.</summary>
        public static float Nominal(IReadOnlyList<WeaponSpec> weapons, float mountCoef, float moduleMult)
        {
            if (weapons == null) return 0f;

            float sum = 0f;
            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponSpec w = weapons[i];
                sum += w.shotsPerSec * DamageFormula.PerHit(w.damagePerShot, mountCoef, moduleMult, 0f);
            }
            return sum;
        }
    }
}
