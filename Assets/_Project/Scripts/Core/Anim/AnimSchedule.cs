using System;
using System.Collections.Generic;

namespace MBI.Core.Anim
{
    /// <summary>
    /// 애니메이션 길이 규칙 (캐릭터 아트 요청 문서 「애니메이션 길이 규칙」 · `260907_W01` 4장).
    ///
    /// <b>말 넷을 가른다.</b>
    ///   그림 — 폴더에 있는 PNG 한 장. 「동작의 크기」의 프레임 수가 이것이다
    ///   칸 — 재생 목록의 한 자리. 같은 그림을 여러 칸이 가리킬 수 있다
    ///   기준 칸 시간 — 한 칸이 화면에 머무는 시간. 1/16초 고정
    ///   필요 칸 — 목표 초 × 16
    ///
    /// <b>왜 초당 프레임을 버렸나.</b> 공통 재생 속도를 두면 길이 = 그림 수 ÷ 속도가 되어
    /// <b>그림 수가 부드러움과 길이를 겸한다.</b> 그러면 무거운 로봇(대기 5장)이 가벼운
    /// 몬스터(대기 7장)보다 빠르게 들썩인다 — 「무거울수록 그림을 적게」가 시간 축에서
    /// 「무거울수록 빠르게」로 뒤집힌다. 지침 §3이 가동률을 셋으로 가른 것과 같은 자리다.
    ///
    /// <b>9프레임 상한은 그림 장수다. 칸수가 아니다.</b> 같은 그림을 두 번 가리켜
    /// 머무르게 하는 것은 새로 그린 그림이 아니므로 상한에 들지 않는다.
    ///
    /// 이 클래스는 순수 계산이다 — <see cref="UnityEngine.MonoBehaviour"/>가 아니고
    /// 유니티 타입을 쓰지 않는다. 그래야 EditMode 테스트가 표(4-5)를 그대로 벡터로 쓴다.
    /// </summary>
    public struct AnimSchedule
    {
        /// <summary>기준 칸 시간의 역수. 1초가 몇 칸인가 — 16이다(4-1).</summary>
        public const int CellsPerSecond = 16;

        /// <summary>칸마다 어느 그림을 보이는가. 값은 0부터 세는 그림 번호다.</summary>
        public int[] Cells;

        /// <summary>실제 초. 반올림이 생기면 목표 초와 다르다 — 그때는 이 값을 문서에 되돌려 적는다(4-2 7).</summary>
        public float ActualSeconds;

        /// <summary>기본 칸 — 복제·삭제 전 칸 목록의 길이.</summary>
        public int BaseCells;

        /// <summary>필요 칸 — 목표 초 × 16을 반올림한 값.</summary>
        public int NeededCells;

        /// <summary>지운 칸 수. 0이 아니면 화면에 한 번도 안 나오는 그림이 생긴다.</summary>
        public int DeletedCells;

        /// <summary>화면에 한 번도 안 나오는 그림 번호(0부터). 삭제가 없으면 빈 배열이다.</summary>
        public int[] UnusedFrames;

        /// <summary>
        /// 규칙을 지킬 수 없을 때의 사유. 비어 있지 않으면 <b>보고 대상</b>이다(4-4).
        /// 지우지 못한 채로도 칸 목록은 돌려준다 — 화면이 멈추는 것보다 낫다.
        /// </summary>
        public string Warning;

        public bool HasWarning => !string.IsNullOrEmpty(Warning);

        /// <summary>
        /// 칸 목록을 만든다. 계산 순서는 4-2 그대로다.
        /// </summary>
        /// <param name="frameCount">그림 장수.</param>
        /// <param name="targetSeconds">목표 초(4-5).</param>
        /// <param name="pingPong">왕복(대기)인가. 왕복은 복제에서 나머지를 쓰지 않고 삭제도 하지 않는다.</param>
        /// <param name="dwellCells">머무름 칸 번호(1부터). 착지처럼 한 칸 더 머무를 자리다. 없으면 null.</param>
        public static AnimSchedule Build(int frameCount, float targetSeconds, bool pingPong, int[] dwellCells = null)
        {
            var r = new AnimSchedule { UnusedFrames = Array.Empty<int>(), Warning = string.Empty };
            if (frameCount <= 0 || targetSeconds <= 0f)
            {
                r.Cells = Array.Empty<int>();
                r.Warning = "그림이 없거나 목표 초가 0 이하다";
                return r;
            }

            int[] baseCells = BaseList(frameCount, pingPong);
            int n = baseCells.Length;
            int needed = (int)Math.Round(targetSeconds * CellsPerSecond, MidpointRounding.AwayFromZero);
            if (needed < 1) needed = 1;

            r.BaseCells = n;
            r.NeededCells = needed;

            if (needed > n) r.Cells = Duplicate(baseCells, needed, pingPong, dwellCells);
            else if (needed < n) r.Cells = Delete(baseCells, needed, pingPong, ref r);
            else r.Cells = baseCells;

            r.ActualSeconds = r.Cells.Length / (float)CellsPerSecond;
            if (r.DeletedCells > 0) r.UnusedFrames = MissingFrames(r.Cells, frameCount);
            return r;
        }

        /// <summary>
        /// 칸 목록의 출발점. 단방향은 그림 순서 그대로, 왕복은 1→N→2로 편다 —
        /// <b>끝을 한 번만 쓴다</b>(4-5). 다섯 장이면 여덟 칸이다.
        /// </summary>
        public static int[] BaseList(int frameCount, bool pingPong)
        {
            if (!pingPong || frameCount < 3)
            {
                var plain = new int[Math.Max(frameCount, 0)];
                for (int i = 0; i < plain.Length; i++) plain[i] = i;
                return plain;
            }

            var cells = new int[frameCount * 2 - 2];
            for (int i = 0; i < frameCount; i++) cells[i] = i;
            for (int i = frameCount; i < cells.Length; i++) cells[i] = frameCount * 2 - 2 - i;
            return cells;
        }

        /// <summary>
        /// 앞에서부터 세어 나가다가 「지금까지 준 개수」가 하나 늘어나는 자리에서만 준다 —
        /// <c>(i×r)÷n</c>의 몫이 <c>((i−1)×r)÷n</c>의 몫보다 클 때다(4-3).
        /// 몰리지 않고 저절로 고르게 흩어진다. <b>손으로 고른 숫자를 쓰지 않기 위한 것이다.</b>
        /// </summary>
        /// <returns>한 칸을 더 받는 칸 번호(1부터).</returns>
        public static int[] SpreadCells(int n, int r)
        {
            var picked = new List<int>();
            if (n <= 0 || r <= 0) return picked.ToArray();
            for (int i = 1; i <= n; i++)
                if (i * r / n > (i - 1) * r / n) picked.Add(i);
            return picked.ToArray();
        }

        private static int[] Duplicate(int[] baseCells, int needed, bool pingPong, int[] dwellCells)
        {
            int n = baseCells.Length;
            int q = needed / n;
            int rem = needed % n;

            // 왕복은 균등 배수만 쓴다 — 한쪽만 늘리면 올라갈 때와 내려올 때 속도가 달라져
            // 흔들림이 절뚝인다. 그리고 대기에는 머무를 이유가 있는 칸이 없다(착지가 없다).
            if (pingPong) rem = 0;

            var extra = new int[n + 1]; // 1부터 세는 칸 번호
            int given = 0;

            // 머무름 칸이 지정돼 있으면 거기부터 준다. 지정이 r보다 많으면 앞의 것부터 받는다.
            if (dwellCells != null)
            {
                for (int i = 0; i < dwellCells.Length && given < rem; i++)
                {
                    int cell = dwellCells[i];
                    if (cell < 1 || cell > n || extra[cell] > 0) continue;
                    extra[cell] = 1;
                    given++;
                }
            }

            // 남은 것은 고르게 뿌린다.
            if (given < rem)
            {
                foreach (int cell in SpreadCells(n, rem - given))
                {
                    if (extra[cell] > 0) continue;
                    extra[cell] = 1;
                    given++;
                    if (given >= rem) break;
                }
            }

            var outCells = new List<int>(needed);
            for (int i = 1; i <= n; i++)
            {
                int times = q + extra[i];
                for (int k = 0; k < times; k++) outCells.Add(baseCells[i - 1]);
            }
            return outCells.ToArray();
        }

        private static int[] Delete(int[] baseCells, int needed, bool pingPong, ref AnimSchedule report)
        {
            int n = baseCells.Length;
            int d = n - needed;

            // 왕복 벌은 지우지 않는다 — 한쪽만 지우면 올라갈 때와 내려올 때가 달라진다.
            if (pingPong)
            {
                report.Warning = $"왕복 벌이라 칸을 지우지 않았다 — 기본 {n}칸 · 필요 {needed}칸. " +
                                 "목표 초를 기본 칸의 배수에서 고른다(4-3 왕복 조항)";
                return baseCells;
            }

            // 후보 = 짝수 번호 칸에서 첫 칸과 마지막 칸을 뺀 것. 첫·끝은 번호가 짝수여도 예외 없이 남긴다 —
            // 이동은 마지막 칸이 첫 칸으로 이어져 한 걸음이 닫히고, 사망은 마지막 칸이 「관절이 꺾이며 멈춤」이다.
            var candidates = new List<int>();
            for (int i = 2; i < n; i++)
                if (i % 2 == 0) candidates.Add(i);

            if (candidates.Count < d)
            {
                report.Warning = $"지울 후보가 모자란다 — 필요 {d}칸 · 짝수 후보 {candidates.Count}칸. " +
                                 "홀수 칸까지 지우지 않고 멈춘다(4-4 3). 목표 초를 늘리거나 그림을 줄인다";
                return baseCells;
            }

            var pickIdx = new HashSet<int>();
            foreach (int k in SpreadCells(candidates.Count, d)) pickIdx.Add(candidates[k - 1]);

            var outCells = new List<int>(needed);
            for (int i = 1; i <= n; i++)
                if (!pickIdx.Contains(i)) outCells.Add(baseCells[i - 1]);

            report.DeletedCells = pickIdx.Count;
            return outCells.ToArray();
        }

        /// <summary>화면에 한 번도 안 나오는 그림 번호. 삭제가 났을 때만 부른다.</summary>
        private static int[] MissingFrames(int[] cells, int frameCount)
        {
            var seen = new HashSet<int>(cells);
            var missing = new List<int>();
            for (int i = 0; i < frameCount; i++)
                if (!seen.Contains(i)) missing.Add(i);
            return missing.ToArray();
        }
    }
}
