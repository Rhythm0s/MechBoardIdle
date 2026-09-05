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
        Pierce,
        Split,
        Explosive,
        Drone,
    }

    /// <summary>AmmoKind ↔ MountItem 변환. 두 축이 겹치는 지점을 한 곳에 모은다.</summary>
    public static class MountItemMap
    {
        public static MountItem From(AmmoKind kind)
        {
            switch (kind)
            {
                case AmmoKind.Pierce: return MountItem.Pierce;
                case AmmoKind.Split: return MountItem.Split;
                default: return MountItem.Explosive;
            }
        }

        /// <summary>탄약 품목인가(드론은 아니다).</summary>
        public static bool IsAmmo(MountItem item) =>
            item == MountItem.Pierce || item == MountItem.Split || item == MountItem.Explosive;

        public static AmmoKind ToAmmoKind(MountItem item)
        {
            switch (item)
            {
                case MountItem.Pierce: return AmmoKind.Pierce;
                case MountItem.Split: return AmmoKind.Split;
                default: return AmmoKind.Explosive;
            }
        }

        /// <summary>
        /// **벨트를 흐르는 품목 → 탄종** (2026-09-05 · `260904_W01` 3-2 품목 개정).
        ///
        /// 마운트에 도착한 것이 전투력으로 얼마인지 세려면 이 대응이 있어야 한다. 종전에는
        /// 출력이 「노드 수 × 라인 스펙」이라 벨트를 흐르는 품목을 볼 일이 없었다.
        ///
        /// **표준탄이 분열 자리다.** `FlowKind.StandardAmmo` 선언에 「구 분열탄 자리」로
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
                    kind = AmmoKind.Split; return true;   // 표준탄이 구 분열탄 자리다
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
