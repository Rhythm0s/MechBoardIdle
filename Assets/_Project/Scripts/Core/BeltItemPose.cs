using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 벨트 칸 안에서 아이템이 **어디에 그려지는가** (2026-09-05 신설).
    ///
    /// <see cref="BeltItemFlow"/>는 아이템을 `progress` 하나(0=입력면 · 1=출력면)로만 들고 있다.
    /// 그 수를 칸 안의 좌표로 바꾸는 것이 여기다. 렌더러에서 계산하지 않고 떼어 둔 이유는
    /// **코너에서 경로가 꺾이기 때문**이다 — 직선처럼 두 점을 이으면 물건이 벨트 밖으로
    /// 지름길을 타고 지나간다.
    ///
    /// 경로는 언제나 **입력면 → 칸 중심 → 출력면** 두 구간이다. 직선 벨트에서는 세 점이
    /// 한 줄에 놓여 결과가 선형 보간과 정확히 같고, 코너에서는 중심에서 꺾여 ㄱ자를 그린다.
    /// 한 식으로 둘을 다 덮으므로 분기가 없다.
    ///
    /// 순수 — 격자도 씬도 모르고, 칸 크기 1을 기준으로 한 **칸 중심 기준 오프셋**만 돌려준다.
    /// 부르는 쪽이 칸 크기를 곱하고 월드 좌표를 더한다.
    /// </summary>
    public static class BeltItemPose
    {
        /// <summary>가장자리까지의 거리. 칸 크기가 1이므로 중심에서 면까지는 절반이다.</summary>
        private const float HalfCell = 0.5f;

        /// <summary>
        /// 칸 중심을 원점으로 한 아이템 위치.
        ///
        /// <paramref name="progress"/>는 0~1 밖으로 나가도 잘라서 쓴다 — 한 틱에 여러 칸을
        /// 건너뛰는 경우가 있는데, 그때 그림이 칸 밖으로 튀어나가면 안 된다.
        /// </summary>
        public static Vector2 LocalOffset(PortFace inFace, PortFace outFace, float progress)
        {
            Vector2 entry = Edge(inFace);
            Vector2 exit = Edge(outFace);
            float t = Mathf.Clamp01(progress);

            // 앞 절반은 입구에서 중심으로, 뒤 절반은 중심에서 출구로.
            return t <= 0.5f
                ? Vector2.Lerp(entry, Vector2.zero, t * 2f)
                : Vector2.Lerp(Vector2.zero, exit, (t - 0.5f) * 2f);
        }

        /// <summary>그 면의 한가운데 점. 면의 방향으로 반 칸 나간 자리다.</summary>
        private static Vector2 Edge(PortFace face)
        {
            Vector2Int d = BeltRouting.Delta(face);
            return new Vector2(d.x, d.y) * HalfCell;
        }
    }
}
