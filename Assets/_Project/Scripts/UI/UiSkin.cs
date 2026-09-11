using UnityEngine;

namespace MBI.UI
{
    /// <summary>
    /// IMGUI 껍데기 하나 (2026-09-11 신설 · 플랜 §68-4 (A) · §68-5 ②).
    ///
    /// **왜 신설하는가.** 화면의 버튼·상자 75곳이 **유니티 내장 껍데기**를 쓰고 있었다 —
    /// 회색 둥근 모서리에 검은 글자다. 문서가 정한 톤(각진 금속 · 미색 테두리 · 주황 강조)과
    /// 무관하고, 촬영본이 「만들다 만 것」으로 보이는 가장 큰 이유였다.
    /// 여기 하나를 갈면 **75곳이 한 번에** 바뀐다.
    ///
    /// ⚠️ **텍스처 소스가 여기 한 곳이다.** 월요일에 아트가 9-슬라이스 자산을 설치하면
    /// <see cref="Fill"/>·<see cref="Border"/> 로 그리던 자리를 **`Resources.Load` 로 바꾸는 것만**으로
    /// 끝난다 — 호출부는 손대지 않는다(플랜 §68-6 개시문). 그래서 버튼마다 `GUIStyle` 을
    /// 새로 짓지 않고 **껍데기에 얹는다.**
    ///
    /// ⚠️ **9-슬라이스로 그린다.** 테두리 4px 는 버튼이 커져도 4px 여야 각져 보인다 —
    /// 통짜로 늘리면 굵어지면서 모서리가 뭉개진다. `GUIStyle.border` 가 그 장치다.
    ///
    /// ⚠️ **`GUI.skin` 은 공용 자산이다.** 그대로 고치면 에디터 창까지 물든다 —
    /// 사본을 하나 지어 그것을 물린다.
    /// </summary>
    public static class UiSkin
    {
        // ────────────────────────────── 색 (UI 문서 · UI 아트 4장) ──────────────────────────────

        /// <summary>판 바탕 — 반투명 검정. <see cref="UiPlate.Tint"/> 와 **같은 계열**이다.</summary>
        public static readonly Color Fill = new Color(0.06f, 0.07f, 0.09f, 0.88f);

        /// <summary>테두리 — 미색. 무채색 금속 위의 유일한 밝은 선이다.</summary>
        public static readonly Color Border = new Color(0.86f, 0.83f, 0.74f, 1f);

        /// <summary>강조 — 주황. **고른 것·눌린 것**에만 쓴다(두 군데 넘게 쓰면 강조가 아니다).</summary>
        public static readonly Color Accent = new Color(0.95f, 0.55f, 0.18f, 1f);

        /// <summary>글자 — 미색보다 밝게. 테두리와 같은 색이면 글자가 테두리에 붙는다.</summary>
        public static readonly Color Text = new Color(0.95f, 0.94f, 0.90f, 1f);

        /// <summary>꺼진 것 — 색을 빼는 것이 아니라 **어둡게** 한다. 회색으로 빼면 바탕에 묻힌다.</summary>
        public static readonly Color Disabled = new Color(0.55f, 0.54f, 0.51f, 1f);

        /// <summary>테두리 두께(px). **버튼 크기와 무관하게 고정**이다 — 9-슬라이스가 그것을 지킨다.</summary>
        public const int BorderPx = 4;

        /// <summary>텍스처 한 변. 9-슬라이스라 실제 크기는 아무래도 좋다 — 테두리 4 + 가운데 4 + 4.</summary>
        private const int TexSize = 12;

        // ────────────────────────────── 물리기 ──────────────────────────────

        private static GUISkin _skin;
        private static Texture2D _normal, _hover, _active, _off, _plate;

        /// <summary>
        /// 이번 OnGUI 에 껍데기와 한글 폰트를 물린다. **각 `OnGUI` 맨 앞에서** 부른다.
        ///
        /// ⚠️ **차례가 있다** — 껍데기를 먼저 갈고 폰트를 나중에 물린다.
        /// 거꾸로 하면 폰트를 물린 껍데기가 통째로 교체되어 **한글이 다시 사라진다.**
        /// </summary>
        public static void Apply()
        {
            Ensure();
            if (_skin != null) GUI.skin = _skin;
            KoreanFont.Apply();
        }

        /// <summary>껍데기가 지어졌는가(시험·진단용). ⚠️ `GUI.skin` 을 건드리지 않는다.</summary>
        public static bool TexturesReady
        {
            get
            {
                EnsureTextures();
                return _normal != null && _hover != null && _active != null && _off != null;
            }
        }

        /// <summary>버튼 바탕 텍스처(시험용). 9-슬라이스 여백이 <see cref="BorderPx"/> 와 맞는지 본다.</summary>
        public static Texture2D NormalTexture { get { EnsureTextures(); return _normal; } }

        /// <summary>판 텍스처 — 상자·영역이 쓴다.</summary>
        public static Texture2D PlateTexture { get { EnsureTextures(); return _plate; } }

        /// <summary>잠긴 버튼 바탕. IMGUI 에 꺼진 상태 칸이 없어 **호출부가 직접 깐다**.</summary>
        public static Texture2D DisabledTexture { get { EnsureTextures(); return _off; } }

        private static void Ensure()
        {
            if (_skin != null) return;
            EnsureTextures();

            // 내장 껍데기의 사본에서 출발한다 — 여기서 안 고친 스타일(스크롤바 등)이
            // 그대로 살아야 화면이 안 깨진다.
            _skin = Object.Instantiate(GUI.skin);
            _skin.hideFlags = HideFlags.HideAndDontSave;

            Dress(_skin.button);
            Dress(_skin.box);
            Dress(_skin.window);
            Dress(_skin.textField);
            Dress(_skin.toggle);

            // 라벨은 바탕을 안 깐다 — 글자마다 판이 깔리면 화면이 상자투성이가 된다.
            // 바탕이 필요한 자리는 `UiPlate` 가 블록 단위로 깐다(§66-35 ②).
            _skin.label.normal.textColor = Text;
            _skin.label.hover.textColor = Text;
            _skin.label.active.textColor = Text;
        }

        /// <summary>스타일 하나를 문서 톤으로 갈아입힌다.</summary>
        private static void Dress(GUIStyle s)
        {
            if (s == null) return;

            s.normal.background = _normal;
            s.hover.background = _hover;
            s.active.background = _active;
            s.focused.background = _normal;
            s.onNormal.background = _active;
            s.onHover.background = _active;
            s.onActive.background = _active;
            s.onFocused.background = _active;

            s.normal.textColor = Text;
            s.hover.textColor = Text;
            s.active.textColor = Accent;   // 눌린 순간은 글자도 주황 — 손가락이 가려도 보인다
            s.focused.textColor = Text;
            s.onNormal.textColor = Accent;
            s.onHover.textColor = Accent;
            s.onActive.textColor = Accent;
            s.onFocused.textColor = Accent;

            // 9-슬라이스 여백. **버튼이 커져도 테두리는 4px** — 통짜로 늘리면 모서리가 뭉개진다.
            s.border = new RectOffset(BorderPx, BorderPx, BorderPx, BorderPx);

            // ⚠️ **꺼진 상태는 `GUIStyle` 칸이 따로 없다** — IMGUI 는 `GUI.enabled = false` 일 때
            // `normal` 을 그대로 쓰고 `GUI.color` 만 흐린다. 그래서 잠긴 버튼을 색으로 말하려면
            // 호출부가 <see cref="DisabledTexture"/> 를 직접 깔아야 한다(튜토리얼 잠금이 그 자리).
            s.alignment = TextAnchor.MiddleCenter;
            s.wordWrap = false;
        }

        private static void EnsureTextures()
        {
            if (_normal != null) return;

            _normal = Make(Fill, Border);
            // 손이 올라간 것은 **밝기**로만 말한다 — 색을 바꾸면 「고른 것」과 헷갈린다.
            _hover = Make(Lighten(Fill, 0.06f), Border);
            // 눌린 것 = 안쪽이 어두워지고 테두리가 주황. 눌렸다는 것이 두 방향으로 보인다.
            _active = Make(Darken(Fill, 0.35f), Accent);
            _off = Make(Darken(Fill, 0.45f), Disabled);
            _plate = Make(UiPlate.Tint, Border);
        }

        /// <summary>
        /// 테두리가 있는 사각 텍스처 하나. **각진 모서리** — 둥글리지 않는다.
        ///
        /// ⚠️ 월요일에 아트 자산이 오면 **이 메서드를 호출하는 자리**(<see cref="EnsureTextures"/>)만
        /// `Resources.Load&lt;Texture2D&gt;("UI/btn_normal")` 로 바뀐다. 그림이 9-슬라이스로 그려질
        /// 것이므로 <see cref="BorderPx"/> 와 `GUIStyle.border` 는 그대로 쓴다.
        /// </summary>
        private static Texture2D Make(Color fill, Color border)
        {
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,   // 각진 것이 톤이다 — 보간하면 테두리가 번진다
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var px = new Color[TexSize * TexSize];
            for (int y = 0; y < TexSize; y++)
            for (int x = 0; x < TexSize; x++)
            {
                bool edge = x < BorderPx || y < BorderPx
                            || x >= TexSize - BorderPx || y >= TexSize - BorderPx;
                px[y * TexSize + x] = edge ? border : fill;
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            return tex;
        }

        private static Color Lighten(Color c, float by) =>
            new Color(c.r + by, c.g + by, c.b + by, c.a);

        private static Color Darken(Color c, float by) =>
            new Color(c.r * (1f - by), c.g * (1f - by), c.b * (1f - by), c.a);
    }
}
