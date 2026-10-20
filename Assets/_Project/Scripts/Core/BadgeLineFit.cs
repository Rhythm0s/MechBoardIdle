using System;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// **배지 목표 줄 — 글자 사각 ⊂ 카드 줄 칸**(사용자 확정 #224 · 2026-10-20 · 44 「파밍: 로봇 B 해금 · 합체 · 모아서 보드를 키운다」가 카드 밖).
    ///
    /// 차례(⚠️ 구현 판단): ① 한 줄 · 시작 크기 → 사다리 바닥 16 ② 두 줄(「  ·  」 자리 중 가운데에 가장 가까운 곳에서 끊음) · 시작 → 16
    /// ③ 두 줄 · 15 → 바닥 8(잘림 없이 작게 · #125 를 「전부 보임」으로 충족). 끝까지 안 들면 바닥 + 두 줄.
    /// </summary>
    public static class BadgeLineFit
    {
        public const string Separator = "  ·  ";
        public const int LadderFloor = 16;
        public const int Floor = 8;

        public struct Result
        {
            public string text;
            public int fontSize;
            public bool fits;
        }

        /// <summary>「  ·  」 가운데 가장 가운데 자리에서 두 줄로. 없으면 그대로.</summary>
        public static string BreakAtSeparator(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            int best = -1, bestDiff = int.MaxValue, i = 0;
            while ((i = text.IndexOf(Separator, i, StringComparison.Ordinal)) >= 0)
            {
                int diff = Math.Abs(i - (text.Length - i - Separator.Length));
                if (diff < bestDiff) { bestDiff = diff; best = i; }
                i += Separator.Length;
            }
            return best < 0 ? text : text.Substring(0, best) + "\n" + text.Substring(best + Separator.Length);
        }

        /// <param name="measure">(글, 크기) → 그린 사각(줄바꿈 포함).</param>
        public static Result Fit(string text, float width, float height, int startSize, Func<string, int, Vector2> measure)
        {
            if (string.IsNullOrEmpty(text)) return new Result { text = text, fontSize = startSize, fits = true };
            string two = BreakAtSeparator(text);
            int top = Mathf.Max(startSize, Floor);
            foreach ((string t, int hi, int lo) in new[] { (text, top, LadderFloor), (two, top, LadderFloor), (two, LadderFloor - 1, Floor) })
                for (int s = hi; s >= lo; s--)
                {
                    Vector2 m = measure(t, s);
                    if (m.x <= width + 0.5f && m.y <= height + 0.5f) return new Result { text = t, fontSize = s, fits = true };
                }
            return new Result { text = two, fontSize = Floor, fits = false };
        }
    }
}
