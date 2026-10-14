namespace MBI.UI
{
    /// <summary>
    /// **줄여서 「…」를 붙여도 되는 것은 숫자뿐이다** (2026-10-14 사용자 확정 ⑥).
    ///
    /// 작은 창에서 글자가 칸을 넘을 때 뒤를 자르고 「…」를 붙이자는 것은 **내 제안이었다.**
    /// 사용자 판정은 **숫자만 해당**이고 **글자는 다 보여야 한다**이다.
    ///
    /// 📌 **까닭이 선다.** 숫자는 줄여도 「무엇인가」가 남는다(1,109 → 1,1…도 수다).
    /// 글자는 줄이면 **뜻이 바뀐다** — 이 리포에서 실제로 그랬다:
    /// 「다음 스테이지로」 → **「음 스테이지」** · 「승리 — 적 전멸」 → **「승리 — 적 전」** ·
    /// 「다시 (Restart)」 → **「다 (Resta」** · 「효과음」 → **「호과음」**.
    /// 마지막 것은 **다른 낱말로 읽혔다** — 자르기는 읽는 사람을 속인다.
    ///
    /// ⚠️⚠️ **그래서 글자가 넘치면 고칠 곳은 글자가 아니라 칸이다.**
    /// 사다리로 줄이고(<see cref="UiText.FitOneLine"/>), 그래도 모자라면 **상자를 키운다**
    /// (2026-10-14 결과 패널이 그 본보기다 · `UiLayout.ResultBannerBoxWidth`).
    /// 사다리 바닥은 16 px = **12 pt** 이고 그 아래로는 안 내려간다.
    /// </summary>
    public static class TextClipPolicy
    {
        /// <summary>
        /// 이 글자를 줄여서 「…」를 붙여도 되는가 — **숫자꼴일 때만** 참이다.
        ///
        /// 숫자꼴 = 숫자와 자릿점·소수점·부호·공백·백분율만으로 된 것. 한 글자라도
        /// 그 밖이면 거짓이다 — 「골드 20」은 글자가 섞였으므로 **통째로 보여야 한다**
        /// (줄이려면 숫자 부분만 따로 떼어 이 함수에 물어야 한다).
        /// </summary>
        public static bool MayEllipsize(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            bool sawDigit = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= '0' && c <= '9') { sawDigit = true; continue; }
                if (c == ',' || c == '.' || c == '+' || c == '-' || c == '%' || c == ' ') continue;
                return false;
            }

            // 숫자가 하나도 없으면 수가 아니다 — 「%」 한 자나 「--」 를 수로 보면 안 된다.
            return sawDigit;
        }
    }
}
