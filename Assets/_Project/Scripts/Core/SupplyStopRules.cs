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

        /// <summary>띠의 높이 (기준 캔버스). **격자 반 칸**이며 구역 이름표가 이미 쓰는 눈금이다.</summary>
        public const float DesignBandHeight = 96f;

        /// <summary>
        /// 실제 창에서 띠가 앉을 자리. **화면 좌표다** — 보드를 스크롤해도 따라가지 않는다(12-2).
        ///
        /// ⚠️ **가로 범위는 문서에 없다.** 12-2가 정한 것은 세로 시작과 높이뿐이라
        /// 창 전체 폭으로 둔다 — 「상단 띠」가 폭을 안 채우면 띠가 아니라 상자가 된다.
        /// **판정 자리로 올린다.**
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

        /// <summary>띠를 그리는가. **경고가 있을 때만 뜨고 없으면 자리를 안 먹는다**(12-2).</summary>
        public static bool BandIsVisible(bool storageEmpty, bool powerShort) =>
            storageEmpty || powerShort;

        // ---- 띠에 적을 것 ----

        /// <summary>전력 부족 문구. 변수 패널이 이미 쓰는 말을 그대로 쓴다 — 두 곳이 갈리지 않게.</summary>
        public const string PowerText = "전력 부족";

        /// <summary>
        /// 저장 재고 0 문구.
        ///
        /// ⚠️ **문구가 문서에 없다**(12-2에 텍스트 규정 자체가 없다). 띠에 아무 말도 안 적으면
        /// 빨간 막대만 뜨므로 잠정으로 둔 말이며, **설계가 정하면 갈아 끼울 자리**다.
        /// </summary>
        public const string StorageText = "탄약 재고 0";

        /// <summary>
        /// 띠에 적을 한 줄.
        ///
        /// ⚠️ **둘이 함께일 때의 처리도 문서에 없다.** 우선순위를 매기지 않고 **둘 다 적는다** —
        /// 하나를 고르면 나머지 하나가 화면에서 사라지고, 그것이 바로 이 장이 막으려는 것이다.
        /// </summary>
        public static string BandText(bool storageEmpty, bool powerShort)
        {
            if (storageEmpty && powerShort) return $"{StorageText} · {PowerText}";
            if (powerShort) return PowerText;
            if (storageEmpty) return StorageText;
            return string.Empty;
        }
    }
}
