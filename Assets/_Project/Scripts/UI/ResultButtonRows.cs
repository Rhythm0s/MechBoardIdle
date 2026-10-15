using System.Collections.Generic;

namespace MBI.UI
{
    /// <summary>
    /// **결과 화면 단추를 몇 줄에 어떻게 나눌까** (2026-10-15 사용자 확정 #136 · QA CID_055).
    ///
    /// 결함: 아주 작은 창(≈586×410 가로 · 캔버스 가로 ≈230 px)에서 단추 셋을 한 줄에 눌러
    /// **셋째가 「음 스테이지」로 잘렸다.** 글자는 사다리 바닥 16 px(= 12 pt) 밑으로 안 내려가고
    /// (#125 「글자는 전부 보인다 · 말줄임은 숫자만」), 상자는 화면 폭에서 깎인다 —
    /// **한 줄이 안 들면 줄을 바꿀 수밖에 없다.**
    ///
    /// 📌 **차례를 지킨다** — 앞에서부터 들어가는 만큼 한 줄에 놓고, 넘치면 다음 줄로 내린다.
    ///    통과 셋이면 [다시 도전][스테이지로] / [다음 스테이지로] 가 된다(기획서 7장 「하단
    ///    [다음 스테이지로]」와 맞는다). 그래도 둘이 안 들면 한 줄에 하나씩 쌓인다.
    /// ⚠️ **한 줄의 단추는 폭이 같다**(그릴 때 줄 폭을 똑같이 나눈다) — 그러니 한 줄이 요구하는
    ///    폭은 **「그 줄에서 가장 긴 단추 × 개수 + 틈」**이다(2026-10-15 #136 재수정). 처음엔 폭의
    ///    **합**으로 셌다가, 긴 단추가 똑같이 나눈 칸에 못 들어 586×410 에서 또 잘렸다.
    /// ⚠️ 한 단추가 혼자서도 폭을 넘으면 그 단추는 **혼자 한 줄**을 쓴다 — 더 줄일 방법이 없다
    ///    (그때는 상자가 화면 폭까지 커지는 것으로 끝이다).
    /// </summary>
    public static class ResultButtonRows
    {
        /// <param name="widths">단추마다 바닥 크기에서 요구하는 폭(여백 포함 · `CalcSize`).</param>
        /// <param name="gap">한 줄 안 단추 사이 틈.</param>
        /// <param name="available">한 줄이 쓸 수 있는 폭(상자 안쪽).</param>
        /// <returns>줄마다 단추 번호 목록. 비어 있으면 빈 목록.</returns>
        public static List<List<int>> Pack(IReadOnlyList<float> widths, float gap, float available)
        {
            var rows = new List<List<int>>();
            if (widths == null || widths.Count == 0) return rows;

            var row = new List<int>();
            float rowMax = 0f;
            for (int i = 0; i < widths.Count; i++)
            {
                float w = widths[i];
                float max = w > rowMax ? w : rowMax;
                float need = max * (row.Count + 1) + gap * row.Count;   // 가장 긴 폭 × 개수 + 틈
                if (row.Count > 0 && need > available)
                {
                    rows.Add(row);
                    row = new List<int>();
                    max = w;
                }
                row.Add(i);
                rowMax = max;
            }
            rows.Add(row);
            return rows;
        }

        /// <summary>한 줄이 요구하는 폭 — **가장 긴 단추 × 개수** + 사이 틈(단추 폭이 같으므로).</summary>
        public static float RowWidth(IReadOnlyList<float> widths, IReadOnlyList<int> row, float gap)
        {
            float max = 0f;
            for (int k = 0; k < row.Count; k++) if (widths[row[k]] > max) max = widths[row[k]];
            return max * row.Count + gap * (row.Count > 0 ? row.Count - 1 : 0);
        }
    }
}
