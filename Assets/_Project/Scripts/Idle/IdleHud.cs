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

        private void EnsureStyles()
        {
            if (_head != null) return;
            _head   = new GUIStyle(GUI.skin.label)  { fontSize = 26, fontStyle = FontStyle.Bold };
            _body   = new GUIStyle(GUI.skin.label)  { fontSize = 16, wordWrap = true };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 16 };
        }

        private void OnGUI()
        {
            if (_dismissed || _idle == null) return;

            OfflineRewardResult r = _idle.LastOfflineReward;
            if (r.creditedHours < minHoursToShow) return; // 방금 껐다 켠 것 — 알릴 것이 없다

            KoreanFont.Apply(); // WebGL엔 시스템 폰트 폴백이 없다
            EnsureStyles();

            // 높이 300 — 232로는 「확인」 버튼이 영역 밖으로 잘려 **창을 닫을 수가 없었다**
            // (2026-09-06 웹빌드 실측). GUILayout은 BeginArea 밖을 그리지 않으므로
            // 버튼이 사라진 것이 아니라 잘린 것이었고, 화면에서 보기 전에는 드러나지 않았다.
            const float w = 420f, h = 300f;
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
            GUILayout.BeginArea(new Rect(box.x + 18f, box.y + 14f, box.width - 36f, box.height - 28f));

            GUILayout.Label("돌아왔다", _head);
            GUILayout.Space(6f);
            GUILayout.Label($"꺼 둔 시간 {Hours(r.creditedHours)}" + (r.capped ? "  (상한까지만 인정)" : ""), _body);
            GUILayout.Label($"파밍 시급 {r.hourlyRate:N1} 고철/시간" + (r.usedDefaultRate ? "  (기록 없음 → 기본값)" : ""), _body);
            GUILayout.Space(4f);
            GUILayout.Label($"받은 고철 {r.scrap:N0}", _head);

            if (r.scrap <= 0d)
            {
                GUILayout.Space(2f);
                // 왜 0인지를 적는다. 안 적으면 미구현으로 읽힌다.
                GUILayout.Label(r.usedDefaultRate
                    ? "상주 스테이지 파밍 기록이 없고 기본 시급이 아직 미확정(TBD)이라 0이다."
                    : "오프라인 계수가 아직 미확정(TBD)이라 0이다.", _body);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("확인", _button, GUILayout.Height(34f))) _dismissed = true;

            GUILayout.EndArea();
        }

        /// <summary>한 시간이 안 되면 분으로 적는다 — 「0.3시간」은 읽고 다시 곱해야 한다.</summary>
        private static string Hours(double hours) =>
            hours < 1d ? $"{hours * 60d:N0}분" : $"{hours:N1}시간";
    }
}
