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
    /// 규정이 서면 그쪽을 따른다. 지금은 **화면 오른쪽 위 · 다른 패널과 안 겹치는 자리**다.
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

        private bool _open;
        private bool _loaded;

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
            KoreanFont.Apply();

            // 버튼은 오른쪽 위 — 변수 패널(하단)과 노드 팔레트(오른쪽 300 아래)를 피한다.
            var button = new Rect(Screen.width - ButtonW - Margin, Margin, ButtonW, ButtonH);
            UiBlockers.Add(button);
            if (GUI.Button(button, _open ? "소리 ▲" : "소리 ▼")) _open = !_open;

            if (!_open) return;

            var panel = new Rect(Screen.width - PanelW - Margin, Margin + ButtonH + 4f, PanelW, PanelH);
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
