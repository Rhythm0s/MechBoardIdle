using System;
using UnityEngine;

namespace MBI.Data
{
    /// <summary>
    /// 로봇A 탄종. 등가선 100(관통 20×5 / 분열 25×4 / 폭발 50×2, balance_v4 spectrum).
    /// MVP는 단일 타겟 히트로 취급(관통 라인·분열 다중·폭발 광역 히트 패턴은 후순위 연출).
    /// </summary>
    public enum AmmoKind
    {
        Pierce,     // 관통 20×5
        Split,      // 분열 25×4
        Explosive   // 폭발 50×2
    }

    /// <summary>
    /// 무기 한 종의 스펙(발당피해 + 현재 물류 생산 발사율). balance_v4 params dA*/pA* 미러.
    ///
    /// shotsPerSec = "지금 물류가 공급하는 발사율"(대표 상태 pA = 관통1/분열1/폭발2).
    /// ⚠️ 이는 무기 기계적 최대치가 아니라 물류 산출이다 — 물류가 게임의 제약(핵심 명제).
    /// 벨트/시뮬(§5-4·5-5) 미구현 → 현재는 v4 pA(대표 상태)를 mock으로 주입. 실 물류 시뮬 완성 시
    /// 이 발사율이 동적으로 산출된다. 마운트 소비 상한(capA=6)을 넘으면 고효율 우선 재배분(ShotAllocator).
    /// 수치는 생성기가 json에서 주입 — 코드 리터럴 금지(§3).
    /// 출력(전투력) = Σ shotsPerSec × damagePerShot = 145(대표 상태·def0·마운트1).
    /// </summary>
    [Serializable]
    public struct WeaponSpec
    {
        [Tooltip("탄종. balance_v4 spectrum 등가선.")]
        public AmmoKind kind;
        [Tooltip("발당피해. params dA0/dA1/dA2 = 20/25/50.")]
        public float damagePerShot;
        [Tooltip("현재 물류 생산 발사율(발/초, mock 대표 상태 pA = 1/1/2). 무기 기계 최대치 아님 — 물류 산출.")]
        public float shotsPerSec;

        public WeaponSpec(AmmoKind kind, float damagePerShot, float shotsPerSec)
        {
            this.kind = kind;
            this.damagePerShot = damagePerShot;
            this.shotsPerSec = shotsPerSec;
        }
    }
}
