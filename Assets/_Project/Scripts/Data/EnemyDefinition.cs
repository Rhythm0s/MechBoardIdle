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
    }
}
