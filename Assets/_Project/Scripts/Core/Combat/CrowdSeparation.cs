using System.Collections.Generic;
using UnityEngine;

namespace MBI.Core.Combat
{
    /// <summary>
    /// **겹친 적끼리 서로 민다** (2026-09-21 사용자 확정 · 육안 ⑤ 「몬스터가 뭉쳐 선다」).
    ///
    /// 🗑️ **구 규칙 「적끼리 밀지 않는다」(2026-09-16) 폐기.** 그 규칙 아래에서 적은
    /// 앞도 양옆도 막히면 **할 수 있는 것이 없었다** — 4방향 · 밀어내기 없음 ·
    /// 경로 탐색 없음이 겹쳐 무리 뒤쪽이 스테이지 내내 굳었다.
    ///
    /// ⚠️⚠️ **일반 몹은 로봇을 안 민다**(사용자 확정 09-21). 밀면 플레이어가 무리에
    /// 떠밀려 다니고, 「길막」이라는 규칙 자체가 뜻을 잃는다.
    ///
    /// ✅ **보스만 예외다**(2026-09-30 사용자 확정 ⑫) — <see cref="PushRobot"/>.
    /// 보스는 한 마리뿐이라 「떠밀려 다닌다」가 안 생기고, 덩치가 밀어붙이는 것이
    /// 보스전에서 읽어야 할 압박이다.
    ///
    /// ⚠️ **밀기는 이동이 아니다.** 겹친 만큼을 조금씩 푸는 것이고, 한 틱에 풀 수 있는
    /// 양에 <see cref="MaxPerTick"/> 상한이 있다 — 없으면 빽빽한 무리가 **터지듯** 튄다.
    ///
    /// 📌 **판정에 안 닿는다** — 자리만 옮긴다. 사거리·피해는 그 자리를 보고 따로 난다.
    /// </summary>
    public static class CrowdSeparation
    {
        /// <summary>한 틱에 한 마리가 밀려날 수 있는 최대(칸). ⚠️ 가정.</summary>
        public const float MaxPerTick = 0.35f;

        /// <summary>
        /// 겹친 쌍마다 서로 반대로 민다. <paramref name="strength"/> 가 0 이면
        /// **아무것도 안 한다**(구 거동으로 되돌리는 값 하나).
        ///
        /// ⚠️ **한 번에 다 풀지 않는다** — 겹친 깊이의 절반씩을 서로 나눠 밀고,
        /// <paramref name="strength"/> 로 그 세기를 조절한다. 다음 틱에 또 민다.
        /// </summary>
        public static void Resolve(IReadOnlyList<CombatEntity> enemies, float strength, float dt)
        {
            if (enemies == null || strength <= 0f || dt <= 0f) return;

            for (int i = 0; i < enemies.Count; i++)
            {
                CombatEntity a = enemies[i];
                if (a == null || !a.IsAlive) continue;

                for (int j = i + 1; j < enemies.Count; j++)
                {
                    CombatEntity b = enemies[j];
                    if (b == null || !b.IsAlive) continue;

                    Vector2 d = b.position - a.position;
                    float want = a.radius + b.radius;
                    float dist = d.magnitude;
                    if (dist >= want) continue;               // 안 겹쳤다

                    // 정확히 포개진 경우 방향이 없다 — 자리로 흔들어 가른다.
                    // ⚠️ `Random` 을 안 쓴다: 같은 판이 판마다 다른 그림을 내면 시험이 못 잰다.
                    Vector2 dir = dist > 1e-5f ? d / dist
                        : new Vector2(((i * 31 + j * 17) % 7) - 3f, ((i * 13 + j * 7) % 5) - 2f)
                            .normalized;
                    if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;

                    float overlap = want - dist;
                    float push = Mathf.Min(overlap * 0.5f * strength * dt, MaxPerTick);
                    if (push <= 0f) continue;

                    a.position -= dir * push;
                    b.position += dir * push;
                }
            }
        }

        /// <summary>
        /// **보스가 로봇을 민다** (2026-09-30 사용자 확정 ⑫).
        ///
        /// ⚠️ **미는 쪽은 보스 하나다** — <see cref="Resolve"/> 와 달리 절반씩 나누지 않고
        /// **로봇만 밀려난다.** 보스가 로봇에 밀려 자리를 잃으면 「덩치가 밀어붙인다」가
        /// 안 읽히고, 보스가 둘일 수 없으므로 서로 미는 축도 필요 없다.
        ///
        /// ⚠️ **세기와 상한은 적끼리와 같은 것을 쓴다**(가정) — 같은 손짓이므로 값이 둘로
        /// 갈리면 만질 때마다 어긋난다(지침 §7). 0 이면 아무것도 안 한다(구 거동).
        ///
        /// 📌 **판정에 안 닿는다** — 자리만 옮긴다. 접촉 피해는 그 자리를 보고 따로 난다.
        /// </summary>
        public static void PushRobot(CombatEntity robot, IReadOnlyList<CombatEntity> enemies,
            float strength, float dt)
        {
            if (robot == null || !robot.IsAlive) return;
            if (enemies == null || strength <= 0f || dt <= 0f) return;

            for (int i = 0; i < enemies.Count; i++)
            {
                CombatEntity e = enemies[i];
                if (e == null || !e.IsAlive || !e.isBoss) continue;   // 보스만 민다

                Vector2 d = robot.position - e.position;
                float want = robot.radius + e.radius;
                float dist = d.magnitude;
                if (dist >= want) continue;                            // 안 겹쳤다

                // 포개진 경우 — `Resolve` 와 같은 이유로 난수를 안 쓴다(결정론).
                Vector2 dir = dist > 1e-5f ? d / dist : Vector2.right;

                float overlap = want - dist;
                // ⚠️ **절반이 아니다** — 겹친 만큼을 로봇 혼자 진다(보스는 안 물러난다).
                float push = Mathf.Min(overlap * strength * dt, MaxPerTick);
                if (push <= 0f) continue;

                robot.position += dir * push;
            }
        }
    }
}
