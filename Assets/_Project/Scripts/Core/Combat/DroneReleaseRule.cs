namespace MBI.Core
{
    /// <summary>
    /// **드론 출격(방출 처리량)** — 사용자 확정 #233 · #234(2026-10-20).
    /// 처리량 = `droneRelease`(기/초) + B 마운트 강화면 `enhanceDroneRelease`. 🗑️ B 강화 = 드론 피해 ×1.45.
    /// 러너와 하네스가 같은 문을 지난다.
    /// </summary>
    public static class DroneReleaseRule
    {
        public static float Throughput(float baseRelease, float enhanceBonus, bool enhancedB) =>
            UnityEngine.Mathf.Max(0f, baseRelease) + (enhancedB ? UnityEngine.Mathf.Max(0f, enhanceBonus) : 0f);
    }
}
