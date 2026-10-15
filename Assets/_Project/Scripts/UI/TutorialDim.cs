using UnityEngine;

namespace MBI.UI
{
    /// <summary>
    /// **튜토리얼 어둠막 — 값 한 곳 · 그리는 법 한 곳** (2026-10-14 사용자 확정 #130 로 꺼냈다).
    ///
    /// 종전에는 이 값이 `BoardController.TutorialDimColor` 안에 **혼자** 살았다(T1 의 고스트
    /// 칸 밖을 덮는 막). T3 에도 같은 막이 필요해지자 **같은 값을 두 곳에 적을 참**이었다 —
    /// 지침 §7 「한 값이 두 곳에 살면 답이 둘이 된다」. 그래서 여기로 꺼내고 둘 다 이것을 읽는다.
    ///
    /// ✅ **값이 섰다 — 0.75**(2026-10-15 사용자 확정 · 2차 육안 「아래 레이어가 더 안 보이게」).
    /// T1 · T3 **둘 다** 이 값을 쓴다(사용자 확정 「둘 다 같이」).
    /// 🗑️ 구 값 0.55(2026-09-11 · 플랜 §68-5 ① · 가정) 폐기 — 이동 모드 흐림 막과 같은 값으로
    ///    둔 것이었다. **이동 모드 흐림(`BoardController.PanDimColor`)은 0.55 그대로다** —
    ///    사용자 지시는 튜토리얼 막에 대한 것이었고, 그 막은 조작 중에 늘 깔린다.
    /// </summary>
    public static class TutorialDim
    {
        /// <summary>튜토리얼 어둠막 색 — **0.75**(사용자 확정 10-15 · T1·T3 공용).</summary>
        public static readonly Color Color = new Color(0.03f, 0.05f, 0.08f, 0.75f);

        /// <summary>
        /// **구멍 하나를 남기고** 화면 전체를 덮는다 — 구멍 둘레를 사각 넷으로 두른다.
        ///
        /// ⚠️ 통째로 덮은 뒤 구멍 자리를 다시 그리는 길을 안 쓴다 — 그러면 구멍 안의 것
        /// (단추)을 이 함수가 알아야 하고, 그림자·테두리가 어둠 위에 떠서 두 겹으로 보인다
        /// (설정 패널이 09-21 에 같은 이유로 넷으로 둘렀다 · `ReviewerShortcuts.DrawDimAround`).
        ///
        /// ⚠️ **눌림은 안 막는다** — 그림만 깐다. 무엇을 누를 수 있는가는 튜토리얼 국면
        /// (`TutorialGate`)이 정하고, 이 막은 **눈을 거기로 모으는** 일만 한다.
        /// </summary>
        public static void DrawAround(Rect hole)
        {
            float w = Screen.width, h = Screen.height;
            Color prev = GUI.color;
            GUI.color = Color;

            Texture2D t = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(0f, 0f, w, Mathf.Max(0f, hole.y)), t);                       // 위
            GUI.DrawTexture(new Rect(0f, hole.yMax, w, Mathf.Max(0f, h - hole.yMax)), t);          // 아래
            GUI.DrawTexture(new Rect(0f, hole.y, Mathf.Max(0f, hole.x), hole.height), t);          // 왼쪽
            GUI.DrawTexture(new Rect(hole.xMax, hole.y, Mathf.Max(0f, w - hole.xMax), hole.height), t); // 오른쪽

            GUI.color = prev;
        }
    }
}
