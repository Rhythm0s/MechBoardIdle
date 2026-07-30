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
    /// MVP: 카메라 슬라이드 + 진입/복귀 버튼. 변수패널·전투력 이중표시·프리셋·미니맵(UI 문서)은 후속.
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

        /// <summary>조립(물류 보드) 레이어가 활성인가 — BoardController가 팔레트 표시 여부 판단.</summary>
        public static bool BoardViewActive { get; private set; }

        private void Start()
        {
            if (cam == null) cam = Camera.main;
            Snap(false);
        }

        private void Update()
        {
            BoardViewActive = _boardView;
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
            var style = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            const float w = 220f, h = 46f;
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 14f, w, h);

            PointerOverButton = rect.Contains(Event.current.mousePosition);

            if (!_boardView)
            {
                if (GUI.Button(rect, "▼ 조립 (물류 보드)", style)) _boardView = true;
            }
            else
            {
                if (GUI.Button(rect, "▲ 전투로", style)) _boardView = false;
            }
        }
    }
}
