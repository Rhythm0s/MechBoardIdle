using UnityEngine;

namespace MBI.Core.Combat
{
    /// <summary>
    /// **누적형 드론의 활동 범위 — 플레이어 기준 N** (2026-09-18 사용자 확정).
    ///
    /// ⚠️⚠️ **왜 필요한가.** 종전에는 누적형이 **제 자리에서** 다음 표적을 골랐다
    /// (<c>NearestLivingEnemyWithin(d.Position, d.AttackRange)</c>). 한 번 날아간 드론은
    /// 그 자리에서 또 사거리 안을 보므로 **적을 타고 한없이 멀어질 수 있다** —
    /// 로봇에서 9.2 → 18.4 → 27.6 … 으로 나가는 길이 규칙 안에 열려 있었다.
    /// 화면에서는 「드론이 어디론가 사라진다」로 보이고, 판정에서는 로봇이 위험한 동안
    /// 화력이 딴 데 가 있는 것이 된다.
    ///
    /// 📌 **그래서 잣대를 드론이 아니라 플레이어에게 둔다.** 고르는 표적도, 날아가는 자리도
    /// **로봇에서 N 안**이어야 한다. 붙어 있던 적이 그 밖으로 나가면 **놓는다.**
    ///
    /// ⚠️ **N 은 값이 미정이다**(설계 역기입 자리). 0 이면 **드론 사거리를 그대로 쓴다** —
    /// 지어낸 수를 넣지 않고 이미 있는 값에서 끌기 위해서다.
    ///
    /// ⚠️ **광역형에는 안 건다.** 그쪽은 궤도 반경이 이미 로봇에 묶여 있어 멀어질 길이 없다.
    /// </summary>
    public static class DroneLeashRule
    {
        /// <summary>
        /// 실제로 쓸 범위. <paramref name="leash"/> 가 0 이하면 드론 사거리를 쓴다.
        ///
        /// ⚠️ **둘 다 0 이면 0 이다** — 그때는 「범위가 없다」가 아니라 **아무 데도 못 간다**가
        /// 맞다. 사거리가 0 인 드론은 애초에 칠 것이 없다.
        /// </summary>
        public static float Radius(float leash, float droneAttackRange)
            => leash > 0f ? leash : Mathf.Max(0f, droneAttackRange);

        /// <summary>이 자리가 로봇에서 N 안인가.</summary>
        public static bool Inside(Vector2 robot, Vector2 point, float radius)
            => radius <= 0f ? point == robot : (point - robot).sqrMagnitude <= radius * radius;

        /// <summary>
        /// N 밖이면 **테두리로 끌어당긴다.** 안이면 그대로다.
        ///
        /// 📌 **되돌려 보내지 않는다** — 로봇 쪽으로 왕복하면 드론이 진자처럼 흔들린다.
        /// 테두리에 세워 두면 로봇이 걸어갈 때 자연히 따라온다.
        /// </summary>
        public static Vector2 Clamp(Vector2 robot, Vector2 point, float radius)
        {
            if (radius <= 0f) return robot;
            Vector2 d = point - robot;
            float dist = d.magnitude;
            if (dist <= radius) return point;
            return robot + d / Mathf.Max(dist, 1e-5f) * radius;
        }

        /// <summary>
        /// 이 표적을 아직 붙잡고 있어도 되는가 — **적이 범위 밖으로 나가면 놓는다.**
        ///
        /// ⚠️ 놓는 잣대는 **적의 자리**다(드론 자리가 아니다). 드론은 적을 따라다니므로
        /// 드론으로 재면 「이미 따라 나간 뒤」에야 놓게 된다.
        /// </summary>
        public static bool KeepsTarget(Vector2 robot, Vector2 targetPos, float radius)
            => Inside(robot, targetPos, radius);
    }
}
