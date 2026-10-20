namespace MBI.Core
{
    /// <summary>
    /// **팔레트 단추 이름 — 두 줄까지 · 낱말 단위**(사용자 확정 #213 · 2026-10-20 · P-13 닫음).
    ///
    /// 한 줄에 드는 이름은 한 줄. 안 들면 **띄어쓰기 자리**에서 끊는다 — 「기초 / 가공소」 · 「생산량 / 모듈」 ·
    /// 「생산속도 / 모듈」 · 「복합 / 가공소」. 띄어쓰기가 여럿이면 두 줄 길이가 가장 비슷한 자리.
    /// 띄어쓰기가 없으면 끊지 않는다(글자 단위로 자르지 않는다 · 종전 줄바꿈에 맡김).
    /// 🗑️ 구 「한 줄 먼저 · 바닥 글자에서도 넘치면 글자 단위 두 줄」(#125) — 「가 / 공소」처럼 낱말이 갈렸다(10-20 육안 33).
    /// </summary>
    public static class PaletteLabelRule
    {
        public static string BreakAtWord(string label)
        {
            if (string.IsNullOrEmpty(label)) return label;
            int best = -1, bestDiff = int.MaxValue;
            for (int i = 0; i < label.Length; i++)
            {
                if (label[i] != ' ') continue;
                int diff = System.Math.Abs(i - (label.Length - i - 1));
                if (diff < bestDiff) { bestDiff = diff; best = i; }
            }
            return best < 0 ? label : label.Substring(0, best) + "\n" + label.Substring(best + 1);
        }

        /// <summary>두 줄 가운데 긴 줄 — 글자 크기를 이 줄에 맞춘다.</summary>
        public static string WidestLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string[] lines = text.Split('\n');
            string w = lines[0];
            for (int i = 1; i < lines.Length; i++) if (lines[i].Length > w.Length) w = lines[i];
            return w;
        }
    }
}
