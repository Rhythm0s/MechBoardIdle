using System.Collections.Generic;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 탑뷰 이동 규칙(전투 시스템 문서「자동 전투 구현 사양」, 2026-08-26 신설).
    ///
    /// 확정 규칙:
    ///   - 이동은 **4방향**. 대각선은 두 방향을 번갈아 낸다.
    ///   - 충돌은 **통과하지 않고 밀어내지도 않는다.** 막히면 멈춘다.
    ///   - **경로 탐색 없음.** 돌아가지 않는다.
    ///
    /// 경로 탐색을 넣지 않는 이유가 밸런스다: 길을 찾아 돌아가면 **장갑형 길막이 사라진다.**
    /// 막히는 것이 그 몬스터의 역할이므로, 우회하는 순간 고방어 유닛의 존재 이유가 없어진다.
    /// </summary>
    public static class GridMovement
    {
        /// <summary>
        /// 4방향 한 걸음. 남은 델타가 큰 축을 먼저 밀어 **두 축을 번갈아** 쓰게 된다 —
        /// 한 축을 끝까지 밀고 꺾으면 ㄱ자로 움직여 대각선으로 읽히지 않는다.
        /// 상태를 들지 않으므로 결정론이 유지된다(같은 입력이면 같은 걸음).
        /// </summary>
        public static Vector2 Step(Vector2 from, Vector2 to, float distance)
        {
            if (distance <= 0f) return from;

            Vector2 delta = to - from;
            float ax = Mathf.Abs(delta.x), ay = Mathf.Abs(delta.y);
            if (ax <= 1e-6f && ay <= 1e-6f) return from;

            // 남은 거리가 큰 축을 민다. 두 축이 비슷해지면 매 걸음 축이 바뀌어 계단이 된다.
            bool horizontal = ax >= ay;
            float remaining = horizontal ? ax : ay;
            float step = Mathf.Min(distance, remaining); // 오버슛 방지

            return horizontal
                ? new Vector2(from.x + Mathf.Sign(delta.x) * step, from.y)
                : new Vector2(from.x, from.y + Mathf.Sign(delta.y) * step);
        }

        /// <summary>두 원이 겹치는가. 반경이 0이면 겹침 판정을 하지 않는다(테스트 기본값 보호).</summary>
        public static bool Overlaps(Vector2 a, float radiusA, Vector2 b, float radiusB)
        {
            float min = radiusA + radiusB;
            if (min <= 0f) return false;
            return (b - a).sqrMagnitude < min * min;
        }

        /// <summary>
        /// 이동 가능한가. **막히면 멈춘다** — 밀어내지도, 돌아가지도 않는다.
        /// 자기 자신과 죽은 개체는 막지 않는다.
        /// </summary>
        /// <summary>
        /// 주 축이 막혔을 때 **부 축으로 한 칸** — 둘 다 막히면 <c>null</c>
        /// (2026-09-16 사용자 확정 · 플랜 §74-12 B).
        ///
        /// ⚠️⚠️ **왜 바꿨나.** <see cref="Step"/> 은 남은 거리가 큰 축 **하나만** 낸다.
        /// 그 한 칸이 막히면 부르는 쪽이 **그대로 멈췄고**, 다른 축이 비어 있어도 안 봤다.
        /// 실측(S1 하네스 · 120초): 살아 있는 83 기 중 **81 기가 굳고 사거리 안은 1 기**였다.
        /// 로봇은 끝까지 한 번에 한 마리만 상대했다.
        ///
        /// 🗑️ **구 주석 「돌아가지도 않는다 · 경로 탐색을 넣으면 장갑형 길막이 사라지므로
        /// 우회는 금지」는 폐기**(2026-09-16 사용자 확정). 그 규칙은 **장갑형 하나가 길을
        /// 막는** 그림으로 쓰였는데, 보병 120 기에서는 **저희끼리 막아 97%가 굳었다.**
        ///
        /// ⚠️ **경로 탐색이 아니다.** 한 칸짜리 곁눈질이고, 둘 다 막히면 **그대로 선다** —
        /// 길막은 그대로 살아 있다.
        /// </summary>
        public static Vector2? StepOrSide(Vector2 from, Vector2 to, float distance,
            float radius, CombatEntity self, IReadOnlyList<CombatEntity> others, CombatEntity robot,
            ref float hold, ref Vector2 heldDir, float holdSeconds, float dt)
        {
            if (hold > 0f) hold -= dt;

            Vector2 main = Step(from, to, distance);
            if (main != from && !IsBlocked(main, radius, self, others, robot))
            {
                hold = 0f;                 // 주 축이 뚫렸으면 곁눈질은 끝이다
                heldDir = Vector2.zero;
                return main;
            }

            // 부 축 — 주 축이 민 방향과 **다른 축**으로 한 칸. 방향은 **표적 쪽**이다.
            Vector2 delta = to - from;
            bool mainWasHorizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);

            float other = mainWasHorizontal ? delta.y : delta.x;
            if (Mathf.Abs(other) <= 1e-6f) return null;   // 그 축으로는 갈 이유가 없다

            // ⚠️⚠️ **한 번 고른 쪽을 잠깐 붙든다**(2026-09-16 설계 완화 · §74-12 B).
            //    매 틱 다시 고르면 두 축이 비슷할 때 좌우가 프레임마다 뒤집혀
            //    **제자리에서 떠는 것처럼** 보인다 — 얼굴 방향에서 겪은 병과 같다.
            Vector2 dir = mainWasHorizontal
                ? new Vector2(0f, Mathf.Sign(other))
                : new Vector2(Mathf.Sign(other), 0f);

            if (hold > 0f && heldDir != Vector2.zero) dir = heldDir;

            float step = Mathf.Min(distance, Mathf.Abs(other));
            Vector2 side = from + dir * step;

            if (IsBlocked(side, radius, self, others, robot)) return null;

            heldDir = dir;
            if (hold <= 0f) hold = holdSeconds;
            return side;
        }

        public static bool IsBlocked(Vector2 target, float radius, CombatEntity self,
            IReadOnlyList<CombatEntity> others, CombatEntity robot)
        {
            if (others != null)
            {
                for (int i = 0; i < others.Count; i++)
                {
                    CombatEntity o = others[i];
                    if (o == null || o == self || !o.IsAlive) continue;
                    if (Overlaps(target, radius, o.position, o.radius)) return true;
                }
            }

            if (robot != null && robot != self && robot.IsAlive &&
                Overlaps(target, radius, robot.position, robot.radius)) return true;

            return false;
        }
    }
}
