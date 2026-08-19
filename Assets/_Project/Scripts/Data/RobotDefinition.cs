using System.Collections.Generic;
using UnityEngine;

namespace MBI.Data
{
    /// <summary>
    /// 플레이어 로봇 정의(단일 원천 SO). CLAUDE.md §5-6·07 로봇 문서.
    ///
    /// 판정식(플레이어블 로봇 기획서「판정식」): 실피해(히트당) = max(1, 발당피해 × 마운트계수 × 모듈배율 − 방어). 난수 0.
    /// - 마운트계수: 물류(S1~S3) = mountCoef(1.0), 강화/S4+ = enhancedMountCoef(enh=1.45).
    ///   스테이지 powerModel로 어느 쪽을 적용할지 결정(StageRunner).
    /// - 소비 상한(capA=6발/초, 고효율 우선)으로 무기 발사가 재배분됨(ShotAllocator).
    ///
    /// 수치는 CombatAssetGenerator가 balance_v4.json에서 주입 — 코드 리터럴 금지(§3).
    /// HP·사거리·이동 등 미확정 전투 튜닝은 여기 두지 않고 CombatTuning에 TBD로 둔다(§3 한 파일=한 책임).
    /// </summary>
    [CreateAssetMenu(fileName = "Robot", menuName = "MBI/Robot Definition", order = 10)]
    public sealed class RobotDefinition : ScriptableObject
    {
        [Header("정체")]
        [Tooltip("안정 키. 예: robotA.")]
        public string robotId;
        [Tooltip("표시명.")]
        public string displayName;

        [Header("무기(마운트A 탄약 스펙트럼)")]
        [Tooltip("탄종별 발당피해(dA) + 물류 생산 발사율(pA·mock 대표 상태). ΣpA×dA = 145 출력.")]
        public List<WeaponSpec> weapons = new List<WeaponSpec>();
        [Tooltip("마운트 소비 상한(발/초, 고효율 우선). params.capA = 6.")]
        public float consumptionCap = 6f;

        [Header("판정식 계수")]
        [Tooltip("마운트계수(물류 상태·S1~S3). 기본 1.0.")]
        public float mountCoef = 1f;
        [Tooltip("강화 마운트계수(S4+/enhanced). params.enh / enhance.enhPoint = 1.45.")]
        public float enhancedMountCoef = 1.45f;
        [Tooltip("모듈배율. params.moduleMult = 1.0.")]
        public float moduleMult = 1f;

        [Header("참조")]
        [Tooltip("전역 밸런스 앵커 단일 원천.")]
        public BalanceConfig balanceRef;
    }
}
