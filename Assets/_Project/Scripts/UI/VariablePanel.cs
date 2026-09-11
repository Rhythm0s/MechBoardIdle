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
    /// <remarks>자리는 <see cref="UiLayout.Band.VariablePanel"/> — 하단 112(UI 문서 9-4).</remarks>
    public sealed class VariablePanel : MonoBehaviour
    {
        // ⚠️ **폐기 — 자리는 이제 `UiLayout` 이 준다**(2026-09-11 · 플랜 §68-4 (A)).
        // 씬이 값을 들고 있어 필드를 지우면 직렬화 경고가 나므로 **표기로만 남긴다.**
        [Tooltip("폐기 — UiLayout.Band.VariablePanel 이 폭을 준다(2026-09-11).")]
        public float width = 300f;
        [Tooltip("폐기 — 자리는 하단 띠다(2026-09-11).")]
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
            // 메인 메뉴가 덮고 있으면 그리지 않는다 — IMGUI 는 뒤에 그리는 쪽이 위로 온다
            // (2026-09-10 · 실측: 오프라인 대화상자가 「게임 시작」 버튼을 덮었다).
            if (MainMenuGate.IsOpen) return;
            UiSkin.Apply(); // 껍데기 + 한글 폰트 — WebGL엔 시스템 폰트 폴백이 없다

            LogisticsResult r = LogisticsOutputBridge.Result;

            // ⚠️ **우상단 300×250 에서 하단 띠로 옮겼다** (2026-09-11 · 플랜 §68-4 (A)).
            //
            // 문서 9-4 의 변수 패널은 **하단 112** 다(`260910_W04` 를 받아 플랜 §69 2번이
            // 「변수 패널은 하단이다」로 이미 한 번 정정했다). 우상단에 있던 동안 이 패널은
            // 노드 팔레트와 자리를 다투었고, 「높이가 250 고정이라 마지막 줄이 한 번도 안 보였다」
            // (2026-09-02)는 그 다툼의 증상이었다.
            //
            // ⚠️ **세로 아홉 줄이 112 에 안 들어간다** — 문서 높이는 지키고 **세 칸으로 접었다.**
            // 무엇을 접었는지가 판정거리이면 설계가 역기입한다(V01 통보).
            Rect band = UiLayout.BandRect(UiLayout.Band.VariablePanel, Screen.width, Screen.height);
            float s = UiLayout.Scale(Screen.height);

            // 이 패널 위 클릭은 보드에 닿지 않아야 한다 — 종전에는 자리를 안 내서
            // 패널을 눌러도 그 아래 칸에 노드가 놓였다(UiBlockers 주석).
            UiBlockers.Add(band);
            EnsureStyles(s);

            GUI.DrawTexture(band, UiSkin.PlateTexture);

            float pad = 16f * s;
            float colW = (band.width - pad * 4f) / 3f;
            float rowH = (band.height - pad) / 3f;

            // ── 1칸: 무엇이 막았나 + 예상/실제/갭 ──────────────────────────────
            float x = pad;
            // ⚠️ **병목 경고가 맨 위다.** 종전 세로 배치에서 맨 아래였다가 잘려
            // 한 번도 뜬 적이 없었다(2026-09-02 실측) — 그 교훈을 자리로 옮긴다.
            string cause = CauseText(LogisticsOutputBridge.GlobalCause);
            if (cause != null && Blink())
                GUI.Label(new Rect(x, band.y + pad * 0.2f, colW, rowH), cause, Warn(_head));
            GUI.Label(new Rect(x, band.y + pad * 0.2f + rowH, colW, rowH), "물류 변수", _head);
            // 「실제」는 **마운트에 닿은 것**이다(2026-09-05 · `260904_W04` 2-1 4번).
            GUI.Label(new Rect(x, band.y + pad * 0.2f + rowH * 2f, colW, rowH),
                $"예상 {r.expected:F1}  실제 {r.actual:F1}  갭 {r.gap:F1}", _label);

            // ── 2칸: 전력 ──────────────────────────────────────────────────────
            x += colW + pad;
            // ⚠️ **전력 줄의 축은 효율이 아니라 사용률이다**(2026-09-06 확정 · UI 문서 3-3).
            // 효율은 min(1, 공급÷수요)라 모자라기 전까지 100%에 붙어 움직이지 않는다 —
            // 사용률(수요÷공급)은 상한이 없어 여유도 초과도 같은 눈금에서 읽힌다.
            float supply = LogisticsOutputBridge.PowerSupply;
            float draw = LogisticsOutputBridge.PowerDraw;
            GUI.Label(new Rect(x, band.y + pad * 0.2f, colW, rowH), "갭 발생원", _head);
            GUI.Label(new Rect(x, band.y + pad * 0.2f + rowH, colW, rowH),
                $"전력 {r.gapPower:F1}  (사용률 {HudBars.UsageText(supply, draw)})", _label);
            HudBars.Usage(new Rect(x, band.y + rowH * 2.3f, colW, 14f * s), supply, draw);

            // ── 3칸: 벨트 · 배율 · 일감률 ───────────────────────────────────────
            x += colW + pad;
            // 벨트는 다른 둘과 축이 다르다(2026-09-05). 전력은 **식**으로 구한 감쇠이고,
            // 벨트는 「만든 것 중 실제로 닿은 비율」을 **역산**한 값이다 — 정체·갈래·거리가
            // 전부 여기 섞여 들어온다. 그래서 「감쇠」가 아니라 「도달」로 적는다.
            //
            // ⚠️ **발열 줄은 여기 없다** — 2026-09-02 폐기 확정 · 09-10 이행.
            GUI.Label(new Rect(x, band.y + pad * 0.2f, colW, rowH),
                $"벨트 {r.gapBelt:F1}  (도달 {Pct(r.beltThrottle)})", _label);
            GUI.Label(new Rect(x, band.y + pad * 0.2f + rowH, colW, rowH),
                $"명목 배율 ×{r.multiple:F2}", _label);
            // 일감률(260831_V07 승인분). **총합은 평균**이고, 어느 노드가 노는지는 보드가 그린다.
            GUI.Label(new Rect(x, band.y + pad * 0.2f + rowH * 2f, colW, rowH),
                $"일감률 평균 {Pct(WorkloadAverage)}  (노는 노드는 전력 0)", _label);
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

        /// <summary>
        /// 글자 크기는 **창을 따라간다** — 13 고정이면 2560 창에서 점이 되고
        /// 800 창에서는 띠 밖으로 넘친다(하단 112 는 기준 캔버스 값이다).
        /// </summary>
        private void EnsureStyles(float scale)
        {
            int size = Mathf.Max(9, Mathf.RoundToInt(26f * scale));
            if (_label != null && _label.fontSize == size) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = size };
            _head = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = FontStyle.Bold };
        }
    }
}
