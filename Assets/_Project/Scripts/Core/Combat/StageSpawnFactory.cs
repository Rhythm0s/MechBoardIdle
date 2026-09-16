using System;
using System.Collections.Generic;
using MBI.Data;

namespace MBI.Core.Combat
{
    /// <summary>
    /// 스테이지 하나를 **적 목록으로** 푸는 유일한 자리 (2026-09-16).
    ///
    /// ⚠️⚠️ **왜 빼냈나.** S1 클리어를 끝까지 재는 하네스가 필요해졌는데, 하네스가 적을
    /// 직접 지으면 **게임이 세우는 판과 다른 판을 재게 된다.** 09-15 에 이미 겪은 모양이다 —
    /// 프로브는 초록인데 화면은 낡았고, 프로브가 사람이 지나는 문을 안 지났기 때문이었다.
    ///
    /// 📌 그래서 **재는 쪽과 노는 쪽이 같은 함수를 부른다.** 여기가 바뀌면 둘 다 바뀐다.
    ///
    /// ⚠️ **화면의 값은 안 넣는다** — 배율·그림은 `onDefinition` 으로 부른 쪽에 넘기고,
    /// 스폰 자체에는 규칙 값만 담는다. `CombatSimulation` 은 순수 규칙이어야 한다.
    /// </summary>
    public static class StageSpawnFactory
    {
        /// <summary>
        /// 자산 값 → 병종 규칙 → 튜닝 폴백. **셋 다 이 차례다**(2026-09-11 · §71-33 ②).
        ///
        /// ⚠️ 종전에는 넷이 튜닝 하나를 똑같이 써서 **보병과 포격이 같이 움직였다.**
        /// </summary>
        public static float Pick(float fromAsset, float fromRule, float fallback)
        {
            if (fromAsset > 0f) return fromAsset;
            if (fromRule > 0f) return fromRule;
            return fallback;
        }

        private static float RoleRange(EnemyDefinition def)
            => def != null ? EnemyAttackRule.RangeOrZero(def.role) : 0f;

        private static float RoleProjectile(EnemyDefinition def)
            => def != null ? EnemyAttackRule.ProjectileSpeedOrZero(def.role) : 0f;

        /// <summary>몸 크기는 HP 로 가른다 — 보스만 다른 규격이다.</summary>
        public static float EnemySize(float maxHp)
            => maxHp >= 1000f ? ArtSpec.BossViewSize : ArtSpec.MonsterSize;

        /// <summary>
        /// 이 스테이지가 내보낼 적 전부.
        /// </summary>
        /// <param name="onDefinition">
        /// 표시명과 그 정의를 부른 쪽에 알린다 — 화면이 배율·그림을 여기서 받아 간다.
        /// 재기만 하는 쪽은 <c>null</c> 로 둔다.
        /// </param>
        public static List<EnemySpawn> Build(
            StageDefinition stage,
            IReadOnlyList<EnemyDefinition> catalog,
            CombatTuning tuning,
            Action<string, EnemyDefinition> onDefinition = null)
        {
            var spawns = new List<EnemySpawn>();
            if (stage == null || stage.composition == null || tuning == null) return spawns;

            var byKey = new Dictionary<string, EnemyDefinition>();
            if (catalog != null)
                foreach (EnemyDefinition e in catalog)
                    if (e != null && !string.IsNullOrEmpty(e.enemyKey)) byKey[e.enemyKey] = e;

            foreach (StageComposition c in stage.composition)
            {
                byKey.TryGetValue(c.enemyKey ?? string.Empty, out EnemyDefinition def);
                float atk = def != null ? def.atk : 0f;
                string label = def != null ? def.displayName : c.enemyKey;

                onDefinition?.Invoke(label, def);

                for (int i = 0; i < c.count; i++)
                {
                    spawns.Add(new EnemySpawn
                    {
                        label = label,
                        hp = c.hp,
                        def = c.def,
                        atk = atk,
                        moveSpeed = Pick(def != null ? def.moveSpeed : 0f, 0f,
                            tuning.enemyMoveSpeedTbd),
                        attackRange = Pick(def != null ? def.attackRange : 0f,
                            RoleRange(def), tuning.enemyAttackRangeTbd),
                        attackInterval = Pick(def != null ? def.attackInterval : 0f, 0f,
                            tuning.enemyAttackIntervalTbd),
                        // ⚠️ 투사체만 마지막 단이 **0**이다 — 즉발이 현행이라 폴백이 따로 없다.
                        projectileSpeed = Pick(def != null ? def.projectileSpeed : 0f,
                            RoleProjectile(def), 0f),
                        radius = EnemySize(c.hp) * 0.5f,
                    });
                }
            }

            return spawns;
        }
    }
}
