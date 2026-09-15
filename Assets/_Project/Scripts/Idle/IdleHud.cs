using MBI.Core;
using MBI.UI;
using UnityEngine;

namespace MBI.Idle
{
    /// <summary>
    /// 오프라인 정산 알림(§5-7).
    ///
    /// **방치 사슬이 실제로 돌았다는 유일한 증빙이다.** 처치 → 지갑 → 저장 → 꺼둔 시간 정산까지
    /// 코드는 다 있었지만 화면에 아무것도 안 떠서, 돌고 있는지 확인할 방법이 없었다.
    /// 잔액은 전투 상태 칸에 한 줄로 나가고(<see cref="IdleSignals.WalletScrap"/>) 여기서는
    /// **접속할 때 한 번** 「얼마를 왜 받았는가」만 말한다.
    ///
    /// ⚠️ **미확정 수치를 감추지 않는다**(§0 역할 경계). 오프라인 계수와 기본 시급은 아직 TBD라
    /// 지급이 0으로 나올 수 있는데, 그때 창을 안 띄우면 「방치가 고장났다」로 읽힌다.
    /// 0이면 0이라고 적고 **왜 0인지**를 같이 적는다.
    /// </summary>
    [RequireComponent(typeof(IdleRuntime))]
    public sealed class IdleHud : MonoBehaviour
    {
        [Tooltip("정산 창을 띄울 최소 경과 시간(시간). 이보다 짧게 껐다 켜면 조용히 넘어간다.")]
        [SerializeField] private double minHoursToShow = 0.01d; // 36초

        private IdleRuntime _idle;
        private bool _dismissed;
        private GUIStyle _head, _body, _button;

        private void Awake() => _idle = GetComponent<IdleRuntime>();

        /// <summary>마지막으로 스타일을 지은 창 높이. 창이 바뀌면 다시 짓는다.</summary>
        private float _styleHeight;

        private void EnsureStyles()
        {
            // ⚠️ **창 높이가 바뀌면 다시 짓는다.** 한 번 짓고 말면 글자가 창을 안 따라간다.
            if (_head != null && Mathf.Abs(_styleHeight - Screen.height) < 0.5f) return;
            _styleHeight = Screen.height;
            // ⚠️ **글자도 창을 따라간다**(2026-09-15 · 육안 ⓒ). 날 픽셀 26/16 은
            // 720×1280 창에서 점만 했다 — 09-14 하단 넷과 같은 병이다.
            float sc = MBI.UI.UiLayout.Scale(Screen.height);
            int head = MBI.UI.KoreanFont.Snap(Mathf.Max(10, Mathf.RoundToInt(52f * sc)));
            int body = MBI.UI.KoreanFont.Snap(Mathf.Max(9, Mathf.RoundToInt(32f * sc)));

            _head   = new GUIStyle(GUI.skin.label)  { fontSize = head, fontStyle = FontStyle.Bold };
            _body   = new GUIStyle(GUI.skin.label)  { fontSize = body, wordWrap = true };
            _button = new GUIStyle(GUI.skin.button) { fontSize = body };
        }

        private void OnGUI()
        {
            // 메인 메뉴가 덮고 있으면 그리지 않는다 — IMGUI 는 뒤에 그리는 쪽이 위로 온다
            // (2026-09-10 · 실측: 오프라인 대화상자가 「게임 시작」 버튼을 덮었다).
            if (MainMenuGate.IsOpen) return;
            if (_dismissed || _idle == null) return;

            OfflineRewardResult r = _idle.LastOfflineReward;
            if (r.creditedHours < minHoursToShow) return; // 방금 껐다 켠 것 — 알릴 것이 없다

            UiSkin.Apply(); // 껍데기 + 한글 폰트 — WebGL엔 시스템 폰트 폴백이 없다
            EnsureStyles();

            // 높이 300 — 232로는 「확인」 버튼이 영역 밖으로 잘려 **창을 닫을 수가 없었다**
            // (2026-09-06 웹빌드 실측). GUILayout은 BeginArea 밖을 그리지 않으므로
            // 버튼이 사라진 것이 아니라 잘린 것이었고, 화면에서 보기 전에는 드러나지 않았다.
            // ⚠️ **날 픽셀 420×300 을 걷었다**(2026-09-15 · 육안 ⓒ · 하단 넷과 같은 병).
            //
            // 창 크기와 무관한 고정 크기라 720×1280 창에서 **글자가 안 읽혔다.**
            // 기준 캔버스(1440×2560) 값으로 적고 배율을 곱한다 — 구 420×300 은 폐기 표기.
            // ⚠️ **크기는 가정이다**(설계 역기입) — 문서에 방치 보상 창 절이 없다.
            float boxScale = MBI.UI.UiLayout.Scale(Screen.height);
            // ⚠️ **높이를 내용에서 잰다**(2026-09-15 · 육안 ① 결함).
            //
            // 760×520 **고정**이었더니 본문 마지막 줄이 잘리고 **「확인」 버튼이 상자 밖으로
            // 밀려 아예 안 보였다** — **창을 닫을 수가 없었다.** 09-06 에 같은 일로
            // 232→300 을 올린 적이 있다(아래 구 주석): **고정 높이는 글이 늘면 또 터진다.**
            //
            // 이제 줄을 먼저 짓고 그 높이로 상자를 잡는다. 글이 늘어도 버튼이 안 밀린다.
            float w = Mathf.Min(760f * boxScale, Screen.width - 48f * boxScale);
            float inset = 36f * boxScale;
            float btnH = 72f * boxScale;

            string[] lines = BodyLines(r);
            float bodyH = _head.CalcHeight(new GUIContent("돌아왔다"), w - inset * 2f)
                          + _head.CalcHeight(new GUIContent($"받은 고철 {r.scrap:N0}"), w - inset * 2f);
            foreach (string line in lines)
                bodyH += _body.CalcHeight(new GUIContent(line), w - inset * 2f);

            float h = Mathf.Min(bodyH + inset * 2.4f + btnH + 24f * boxScale,
                Screen.height - 48f * boxScale);
            var box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            // 창 뒤로 클릭이 새면 안 된다 — 창을 닫으려다 그 아래 칸에 노드가 놓인다.
            UiBlockers.Add(box);

            // ⚠️ **불투명 바탕을 먼저 깐다.** 기본 스킨 상자는 반투명이라 뒤의 로봇이
            // 글자를 뚫고 보였다 — 「받은 고철」 숫자가 로봇 몸통에 겹쳐 안 읽혔다.
            Color prevBg = GUI.color;
            GUI.color = new Color(0.06f, 0.07f, 0.09f, 0.97f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = prevBg;

            GUI.Box(box, GUIContent.none);

            // ⚠️ **버튼은 GUILayout 밖에서 상자 하단에 앉힌다**(2026-09-15 · 육안 ①).
            // 흐름 배치에 맡기면 글이 길어질 때 **버튼부터 밖으로 밀린다** — 그러면 창을
            // 닫을 수가 없다. 자리를 먼저 떼어 두고 나머지를 글이 쓴다.
            var bodyArea = new Rect(box.x + inset, box.y + inset * 0.8f,
                box.width - inset * 2f, box.height - inset * 1.6f - btnH);
            GUILayout.BeginArea(bodyArea);

            GUILayout.Label("돌아왔다", _head);
            foreach (string line in lines) GUILayout.Label(line, _body);
            GUILayout.Label($"받은 고철 {r.scrap:N0}", _head);

            GUILayout.EndArea();

            var btn = new Rect(box.x + inset, box.yMax - inset * 0.6f - btnH,
                box.width - inset * 2f, btnH);
            if (UiSkin.Button(btn, "확인", _button)) _dismissed = true;

            // ⚠️ **바깥을 눌러도 닫힌다**(2026-09-15 사용자 확정 · 육안 ①).
            // 닫는 길이 하나뿐이면 그 하나가 안 보이는 순간 **갇힌다** — 오늘이 그랬다.
            if (Event.current.type == EventType.MouseDown && !box.Contains(Event.current.mousePosition))
            {
                _dismissed = true;
                Event.current.Use();
            }
        }

        /// <summary>
        /// 본문 줄 — **재는 곳과 그리는 곳이 같은 목록을 쓴다**(2026-09-15).
        /// 둘이 갈리면 높이 셈이 화면과 어긋나 또 잘린다.
        /// </summary>
        private static string[] BodyLines(OfflineRewardResult r)
        {
            string time = $"꺼 둔 시간 {Hours(r.creditedHours)}" + (r.capped ? "  (상한까지만 인정)" : "");
            string rate = $"파밍 시급 {r.hourlyRate:N1} 고철/시간"
                          + (r.usedDefaultRate ? "  (기록 없음 → 기본값)" : "");

            if (r.scrap > 0d) return new[] { time, rate };

            // 왜 0인지를 적는다. 안 적으면 미구현으로 읽힌다.
            string why = r.usedDefaultRate
                ? "상주 스테이지 파밍 기록이 없고 기본 시급이 아직 미확정(TBD)이라 0이다."
                : "오프라인 계수가 아직 미확정(TBD)이라 0이다.";
            return new[] { time, rate, why };
        }

        /// <summary>한 시간이 안 되면 분으로 적는다 — 「0.3시간」은 읽고 다시 곱해야 한다.</summary>
        private static string Hours(double hours) =>
            hours < 1d ? $"{hours * 60d:N0}분" : $"{hours:N1}시간";
    }
}
