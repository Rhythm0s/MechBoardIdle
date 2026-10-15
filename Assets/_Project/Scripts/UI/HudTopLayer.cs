using System;
using System.Collections.Generic;

namespace MBI.UI
{
    /// <summary>
    /// **HUD 맨 위 층** — 다른 어셈블리의 판을 전투 화면 OnGUI 끝에서 그리게 하는 갈고리
    /// (2026-10-15 사용자 확정 #142 b · QA CID_102).
    ///
    /// ⚠️⚠️ 오프라인 보상 판(`IdleHud`)이 제 OnGUI 에서 그려져 **S1 배지 카드 밑에 깔렸다** —
    ///    IMGUI 는 나중에 그린 것이 위로 오고, 컴포넌트 사이 OnGUI 차례는 세울 수 없다
    ///    (#133 에서 `[DefaultExecutionOrder]` 로 안 된 것을 확인했다). 그래서 #133·#134 처럼
    ///    **`StageRunner.OnGUI` 끝**에서 그린다. 전투 쪽은 `MBI.Idle` 을 모르므로 여기에 걸어 둔다.
    /// </summary>
    public static class HudTopLayer
    {
        private static readonly List<Action> Draws = new List<Action>();

        public static void Add(Action draw)
        {
            if (draw != null && !Draws.Contains(draw)) Draws.Add(draw);
        }

        public static void Remove(Action draw) => Draws.Remove(draw);

        /// <summary>걸린 판을 차례대로 그린다. **`StageRunner.OnGUI` 끝에서만** 부른다.</summary>
        public static void DrawAll()
        {
            for (int i = 0; i < Draws.Count; i++) Draws[i]?.Invoke();
        }
    }
}
