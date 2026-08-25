using UnityEngine;

namespace MBI.Combat
{
    /// <summary>
    /// 런타임 생성 1유닛 흰 사각 스프라이트(공유). 아트 자산 없이 플레이스홀더 렌더용.
    /// 본체·HP바·탄선 FX가 공유한다. 아트 리소스 준비 시 실제 스프라이트로 교체.
    /// </summary>
    public static class PlaceholderSprite
    {
        private static Sprite _white;

        public static Sprite White()
        {
            if (_white != null) return _white;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            _white.name = "MBI_WhiteSquare";
            return _white;
        }

        private static Sprite _softDisc;

        /// <summary>
        /// 가장자리가 부드러운 원(1유닛). 바닥 그림자용 — 스케일로 눌러 타원을 만든다.
        ///
        /// 코드로 그리는 이유(260825_V01 §3 질의 회신): 유닛마다 그림자를 따로 뽑으면 8장이
        /// 늘고 발밑 오프셋을 데이터로 관리해야 한다. 무엇보다 **배경 제거를 켜면 그림자가
        /// 같이 잘리는 문제가 여기서는 아예 생기지 않는다** — 본체 아트에 그림자를 안 그리면 되기 때문이다.
        /// </summary>
        public static Sprite SoftDisc()
        {
            if (_softDisc != null) return _softDisc;

            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float r = n * 0.5f;
            var c = new Vector2(r, r);

            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / r;
                // 중심에서 가장자리로 갈수록 옅어진다 — 딱딱한 원은 바닥에 붙은 스티커처럼 보인다.
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            tex.Apply();

            _softDisc = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            _softDisc.name = "MBI_SoftDisc";
            return _softDisc;
        }
    }
}
