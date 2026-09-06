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

        /// <summary>보드 일감률 평균. 아직 안 실렸으면 만가동으로 본다 — 0으로 그리면 거짓말이다.</summary>
        private static float WorkloadAverage
        {
            get
            {
                var perNode = LogisticsOutputBridge.Workload.perNode;
                return perNode == null || perNode.Count == 0 ? 1f : LogisticsOutputBridge.Workload.average;
            }
        }

        private void OnGUI()
        {
            KoreanFont.Apply(); // WebGL엔 시스템 폰트 폴백이 없다 — 스타일보다 먼저 물린다
            EnsureStyles();

            LogisticsResult r = LogisticsOutputBridge.Result;
            var rect = new Rect(Screen.width - width - margin, margin, width, 250f);
            // 이 패널 위 클릭은 보드에 닿지 않아야 한다 — 종전에는 자리를 안 내서
            // 패널을 눌러도 그 아래 칸에 노드가 놓였다(UiBlockers 주석).
            UiBlockers.Add(rect);

            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("물류 변수", _head);

            // ⚠️ **병목 경고가 맨 위다.** 종전에는 맨 아래였는데 패널 높이가 250 고정이라
            // 마지막 줄이 영역 밖으로 밀려 **한 번도 화면에 뜬 적이 없었다**(2026-09-02 실측).
            // 점멸 코드도 문구도 있는데 잘려 있었다 — 「그리는가」가 아니라 「보이는가」의 문제다.
            //
            // 높이를 늘리는 쪽은 못 쓴다. 패널은 y 12에서 시작하고 노드 팔레트 라벨이 y 274라,
            // 250을 넘기면 그쪽과 겹친다(같은 날 겹침 넷을 고친 뒤라 더 만들지 않는다).
            // 순서를 바꾸면 높이가 그대로다. 병목이 이 패널에서 가장 급한 줄이기도 하다.
            string cause = CauseText(LogisticsOutputBridge.GlobalCause);
            if (cause != null && Blink()) GUILayout.Label(cause, Warn(_label));

            // 「실제」는 **마운트에 닿은 것**이다(2026-09-05 · `260904_W04` 2-1 4번).
            // 종전에는 계산값이라 벨트를 어떻게 깔든 노드 수만 같으면 같은 수가 떴다.
            GUILayout.Label($"예상 {r.expected:F1}   실제 {r.actual:F1}   갭 {r.gap:F1}", _label);
            GUILayout.Space(4f);

            GUILayout.Label("갭 발생원", _head);
            GUILayout.Label($"전력  {r.gapPower:F1}   (효율 {Pct(r.powerEfficiency)})", _label);
            GUILayout.Label($"발열  {r.gapHeat:F1}   (감쇠 {Pct(r.heatThrottle)})", _label);
            // 벨트는 다른 둘과 축이 다르다(2026-09-05). 전력·발열은 **식**으로 구한 감쇠이고,
            // 벨트는 「만든 것 중 실제로 닿은 비율」을 **역산**한 값이다 — 정체·갈래·거리가
            // 전부 여기 섞여 들어온다. 그래서 「감쇠」가 아니라 「도달」로 적는다.
            GUILayout.Label($"벨트  {r.gapBelt:F1}   (도달 {Pct(r.beltThrottle)})", _label);
            GUILayout.Space(4f);

            GUILayout.Label($"명목 배율 ×{r.multiple:F2}", _label);

            // 일감률(260831_V07 승인분). **총합은 평균**이고, 어느 노드가 노는지는 보드가 그린다.
            // 전력 수요가 이 값을 타므로 갭 발생원 「전력」과 같은 눈길에서 읽혀야 한다.
            GUILayout.Label($"일감률 평균 {Pct(WorkloadAverage)}   (노는 노드는 전력 0)", _label);

            GUILayout.EndArea();
        }

        /// <summary>
        /// 전역 원인 → 문구. 우선순위 판정은 Provider가 이미 했다(여기선 매핑만).
        ///
        /// ⚠️ **이모지를 쓰지 않는다.** 「⚡」·「🔥」로 두었더니 WebGL에서 두부(□)로 찍혔다
        /// (2026-09-02 실측). 한글 폰트에 이모지 글리프가 없고, WebGL엔 시스템 폰트 폴백도 없다
        /// — 한글이 통째로 사라지는 것과 같은 뿌리다(KoreanFont.Apply가 있는 이유).
        ///
        /// 눈에 띄는 일은 색(주황)과 점멸이 이미 한다. 표기는 HUD의 「[전력 부족]」과 맞춘다.
        /// </summary>
        private static string CauseText(ConstraintCause cause)
        {
            switch (cause)
            {
                case ConstraintCause.Power: return "[!] 전력 부족";
                case ConstraintCause.Heat: return "[!] 발열 초과";
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
