using UnityEngine;

namespace MBI.Data
{
    /// <summary>
    /// 실시간 탑뷰 전투의 튜닝 상수(§5-6·7). ⚠️ 전부 TBD placeholder — 확정 밸런스 아님.
    ///
    /// CLAUDE.md §0·§3 역할 경계: 로봇 HP·전투 타이밍·이동/사거리는 §9에서 미확정(TBD).
    /// Claude Code가 수치를 임의 확정하지 않는다 → 여기에 명시 placeholder로 두고 사용자에게 보고
    /// → chat+Notion 확정 후 balance_v4.json/CLAUDE.md 원천에 반영. (§3 "미확정치는 TBD 밴드 상수")
    ///
    /// BalanceConfig(밸런스 계약)와 분리한다: 여기 값을 계약에 섞으면 생성기 재실행이 덮어쓰고
    /// 오염된다(§3 한 파일=한 책임, BoardConfig 선례). 이 SO는 CombatAssetGenerator가 LoadOrCreate만
    /// 하고 값은 덮어쓰지 않는다(인스펙터 조정 유지).
    /// </summary>
    [CreateAssetMenu(fileName = "CombatTuning", menuName = "MBI/Combat Tuning (TBD)", order = 13)]
    public sealed class CombatTuning : ScriptableObject
    {
        [Header("로봇 (⚠️ TBD — 생존축 후순위, §9 미확정)")]
        [Tooltip("TBD — 로봇 최대 HP. chat+Notion 확정 필요. §9 생존축 예산 밖·후순위.")]
        public float robotHpTbd = 3000f;
        [Tooltip("TBD — 로봇 사거리(유닛). chat+Notion 확정 필요. 기본은 arena 전체 커버.")]
        public float robotAttackRangeTbd = 100f;
        [Tooltip("TBD — 로봇 이동 속도(유닛/초). WASD/화살표로 조작(카이팅). chat+Notion 확정 필요.")]
        public float robotMoveSpeedTbd = 4.5f;

        [Header("적 (⚠️ TBD)")]
        [Tooltip("TBD — 적 이동 속도(유닛/초). chat+Notion 확정 필요.")]
        public float enemyMoveSpeedTbd = 1.2f;
        [Tooltip("TBD — 적 공격 사거리(유닛). 이 거리 내에서 로봇을 타격.")]
        public float enemyAttackRangeTbd = 1.0f;
        [Tooltip("TBD — 적 공격 간격(초). atk를 이 주기로 로봇에 가함.")]
        public float enemyAttackIntervalTbd = 1.5f;

        [Header("스폰 / 아레나 (⚠️ TBD)")]
        [Tooltip("TBD — 스폰 간격(초). 0이면 시작 시 전원 스폰(웨이브 없음).")]
        public float spawnCadenceTbd = 0.35f;
        [Tooltip("TBD — 아레나 반경(유닛). 적은 이 반경 경계에서 스폰되어 중앙 로봇으로 접근.")]
        public float arenaRadiusTbd = 6f;

        [Header("히트 패턴 (로봇A 탄종 = 단일 표적)")]
        // 07 5장(스테이징): 등가선은 단일 표적 기준, 표적 수/광역은 스펙트럼 밖 역할 축(드론 2종 한정).
        // → 로봇A 관통/분열/폭발은 전부 단일 표적. 멀티샷/AoE 메커니즘(HitResolver)은 드론용으로 보존.
        [Tooltip("멀티샷 표적 수. 로봇A 분열탄 = 1(단일). 다중은 드론 역할.")]
        public int multiShotCountTbd = 1;
        [Tooltip("AoE 스플래시 반경(유닛). 로봇A 폭발탄은 splashFactor 0이라 무효(드론용 보존).")]
        public float aoeRadiusTbd = 1.5f;
        [Tooltip("AoE 스플래시 배율. 로봇A 폭발탄 = 0(스플래시 없음, 단일 표적). 드론 광역형이 >0 사용.")]
        public float aoeSplashFactorTbd = 0f;

        // 물류 출력(전투력 입력)은 별도 상수를 두지 않는다 — RobotDefinition.weapons의 mock 생산율(pA)에서
        // MockLogisticsOutput이 집계(ΣpA×dA=145). 실 물류 시뮬(§5-4·5-5) 완성 시 그쪽이 동적 산출.
    }
}
