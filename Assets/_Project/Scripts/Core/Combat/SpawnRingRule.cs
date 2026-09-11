using UnityEngine;

namespace MBI.Core.Combat
{
    /// <summary>
    /// 적이 나타나는 **링** — 원점이 아니라 **로봇**을 중심으로 돈다
    /// (2026-09-11 사용자 확정 · 플랜 §71-28 · 전장 구조 1·2).
    ///
    /// **왜 바꿨는가.** 종전에는 링이 **월드 원점 고정**이었고 반경도 `arenaRadiusTbd` 6 이었다.
    /// 로봇이 원점에 붙어 있을 때만 맞는 배치이며, 로봇이 움직이면 적이 **화면 안에서
    /// 튀어나왔다.** 전장이 아레나가 아니라 **로봇을 따라다니는 판**이 되면서
    /// 「적은 화면 밖에서 걸어 들어온다」가 규칙이 된다.
    ///
    /// ⚠️ **반경은 여기서 안 정한다.** 시뮬은 카메라가 없으므로 **밖에서 재서 넣는다** —
    /// `SetVisibleBounds` 와 같은 자리다. 값이 W03 으로 오기 전까지는
    /// **카메라 사각의 대각선 반 + 한 칸**을 가정으로 쓴다(그 계산은 러너가 한다).
    ///
    /// ⚠️ **각도는 결정론이다** — 난수 0. 같은 판을 두 번 돌리면 같은 자리에 선다.
    /// </summary>
    public static class SpawnRingRule
    {
        /// <summary>
        /// 링 위 <paramref name="index"/> 번째 자리의 **단위 방향**.
        /// 균등 분할이라 <paramref name="count"/> 가 1 이면 항상 오른쪽이다.
        /// </summary>
        public static Vector2 Direction(int index, int count)
        {
            if (count <= 0) return Vector2.right;
            float angle = 2f * Mathf.PI * index / count;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        /// <summary>로봇을 중심으로 한 링 위의 자리.</summary>
        public static Vector2 Position(Vector2 robot, int index, int count, float radius) =>
            robot + Direction(index, count) * Mathf.Max(0f, radius);

        /// <summary>
        /// 카메라 사각에서 **화면 밖 링 반경**을 낸다 — 가정값
        /// (2026-09-11 · W03 이 값을 주면 이 계산은 안 쓴다).
        ///
        /// **대각선 반 + 여유 한 칸.** 대각선을 쓰는 이유는 **모서리**가 화면에서 가장 먼
        /// 점이기 때문이다 — 가로나 세로 반만 쓰면 모서리 쪽에서 적이 **화면 안에 선다.**
        /// </summary>
        public static float RadiusFromView(float viewWidth, float viewHeight, float margin)
        {
            float half = new Vector2(viewWidth, viewHeight).magnitude * 0.5f;
            return half + Mathf.Max(0f, margin);
        }
    }

    /// <summary>
    /// **너무 멀어진 적을 로봇 쪽 링으로 되돌린다** (2026-09-11 사용자 확정 · §71-28 2).
    ///
    /// **왜 필요한가.** 이동 클램프를 걷어 내면 적이 아레나 밖으로 나갈 수 있고, 로봇이
    /// 반대쪽으로 달리면 **영영 못 닿는 적**이 생긴다. 그 적은 살아 있으므로 스테이지가
    /// 안 끝나고, 화면에도 없어 **왜 안 끝나는지가 안 보인다.**
    ///
    /// ⚠️ **지우지 않고 되돌린다.** 지우면 개체 수가 줄어 **스테이지 난이도가 조용히 낮아진다** —
    /// 통과 조건이 「개체 수 불변」인 이유다. HP 도 유지한다(⚠️ **가정** — 때려 놓은 것이
    /// 되살아나면 플레이어가 한 일이 사라진다).
    ///
    /// ⚠️ **잣대는 시간이다** — 「닿을 수 있는가」를 묻는 것이라 거리만으로는 못 정한다.
    /// 느린 적에게는 같은 거리가 더 멀다.
    /// </summary>
    public static class OffscreenRespawnRule
    {
        /// <summary>
        /// 못 닿는다고 보는 시간(초). ✅ **사용자 확정 20**(2026-09-11 · 플랜 §71-27) —
        /// **가정이 아니다.** 설계가 문서에 역기입한다.
        /// 짧으면 잠깐 뒤처진 적까지 순간이동해 **화면에서 적이 튄다.**
        /// </summary>
        public const float UnreachableSeconds = 20f;

        /// <summary>
        /// 이 적을 되돌려야 하는가. <paramref name="distance"/> 는 로봇까지의 거리다.
        ///
        /// ⚠️ **속도 0 은 되돌리지 않는다** — 못 움직이는 적은 애초에 다가올 수 없고,
        /// 그것은 「멀어졌다」가 아니라 **그렇게 놓인 것**이다(포탑 같은 것이 오면 그렇다).
        /// </summary>
        public static bool NeedsRespawn(float distance, float moveSpeed,
            float seconds = UnreachableSeconds)
        {
            if (moveSpeed <= 0f) return false;
            return distance > moveSpeed * seconds;
        }
    }
}
