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

        public CurrencyWallet(double scrap = 0d, double enhMaterial = 0d)
        {
            Scrap = scrap < 0d ? 0d : scrap;
            EnhMaterial = enhMaterial < 0d ? 0d : enhMaterial;
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
