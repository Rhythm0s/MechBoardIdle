using System.Collections.Generic;
using MBI.Data;

namespace MBI.Core
{
    /// <summary>
    /// **상주 파밍 층이 N초마다 무엇을 몇 마리 채우는가** (2026-10-13 사용자 확정 #111·#112).
    ///
    /// 규칙은 한 줄이다 — **종류마다 「있어야 할 수 − 지금 산 수」만큼 채운다.**
    ///
    /// 📌 **정원을 따로 안 센다.** 표(`FARM_COMP_DATA`)의 `Weight` 가 **마릿수**이고
    /// 그 합이 곧 정원이다(명세 · 여섯 판 검산 완료). 정원을 딴 칸에서 또 읽으면
    /// 둘이 어긋나는 날이 온다 — 한 값이 두 곳에 살면 답이 둘이 된다(지침 §7).
    ///
    /// ⚠️ **결정론이다.** 난수가 없다 — 같은 상태면 같은 답이다. 파밍 하네스가 그것을 잰다.
    ///
    /// ⚠️ **넘치면 안 지운다.** 지금 산 것이 있어야 할 수보다 많아도(표가 줄었거나
    /// 도전에서 돌아온 직후) **죽이지 않는다** — 채우는 규칙이지 맞추는 규칙이 아니다.
    /// </summary>
    public static class FarmRefillRule
    {
        /// <summary>한 종류를 몇 마리 채울까.</summary>
        public struct Refill
        {
            /// <summary>그 종류의 표 줄(체력·방어까지 들고 있다).</summary>
            public StageComposition entry;

            /// <summary>이번에 넣을 수.</summary>
            public int count;
        }

        /// <summary>
        /// **쓸 수 있는 줄인가** — 이름이 있고 수가 1 이상이라야 센다.
        ///
        /// ⚠️⚠️ **세는 쪽과 채우는 쪽이 같은 자를 써야 한다**(2026-10-13 · 시험이 잡았다).
        /// 처음에는 <see cref="CapacityOf"/> 가 이름을 안 보고 수만 더했는데, 그러면
        /// **이름 없는 줄이 정원에는 들고 보충에는 안 들어** 정원이 영영 안 찬다 —
        /// 「정원 17 인데 15 마리만 서는」 판이 된다. 거르는 자리를 하나로 둔다(지침 §7).
        /// </summary>
        private static bool Usable(StageComposition e) =>
            e.count > 0 && !string.IsNullOrEmpty(e.enemyKey);

        /// <summary>표가 말하는 정원 — 쓸 수 있는 줄의 `Weight` 합이다.</summary>
        public static int CapacityOf(IReadOnlyList<StageComposition> roster)
        {
            if (roster == null) return 0;
            int sum = 0;
            for (int i = 0; i < roster.Count; i++)
                if (Usable(roster[i])) sum += roster[i].count;
            return sum;
        }

        /// <summary>
        /// 이번 보충에 넣을 것들.
        /// </summary>
        /// <param name="roster">그 판의 파밍 구성(표 그대로).</param>
        /// <param name="aliveByKind">지금 살아 있는 수 — 종류 이름 → 수. 비면 전부 0 으로 본다.</param>
        /// <param name="into">결과를 담을 곳. 비우고 채운다.</param>
        public static void Plan(IReadOnlyList<StageComposition> roster,
            IReadOnlyDictionary<string, int> aliveByKind, List<Refill> into)
        {
            if (into == null) return;
            into.Clear();
            if (roster == null) return;

            for (int i = 0; i < roster.Count; i++)
            {
                StageComposition e = roster[i];
                if (!Usable(e)) continue;

                int alive = 0;
                if (aliveByKind != null) aliveByKind.TryGetValue(e.enemyKey, out alive);

                int need = e.count - alive;
                if (need <= 0) continue;        // 넘치면 안 지운다

                into.Add(new Refill { entry = e, count = need });
            }
        }

        /// <summary>이번 보충에 넣을 **마릿수 합** — 하네스·진단이 읽는다.</summary>
        public static int TotalOf(IReadOnlyList<Refill> plan)
        {
            if (plan == null) return 0;
            int sum = 0;
            for (int i = 0; i < plan.Count; i++) sum += plan[i].count;
            return sum;
        }
    }
}
