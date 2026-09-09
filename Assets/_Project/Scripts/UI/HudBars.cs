using System.Collections.Generic;
using MBI.Core;
using UnityEngine;

namespace MBI.UI
{
    /// <summary>
    /// HUD 막대 셋을 IMGUI로 그린다 — **사용률 막대 · 눈금 바 · 나뉜 막대**
    /// (2026-09-09 신설 · UI 문서 3-3 · 11-3).
    ///
    /// **판정은 여기서 하지 않는다.** 몇 칸인지·어느 띠인지·값이 있는지는
    /// <see cref="HudMeters"/>가 정하고 여기는 그 답을 픽셀로 옮긴다(§3 「UI는 매핑만」).
    /// 그래서 규칙은 배치모드가 보고, 이 파일은 화면이 본다.
    ///
    /// ⚠️ **이모지를 쓰지 않는다** — WebGL에서 두부(□)로 찍힌다(VariablePanel 주석과 같은 뿌리).
    /// </summary>
    public static class HudBars
    {
        /// <summary>막대 한 줄의 세로 크기(px). 글자 한 줄과 나란히 읽히는 높이다.</summary>
        public const float BarHeight = 14f;

        private static Texture2D _white;
        private static Texture2D White
        {
            get
            {
                if (_white != null) return _white;
                // hideFlags를 안 주면 플레이 종료 때 「정리되지 않은 텍스처」로 남는다.
                _white = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
                return _white;
            }
        }

        /// <summary>단색 사각 하나. GUI.color를 건드리고 되돌린다 — 호출자의 색을 안 물들인다.</summary>
        public static void Fill(Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, White);
            GUI.color = prev;
        }

        private static void Frame(Rect rect, Color color)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, 1f), color);
            Fill(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            Fill(new Rect(rect.x, rect.y, 1f, rect.height), color);
            Fill(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        private static readonly Color Bed = new Color(0.10f, 0.11f, 0.13f, 0.85f);
        private static readonly Color Edge = new Color(0.96f, 0.94f, 0.86f, 0.35f);

        // ── 사용률 막대 ────────────────────────────────────────────────────────────

        private static readonly Color UsageNormal = new Color(0.45f, 0.80f, 0.55f);
        private static readonly Color UsageCaution = new Color(0.95f, 0.85f, 0.35f);
        private static readonly Color UsageWarning = new Color(0.98f, 0.62f, 0.25f);
        private static readonly Color UsageOver = new Color(0.92f, 0.30f, 0.28f);

        private static Color ColorOf(HudMeters.UsageBand band)
        {
            switch (band)
            {
                case HudMeters.UsageBand.Caution: return UsageCaution;
                case HudMeters.UsageBand.Warning: return UsageWarning;
                case HudMeters.UsageBand.Over: return UsageOver;
                default: return UsageNormal;
            }
        }

        /// <summary>점멸 위상 — 100% 초과에서만 쓴다. 변수 패널의 병목 점멸과 같은 주기다.</summary>
        private static bool Blink() => ((int)(Time.unscaledTime * 2.5f) & 1) == 0;

        /// <summary>
        /// 전력 사용률 막대. <paramref name="supply"/>·<paramref name="draw"/>를 **나누지 않은 채**
        /// 받는다 — 나눌 수 없는 자리를 여기서 알아보아야 숫자를 `—`로 적을 수 있다.
        ///
        /// 100%를 넘으면 막대는 가득 찬 채 **빨간색으로 점멸**한다. 길이로는 더 못 넘치니
        /// 넘친 양은 옆 숫자(예: `130%`)가 말한다.
        /// </summary>
        public static void Usage(Rect rect, float supply, float draw)
        {
            float usage = HudMeters.PowerUsage(supply, draw);
            HudMeters.UsageBand band = HudMeters.BandOf(usage);
            float fill = HudMeters.UsageFill(usage, draw);

            Fill(rect, Bed);
            // 값이 없으면 **비운다** — 0%로 채우면 「안 쓰는 중」과 구별이 사라진다.
            bool over = band == HudMeters.UsageBand.Over || (band == HudMeters.UsageBand.Undefined && draw > 0f);
            if (!(over && !Blink()))
            {
                Color c = over ? UsageOver : ColorOf(band);
                Fill(new Rect(rect.x + 1f, rect.y + 1f, (rect.width - 2f) * fill, rect.height - 2f), c);
            }
            // 80·90 눈금 — 띠 경계를 막대 위에 새겨 둔다(어디서 노랑이 되는지가 보여야 한다).
            Fill(new Rect(rect.x + rect.width * 0.8f, rect.y, 1f, rect.height), Edge);
            Fill(new Rect(rect.x + rect.width * 0.9f, rect.y, 1f, rect.height), Edge);
            Frame(rect, Edge);
        }

        /// <summary>사용률 숫자. 낼 수 없으면 `—`.</summary>
        public static string UsageText(float supply, float draw) =>
            HudMeters.UsageText(HudMeters.PowerUsage(supply, draw));

        // ── 회피 눈금 바 ───────────────────────────────────────────────────────────

        private static readonly Color TickOn = new Color(0.55f, 0.85f, 1f);
        private static readonly Color TickInvincible = new Color(1f, 0.95f, 0.6f);

        /// <summary>
        /// 회피 스택 눈금 바 — **길이는 고정이고 눈금 수가 상한을 따른다**(UI 문서 11-3).
        /// 부스터를 더 놓으면 칸이 늘어나는 것이 화면에서 보여야 한다.
        ///
        /// 상한이 0이면(부스터 없음) **빈 테두리만** 남긴다 — 칸이 하나도 없는 것이 사실이다.
        /// </summary>
        public static void Ticks(Rect rect, int stacks, int capacity, bool invincible)
        {
            Fill(rect, Bed);
            int ticks = HudMeters.TickCount(capacity);
            if (ticks > 0)
            {
                int filled = HudMeters.FilledTicks(stacks, capacity);
                float w = (rect.width - 2f) / ticks;
                Color on = invincible ? TickInvincible : TickOn;
                for (int i = 0; i < ticks; i++)
                {
                    var cell = new Rect(rect.x + 1f + w * i + 1f, rect.y + 1f, w - 2f, rect.height - 2f);
                    Fill(cell, i < filled ? on : new Color(on.r, on.g, on.b, 0.18f));
                }
            }
            Frame(rect, Edge);
        }

        // ── 나뉜 막대(탄약 줄) ─────────────────────────────────────────────────────

        /// <summary>막대 한 칸. 값이 0인 칸은 <see cref="Segments"/>가 아예 안 그린다.</summary>
        public struct Segment
        {
            public string label;
            public float value;
            public Color color;
        }

        /// <summary>칸 이름이 들어가려면 최소 이만큼은 있어야 한다 — 아니면 이름을 접는다.</summary>
        private const float LabelMinWidth = 30f;

        /// <summary>
        /// 막대 하나를 <paramref name="segments"/>로 나누고 **칸 안에 이름**을 적는다(UI 문서 3-3).
        ///
        /// ⚠️ **재고가 0인 탄종은 칸을 차지하지 않는다.** 세 칸을 늘 그려 두면 「지금 뭐가
        /// 있는가」가 아니라 「탄종이 셋이다」만 읽힌다 — 화면이 답해야 하는 물음이 아니다.
        ///
        /// <paramref name="capacity"/>가 0보다 크면 **총량 상한이 막대 전체**다(빈 꼬리가 남는다).
        /// 0이면 있는 것끼리 비율로 나눈다.
        /// </summary>
        public static void Segments(Rect rect, IReadOnlyList<Segment> segments, float capacity, GUIStyle labelStyle)
        {
            Fill(rect, Bed);

            float sum = 0f;
            for (int i = 0; i < segments.Count; i++)
                if (HudMeters.SegmentIsVisible(segments[i].value)) sum += segments[i].value;

            float span = HudMeters.SegmentSpan(sum, capacity);
            if (span > 0f)
            {
                float x = rect.x + 1f;
                float usable = rect.width - 2f;
                for (int i = 0; i < segments.Count; i++)
                {
                    Segment s = segments[i];
                    if (!HudMeters.SegmentIsVisible(s.value)) continue; // 0인 탄종은 칸이 없다
                    float w = usable * (s.value / span);
                    var cell = new Rect(x, rect.y + 1f, w, rect.height - 2f);
                    Fill(cell, s.color);
                    if (w >= LabelMinWidth && labelStyle != null)
                    {
                        // 칸 색이 밝아 검은 글자가 읽힌다 — 흰 글자는 노랑 칸에서 사라진다.
                        Color prev = GUI.contentColor;
                        GUI.contentColor = new Color(0.08f, 0.08f, 0.10f);
                        GUI.Label(cell, s.label, labelStyle);
                        GUI.contentColor = prev;
                    }
                    x += w;
                }
            }
            Frame(rect, Edge);
        }

        /// <summary>
        /// 세로로 흐르는 IMGUI 배치 안에서 막대 한 줄을 얻는다.
        /// <c>GUILayout</c> 사이에 끼워도 다음 줄이 겹치지 않게 자리를 미리 잡아 둔다.
        /// </summary>
        public static Rect Row(float width, float height = BarHeight) =>
            GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
    }
}
