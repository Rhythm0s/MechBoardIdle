using UnityEngine;

namespace MBI.UI
{
    /// <summary>
    /// 글자 뒤에 까는 **반투명 어두운 바탕 판** (2026-09-10 사용자 확정 · 플랜 §66-35 ②).
    ///
    /// **왜 필요해졌는가.** 조립 화면 위쪽 30% 에 전투를 그리기 시작하면서
    /// (<see cref="MBI.Core.CombatInsetView"/>) 그 자리의 HUD 글자·노드 팔레트·모드 버튼·배율이
    /// **밝은 바닥 그림 위에 얹혔다.** 종전에는 검은 바탕이라 흰 글자가 읽혔다 —
    /// 화면을 채운 대가로 **대비를 잃은 것**이며, 촬영 A·D 구간에 계속 찍힌다.
    ///
    /// ⚠️ **블록 단위로만 깐다.** 위쪽 30% 를 통째로 덮으면 전투를 그린 뜻이 사라진다.
    /// 글자가 실제로 있는 자리만 가리고 **그 사이로 전투가 보이게** 한다.
    ///
    /// ⚠️ **알파는 가정이다.** UI 문서에 이 판의 절이 없다 — 읽히는 최소로 잡았고
    /// 설계가 역기입한다. 값이 서면 <see cref="Alpha"/> 하나만 바뀐다.
    /// </summary>
    public static class UiPlate
    {
        /// <summary>바탕 판 불투명도. ⚠️ **가정** — UI 문서 값 자리(설계 역기입).</summary>
        public const float Alpha = 0.72f;

        /// <summary>판 색. 조립 화면 바탕과 같은 계열이라 새 색을 들이지 않는다.</summary>
        public static Color Tint => new Color(0.04f, 0.05f, 0.07f, Alpha);

        /// <summary>
        /// 글자가 판 가장자리에 닿지 않게 넓히는 여백 — **기준 캔버스 값**이다.
        ///
        /// ⚠️⚠️ **날 픽셀이었다**(2026-10-13 사용자 스크린샷 ①② · 빌드 2687da8).
        ///
        /// 종전에는 이 6 을 **그 창의 픽셀로** 그대로 둘렀다. 그런데 자리를 쌓는 쪽
        /// (<see cref="UiLayout"/>)은 틈을 **기준 캔버스 값 × 배율**로 내므로, 창이 작아지면
        /// 틈만 줄고 여백은 안 줄었다 — 넓은 창(세로 690)에서 진행 띠와 배지 카드의
        /// **판이 9.8 픽셀 겹쳤다.** 기준 캔버스(2560)에서도 이미 4 픽셀 겹쳐 있었다.
        ///
        /// 📌 **시험은 초록이었다.** <c>StageProgressTests</c> 가 견준 것은 `Rect` 였고
        /// 사람이 본 것은 **판**이다 — 재는 문이 사람이 지나는 문과 달랐다.
        /// 그래서 판끼리 견주는 시험을 따로 세웠다(<c>UiPlateGapTests</c>).
        /// </summary>
        public const float Pad = 6f;

        /// <summary>이 창에서 실제로 두르는 여백 픽셀.</summary>
        public static float PadPx(float scale) => Pad * scale;

        /// <summary>
        /// 이 자리에 판을 깐다. **그리기 전에** 부른다 — IMGUI 는 뒤에 그리는 쪽이 위로 온다.
        /// </summary>
        public static void Draw(Rect area)
        {
            Rect r = Padded(area, UiLayout.CurrentScale);

            // ⚠️⚠️ **그림이 있으면 그림을 깐다**(2026-09-18 사용자 육안 · 시안 4 ①).
            //
            // 종전에는 **흰 사각에 색만 입혔다** — 모서리가 각지고 테두리가 없어
            // 화면에서 「판」이 아니라 **글자 밑의 어두운 자국**으로 읽혔다.
            // 패널 자산이 이미 리포에 있고(`UiSkinAssets.panel`) 9-슬라이스로 깔리므로
            // **새 자산 없이** 둥근 모서리와 테두리가 선다.
            //
            // 📌 **자리를 한 곳으로 모은 값이 여기서 돌아온다** — 칩·배지·카드·띠가 전부
            // 이 함수를 지나므로, 여기 한 줄이 화면 전체의 꼴을 바꾼다.
            //
            // ⚠️ 그림이 없으면 **종전 그대로** 떨어진다 — 자산이 없다고 화면이 사라지면 안 된다.
            if (UiSkin.UsingArt && UiSkin.PlateTexture != null)
            {
                UiSkin.DrawPlate(r);
                return;
            }

            Color prev = GUI.color;
            GUI.color = Tint;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        /// <summary>
        /// 색을 입힌 판 — **배지처럼 눈에 띄어야 하는 자리**(2026-09-18 · 시안 4 ①).
        ///
        /// ⚠️ 9-슬라이스 그림에 `GUI.color` 를 곱한다 — 새 그림을 만들지 않고 **같은 판에
        /// 색만 태운다.** 색이 무엇을 뜻하는지는 부르는 쪽이 정한다(여기는 그리기만 한다).
        /// </summary>
        public static void DrawTinted(Rect area, Color tint)
        {
            Color prev = GUI.color;
            GUI.color = tint;
            Draw(area);
            GUI.color = prev;
        }

        /// <summary>
        /// 여백을 두른 자리 — **배율을 받아서** 두른다. 시험이 같은 셈을 쓰도록 밖으로 낸다.
        ///
        /// ⚠️ 배율을 **받는다**: 편집기 시험에서 <c>Screen.height</c> 는 그 창의 수라
        /// 안에서 읽으면 시험이 돌 때마다 답이 달라진다.
        /// </summary>
        public static Rect Padded(Rect area, float scale)
        {
            float pad = PadPx(scale);
            return new Rect(area.x - pad, area.y - pad,
                            area.width + pad * 2f, area.height + pad * 2f);
        }
    }
}
