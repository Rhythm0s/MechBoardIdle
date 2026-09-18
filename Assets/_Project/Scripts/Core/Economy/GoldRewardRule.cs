namespace MBI.Core
{
    /// <summary>
    /// 처치 → 골드 환산 (2026-09-18 사용자 확정 · 「몬스터 20처치마다 5골드」).
    ///
    /// ⚠️ <see cref="KillRewardRule"/> 와 **다른 재화다.** 고철은 매 처치마다 나오고
    /// 골드는 **스무 처치를 모아야** 한 번 나온다 — 그래서 규칙이 하나 더 필요하다.
    /// 둘을 한 함수에 묶으면 「마리당」과 「몇 마리마다」가 한 자리에서 섞인다.
    ///
    /// ⚠️⚠️ **남은 처치 수를 부른 쪽이 든다**(<c>carry</c>). 함수 안에 두면 판이 둘일 때
    /// 계산이 섞이고, 저장에도 못 싣는다 — 19 마리를 잡고 끈 사람은 켤 때 19 에서
    /// 이어져야 한다. 값이 사는 곳은 저장 한 곳이다(지침 §7).
    ///
    /// ⚠️ **활용처는 아직 없다**(2026-09-18) — 지갑에 쌓이기만 한다. 쓸 데가 정해지면
    /// 그것은 설계 몫이고 이 규칙은 안 바뀐다.
    /// </summary>
    public static class GoldRewardRule
    {
        // ⚠️ **수를 안 든다.** 「20 마리마다 5 골드」는 `EconomyConfig` 한 자산에만 산다 —
        //    여기 상수로도 적어 두면 자산을 고쳤을 때 답이 둘이 된다(지침 §7).

        /// <summary>
        /// 이번에 줄 골드. <paramref name="carry"/> 는 **아직 골드가 안 된 처치 수**이며
        /// 이 함수가 깎아 돌려준다.
        ///
        /// ⚠️ **나머지를 버리지 않는다** — 한 틱에 마흔이 죽으면 10골드이고, 남는 0 이 carry 다.
        /// 버리면 많이 잡을수록 손해가 되어 규칙이 뒤집힌다.
        /// </summary>
        public static int Award(ref int carry, int kills, int killsPerAward, int goldPerAward)
        {
            if (kills <= 0 || killsPerAward <= 0 || goldPerAward <= 0) return 0;
            if (carry < 0) carry = 0;

            carry += kills;
            int awards = carry / killsPerAward;
            carry -= awards * killsPerAward;
            return awards * goldPerAward;
        }

        /// <summary>다음 골드까지 남은 처치 수. 화면이 읽는다.</summary>
        public static int UntilNext(int carry, int killsPerAward)
        {
            if (killsPerAward <= 0) return 0;
            if (carry < 0) carry = 0;
            return killsPerAward - (carry % killsPerAward);
        }
    }
}
