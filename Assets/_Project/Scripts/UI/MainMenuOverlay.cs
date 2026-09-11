using MBI.Core;
using MBI.Data;
using UnityEngine;

namespace MBI.UI
{
    /// <summary>
    /// 부팅하면 먼저 뜨는 메인 메뉴 (2026-09-10 사용자 확정 · 플랜 §66-10).
    ///
    /// **이 게임이 무엇인지 먼저 말하는 자리다.** 종전에는 부팅 즉시 전투가 돌아,
    /// 링크를 받은 사람이 **포트폴리오용 데모라는 것을 모른 채** 화면을 봤다.
    ///
    /// **게임은 「게임 시작」 뒤에 시작한다** — <see cref="MainMenuGate"/> 가 잡는다.
    /// 시간을 멈추는 것이 아니라 **시작 자체를 미루는 것**이다(그쪽 주석 참조).
    ///
    /// **슬라이더를 새로 만들지 않는다** — 「볼륨」은 <see cref="AudioOptionsPanel"/> 을 열 뿐이다.
    /// 같은 값을 두 곳에서 그리면 한쪽만 고쳐지는 날이 온다.
    ///
    /// ⚠️ **UI 문서에 이 화면의 절이 없다.** 자리·크기·차례는 구현 가정이며 설계가
    /// 역기입한다(`260910_W02` 6장이 옵션 절을 접수한 것과 같은 자리).
    ///
    /// ⚠️ **영상에는 안 들어간다**(사용자 확정 2026-09-10). 촬영 스크립트는 그대로다.
    /// </summary>
    public sealed class MainMenuOverlay : MonoBehaviour
    {
        [Tooltip("주소와 문구. 비어 있으면 그 버튼은 비활성이다.")]
        public PortfolioLinks links;

        [Tooltip("볼륨 버튼이 열어 줄 패널. 없으면 볼륨 버튼을 안 그린다.")]
        public AudioOptionsPanel audioOptions;

        // 세로 캔버스(1440×2560) 기준 치수 — 구역 이름표·경고 띠와 **같은 환산**을 쓴다.
        private const float DesignHeight = 2560f;
        private const float TitleTop = 640f;
        private const float ButtonW = 720f;
        private const float ButtonH = 132f;
        private const float ButtonGap = 28f;
        private const float ButtonsTop = 1180f;

        private bool _open = true;

        /// <summary>
        /// ⚠️ **Awake 에서 잡는다.** 유니티는 모든 <c>Awake</c> 를 모든 <c>Start</c> 보다 먼저
        /// 돌리므로, 여기서 잠가야 <c>StageRunner.Start</c> 가 그것을 보고 시작을 미룬다.
        /// <c>Start</c> 에서 잠그면 **차례가 보장되지 않아 어떤 프레임에는 이미 시작해 있다.**
        /// </summary>
        private void Awake()
        {
            MainMenuGate.Reset();
            MainMenuGate.Open();
        }

        /// <summary>세로 기준 캔버스의 길이를 지금 창 길이로 환산한다.</summary>
        private static float S(float designPixels) => designPixels * Screen.height / DesignHeight;

        private void OnGUI()
        {
            if (!_open) return;

            // 볼륨 패널이 이 위에 선다(`AudioOptionsPanel.MenuDepth`) — 메뉴가 여는 것이
            // 메뉴 뒤로 들어가면 안 된다.
            GUI.depth = AudioOptionsPanel.MenuDepth;

            UiSkin.Apply(); // 껍데기 + 한글 폰트 — WebGL엔 시스템 폰트 폴백이 없다

            // 화면을 통째로 덮는다. 자리를 내놓아야 아래 보드·전투가 같이 눌리지 않는다.
            var full = new Rect(0f, 0f, Screen.width, Screen.height);
            UiBlockers.Add(full);
            GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.96f);
            GUI.DrawTexture(full, Texture2D.whiteTexture);
            GUI.color = Color.white;

            DrawTitle();
            DrawButtons();
        }

        private void DrawTitle()
        {
            var title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(S(96f)),
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(0f, S(TitleTop), Screen.width, S(140f)), "MECH BOARD IDLE", title);

            // ⚠️ **문구가 제목 바로 아래다.** 버튼 사이에 끼우면 안 읽고 지나간다.
            var notice = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = Mathf.RoundToInt(S(44f)),
                wordWrap = true
            };
            string line = links != null && !string.IsNullOrWhiteSpace(links.notice)
                ? links.notice
                : "포트폴리오용 데모";
            GUI.Label(new Rect(Screen.width * 0.1f, S(TitleTop + 160f), Screen.width * 0.8f, S(220f)),
                line, notice);
        }

        private void DrawButtons()
        {
            var style = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(S(48f)) };
            float x = (Screen.width - S(ButtonW)) * 0.5f;
            float y = S(ButtonsTop);
            float h = S(ButtonH);
            float step = h + S(ButtonGap);

            if (GUI.Button(new Rect(x, y, S(ButtonW), h), "게임 시작", style)) StartGame();
            y += step;

            if (audioOptions != null)
            {
                if (GUI.Button(new Rect(x, y, S(ButtonW), h), "볼륨", style)) audioOptions.Open();
                y += step;
            }

            y = LinkButton(x, y, h, step, style, "포트폴리오 문서 보기",
                links != null ? links.documentUrl : null);
            y = LinkButton(x, y, h, step, style, "관련 노션 보기",
                links != null ? links.notionUrl : null);

            DrawVersion();
        }

        /// <summary>
        /// 주소가 없으면 **비활성으로 그린다** — 없애지 않는다.
        /// 없애면 「원래 없는 기능」으로 읽히고, 비활성이면 **아직 안 들어온 값**으로 읽힌다.
        ///
        /// ⚠️ <c>Application.OpenURL</c> 은 웹에서 **버튼을 누른 그 순간(사용자 제스처) 안에서만**
        /// 새 탭이 열린다. 여기서 바로 부르므로 성립한다 — 한 프레임 미루면 막힌다.
        /// </summary>
        private float LinkButton(float x, float y, float h, float step, GUIStyle style,
            string label, string url)
        {
            bool usable = PortfolioLinks.IsUsable(url);

            bool was = GUI.enabled;
            GUI.enabled = usable;
            if (GUI.Button(new Rect(x, y, S(ButtonW), h), usable ? label : label + "  (준비 중)", style))
                Application.OpenURL(url);
            GUI.enabled = was;

            return y + step;
        }

        private void DrawVersion()
        {
            if (links == null || string.IsNullOrWhiteSpace(links.version)) return;
            var small = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = Mathf.RoundToInt(S(36f))
            };
            GUI.Label(new Rect(0f, Screen.height - S(120f), Screen.width, S(80f)), links.version, small);
        }

        private void StartGame()
        {
            _open = false;
            MainMenuGate.Close();
        }
    }
}
