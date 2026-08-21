using System.Collections.Generic;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>자동 조종 입력(순수 값 — 씬도 SO도 모른다).</summary>
    public struct AutoPilotContext
    {
        public Vector2 robotPos;
        public IReadOnlyList<CombatEntity> enemies;
        public float arenaRadius;
        public float desiredGap;   // 이 거리보다 가까우면 물러난다(TBD — CombatTuning)
        public float moveSpeed;    // 유닛/초 (TBD)
        public float dt;
    }

    /// <summary>
    /// 자동 조종 카이팅(§5-7). 결정론 — 난수 0, 같은 입력이면 항상 같은 위치.
    ///
    /// 왜 필요한가: 전투가 WASD 조작을 전제하면 **로봇이 가만히 서서 죽는다.** 방치형이라는 주장이
    /// 화면에서 바로 깨지므로, 로봇이 스스로 거리를 유지하는 것 자체가 방치 성립 조건이다.
    ///
    /// 규칙 세 가지:
    ///   1. 최근접 위협이 <see cref="AutoPilotContext.desiredGap"/>보다 가까우면 반대 방향으로 물러난다
    ///   2. 아레나 경계를 넘으면 **멈추지 않고 접선 방향으로 미끄러진다** — 경계에 클램프만 하면
    ///      구석에 몰려 붙박이가 되고, 그 자리에서 둘러싸여 죽는다
    ///   3. 위협이 없으면 원점으로 돌아온다(다음 스폰을 중앙에서 맞이한다)
    ///
    /// 사거리는 아레나 전체를 덮으므로(robotAttackRangeTbd) 이 이동은 **생존 축 전용**이고
    /// DPS나 §9 예산식에 영향을 주지 않는다.
    /// </summary>
    public static class AutoPilotPolicy
    {
        /// <summary>최근접 생존 적 방향(정규화). 없으면 zero. 동률은 낮은 인덱스 우선(결정론).</summary>
        public static Vector2 ThreatDirection(Vector2 pos, IReadOnlyList<CombatEntity> enemies)
        {
            CombatEntity nearest = NearestLiving(pos, enemies, out float _);
            if (nearest == null) return Vector2.zero;

            Vector2 delta = nearest.position - pos;
            return delta.sqrMagnitude > 1e-8f ? delta.normalized : Vector2.right; // 완전 겹침이면 임의의 고정 방향
        }

        /// <summary>이번 프레임의 목표 위치.</summary>
        public static Vector2 NextPosition(in AutoPilotContext ctx)
        {
            float step = ctx.moveSpeed * ctx.dt;
            if (step <= 0f) return ctx.robotPos;

            Vector2 dir = DesiredDirection(ctx);
            if (dir == Vector2.zero) return ctx.robotPos;

            Vector2 desired = ctx.robotPos + dir * step;
            if (ctx.arenaRadius <= 0f || desired.magnitude <= ctx.arenaRadius) return desired;

            // 경계 밖 — 접선으로 미끄러진다(클램프만 하면 구석에 박힌다).
            Vector2 outward = ctx.robotPos.sqrMagnitude > 1e-8f ? ctx.robotPos.normalized : dir;
            Vector2 tangent = new Vector2(-outward.y, outward.x);
            if (Vector2.Dot(tangent, dir) < 0f) tangent = -tangent; // 가려던 쪽에 가까운 접선을 고른다

            Vector2 slid = ctx.robotPos + tangent * step;
            return slid.magnitude > ctx.arenaRadius ? slid.normalized * ctx.arenaRadius : slid;
        }

        // 물러날지 돌아올지 결정.
        private static Vector2 DesiredDirection(in AutoPilotContext ctx)
        {
            CombatEntity nearest = NearestLiving(ctx.robotPos, ctx.enemies, out float dist);

            if (nearest == null)
            {
                // 위협 없음 → 원점 복귀. 이미 원점이면 가만히.
                return ctx.robotPos.sqrMagnitude > 1e-8f ? -ctx.robotPos.normalized : Vector2.zero;
            }

            if (dist >= ctx.desiredGap) return Vector2.zero; // 충분히 멀다 — 굳이 움직이지 않는다

            Vector2 away = ctx.robotPos - nearest.position;
            return away.sqrMagnitude > 1e-8f ? away.normalized : Vector2.right;
        }

        private static CombatEntity NearestLiving(Vector2 pos, IReadOnlyList<CombatEntity> enemies, out float distance)
        {
            distance = float.PositiveInfinity;
            if (enemies == null) return null;

            CombatEntity best = null;
            for (int i = 0; i < enemies.Count; i++)
            {
                CombatEntity e = enemies[i];
                if (e == null || !e.IsAlive) continue;

                float d = (e.position - pos).magnitude;
                if (d >= distance) continue; // 동률이면 앞선 인덱스 유지 = 결정론
                distance = d;
                best = e;
            }
            return best;
        }
    }
}
