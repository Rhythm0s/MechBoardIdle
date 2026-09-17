namespace MBI.Core
{
    /// <summary>
    /// 합체가 **공장에 거는 것** (2026-09-17 신설 · `260917_W03` 7-2 #2).
    ///
    /// 전투는 `MBI.Core.Combat` 이 들고 물류는 `MBI.Logistics` 가 도는데, 합체 지속 중에는
    /// **두 보드의 모든 노드 산출량이 ×2** 가 된다. 둘을 직접 물리면 물류가 전투를 알아야
    /// 하므로, `SupplySignals` · `LogisticsOutputBridge` 와 같은 결로 **신호 한 칸**을 둔다.
    ///
    /// ⚠️⚠️ **값을 여기에 적지 않는다.** 배율은 `balance_v4.json` 의 `mergeOutputMult` 이고
    /// `BalanceConfig` 가 미러한다 — 여기 상수를 두면 원천이 둘이 된다(지침 §7 ·
    /// W03 「하드코딩 금지」). 이 칸은 **지금 켜져 있는가**만 든다.
    ///
    /// ⚠️ **끄는 것을 잊으면 합체가 안 끝난다.** 러너가 매 틱 `IsActive` 에서 다시 쓰므로
    /// 합체가 끝난 틱에 저절로 1.0 이 된다 — 켤 때만 쓰고 끌 때 안 쓰면 공장이 영영 두 배다.
    /// </summary>
    public static class MergeSignals
    {
        /// <summary>배율이 없을 때의 값. **1.0 이 기본이고, 합체만 예외다.**</summary>
        public const float None = 1f;

        /// <summary>
        /// 지금 노드 산출에 걸리는 배율. 합체 중이 아니면 <see cref="None"/>.
        /// ⚠️ **벨트는 이것을 안 본다** — 한 줄 12/초는 그대로다(W03 7-2 #2).
        /// </summary>
        public static float OutputMultiplier = None;

        /// <summary>스테이지가 새로 서면 되돌린다 — 앞 판의 합체가 남아 있지 않게.</summary>
        public static void Reset() => OutputMultiplier = None;
    }
}
