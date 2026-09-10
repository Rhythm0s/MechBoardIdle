using MBI.Core.Audio;
using UnityEngine;

namespace MBI.UI
{
    /// <summary>
    /// 배경 음악 볼륨 조절 (2026-09-10 사용자 확정 · 「기본 30% · 조절 가능하도록」).
    ///
    /// **가장 작은 것으로 둔다** — 버튼 하나와 슬라이더 하나다. 사운드 문서 7장이 채널을
    /// 셋으로 갈라 두었으므로 효과음·조작음도 같은 자리에 붙을 수 있지만,
    /// **지시받은 것은 배경 음악 하나**라 그것만 만들었다.
    ///
    /// ⚠️ **값은 사람 것이라 기기에 남긴다**(`PlayerPrefs`). 리포에는 안 들어간다 —
    /// SO 의 `musicVolume` 은 **기본값**이고 여기서 고친 값은 그 사람의 것이다.
    ///
    /// ⚠️ **UI 문서에 이 패널의 규정이 없다.** 자리·크기·여는 방법은 구현 가정이며,
    /// 규정이 서면 그쪽을 따른다. 지금은 **오른쪽 아래 · 심사자용 바로가기 바로 위**다.
    ///
    /// ⚠️ **자리를 두 번 옮겼다**(둘 다 2026-09-10 실측).
    /// ① 오른쪽 위 구석 — <see cref="VariablePanel"/> 이 같은 구석을 300×250 으로 차지해
    ///    **버튼이 그려지기만 하고 안 눌렸다**(겹친 자리는 뒤에 그리는 쪽이 클릭을 먹는다).
    /// ② 변수 패널 바로 아래 — 이번에는 **노드 팔레트 제목과 글자가 겹쳤다**
    ///    (「노드 팔레소리▼」). 오른쪽 위는 위에서 아래로 변수 패널과 팔레트가 잇달아 차지한다.
    ///
    /// 그래서 **아래에서 위로** 붙인다. 화면 세로가 얼마든 바닥에서 같은 거리라
    /// 위쪽이 무엇으로 차든 안 걸린다 — 위에서 재던 것이 두 번 다 걸린 자리다.
    /// </summary>
    public sealed class AudioOptionsPanel : MonoBehaviour
    {
        /// <summary>기기에 남기는 자리. 이름이 바뀌면 사람이 고른 값이 사라진다.</summary>
        private const string PrefKey = "mbi.music.volume";

        private const float ButtonW = 96f;
        private const float ButtonH = 32f;
        private const float PanelW = 240f;
        private const float PanelH = 76f;
        private const float Margin = 12f;

        /// <summary>메인 메뉴가 쓰는 깊이. 이 패널은 그보다 **하나 앞**에 선다.</summary>
        public const int MenuDepth = 0;

        /// <summary>
        /// 바닥에서 버튼 아랫변까지의 거리. **심사자용 바로가기 패널 바로 위**다 —
        /// 그 패널이 <c>Screen.height - 190</c> 에서 시작하므로 그보다 위에 선다.
        ///
        /// ⚠️ **아래에서 잰다.** 위에서 재면 변수 패널·노드 팔레트가 차례로 자리를 넓힐 때마다
        /// 다시 밀려야 한다 — 실제로 2026-09-10 에 두 번 걸렸다.
        ///
        /// ⚠️ **숫자를 여기 두는 것은 임시다.** 화면 요소의 자리를 한곳에서 정하는 규정이
        /// UI 문서에 서면 그쪽을 따른다(설계 몫 · `260910_W02` 6장에서 옵션 절 신설로 접수됨).
        /// </summary>
        private const float BottomInset = 190f + ButtonH + Margin;

        private bool _open;
        private bool _loaded;

        /// <summary>
        /// 밖에서 연다 — 메인 메뉴의 「볼륨」이 부른다.
        /// **슬라이더를 새로 만들지 않기 위해서다**(플랜 §66-10). 같은 값을 두 곳에서
        /// 그리면 한쪽만 고쳐지는 날이 온다.
        /// </summary>
        public void Open() => _open = true;

        private void Awake() => Load();

        /// <summary>
        /// 기기에 남은 값을 읽는다. 없으면 <see cref="MusicVolume.StartupDefault"/> —
        /// **배포판은 30%, 시험판은 0%** 다(사용자 확정 2026-09-10).
        ///
        /// ⚠️ **남은 값이 있으면 그것이 이긴다.** 시험판에서 슬라이더를 올린 사람에게
        /// 다음에 다시 0 을 들이밀지 않는다.
        /// </summary>
        private void Load()
        {
            if (_loaded) return;
            _loaded = true;
            MusicVolume.Set(PlayerPrefs.GetFloat(PrefKey, MusicVolume.StartupDefault));
        }

        private static void Save()
        {
            PlayerPrefs.SetFloat(PrefKey, MusicVolume.Value);
            PlayerPrefs.Save();
        }

        private void OnGUI()
        {
            // ⚠️ **메뉴보다 앞에 그린다**(2026-09-10 · 플랜 §66-35 ①).
            // `GUI.depth` 는 **낮을수록 앞**이다. 이 패널은 메뉴의 「볼륨」이 여는 것이라
            // 메뉴 뒤로 들어가면 **열려도 못 읽고 못 만진다** — 실제로 그랬다.
            //
            // ⚠️ 메뉴가 억제하는 `OnGUI` 일곱에 **이 패널은 들어 있지 않다.** 넣으면
            // 메뉴의 「볼륨」이 아무것도 안 여는 버튼이 된다.
            GUI.depth = MenuDepth - 1;

            KoreanFont.Apply();

            // 버튼은 오른쪽 아래 — 바닥에서 잰다(위쪽은 두 번 다 걸렸다).
            float top = Screen.height - BottomInset;
            var button = new Rect(Screen.width - ButtonW - Margin, top, ButtonW, ButtonH);
            UiBlockers.Add(button);
            if (GUI.Button(button, _open ? "소리 ▲" : "소리 ▼")) _open = !_open;

            if (!_open) return;

            // ⚠️ **슬라이더는 버튼 위로 편다.** 아래로 펴면 화면 밖으로 나간다.
            var panel = new Rect(Screen.width - PanelW - Margin, top - PanelH - 4f, PanelW, PanelH);
            UiBlockers.Add(panel);
            GUI.Box(panel, GUIContent.none);

            var label = new Rect(panel.x + 10f, panel.y + 8f, panel.width - 20f, 22f);
            GUI.Label(label, $"배경 음악  {MusicVolume.Label(MusicVolume.Value)}");

            var slider = new Rect(panel.x + 10f, panel.y + 34f, panel.width - 20f, 20f);
            float next = GUI.HorizontalSlider(slider, MusicVolume.Value, 0f, 1f);

            // ⚠️ **바뀐 프레임에만 남긴다.** 매 프레임 `PlayerPrefs`를 쓰면 디스크를 계속 두드린다.
            if (!Mathf.Approximately(next, MusicVolume.Value))
            {
                MusicVolume.Set(next);
                Save();
            }
        }
    }
}
