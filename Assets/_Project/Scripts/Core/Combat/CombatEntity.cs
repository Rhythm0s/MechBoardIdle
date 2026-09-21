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
        /// <summary>
        /// 부 축으로 곁눈질한 방향과 **남은 유지 시간** (2026-09-16 설계 완화 · §74-12 B).
        ///
        /// ⚠️⚠️ **한 번 고른 쪽을 잠깐 붙든다.** 매 틱 다시 고르면 두 축이 비슷할 때
        /// 좌우가 프레임마다 뒤집혀 **제자리에서 떠는 것처럼** 보인다 —
        /// 얼굴 방향에서 이미 같은 병을 겪었다(조준 관성).
        ///
        /// ⚠️ 유지 시간은 **가정**이며 값은 `CombatTuning` 에 있다(코드가 안 들고 있다).
        /// </summary>
        public float sideStepHold;

        /// <summary>붙들고 있는 곁눈질 방향(단위 벡터). 0 이면 없다.</summary>
        public Vector2 sideStepDir;

        /// <summary>
        /// **곁눈질로 더 갈 수 있는 거리**(칸 · 2026-09-19 사용자 확정 · 리허설 1 ② 후속).
        ///
        /// 🗑️ **「한 칸」 폐기** — 종전 곁눈질은 부 축으로 **표적까지 남은 양**까지만 갔다.
        /// 그래서 적이 표적과 **거의 한 축에 서면** 남은 양이 0 에 가까워 곁눈질이
        /// 제자리걸음이 되고, 앞이 막히면 **그대로 굳었다**(09-19 실측 — 굳음 100% 길막).
        ///
        /// 이제 **예산**으로 잰다. 주 축이 뚫리면 가득 채우고, 곁눈질로 간 만큼 깎는다.
        /// 예산이 다하면 그대로 선다 — **무한정 옆으로 흐르지 않는다**(그러면 적이
        /// 표적을 두고 하염없이 미끄러진다).
        ///
        /// ⚠️ 상한은 **가정**이며 값은 `CombatTuning` 에 있다(코드가 안 들고 있다).
        /// ⚠️ 음수로 시작하지 않는다 — 0 이면 「아직 안 받았다」가 아니라 「다 썼다」이므로,
        ///    처음 한 번은 주 축이 뚫리는 프레임에 채워진다.
        /// </summary>
        public float sideStepBudget;

        /// <summary>
        /// 곁눈질 예산을 **한 번이라도 받아 봤는가** (2026-09-21 사용자 육안 ⑤).
        ///
        /// ⚠️⚠️ 이것이 없으면 <see cref="sideStepBudget"/> 의 0 이 **두 가지 뜻**을 갖는다 —
        /// 「다 썼다」와 「아직 못 받았다」. 채우는 곳이 「주 축이 뚫렸을 때」 하나뿐이라
        /// **태어날 때부터 막힌 적은 영영 0** 이었고, 무리 뒤쪽이 통째로 굳었다.
        /// </summary>
        public bool sideStepPrimed;

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
