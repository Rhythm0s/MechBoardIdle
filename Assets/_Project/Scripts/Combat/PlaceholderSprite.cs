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

        private static Sprite _ring;

        /// <summary>
        /// **속이 빈 얇은 고리** — 광역형 드론의 범위 자리표시
        /// (2026-09-17 · `260917_W08` 5-5 · 연출 아트 요청 문서「광역형 드론 범위 타격」).
        ///
        /// ⚠️⚠️ **속이 찬 그림과 갈라야 한다.** 폭발탄 스플래시는 **속이 찬 터짐**이고
        /// 이쪽은 **바닥에 퍼지는 테두리**다 — 같은 모양으로 그리면 둘이 한 사건으로 읽힌다.
        ///
        /// 📌 자리표시일 뿐이라 **아트가 오면 이 함수를 안 쓴다**(자산이 있으면 그쪽이 이긴다).
        /// </summary>
        public static Sprite Ring()
        {
            if (_ring != null) return _ring;

            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float r = n * 0.5f;
            var c = new Vector2(r, r);

            // 테두리 두께 — 반지름의 6%. 얇게 두면 크게 늘려도 선으로 읽힌다.
            const float thickness = 0.06f;

            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / r;
                // 1(가장자리)에서 멀어질수록 옅어진다 — 가운데는 **완전히 비어 있다**.
                float a = Mathf.Clamp01(1f - Mathf.Abs(1f - d) / thickness);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();

            _ring = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            _ring.name = "MBI_Ring";
            return _ring;
        }

        /// <summary>
        /// **줄기** — 회피 잔상을 잇는 빛줄기 (2026-09-18 사용자 확정 · 참고 이미지).
        ///
        /// ⚠️ **가운데가 굵고 양끝이 가늘다.** 고른 굵기의 막대를 늘이면 「막대기」로 읽히고
        /// 「지나간 자국」으로 안 읽힌다 — 두께를 x 방향 사인으로 준다.
        ///
        /// ⚠️ **자산이 아니라 코드 생성이다** — 연출 아트 문서에 줄기 절이 없다.
        /// 그림이 오면 이 함수를 안 부르면 된다(부르는 쪽이 자산을 먼저 본다).
        /// </summary>
        public static Sprite Streak()
        {
            if (_streak != null) return _streak;

            const int w = 128, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float half = h * 0.5f;

            for (int x = 0; x < w; x++)
            {
                // 양끝 0, 가운데 1 — 그 값이 그 자리의 **반두께**다.
                float t = Mathf.Sin(Mathf.PI * (x + 0.5f) / w);
                float halfThick = Mathf.Max(1f, t * half);

                for (int y = 0; y < h; y++)
                {
                    float d = Mathf.Abs(y + 0.5f - half);
                    // 가장자리를 부드럽게 — 딱 자르면 늘였을 때 계단이 보인다.
                    float a = Mathf.Clamp01(1f - d / halfThick);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            tex.Apply();

            _streak = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
            _streak.name = "MBI_Streak";
            return _streak;
        }

        private static Sprite _streak;
    }
}
