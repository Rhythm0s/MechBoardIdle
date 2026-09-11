using UnityEngine;

namespace MBI.Core
{
    /// <summary>전투 진영.</summary>
    public enum Faction
    {
        Robot,
        Enemy
    }

    /// <summary>
    /// 전투 런타임 엔티티(순수 C#) — 로봇 1기 + 적 다수. 위치·HP·(적)이동·공격 상태를 담는다.
    /// SO(정의)와 분리된 가변 상태 — CombatSimulation이 소유·갱신. Unity 없이 검증 가능.
    /// </summary>
    public sealed class CombatEntity
    {
        public Faction faction;
        public string label;
        public Vector2 position;

        public float hp;
        public float maxHp;
        public float def;          // 적: composition def(히트당 뺄셈). 로봇: 0(방어 스탯 없음 §9).
        public float radius;       // 충돌 반경 — 완전 겹침 방지(분리 처리). 0이면 분리 없음.

        // 적 전용 이동·공격
        public float atk;
        public float moveSpeed;
        public float attackRange;
        public float attackInterval;
        public float attackCooldown; // 남은 재사용 대기(초)

        /// <summary>
        /// 투사체 속도(유닛/초). **0 = 즉발**(현행 · 보병·장갑·보스).
        /// 0 보다 크면 공격이 그 자리에서 hp 를 깎지 않고 **날아가는 것 하나를 낳는다**
        /// (2026-09-11 · §71-33 ② 공격 패턴 (가) · 포격만).
        /// </summary>
        public float projectileSpeed;

        public bool IsAlive => hp > 0f;
    }
}
