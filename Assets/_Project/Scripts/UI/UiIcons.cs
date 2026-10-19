using UnityEngine;

namespace MBI.UI
{
    /// <summary>
    /// **재화 아이콘 + 글자 한 줄** (2026-10-19 사용자 확정 #175 ⓐ — 비용 표시 = 고철 아이콘 + [금액]).
    ///
    /// 아이콘 그림의 원천은 `CombatTuning.iconScrapSprite` · `iconGoldSprite`(칩 줄이 쓰는 그것)이다.
    /// 보드(로직스)는 전투 자산을 모르므로 **러너가 시작할 때 여기에 걸어 둔다** — 그림 하나를 두 곳이 따로 들지 않는다.
    /// 그림이 없으면(시험 · 자산 누락) 색 원으로 떨어진다(칩 줄의 폴백과 같은 결).
    /// </summary>
    public static class UiIcons
    {
        /// <summary>고철 아이콘 — 러너가 건다.</summary>
        public static Sprite Scrap;

        /// <summary>골드 아이콘 — 러너가 건다.</summary>
        public static Sprite Gold;

        /// <summary>고철 폴백 색(그림이 없을 때) — 칩 줄 고철 글자와 같은 결. ⚠️ 가정.</summary>
        public static readonly Color ScrapFallback = new Color(0.78f, 0.8f, 0.86f);

        /// <summary>아이콘 하나를 그린다. 그림이 없으면 색 원.</summary>
        public static void Draw(Rect r, Sprite s, Color fallback)
        {
            if (s != null && s.texture != null)
            {
                Rect tr = s.textureRect;
                var uv = new Rect(tr.x / s.texture.width, tr.y / s.texture.height,
                                  tr.width / s.texture.width, tr.height / s.texture.height);
                GUI.DrawTextureWithTexCoords(r, s.texture, uv, true);
                return;
            }
            Color prev = GUI.color;
            GUI.color = new Color(fallback.r, fallback.g, fallback.b, prev.a);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.ScaleToFit);
            GUI.color = prev;
        }

        /// <summary>「앞 글자 + 아이콘 + 뒤 글자」 한 줄의 폭.</summary>
        public static float Measure(GUIStyle st, string prefix, string suffix)
        {
            float h = LineHeight(st);
            float a = string.IsNullOrEmpty(prefix) ? 0f : st.CalcSize(new GUIContent(prefix)).x;
            float b = string.IsNullOrEmpty(suffix) ? 0f : st.CalcSize(new GUIContent(suffix)).x;
            return a + h * 0.9f + Gap(h) + b;
        }

        /// <summary>
        /// 「앞 글자 + 아이콘 + 뒤 글자」를 한 줄로 그린다(왼쪽 맞춤 · 세로 가운데). 아이콘 크기 = 글자 높이의 0.9.
        /// </summary>
        public static void DrawLine(Rect line, GUIStyle st, string prefix, Sprite icon, Color fallback, string suffix)
        {
            var one = new GUIStyle(st) { wordWrap = false, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Overflow };
            float h = LineHeight(one);
            float x = line.x;
            if (!string.IsNullOrEmpty(prefix))
            {
                float a = one.CalcSize(new GUIContent(prefix)).x;
                GUI.Label(new Rect(x, line.y, a + 1f, line.height), prefix, one);
                x += a;
            }
            float size = h * 0.9f;
            Draw(new Rect(x, line.y + (line.height - size) * 0.5f, size, size), icon, fallback);
            x += size + Gap(h);
            if (!string.IsNullOrEmpty(suffix))
                GUI.Label(new Rect(x, line.y, line.xMax - x + 40f, line.height), suffix, one);
        }

        private static float LineHeight(GUIStyle st) => Mathf.Max(8f, st.CalcSize(new GUIContent("가")).y);
        private static float Gap(float h) => Mathf.Max(2f, h * 0.12f);
    }
}
