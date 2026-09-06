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

        private void Start()
        {
            if (cam == null) cam = Camera.main;
            Snap(false);
        }

        private void Update()
        {
            GameViewSignals.BoardViewActive = _boardView;
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
            MBI.UI.KoreanFont.Apply(); // WebGL엔 시스템 폰트 폴백이 없다
            var style = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            const float w = 220f, h = 46f;
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 14f, w, h);

            MBI.UI.UiBlockers.Add(rect); // 보드가 누르는 순간 판정한다 — UiBlockers 주석
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
        }
    }
}
