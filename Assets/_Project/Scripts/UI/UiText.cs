using UnityEngine;

namespace MBI.UI
{
    /// <summary>
    /// **글자가 자리에 드는가를 재는 한 곳** (2026-09-19 사용자 육안 ⑤).
    ///
    /// 📌 **크기는 두 변에서 잡는다.** 높이만 보고 정하면 긴 이름이 폭을 넘어 줄이 바뀌고,
    /// 폭만 보면 두 줄이 판을 넘친다. 이 리포에서 같은 병이 다섯 번 왔다
    /// (탭 줄 · 팔레트 · 원형 버튼 · 노드 이름판 · 스테이지 배지) — 이제 여기서 잰다.
    ///
    /// ⚠️⚠️ **사다리 밖 크기는 안 쓴다.** 유니티 동적 폰트는 **크기마다 글리프를 굽는다** —
    /// 비례로 크기를 내면 창 크기마다 새 크기가 생겨 아틀라스가 차고, 차면 **에러 없이
    /// 글자만 사라진다**(09-14·09-15 에 네 번 온 병 · <see cref="KoreanFont.Ladder"/> 주석).
    /// 그래서 고르는 것은 **사다리 위의 값 하나**다.
    ///
    /// ⚠️ **사다리를 큰 쪽부터 내려온다** — 작은 쪽부터 올라가면 **맨 처음 드는 가장 작은
    /// 크기**를 골라 늘 깨알같이 찍힌다. 원하는 것은 **드는 것 중 가장 큰 크기**다.
    /// </summary>
    public static class UiText
    {
        /// <summary>
        /// 이 글자가 <paramref name="maxWidth"/> 안에 **한 줄로** 드는 가장 큰 사다리 크기를
        /// <paramref name="style"/> 에 물리고 그 크기를 돌려준다.
        ///
        /// ⚠️ <c>wordWrap</c> 을 **끈다** — 켜 둔 채로 재면 <see cref="GUIStyle.CalcSize"/> 가
        /// 줄이 바뀐 뒤의 폭을 돌려줘 「들었다」고 거짓말한다.
        ///
        /// 사다리 맨 아래도 안 들면 **맨 아래를 쓴다** — 자르는 것보다 삐져나오는 편이
        /// 「무엇인지」를 남긴다(넘침 여부는 부르는 쪽이 판을 넓혀 고칠 일이다).
        /// </summary>
        public static int FitOneLine(GUIStyle style, string text, float maxWidth)
        {
            if (style == null || string.IsNullOrEmpty(text) || maxWidth <= 0f)
                return style != null ? style.fontSize : 0;

            style.wordWrap = false;

            int[] ladder = KoreanFont.Ladder;
            for (int i = ladder.Length - 1; i >= 0; i--)
            {
                style.fontSize = ladder[i];
                if (style.CalcSize(new GUIContent(text)).x <= maxWidth) return ladder[i];
            }

            style.fontSize = ladder[0];
            return ladder[0];
        }
    }
}
