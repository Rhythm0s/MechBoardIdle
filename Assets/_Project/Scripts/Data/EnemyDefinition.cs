using System.Collections.Generic;
using UnityEngine;

namespace MBI.Data
{
    /// <summary>몬스터 병종. balance_v4 enemies[].key 미러.</summary>
    public enum EnemyRole
    {
        Infantry,   // 보병
        Artillery,  // 포격
        Armor,      // 장갑
        Boss        // 강적(보스)
    }

    /// <summary>
    /// 적(몬스터) 카탈로그 정의(단일 원천 SO). CLAUDE.md §9 몬스터 곡선 / 09 스테이지.
    ///
    /// atk만 카탈로그에 둔다 — hp·def·수는 스테이지별(StageDefinition.composition).
    /// ⚠️ balance_v4 enemies[].atk 는 confirmed:false(미확정) — "검증 완료" 표기 금지(§7).
    /// 수치는 CombatAssetGenerator가 balance_v4.json에서 주입 — 코드 리터럴 금지(§3).
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy", menuName = "MBI/Enemy Definition", order = 11)]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("정체")]
        [Tooltip("안정 키(스테이지 composition의 enemy 참조). infantry/artillery/armor/boss.")]
        public string enemyKey;
        [Tooltip("표시명. 보병 / 포격 / 장갑 / 강적.")]
        public string displayName;
        [Tooltip("병종 구분.")]
        public EnemyRole role;

        [Header("공격력 (⚠️ 미확정 — confirmed:false)")]
        [Tooltip("공격력. enemies[].atk = 보병6/포격10/장갑8/보스20. balance_v4에서 confirmed:false.")]
        public float atk;
        [Tooltip("balance_v4 enemies[].confirmed. false면 미확정치(§7 오표기 방지).")]
        public bool atkConfirmed;

        // ── 행동 (2026-09-11 신설 · 플랜 §71-33 ② 공격 패턴 (가)) ──────────────
        //
        // **왜 여기인가.** 적 넷이 `CombatTuning` 의 TBD 셋을 **똑같이** 쓰고 있었다 —
        // 값을 병종마다 다르게 줄 **자리 자체가 없었다.** hp·def 는 스테이지가, atk 는 이 SO 가
        // 들고 있으니 병종 축 값은 여기 붙는 것이 맞다.
        //
        // ⚠️ **0 = 「현행대로」다.** 빈 칸을 0 으로 읽어 적이 멈추거나 사거리를 잃으면
        // 자산 하나가 전투를 조용히 망가뜨린다. 0 이면 러너가 규칙 가정 → 튜닝 순으로 물린다.
        //
        // ⚠️ **여기 칸이 W03 의 착지점이다.** 값이 오면 이 칸에 적히고 코드의 가정은 안 쓰인다.
        [Header("행동 (0 = 현행 유지 · ⚠️ 값은 260911_W03 대기)")]
        [Tooltip("이동 속도(유닛/초). 0이면 CombatTuning.enemyMoveSpeedTbd.")]
        public float moveSpeed;
        [Tooltip("공격 사거리(유닛). 0이면 병종 규칙(포격 7 가정) → 없으면 CombatTuning.")]
        public float attackRange;
        [Tooltip("공격 간격(초). 0이면 CombatTuning.enemyAttackIntervalTbd.")]
        public float attackInterval;
        [Tooltip("투사체 속도(유닛/초). 0이면 병종 규칙(포격만 6 가정) → 그 외 즉발. " +
                 "명중은 접촉으로 본다 — 유도가 아니라 쏜 순간의 로봇 자리를 겨눈다.")]
        public float projectileSpeed;

        [Header("그림 (경로는 생성기에만 — 런타임은 이 참조만 본다)")]
        [Tooltip("스틸 한 장. 벌이 없거나 아직 안 걸린 방향에서 그대로 남는다. null이면 색 사각 폴백.")]
        public Sprite sprite;

        [Tooltip("대기·이동·사망 벌. 비면 스틸 한 장이 그대로 남는다 — 자리표시 움직임을 만들지 않는다.")]
        public List<UnitAnimClip> animClips = new List<UnitAnimClip>();

        [Header("화면 크기")]
        [Tooltip("스프라이트를 몇 배로 키워 그리는가. 보통 1 — 보스만 2(벌이 256이라 코드가 키운다). " +
                 "정수만 쓴다. 값의 원천은 ArtSpec.BossViewScale 이고 생성기가 여기에 옮긴다.")]
        public int viewScale = 1;
    }
}
