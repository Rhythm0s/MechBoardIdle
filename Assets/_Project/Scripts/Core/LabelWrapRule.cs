using System;

namespace MBI.Core
{
    /// <summary>
    /// **글자를 어디서 끊을 것인가** (2026-09-19 사용자 육안 ⑤ — 「애매하게 두 줄이 넘어간다」).
    ///
    /// 📌 **한글은 낱자 단위로 줄이 바뀐다.** 유니티 IMGUI 의 <c>wordWrap</c> 은 CJK 를
    /// **아무 데서나** 끊으므로 「기초 가공소」가 「기초 가공 / 소」가 되고 「태그 — 교대」가
    /// 「태그 — 교 / 대」가 된다. 뜻이 없는 자리에서 끊긴 줄은 **읽는 사람이 한 번 멈춘다.**
    ///
    /// 그래서 **끊을 자리를 우리가 정한다** — 띄어쓰기가 있으면 그 자리에서 끊고,
    /// 없으면 손대지 않는다(억지로 끊으면 그것이 또 아무 데나 끊는 것이다).
    ///
    /// ⚠️ **줄 수를 늘리지 않는다.** 이 규칙은 「두 줄로 만든다」가 아니라 「두 줄이 될
    /// 바에는 **뜻이 있는 자리**에서 끊는다」이다. 한 줄에 드는지는 그리는 쪽이 재고,
    /// 여기는 **끊을 자리만** 안다.
    ///
    /// ⚠️ **글자를 지우거나 바꾸지 않는다** — 띄어쓰기 하나가 줄바꿈으로 바뀔 뿐이다.
    /// </summary>
    public static class LabelWrapRule
    {
        /// <summary>
        /// 두 쪽의 길이가 **가장 고른** 띄어쓰기에서 끊는다.
        ///
        /// 가운데를 고르는 까닭 — 한쪽만 길면 그 줄이 다시 넘쳐 결국 세 줄이 된다.
        /// 「기초 가공소」는 띄어쓰기가 하나뿐이라 「기초 / 가공소」가 되고,
        /// 「탭 = 교대 · 길게 = 자동」처럼 여럿이면 가운데에 가장 가까운 것을 고른다.
        ///
        /// 띄어쓰기가 없으면 **받은 그대로** 돌려준다 — 끊을 자리가 없다는 뜻이다.
        /// 이미 줄바꿈이 들어 있으면 손대지 않는다(부르는 쪽이 이미 정한 자리다).
        /// </summary>
        public static string AtSpace(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (text.IndexOf('\n') >= 0) return text;

            int best = -1;
            int bestGap = int.MaxValue;
            int mid = text.Length / 2;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != ' ') continue;
                // 맨 앞·맨 뒤 띄어쓰기는 끊어 봐야 빈 줄이 하나 생길 뿐이다.
                if (i == 0 || i == text.Length - 1) continue;

                int gap = Math.Abs(i - mid);
                if (gap >= bestGap) continue;
                bestGap = gap;
                best = i;
            }

            if (best < 0) return text;
            return text.Substring(0, best) + "\n" + text.Substring(best + 1);
        }

        /// <summary>
        /// **정한 토막에서 끊는다** — 가운데가 아니라 **뜻이 갈리는 자리**를 아는 경우다.
        ///
        /// 「탭 = 교대 · 길게 = 자동 켬/끔」의 <c>·</c> 처럼 **문구 안에 이미 경계가 있는**
        /// 자리에 쓴다. 이렇게 하면 **문구는 한 곳(상수)에 그대로 두고** 줄만 나눌 수 있다 —
        /// 두 줄짜리 문구를 따로 두면 **같은 말이 두 곳에 살게 된다**(지침 §7).
        ///
        /// 그 토막이 없으면 <see cref="AtSpace"/> 로 떨어진다.
        /// </summary>
        public static string At(string text, string separator)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(separator)) return text;
            if (text.IndexOf('\n') >= 0) return text;

            int i = text.IndexOf(separator, StringComparison.Ordinal);
            if (i < 0) return AtSpace(text);
            return text.Substring(0, i).TrimEnd() + "\n" + text.Substring(i + separator.Length).TrimStart();
        }
    }
}
