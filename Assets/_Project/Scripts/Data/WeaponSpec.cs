using System;
using UnityEngine;

namespace MBI.Data
{
    /// <summary>
    /// 로봇A 탄종 — 관통 20×5 / **표준 10×6** / 폭발 50×2 (`260909_W01` 2-1 확정).
    ///
    /// ⚠️ **표준탄이 표준탄으로 바뀐 것은 개명이 아니라 좌표 변경이다.** 자리는 같지만
    /// 값이 25×4에서 10×6으로 내려갔고, 그 근거는 취향이 아니라 구조다 — 2026-09-04
    /// 개정으로 **특수탄 둘이 표준탄을 재료로 먹으므로**, 표준탄이 관통탄보다 세면
    /// 「더 긴 체인으로 더 적게 얻는」 자리가 생겨 특수탄이 지배당한다(W01 2-2).
    ///
    /// ⚠️ **등가 축도 함께 바뀌었다** — 초당 출력이 아니라 **노드당 출력**이다. 구 등가선
    /// 「스펙 × 발당피해 = 100」은 체인 길이가 값 밖에 있을 때의 장치였고, 개정으로
    /// 체인이 노드 수에 들어오면서 축이 옮겨졌다. 표준탄만 초당 60인 것은 결함이 아니다.
    ///
    /// MVP는 단일 타겟 히트로 취급(관통 라인·표준 다중·폭발 광역 히트 패턴은 후순위 연출).
    ///
    /// ⚠️ **정수 값을 명시한다.** `AmmoInventory`가 `_stacks[(int)kind]`로 색인하고
    /// `BalanceConfig.lineSpecShots`가 `Vector3` 성분 순서로 대응하며 보드가 `(AmmoKind)k`로
    /// 되돌린다 — 세 곳이 순서에 기대므로 **개명하면서 번호가 흔들리면 조용히 어긋난다.**
    /// </summary>
    public enum AmmoKind
    {
        Pierce = 0,     // 관통 20×5
        Standard = 1,   // 표준 10×6 — 구 표준탄 자리(번호 보존)
        Explosive = 2   // 폭발 50×2
    }

    /// <summary>
    /// 무기 한 종의 스펙(발당피해 + 현재 물류 생산 발사율). balance_v4 params dA*/pA* 미러.
    ///
    /// shotsPerSec = "지금 물류가 공급하는 발사율"(대표 상태 pA = 관통1/표준1/폭발2).
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
