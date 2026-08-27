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
    }
}
