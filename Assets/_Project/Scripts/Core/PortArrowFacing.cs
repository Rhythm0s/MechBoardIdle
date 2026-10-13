using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// **포트 커넥터 그림을 어느 쪽으로 돌릴 것인가** (2026-10-13 신설 · 커넥터 배선).
    ///
    /// 자산은 **한 쪽을 보는 한 장씩**이다(<c>port_in_small</c> 는 위로 가는 화살,
    /// <c>port_out_small</c> 은 아래로 가는 화살) — 면마다 넉 장을 만들지 않고 코드가 돌린다.
    /// `portArrow` 가 이미 쓰는 규약과 같다.
    ///
    /// 📌 **두 그림이 같은 각도로 돈다.** 화살이 가리켜야 하는 것은 **흐름**이고,
    /// 입력은 **밖에서 안으로**(= −면), 출력은 **안에서 밖으로**(= +면)다.
    /// 입력 자산은 ↑, 출력 자산은 ↓ 이므로 「↑ 를 −면으로 돌리는 회전」 하나를 쓰면
    /// 그 회전이 ↓ 를 **+면**으로 보낸다 — 각을 둘로 갈라 적으면 한쪽만 고쳐지는 날이 온다.
    ///
    /// ⚠️ **밖으로 나간 자리와 안으로 파인 자리는 그대로 둔다** — 거리는 부르는 쪽이 정하고
    /// 여기는 **각도만** 낸다.
    /// </summary>
    public static class PortArrowFacing
    {
        /// <summary>면 → 셀 중심에서 그 변으로 가는 방향. 보드의 `FaceOffset` 과 같은 규칙이다.</summary>
        public static Vector2 Offset(PortFace face)
        {
            switch (face)
            {
                case PortFace.East: return new Vector2(1f, 0f);
                case PortFace.West: return new Vector2(-1f, 0f);
                case PortFace.North: return new Vector2(0f, 1f);
                default: return new Vector2(0f, -1f); // South
            }
        }

        /// <summary>
        /// 그 면의 커넥터를 돌릴 각(도 · Z축). **입력·출력이 같은 값을 쓴다.**
        /// 기준은 「↑ 를 −면으로 보내는 회전」이다.
        /// </summary>
        public static float ZDegrees(PortFace face) =>
            Vector2.SignedAngle(Vector2.up, -Offset(face));

        /// <summary>그 회전이 입력 화살(↑)을 보내는 방향 — **셀 안쪽**이어야 한다.</summary>
        public static Vector2 InflowDirection(PortFace face) =>
            Rotate(Vector2.up, ZDegrees(face));

        /// <summary>그 회전이 출력 화살(↓)을 보내는 방향 — **셀 바깥**이어야 한다.</summary>
        public static Vector2 OutflowDirection(PortFace face) =>
            Rotate(Vector2.down, ZDegrees(face));

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
