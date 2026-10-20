using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// **마운트 팝오버** (2026-10-19 사용자 확정 #171 · (a)안).
    ///
    /// 보드에서 **마운트 칸**(그 판 주인의 슬롯 묶음 칸 + 포트 칸)을 탭하면 노드 팝오버 틀로 뜨는 판이다.
    /// 줄 = 제목 「마운트 A/B」 · 「적재 n/cap · 탄종」 · 「강화 재료 n/비용」 · 단추 「강화 (재료 전부 투입)」.
    /// 재료 수치가 화면에 처음 보이는 자리다(HUD 칩 줄엔 안 넣는다 · 사용자 (a)만 선택).
    ///
    /// ⚠️ 글자는 가정이다(사용자가 바꿀 수 있다). 숫자는 러너가 실어 둔 값을 읽는다 — 여기서 짓지 않는다.
    /// ⚠️ 강화 신호는 설정 판 단추와 **같은 문**(`IdleSignals.RequestMountEnhance`)이다 — 재료를 쓰는 곳은
    ///    방치 런타임 한 곳 그대로이고, 이중 지출은 `MountEnhanceRule.CanEnhance` 가 막는다.
    /// </summary>
    public static class MountPopover
    {
        /// <summary>
        /// 이 칸이 <paramref name="owner"/> 판의 **마운트 칸**인가 — 슬롯 묶음 칸(그림 열) 또는 포트 칸.
        /// A = x1 · y0~3 + 포트 (1,4) · B = x3 · x8 · y10~13 + 포트 (2,10) · (9,10).
        /// 묶음 칸은 마스크 밖(노드를 못 놓는 자리)이고 포트 칸은 파츠 안 일반 칸이다.
        /// </summary>
        public static bool IsMountCell(Vector2Int cell, MountOwner owner) =>
            IsSlotCell(cell, owner) || IsPortCell(cell, owner);

        /// <summary>슬롯 묶음 칸 — `MountDisplay` 가 그림을 세우는 바로 그 칸들(열 · 줄을 거기서 읽는다).</summary>
        public static bool IsSlotCell(Vector2Int cell, MountOwner owner)
        {
            int y0 = MountDisplay.SlotRowStart(owner);
            if (cell.y < y0 || cell.y >= y0 + MountDisplay.SlotsPerPort) return false;
            foreach (MountPort p in PartLayout.MountPortsFor(owner))
                if (cell.x == MountDisplay.GroupColumn(p.cell, p.face, owner)) return true;
            return false;
        }

        /// <summary>포트 칸 — 그 판 주인의 마운트 고정 포트가 붙은 칸.</summary>
        public static bool IsPortCell(Vector2Int cell, MountOwner owner)
        {
            foreach (MountPort p in PartLayout.MountPortsFor(owner))
                if (p.cell == cell) return true;
            return false;
        }

        /// <summary>제목 — ⚠️ 가정.</summary>
        public static string Title(MountOwner owner) =>
            owner == MountOwner.RobotB ? "마운트 B" : "마운트 A";

        /// <summary>적재 줄 — 「적재 n/cap · 탄종」. 실린 것이 없으면 「비어 있음」. ⚠️ 가정.</summary>
        public static string LoadLine(float now, float capacity, string kinds) =>
            $"적재 {Floor(now)}/{Floor(capacity)} · {(string.IsNullOrEmpty(kinds) ? "비어 있음" : kinds)}";

        /// <summary>재료 줄 — 「강화 재료 n/비용」. ⚠️ 가정.</summary>
        public static string MaterialLine(double material, double cost) =>
            $"강화 재료 {Floor((float)material)}/{Floor((float)cost)}";

        /// <summary>
        /// 재료 + 고철 줄 — 「강화 재료 n/100 · 고철 n/300」(#172 보충 · 둘 다 한 줄). 고철 비용 0 이면 재료만. ⚠️ 가정.
        /// </summary>
        /// <summary>#175 ⓐ 아이콘 꼴 앞 조각 — 「강화 재료 n/100 · 」(뒤에 고철 아이콘).</summary>
        public static string MaterialPrefix(double material, double cost) => MaterialLine(material, cost) + " · ";

        /// <summary>#175 ⓐ 아이콘 꼴 뒤 조각 — 「250/300」(고철 아이콘 뒤).</summary>
        public static string ScrapSuffix(double scrap, double scrapCost) =>
            $"{Floor((float)scrap)}/{Floor((float)scrapCost)}";

        public static string MaterialLine(double material, double cost, double scrap, double scrapCost) =>
            scrapCost <= 0d ? MaterialLine(material, cost)
                : MaterialLine(material, cost) + $" · 고철 {Floor((float)scrap)}/{Floor((float)scrapCost)}";

        /// <summary>단추 글자 — 켜졌으면 「강화 ×n 적용」, 아니면 설정 판과 같은 글자.</summary>
        public static string ButtonLabel(bool enhanced, float enhancedCoef) =>
            enhanced ? MountEnhanceRule.AppliedLabel(enhancedCoef) : MountEnhanceRule.ButtonLabel;

        /// <summary>
        /// 단추가 눌리는가 — **고칠 수 있는 층**(도전 층은 보기만 · #148 결)이고 강화 조건이 서야 한다.
        /// </summary>
        public static bool ButtonEnabled(bool canEdit, bool enhanced, double material, double cost) =>
            canEdit && MountEnhanceRule.CanEnhance(enhanced, material, cost);

        /// <summary>재료 + 고철(#172) — 팝오버 단추가 쓰는 판.</summary>
        public static bool ButtonEnabled(bool canEdit, bool enhanced, double material, double cost,
                                         double scrap, double scrapCost) =>
            canEdit && MountEnhanceRule.CanEnhance(enhanced, material, cost, scrap, scrapCost);

        // ── 러너가 싣는 값(로직스는 러너를 모른다 — 신호로 건넨다) ─────────────────

        private static readonly float[] Now = new float[2];
        private static readonly float[] Cap = new float[2];
        private static readonly string[] Kinds = { "", "" };

        /// <summary>강화 비용(s4Cost) — 러너가 싣는다. 0 이면 단추가 안 눌린다.</summary>
        public static float EnhanceCost;

        /// <summary>강화 배율(enhancedMountCoef) — 「적용」 글자에 쓴다.</summary>
        public static float EnhancedCoef = 1f;

        /// <summary>B 강화 출격 보너스(기/초) — 러너가 자산(`enhanceDroneRelease`)에서 넣는다(#234).</summary>
        public static float DroneReleaseBonus;

        /// <summary>러너가 매 프레임 그 로봇의 적재를 싣는다.</summary>
        public static void Publish(MountOwner owner, float now, float capacity, string kinds)
        {
            int i = owner == MountOwner.RobotB ? 1 : 0;
            Now[i] = now;
            Cap[i] = capacity;
            Kinds[i] = kinds ?? "";
        }

        public static float LoadOf(MountOwner owner) => Now[owner == MountOwner.RobotB ? 1 : 0];
        public static float CapacityOf(MountOwner owner) => Cap[owner == MountOwner.RobotB ? 1 : 0];
        public static string KindsOf(MountOwner owner) => Kinds[owner == MountOwner.RobotB ? 1 : 0];

        /// <summary>화면 숫자는 내림(`ChallengeEntryNotice` 와 같은 결 — 39.6 을 40 으로 안 올린다).</summary>
        private static int Floor(float v) => v <= 0f ? 0 : (int)System.Math.Floor(v + 1e-4f);
    }
}
