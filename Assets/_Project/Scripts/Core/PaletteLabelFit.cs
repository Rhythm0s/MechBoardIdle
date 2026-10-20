using System;
using System.Collections.Generic;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// **팔레트 라벨 맞춤 — 글자 사각 ⊂ 단추 사각**(사용자 확정 #219 · #220 · 2026-10-20).
    ///
    /// 보이는 이름 **전부**가 라벨 띠 안에 드는 가장 큰 글자 하나를 고른다(단추 셋 같은 크기).
    /// ① 띠 = 단추 하단 · 높이 = 단추 반에서 시작 ② 글자는 상한(24 ⚠️)부터 1px 씩 내림 —
    /// **팔레트 라벨에 한해 사다리 바닥 16 아래도 허용**(⚠️ #125 「글자 전부 보임」을 「잘림 없이 작게」로 충족 · 팔레트 한정 예외)
    /// ③ 그래도 두 줄이 안 들면 띠를 2/3 까지 늘린다(⚠️ · 그림은 띠 위 남는 칸으로 줄어든다) ④ 끝까지 안 들면 바닥 글자 + 클립.
    /// 이름 줄바꿈은 `PaletteLabelRule`(한 줄에 들면 한 줄 · 아니면 낱말 자리 두 줄).
    /// </summary>
    public static class PaletteLabelFit
    {
        public const int FontCap = 24;
        public const int FontFloor = 6;
        public const float BandStartRatio = 0.5f;
        public const float BandMaxRatio = 2f / 3f;

        public struct Result
        {
            public int fontSize;
            public float bandHeight;
            public bool fits;
        }

        /// <summary>띠 가장자리 여백(단추 변의 3% · 바닥 1px).</summary>
        public static float Inset(float side) => Mathf.Max(1f, side * 0.03f);

        /// <summary>띠 안쪽 폭.</summary>
        public static float BandWidth(float side) => Mathf.Max(0f, side - 2f * Inset(side) - 2f);

        /// <summary>이 글자 크기에서 보일 글 — 한 줄에 들면 한 줄, 아니면 낱말 자리 두 줄.</summary>
        public static string TextAt(string name, int size, float bandWidth, Func<string, int, Vector2> measure) =>
            measure(name, size).x <= bandWidth + 0.5f ? name : PaletteLabelRule.BreakAtWord(name);

        /// <param name="measure">(글, 크기) → 그린 사각 크기(줄바꿈 포함). 화면은 GUIStyle.CalcSize · 시험은 가짜.</param>
        public static Result Compute(IList<string> names, float side, Func<string, int, Vector2> measure)
        {
            float w = BandWidth(side);
            float inset = Inset(side);
            foreach (float ratio in new[] { BandStartRatio, BandMaxRatio })
            {
                float hMax = side * ratio - inset;
                for (int size = FontCap; size >= FontFloor; size--)
                {
                    float needH = 0f;
                    bool ok = true;
                    if (names != null)
                        foreach (string n in names)
                        {
                            if (string.IsNullOrEmpty(n)) continue;
                            Vector2 m = measure(TextAt(n, size, w, measure), size);
                            if (m.x > w + 0.5f || m.y > hMax) { ok = false; break; }
                            if (m.y > needH) needH = m.y;
                        }
                    if (ok)
                        return new Result { fontSize = size, fits = true,
                            bandHeight = Mathf.Max(side * BandStartRatio - inset, Mathf.Min(hMax, needH + 2f)) };
                }
            }
            return new Result { fontSize = FontFloor, fits = false, bandHeight = side * BandMaxRatio - inset };
        }

        /// <summary>단추 안 띠 사각(하단 · 좌우 여백).</summary>
        public static Rect BandRect(Rect button, float bandHeight)
        {
            float inset = Inset(Mathf.Min(button.width, button.height));
            float h = Mathf.Min(bandHeight, button.height - 2f * inset);
            return new Rect(button.x + inset, button.yMax - inset - h, button.width - 2f * inset, h);
        }

        /// <summary>그림 사각 — 띠가 단추 반을 넘게 늘면 띠 위 남는 칸으로 줄인다(아니면 단추 가득 · 띠가 덮음).</summary>
        public static Rect ThumbRect(Rect button, float bandHeight, float fill)
        {
            float side = Mathf.Min(button.width, button.height);
            bool grown = bandHeight > side * BandStartRatio + 0.5f;
            float availH = grown ? button.height - bandHeight - 2f * Inset(side) : button.height;
            float s = Mathf.Max(0f, Mathf.Min(button.width, availH) * fill);
            float top = grown ? button.y + Inset(side) + (availH - s) * 0.5f : button.y + (button.height - s) * 0.5f;
            return new Rect(button.x + (button.width - s) * 0.5f, top, s, s);
        }
    }
}
