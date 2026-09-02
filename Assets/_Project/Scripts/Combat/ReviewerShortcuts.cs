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

        [Tooltip("튜토리얼 전용 스테이지 자산. 이 버튼은 개발 빌드에만 뜬다.")]
        public StageDefinition tutorialStage;

        /// <summary>
        /// 튜토리얼 복귀를 보여 줄 것인가 — **개발 빌드에서만**(260902_W08 §2-2).
        ///
        /// 스테이지 0은 스테이지가 아니라 튜토리얼이므로 스테이지 이동 목록에 뜨지 않는다.
        /// 그런데 촬영은 개발 빌드로 하고 A구간 재테이크에는 복귀가 필요하다 —
        /// 배포 빌드(심사자용)와 촬영용 빌드는 같은 코드이므로 여기서 갈린다.
        /// <c>Debug.isDebugBuild</c>는 에디터와 Development Build에서만 참이다.
        /// </summary>
        private static bool ShowTutorial => Debug.isDebugBuild;

        private bool _open;

        private void OnGUI()
        {
            // ⚠️ **조립 화면에서는 그리지 않는다.** 우측 하단이 조립 화면에서는 노드 팔레트 자리라,
            // 그대로 두면 「병합기」 버튼을 통째로 덮어 **보드에서 병합기를 고를 수 없다**
            // (2026-09-02 브라우저 실측 — A구간 촬영이 막혔다).
            //
            // 없애는 대신 화면을 가린 것이 아니다. 스테이지 이동은 전투 화면의 일이고,
            // 조립 중에 스테이지를 갈아 끼울 이유도 없다.
            if (GameViewSignals.BoardViewActive) return;

            KoreanFont.Apply();

            var button = new GUIStyle(GUI.skin.button) { fontSize = 13 };
            const float w = 236f, h = 26f, pad = 4f;

            // ⚠️ **좌측은 못 쓴다.** y 212에 뒀더니 HUD·태그 버튼과 통째로 겹쳐
            // 글자가 서로 위에 찍혔다(2026-09-01 브라우저 실측).
            // 오른쪽 아래로 옮긴다 — 변수 패널(우상단)과 레이어 버튼(하단 중앙) 사이가 비어 있다.
            float x = Screen.width - w - 12f;
            float y = Screen.height - 190f;

            if (!_open)
            {
                if (GUI.Button(new Rect(x, y, w, h), "심사자용 바로가기 >", button)) _open = true;
                return;
            }

            if (GUI.Button(new Rect(x, y, w, h), "심사자용 바로가기 v", button)) _open = false;
            y += h + pad;

            // 안내 한 줄 — 이것이 있어야 「밸런스를 못 맞춰 넣었나」로 안 읽힌다.
            var note = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            GUI.Label(new Rect(x, y, w, 30f),
                "포트폴리오용 바로가기입니다. 원래 진행은 튜토리얼부터 순서대로입니다", note);
            y += 32f;

            DrawStageButtons(y, x, h, pad, button);
            y += h + pad;

            DrawGaugeButton(y, x, w, h, button);
            y += h + pad;

            DrawResetButton(y, x, w, h, button);
        }

        /// <summary>
        /// 저장 초기화 — **개발 빌드 전용 촬영 도구**(260902_W09 §1-2 승인).
        ///
        /// 없으면 9월 6일에 A구간을 **한 번밖에 못 찍는다.** 병합기를 지우고 복귀해도
        /// 마운트 40이 남아 「8초 쌓이는」 장면이 재현되지 않는다(2026-09-02 실측).
        /// 완충일이 0인 일정에서 첫 테이크가 곧 최종본이 되는 것은 받을 수 없다.
        ///
        /// 처음 상태로 되돌리는 것은 넷이다 — 저장 · 창고와 마운트 · 비워 둔 칸 · 튜토리얼 진행.
        /// 하나라도 빠지면 「다시 찍을 수 있다」가 성립하지 않는다.
        /// </summary>
        private void DrawResetButton(float y, float x, float w, float h, GUIStyle style)
        {
            if (!ShowTutorial || runner == null) return; // 배포 빌드에는 없다

            if (!GUI.Button(new Rect(x, y, w, h), "처음부터 (저장 초기화)", style)) return;

            IdleSignals.RequestSaveReset(); // 저장 — 방치 런타임이 지운다
            runner.ResetCarry();            // 창고와 마운트

            if (tutorialStage != null)
            {
                runner.LoadStage(tutorialStage);
                if (stage0 != null) stage0.Reenter();
            }

            // ⚠️ **비워 둔 칸은 맨 마지막에 요청한다.** Reenter가 고스트를 다시 걸면서
            // TutorialSignals.Reset()을 부르는데, 그것이 이 요청까지 지운다 —
            // 앞에 두었더니 병합기가 안 지워져 넷 중 셋만 되돌아갔다(2026-09-02 실측).
            TutorialSignals.ClearEmptySlotRequested = true;
        }

        /// <summary>
        /// 스테이지 버튼은 **가로로 깐다.** 세로로 쌓으면 일곱 개가 미니맵까지 내려가 겹친다.
        /// </summary>
        private void DrawStageButtons(float y, float x, float h, float pad, GUIStyle style)
        {
            if (runner == null || stages == null) return;

            // ⚠️ 폭을 패널에서 역산한다. 40으로 고정했더니 일곱 개가 S4에서 잘렸다
            // (2026-09-01 브라우저 실측). 스테이지가 늘어도 안 잘리게 나눠 쓴다.
            int count = 0;
            for (int i = 0; i < stages.Count; i++) if (Shows(stages[i])) count++;
            if (count == 0) return;
            float bw = (236f - pad * (count - 1)) / count;
            float bx = x;

            for (int i = 0; i < stages.Count; i++)
            {
                StageDefinition s = stages[i];
                if (!Shows(s)) continue;

                bool here = runner.CurrentStage == s;
                Color prev = GUI.color;
                if (here) GUI.color = new Color(1f, 0.92f, 0.45f); // 지금 있는 곳

                // 튜토리얼은 번호를 안 쓴다(260902_W09 §2). 버튼이 좁아 짧게 적는다.
                string label = s == tutorialStage ? "튜토" : s.stageId;
                if (GUI.Button(new Rect(bx, y, bw, h), label, style)) GoTo(s);

                GUI.color = prev;
                bx += bw + pad;
            }
        }

        /// <summary>목록에 뜨는 항목인가. 튜토리얼은 개발 빌드에서만 뜬다.</summary>
        private bool Shows(StageDefinition s) =>
            s != null && (s != tutorialStage || ShowTutorial);

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
            // 튜토리얼로 돌아가는 것만은 반대다 — 끄는 것이 아니라 다시 켠다.
            if (s == tutorialStage && tutorialStage != null)
            {
                runner.LoadStage(s);
                if (stage0 != null) stage0.Reenter();
                return;
            }

            if (stage0 != null && stage0.enabled) stage0.enabled = false;
            runner.LoadStage(s);
        }

    }
}
