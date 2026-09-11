using MBI.Core;
using UnityEngine;

namespace MBI.Logistics
{
    /// <summary>
    /// 화면 레이어 전환(UI 문서 2장) — 한 씬에서 전투(레이어1)와 물류 보드(레이어2)를 슬라이드로 잇는다.
    ///
    /// 설계원칙 "연속성"(1장): 조립 진입 시 전투가 멈추지 않는다 — StageRunner/BoardController는 항상 구동,
    /// 이 컨트롤러는 **카메라만** 두 영역(전투 원점 · 보드 오프셋) 사이로 슬라이드한다(씬 로딩 없음).
    /// 전투 HUD는 StageRunner.OnGUI가 화면 좌상단에 항상 그리므로 두 레이어에서 유지된다.
    ///
    /// MVP: 카메라 슬라이드 + 진입/복귀 버튼.
    /// 변수패널(<see cref="MBI.UI.VariablePanel"/>) · 전투력 이중표시(StageRunner.OutputLine) ·
    /// 미니맵(BoardController.DrawMiniMap)은 **구현 완료** — 이 줄이 「후속」으로 남아 있었다.
    ///
    /// ⚠️ **프리셋은 이번 주 미구현 — 영상 이후 재개**(2026-08-31 확정).
    /// 폐기가 아니라 **순서를 뒤로 민 것**이다. 「구현하지 않는다」로 적어 두면
    /// 다음에 이 파일을 여는 사람이 폐기된 기능으로 읽는다.
    /// 프리셋에 딸린 「자동 프리셋 복귀」(물류 병목 피드백)도 같은 시점까지 함께 빠진다.
    /// </summary>
    public sealed class GameLayerController : MonoBehaviour
    {
        [Header("카메라")]
        [Tooltip("슬라이드 대상. 비우면 Camera.main.")]
        public Camera cam;

        [Header("레이어 1 — 전투 뷰")]
        public Vector2 combatCenter = Vector2.zero;
        public float combatSize = 8f;

        [Header("레이어 2 — 조립/물류 뷰")]
        public Vector2 boardCenter = new Vector2(0f, -20f);
        public float boardSize = 5f;

        [Tooltip("슬라이드 속도(수렴 계수).")]
        public float slideSpeed = 6f;

        private bool _boardView; // true = 조립 레이어

        /// <summary>레이어 버튼 위에 포인터가 있으면 true — BoardController가 보드 입력을 무시(오배치 방지).</summary>
        public static bool PointerOverButton { get; private set; }

        /// <summary>
        /// 레이어 버튼이 차지한 자리. **누르는 순간** 판정하려면 값이 아니라 자리가 있어야 한다.
        ///
        /// <see cref="PointerOverButton"/>은 OnGUI에서 세워지는데 입력 콜백은 그 앞에서 돌아
        /// 항상 한 프레임 전 값이다. 마우스는 커서가 미리 얹혀 있어 맞지만 터치는 얹혀 있는
        /// 시간이 없어, 이 버튼을 눌러도 보드가 같이 눌렸다(BoardController._uiRects 주석).
        /// </summary>
        public static Rect ButtonRect { get; private set; }

        /// <summary>
        /// 조립(물류 보드) 레이어가 활성인가 — BoardController가 팔레트 표시 여부 판단.
        ///
        /// **값은 <see cref="GameViewSignals"/>가 들고 있다.** 여기 따로 두면 원천이 둘이 되고,
        /// 전투 쪽(다른 어셈블리)이 읽는 값과 보드가 읽는 값이 갈릴 수 있다.
        /// </summary>
        public static bool BoardViewActive => GameViewSignals.BoardViewActive;

        [Tooltip("조립 화면 상단에 전투를 비추는 두 번째 카메라. 비우면 처음 볼 때 만든다.")]
        public Camera combatInsetCam;

        private void Start()
        {
            if (cam == null) cam = Camera.main;
            EnsureInsetCamera();
            Snap(false);
        }

        /// <summary>
        /// 조립 화면 위쪽에 전투를 비추는 카메라를 마련한다
        /// (2026-09-10 사용자 확정 · 플랜 §66-21 d · UI 문서 「연속성」).
        ///
        /// **왜 두 번째 카메라인가.** 하나뿐인 카메라는 전투와 보드 사이를 **오간다** —
        /// 한 대로는 두 자리를 동시에 비출 수 없다. 시뮬은 원래도 계속 돌고 있었으므로
        /// **바뀌는 것은 보여 주는가뿐**이다.
        ///
        /// ⚠️ **자리는 코어가 정한다**(<see cref="CombatInsetView"/>) — 경고 띠와 맞닿되
        /// 겹치지 않아야 하고, 그 판정은 띠를 그리는 쪽과 같은 값을 봐야 한다.
        ///
        /// ⚠️ **깊이를 주 카메라보다 높게 둔다.** 낮으면 주 카메라가 나중에 그리며
        /// 화면 전체를 덮어 **이 자리가 지워진다.**
        /// </summary>
        private void EnsureInsetCamera()
        {
            if (combatInsetCam == null)
            {
                var go = new GameObject("CombatInsetCamera");
                go.transform.SetParent(transform, false);
                combatInsetCam = go.AddComponent<Camera>();
            }

            combatInsetCam.orthographic = true;
            combatInsetCam.rect = CombatInsetView.Viewport;
            combatInsetCam.depth = (cam != null ? cam.depth : 0f) + 1f;
            // 전투 배경이 이 자리를 채우지만, 배경이 아직 안 깔린 프레임에 보드가 비치면
            // 두 화면이 겹쳐 보인다 — 자기 자리를 먼저 지운다.
            combatInsetCam.clearFlags = CameraClearFlags.SolidColor;
            combatInsetCam.backgroundColor = cam != null ? cam.backgroundColor : Color.black;
            combatInsetCam.cullingMask = cam != null ? cam.cullingMask : ~0;
            combatInsetCam.transform.position = new Vector3(combatCenter.x, combatCenter.y, -10f);
            combatInsetCam.orthographicSize = combatSize;
            combatInsetCam.enabled = false; // 전투 화면에서는 필요 없다 — 주 카메라가 이미 전투다
        }

        private void Update()
        {
            GameViewSignals.BoardViewActive = _boardView;

            // ⚠️ **조립 화면에서만 켠다.** 전투 화면에서는 주 카메라가 이미 전투를 비추고 있어
            // 같은 그림을 두 번 그리는 낭비이고, 위쪽 30% 만 배율이 달라져 이상하게 보인다.
            if (combatInsetCam != null)
            {
                combatInsetCam.enabled = _boardView;
                combatInsetCam.transform.position =
                    new Vector3(combatCenter.x, combatCenter.y, combatInsetCam.transform.position.z);
                combatInsetCam.orthographicSize = combatSize;
            }

            if (cam == null) return;
            Vector2 tc = _boardView ? boardCenter : combatCenter;
            float ts = _boardView ? boardSize : combatSize;

            Vector3 p = cam.transform.position;
            float k = Mathf.Clamp01(slideSpeed * Time.deltaTime);
            cam.transform.position = Vector3.Lerp(p, new Vector3(tc.x, tc.y, p.z), k);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, ts, k);
        }

        private void Snap(bool board)
        {
            _boardView = board;
            if (cam == null) return;
            Vector2 c = board ? boardCenter : combatCenter;
            cam.transform.position = new Vector3(c.x, c.y, cam.transform.position.z);
            cam.orthographicSize = board ? boardSize : combatSize;
        }

        // 진입/복귀 버튼(하단 중앙, UI 문서 "조립 진입 버튼" 항상 노출). 전투 HUD는 StageRunner가 그림.
        private void OnGUI()
        {
            // 메인 메뉴가 덮고 있으면 그리지 않는다 — IMGUI 는 뒤에 그리는 쪽이 위로 온다
            // (2026-09-10 · 실측: 오프라인 대화상자가 「게임 시작」 버튼을 덮었다).
            if (MainMenuGate.IsOpen) return;
            MBI.UI.UiSkin.Apply(); // 껍데기 + 한글 폰트 — WebGL엔 시스템 폰트 폴백이 없다
            // ⚠️ **날 픽셀 220×46 을 걷었다**(2026-09-11 · 플랜 §68-4 (A)).
            // 문서는 조립 진입을 **막대 600×160**(기준 1440×2560)으로 정해 두었고,
            // 자리는 **액션바 띠**(하단 128) 가운데다. 종전 값은 문서 어디에도 없었고
            // 창이 커질수록 손가락에 비해 작아졌다 — 최소 150px 규정을 한참 밑돈다.
            // ⚠️ **화면마다 자리가 다르다**(2026-09-11 사용자 확정 · 레이어 둘).
            // 전투 화면에서는 **레이어 1** 의 조립 진입 막대(720, 2460 · 600×160)이고,
            // 조립 화면에서는 **레이어 2** 의 액션바 안이다 — 조립 진입 막대는 조립 화면 띠에
            // 안 들어간다. 한 자리로 두면 두 레이어의 수를 섞게 된다.
            var rect = _boardView
                ? MBI.UI.UiLayout.ExitBoardRect(Screen.width, Screen.height)
                : MBI.UI.UiLayout.EnterBoardRect(Screen.width, Screen.height);
            // 글자는 버튼 높이를 따라간다 — 고정 18 이면 큰 버튼 안에서 점이 된다.
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(12, Mathf.RoundToInt(rect.height * 0.28f)),
            };

            // ⚠️ **조립 화면에서는 액션바 띠에 그릇을 깐다**(2026-09-11 실측 · §71-19 ②).
            // 안 깔면 그 자리로 **보드 실루엣의 다리**가 비쳐 보인다 — 부유 띠와 같은 뿌리이며
            // **임시 가림**이다. 보드 뷰포트를 UI 9-4 의 768~2098 로 자르는 구조 정리가
            // 남아 있고 그것은 **구현 재량**이다(촬영 뒤 · `BoardController` 같은 자리 주석).
            // 전투 화면에서는 안 깐다: 거기 막대는 **레이어 1** 이라 띠가 없다.
            if (_boardView)
            {
                Rect bar = MBI.UI.UiLayout.BandRect(
                    MBI.UI.UiLayout.Band.ActionBar, Screen.width, Screen.height);
                MBI.UI.UiBlockers.Add(bar);
                GUI.DrawTexture(bar, MBI.UI.UiSkin.PlateTexture);
            }

            MBI.UI.UiBlockers.Add(rect); // 보드가 누르는 순간 판정한다 — UiBlockers 주석

            // ⚠️ **국면이 허용할 때만 눌린다**(2026-09-11 · 플랜 §71-16 ④).
            // 조립 모드를 고르라고 해 놓고 「전투로」가 눌리면 국면 밖으로 나가 버린다.
            bool allowed = TutorialGate.Allows(_boardView
                ? TutorialGate.Control.ExitBoard
                : TutorialGate.Control.EnterBoard);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && allowed;
            ButtonRect = rect;
            PointerOverButton = rect.Contains(Event.current.mousePosition);

            if (!_boardView)
            {
                // 강제 버튼(튜토리얼 기획서 2장) — 지금 눌러야 할 버튼을 빛나게 한다.
                // 기본 모드가 이동이라 모드를 모르면 화면만 움직이고 벨트가 안 깔린다(T-7).
                Color prev = GUI.color;
                if (TutorialSignals.HighlightBoardButton)
                    GUI.color = new Color(1f, 0.92f, 0.45f,
                        0.75f + 0.25f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f)));

                if (GUI.Button(rect, "▼ 조립 (물류 보드)", style)) _boardView = true;
                GUI.color = prev;
            }
            else
            {
                if (GUI.Button(rect, "▲ 전투로", style)) _boardView = false;
            }

            // ⚠️ **어둠막은 버튼 뒤에** — 앞에 그리면 버튼이 제 바탕으로 덮는다(2026-09-11).
            if (!allowed) GUI.DrawTexture(rect, MBI.UI.UiSkin.DisabledTexture);
            GUI.enabled = wasEnabled;
        }
    }
}
