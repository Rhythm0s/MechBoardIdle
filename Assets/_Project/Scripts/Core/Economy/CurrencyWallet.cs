namespace MBI.Core
{
    /// <summary>
    /// 재화 지갑(§5-7). 고철과 강화재료는 **출처도 용도도 다른 별개 재화**다(E2 분리):
    /// 고철은 킬 기반(파밍·도전·오프라인), 강화재료는 클리어 한정.
    ///
    /// ⚠️ 그래서 <c>Add(종류, 양)</c> 같은 통합 API도, 둘을 바꾸는 변환 메서드도 두지 않는다.
    /// 타입 수준에서 갈라 놓아야 "고철이 늘었으니 강화도 되겠지" 같은 혼동이 코드로 새지 않는다
    /// (§7 [2026-07-11] 재화 혼동 부정피드백 오류 — 같은 실수를 구조로 막는다).
    ///
    /// 특히 오프라인 보상은 고철만 지급한다 — 꺼둔 시간으로 S4 강화 벽을 우회할 수 없어야
    /// 닫힌 곡선(Σ S1~S3 보상 = s4Cost)이 유지된다.
    /// </summary>
    public sealed class CurrencyWallet
    {
        /// <summary>고철 — 킬 기반 수입(파밍·도전·오프라인).</summary>
        public double Scrap { get; private set; }

        /// <summary>강화재료 — 스테이지 최초 클리어 보상 한정.</summary>
        public double EnhMaterial { get; private set; }

        /// <summary>
        /// 골드 — **처치 스무 마리마다 5**(2026-09-18 사용자 확정 · <see cref="GoldRewardRule"/>).
        ///
        /// ⚠️ **활용처가 아직 없다.** 쌓이기만 하며 쓰는 곳은 설계가 정한다 —
        /// 그래도 지갑에 들이는 까닭은 **적립 규칙이 생겼기 때문**이고, 규칙이 있는데
        /// 담을 데가 없으면 화면이 제 수를 따로 세게 된다(지침 §7 이 막는 자리).
        ///
        /// ⚠️ 고철·강화재료와 **서로 안 바뀐다** — 이 클래스가 변환 메서드를 안 두는 이유 그대로다.
        /// </summary>
        public double Gold { get; private set; }

        public CurrencyWallet(double scrap = 0d, double enhMaterial = 0d, double gold = 0d)
        {
            Scrap = scrap < 0d ? 0d : scrap;
            EnhMaterial = enhMaterial < 0d ? 0d : enhMaterial;
            Gold = gold < 0d ? 0d : gold;
        }

        /// <summary>고철 적립. 음수는 무시한다 — 차감은 TrySpend로만(경로를 하나로 묶는다).</summary>
        public void AddScrap(double amount)
        {
            if (amount > 0d) Scrap += amount;
        }

        public void AddEnhMaterial(double amount)
        {
            if (amount > 0d) EnhMaterial += amount;
        }

        /// <summary>골드 적립. 고철과 같은 규약이다 — 음수는 무시하고 차감은 TrySpend 로만.</summary>
        public void AddGold(double amount)
        {
            if (amount > 0d) Gold += amount;
        }

        public bool TrySpendGold(double amount)
        {
            if (amount <= 0d || Gold < amount) return false;
            Gold -= amount;
            return true;
        }

        /// <summary>고철 지출. 모자라면 아무것도 깎지 않고 false.</summary>
        public bool TrySpendScrap(double amount)
        {
            if (amount <= 0d || Scrap < amount) return false;
            Scrap -= amount;
            return true;
        }

        public bool TrySpendEnhMaterial(double amount)
        {
            if (amount <= 0d || EnhMaterial < amount) return false;
            EnhMaterial -= amount;
            return true;
        }
    }
}
