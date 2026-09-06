using System;

namespace MBI.Core
{
    /// <summary>
    /// 알파 마스크 한 장 — 캔버스 크기와 「여기 픽셀이 있는가」만 든다.
    ///
    /// 색을 안 들고 있는 이유는 이 도구가 재는 것이 **실루엣**이기 때문이다.
    /// 톤·명암·강조색은 숫자로 못 재고 사람이 나란히 놓고 봐야 한다
    /// (보드 아트 요청 문서 3-2 「겹침은 톤을 재지 않는다」).
    /// </summary>
    public readonly struct AlphaMask
    {
        public readonly int width;
        public readonly int height;
        private readonly bool[] _bits;

        public AlphaMask(int width, int height, bool[] bits)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException($"캔버스가 비었다: {width}×{height}");
            if (bits == null || bits.Length != width * height)
                throw new ArgumentException($"마스크 길이가 캔버스와 다르다: {bits?.Length ?? -1} vs {width * height}");

            this.width = width;
            this.height = height;
            _bits = bits;
        }

        public bool this[int x, int y] => _bits[y * width + x];

        /// <summary>차 있는 픽셀 수.</summary>
        public int Count
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _bits.Length; i++) if (_bits[i]) n++;
                return n;
            }
        }

        public bool IsEmpty => Count == 0;
    }

    /// <summary>자산 네 변의 빈 픽셀 수. 보드 3-0이 「네 변에 여백을 남긴다」로 쓰는 그 값이다.</summary>
    public readonly struct MarginPx
    {
        public readonly int left, right, top, bottom;

        public MarginPx(int left, int right, int top, int bottom)
        {
            this.left = left; this.right = right; this.top = top; this.bottom = bottom;
        }

        /// <summary>네 변 중 가장 좁은 곳. 판정은 늘 이 값으로 한다 — 한 변만 붙어도 붙은 것이다.</summary>
        public int Min => Math.Min(Math.Min(left, right), Math.Min(top, bottom));

        public override string ToString() => $"L{left} R{right} T{top} B{bottom}";
    }

    /// <summary>
    /// 실루엣 겹침·여백·가로세로비를 재는 유일한 자리 (2026-09-06 신설 · 사용자 확정).
    ///
    /// ⚠️ **캔버스 그대로만 잰다. 자르거나 늘이는 길을 두지 않는다** (`260906_W02` 2-1 확정).
    /// 옵션으로 두면 언젠가 켜지기 때문이다. 실제로 그 일이 있었다 —
    /// 2026-09-06에 알파 bbox로 잘라 정사각으로 늘여 재던 값이 판정 셋에 들어갔고,
    /// 그 방식은 **가로로 긴 것과 세로로 긴 것을 같은 모양으로 만든다.**
    /// 여백이 사라지고 가로세로비가 사라지는데, 두 문서가 그 둘을 정보로 쓰고 있다 —
    /// 보드 6-3(여백 없는 것은 마운트 포트 하나뿐) · 품목 I-3(폭발탄을 크기로 벌린다).
    ///
    /// 이 클래스가 낸 숫자만 판정 근거로 쓴다. 손 계산과 임시 스크립트 값은 근거가 아니다
    /// (지침 §10 「측정법 한 줄 의무」 · 2026-09-06 사용자 확정).
    /// </summary>
    public static class SilhouetteOverlap
    {
        /// <summary>
        /// 두 실루엣의 겹침 = 함께 차 있는 넓이 ÷ 둘을 합친 넓이.
        ///
        /// 둘 다 비어 있으면 0을 낸다 — 나눌 것이 없는 것을 1.0(완전히 같다)으로 적으면 거짓말이 된다.
        /// </summary>
        public static float Ratio(AlphaMask a, AlphaMask b)
        {
            RequireSameCanvas(a, b);

            int both = 0, either = 0;
            for (int y = 0; y < a.height; y++)
            for (int x = 0; x < a.width; x++)
            {
                bool pa = a[x, y], pb = b[x, y];
                if (pa && pb) both++;
                if (pa || pb) either++;
            }

            return either == 0 ? 0f : both / (float)either;
        }

        /// <summary>네 변의 여백. 빈 마스크는 잴 것이 없으므로 예외다.</summary>
        public static MarginPx Margins(AlphaMask m)
        {
            if (!TryBounds(m, out int minX, out int maxX, out int minY, out int maxY))
                throw new ArgumentException("빈 마스크의 여백은 잴 수 없다");

            return new MarginPx(minX, m.width - 1 - maxX, minY, m.height - 1 - maxY);
        }

        /// <summary>
        /// 채워진 부분의 가로 ÷ 세로. 1보다 크면 가로로 길다.
        ///
        /// 이 값이 있어야 「가로 직사각과 세로 직사각은 다른 것」이 숫자로 남는다 —
        /// 구 측정법이 지우던 바로 그 정보다.
        /// </summary>
        public static float AspectRatio(AlphaMask m)
        {
            if (!TryBounds(m, out int minX, out int maxX, out int minY, out int maxY))
                throw new ArgumentException("빈 마스크의 가로세로비는 잴 수 없다");

            return (maxX - minX + 1) / (float)(maxY - minY + 1);
        }

        /// <summary>채워진 부분을 감싸는 사각형. 없으면 false.</summary>
        public static bool TryBounds(AlphaMask m, out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = m.width; maxX = -1; minY = m.height; maxY = -1;

            for (int y = 0; y < m.height; y++)
            for (int x = 0; x < m.width; x++)
            {
                if (!m[x, y]) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            return maxX >= 0;
        }

        /// <summary>
        /// 캔버스가 다르면 잴 수 없다.
        ///
        /// 맞추려면 늘이거나 잘라야 하는데 둘 다 이 도구가 하지 않기로 한 것이다.
        /// 화면에서 나란히 보이는 것이 캔버스이므로, 캔버스가 다른 둘은 애초에
        /// 같은 자리에 놓이지 않는다 — 노드는 전부 192, 품목은 전부 64다.
        /// </summary>
        private static void RequireSameCanvas(AlphaMask a, AlphaMask b)
        {
            if (a.width != b.width || a.height != b.height)
                throw new ArgumentException(
                    $"캔버스가 다르면 겹침을 잴 수 없다: {a.width}×{a.height} vs {b.width}×{b.height}. " +
                    "늘이거나 잘라서 맞추지 않는다 — 그것이 2026-09-06에 폐기된 방식이다");
        }
    }
}
