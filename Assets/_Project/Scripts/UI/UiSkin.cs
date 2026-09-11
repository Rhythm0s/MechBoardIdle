using MBI.Data;
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

        /// <summary>
        /// 강조 — 주황. ⚠️ **상태 표시에서는 물러났다**(2026-09-11 · 아트 요청).
        ///
        /// 눌림·고름을 **주황 곱**으로 말하던 자리를 <see cref="PressedTint"/>·<see cref="HoverTint"/>
        /// 의 **명도 곱**으로 옮겼다. 그릇 그림이 이미 색을 갖고 있어서, 거기에 주황을 곱하면
        /// **그림의 색조가 통째로 돌아간다** — 금속이 주황 금속이 된다.
        /// 명도는 색조를 안 건드리고 **밝기만** 옮기므로 어떤 그림에도 얹힌다.
        ///
        /// 색 자체는 남긴다 — 경고·강조가 **색으로** 말해야 하는 자리가 따로 있다.
        /// </summary>
        public static readonly Color Accent = new Color(0.95f, 0.55f, 0.18f, 1f);

        // ───────────────── 상태 틴트 — **바꿀 자리는 여기 하나** ─────────────────
        //
        //  (2026-09-11 아트 요청 · 플랜 M-3). 눌림·손 올림을 **명도 곱**으로 말한다.
        //  ⚠️ **값 둘 다 가정**이다 — 문서에 상태 밝기의 절이 없다(설계 역기입 자리).
        //
        //  ⚠️ **지금은 글자에만 걸린다.** 바탕은 아직 상태마다 **다른 텍스처**를 물리는
        //  방식이고(코드 생성본은 그렇게 지어 두었다), 그림을 명도만 바꿔 쓰려면
        //  ① 임포트 텍스처를 읽을 수 있게 하거나(메모리 두 배) ② 그릴 때 `GUI.color` 로
        //  곱해야 한다 — 둘 다 호출부를 건드린다.
        //  **c13 의 눌림·잠김이 오면 그 그림을 그대로 물리므로 곱할 일이 없어진다.**
        //  안 오는 자리만 여기 값으로 메우게 되고, 그때 바꿀 곳이 이 둘뿐이다.

        /// <summary>눌린 상태의 밝기 곱. ⚠️ **가정 0.78** — 어두워지는 쪽이 눌림이다.</summary>
        public const float PressedMul = 0.78f;

        /// <summary>손이 올라간 상태의 밝기 곱. ⚠️ **가정 1.12** — 밝아지는 쪽이 반응이다.</summary>
        public const float HoverMul = 1.12f;

        /// <summary>
        /// 잠긴 상태의 밝기 곱 — **DIM**(2026-09-11 사용자 확정 · 플랜 §71-33 ③).
        ///
        /// 0.45 는 보드의 튜토리얼 어둠막(불투명도 0.55)이 남기는 밝기와 같다 —
        /// 화면 안에서 「잠김」이 **한 가지 어둡기**로만 말해지게 맞춘 것이다.
        /// 두 자리가 다른 값을 쓰면 같은 뜻이 두 밝기로 보인다.
        /// </summary>
        public const float LockedMul = 0.45f;

        /// <summary>밝기만 옮긴다 — **색조·알파는 안 건드린다.**</summary>
        public static Color Brighten(Color c, float mul) =>
            new Color(Mathf.Clamp01(c.r * mul), Mathf.Clamp01(c.g * mul), Mathf.Clamp01(c.b * mul), c.a);

        /// <summary>눌린 글자색.</summary>
        public static Color PressedTint => Brighten(Text, PressedMul);

        /// <summary>손이 올라간 글자색.</summary>
        public static Color HoverTint => Brighten(Text, HoverMul);

        /// <summary>글자 — 미색보다 밝게. 테두리와 같은 색이면 글자가 테두리에 붙는다.</summary>
        public static readonly Color Text = new Color(0.95f, 0.94f, 0.90f, 1f);

        /// <summary>꺼진 것 — 색을 빼는 것이 아니라 **어둡게** 한다. 회색으로 빼면 바탕에 묻힌다.</summary>
        public static readonly Color Disabled = new Color(0.55f, 0.54f, 0.51f, 1f);

        /// <summary>
        /// 코드 생성본의 테두리 두께(px). **버튼 크기와 무관하게 고정**이다 — 9-슬라이스가 지킨다.
        ///
        /// ⚠️ **그림 자산은 다른 값을 쓴다** — 원본 64 에 여백 16(`260911_W02` 9장).
        /// 그래서 여백은 상수 하나가 아니라 <see cref="ActiveBorder"/> 가 낸다.
        /// </summary>
        public const int BorderPx = 4;

        /// <summary>지금 쓰는 9-슬라이스 여백 — 그림이 있으면 그림 값, 없으면 코드 생성본 값.</summary>
        public static int ActiveBorder { get { EnsureTextures(); return _border; } }

        /// <summary>그림 자산으로 서 있는가(시험·진단용). 거짓이면 코드 생성본이다.</summary>
        public static bool UsingArt { get { EnsureTextures(); return _usingArt; } }

        /// <summary>텍스처 한 변. 9-슬라이스라 실제 크기는 아무래도 좋다 — 테두리 4 + 가운데 4 + 4.</summary>
        private const int TexSize = 12;

        // ────────────────────────────── 물리기 ──────────────────────────────

        private static GUISkin _skin;
        private static Texture2D _normal, _hover, _active, _off, _plate;
        private static int _border = BorderPx;
        private static bool _usingArt;

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

        /// <summary>
        /// 노드 상태 표식 그림 (2026-09-11 · 플랜 §71-33 ③). **없으면 null** —
        /// 자리표시를 대신 주지 않는다. 무엇을 뜻하는지가 그림에만 있어 흰 사각으로는 뜻이 안 선다.
        /// </summary>
        public static Texture2D IconTexture(MBI.Core.NodeIcon icon)
        {
            var art = Resources.Load<UiSkinAssets>(UiSkinAssets.ResourcePath);
            if (art == null) return null;

            switch (icon)
            {
                case MBI.Core.NodeIcon.Normal: return art.iconLogiNormal;
                case MBI.Core.NodeIcon.Slow: return art.iconLogiSlow;
                case MBI.Core.NodeIcon.Stopped: return art.iconLogiStop;
                case MBI.Core.NodeIcon.NotConnected: return art.iconNotConnected;
                case MBI.Core.NodeIcon.PowerShort: return art.iconPowerShort;
                default: return null;
            }
        }

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

            // ⚠️ **주황 곱 → 명도 곱**(2026-09-11 · 위 「상태 틴트」 절이 유일한 자리).
            // 종전에는 눌림·고름을 주황으로 말했는데, 그릇 그림 위에서는 **색조가 돌아간다.**
            s.normal.textColor = Text;
            s.hover.textColor = HoverTint;
            s.active.textColor = PressedTint;   // 눌린 순간은 글자가 어두워진다 — 바탕과 같은 방향
            s.focused.textColor = Text;
            s.onNormal.textColor = PressedTint;
            s.onHover.textColor = PressedTint;
            s.onActive.textColor = PressedTint;
            s.onFocused.textColor = PressedTint;

            // 9-슬라이스 여백. **버튼이 커져도 테두리는 4px** — 통짜로 늘리면 모서리가 뭉개진다.
            s.border = new RectOffset(_border, _border, _border, _border);

            // ⚠️ **꺼진 상태는 `GUIStyle` 칸이 따로 없다** — IMGUI 는 `GUI.enabled = false` 일 때
            // `normal` 을 그대로 쓰고 `GUI.color` 만 흐린다. 그래서 잠긴 버튼을 색으로 말하려면
            // 호출부가 <see cref="DisabledTexture"/> 를 직접 깔아야 한다(튜토리얼 잠금이 그 자리).
            s.alignment = TextAnchor.MiddleCenter;
            s.wordWrap = false;
        }

        private static void EnsureTextures()
        {
            if (_normal != null) return;

            // ⚠️ **그림이 있으면 그림이 이긴다 — 없으면 코드 생성본으로 떨어진다.**
            // 「자산이 오면 갈아끼운다」가 말이 되려면 **안 온 상태에서도 화면이 서야** 한다.
            // 여기 한 자리가 그 갈림길이고, 호출부는 어느 쪽인지 모른다(`260911_W02` 9장).
            var art = Resources.Load<UiSkinAssets>(UiSkinAssets.ResourcePath);
            if (art != null && art.HasAny)
            {
                _usingArt = true;
                _border = Mathf.Max(1, art.border);

                _normal = art.buttonNormal;

                // ⚠️ **없는 상태는 기본 그림을 틴트해서 쓴다**(2026-09-11 사용자 확정 · §71-33 ③).
                // 종전에는 코드 생성본으로 메웠는데, 그러면 **한 버튼 안에서 기본은 그림이고
                // 눌림은 코드**가 되어 누를 때마다 톤이 갈린다. 밝기만 옮기면 같은 그림이
                // 같은 톤으로 밝아지고 어두워진다.
                //
                // ⚠️ **그림을 돌려 쓰지는 않는다** — 같은 텍스처를 그대로 물리면
                // **눌러도 안 바뀌는 버튼**이 되어 상태가 화면에서 사라진다. 틴트가 그 차이다.
                _hover = art.buttonNormal != null
                    ? Tinted(art.buttonNormal, HoverMul) : Make(Lighten(Fill, 0.06f), Border);
                _active = art.buttonPressed != null ? art.buttonPressed
                    : art.buttonNormal != null ? Tinted(art.buttonNormal, PressedMul)
                    : Make(Darken(Fill, 0.35f), Accent);
                _off = art.buttonLocked != null ? art.buttonLocked
                    : art.buttonNormal != null ? Tinted(art.buttonNormal, LockedMul)
                    : Make(Darken(Fill, 0.45f), Disabled);
                // ⚠️ **패널 하나가 패널과 띠를 겸한다**(사용자 확정) — 띠용 칸을 따로 안 늘린다.
                // 9-슬라이스라 같은 그림이 어떤 비율로도 늘어난다.
                _plate = art.panel != null ? art.panel : Make(UiPlate.Tint, Border);

                if (_normal != null) return;
            }

            _usingArt = false;
            _border = BorderPx;
            _normal = Make(Fill, Border);
            // 손이 올라간 것은 **밝기**로만 말한다 — 색을 바꾸면 「고른 것」과 헷갈린다.
            _hover = Make(Lighten(Fill, 0.06f), Border);
            // 눌린 것 = 안쪽이 어두워지고 테두리가 주황. 눌렸다는 것이 두 방향으로 보인다.
            // ⚠️ 코드 생성본도 **같은 방향**으로 맞춘다 — 눌리면 어두워진다.
            // 테두리까지 주황으로 바꾸던 것을 걷었다(그림이 오면 톤이 갈린다).
            _active = Make(Darken(Fill, 0.35f), Brighten(Border, PressedMul));
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

        /// <summary>
        /// 텍스처 하나를 **밝기만 옮겨** 베낀다 (2026-09-11 · §71-33 ③ 폴백).
        ///
        /// ⚠️ **`GetPixels` 로 읽지 않는다.** 임포트한 그림은 보통 `isReadable = false` 라
        /// 읽는 순간 예외가 나고, 읽게 켜면 **메모리가 두 배**가 된다(사본이 CPU 쪽에 남는다).
        /// `Graphics.Blit` 은 GPU 에서 베끼므로 원본 설정을 안 건드린다.
        ///
        /// ⚠️ 못 베끼면 **원본을 그대로 돌려준다** — 틴트가 없는 편이
        /// 버튼이 통째로 안 보이는 것보다 낫다.
        /// </summary>
        private static Texture2D Tinted(Texture2D src, float mul)
        {
            if (src == null) return null;

            RenderTexture rt = RenderTexture.GetTemporary(
                src.width, src.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;

                var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);

                Color[] px = copy.GetPixels();   // 사본은 우리가 지었으니 읽을 수 있다
                for (int i = 0; i < px.Length; i++) px[i] = Brighten(px[i], mul);
                copy.SetPixels(px);
                copy.Apply(false, false);
                return copy;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MBI] 그릇 틴트 실패 — 원본을 그대로 쓴다: {e.Message}");
                return src;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static Color Lighten(Color c, float by) =>
            new Color(c.r + by, c.g + by, c.b + by, c.a);

        private static Color Darken(Color c, float by) =>
            new Color(c.r * (1f - by), c.g * (1f - by), c.b * (1f - by), c.a);
    }
}
