using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 전투 바닥을 **몇 장 깔고 어디에 둘 것인가** (2026-09-16 신설 · 육안 10차 ②).
    ///
    /// ⚠️ **왜 러너에서 떼어냈는가.** 「아래로 움직이면 상단 배경이 짧게 사라진다」는
    /// 결함이 두 줄짜리 산수에 있었는데, 그 산수가 `MonoBehaviour` 안에 있어 **시험이
    /// 못 닿았다.** 눈으로만 잡히는 결함은 눈을 안 뜨면 또 돌아온다.
    ///
    /// 깔아 둔 격자가 지켜야 하는 것은 하나다 —
    /// **기준점에서 시야 반크기만큼 간 자리가 언제나 격자 안에 있어야 한다.**
    /// </summary>
    public static class BackgroundTiling
    {
        /// <summary>
        /// 한 축을 덮는 데 필요한 장수.
        ///
        /// 🗑️ **구 식 `ceil(2·half / tile) + 2` 는 폐기**(2026-09-09 ~ 09-16).
        /// 여유가 **양쪽 합쳐 한 장**이었는데, 아래의 <see cref="SnapOrigin"/> 가 그때는
        /// 내림이라 격자 원점이 기준점보다 **최대 한 장 아래**에 앉았다. 그러면
        /// **위쪽 여유가 0 이 된다** — 카메라가 로봇을 따라가며 조금이라도 뒤처지면
        /// 그 프레임에 윗변 밖이 빈다. 아래로 걸을 때만 보인 까닭이 이것이다
        /// (아래로 갈 때 카메라가 로봇보다 **위**에 남는다).
        ///
        /// 지금은 <see cref="MarginTiles"/> 장을 **양쪽에 각각** 둔다.
        /// </summary>
        public static int Count(float halfExtent, float tile)
        {
            if (tile <= 0.0001f) return 0;
            return Mathf.CeilToInt(halfExtent * 2f / tile) + MarginTiles * 2;
        }

        /// <summary>
        /// 시야 밖에 **한쪽마다** 더 까는 장수.
        ///
        /// 1 장이면 반올림 스냅(±½장)까지만 흡수한다. 카메라가 로봇을 lerp 로 따라가므로
        /// **뒤처진 거리**까지 흡수해야 한다 — 로봇 4.5 유닛/초 · 따라붙는 계수 6/초 이면
        /// 정상 상태 뒤처짐이 대략 **0.75 유닛**이고, 한 장이 1.33 유닛이라 반 장(0.67)으로는
        /// 모자란다. 2 장이면 여유가 **1.5 장(2.0 유닛)** 이라 넉넉히 넘는다.
        ///
        /// ⚠️ **저 두 값을 여기 안 적는다** — 적으면 한 값이 두 곳에 살고, 튜닝이 바뀌어도
        /// 이 수가 안 따라온다(지침 §7). 여기 사는 것은 「여유 두 장」 하나이고,
        /// 근거는 주석으로만 남긴다. 값이 크게 바뀌면 시험이 아니라 **눈이** 먼저 안다.
        /// </summary>
        public const int MarginTiles = 2;

        /// <summary>
        /// 격자 원점 — 기준점을 **가장 가까운 격자 눈금에 반올림**한 자리.
        ///
        /// 🗑️ **구 식 `p - Repeat(p, tile)` 은 폐기**(내림). 내림은 원점을 기준점의
        /// **아래·왼쪽에만** 놓아 여유가 한쪽으로 쏠린다. 반올림은 원점을 기준점에서
        /// **±½장 안**에 두므로 네 방향 여유가 같다.
        ///
        /// 한 장 폭의 나머지만 쓰는 뜻은 그대로다 — 장수가 안 늘고, 격자가 자기 자신과
        /// 이어져 끝이 안 보인다.
        /// </summary>
        public static Vector2 SnapOrigin(Vector2 reference, Vector2 tile)
        {
            if (tile.x <= 0.0001f || tile.y <= 0.0001f) return reference;
            return new Vector2(
                Mathf.Round(reference.x / tile.x) * tile.x,
                Mathf.Round(reference.y / tile.y) * tile.y);
        }

        /// <summary>
        /// 이 장수·이 한 장 크기로 깔면 원점에서 **어디까지** 덮이는가(반크기).
        ///
        /// 장들이 원점을 가운데 두고 늘어서므로 바깥 끝은 `장수 × 한 장 ÷ 2` 다.
        /// 시험이 이 값을 시야 + 스냅 오차와 견준다.
        /// </summary>
        public static float Reach(int count, float tile) => count * tile * 0.5f;
    }
}
