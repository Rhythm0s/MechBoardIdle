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

        /// <summary>글자가 판 가장자리에 닿지 않게 넓히는 여백.</summary>
        public const float Pad = 6f;

        /// <summary>
        /// 이 자리에 판을 깐다. **그리기 전에** 부른다 — IMGUI 는 뒤에 그리는 쪽이 위로 온다.
        /// </summary>
        public static void Draw(Rect area)
        {
            Color prev = GUI.color;
            GUI.color = Tint;
            GUI.DrawTexture(Padded(area), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        /// <summary>여백을 두른 자리. 시험이 같은 셈을 쓰도록 밖으로 낸다.</summary>
        public static Rect Padded(Rect area) =>
            new Rect(area.x - Pad, area.y - Pad, area.width + Pad * 2f, area.height + Pad * 2f);
    }
}
