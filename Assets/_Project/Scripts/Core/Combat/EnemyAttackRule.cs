using MBI.Data;
using UnityEngine;

namespace MBI.Core.Combat
{
    /// <summary>
    /// 병종별 공격 행동 (2026-09-11 · 플랜 §71-33 ② · 공격 패턴 (가)).
    ///
    /// **왜 필요한가.** 09-11 실측에서 적 넷이 `enemyMoveSpeedTbd` · `enemyAttackRangeTbd` ·
    /// `enemyAttackIntervalTbd` 를 **똑같이** 썼다. 화면에서 보병과 포격이 구분되지 않았고,
    /// 공격은 전부 `hp -= atk` **즉발**이라 **날아오는 것이 하나도 없었다.**
    ///
    /// ⚠️ **(가)는 포격 하나만 바꾼다.** 보병·장갑·보스는 현행 그대로다 —
    /// 장갑의 길막(`GridMovement.IsBlocked`)은 손대지 않는다. 한 번에 넷을 다 흔들면
    /// 무엇이 무엇을 바꿨는지 화면에서 못 가른다.
    ///
    /// ⚠️ **여기 값은 전부 가정이다.** `260911_W03` 이 포격 사거리·주기·투사체 속도를 주면
    /// `EnemyDefinition` 칸에 적히고 이 기본값은 안 쓰인다 — **교체 자리는 SO 쪽**이다.
    /// </summary>
    public static class EnemyAttackRule
    {
        /// <summary>
        /// 포격 사거리(유닛). ⚠️ **가정** — 플랜이 「6~8」로 폭을 줬고 그 가운데를 쓴다.
        /// 보병 사거리 1 과 갈라져 보여야 포격이 **멀리서 쏘는 적**으로 읽힌다.
        /// </summary>
        public const float ArtilleryRangeAssumed = 7f;

        /// <summary>
        /// 투사체 속도(유닛/초). ⚠️ **가정 · 하나뿐이다** — 병종마다 다른 속도를 두면
        /// 값이 늘어나는 만큼 확정할 것도 늘어난다. 지금은 「날아오는 것이 보인다」가 목표다.
        ///
        /// 적 이동 속도(1.2)의 다섯 배로 둔다 — 걸어오는 적보다 빠르지만 눈으로 쫓을 수 있다.
        /// </summary>
        public const float ProjectileSpeedAssumed = 6f;

        /// <summary>이 병종이 투사체를 쏘는가. **포격만이다**(그 외는 즉발 현행).</summary>
        public static bool UsesProjectile(EnemyRole role) => role == EnemyRole.Artillery;

        /// <summary>
        /// 병종의 기본 사거리. 0 을 돌려주면 「현행 유지」 — 부르는 쪽이 `CombatTuning` 값을 쓴다.
        /// ⚠️ 여기서 튜닝 SO 를 읽지 않는다. 시뮬 규칙은 순수 값이어야 시험에서 돈다.
        /// </summary>
        public static float RangeOrZero(EnemyRole role) =>
            role == EnemyRole.Artillery ? ArtilleryRangeAssumed : 0f;

        /// <summary>병종의 기본 투사체 속도. 0 = 즉발.</summary>
        public static float ProjectileSpeedOrZero(EnemyRole role) =>
            UsesProjectile(role) ? ProjectileSpeedAssumed : 0f;

        /// <summary>
        /// **명중 = 접촉**(사용자 확정 · §71-33 ②). 맞았는지를 거리 하나로만 본다 —
        /// 궤적 선분 교차나 예측 조준을 넣지 않는다.
        ///
        /// ⚠️ 그래서 **빠른 투사체는 한 틱에 로봇을 뛰어넘을 수 있다.** 지금 속도(6)와
        /// 로봇 반경에서는 한 틱 이동이 반경보다 훨씬 작아 안 생기지만, W03 이 속도를
        /// 크게 올리면 그때는 선분 판정이 필요해진다. **값이 바뀌면 이 줄을 다시 본다.**
        /// </summary>
        public static bool Hits(Vector2 projectile, Vector2 target, float contactRadius) =>
            (target - projectile).sqrMagnitude <= contactRadius * contactRadius;
    }
}
