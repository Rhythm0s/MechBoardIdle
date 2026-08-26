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
        public float attackRange;  // 이 거리 안이면 제자리 사격
        public float moveSpeed;    // 유닛/초 (TBD)
        public float dt;
    }

    /// <summary>
    /// 자동 조종(전투 시스템 문서「자동 전투 구현 사양」).
    /// 결정론 — 난수 0, 같은 입력이면 항상 같은 위치.
    ///
    /// 규칙은 둘뿐이다:
    ///   1. **사거리 안 → 제자리 사격.** 다가가지 않는다.
    ///   2. **사거리 밖 → 최근접 적을 향해 4방향 이동.**
    ///
    /// ⚠️ **카이팅은 넣지 않는다**(2026-08-26 판정). 로봇은 사거리 유지를 위해 물러나지 않는다.
    ///   ① 접근당하는 상황의 답은 **회피 시스템(부스터 노드)**이며 카이팅은 그것과 중복이다.
    ///   ② 카이팅은 자원을 쓰지 않아 **추진제·부스터 노드의 존재 이유를 없앤다.**
    ///   ③ 전투력 출처가 물류가 아니라 이동 로직으로 옮겨간다 —
    ///      표적 선택·경로 탐색에서 판단을 걷어낸 것과 같은 형태의 오염이다.
    ///
    /// 그래서 여기에는 후퇴도, 원점 복귀도, 경계 접선 미끄러짐도 없다. 접근 하나뿐이다.
    /// </summary>
    public static class AutoPilotPolicy
    {
        /// <summary>최근접 생존 적 방향(정규화). 없으면 zero. 동률은 낮은 인덱스 우선(결정론).</summary>
        public static Vector2 ThreatDirection(Vector2 pos, IReadOnlyList<CombatEntity> enemies)
        {
            CombatEntity nearest = NearestLiving(pos, enemies, out float _);
            if (nearest == null) return Vector2.zero;

            Vector2 delta = nearest.position - pos;
            return delta.sqrMagnitude > 1e-8f ? delta.normalized : Vector2.right;
        }

        /// <summary>
        /// 이번 프레임의 목표 위치. 사거리 안이거나 적이 없으면 **제자리**다.
        /// 충돌 판정은 호출자가 한다(시뮬이 적 목록과 반경을 들고 있다).
        /// </summary>
        public static Vector2 NextPosition(in AutoPilotContext ctx)
        {
            float step = ctx.moveSpeed * ctx.dt;
            if (step <= 0f) return ctx.robotPos;

            CombatEntity nearest = NearestLiving(ctx.robotPos, ctx.enemies, out float dist);
            if (nearest == null) return ctx.robotPos;          // 적 없음 → 가만히(원점 복귀 없음)
            if (dist <= ctx.attackRange) return ctx.robotPos;  // 사거리 안 → 제자리 사격

            // 사거리 밖 → 최근접 적을 향해 4방향으로 접근.
            Vector2 next = GridMovement.Step(ctx.robotPos, nearest.position, step);

            // 아레나 밖으로는 나가지 않는다. 적이 아레나 안에 있어 보통은 걸리지 않지만,
            // 걸리면 그 걸음을 버린다 — 접선으로 미끄러지면 그것이 곧 이동 판단이 된다.
            if (ctx.arenaRadius > 0f && next.magnitude > ctx.arenaRadius) return ctx.robotPos;

            return next;
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
                if (d >= distance) continue; // 동률이면 앞선 인덱스 유지 = 먼저 등장한 쪽(결정론)
                distance = d;
                best = e;
            }
            return best;
        }
    }
}
