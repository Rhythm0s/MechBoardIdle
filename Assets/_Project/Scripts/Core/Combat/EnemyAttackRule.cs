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
    /// ⚠️ **임시 배치다 — 최종형이 아니다**(2026-09-14 · 260911_W03 1-2 명시).
    /// (가)로 가면 **장갑이 보병과 성격이 겹쳐** 셋을 가른 이유가 그 순간 사라진다 —
    /// 둘 다 「걸어와서 접촉으로 때린다」가 된다. 확정된 것은 **성격 구분과 명중 판정뿐**이며,
    /// 장갑이 **돌진인가 느린 근접인가는 미결**이다(W03 7장 2번).
    /// 그 답이 오면 여기에 값을 얹는 것이 아니라 **새 규칙 하나가 선다.**
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
        ///
        /// 🗑️🗑️ **폐기 — 값의 집이 표로 갔다**(2026-09-18 · `ENEMY_DATA` · 플랜 §85-12).
        ///
        /// 이 파일 머리가 이미 적어 둔 그대로다 — 「값을 주면 `EnemyDefinition` 칸에 적히고
        /// 이 기본값은 안 쓰인다 — **교체 자리는 SO 쪽**」. 표가 포격 줄에 **7** 을 적고
        /// 생성기가 그것을 자산에 굽는다. 그래서 **1단(자산)에서 멈추고 여기까지 안 내려온다.**
        ///
        /// ⚠️ **지우지는 않는다** — 표의 칸이 비면 0 이 들어가고 폴백이 다시 산다
        /// (「0 = 안 정함」 규약). 그때 이 수가 없으면 포격이 보병과 같은 사거리로 선다.
        /// </summary>
        public const float ArtilleryRangeAssumed = 7f;

        /// <summary>
        /// 투사체 속도(유닛/초). ⚠️ **가정 · 하나뿐이다** — 병종마다 다른 속도를 두면
        /// 값이 늘어나는 만큼 확정할 것도 늘어난다. 지금은 「날아오는 것이 보인다」가 목표다.
        ///
        /// ✅ **6 → 4.2**(2026-09-18 사용자 확정 · 육안 뒤 「지금 속도의 70%」).
        /// 🗑️ 구 6 폐기 — 적 이동 속도(1.2)의 다섯 배였는데 **눈으로 못 쫓았다.**
        /// 지금은 3.5 배다(1.2 × 3.5 = 4.2). 걸어오는 적보다는 여전히 빠르다.
        ///
        /// 📌 **명중 판정은 그대로 둔다** — 느려지는 쪽이라 「한 틱에 로봇을 뛰어넘는」
        /// 걱정은 멀어졌다(아래 <see cref="Hits"/> 주석이 경고하던 자리는 빨라질 때다).
        ///
        /// 🗑️🗑️ **폐기 — 값의 집이 표로 갔다**(2026-09-18 · `ENEMY_DATA` 포격 줄 4.2).
        ///    위 `ArtilleryRangeAssumed` 와 같은 까닭이며 **지우지 않는 까닭도 같다**(폴백).
        ///
        /// ⚠️ **근거 문장이 낡았다** — 위에 적힌 「적 이동 속도(1.2)의 … 3.5 배」는 **C# 기본값**을
        ///    보고 쓴 것이고, 자산의 이동 속도는 **1.5** 다(4.2 ÷ 1.5 = 2.8 배).
        ///    **값(4.2)은 사용자 확정이라 안 건드린다** — 낡은 것은 이 설명이며 설계가 고칠 자리다.
        /// </summary>
        public const float ProjectileSpeedAssumed = 4.2f;

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
