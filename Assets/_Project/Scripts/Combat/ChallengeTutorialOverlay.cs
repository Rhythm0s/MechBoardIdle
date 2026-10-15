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
    /// ✅ **`StageRunner.OnGUI` 의 끝에서 부른다**(2026-10-15 사용자 확정 #133 · QA CID_032).
    /// 막이 덮어야 할 HUD(칩·Stage 띠·배지)·원형 셋(로봇 B·Auto·합체)·「도전」이 **전부 그
    /// OnGUI 에서** 그려진다. 같은 OnGUI 의 끝이면 그것들 위에 앉는 것이 **보장된다.**
    ///
    /// 🗑️ **구 길 「별도 컴포넌트 + `[DefaultExecutionOrder(1001)]` 로 맨 뒤」 폐기.**
    ///    실행 순서는 **OnGUI 차례를 세우지 못했다** — 이 막이 `StageRunner` 보다 **먼저**
    ///    그려져 HUD·원형 셋이 막 **위**에 밝게 남았다(10-15 8126 실플레이 · 576×1024).
    ///    내가 10-14 에 8123 에서 「화면 전체 어둠」이라 보고한 것도 틀렸다 — 그 스크린샷에도
    ///    HUD 는 밝았다. `AudioOptionsPanel` 의 1000 이 먹은 듯 보인 것은 그쪽이 **같은 오브젝트의
    ///    메뉴**와만 다퉜기 때문이지, 다른 컴포넌트와의 차례를 세운 것이 아니었다.
    ///
    /// ⚠️ **눌림은 안 바꾼다.** 막은 그림이고 단추가 아니다 — 무엇이 눌리는가는 여전히
    /// `TutorialGate` 가 정한다(#115 ⓒ 범위 불변). 막은 **눈을 「도전」으로 모으는** 일만 한다.
    ///
    /// 🗑️ 구 「붙이는 곳은 `StageRunner.Start`」 폐기 — 컴포넌트가 아니라 정적 함수가 됐다.
    /// </summary>
    public static class ChallengeTutorialOverlay
    {
        /// <summary>
        /// 막과 문구를 그린다. **`StageRunner.OnGUI` 맨 끝(합체 연출 바로 앞)에서만** 부른다 —
        /// 다른 자리에서 부르면 그 뒤에 그려지는 것이 막 위로 올라온다.
        /// </summary>
        public static void Draw()
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
