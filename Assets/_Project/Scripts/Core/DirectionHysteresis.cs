using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 움직인 방향을 넷 중 하나로 접는다 — **경계에서 떨지 않게**
    /// (2026-09-11 사용자 육안 · 플랜 §71-16 ⑦).
    ///
    /// **왜 떨었는가.** 종전 규칙은 「우세 축이 이긴다」 하나였다(`|dx| >= |dy|`).
    /// 대각으로 갈 때 두 축이 **거의 같으므로** 프레임마다 미세한 차이로 승자가 뒤집히고,
    /// 그때마다 벌이 갈려 **동면과 북면이 번갈아 깜빡였다.** 걷는 그림이 두 장 겹쳐
    /// 보이는 그 증상이다.
    ///
    /// **고치는 법은 마지막 축을 유지하는 것이다.** 새 축이 이기려면 **충분히** 이겨야 한다 —
    /// 그 「충분히」가 <see cref="Hysteresis"/> 다. 한 번 정해진 방향은 관성을 갖는다.
    ///
    /// ⚠️ **자동 이동에도 같이 걸린다.** 떨림은 입력이 만든 것이 아니라 **위치 변화를
    /// 방향으로 접는 자리**에서 나므로, 손으로 몰든 시뮬이 몰든 같은 곳에서 난다.
    /// </summary>
    public static class DirectionHysteresis
    {
        /// <summary>
        /// 축을 바꾸려면 새 축이 옛 축의 **몇 배**여야 하는가. ⚠️ **가정 1.5** —
        /// UI·연출 문서에 이 값의 절이 없다(설계 역기입 자리).
        ///
        /// 1.0 이면 종전과 같아 다시 떨고, 너무 크면 **방향이 안 바뀐다** —
        /// 대각으로 꺾어도 옛 면을 붙들고 미끄러진다. 1.5 는 「확실히 꺾었을 때만」에 해당한다.
        /// </summary>
        public const float Hysteresis = 1.5f;

        /// <summary>
        /// 우세 축만 보는 옛 규칙. **첫 방향을 정할 때** 쓴다 — 붙들 옛 축이 없다.
        /// </summary>
        public static UnitAnimDirection Dominant(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                return delta.x >= 0f ? UnitAnimDirection.East : UnitAnimDirection.West;
            return delta.y >= 0f ? UnitAnimDirection.North : UnitAnimDirection.South;
        }

        /// <summary>그 방향이 가로축인가(동·서).</summary>
        public static bool IsHorizontal(UnitAnimDirection dir) =>
            dir == UnitAnimDirection.East || dir == UnitAnimDirection.West;

        /// <summary>
        /// 마지막 방향을 붙들고 새 방향을 고른다.
        ///
        /// **축은 관성을 갖고, 축 안에서 좌우·상하는 그대로 따라간다** — 동에서 서로 도는 것은
        /// 같은 축이라 떨림이 아니다(실제로 반대로 걷는 것이다). 막아야 하는 것은
        /// **축이 뒤집히는 것**뿐이다.
        /// </summary>
        public static UnitAnimDirection Resolve(Vector2 delta, UnitAnimDirection last,
            float hysteresis = Hysteresis)
        {
            float ax = Mathf.Abs(delta.x), ay = Mathf.Abs(delta.y);
            float h = Mathf.Max(1f, hysteresis); // 1 보다 작으면 관성이 아니라 반대가 된다

            if (IsHorizontal(last))
            {
                // 세로가 가로를 h 배 넘게 이겨야 축을 넘긴다.
                if (ay > ax * h) return delta.y >= 0f ? UnitAnimDirection.North : UnitAnimDirection.South;
                return delta.x >= 0f ? UnitAnimDirection.East : UnitAnimDirection.West;
            }

            if (ax > ay * h) return delta.x >= 0f ? UnitAnimDirection.East : UnitAnimDirection.West;
            return delta.y >= 0f ? UnitAnimDirection.North : UnitAnimDirection.South;
        }
    }
}
