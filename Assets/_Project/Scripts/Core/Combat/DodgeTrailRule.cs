using UnityEngine;

namespace MBI.Core.Combat
{
    /// <summary>
    /// 회피 잔상과 줄기의 **자리 셈** (2026-09-18 사용자 확정 · 참고 이미지 = 대시 잔상과
    /// 이어지는 빛줄기 · ⚠️ 연출값은 가정 · 설계 사후 `vfx_booster` 행 개정).
    ///
    /// ⚠️ **순수 계산만 둔다** — 그림도 시간도 없다. 그래야 시험이 **자리를 잴 수 있다**
    /// (`MonoBehaviour` 안에 넣으면 씬을 세워야 재진다).
    ///
    /// ⚠️⚠️ **판정에 손대지 않는다.** 회피의 무적·거리·시간은 `DodgeMotion`·`DodgeSystem` 이
    /// 그대로 쥔다 — 여기는 **지나간 자리를 몇 군데로 나눌 것인가**만 낸다.
    /// </summary>
    public static class DodgeTrailRule
    {
        /// <summary>잔상 장수 — ⚠️ 가정(사용자 「2~3장」의 가운데).</summary>
        public const int DefaultAfterimages = 3;

        /// <summary>
        /// 잔상이 설 자리들. **시작점과 끝점 사이를 등간격으로 나눈다.**
        ///
        /// ⚠️ **끝점에는 안 둔다** — 거기에는 로봇 본체가 서 있다. 겹치면 잔상이
        /// 「한 장 더 그린 로봇」으로 보이고 움직임이 안 읽힌다.
        /// ⚠️ **시작점에는 둔다** — 「어디서 출발했나」가 잔상의 내용물이다.
        ///
        /// 그래서 n 장이면 u = 0, 1/n, 2/n … (n-1)/n 이다.
        /// </summary>
        public static Vector2[] AfterimagePositions(Vector2 from, Vector2 to, int count)
        {
            if (count <= 0) return new Vector2[0];

            var spots = new Vector2[count];
            for (int i = 0; i < count; i++)
                spots[i] = Vector2.Lerp(from, to, i / (float)count);
            return spots;
        }

        /// <summary>
        /// 장수마다의 **옅기** — 뒤엣것(시작점 쪽)일수록 옅다.
        /// 0 번이 가장 옅고 마지막이 가장 진하다 — 「사라져 가는 자국」의 방향이다.
        /// </summary>
        public static float AfterimageAlpha(int index, int count, float peak)
        {
            if (count <= 1) return peak;
            return peak * (index + 1) / count;
        }

        /// <summary>줄기의 가운데 — 시작점과 끝점의 한가운데다.</summary>
        public static Vector2 StreakCenter(Vector2 from, Vector2 to) => (from + to) * 0.5f;

        /// <summary>줄기의 길이. 0 이면 그릴 것이 없다(제자리 회피).</summary>
        public static float StreakLength(Vector2 from, Vector2 to) => (to - from).magnitude;

        /// <summary>줄기가 누울 각도(도). 시작점에서 끝점을 본다.</summary>
        public static float StreakDegrees(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            if (d.sqrMagnitude <= 0f) return 0f;
            return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        }
    }
}
