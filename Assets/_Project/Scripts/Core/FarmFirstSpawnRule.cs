namespace MBI.Core
{
    /// <summary>
    /// **파밍 첫 스폰 띠 ×1.3 · 막 동안 스폰 보류** (사용자 확정 #206 · #207 · 2026-10-19).
    ///
    /// #206 — 파밍 층이 **새로 설 때**(진입 · 파괴 뒤 밀림 · 점프 · ⚠️ 가정: 도전 뒤 복귀도 판이 새로 선다)
    /// 정원을 채우는 **첫 묶음만** 스폰 띠 반지름을 ×1.3. 그 뒤 리필은 지금 띠 그대로 · 도전 층 무관.
    /// ⚠️ 값 1.3 = 사용자 확정 · 표(COMBAT_RULE) 행은 아직 없다 → 상수(가정 표기).
    ///
    /// #207 — S1 T3 · S3 강화 · S6 B 강화 막(#203 정지 셋)이 선 동안은 첫 묶음을 **안 낸다**(리필 시계도 안 돎).
    /// 막이 걷힌 뒤 첫 묶음이 ×1.3 띠로 선다. 막 판단 신호는 앞 프레임 값이라, 판이 선 직후
    /// 한 프레임 새는 것을 막으려고 <see cref="SettleFrames"/> 만큼 기다린 뒤 낸다.
    /// </summary>
    public static class FarmFirstSpawnRule
    {
        /// <summary>첫 묶음 띠 배율(⚠️ 사용자 확정 값 · 표 행 없음).</summary>
        public const float FirstBandScale = 1.3f;

        /// <summary>판이 선 뒤 막 신호가 설 때까지 기다리는 프레임 수(⚠️ 구현 판단).</summary>
        public const int SettleFrames = 2;

        /// <summary>첫 묶음 띠 — 안 · 밖 둘 다 같은 배율.</summary>
        public static (float min, float max) FirstBand(float min, float max) =>
            (min * FirstBandScale, max * FirstBandScale);

        /// <summary>
        /// 이번 프레임에 첫 묶음을 내는가 — 막(정지)이 없고 기다림이 끝났을 때.
        /// <paramref name="framesWaited"/> = 판이 선 뒤 정지 아닌 프레임 수(이번 프레임 포함).
        /// </summary>
        public static bool ShouldSpawnFirst(bool pending, bool paused, int framesWaited) =>
            pending && !paused && framesWaited >= SettleFrames;
    }
}
