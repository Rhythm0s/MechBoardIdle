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
        /// **한 방향이 붙드는 각 = 120°**(2026-09-16 사용자 확정 · 플랜 §74-15 ①).
        ///
        /// 읽는 법: **지금 보는 쪽에서 ±60° 안이면 그대로 유지하고, 벗어나면 넘긴다.**
        /// 네 방향이 각각 120° 를 쥐므로 이웃끼리 **30° 씩 겹친다** — 그 겹치는 띠가
        /// 히스테리시스다. 겹침이 없으면(=90° 씩 딱 나누면) 경계선 위에서 떤다.
        ///
        /// ⚠️ **사용자 보고가 근거다** — 「표적이 대각선이면 얼굴이 좌/우로 매우 빠르게 뒤집힌다」
        /// (육안 10차 ①). 그때 조준은 관성 **1** 로 돌고 있었다(=45° 로 딱 나눔).
        /// 45° 언저리에 표적이 서 있으면 로봇이나 적이 조금만 움직여도 승자가 오가고,
        /// 그때마다 얼굴이 뒤집혔다.
        ///
        /// **왜 배수인가.** 이 클래스는 각을 안 쓰고 두 축의 크기만 견준다(`ay > ax * h`).
        /// 경계각 θ 에서 `ay / ax = tan θ` 이므로 **h = tan 60° = √3** 이다.
        /// 각으로 적어 두면 읽는 사람이 60 을 넣고, 배수로만 적어 두면 어디서 온 수인지
        /// 모른다 — 그래서 <see cref="RetainDegrees"/> 에서 **계산해서** 낸다.
        ///
        /// 📌 구 값 **1.5 는 폐기**(2026-09-11~09-16). 1.5 = tan 56.3° 라 붙드는 각이
        /// 112.6° 였다 — 가정이었고 문서 절이 없었다. 지금 값은 사용자 확정이다.
        /// </summary>
        public const float RetainDegrees = 120f;

        /// <summary>
        /// 축을 바꾸려면 새 축이 옛 축의 **몇 배**여야 하는가 = `tan(120° / 2)` = √3.
        ///
        /// 1.0 이면 겹침이 없어 다시 떨고, 너무 크면 **방향이 안 바뀐다** —
        /// 대각으로 꺾어도 옛 면을 붙들고 미끄러진다.
        /// </summary>
        public static readonly float Hysteresis =
            Mathf.Tan(RetainDegrees * 0.5f * Mathf.Deg2Rad);

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
        ///
        /// <paramref name="hysteresis"/> 를 안 주면 <see cref="Hysteresis"/>(120°) 를 쓴다.
        /// ⚠️ **기본값 자리에 못 적는다** — `Hysteresis` 가 `const` 가 아니라 `Mathf.Tan` 으로
        /// 구하는 `static readonly` 이기 때문이다. 0 이하가 오면 기본값으로 읽는다.
        /// </summary>
        public static UnitAnimDirection Resolve(Vector2 delta, UnitAnimDirection last,
            float hysteresis = 0f)
        {
            if (hysteresis <= 0f) hysteresis = Hysteresis;
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
