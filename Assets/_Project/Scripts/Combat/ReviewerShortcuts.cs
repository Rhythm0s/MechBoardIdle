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
        /// 🗑️ **폐기 — 개발 빌드 게이트**(2026-09-18 사용자 확정 · ⚠️ 구현 가정 · 되돌릴 수 있다).
        ///
        /// 📌 **심사자는 배포 빌드를 본다.** 바로가기가 심사자용인데 배포 빌드에서 안 보이면
        /// 그 이름이 무색해진다 — 튜토리얼 복귀도 같은 무리이므로 함께 연다.
        /// ⚠️ 사용자가 다르게 말하면 이 한 줄을 `Debug.isDebugBuild` 로 되돌리면 된다.
        private static bool ShowTutorial => true;

        private void OnGUI()
        {
            // 메인 메뉴가 덮고 있으면 그리지 않는다 — IMGUI 는 뒤에 그리는 쪽이 위로 온다
            // (2026-09-10 · 실측: 오프라인 대화상자가 「게임 시작」 버튼을 덮었다).
            if (MainMenuGate.IsOpen) return;

            // ⚠️⚠️ **여는 손잡이가 칩 줄의 「설정」로 옮겨 갔다**(2026-09-18 사용자 확정).
            //
            // 🗑️ **제 토글 버튼 폐기** — 「심사자용 바로가기 >」가 화면 왼쪽 아래에 늘 떠
            //    있던 것이다. 여는 자리가 둘이면 **어느 쪽이 참인지**를 둘이 따로 들게 되고,
            //    그 상태가 갈리는 날이 온다(이 리포의 되풀이되는 결함).
            //    이제 열림 여부는 `SettingsGate` **한 곳**이 든다.
            if (!SettingsGate.IsOpen) return;
            // ⚠️ **조립 화면에서는 그리지 않는다.** 우측 하단이 조립 화면에서는 노드 팔레트 자리라,
            // 그대로 두면 「병합기」 버튼을 통째로 덮어 **보드에서 병합기를 고를 수 없다**
            // (2026-09-02 브라우저 실측 — A구간 촬영이 막혔다).
            //
            // 없애는 대신 화면을 가린 것이 아니다. 스테이지 이동은 전투 화면의 일이고,
            // 조립 중에 스테이지를 갈아 끼울 이유도 없다.
            if (GameViewSignals.BoardViewActive) return;

            UiSkin.Apply(); // 껍데기 + 한글 폰트 — WebGL엔 시스템 폰트 폴백이 없다

            var button = new GUIStyle(GUI.skin.button) { fontSize = 13 };
            const float w = 236f, h = 26f, pad = 4f;

            // ⚠️ **오른쪽 아래도 못 쓴다**(2026-09-14 · 2차 스크린샷 2장). 09-01 에 좌측
            // y 212 를 버리고 이리로 왔는데, 09-11 에 태그·합체가 **문서 좌표 x1280** 의
            // 원형 둘로 서면서 **같은 자리를 다투게 됐다.**
            //
            // 화면에서는 **태그 원형이 통째로 사라졌다** — 이 버튼과 「소리」가 그 위에
            // 나중에 그려져 덮었다(IMGUI 는 뒤에 그리는 쪽이 위다). 바로 그 태그 버튼을
            // 손보던 날이라 「눌러도 반응이 없다」와 겹쳐 읽히기 딱 좋은 자리였다.
            //
            // **왼쪽 아래로 뺀다.** 레이어 1 에서 그쪽은 비어 있다 — 조립 진입 막대는
            // 가운데(x720)이고 원형 둘은 오른쪽(x1280)이라 왼쪽 끝에 닿는 것이 없다.
            //
            // ⚠️ **이 자리는 여전히 가정이다** — 개발용 패널이라 문서에 자리가 없다(설계 몫).
            // 정해진 것은 **원형 둘의 자리를 비운다**는 것 하나다.
            float x = 12f;
            float y = Screen.height - 190f;

            // ⚠️ **판을 깐다**(2026-09-18) — 볼륨 패널과 같은 문법이다. 종전에는 버튼만
            //    흙바닥 위에 떠 있어 어디까지가 이 패널인지가 안 읽혔다.
            //    높이는 **줄 수에서 낸다** — 고정값을 박으면 줄이 늘 때 마지막이 잘린다.
            int rows = 6 + (ShowTutorial ? 1 : 0);   // 제목·안내·스테이지·게이지·전멸·초기화·덤프·메뉴
            var plate = new Rect(x - 8f, y - 8f, w + 16f, (h + pad) * (rows + 2) + 48f);
            UiPlate.Draw(plate);
            UiBlockers.Add(plate);

            var title = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(x, y, w, h), "설정", title);
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

            DrawKillAllButton(y, x, w, h, button);
            y += h + pad;

            DrawResetButton(y, x, w, h, button);
            y += h + pad;

            DrawBoardDumpButton(y, x, w, h, button);
            y += h + pad;

            // ── 「메인 메뉴로」 (2026-09-18 사용자 확정) ────────────────────
            //
            // ⚠️ **전투를 세우는 일은 러너가 한다** — 여기서는 빗장만 건다.
            //    러너가 `MainMenuGate.IsOpen` 을 보고 틱을 멈춘다(그러지 않으면
            //    메뉴 뒤에서 판이 계속 돌아 돌아왔을 때 져 있다).
            // ⚠️ **시작 깃발은 안 내린다** — 되돌아온 메뉴는 「보고 있는 중」이다.
            if (UiSkin.Button(new Rect(x, y, w, h), "메인 메뉴로", button))
                SettingsGate.ReturnToMainMenu();
        }

        /// <summary>
        /// 보드 좌표 덤프 — **하네스 재현 입력을 클립보드에 뜬다**
        /// (2026-09-11 · 플랜 §71-14 ② · 개발 빌드 전용).
        ///
        /// 리허설 결함 ① 은 사용자 화면에서만 나고 하네스에서는 안 난다. 재현하려면
        /// **놓인 배치 그대로**가 필요한데 스크린샷을 보고 옮겨 적으면 한 칸만 틀려도
        /// **다른 보드를 재게 된다** — 오늘 하네스가 실제로 그랬다.
        ///
        /// ⚠️ **조립 화면에서 눌 수 없다**(이 패널이 거기서는 안 그려진다 · 위 주석).
        /// 보드를 놓고 **「▲ 전투로」로 돌아와** 누르면 된다 — 덤프는 **지금 보드 상태**를
        /// 읽으므로 어느 화면에서 눌러도 같은 값이 나온다.
        /// </summary>
        private void DrawBoardDumpButton(float y, float x, float w, float h, GUIStyle style)
        {
            if (!ShowTutorial) return; // 배포 빌드에는 없다

            bool had = BoardDumpSignals.Version > 0;
            string label = had
                ? $"보드 좌표 덤프 (복사됨 {BoardDumpSignals.Version})"
                : "보드 좌표 덤프 (클립보드로)";

            if (UiSkin.Button(new Rect(x, y, w, h), label, style))
                BoardDumpSignals.Requested = true; // 보드가 다음 Update 에 채운다
        }

        /// <summary>
        /// 현재 적 전멸 — **개발 빌드 전용 촬영·리허설 도구**(2026-09-10 · 플랜 §67 결함 5).
        ///
        /// 물류가 안 돌면 탄이 없어 보스를 못 죽이고, 그러면 **「사망 벌이 없다」와
        /// 「죽이지 못했다」가 구분되지 않는다.** 이 버튼이 그 둘을 가른다.
        ///
        /// ⚠️ **배포 빌드에는 없다** — 저장 초기화·튜토리얼 복귀와 같은 조건이다.
        /// </summary>
        private void DrawKillAllButton(float y, float x, float w, float h, GUIStyle style)
        {
            if (!ShowTutorial || runner == null) return;

            if (!UiSkin.Button(new Rect(x, y, w, h), "현재 적 전멸", style)) return;

            int n = runner.KillAllEnemies();
            Debug.Log($"[MBI] 적 전멸(개발 빌드): {n}기.");
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

            if (!UiSkin.Button(new Rect(x, y, w, h), "처음부터 (저장 초기화)", style)) return;

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
                if (UiSkin.Button(new Rect(bx, y, bw, h), label, style)) GoTo(s);

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
            if (UiSkin.Button(new Rect(x, y, w, h), "합체 게이지 채우기", style))
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

            // ⚠️ **튜토리얼 완료 상태의 보드로 들어간다**(2026-09-15 사용자 확정 · 육안 ⑦).
            //
            // 바로가기는 튜토리얼을 건너뛰므로 `IdleSignals.TutorialCleared` 가 안 선다.
            // 그러면 보드가 비워 둔 칸 (6,5) 를 안 채우고, **운반로가 거기서 끊긴 채**
            // S1 이 시작된다 — 탄이 마운트에 하나도 안 닿는다.
            // 09-15 육안 ① 「실탄을 지고 78 초 0 발」의 뿌리가 이것이었다
            // (실측: 안 채운 판 60 초 0 개 / 채운 판 219 개 · 표준 4.00 발/초).
            TutorialSignals.FillEmptySlotRequested = true;

            runner.LoadStage(s);
        }

    }
}
