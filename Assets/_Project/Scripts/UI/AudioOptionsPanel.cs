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
    /// <remarks>
    /// ⚠️ **실행 순서를 못 박는다**(2026-09-15 · 「소리 버튼이 안 보인다」 결함).
    ///
    /// **무엇이 문제였나.** 이 패널과 <see cref="MainMenuOverlay"/> 는 **같은 게임오브젝트**에
    /// 붙어 있고 둘 다 실행 순서를 안 정했다. 그러면 **컴포넌트 등록 순서**가 그리는 차례를
    /// 정하는데, 이 패널이 **먼저** 그려지고 메뉴의 **전체 화면 배경이 그 위를 덮었다** —
    /// 진단 로그는 「OnGUI 돈다 · 자리 2412,48 · 420×300」으로 멀쩡했고, 그 자리에 **자홍
    /// 사각을 칠해도 안 보였다.** 그리기가 아니라 **덮임**이었다.
    ///
    /// ⚠️ **`GUI.depth` 로는 못 고쳤다**(09-10 에 `MenuDepth − 1` 로 시도 · 09-15 실측으로
    /// 걷었다). IMGUI 의 depth 는 같은 프레임 안에서 기대대로 안 섰다.
    ///
    /// **1000 은 「맨 뒤」라는 뜻이다** — 소리 버튼은 조립·전투·메뉴 **세 화면에서 같은
    /// 자리에 늘 떠 있어야** 하므로, 무엇이 그려지든 그 뒤에 와야 한다.
    /// 값 자체는 가정이고, 다른 UI 가 이보다 뒤로 가야 할 일이 생기면 그때 다시 잰다.
    /// </remarks>
    [DefaultExecutionOrder(1000)]
    public sealed class AudioOptionsPanel : MonoBehaviour
    {
        /// <summary>진단 로그를 한 번만 찍었는가 (2026-09-15 · 걷을 자리).</summary>
        private static bool _loggedOnce;

        /// <summary>자리 로그를 한 번만 찍었는가 (2026-09-15 · 걷을 자리).</summary>
        private static bool _loggedRect;

        /// <summary>기기에 남기는 자리. 이름이 바뀌면 사람이 고른 값이 사라진다.</summary>
        private const string PrefKey = "mbi.music.volume";

        private const float ButtonW = 96f;
        private const float ButtonH = 32f;
        /// <summary>
        /// 🗑️ **폐기 — 자리를 `UiLayout.SoundPanelRect` 가 낸다**(2026-10-13).
        /// 가로는 이제 `UiLayout.SoundPanelWidth`(576 = 240 × 2.4) 하나다.
        /// 지우지 않고 남긴다 — 576 이 어디서 온 수인지 읽는 사람이 알 수 있게.
        /// </summary>
        private const float PanelW = 240f;
        /// <summary>
        /// 슬라이더 **셋** 이 들어간다 (2026-09-11 · 사운드 문서 7장 채널 셋).
        /// 한 줄 = 이름표 26 + 슬라이더 20 + 사이 6 = 52. 위아래 여백 16.
        /// </summary>
        /// <summary>
        /// 🗑️ **폐기 — 날 픽셀 줄을 전제한 높이**(2026-09-18). 줄을 배율에 태우면서
        /// 높이도 **줄에서 낸다**(그리는 자리 참조). 값을 남기는 것은 **왜 바뀌었는지**를
        /// 읽는 사람이 알 수 있게 하려는 것이고, **읽는 곳은 없다.**
        /// </summary>
        private const float PanelH = 16f + 52f * 3f;
        private const float Margin = 12f;

        /// <summary>메인 메뉴가 쓰는 깊이. 이 패널은 그보다 **하나 앞**에 선다.</summary>
        public const int MenuDepth = 0;

        /// <summary>
        /// 🗑️🗑️ **폐기 — 읽는 곳이 없다**(2026-09-19).
        ///
        /// **왜 죽었나.** 이 값은 「소리」 버튼을 **화면 바닥에서** 재던 것인데,
        /// 09-18 에 버튼이 **상단 칩 줄**(<c>UiLayout.ChipSoundRect</c>)로 옮겨 가면서
        /// 재는 쪽이 사라졌다. 상수만 남았다.
        ///
        /// ⚠️⚠️ **셈의 근거도 이미 낡았다.** 「심사자용 패널이 <c>Screen.height - 190</c>
        /// 에서 시작한다」는 전제로 190 을 박아 두었는데, 09-19 에 그 패널이
        /// **높이를 먼저 내고 윗변을 거기서 빼는** 방식으로 바뀌었다(윗변은 이제
        /// 바닥에서 286 쯤이고, 줄이 늘면 또 달라진다). **읽는 곳이 있었다면 이 상수는
        /// 조용히 틀린 자리를 가리켰을 것이다** — 값이 두 곳에 살면 답이 둘이 된다(지침 §7).
        ///
        /// 되살릴 일이 생기면 **190 을 옮겨 적지 말고** `ReviewerShortcuts` 가 낸
        /// 판 높이를 물어서 쓴다.
        ///
        /// ── 종전 근거(자리 자체의 까닭은 아직 유효하다) ──
        ///
        /// ⚠️ **가로도 왼쪽으로 옮겼다**(2026-09-14 · 2차 스크린샷). 오른쪽 아래는
        /// 문서가 **태그·합체 원형 둘**(레이어 1 · x1280)에 준 자리인데, 이 버튼과
        /// 심사자용 패널이 그 위에 나중에 그려져 **태그 원형을 통째로 덮고 있었다.**
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
        /// 기기에 남은 값을 읽는다 — **채널 셋 각각**(2026-09-11 · 플랜 §71-16 ③).
        /// 없으면 기본값: 배경은 <see cref="MusicVolume.StartupDefault"/>(배포 30% · 시험 0%),
        /// 효과음·조작음은 **가정 100%**.
        ///
        /// ⚠️ **남은 값이 있으면 그것이 이긴다.** 시험판에서 슬라이더를 올린 사람에게
        /// 다음에 다시 0 을 들이밀지 않는다.
        ///
        /// ⚠️ **구 키 하나를 이어받는다** — 종전에는 배경만 `PrefKey` 에 저장했다.
        /// 새 키가 없고 구 키가 있으면 그것을 배경 값으로 읽는다. 안 이어받으면
        /// 이미 볼륨을 맞춰 둔 기기가 **다음 실행에서 0 으로 되돌아간다.**
        /// </summary>
        private void Load()
        {
            if (_loaded) return;
            _loaded = true;

            string musicKey = AudioChannels.KeyOf(AudioChannels.Channel.Music);
            if (!PlayerPrefs.HasKey(musicKey) && PlayerPrefs.HasKey(PrefKey))
                PlayerPrefs.SetFloat(musicKey, PlayerPrefs.GetFloat(PrefKey));

            AudioChannels.Load();
        }

        /// <summary>
        /// ⚠️⚠️ **제 버튼과 제 판을 폐기한다**(2026-10-14 사용자 확정 ⑤
        /// 「열림, 소리 버튼 제거하구 설정 패널 안에 포함시키도록 하자」).
        ///
        /// 종전에는 칩 줄에 「소리」/「닫기」 단추를 그리고, 누르면 **떠 있는 판**을 폈다.
        /// 그 판이 진행 띠·스테이지 단추 위에 겹쳐 앉는 것이 사용자 스크린샷에 잡혔다
        /// (슬라이더 셋이 「스테이지」·「도전」 단추를 가로질렀다).
        ///
        /// 📌 이제 **설정 패널이 그 자리를 가진다** — 그리는 것은
        /// <see cref="DrawChannels"/> 하나이고 부르는 쪽은 `ReviewerShortcuts` 다.
        /// 값·저장·미리듣기는 그대로 여기 있다(채널 상태의 임자는 바뀌지 않았다).
        ///
        /// ⚠️ 남은 것 — <c>UiLayout.SoundPanelRect</c>·<c>ChipSoundRect</c> 는 지우지 않았다
        /// (폐기 표기). 되살리려면 이 `OnGUI` 가 다시 그리면 된다.
        /// </summary>
        private void OnGUI()
        {
            // 아무것도 안 그린다 — 소리 줄은 설정 패널이 그린다.
        }

        /// <summary>
        /// **줄 여백을 줄였다**(#159 · 2026-10-16 사용자 육안 「너무 크다」 · 설정 판 높이를 줄인다).
        /// 종전 한 줄 = 이름표 30 + 슬라이더 26 + 틈 14 = 70 → 이름표 22 + 슬라이더 14 + 틈 3 = 39(약 절반 · ⚠️ 가정).
        /// ⚠️ **글자 크기는 그대로다** — 글자는 종전 이름표 높이 30 에서 낸다(<see cref="FontBasis"/>).
        /// </summary>
        public const float RowLabel = 22f, RowSlider = 14f, RowGap = 3f;

        /// <summary>글자 크기를 내는 높이 — 종전 이름표 높이 30(#159 에서도 안 바꾼다).</summary>
        public const float FontBasis = 30f;

        /// <summary>채널 이름표 글자 크기 — 종전 식 그대로.</summary>
        public static int LabelFontSize(float scale) =>
            KoreanFont.Snap(Mathf.Max(11, Mathf.RoundToInt(FontBasis * scale * 0.62f)));

        /// <summary>채널 한 줄이 쓰는 높이(이름표 + 슬라이더 + 틈).</summary>
        public static float ChannelRowHeight(float scale) =>
            (RowLabel + RowSlider + RowGap) * scale;

        /// <summary>채널 셋이 쓰는 높이 — 부르는 쪽이 판 높이에 더한다.</summary>
        public static float ChannelsHeight(float scale) => ChannelRowHeight(scale) * 3f;

        /// <summary>
        /// **채널 셋을 그린다** — 배경 음악 · 효과음 · 조작음.
        ///
        /// 채널 셋 — 사운드 문서 7장. **하나로 묶지 않는다**: 셋은 사람이 서로 다른
        /// 이유로 줄이는 것이라, 묶으면 하나가 거슬려도 나머지 둘까지 함께 잃는다.
        ///
        /// ⚠️⚠️ **날 픽셀 줄을 안 쓴다**(2026-09-18 사용자 육안 · 「효과음」이 「호과음」으로).
        /// 이름표·슬라이더·줄 간격을 **전부 배율에 태운다** — 큰 창에서 글자가 줄보다 커져
        /// 위가 잘리면, 잘린 자리가 받침처럼 보여 다른 글자로 읽힌다.
        /// </summary>
        /// <returns>쓴 높이.</returns>
        public static float DrawChannels(float x, float y, float width, float scale)
        {
            float rowLabel = RowLabel * scale;
            float rowSlider = RowSlider * scale;
            float rowGap = RowGap * scale;

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = LabelFontSize(scale),   // #159 — 여백만 줄이고 글자는 그대로
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Overflow,   // 재기 전에 잘리는 함정을 피한다
            };

            float top = y;
            foreach (AudioChannels.Channel ch in new[]
            {
                AudioChannels.Channel.Music,
                AudioChannels.Channel.Effect,
                AudioChannels.Channel.Ui,
            })
            {
                float now = AudioChannels.Value(ch);
                GUI.Label(new Rect(x, y, width, rowLabel),
                    $"{AudioChannels.Label(ch)}  {MusicVolume.Label(now)}", labelStyle);

                float next = GUI.HorizontalSlider(
                    new Rect(x, y + rowLabel, width, rowSlider), now, 0f, 1f);

                // ⚠️ **바뀐 프레임에만 남긴다.** 매 프레임 `PlayerPrefs` 를 쓰면 디스크를 계속 두드린다.
                if (!Mathf.Approximately(next, now))
                {
                    AudioChannels.Set(ch, next);
                    AudioChannels.Save(ch);

                    // ⚠️ **이 순간이 「사람이 누른 순간」이다** — 웹 오디오의 잠금이 풀리는
                    // 자리라 재생기가 여기서 곡을 새로 걸 수 있다(2026-09-10 · 결함 ③).
                    // **어느 슬라이더를 만졌든 잠금은 풀린다** — 제스처는 채널을 안 가린다.
                    MusicVolume.PreviewRequested = true;
                }

                y += rowLabel + rowSlider + rowGap;
            }

            return y - top;
        }
    }
}