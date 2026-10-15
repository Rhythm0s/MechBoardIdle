using MBI.Core;
using MBI.UI;
using UnityEngine;

namespace MBI.Combat
{
    /// <summary>
    /// **T3 — 「도전」 하나만 남기고 덮는 막 + 안내 문구** (2026-10-14 사용자 확정 #130).
    ///
    /// 켜고 끄는 판단은 <see cref="ChallengeTutorialOverlayRule"/> 하나가 한다 — 막과 문구가
    /// 한 몸으로 움직여야 하기 때문이다. 여기는 **그리기만** 한다.
    ///
    /// ⚠️⚠️ **맨 뒤에 그린다 — `GUI.depth` 가 아니라 실행 순서로.** 막이 다른 화면 **위**에
    /// 앉아야 하는데, IMGUI 의 depth 는 이 리포에서 기대대로 안 섰다(09-10 시도 · 09-15 실측으로
    /// 걷었다 · `AudioOptionsPanel` 주석). 같은 문제를 그쪽이 `[DefaultExecutionOrder(1000)]` 로
    /// 풀었고, 이 막은 **그보다도 뒤**라야 하므로 1001 이다(가정 · 값 자체는 「맨 뒤」라는 뜻뿐).
    ///
    /// ⚠️ **눌림은 안 바꾼다.** 막은 그림이고 단추가 아니다 — 무엇이 눌리는가는 여전히
    /// `TutorialGate` 가 정한다(#115 ⓒ 범위 불변). 막은 **눈을 「도전」으로 모으는** 일만 한다.
    ///
    /// ⚠️ **붙이는 곳은 `StageRunner.Start` 다** — 씬 생성기를 다시 돌리지 않으려고.
    /// 씬을 다시 구우면 다른 배선까지 함께 흔들린다(09-17 씬이 묵어 목록이 0/0 이던 사고).
    /// </summary>
    [DefaultExecutionOrder(1001)]
    public sealed class ChallengeTutorialOverlay : MonoBehaviour
    {
        private void OnGUI()
        {
            if (!ChallengeTutorialOverlayRule.ShouldShow(
                    TutorialSignals.HighlightChallengeButton,
                    TutorialSignals.BoardViewOpen,
                    SettingsGate.IsOpen,
                    MainMenuGate.IsOpen))
                return;

            UiSkin.Apply(); // 껍데기 + 한글 폰트 — WebGL엔 시스템 폰트 폴백이 없다

            float sc = UiLayout.Scale(Screen.height);

            // ── 막 — 「도전」 단추 자리만 남긴다 ──
            // 자리는 단추가 쓰는 그 함수에서 받는다(지침 §7) — 따로 재면 구멍과 단추가 어긋난다.
            // 판 테두리(`UiPlate.Pad`)만큼 넓혀 단추 테두리가 반쯤 어둠에 걸리지 않게 한다.
            Rect button = UiLayout.ChallengeButtonRect(Screen.width, Screen.height);
            if (button.width <= 1f || button.height <= 1f) return;   // 단추가 없는 창이면 막도 없다
            Rect hole = new Rect(button.x - UiPlate.Pad, button.y - UiPlate.Pad,
                                 button.width + UiPlate.Pad * 2f, button.height + UiPlate.Pad * 2f);
            TutorialDim.DrawAround(hole);

            // ── 문구 — 단추 **바로 아래**, 오른쪽 끝을 단추에 맞춘다(자리·크기 가정) ──
            //
            // 📌 크기는 배지 안내 줄과 **같은 셈**이다(`StageBadgeLineFontSize` · 사용자 확정
            //    「튜토리얼 안내 줄 꼴」). 판은 다른 패널과 같은 `UiPlate`.
            // ⚠️ **글자는 다 보여야 한다**(#125). 한 줄이 화면 폭을 넘으면 자르지 않고
            //    **줄을 바꾼다** — 높이는 바뀐 줄 수에서 잰다.
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = UiLayout.StageBadgeLineFontSize(Screen.height),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow,
            };
            style.normal.textColor = Color.white;

            string text = ChallengeTutorialOverlayRule.Prompt;
            float pad = UiPlate.Pad + 8f * sc;
            float maxW = Screen.width - 24f;
            float textW = style.CalcSize(new GUIContent(text)).x;
            float w = Mathf.Min(textW + pad * 2f, maxW);

            float textH;
            if (textW + pad * 2f > maxW)
            {
                style.wordWrap = true;
                textH = style.CalcHeight(new GUIContent(text), w - pad * 2f);
            }
            else
            {
                textH = style.lineHeight;
            }
            float h = textH + pad * 2f;

            float x = Mathf.Clamp(button.xMax - w, 12f, Screen.width - 12f - w);
            float y = button.yMax + UiLayout.PlateGap * sc;
            var plate = new Rect(x, y, w, h);

            UiPlate.Draw(plate);
            GUI.Label(new Rect(plate.x + pad, plate.y + pad, plate.width - pad * 2f, textH), text, style);
        }
    }
}
