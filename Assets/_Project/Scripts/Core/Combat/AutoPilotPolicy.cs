using System.Collections.Generic;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>자동 조종 입력(순수 값 — 씬도 SO도 모른다).</summary>
    public struct AutoPilotContext
    {
        public Vector2 robotPos;
        public IReadOnlyList<CombatEntity> enemies;
        /// <summary>
        /// ⚠️ **폐기 — 아무도 안 본다**(2026-09-11 사용자 확정 · §71-28 1).
        ///
        /// 이동 가능 반경이었다. 전장이 **로봇을 따라다니는 판**이 되면서 경계가 없어졌고,
        /// 링은 이제 <see cref="SpawnRingRule"/> 이 **로봇 기준**으로 낸다.
        ///
        /// **자리는 남긴다** — 부르는 시험이 스물이고, 값을 안 보는 것이 곧 규칙이라
        /// 지우는 것보다 **안 보는 것을 적어 두는 편**이 낫다.
        /// </summary>
        public float arenaRadius;
        public float attackRange;  // 이 거리 안이면 칠 수 있다
        public float moveSpeed;    // 유닛/초 (TBD)
        public float dt;

        /// <summary>
        /// **사거리 안에 이만큼 넘게 있으면 제자리**에서 쏜다 (2026-09-15 사용자 확정 · 가정 3).
        ///
        /// ⚠️ **0 이면 종전 규칙**이다 — 사거리 안에 하나라도 있으면 제자리.
        /// 시험 스물이 이 구조체를 만들므로 **0 을 종전으로 두어야** 안 건드린 시험이 안 깨진다.
        /// </summary>
        public int holdWhenMoreThan;
    }

    /// <summary>
    /// 자동 조종(전투 시스템 문서「자동 전투 구현 사양」).
    /// 결정론 — 난수 0, 같은 입력이면 항상 같은 위치.
    ///
    /// 규칙은 둘뿐이다:
    ///   1. **사거리 안에 <c>holdWhenMoreThan</c> 기 넘게 있으면 → 제자리 사격.**
    ///   2. **그보다 적으면 → 가장 가까운 무리로 4방향 이동.**
    ///
    /// ⚠️ **「사거리 안에 하나라도 있으면 제자리」에서 바뀌었다**(2026-09-15 사용자 확정).
    /// 구 규칙에서는 적이 많을수록 **최근접이 늘 사거리 안**이라 로봇이 한 걸음도 안 걸었다 —
    /// S1 실측 **걸은 틱 0 / 1956**. 사거리를 줄여도(9.2) 스폰 띠를 넓혀도(4~14) 그대로였다.
    /// 뿌리가 거리가 아니라 **「최근접만 본다」** 였다.
    ///
    /// ⚠️ **「무리」 판정은 구현 재량**(사용자 위임). **최근접 적 둘레**를 무리로 본다 —
    /// 그 적에서 <see cref="AutoPilotContext.attackRange"/> 안에 있는 것들의 **무게중심**으로 간다.
    /// 반경을 새로 만들지 않고 사거리를 쓰는 이유는 그것이 이미
    /// **「한 자리에서 칠 수 있는 범위」**를 뜻하기 때문이다 — 무리의 뜻과 같다.
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
        /// <summary>그 자리에서 <paramref name="radius"/> 안에 있는 **생존 적 수**.</summary>
        public static int CountWithin(Vector2 pos, IReadOnlyList<CombatEntity> enemies, float radius)
        {
            if (enemies == null || radius <= 0f) return 0;

            float r2 = radius * radius;
            int n = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                CombatEntity e = enemies[i];
                if (e == null || e.hp <= 0f) continue;
                if ((e.position - pos).sqrMagnitude <= r2) n++;
            }
            return n;
        }

        /// <summary>
        /// **가장 가까운 무리의 무게중심** — <paramref name="seed"/> 둘레
        /// <paramref name="radius"/> 안에 있는 생존 적들의 평균 자리.
        ///
        /// ⚠️ **결정론이다** — 난수도, 순서 의존도 없다(평균은 순서를 안 탄다).
        /// 무리가 하나뿐이면 그 적의 자리 그대로다.
        /// </summary>
        public static Vector2 ClusterCenter(CombatEntity seed, IReadOnlyList<CombatEntity> enemies,
            float radius)
        {
            if (seed == null) return Vector2.zero;
            if (enemies == null || radius <= 0f) return seed.position;

            float r2 = radius * radius;
            Vector2 sum = Vector2.zero;
            int n = 0;

            for (int i = 0; i < enemies.Count; i++)
            {
                CombatEntity e = enemies[i];
                if (e == null || e.hp <= 0f) continue;
                if ((e.position - seed.position).sqrMagnitude > r2) continue;
                sum += e.position;
                n++;
            }
            return n > 0 ? sum / n : seed.position;
        }

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

            // **사거리 안에 충분히 많으면 제자리**에서 쏜다.
            if (CountWithin(ctx.robotPos, ctx.enemies, ctx.attackRange) > ctx.holdWhenMoreThan)
                return ctx.robotPos;

            // 사거리 안이 성기다 → **가장 가까운 무리**로 간다. 그 무리가 이미 발밑이면 제자리.
            Vector2 target = ClusterCenter(nearest, ctx.enemies, ctx.attackRange);
            if ((target - ctx.robotPos).sqrMagnitude <= 1e-8f) return ctx.robotPos;

            Vector2 next = GridMovement.Step(ctx.robotPos, target, step);

            // ⚠️ **아레나 클램프를 걷었다**(2026-09-11 사용자 확정 · 플랜 §71-28 1).
            //
            // 종전에는 `magnitude > arenaRadius` 면 그 걸음을 버렸다 — **월드 원점 기준
            // 반경 6** 의 원 안에 로봇을 가둔 것이다. 전장이 아레나가 아니라 **로봇을
            // 따라다니는 판**이 되면서 그 원의 근거가 사라졌다.
            //
            // 나가지 못하게 하던 이유(접선 미끄러짐)는 **경계가 있을 때만** 생기는 문제다.
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
