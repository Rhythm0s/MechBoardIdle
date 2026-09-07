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

        [Header("자동 전투 (⚠️ TBD — 밸런스 아님, 연출·조작감)")]
        // autoPilotDesiredGapTbd 삭제(260829_V03 승인) — 2026-08-26 카이팅 폐기로
        // 「후퇴 개시 거리」라는 개념 자체가 사라졌다. 읽는 곳도 0건이었다.
        [Tooltip("TBD — 수동 입력 후 자동 조종이 다시 잡기까지의 유예(초). 조작 중 자동이 끼어들지 않게.")]
        public float manualOverrideGraceTbd = 2f;
        [Tooltip("TBD — 전투 종료 후 자동 재시작까지 대기(초). 승리 연출을 볼 시간 vs 방치 효율.")]
        public float autoRestartDelayTbd = 1.5f;

        [Header("애니메이션 길이 (260907_W01 4-5 · 사용자 확정)")]
        // 초당 프레임은 폐기됐다(W01 2-2). 한 칸은 1/16초로 고정이고 벌마다 목표 초를 사람이 정한다 —
        // 그래야 그림 수가 부드러움과 길이를 겸하지 않는다. 칸 배분은 AnimSchedule 이 계산한다(4-6).
        // 값 자체는 확정이라 TBD 가 아니다. 하드코딩 금지(지침 §3)라 여기에 둔다.
        [Tooltip("한 칸이 화면에 머무는 시간(초). 1/16 고정 — 벌마다 바꾸지 않는다.")]
        public float animCellSeconds = 1f / 16f;
        [Tooltip("대기 목표 초. 왕복이라 기본 칸(8)의 배수에서만 고른다.")]
        public float animIdleSeconds = 1.00f;
        [Tooltip("이동 목표 초. 나머지 넷이 착지 머무름으로 열린다.")]
        public float animMoveSeconds = 1.00f;
        [Tooltip("사망 목표 초.")]
        public float animDeathSeconds = 2.00f;
        [Tooltip("태그 전환 목표 초. 잠정 — 전투 시스템 문서「태그 규칙」 확인 뒤 확정한다(W01 9장 2).")]
        public float animTagInSeconds = 0.75f;
        // 코드 이동은 발사 프레임보다 뒤에 끝나고 클립보다는 먼저 끝난다(`260907_W02` 2-3 사용자 확정).
        // 설계가 확정한 것은 값이 아니라 순서다 — 태그 지속 시간이 바뀌어도 이 꼬리는 그대로 두면
        // 이동이 따라 맞는다. 꼬리 칸이 남아야 반동과 수렴이 제자리에서 보인다(15-1 5-1 구간 3·4).
        [Tooltip("태그 진입에서 도착 뒤에 남기는 꼬리 칸 수. 이동은 이만큼 일찍 끝난다.")]
        public int animTagEntryTrailCells = 3;

        [Header("히트 패턴 (로봇A 탄종 = 단일 표적)")]
        // 플레이어블 로봇 기획서「무기 스펙트럼」(스테이징): 등가선은 단일 표적 기준, 표적 수/광역은 스펙트럼 밖 역할 축(드론 2종 한정).
        // → 로봇A 관통/분열/폭발은 전부 단일 표적. 멀티샷/AoE 메커니즘(HitResolver)은 드론용으로 보존.
        [Tooltip("멀티샷 표적 수. 로봇A 분열탄 = 1(단일). 다중은 드론 역할.")]
        public int multiShotCountTbd = 1;
        [Tooltip("AoE 스플래시 반경(유닛). 로봇A 폭발탄은 splashFactor 0이라 무효(드론용 보존).")]
        public float aoeRadiusTbd = 1.5f;
        [Tooltip("AoE 스플래시 배율. 로봇A 폭발탄 = 0(스플래시 없음, 단일 표적). 드론 광역형이 >0 사용.")]
        public float aoeSplashFactorTbd = 0f;

        [Header("수동 회피 입력 — 화면 플릭")]
        // 회피 자체의 확정치(무적 0.167초 · 스택 상한 3)는 DodgeSystem이 든다. 여기는 **입력 인식** 축이라
        // 별개다 — 플릭으로 볼 문턱은 원천에 없어 TBD다(문턱을 무적 값에서 끌어오면 안 된다).
        [Tooltip("TBD — 플릭으로 인정할 최소 이동(픽셀). chat+Notion 확정 필요.")]
        public float flickMinPixelsTbd = 40f;
        [Tooltip("TBD — 플릭으로 인정할 최대 시간(초). 이보다 오래 끌면 드래그로 본다.")]
        public float flickMaxSecondsTbd = 0.3f;

        // 마운트 탄약 용량(capA=6)은 RobotDefinition.consumptionCap 단일 소스로 통합(§3 한 파일=한 책임, L4-R).
        //   — 중복 미러(mountAmmoCapTbd) 제거. HUD/발사 배분은 robot.consumptionCap을 읽는다.
        // 물류 출력(전투력 입력)은 별도 상수를 두지 않는다 — RobotDefinition.weapons의 mock 생산율(pA)에서
        // MockLogisticsOutput이 집계(ΣpA×dA=145). 실 물류 시뮬(§5-4·5-5) 완성 시 그쪽이 동적 산출.
    }
}
