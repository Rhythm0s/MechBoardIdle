using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// HUD 막대의 **규칙만** 담는다 — 그리는 일은 UI가 하고 여기는 값을 판정한다
    /// (2026-09-09 신설 · UI 문서「변수 패널 실시간 표시」 3-3 · 「회피 스택 표시」 11-3).
    ///
    /// **왜 코어에 두는가.** 이 규칙들은 화면 없이도 시험할 수 있는 것들이다 —
    /// 「사용률이 0으로 나뉘는가」·「눈금이 몇 칸인가」는 `OnGUI` 밖의 판정이고,
    /// UI에 두면 <b>테스트가 못 보는 자리</b>가 된다(지침 §7 「테스트가 못 보는 자리」).
    /// </summary>
    public static class HudMeters
    {
        // ── 전력 사용률 ────────────────────────────────────────────────────────────
        //
        // **사용률 = 수요 ÷ 공급 · 상한이 없다**(2026-09-06 사용자 확정 · UI 문서 3-3).
        // 85%면 여유가 있고 100%를 넘으면 모자란다 — 전력 효율과 **방향이 반대**다.

        /// <summary>사용률을 낼 수 없는 자리 — 공급이 0이면 나눌 수가 없다.</summary>
        public static bool UsageIsUndefined(float supply) => supply <= 0f;

        /// <summary>
        /// 전력 사용률. <b>낼 수 없으면 <see cref="float.NaN"/></b>를 준다.
        ///
        /// ⚠️ **0으로 덮지 않는다.** 0은 「하나도 안 쓴다」는 뜻이고 NaN 은 「모른다」는 뜻이라,
        /// 덮으면 **없는 값이 0%로 화면에 뜬다** — UI 문서 3-3이 「없는 값을 0%나 100%로 적으면
        /// 거짓말이 된다」로 막아 둔 자리다.
        /// </summary>
        public static float PowerUsage(float supply, float draw) =>
            UsageIsUndefined(supply) ? float.NaN : draw / supply;

        /// <summary>사용률 막대가 지금 어느 띠에 있는가. 눈금 네 줄은 UI 문서 3-3이 정했다.</summary>
        public enum UsageBand
        {
            /// <summary>낼 수 없는 값 — 숫자는 `—`.</summary>
            Undefined,
            Normal,   // 0~80%
            Caution,  // 80~90%  [주의]
            Warning,  // 90~100% [경고]
            Over,     // 100% 초과 — 빨간색 점멸 + 벨트 흐름 중단 시각화
        }

        public static UsageBand BandOf(float usage)
        {
            if (float.IsNaN(usage)) return UsageBand.Undefined;
            if (usage > 1f) return UsageBand.Over;
            if (usage >= 0.9f) return UsageBand.Warning;
            if (usage >= 0.8f) return UsageBand.Caution;
            return UsageBand.Normal;
        }

        /// <summary>
        /// 막대가 실제로 차는 몫(0~1). **상한이 없는 축을 길이가 있는 막대에 담는 자리**다.
        ///
        /// 낼 수 없으면 <b>빈 막대</b>, 100%를 넘으면 <b>가득</b>이다 — 넘친 양은 길이가 아니라
        /// 색과 점멸이 말한다(UI 문서 3-3의 「공급 0 · 수요 있음 → 가득 채우고 빨간색 점멸」).
        /// </summary>
        public static float UsageFill(float usage, float draw)
        {
            if (!float.IsNaN(usage)) return Mathf.Clamp01(usage);
            // 공급이 0인데 수요가 있으면 「모자람이 극에 달한 것」이라 가득 채운다.
            // 수요도 0이면 아무 일도 없는 것이라 빈 막대다.
            return draw > 0f ? 1f : 0f;
        }

        /// <summary>값이 없으면 `—`. 있으면 백분율 정수.</summary>
        public static string UsageText(float usage) =>
            float.IsNaN(usage) ? "—" : $"{usage * 100f:F0}%";

        // ── 회피 스택 눈금 ─────────────────────────────────────────────────────────
        //
        // **눈금으로 나눈 바 하나 · 길이는 고정 · 눈금 수가 상한을 따른다**(UI 문서 11-3).
        // 부스터 1대면 눈금 2개, 5대면 10개다.

        /// <summary>눈금 상한. 넘으면 눈금을 10으로 고정하고 옆에 `10+`를 붙인다.</summary>
        public const int MaxTicks = 10;

        /// <summary>
        /// 실제로 그릴 눈금 수. 상한이 <see cref="MaxTicks"/>를 넘어도 **눈금은 열이다.**
        ///
        /// 근거 — 추진제는 군수 한 대 기준 15초에 하나라 열 칸을 채우려면 150초인데
        /// 스테이지 제한이 120초다. 게다가 「자동의 절약은 없다」라 실제 스택은 늘 밑바닥 근처다.
        /// **눈금 10 위쪽은 버려도 잃는 정보가 없다.**
        /// </summary>
        public static int TickCount(int capacity) => Mathf.Clamp(capacity, 0, MaxTicks);

        /// <summary>채워진 눈금 수. 상한을 넘겨도 **열이 다 찬 상태**로 둔다.</summary>
        public static int FilledTicks(int stacks, int capacity)
        {
            int ticks = TickCount(capacity);
            if (ticks <= 0) return 0;
            if (capacity <= MaxTicks) return Mathf.Clamp(stacks, 0, ticks);
            // 상한이 10을 넘으면 눈금 하나가 여러 칸을 대표한다 — 비율로 접는다.
            return Mathf.Clamp(Mathf.RoundToInt(stacks / (float)capacity * ticks), 0, ticks);
        }

        /// <summary>상한이 눈금 수보다 크면 붙는 꼬리표. 아니면 빈 문자열.</summary>
        public static string OverflowTag(int capacity) => capacity > MaxTicks ? "10+" : string.Empty;

        // ── 나뉜 막대(탄약 줄) ─────────────────────────────────────────────────────

        /// <summary>
        /// 이 탄종이 막대에서 칸을 차지하는가. **재고가 0이면 칸이 없다**(UI 문서 3-3).
        ///
        /// 세 칸을 늘 그려 두면 화면이 답하는 물음이 「지금 뭐가 있는가」가 아니라
        /// 「탄종이 셋이다」로 바뀐다 — 후자는 아무도 안 묻는다.
        /// </summary>
        public static bool SegmentIsVisible(float value) => value > 0f;

        /// <summary>
        /// 점멸이 지금 켜져 있는가. **초당 2.5번**이며 화면 셋이 같은 박자를 쓴다 —
        /// 변수 패널 바 · 전투 원인 배지 · 조립 마운트 그리드.
        ///
        /// ⚠️ **주기가 문서에 없다**(UI 문서 3-3·12-4 어디에도 초·Hz가 없다). 여기 있는 값은
        /// 종전 코드가 쓰던 것을 **한 자리로 모은 것**이지 새로 정한 것이 아니다.
        /// 박자가 갈리면 같은 「안 된다」가 서로 다른 사건으로 읽힌다.
        /// </summary>
        public static bool BlinkOn(float unscaledTime) => ((int)(unscaledTime * 2.5f) & 1) == 0;

        /// <summary>
        /// 0인 칸이 차지하는 몫(막대 폭 대비). **재고가 0인 탄종도 칸을 유지한다**
        /// (2026-09-09 사용자 확정 · UI 문서 3-3 · 구 표기 「칸을 차지하지 않는다」 폐기).
        ///
        /// **숨기면 「안 만들고 있다」와 「다 썼다」가 같은 화면이 된다.** 그 둘은 플레이어가
        /// 해야 할 일이 정반대인 상태다.
        ///
        /// ⚠️ **폭이 문서에 없다.** 12장도 3-3도 「0을 적는다」까지만 정했다. 이름 한 글자와
        /// 0이 들어갈 만큼으로 잡은 잠정값이며 화면에서 고칠 자리다.
        /// </summary>
        public const float EmptySegmentShare = 0.12f;

        /// <summary>
        /// 값이 있는 칸들이 나눠 가질 폭. 0인 칸이 먼저 자리를 떼어 간 나머지다.
        ///
        /// ⚠️ **0인 칸이 막대를 다 먹지 않게 막는다** — 셋 다 0이면 떼어 갈 몫이 폭을 넘으므로
        /// 그때는 고르게 나눈다. 안 막으면 폭이 음수가 되어 칸이 뒤집힌다.
        /// </summary>
        public static float FilledWidth(float totalWidth, int emptyCount)
        {
            float reserved = totalWidth * EmptySegmentShare * emptyCount;
            return Mathf.Max(0f, totalWidth - reserved);
        }

        /// <summary>0인 칸 하나의 폭. 값이 있는 칸이 하나도 없으면 고르게 나눠 갖는다.</summary>
        public static float EmptySegmentWidth(float totalWidth, int emptyCount, bool anyFilled)
        {
            if (emptyCount <= 0) return 0f;
            return anyFilled ? totalWidth * EmptySegmentShare : totalWidth / emptyCount;
        }

        /// <summary>
        /// 막대 전체가 나타내는 양. 상한이 있으면 <b>상한이 막대 전체</b>라 빈 꼬리가 남고,
        /// 상한이 없으면 있는 것끼리의 합으로 나눈다.
        ///
        /// 합이 상한을 넘는 경우에도 잘리지 않게 큰 쪽을 쓴다 — 창고가 넘친 상태를
        /// 막대 밖으로 밀어내면 그 사실이 화면에서 사라진다.
        /// </summary>
        public static float SegmentSpan(float sum, float capacity) =>
            capacity > 0f ? Mathf.Max(capacity, sum) : sum;
    }
}
