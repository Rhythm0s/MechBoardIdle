using System.Collections.Generic;

namespace MBI.Core
{
    /// <summary>스테이지 목록의 한 줄 — 무슨 판이고 열렸는가.</summary>
    public struct StageListEntry
    {
        /// <summary>자산의 식별자 그대로(<c>S1</c>~<c>S6</c>). 지어내지 않는다.</summary>
        public string stageId;

        /// <summary>여섯 중 몇 번째인가(1부터).</summary>
        public int number;

        /// <summary>고를 수 있는가.</summary>
        public bool unlocked;

        /// <summary>이미 도전을 통과한 판인가 — 목록에 표를 붙이는 데 쓴다.</summary>
        public bool cleared;
    }

    /// <summary>
    /// **스테이지 목록과 해금** — 순수 규칙 (2026-10-13 사용자 확정 · 순서 5).
    ///
    /// 규칙 한 줄 — **앞 판의 도전을 통과하면 다음 판이 열린다.**
    ///
    /// 📌 **왜 「도전 통과」인가.** 파밍은 끝이 없어서 「깼다」는 순간이 없다. 판이 열리는
    /// 사건은 도전 층에서만 일어나고, 그 사건은 이미 저장에 남아 있다
    /// (<c>clearedStageIds</c> · 최초 클리어 보상 판정이 쓰던 그 장부다).
    ///
    /// ⚠️⚠️ **새 장부를 만들지 않는다**(지침 §7). 「해금된 판 목록」을 따로 저장하면
    /// 같은 사실이 두 곳에 살고, 둘이 어긋나는 날 플레이어는 **깼는데 안 열린 판**을 본다.
    /// 그래서 해금은 **저장하지 않고 매번 통과 기록에서 계산한다.**
    ///
    /// ⚠️ **튜토리얼 전용 판은 목록 밖이다** — 여섯에 안 들고
    /// (<see cref="StageMapName.NumberOf"/> 가 0), 고를 수 있는 판이 아니다
    /// (스테이지 기획서 3장). 번호를 못 읽는 자산은 조용히 건너뛴다.
    ///
    /// ⚠️ **첫 판은 언제나 열려 있다** — 앞 판이 없으므로 조건이 없다.
    /// </summary>
    public static class StageUnlockRule
    {
        /// <summary>
        /// 목록을 짠다 — 번호 순으로, 튜토리얼은 빼고, 열렸는지까지 적어서.
        ///
        /// <paramref name="stageIds"/> 는 자산 목록의 식별자들이고
        /// <paramref name="clearedIds"/> 는 저장의 통과 기록이다.
        /// <paramref name="into"/> 는 **비우고 채운다**(호출자가 재사용한다).
        /// </summary>
        public static void Build(IReadOnlyList<string> stageIds,
                                 ICollection<string> clearedIds,
                                 List<StageListEntry> into)
        {
            if (into == null) return;
            into.Clear();
            if (stageIds == null) return;

            // 번호 → 식별자. 같은 번호가 둘이면 **먼저 온 것을 쓴다**(목록이 이상한 경우).
            var byNumber = new Dictionary<int, string>();
            for (int i = 0; i < stageIds.Count; i++)
            {
                string id = stageIds[i];
                int n = StageMapName.NumberOf(id);
                if (n <= 0) continue;                       // 튜토리얼·못 읽는 자산은 목록 밖
                if (!byNumber.ContainsKey(n)) byNumber[n] = id;
            }

            for (int n = 1; n <= StageMapName.TotalStages; n++)
            {
                if (!byNumber.TryGetValue(n, out string id)) continue;   // 자산이 없으면 안 적는다

                into.Add(new StageListEntry
                {
                    stageId = id,
                    number = n,
                    unlocked = IsUnlocked(n, byNumber, clearedIds),
                    cleared = Cleared(clearedIds, id),
                });
            }
        }

        /// <summary>
        /// 그 판이 열렸는가 — 첫 판은 늘 열리고, 그 뒤는 **앞 판을 통과했는가**다.
        ///
        /// ⚠️ 앞 판 자산이 목록에 없으면 **열어 준다** — 자산이 덜 구워진 동안
        /// 뒷 판 전부가 잠겨 아무것도 못 고르는 쪽이 더 나쁘다(가정 · 설계 역기입 자리).
        /// </summary>
        private static bool IsUnlocked(int number,
                                       Dictionary<int, string> byNumber,
                                       ICollection<string> clearedIds)
        {
            if (number <= 1) return true;
            if (!byNumber.TryGetValue(number - 1, out string prev)) return true;
            return Cleared(clearedIds, prev);
        }

        private static bool Cleared(ICollection<string> clearedIds, string id)
            => clearedIds != null && !string.IsNullOrEmpty(id) && clearedIds.Contains(id);

        /// <summary>
        /// 고를 수 있는 판의 수 — 화면이 「n/6 열림」을 적는 데 쓴다.
        /// </summary>
        public static int UnlockedCount(IReadOnlyList<StageListEntry> list)
        {
            if (list == null) return 0;
            int k = 0;
            for (int i = 0; i < list.Count; i++) if (list[i].unlocked) k++;
            return k;
        }
    }
}
