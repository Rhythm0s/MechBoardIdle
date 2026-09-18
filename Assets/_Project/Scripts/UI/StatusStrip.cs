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

        private static readonly Color WarnText = new Color(1f, 0.72f, 0.42f);
        private static readonly Color ScrapText = new Color(0.95f, 0.94f, 0.90f);
        private static readonly Color QuietText = new Color(0.72f, 0.74f, 0.78f);

        /// <summary>
        /// 그린다. **자리는 부르는 쪽이 준다**(두 화면의 큰 버튼이 다른 데 있다).
        ///
        /// ⚠️ **한 줄로 요약한다** — 목록 셋을 다 펴면 큰 버튼을 밀어낸다. 자세한 것은
        /// 노드 위 표식과 팝오버가 이미 말하고, 여기는 **「지금 막힌 데가 있다」**를 말한다.
        /// </summary>
        public static void Draw(Rect strip)
        {
            if (strip.height <= 4f || strip.width <= 4f) return;

            UiPlate.Draw(strip);
            UiBlockers.Add(strip);

            List<string> problems = Problems(out bool warn);

            float sc = UiLayout.Scale(Screen.height);
            float pad = 18f * sc;
            float rowH = strip.height * 0.5f;

            // ✅ **작게**(2026-09-18 사용자 육안 ④ — 「하단 고철 개수가 너무 큼」).
            //    🗑️ 구 0.58 폐기. 이 줄은 **읽는 수**이지 화면의 머리글이 아니다 —
            //    크게 두면 그 아래 문제 요약보다 무거워져 눈이 수부터 읽는다.
            var head = new GUIStyle(GUI.skin.label)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(11, Mathf.RoundToInt(rowH * 0.40f))),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Overflow,
            };
            head.normal.textColor = ScrapText;

            // 고철 — **방치 런타임이 게시한 잔액**이다(여기서 세지 않는다).
            GUI.Label(new Rect(strip.x + pad, strip.y, strip.width - pad * 2f, rowH),
                      "고철 " + IdleSignals.WalletScrap.ToString("N0"), head);

            // ⚠️ **머리 줄보다 작아야 한다.** 고철을 0.40 으로 줄였으니 이 줄도 같이 내린다 —
            //    안 내리면 요약이 수보다 커져 무게가 뒤집힌다(구 0.46 폐기).
            var row = new GUIStyle(head)
            {
                fontSize = KoreanFont.Snap(Mathf.Max(10, Mathf.RoundToInt(rowH * 0.34f))),
                fontStyle = FontStyle.Normal,
            };
            row.normal.textColor = warn ? WarnText : QuietText;

            // ⚠️ 문제가 없으면 「문제 없음」이다 — **없는 문제를 지어내 채우지 않는다.**
            string text;
            if (problems.Count == 0) text = "물류 · 조립 — 문제 없음";
            else if (problems.Count == 1) text = "⚠ " + problems[0];
            else text = "⚠ " + problems[0] + "  외 " + (problems.Count - 1) + "건";

            GUI.Label(new Rect(strip.x + pad, strip.y + rowH, strip.width - pad * 2f, rowH),
                      text, row);
        }
    }
}
