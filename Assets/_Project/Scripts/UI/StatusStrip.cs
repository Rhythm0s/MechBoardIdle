using System.Collections.Generic;
using MBI.Core;
using UnityEngine;

namespace MBI.UI
{
    /// <summary>
    /// **고철 수치 + 물류 문제 요약** — 전투·조립 **두 화면이 같은 자리에서 같은 것을 보인다**
    /// (2026-09-18 사용자 육안 · 시안 4 ② · 「하단 큰 버튼 바로 위」).
    ///
    /// 🗑️ **폐기 — 상단 경고 띠**(구 `SupplyStopRules.BandRect` 768~1008 · 같은 날 오후에
    ///    240 으로 넓혔던 그 띠다). **인셋 바로 아래는 보드의 자리**인데 띠가 거기 앉아
    ///    노드 이름판 위에 「고철 6,406」이 겹쳐 떴다(사용자 스크린샷 2).
    ///
    /// 🗑️ **함께 폐기 — 경고를 캐릭터 옆에 띄우던 것.** 「나가는 곳이 없다」는 물류의 말이지
    ///    로봇의 말이 아니다. 말은 그 말이 고쳐질 자리 곁에 있어야 한다.
    ///
    /// 📌 **왜 한 파일인가.** 같은 줄을 전투(`MBI.Combat`)와 조립(`MBI.Logistics`)이 각각
    /// 그리면 **같은 일을 하는 자리가 둘**이 된다 — 이 리포에서 되풀이된 결함 종류다.
    /// 그래서 **모으는 것도 그리는 것도 여기 하나**이고, 두 화면은 자리만 달리 준다.
    ///
    /// ⚠️ **판정하지 않는다.** 무엇이 문제인가는 <see cref="SupplyStopRules"/> 가 이미 냈다.
    /// ⚠️ **고철을 세지 않는다.** 잔액은 방치 런타임이 게시한 것을 그대로 읽는다.
    /// </summary>
    public static class StatusStrip
    {
        /// <summary>띠 높이 (기준 캔버스 · ⚠️ 가정 — 머리 한 줄 + 요약 한 줄).</summary>
        public const float DesignHeight = 132f;

        /// <summary>큰 버튼과의 사이 (기준 캔버스 · ⚠️ 가정 — 붙으면 둘 다 안 읽힌다).</summary>
        public const float DesignGap = 16f;

        /// <summary>
        /// 띠가 앉을 자리 — **하단 큰 버튼 바로 위**.
        ///
        /// ⚠️ 두 화면의 큰 버튼이 서로 다른 y 에 있다(전투 「▼ 조립」 · 조립 「▲ 전투로」).
        /// 그래서 **버튼에서 재고**, 화면 이름으로 가르지 않는다 — 버튼이 움직이면 띠도 따라간다.
        /// </summary>
        public static Rect RectAbove(Rect bigButton, float screenWidth, float screenHeight)
        {
            float sc = UiLayout.Scale(screenHeight);
            float h = DesignHeight * sc;
            float pad = UiLayout.ChipBarPad * sc;
            float w = Mathf.Max(0f, screenWidth - pad * 2f);
            float y = bigButton.y - DesignGap * sc - h;
            return new Rect(pad, y, w, h);
        }

        /// <summary>
        /// 지금 무엇이 문제인가 — **판정은 전부 이미 있던 함수가 낸다.**
        /// 여기는 그 참·거짓을 모아 오기만 한다.
        /// </summary>
        public static List<string> Problems(out bool warn)
        {
            // ⚠️ **재고가 아니라 생산을 본다**(2026-09-15 사용자 육안).
            //    ⚠️ **만재는 경고가 아니다**(2026-09-16 · §74-21 ③).
            float stockOnHand = SupplySignals.StorageStock + SupplySignals.MountTotal;
            bool stopped = SupplyStopRules.ProductionIsStopped(
                SupplySignals.HasCombat, LogisticsOutputBridge.AmmoProduce, stockOnHand);
            bool powerShort = SupplyStopRules.PowerIsShort(
                LogisticsOutputBridge.PowerSupply, LogisticsOutputBridge.PowerDraw);

            // 미연결 — 물류가 이미 낸 원인을 옮길 뿐이다(여기서 그래프를 다시 안 본다).
            ConstraintCause cause = LogisticsOutputBridge.GlobalCause;
            bool notConnected = cause == ConstraintCause.Blocked || cause == ConstraintCause.NoInput;
            bool mountEmpty = SupplyStopRules.MountIsEmpty(
                SupplySignals.HasCombat, SupplySignals.MountTotal);

            warn = SupplyStopRules.BandIsVisible(stopped, powerShort);
            return SupplyStopRules.Problems(notConnected, stopped, powerShort, mountEmpty);
        }

        // 🗑️ **폐기**(2026-09-30) — `WarnText`(주황)와 `ScrapText`(흰끼)는 부르는 곳이 0 이다.
        //    주황은 **빨강**으로 갈렸고(확정 ③), 고철 글자는 **칩 줄로 옮겨 갔다**(확정 ②).
        //    값을 남겨 두면 다음 사람이 「여기도 쓰나」를 다시 확인해야 한다.

        private static readonly Color QuietText = new Color(0.72f, 0.74f, 0.78f);

        /// <summary>
        /// 경고 글자색 — **빨강** (2026-09-30 사용자 확정 ③). ⚠️ 값은 가정이다.
        /// </summary>
        private static readonly Color AlarmText = new Color(1f, 0.32f, 0.28f);

        /// <summary>점멸 한 주기(초). ⚠️ 가정 — 너무 빠르면 읽기 전에 사라진다.</summary>
        public const float BlinkSeconds = 0.8f;

        /// <summary>
        /// 점멸의 지금 밝기(0~1). **꺼져도 완전히 안 사라진다** — 0 까지 내리면
        /// 글자가 있다 없다 해서 읽는 사람이 눈으로 쫓게 된다.
        ///
        /// ⚠️ **`unscaledTime` 이다** — 설정 판이 게임을 세워도 경고는 계속 뛴다.
        /// 멈춘 동안 경고가 굳으면 「지금 문제가 있다」가 안 읽힌다.
        /// </summary>
        public static float BlinkAlpha(float time)
        {
            float phase = Mathf.Repeat(time, BlinkSeconds) / BlinkSeconds;
            return Mathf.Lerp(0.45f, 1f, Mathf.Abs(Mathf.Sin(phase * Mathf.PI)));
        }

        /// <summary>
        /// 사다리에서 **한 칸 아래** 크기. 맨 아래면 제 값을 그대로 돌려준다.
        ///
        /// ⚠️ <see cref="KoreanFont.Snap"/> 은 **올려** 잡으므로 내려갈 때는 쓸 수 없다.
        /// </summary>
        /// <summary>시험이 보는 문 — 규칙은 하나이고 시험이 그 하나를 본다.</summary>
        public static int NextSmallerForTest(int px) => NextSmaller(px);

        private static int NextSmaller(int px)
        {
            int[] ladder = KoreanFont.Ladder;
            int best = ladder[0];
            for (int i = 0; i < ladder.Length; i++)
                if (ladder[i] < px && ladder[i] > best) best = ladder[i];
            return best < px ? best : px;
        }

        /// <summary>
        /// 그린다. **자리는 부르는 쪽이 준다**(두 화면의 큰 버튼이 다른 데 있다).
        ///
        /// ⚠️⚠️ **판을 안 깐다** (2026-09-30 사용자 확정 ①).
        /// 🗑️ 구 꼴 폐기 — 판 + 왼쪽 정렬 두 줄(고철 / 문제 요약).
        ///    · **고철은 위로 갔다**(골드 칩 옆 · 확정 ②) — 재화는 재화끼리 둔다.
        ///    · 남은 한 줄에 판을 두르면 **아무 일도 없는데 화면에 상자가 하나 더** 있다.
        ///      문제가 없을 때 가장 좋은 표시는 **거의 안 보이는 것**이다.
        ///
        /// ⚠️ **한 줄로 요약한다** — 목록 셋을 다 펴면 큰 버튼을 밀어낸다. 자세한 것은
        /// 노드 위 표식과 팝오버가 이미 말하고, 여기는 **「지금 막힌 데가 있다」**를 말한다.
        ///
        /// ✅ **문제가 있으면 빨갛게 점멸한다**(확정 ③) — 조용한 회색과 **색이 갈려야**
        /// 눈이 간다. 문구는 물류가 낸 **그 문제의 말**을 그대로 쓴다(여기서 지어내지 않는다).
        /// </summary>
        public static void Draw(Rect strip)
        {
            if (strip.height <= 4f || strip.width <= 4f) return;

            // ⚠️ `warn`(띠 노출 판정)은 이제 여기서 안 쓴다 — 색을 가르는 것은 **문제가 있는가**
            //    하나다. 부르는 쪽이 그 값을 여전히 쓰므로 함수는 그대로 둔다.
            List<string> problems = Problems(out _);
            bool alarm = problems.Count > 0;

            // ⚠️ **막는 자리는 그대로 잡는다** — 글자 뒤로 터치가 새면 그 밑의 판이 눌린다.
            UiBlockers.Add(strip);

            float sc = UiLayout.Scale(Screen.height);

            var row = new GUIStyle(GUI.skin.label)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(11,
                    Mathf.RoundToInt(strip.height * 0.30f))),
                fontStyle = alarm ? FontStyle.Bold : FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow,
            };

            // ⚠️ 문제가 없으면 「문제 없음」이다 — **없는 문제를 지어내 채우지 않는다.**
            string text;
            if (!alarm) text = "물류 · 조립 — 문제 없음";
            else if (problems.Count == 1) text = "⚠ " + problems[0];
            else text = "⚠ " + problems[0] + "  외 " + (problems.Count - 1) + "건";

            // ⚠️⚠️ **칸에 맞게 줄인다**(2026-09-30 · 빌드 육안 두 번째).
            //
            // 🗑️ 구 처리 폐기 — 자리만 좁히고 글자는 그대로 뒀다. `Overflow` 라
            //    좁힌 칸 밖으로 **그대로 흘러넘쳐** 마일스톤 카드를 다시 침범했다.
            //    자리를 좁히는 것과 글자가 드는 것은 **다른 일**이다.
            //
            // 📌 **재서 줄인다** — `CalcSize` 로 실제 폭을 보고 들 때까지 내린다.
            //
            // ⚠️⚠️ **1 씩 빼면 안 내려간다.** `KoreanFont.Snap` 은 사다리
            //    <c>{16,24,36,44}</c> 위로 **올려** 잡으므로 <c>Snap(44-1)</c> 이 다시 44 다.
            //    사다리를 **한 칸씩 내려가야** 한다 — 크기 가짓수가 넷뿐인 것은 아틀라스
            //    때문이고(`KoreanFont.Snap` 주석), 그 제약을 여기서 우회하면 안 된다.
            //
            // ⚠️ 맨 아래(16)에서도 넘치면 **넘치는 채로 둔다** — 글자를 잘라 뜻을 바꾸지
            //    않는다. 그때는 자리가 좁은 것이지 글자가 큰 것이 아니다.
            var content = new GUIContent(text);
            while (row.CalcSize(content).x > strip.width)
            {
                int next = NextSmaller(row.fontSize);
                if (next == row.fontSize) break;      // 사다리 바닥 — 더 못 내린다
                row.fontSize = next;
            }

            Color prev = GUI.color;
            if (alarm)
            {
                row.normal.textColor = AlarmText;
                GUI.color = new Color(prev.r, prev.g, prev.b,
                                      prev.a * BlinkAlpha(Time.unscaledTime));
            }
            else
            {
                row.normal.textColor = QuietText;
            }

            GUI.Label(strip, content, row);
            GUI.color = prev;
        }
    }
}