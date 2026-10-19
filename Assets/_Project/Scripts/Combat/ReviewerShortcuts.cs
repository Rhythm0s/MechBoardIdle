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

        /// <summary>설정 판이 화면보다 길 때의 굴림 자리(2026-10-14).</summary>
        private Vector2 _settingsScroll;

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

            // ✅ **글자 2배 · 판 1.75배**(2026-09-21 사용자 확정 ④ — 「지금 좁고 작음」).
            //    ⚠️ 배수 둘 다 가정이다(받은 말은 「글자 2배 · 1.5~2배」). 가운데를 잡았다.
            //    ⚠️ 글자는 **사다리 밖 크기**를 쓴다 — 이 판은 개발용이라 아틀라스 예산
            //       계산(`KoreanFont.Ladder`)에 안 든다. 13 → 26 은 사다리에 없는 수다.
            // ⚠️ **1.75 → 2.6**(2026-09-21 리허설 ④ — 「지금도 작음 · 오른쪽 글자 잘림」).
            //    글자도 26 → 40 이다(구 13 의 세 배 남짓). ⚠️ 둘 다 가정.
            //
            // ⚠️⚠️ **판이 화면을 넘지 않게 묶는다.** 236 × 2.6 = 614 인데 좁은 창에서는
            //    화면이 그보다 좁을 수 있다 — 넘으면 오른쪽 글자가 잘린다(이번에 난 일).
            //    그래서 **화면 폭에서 여백을 뺀 값**을 상한으로 둔다.
            // ⚠️⚠️ **배율을 화면에서 낸다**(2026-09-21 · 내가 띄워 보고 두 번 고쳤다).
            //
            // 1.75 는 작았고(사용자 ④), 2.6 은 **판이 화면 아래로 넘쳤다**(내 실측).
            // 박힌 배수로는 창 크기마다 한쪽이 깨진다 — **넘지 않는 선에서 가장 크게** 잡는다.
            //   · 가로: 236 이 창 폭에 여백 16 을 두고 들어가는 배수
            //   · 세로: 판 전체(줄 아홉치)가 창 높이에 들어가는 배수
            // ⚠️ 상한 2.6 · 하한 1.0 은 가정이다.
            const float rowsTall = 8f * 26f + 9f * 4f + 44f + 16f;   // 배율 1 일 때의 판 높이
            float scale = Mathf.Clamp(
                Mathf.Min((Screen.width - 32f) / 236f, (Screen.height - 32f) / rowsTall),
                1f, 2.6f);

            var button = new GUIStyle(GUI.skin.button)
            { fontSize = Mathf.Max(13, Mathf.RoundToInt(15f * scale)) };
            float w = 236f * scale;
            float h = 26f * scale, pad = 4f * scale;

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
            // ✅ **화면 가운데**(2026-09-21 사용자 확정 ③).
            //    🗑️ 구 자리(왼쪽 아래 `x = 12`) 폐기 — 09-14 에 오른쪽 아래에서 도망쳐 온
            //    자리였고, 그 뒤로도 「안 보인다」가 두 번 더 났다(09-19 판이 화면 밖).
            //    가운데는 **다투는 것이 없는 유일한 자리**다: 원형 둘은 오른쪽, 조립
            //    진입 막대는 아래, 칩 줄은 위다. 그리고 이제 **판이 열리면 게임이 선다** —
            //    가운데를 가려도 가릴 것이 안 움직인다.
            float x = Mathf.Max(8f, (Screen.width - w) * 0.5f);

            // ⚠️ **판을 깐다**(2026-09-18) — 볼륨 패널과 같은 문법이다. 종전에는 버튼만
            //    흙바닥 위에 떠 있어 어디까지가 이 패널인지가 안 읽혔다.
            //    높이는 **줄 수에서 낸다** — 고정값을 박으면 줄이 늘 때 마지막이 잘린다.
            // 🗑️ **줄 수를 `ShowTutorial` 로 세던 것 폐기** — 배포 빌드에서 **판만 짧아졌다.**
            //    개발 전용 버튼 셋(전멸·초기화·덤프)은 **안 그려도 `y` 는 그대로 내려가므로**
            //    차지하는 높이가 두 빌드에서 **같다.** 세는 쪽만 달랐던 것이다.

            // ⚠️⚠️ **판을 바닥에서 올려 세운다**(2026-09-19 사용자 육안 ⑤ · 스크린샷 1).
            //
            // 종전은 윗변을 `Screen.height - 190` 으로 **박아** 두고 높이만 줄 수에서 냈다.
            // 줄이 여덟이면 판이 **318** 이라 아래로 130 넘게 흘러 **「처음부터」·「보드 좌표
            // 덤프」·「메인 메뉴로」가 화면 밖에 있었다** — 「메인 메뉴로」를 09-18 에 붙여
            // 놓고도 사용자 화면에서는 **한 번도 보인 적이 없던** 까닭이 이것이다.
            //
            // 📌 **높이를 먼저 내고 윗변을 거기서 뺀다** — 줄이 늘어도 다시 안 잘린다.
            //    아랫변 여백 16 은 가정이다(다른 패널과 같은 여백).
            // ✅ **닫기 한 줄이 늘었다**(2026-09-21 사용자 확정 ③ — 「닫기 버튼 = 패널 하단」).
            // ⚠️⚠️ **줄 수를 한 곳에서 낸다**(2026-10-13). 「도전 즉시 종료」는
            //    **도전 중일 때만** 뜨는 유일한 줄이라, 높이와 그리는 쪽이 따로 세면
            //    둘이 어긋난다 — 높이만 7 로 두면 뜬 날 **「닫기」가 판 밖으로** 밀리고
            //    (09-19 에 「메인 메뉴로」가 그렇게 사라졌다), 자리만 밀면 안 뜬 날
            //    **빈 칸**이 남는다(빌드 실측). 한 값이 두 곳에 살면 안 된다(지침 §7).
            bool showFinish = ShowTutorial && runner != null && runner.CanFinishChallengeNow;
            // 스테이지 · 게이지 · 전멸 · [도전 즉시 종료] · 초기화 · 덤프 · 메인 메뉴 · 닫기
            // 🗑️ 「강화(#150)」 줄 — 사용자 확정 #179(10-19)로 걷음. 강화 길 = 마운트 팝오버(#171) 하나.
            int buttonRows = showFinish ? 8 : 7;
            float noteH = 44f * scale;  // 안내 두 줄

            // ✅ **소리 줄이 이 판 안으로 들어왔다**(2026-10-14 사용자 확정 ⑤).
            //    ⚠️ 높이를 **반드시** 더한다 — 안 더하면 아래 단추들이 판 밖으로 밀린다
            //    (09-19 에 「메인 메뉴로」가 그렇게 사라졌다 · 위 주석의 그 병이다).
            float soundH = MBI.UI.AudioOptionsPanel.ChannelsHeight(scale);
            float plateH = 8f * scale + (h + pad) + noteH + soundH
                           + (h + pad) * buttonRows + 8f * scale;
            // ⚠️⚠️ **화면보다 길면 굴린다**(2026-10-14 · 소리 줄이 들어온 날 바로 걸렸다).
            //
            // 종전에는 「위에서 8 을 띄우고 시작」하는 것이 전부라, 판이 화면보다 길면
            // **아래가 그냥 잘렸다** — 소리 줄 셋(210 × 배율)이 들어오자 그날로
            // **「닫기」가 화면 밖**으로 밀렸다(09-19 에 「메인 메뉴로」가 사라진 그 병이
            // 같은 꼴로 돌아온 것이다). 줄이 또 늘어도 안 잘리게 **굴림틀**을 둔다.
            //
            // 📌 안쪽 좌표를 그대로 쓴다 — 보임틀의 `viewRect` 를 **판 자리 그대로** 주면
            //    아래 그리는 코드(전부 절대 좌표다)를 한 줄도 안 고쳐도 된다.
            float avail = Screen.height - 16f;
            bool scroll = plateH > avail;
            float plateDrawH = scroll ? avail : plateH;

            // ⚠️⚠️ **세로 막대가 먹는 폭만큼 내용을 좁힌다**(2026-10-14 · 빌드 실측).
            //    안 좁히면 내용이 보임틀보다 넓어 **가로 막대까지 생기고**, 그 막대가
            //    맨 아랫줄을 먹어 **「닫기」가 또 잘렸다.** 막대 폭은 껍데기에서 받는다 —
            //    배율로 지어내면(18 × 0.3 = 5 px) 실제 막대(15 px)보다 좁아 또 넘친다.
            float barW = scroll
                ? GUI.skin.verticalScrollbar.fixedWidth + GUI.skin.verticalScrollbar.margin.left
                : 0f;

            float y = Mathf.Max(8f, (Screen.height - plateDrawH) * 0.5f);

            var plate = new Rect(x - 8f * scale, y - 8f * scale,
                                 w + 16f * scale, plateDrawH);

            // ✅ **판 빼고 화면을 어둡게**(2026-09-21 사용자 확정 ⑤).
            //    판보다 **먼저** 깐다 — 나중에 깔면 판까지 덮는다(IMGUI 는 뒤가 위다).
            //    ⚠️ 사각 넷으로 두른다: 통째로 덮고 판을 다시 그리면 판 **그림자**가
            //       어둠 위에 떠서 테두리가 두 겹으로 보인다.
            //    ⚠️ 알파 0.62 는 가정이다.
            //
            // 🗑️ **여기서 어둠을 깔던 것 폐기**(2026-10-15 사용자 확정 · #133).
            //    이 OnGUI 는 `StageRunner.OnGUI` 보다 **먼저** 돈다 — 그래서 HUD 칩·원형 셋이
            //    이 어둠 **위**에 밝게 남았다(10-15 8126 실측 · T3 막과 같은 병).
            //    이제 **구멍 자리만 적어 두고**, 어둠은 `StageRunner.OnGUI` 끝이
            //    <see cref="DrawSettingsDim"/> 로 깐다. **판과 단추는 여기 그대로 둔다** —
            //    판째로 뒤로 옮기면 그 아래 HUD 단추가 눌림을 먼저 가져간다
            //    (IMGUI 는 그리기는 나중이 위 · 누름은 먼저가 이긴다 · 삭제 창 #126 의 그 병).
            //    구멍은 **판만**이다. 「- 일시 정지 -」는 어둠을 깐 **뒤에** 그 위에 다시 쓴다
            //    (아래 `DrawSettingsDim`) — 구멍에 줄까지 넣었더니 판 아래에 **안 어두운 띠**가
            //    생겼다(10-15 8127 실측). 원래 설계는 「어둠 위에 밝은 글자」다.
            s_dimHole = plate;
            s_pausedRect = new Rect(plate.x, plate.yMax + 8f * scale, plate.width, 36f * scale);
            s_pausedPx = Mathf.Max(14, Mathf.RoundToInt(16f * scale));

            UiPlate.Draw(plate);
            UiBlockers.Add(plate);

            if (scroll)
            {
                // 굴림 막대가 오른쪽을 먹으므로 내용 폭을 그만큼 줄여 준다.
                _settingsScroll = GUI.BeginScrollView(
                    plate, _settingsScroll,
                    new Rect(plate.x, plate.y, plate.width - barW, plateH));

                // ⚠️ **판을 세운 뒤에** 좁힌다 — 판까지 좁히면 보임틀과 내용이 같이 줄어
                //    가로 막대가 끝내 안 사라진다(셈을 해 보면 8 × 배율 ≥ 막대 폭이라야 하는데
                //    작은 창에서는 그 조건이 안 선다).
                w -= barW;
            }

            var title = new GUIStyle(GUI.skin.label)
            { fontSize = Mathf.Max(14, Mathf.RoundToInt(17f * scale)), fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(x, y, w, h), "설정", title);
            y += h + pad;

            // 안내 — 이것이 있어야 「밸런스를 못 맞춰 넣었나」로 안 읽힌다.
            //
            // ⚠️ **두 줄 자리를 준다**(2026-09-19 사용자 육안 ⑤). 30 은 한 줄치라 둘째 줄이
            //    **아래 버튼 밑으로 깔려** 「…원래 진행은 튜토」까지만 읽혔다. 끊는 자리도
            //    문장 사이로 못 박는다 — 맡겨 두면 「튜토 / 리얼」로 갈린다.
            var note = new GUIStyle(GUI.skin.label)
            { fontSize = Mathf.Max(12, Mathf.RoundToInt(13f * scale)), wordWrap = true };
            GUI.Label(new Rect(x, y, w, noteH),
                "포트폴리오용 바로가기입니다.\n원래 진행은 튜토리얼부터 순서대로입니다", note);
            y += noteH;

            // ── 소리 (2026-10-14 사용자 확정 ⑤) ──────────────────────────
            //
            // 📌 **그리는 것은 `AudioOptionsPanel` 하나다**(지침 §7) — 값·저장·미리듣기가
            //    거기 살고, 여기서 또 슬라이더를 만들면 같은 값이 두 곳에 사는 꼴이 된다.
            //    종전에는 그 패널이 **떠 있는 판**으로 혼자 폈고, 그것이 진행 띠와
            //    스테이지·도전 단추 위에 겹쳐 앉았다(사용자 스크린샷).
            y += MBI.UI.AudioOptionsPanel.DrawChannels(x, y, w, scale);

            // 🗑️ **설정 판 강화 단추 폐기**(사용자 확정 #179 · 2026-10-19) — 강화 길은 마운트 팝오버(#171) 하나다.
            //    신호(`RequestMountEnhance`) · 규칙(`MountEnhanceRule`) · 저장 표식 · #162 점프 자동은 그대로.
            //    아래 단추들이 한 줄 위로 당겨진다. `DrawEnhanceButton` 은 지우지 않고 남긴다(부르는 곳 0 · 폐기 표기).

            DrawStageButtons(y, x, w, h, pad, button);
            y += h + pad;

            DrawGaugeButton(y, x, w, h, button);
            y += h + pad;

            DrawKillAllButton(y, x, w, h, button);
            y += h + pad;

            // ⚠️ 위에서 낸 `showFinish` 를 그대로 쓴다 — 여기서 다시 묻지 않는다.
            if (showFinish)
            {
                DrawFinishChallengeButton(y, x, w, h, button);
                y += h + pad;
            }

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
            y += h + pad;

            // ✅ **닫기는 판 맨 아래**(2026-09-21 사용자 확정 ③).
            //    칩 줄의 설정 아이콘으로도 닫히지만, **판을 보고 있는 사람의 손이
            //    닿는 자리**는 판 안이다.
            if (UiSkin.Button(new Rect(x, y, w, h), "닫기", button))
                SettingsGate.Close();

            if (scroll) GUI.EndScrollView();

            // ✅ **판 바깥 아래에 「- 일시 정지 -」**(2026-09-21 사용자 확정 ⑥).
            //    판이 열려 있는 동안 게임이 서므로, **왜 멈춰 있는지**를 화면이 말해야 한다.
            //    ⚠️ 판 **밖**이라 어둠 위에 앉는다 — 그래서 밝은 글자다.
            //    🗑️ 여기서 그리던 것 폐기(10-15 · #133) — 이 OnGUI 뒤에 `StageRunner` 가 그리는 것
            //       (마일스톤 카드 · 어둠막)이 이 글자를 덮었다. 이제 `DrawSettingsDim` 이
            //       어둠을 깐 **뒤에** 쓴다. 글자는 단추가 아니라 옮겨도 눌림과 무관하다.
        }

        /// <summary>
        /// 판을 뺀 화면을 어둡게 (2026-09-21 사용자 확정 ⑤).
        ///
        /// ⚠️ **사각 넷으로 두른다.** 화면을 통째로 덮고 판을 다시 그리는 길도 있지만,
        /// 그러면 판 그림자가 어둠 위에 떠서 **테두리가 두 겹**으로 보인다.
        /// ⚠️ 알파는 가정이다.
        /// </summary>
        /// <summary>
        /// 설정 판 어둠막의 **구멍** — 이 패널이 그릴 때마다 적는다(판 + 「- 일시 정지 -」 줄).
        /// </summary>
        private static Rect s_dimHole;

        /// <summary>판 아래 「- 일시 정지 -」 자리와 글자 크기 — 이 패널이 그릴 때마다 적는다.</summary>
        private static Rect s_pausedRect;
        private static int s_pausedPx = 14;

        /// <summary>
        /// **설정 판 어둠막을 깐다 — `StageRunner.OnGUI` 의 끝에서만 부른다**(2026-10-15 · #133).
        ///
        /// HUD 칩·Stage 띠·배지·원형 셋이 전부 그 OnGUI 에서 그려지므로, 그 끝에서 깔아야
        /// HUD 가 어둠 **아래**로 간다. 판·단추는 이 패널의 OnGUI 가 이미 그렸고 구멍이 그 자리를 비운다.
        /// 어둠은 그림뿐이라 눌림을 안 가져간다 — 판의 단추는 그대로 눌린다.
        /// </summary>
        public static void DrawSettingsDim()
        {
            if (!SettingsGate.IsOpen || MainMenuGate.IsOpen) return;
            if (GameViewSignals.BoardViewActive) return;   // 판이 안 그려지는 화면 — 구멍도 없다
            if (s_dimHole.width <= 1f || s_dimHole.height <= 1f) return;
            DrawDimAround(s_dimHole);

            // ✅ **판 바깥 아래에 「- 일시 정지 -」**(2026-09-21 사용자 확정 ⑥).
            //    판이 열려 있는 동안 게임이 서므로, **왜 멈춰 있는지**를 화면이 말해야 한다.
            //    ⚠️ 판 **밖**이라 어둠 위에 앉는다 — 그래서 밝은 글자다. 어둠 **뒤에** 쓴다.
            var pausedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = s_pausedPx,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            pausedStyle.normal.textColor = new Color(0.95f, 0.92f, 0.75f, 0.95f);
            GUI.Label(s_pausedRect, "- 일시 정지 -", pausedStyle);
        }

        private static void DrawDimAround(Rect plate)
        {
            var dim = new Color(0f, 0f, 0f, 0.62f);
            float sw = Screen.width, sh = Screen.height;

            HudBars.Fill(new Rect(0f, 0f, sw, Mathf.Max(0f, plate.y)), dim);
            HudBars.Fill(new Rect(0f, plate.yMax, sw, Mathf.Max(0f, sh - plate.yMax)), dim);
            HudBars.Fill(new Rect(0f, plate.y, Mathf.Max(0f, plate.x), plate.height), dim);
            HudBars.Fill(new Rect(plate.xMax, plate.y,
                                  Mathf.Max(0f, sw - plate.xMax), plate.height), dim);
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
        /// **도전 즉시 종료** — 개발 빌드 전용(2026-10-13 사용자 요청).
        ///
        /// 결과 화면(단추 셋)을 보려면 도전을 끝까지 돌려야 하는데 S1 이 95 초다.
        /// 자리·문안을 볼 때마다 그만큼 기다리면 눈으로 고치는 일이 안 된다.
        ///
        /// ⚠️ **도전 중일 때만 뜬다** — 파밍에는 끝이 없고 튜토리얼에는 도전이 없다.
        ///    안 뜨는 자리에 회색 단추를 두면 「왜 안 눌리나」를 또 묻게 된다.
        /// ⚠️ 승패는 러너가 지어내지 않는다(`FinishChallengeNow` 주석).
        /// </summary>
        /// <remarks>⚠️ 띄울지 말지는 <c>showFinish</c> 가 **한 곳에서** 정한다 — 여기서 또 묻지 않는다.</remarks>
        private void DrawFinishChallengeButton(float y, float x, float w, float h, GUIStyle style)
        {
            if (!UiSkin.Button(new Rect(x, y, w, h), "도전 즉시 종료 (결과 화면)", style)) return;

            runner.FinishChallengeNow();
            SettingsGate.Close();   // 판이 열려 있으면 결과가 그 뒤에서 난다
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
        /// <summary>
        /// **마운트 강화 단추** (2026-10-16 사용자 확정 #150 · 최소판).
        ///
        /// ⚠️ 자리 = 설정 판(구현 판단). 지시는 「HUD 강화재료 표시 옆」이었는데 그 표시는
        ///    「i」 진단 패널 안에만 있고, 상단 칩 줄은 고철·골드·설정으로 차 있다.
        ///    설정 판은 줄 수에서 높이를 내고 굴림틀이 있어 한 줄을 더해도 안 잘린다.
        /// ⚠️ 바로가기가 아니라 **진행 단추**다 — 배포 빌드에도 늘 그린다(ShowTutorial 과 무관).
        /// 재료를 쓰는 것은 방치 런타임이다 — 여기서는 요청만 놓는다(지갑을 두 곳이 만지지 않는다).
        /// </summary>
        // 🗑️ 폐기(#179) — 부르는 곳 0. 강화 단추는 마운트 팝오버(`BoardController.DrawMountPanel`)에만 있다.
        private void DrawEnhanceButton(float y, float x, float w, float h, GUIStyle style)
        {
            float cost = runner != null ? runner.EnhanceCost : 0f;
            bool enhanced = IdleSignals.MountEnhanced;
            string label = enhanced
                ? MountEnhanceRule.AppliedLabel(runner != null ? runner.EnhancedMountCoefValue : 1f)
                : MountEnhanceRule.ButtonLabel;

            bool was = GUI.enabled;
            GUI.enabled = was && MountEnhanceRule.CanEnhance(enhanced, IdleSignals.WalletEnhMaterial, cost,
                                                             IdleSignals.WalletScrap, BuildCost.EnhanceScrap);   // #172 재료 + 고철
            if (UiSkin.Button(new Rect(x, y, w, h), label, style))
                IdleSignals.RequestMountEnhance(cost);
            GUI.enabled = was;
        }

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

            // ⚠️⚠️ **자동 교대도 되돌린다**(2026-10-13 사용자 결함 · 빌드 cf82fb6 —
            //    「처음부터 뒤 게임이 로봇 B 로 시작한다」).
            //
            // **왜 이것이 B 를 세웠나.** 초기화는 새 시뮬을 짓고 새 시뮬의 활성은 A 다
            // (`TagBattle` 생성자). 그런데 **마운트 둘이 다 비어 있는 상태**로 서므로
            // `TagSystem.EvaluateAuto` 의 `activeDepleted` 가 **첫 틱에 참**이 되고,
            // 자동 교대가 켜져 있으면 **곧바로 B 로 넘어간다.** 편집 탭은 활성 로봇을
            // 따라가므로(`BoardTabFollow`) 조립 화면도 B 판을 연다 — 사용자가 본 것이다.
            //
            // 📌 **탭을 따로 안 건드린다.** 탭은 활성 로봇의 **그림자**라 활성이 A 면
            //    저절로 A 다 — 둘을 각자 맞추면 답이 둘이 된다(지침 §7).
            //
            // ⚠️⚠️ **종전 판단을 뒤집는다.** `TagAutoMode` 주석은 「조작 취향이라 저장
            //    초기화로 안 지운다」고 적어 두었다(09-16). 그 까닭(촬영마다 다시 켜야
            //    한다)은 여전히 참이지만, **지금은 켜져 있으면 튜토리얼이 B 로 선다** —
            //    B 는 S4 를 넘겨야 열리는 로봇이다(#84). 로봇 잠금이 서면 그 잠금이
            //    이 자리를 대신하므로, 이 줄은 **잠금이 설 때 다시 볼 자리**다.
            TagAutoMode.Reset();

            // ⚠️ **비워 둔 칸은 맨 마지막에 요청한다.** Reenter가 고스트를 다시 걸면서
            // TutorialSignals.Reset()을 부르는데, 그것이 이 요청까지 지운다 —
            // 앞에 두었더니 병합기가 안 지워져 넷 중 셋만 되돌아갔다(2026-09-02 실측).
            TutorialSignals.ClearEmptySlotRequested = true;
        }

        /// <summary>
        /// 스테이지 버튼은 **가로로 깐다.** 세로로 쌓으면 일곱 개가 미니맵까지 내려가 겹친다.
        /// </summary>
        private void DrawStageButtons(float y, float x, float w, float h, float pad, GUIStyle style)
        {
            if (runner == null || stages == null) return;

            // ⚠️ 폭을 패널에서 역산한다. 40으로 고정했더니 일곱 개가 S4에서 잘렸다
            // (2026-09-01 브라우저 실측). 스테이지가 늘어도 안 잘리게 나눠 쓴다.
            int count = 0;
            for (int i = 0; i < stages.Count; i++) if (Shows(stages[i])) count++;
            if (count == 0) return;

            // ⚠️⚠️ **똑같이 나누면 한글 버튼만 잘린다**(2026-09-21 사용자 육안 ⓑ —
            //    「튜토」가 **「류토」처럼** 보인다).
            //
            // 236 을 일곱으로 나누면 한 칸이 **30px** 인데, 13px 한글 두 자는 테두리까지
            // 26 + 여백이라 **칸을 넘는다.** `S1` 은 라틴 두 자라 들어가서, 화면에서는
            // **한글 버튼 하나만** 망가진 것처럼 보였다 — 글자가 잘리며 「튜」의 오른쪽이
            // 날아가 다른 글자로 읽혔다.
            //
            // 📌 **폭은 글자에서 낸다** — 칸 수로 나누면 「무엇이 들어가는가」를 안 본 것이다.
            //    09-16 이름판 · 09-19 태그 원형과 **같은 병**이고, 고치는 법도 같다.
            var labels = new List<string>(count);
            for (int i = 0; i < stages.Count; i++)
                if (Shows(stages[i]))
                    // 튜토리얼은 번호를 안 쓴다(260902_W09 §2). 버튼이 좁아 짧게 적는다.
                    labels.Add(stages[i] == tutorialStage ? "튜토" : stages[i].stageId);

            // ⚠️ **판 폭을 받아 쓴다**(2026-09-21). 종전에는 236 이 여기 박혀 있어서,
            //    판을 키워도 이 줄만 옛 폭으로 나뉘어 버튼이 점처럼 작았다.
            float avail = w - pad * (count - 1);

            // ⚠️ **새 글자 크기를 안 만든다** — 12·13 은 이 패널이 이미 쓰는 둘이다.
            //    동적 폰트 아틀라스는 크기마다 굽는다(09-15 촬영 차단 결함의 뿌리).
            // ⚠️ **버튼 글자도 판을 따라 커진다**(2026-09-21 리허설 ④ — 「튜토·S1 이 아주 작다」).
            //    종전에는 13 에서 12 로만 내렸는데, 판이 2.6 배가 된 지금 그 크기는 점이다.
            //    ⚠️ 40 → 32 → 26 으로만 내린다(셋 다 이 판이 쓰는 크기 · 새 크기를 안 만든다).
            var need = new float[count];
            float sum = Measure(style, labels, need);
            for (int shrink = 0; shrink < 3 && sum > avail; shrink++)
            {
                style.fontSize = Mathf.Max(11, Mathf.RoundToInt(style.fontSize * 0.85f));
                sum = Measure(style, labels, need);
            }

            float bx = x;
            int k = 0;
            for (int i = 0; i < stages.Count; i++)
            {
                StageDefinition s = stages[i];
                if (!Shows(s)) continue;

                bool here = runner.CurrentStage == s;
                Color prev = GUI.color;
                if (here) GUI.color = new Color(1f, 0.92f, 0.45f); // 지금 있는 곳

                // 남는 폭은 **글자 넓이에 비례해서** 나눈다. 모자라면 같은 비율로 줄어
                // 「한 칸만 잘리는」 일이 안 생긴다.
                float bw = sum > 0.01f ? avail * (need[k] / sum) : avail / count;
                if (UiSkin.Button(new Rect(bx, y, bw, h), labels[k], style)) GoTo(s);

                GUI.color = prev;
                bx += bw + pad;
                k++;
            }
        }


        /// <summary>
        /// 버튼마다 **글자가 드는 폭**을 잰다. 돌려주는 것은 그 합이다.
        ///
        /// ⚠️ <see cref="GUIStyle.CalcSize"/> 는 테두리 여백까지 넣어 잰다 — 거기에
        /// 손가락 여백을 조금 더한다(⚠️ 4 는 가정).
        /// </summary>
        private static float Measure(GUIStyle style, List<string> labels, float[] need)
        {
            float sum = 0f;
            for (int i = 0; i < labels.Count; i++)
            {
                need[i] = style.CalcSize(new GUIContent(labels[i])).x + 4f;
                sum += need[i];
            }
            return sum;
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

            // 사용자 확정 #162 — S4 이상 점프는 강화 표식을 켠다(재료 안 씀 · 판이 서기 **전에** 켜야 계수가 바로 선다).
            if (s != null && MountEnhanceRule.JumpTurnsOn(StageMapName.NumberOf(s.stageId)))
                IdleSignals.RequestJumpEnhance();
            // #195 — S6 점프는 B 표식도(A · B 둘 다 자동).
            if (s != null && MountEnhanceRule.JumpTurnsOnB(StageMapName.NumberOf(s.stageId)))
                IdleSignals.RequestJumpEnhanceB();

            // 사용자 확정 #167 · #168 — S5 · S6 점프는 로봇 A 보드를 폭발탄 프리셋으로 세운다(저장 보드 덮음 ⚠️ 가정).
            if (s != null && PresetBoards.ExplosiveOnJump(StageMapName.NumberOf(s.stageId)))
                PresetBoards.ExplosiveRequested = true;

            // 사용자 확정 #103 → #173 — 로봇 B 자동 해금은 **바로가기 S5 · S6 점프에만** 남는다(S1~S4 점프는 끔 ·
            //    골드로 산 해금은 저장 칸이라 그대로). 판이 서기 **전에** 세운다(태그 원형 · 자동 교대가 첫 프레임에 읽는다).
            if (s != null) RobotBGate.JumpUnlocked = RobotBGate.JumpOpens(StageMapName.NumberOf(s.stageId));

            runner.LoadStage(s);
            runner.FillMountsForJump();   // 사용자 확정 #156 — 바로가기 점프는 마운트 가득으로 든다(합체 게이지는 그대로)
        }

    }
}
