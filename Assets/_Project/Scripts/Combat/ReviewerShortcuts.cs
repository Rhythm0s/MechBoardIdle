using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using MBI.UI;
using UnityEngine;

namespace MBI.Combat
{
    /// <summary>
    /// 심사자용 바로가기 — **원하는 스테이지와 상태로 바로 들어간다**(260901_W04 §3층 확정).
    ///
    /// **왜 숨기지 않는가.** 이 빌드는 포트폴리오 심사자가 링크로 직접 플레이하는 것이 목적이다.
    /// S6 합체를 보려면 스테이지 0부터 다 통과해야 한다면 아무도 거기까지 가지 않고,
    /// 영상에서 본 것을 직접 확인할 방법이 사라진다.
    ///
    /// **이름을 「치트」로 두지 않는 이유도 같다.** 숨겨 둔 치트는 발견되면
    /// 「밸런스를 못 맞춰서 넣었나」로 읽히고, 드러난 바로가기는 시간을 아껴 주려는 배려로 읽힌다.
    /// 같은 코드인데 이름과 배치가 뜻을 바꾼다.
    ///
    /// ⚠️ 촬영과 리허설에도 이것이 쓰인다. 재촬영마다 스테이지 3까지 4~5분,
    /// 합체 게이지에 90초가 드는데 리허설은 전 구간을 반복하므로 그 비용이 촬영보다 크다.
    /// </summary>
    public sealed class ReviewerShortcuts : MonoBehaviour
    {
        [Tooltip("전투 러너.")]
        public StageRunner runner;

        [Tooltip("바로 갈 수 있는 스테이지 목록. 씬 생성기가 주입한다.")]
        public List<StageDefinition> stages = new List<StageDefinition>();

        [Tooltip("스테이지를 바꿀 때 함께 꺼야 하는 튜토리얼 세션. 없으면 비워 둔다.")]
        public Stage0Session stage0;

        private bool _open;

        private void OnGUI()
        {
            KoreanFont.Apply();

            var button = new GUIStyle(GUI.skin.button) { fontSize = 13 };
            const float w = 150f, h = 26f, pad = 4f;
            // 좌측 여백 — 전투 HUD(위)와 미니맵(아래) 사이. 보드는 x 280부터라 겹치지 않는다.
            const float x = 12f;
            float y = 212f;

            if (!_open)
            {
                if (GUI.Button(new Rect(x, y, w, h), "심사자용 바로가기 >", button)) _open = true;
                return;
            }

            if (GUI.Button(new Rect(x, y, w, h), "심사자용 바로가기 v", button)) _open = false;
            y += h + pad;

            // 안내 한 줄 — 이것이 있어야 「밸런스를 못 맞춰 넣었나」로 안 읽힌다.
            var note = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            GUI.Label(new Rect(x, y, 300f, 32f),
                "포트폴리오용 바로가기입니다. 원래 진행은 스테이지 0부터 순서대로입니다", note);
            y += 34f;

            DrawStageButtons(y, x, h, pad, button);
            y += h + pad;

            DrawGaugeButton(y, x, w, h, button);
        }

        /// <summary>
        /// 스테이지 버튼은 **가로로 깐다.** 세로로 쌓으면 일곱 개가 미니맵까지 내려가 겹친다.
        /// </summary>
        private void DrawStageButtons(float y, float x, float h, float pad, GUIStyle style)
        {
            if (runner == null || stages == null) return;

            const float bw = 42f;
            float bx = x;

            for (int i = 0; i < stages.Count; i++)
            {
                StageDefinition s = stages[i];
                if (s == null) continue;

                bool here = runner.CurrentStage == s;
                Color prev = GUI.color;
                if (here) GUI.color = new Color(1f, 0.92f, 0.45f); // 지금 있는 곳

                if (GUI.Button(new Rect(bx, y, bw, h), s.stageId, style)) GoTo(s);

                GUI.color = prev;
                bx += bw + pad;
            }
        }

        private void DrawGaugeButton(float y, float x, float w, float h, GUIStyle style)
        {
            MergeSystem merge = runner != null && runner.Sim != null ? runner.Sim.Merge : null;

            // 이미 썼거나 진행 중이면 누를 수 없다 — 스테이지당 1회 규칙을 바로가기가 깨지 않는다.
            GUI.enabled = merge != null && !merge.UsedThisStage && !merge.IsActive;
            if (GUI.Button(new Rect(x, y, w, h), "합체 게이지 채우기", style))
                merge.FillGaugeAlmost();
            GUI.enabled = true;
        }

        /// <summary>
        /// 스테이지를 바꾼다. **튜토리얼 세션을 먼저 끈다** — 켜진 채로 두면 그것이
        /// 전투를 억제하고 목표를 관찰해 엉뚱한 곳에서 스테이지 1로 넘겨 버린다.
        /// </summary>
        private void GoTo(StageDefinition s)
        {
            if (stage0 != null && stage0.enabled) stage0.enabled = false;
            runner.LoadStage(s);
        }

    }
}
