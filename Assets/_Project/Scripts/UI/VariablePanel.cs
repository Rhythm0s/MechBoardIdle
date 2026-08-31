using MBI.Core;
using UnityEngine;

namespace MBI.UI
{
    /// <summary>
    /// 변수 패널(§5-6 커밋 C) — 물류가 지금 무엇에 막혀 있는지를 숫자로 보여준다.
    ///
    /// 브릿지의 <see cref="LogisticsOutputBridge.Result"/> 하나만 읽어 그린다.
    /// **판정은 전혀 하지 않는다**(§3 UI는 매핑만): 어느 게 병목인지, 갭이 얼마인지는
    /// 전부 물류 코어(LogisticsSimulation)가 정한 값이고 여기서는 배치만 한다.
    ///
    /// 갭 분해 3항의 합은 총갭과 정확히 같다(같은 롤링 창 — RollingWindow).
    /// 화면에서 합이 안 맞아 보이면 그건 표시 버그가 아니라 게시 경로 버그다.
    /// </summary>
    public sealed class VariablePanel : MonoBehaviour
    {
        [Tooltip("패널 폭(px).")]
        public float width = 300f;
        [Tooltip("화면 우측·상단 여백(px).")]
        public float margin = 12f;

        private GUIStyle _label;
        private GUIStyle _head;

        private void OnGUI()
        {
            KoreanFont.Apply(); // WebGL엔 시스템 폰트 폴백이 없다 — 스타일보다 먼저 물린다
            EnsureStyles();

            LogisticsResult r = LogisticsOutputBridge.Result;
            var rect = new Rect(Screen.width - width - margin, margin, width, 250f);

            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("물류 변수", _head);

            GUILayout.Label($"예상 {r.expected:F1}   실제 {r.actual:F1}   갭 {r.gap:F1}", _label);
            GUILayout.Space(4f);

            GUILayout.Label("갭 발생원", _head);
            GUILayout.Label($"전력  {r.gapPower:F1}   (효율 {Pct(r.powerEfficiency)})", _label);
            GUILayout.Label($"발열  {r.gapHeat:F1}   (감쇠 {Pct(r.heatThrottle)})", _label);
            GUILayout.Label($"벨트  {r.gapBelt:F1}   (감쇠 {Pct(r.beltThrottle)})", _label);
            GUILayout.Space(4f);

            GUILayout.Label($"명목 배율 ×{r.multiple:F2}", _label);

            string cause = CauseText(LogisticsOutputBridge.GlobalCause);
            if (cause != null && Blink())
                GUILayout.Label(cause, Warn(_label));

            GUILayout.EndArea();
        }

        // 전역 원인 → 문구. 우선순위 판정은 Provider가 이미 했다(여기선 매핑만).
        private static string CauseText(ConstraintCause cause)
        {
            switch (cause)
            {
                case ConstraintCause.Power: return "⚡ 전력 부족";
                case ConstraintCause.Heat: return "🔥 발열 초과";
                default: return null;
            }
        }

        private static string Pct(float ratio) => $"{Mathf.Clamp01(ratio) * 100f:F0}%";

        // 전역 원인은 1차 표시자라 눈에 띄어야 한다(노드 색은 2차).
        private static bool Blink() => ((int)(Time.unscaledTime * 2.5f) & 1) == 0;

        private static GUIStyle Warn(GUIStyle basis)
        {
            var s = new GUIStyle(basis);
            s.normal.textColor = new Color(0.98f, 0.72f, 0.25f);
            return s;
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _head = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        }
    }
}
