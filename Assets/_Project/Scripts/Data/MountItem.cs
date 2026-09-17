namespace MBI.Data
{
    /// <summary>
    /// 마운트 슬롯에 적재되는 품목(조립 시스템 문서 12장「품목과 재고」).
    ///
    /// 슬롯 하나에는 **아이디가 같은 것만** 쌓인다. 같은 품목이 여러 슬롯을 차지할 수 있고,
    /// 슬롯을 먼저 차지하는 것은 **벨트로 먼저 도착한 것**이다 —
    /// 분류기를 안 쓰면 한 탄종이 칸을 다 먹는데, 그것이 의도된 결과다.
    ///
    /// 여기 있는 것은 마운트에 들어가는 품목만이다(품목 9종 중 소비재).
    /// 드론 2종 구분(누적형·광역형)은 사양 미확정이라 아직 하나로 둔다.
    /// </summary>
    public enum MountItem
    {
        None = 0,
        Pierce = 1,
        Standard = 2,   // 표준탄 — 구 표준 자리(번호 보존)
        Explosive = 3,
        Drone = 4,      // 누적형 드론 — 구 이름 그대로다(자산·저장에 박혀 있다)

        /// <summary>
        /// 광역형 드론 (2026-09-16 신설 · 조립 문서 7-3-1 · 사용자 확정).
        ///
        /// ⚠️ **`Drone` 을 「누적형」으로 개명하지 않는다** — 그 이름이 저장·자산·시험에
        /// 박혀 있어 바꾸면 조용히 어긋나는 자리가 생긴다. 뜻만 좁힌다.
        /// </summary>
        DroneAoe = 5,
    }

    /// <summary>AmmoKind ↔ MountItem 변환. 두 축이 겹치는 지점을 한 곳에 모은다.</summary>
    public static class MountItemMap
    {
        public static MountItem From(AmmoKind kind)
        {
            switch (kind)
            {
                case AmmoKind.Pierce: return MountItem.Pierce;
                case AmmoKind.Standard: return MountItem.Standard;
                default: return MountItem.Explosive;
            }
        }

        /// <summary>
        /// 마운트 품목 → **벨트를 흐르는 품목** (2026-09-14 · §72-24 ③).
        ///
        /// 슬롯 칸에 그릴 아이콘을 `BoardArtSet.ItemSprite` 에서 고르려면 이 대응이 필요하다.
        /// **여기서 새로 정하는 것이 없다** — 이미 선 대응을 반대로 읽을 뿐이다.
        /// </summary>
        public static FlowKind ToFlow(MountItem item)
        {
            switch (item)
            {
                case MountItem.Pierce: return FlowKind.PierceAmmo;
                case MountItem.Standard: return FlowKind.StandardAmmo;
                case MountItem.Explosive: return FlowKind.ExplosiveAmmo;
                case MountItem.Drone: return FlowKind.StackDrone;
                case MountItem.DroneAoe: return FlowKind.AoeDrone;
                default: return FlowKind.None;
            }
        }

        /// <summary>
        /// **벨트를 흐르는 품목 → 마운트 품목** (2026-09-16 · 조립 문서 7-3-1).
        ///
        /// ⚠️⚠️ **「고정 포트에 도착한 것은 곧바로 마운트 적재」**가 문서의 규칙인데,
        /// 드론에는 그 길이 없었다 — 도착한 드론은 **아무 데도 안 쌓이고** B 의 재고는
        /// **기초 가공 생산량**이 대신 채우고 있었다(임시 길 · 폐기).
        ///
        /// ⚠️ 탄약이 아닌 것과 드론이 아닌 것(부품·배터리)은 <c>false</c> 다 —
        /// 마운트에 갈 자리가 없다.
        /// </summary>
        public static bool TryMountItemOf(FlowKind flow, out MountItem item)
        {
            switch (flow)
            {
                case FlowKind.PierceAmmo: item = MountItem.Pierce; return true;
                case FlowKind.StandardAmmo: item = MountItem.Standard; return true;
                case FlowKind.ExplosiveAmmo: item = MountItem.Explosive; return true;
                case FlowKind.Ammo: item = MountItem.Pierce; return true; // 구 자산 호환
                case FlowKind.StackDrone: item = MountItem.Drone; return true;
                case FlowKind.AoeDrone: item = MountItem.DroneAoe; return true;
                default: item = MountItem.None; return false;
            }
        }

        /// <summary>드론 품목인가(둘 중 하나).</summary>
        public static bool IsDrone(MountItem item) =>
            item == MountItem.Drone || item == MountItem.DroneAoe;

        /// <summary>탄약 품목인가(드론은 아니다).</summary>
        public static bool IsAmmo(MountItem item) =>
            item == MountItem.Pierce || item == MountItem.Standard || item == MountItem.Explosive;

        public static AmmoKind ToAmmoKind(MountItem item)
        {
            switch (item)
            {
                case MountItem.Pierce: return AmmoKind.Pierce;
                case MountItem.Standard: return AmmoKind.Standard;
                default: return AmmoKind.Explosive;
            }
        }

        /// <summary>
        /// **벨트를 흐르는 품목 → 탄종** (2026-09-05 · `260904_W01` 3-2 품목 개정).
        ///
        /// 마운트에 도착한 것이 전투력으로 얼마인지 세려면 이 대응이 있어야 한다. 종전에는
        /// 출력이 「노드 수 × 라인 스펙」이라 벨트를 흐르는 품목을 볼 일이 없었다.
        ///
        /// **표준탄이 표준 자리다.** `FlowKind.StandardAmmo` 선언에 「구 표준탄 자리」로
        /// 적혀 있는 대응을 그대로 쓴다 — 여기서 새로 정하는 것이 아니다.
        ///
        /// ⚠️ 탄약이 아닌 것은 <see cref="AmmoKind"/>로 옮길 자리가 없다. 드론은 별도 장치이고
        /// 부품·배터리는 마운트에 안 간다 — 그래서 <c>false</c>를 돌려주고, 부르는 쪽이
        /// **세지 않는다.** 기본값으로 폭발탄을 돌려주면 부품이 발당 50으로 세어진다.
        /// </summary>
        public static bool TryAmmoKindOf(FlowKind flow, out AmmoKind kind)
        {
            switch (flow)
            {
                case FlowKind.PierceAmmo:
                    kind = AmmoKind.Pierce; return true;
                case FlowKind.StandardAmmo:
                    kind = AmmoKind.Standard; return true;   // 표준탄이 구 표준탄 자리다
                case FlowKind.ExplosiveAmmo:
                    kind = AmmoKind.Explosive; return true;
                case FlowKind.Ammo:
                    kind = AmmoKind.Pierce; return true;  // 폐기된 구 품목 — 구 자산 호환용
                default:
                    kind = default; return false;
            }
        }
    }
}
