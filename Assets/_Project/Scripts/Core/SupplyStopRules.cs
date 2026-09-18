using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 공급 정지 표시의 규칙만 (2026-09-09 신설 · UI 문서 12장 · `260909_W01` 3장).
    /// **그리지 않는다** — 자리와 참·거짓만 낸다.
    ///
    /// 이 장이 얹히는 자리는 **0차 표시 위계**다(12-4). 1차에서 3차까지는 「어디가 막혔나」를
    /// 찾게 하는 장치인데, 공격이 아예 멈춘 상태에서는 **찾기 전에 멈췄다는 것부터** 알아야 한다.
    /// 0차는 원인을 말하지 않으므로 흐름 우선 원칙을 깨지 않는다.
    /// </summary>
    public static class SupplyStopRules
    {
        // ---- 상단 경고 띠의 자리 (UI 문서 12-2) ----

        /// <summary>
        /// 문서 기준 해상도의 세로 (UI 문서 9-4 「화면 배치 (레이어 2, 1440 × 2560 기준)」).
        /// 화면 픽셀이 아니라 **기준 캔버스**이며, 실제 창에는 비율로 환산해 앉힌다 —
        /// `BoardController`의 구역 이름표가 쓰는 것과 같은 환산이다.
        /// </summary>
        public const float DesignScreenHeight = 2560f;

        /// <summary>띠가 시작하는 세로 (기준 캔버스). 그 위 0~768은 전투 화면이다(9-4).</summary>
        public const float DesignBandTop = 768f;

        /// <summary>
        /// 띠의 높이 (기준 캔버스).
        ///
        /// 🗑️ **구 96 폐기**(2026-09-18 사용자 확정 · 시안 3). 이 띠는 이제 경고 한 줄이
        /// 아니라 **조립 화면의 상단 상태 영역**이다 — 고철 수치와 **문제 목록**(미연결 ·
        /// 생산 정지 · 전력 부족)이 들어온다. 한 줄 높이로는 목록이 안 들어간다.
        ///
        /// 🗑️🗑️ **그 240 도 폐기다**(같은 날 사용자 육안 · 시안 4 ②). **인셋 바로 아래는
        /// 보드의 자리**인데 띠가 거기 앉아 **노드 이름판 위에 「고철 6,406」이 겹쳐** 떴다.
        /// 고철 수치와 문제 요약은 **하단 큰 버튼 바로 위**로 갔다(<see cref="MBI.UI.StatusStrip"/>).
        ///
        /// 📌 **값을 96 으로 되돌린다** — 이 사각은 이제 **인셋이 어디까지인가**를 재는 데만
        /// 쓰인다(<c>CombatInsetView.ClearsBand</c>). 그리는 곳은 없다.
        /// </summary>
        public const float DesignBandHeight = 96f;

        /// <summary>
        /// 실제 창에서 띠가 앉을 자리. **화면 좌표다** — 보드를 스크롤해도 따라가지 않는다(12-2).
        ///
        /// ✅ **가로는 창 전체 폭이다** (2026-09-10 · `260910_W01` 4장 1번 확정).
        /// 「화면 고정 · 보드 좌표가 아니라 화면 좌표」라는 12-2의 성격에서 따라온다.
        /// ⚠️ **W01이 구현의 근거 하나를 고쳤다** — 「뷰포트 폭이면 우상단 변수 패널과 겹친다」로
        /// 적었는데 **변수 패널은 하단이다.** 겹칠 후보는 좌상단 전투력 오버레이였다. 결론은 같다.
        /// </summary>
        public static Rect BandRect(float screenWidth, float screenHeight)
        {
            float scale = screenHeight / DesignScreenHeight;
            return new Rect(0f, DesignBandTop * scale, screenWidth, DesignBandHeight * scale);
        }

        // ---- 무엇이 경고인가 ----

        /// <summary>
        /// 저장 노드(창고)가 비었는가. **조립 화면이 보는 층**이다(12-1).
        ///
        /// ⚠️ **탄종별이 아니라 총량으로 본다.** 문서는 「저장 노드 재고 0」까지만 적었고
        /// 한 탄종만 0인 경우를 가르지 않았다 — 여기서 정하는 것이 아니라 **넓은 쪽**을
        /// 골랐다. 하나만 0이어도 뜨게 하면 관통을 안 만드는 배치에서 늘 켜져 있다.
        /// </summary>
        public static bool StorageIsEmpty(bool hasCombat, float storageStock) =>
            hasCombat && storageStock <= 0f;

        /// <summary>
        /// **생산이 실제로 멈췄는가** (2026-09-15 사용자 육안 — 「생산이 멈췄다는데 안 멈췄는데?」).
        ///
        /// ⚠️⚠️ **띠가 재고를 보고 생산을 말하고 있었다.** 조건은
        /// <see cref="StorageIsEmpty"/>(창고 0)인데 적는 말은 「생산이 멈췄습니다」다 —
        /// **둘은 다른 것이다.**
        ///
        /// 실측으로 두 자리에서 어긋났다 —
        /// ① **멀쩡한 판의 처음 18초.** 창고는 첫 도착(5.6초) 뒤에야 차기 시작하므로
        ///    그때까지 0 이다. 아무 문제가 없는데 띠가 뜬다.
        /// ② **운반로가 끊긴 판.** 노드는 **멀쩡히 만들고 있고** 벨트에 물건이 보이는데
        ///    닿지를 못한다 — 멈춘 것은 생산이 아니라 **전달**이다.
        ///    (끊긴 것은 「나가는 곳이 없다」가 이미 따로 말한다.)
        ///
        /// 📌 **말과 조건을 맞춘다.** 「생산이 멈췄다」는 **만드는 것이 0** 일 때 참이다.
        /// 재고가 비는 것은 **결과**이고, 결과를 원인의 말로 적으면 읽는 사람이 엉뚱한
        /// 곳을 고친다(오늘 사용자가 바로 그 물음을 했다).
        ///
        /// ⚠️ **전투가 없으면 안 띄운다** — 조립만 만지는 동안 붉은 띠가 상주하면
        /// 띠가 뜻을 잃는다(0차 표시 위계 · UI 문서 12-4).
        /// </summary>
        public static bool ProductionIsStopped(bool hasCombat, float ammoProduce) =>
            ProductionIsStopped(hasCombat, ammoProduce, stockOnHand: 0f);

        /// <summary>
        /// **만재는 경고가 아니다** (2026-09-16 사용자 확정 · 플랜 §74-21 ③).
        ///
        /// ⚠️⚠️ **생산이 0 인 까닭이 둘이다.**
        /// · **댈 것이 없다** — 줄이 끊겼거나 재료가 없다. 이것은 **사건**이다.
        /// · **받을 데가 없다** — 마운트·창고가 가득이라 상류가 스스로 멈췄다.
        ///   이것은 **잘 돌아가는 판의 정상 상태**다.
        ///
        /// 종전 판은 둘을 안 갈라, 보드가 **가득 차 있을수록** 붉은 띠가 떴다 —
        /// 09-16 실측에서 마운트 평균이 39.9/40 이었으니 사실상 상주하는 셈이다.
        /// 사용자가 「만재는 경고가 아니다」로 자른 자리다.
        ///
        /// 📌 **가르는 잣대는 손에 든 것**이다 — 창고든 마운트든 **남아 있으면**
        /// 지금 쏠 것이 있다는 뜻이고, 그때 멈춘 생산은 고칠 거리가 아니다.
        /// ⚠️ **가정** — 문서에 「만재 정체」 절이 없다(설계 역기입 자리).
        ///
        /// ⚠️ **대기 로봇 보드는 애초에 여기 안 온다** — 띠가 읽는 다리
        /// (`LogisticsOutputBridge`)는 2026-09-16 부터 **싸우는 판의 것만** 싣는다.
        /// 그래도 이 잣대가 필요한 것은, **싸우는 판도 가득 찰 수 있기** 때문이다.
        /// </summary>
        public static bool ProductionIsStopped(bool hasCombat, float ammoProduce, float stockOnHand) =>
            hasCombat && ammoProduce <= 0f && stockOnHand <= 0f;

        /// <summary>
        /// 전력이 모자란가. **사용률이 100%를 넘은 자리**이며 변수 패널의 빨간 점멸과 같은 판정이다
        /// (<see cref="HudMeters.UsageBand.Over"/> · UI 문서 3-3).
        ///
        /// ⚠️ 공급이 없는데 수요가 있는 경우도 여기 들어온다 — `PowerUsage`가 NaN을 내고
        /// 그때는 <paramref name="draw"/>가 0보다 큰지로 가른다. **없는 값을 0으로 덮지 않는다.**
        /// </summary>
        public static bool PowerIsShort(float supply, float draw)
        {
            if (HudMeters.UsageIsUndefined(supply)) return draw > 0f;
            return HudMeters.BandOf(HudMeters.PowerUsage(supply, draw)) == HudMeters.UsageBand.Over;
        }

        /// <summary>
        /// 마운트가 비었는가 — **공격이 실제로 멈춘 자리**다(12-1 · 전투 시스템 문서 10-2 #11).
        ///
        /// ⚠️ **시작 직후에 잠깐 뜨는 것도 사건이 맞다**(`260909_W01` 3장). 그때 로봇은
        /// 실제로 서 있기만 한다. 다만 전투가 아직 없으면 값 자체가 없는 것이라 거짓이다.
        /// </summary>
        public static bool MountIsEmpty(bool hasCombat, float mountTotal) =>
            hasCombat && mountTotal <= 0f;

        /// <summary>
        /// 띠에 **경고**가 있는가.
        ///
        /// ⚠️ **「띠를 그리는가」와 갈랐다**(2026-09-18 · 시안 3). 자리 자체는 이제
        /// 조립 화면에서 **늘 확보된다**(고철 수치가 거기 산다) — 달라지는 것은
        /// **경고를 적는가**이며, 그 판정이 이 함수다. 이름을 바꾸지 않은 까닭은
        /// 부르는 자리와 시험이 이 이름을 이미 쓰기 때문이다.
        /// </summary>
        public static bool BandIsVisible(bool storageEmpty, bool powerShort) =>
            storageEmpty || powerShort;

        /// <summary>
        /// 상단 영역에 적을 **문제 목록** (2026-09-18 설계 지시 · 조립 화면 상단).
        ///
        /// ⚠️⚠️ **여기서 새로 판정하지 않는다.** 셋 다 이미 다른 함수가 내는 참·거짓이고
        /// 이 함수는 **말로 옮기기만** 한다 — 판정이 두 곳에 살면 화면과 띠가 서로 다른
        /// 것을 믿게 된다(지침 §7).
        ///
        /// ⚠️ **빈 목록이 곧 「문제 없음」이다** — 없는 문제를 지어내 채우지 않는다.
        /// </summary>
        public static System.Collections.Generic.List<string> Problems(
            bool notConnected, bool productionStopped, bool powerShort, bool mountEmpty)
        {
            var list = new System.Collections.Generic.List<string>(4);
            if (notConnected) list.Add("미연결 — 나가는 곳이 없는 노드가 있다");
            if (productionStopped) list.Add("생산 정지 — 만드는 것이 없다");
            if (powerShort) list.Add("전력 부족 — 수요가 공급을 넘었다");
            if (mountEmpty) list.Add("마운트 빔 — 싣고 나갈 것이 없다");
            return list;
        }

        // ---- 띠에 적을 것 ----

        /// <summary>
        /// 띠에 적는 말 — **「생산이 멈췄습니다」 하나다** (2026-09-10 · `260910_W01` 4장).
        ///
        /// ⚠️ **원인을 말하지 않는다.** UI 문서가 0차의 성립 근거를 「0차는 원인을 말하지 않으므로
        /// 흐름 우선 원칙을 깨지 않는다」로 적어 두었는데, 구 잠정 문구 「탄약 재고 0 · 전력 부족」은
        /// **원인을 말한다.** 그것을 두면 0차가 1차에서 3차의 일을 대신하게 된다.
        ///
        /// **원인은 하단 변수 패널 두 줄이 이미 말한다** — 「하단은 얼마나, 상단은 멈췄다」.
        ///
        /// ⚠️ 구 표기 폐기 — `PowerText`·`StorageText` 둘과 「둘 다일 때 어떻게 적는가」가
        /// 함께 사라졌다. **문구가 하나가 되면서 그 물음 자체가 없어진다**(W01 4장 3번).
        /// </summary>
        public const string BandText = "생산이 멈췄습니다";
    }
}
