using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// **태그 적재 링 분모**(사용자 확정 #251 · 2026-10-20) — 「모든 마운트가 다 찼을 때」 기준 · 로봇마다 고정(달라도 됨).
    /// A = 슬롯 × 탄 스택(4 × 10 = 40) = <see cref="MountLoad.FullCapacity"/> · B = 슬롯 × **드론** 스택(8 × 5 = 40).
    /// ⚠️ B 에 FullCapacity 를 쓰면 빈 슬롯을 「가장 큰 스택」(탄 10)으로 세어 드론이 들어올수록 분모가 80 → 40 으로 줄어든다(구현 판단).
    /// 🗑️ 종전 분모 <see cref="MountLoad.Capacity"/>(차 있는 슬롯만) — 새 슬롯이 열릴 때마다 링이 톱니로 떨어졌다(10-20 사용자 육안).
    /// 만충(태그 스킬 조건 <see cref="MountLoad.IsFull"/>)은 그대로다.
    /// </summary>
    public static class TagRingRule
    {
        public static float Denominator(MountLoad m, bool droneMount)
        {
            if (m == null) return 0f;
            return droneMount ? m.SlotCount * m.StackLimitOf(MountItem.Drone) : m.FullCapacity;
        }

        public static float Ratio(MountLoad m, bool droneMount)
        {
            float d = Denominator(m, droneMount);
            return d > 0f ? Mathf.Clamp01(m.Total / d) : 0f;
        }
    }
}
