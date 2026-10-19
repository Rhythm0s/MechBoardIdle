namespace MBI.Core
{
    /// <summary>
    /// **고철 소비 · 로봇 B 해금 값 — 규칙과 신호** (2026-10-19 사용자 확정 #172 · #173 · #174).
    ///
    /// 값의 원천은 `EconomyConfig`(ECONOMY 표 5~10행) 하나다. 방치 런타임이 매 프레임 여기에 **게시**하고,
    /// 보드(설치 · 모듈) · 설정 판 · 마운트 팝오버 · 해금 단추가 **읽는다** — 화면이 자산을 따로 들면
    /// 같은 값이 두 곳에 산다(지침 §7).
    ///
    /// ⚠️ **지갑을 만지는 곳은 여전히 방치 런타임 하나다.** 보드는 「쓰겠다」를 **장부(대기 지출)**에 적고,
    ///    런타임이 그 프레임에 가져가 지갑에서 뺀다. 한 프레임에 여러 번 놓아도 대기 지출을 빼고 재므로
    ///    잔액을 넘겨 쓰지 않는다.
    ///
    /// ⚠️ 값이 0 이면(런타임이 없는 시험 · 하네스) **공짜**다 — 비용 없는 판이 종전 거동이다.
    /// </summary>
    public static class BuildCost
    {
        /// <summary>노드 설치 고철(종류 무관 · #172). 벨트 · 병합기 · 분류기는 0.</summary>
        public static double NodeScrap;

        /// <summary>모듈 장착 고철(#172).</summary>
        public static double ModuleScrap;

        /// <summary>마운트 강화 고철(#172) — 재료와 **둘 다** 있어야 한다.</summary>
        public static double EnhanceScrap;

        /// <summary>로봇 B 해금 골드(#173).</summary>
        public static double RobotBGold;

        private static double _pendingScrap;

        /// <summary>
        /// 이 값을 낼 수 있는가 — 잔액에서 **아직 안 빠진 대기 지출**을 뺀 몫으로 잰다. 비용 0 은 늘 참.
        /// </summary>
        public static bool CanAfford(double wallet, double pending, double cost) =>
            cost <= 0d || wallet - pending >= cost - 1e-9;

        /// <summary>지금 고철로 이 값을 낼 수 있는가(대기 지출 포함).</summary>
        public static bool CanAffordScrap(double cost) =>
            CanAfford(IdleSignals.WalletScrap, _pendingScrap, cost);

        /// <summary>
        /// 고철을 쓰겠다 — 낼 수 있으면 대기 지출에 적고 참. **놓기 · 붙이기가 성공한 뒤에** 부른다
        /// (실패한 손에 값을 물리지 않는다). 비용 0 이면 아무것도 안 적고 참.
        /// </summary>
        public static bool TryReserveScrap(double cost)
        {
            if (cost <= 0d) return true;
            if (!CanAffordScrap(cost)) return false;
            _pendingScrap += cost;
            return true;
        }

        /// <summary>대기 지출을 가져가며 비운다 — 방치 런타임만 부른다.</summary>
        public static double DrainPendingScrap()
        {
            double v = _pendingScrap;
            _pendingScrap = 0d;
            return v;
        }

        /// <summary>팔레트 단추 비용 글자 — 「고철 20」. ⚠️ 가정.</summary>
        public static string ScrapLabel(double cost) => $"고철 {Floor(cost)}";

        /// <summary>노드 팝오버 비용 줄 — 「설치 비용 고철 20 · 환불 없음」. ⚠️ 가정.</summary>
        public static string NodeInstallLine(double cost) => $"설치 비용 고철 {Floor(cost)} · 환불 없음";

        /// <summary>하단 상태 띠 한 줄 — 노드 하나도 못 놓을 때. ⚠️ 가정.</summary>
        public const string ScrapShortLine = "고철 부족";

        /// <summary>노드 하나도 못 놓는가 — 상태 띠가 읽는다(비용 0 이면 거짓).</summary>
        public static bool ScrapShortForNode(double wallet, double nodeCost) =>
            nodeCost > 0d && wallet < nodeCost - 1e-9;

        /// <summary>처음 상태로 — 시험 · 런타임 시작이 쓴다.</summary>
        public static void Reset()
        {
            NodeScrap = ModuleScrap = EnhanceScrap = RobotBGold = 0d;
            _pendingScrap = 0d;
        }

        private static int Floor(double v) => v <= 0d ? 0 : (int)System.Math.Floor(v + 1e-6);
    }
}
