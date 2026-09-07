using MBI.Core.Anim;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 애니메이션 길이 규칙을 고정한다 (`260907_W01` 4장).
    ///
    /// <b>벡터는 W01 4-5 표 그대로다.</b> 설계가 넷을 손으로 세어 표에 적었고, 그 값이
    /// 코드에서도 나오는지를 여기서 본다 — 배분 칸 번호까지 같아야 한다. 표와 코드가
    /// 갈리면 「칸 배분은 계산 결과다. 손으로 적지 않는다」(4-6)가 무너진다.
    /// </summary>
    public sealed class AnimScheduleTests
    {
        // ---- 4-5 표 넷 ----

        /// <summary>대기 5장 왕복 → 기본 8칸 · 1.00초 = 16칸 · 전 칸 ×2.</summary>
        [Test]
        public void Idle_FiveFrames_PingPong_OneSecond()
        {
            AnimSchedule s = AnimSchedule.Build(5, 1.00f, pingPong: true);

            Assert.AreEqual(8, s.BaseCells, "다섯 장 왕복은 여덟 칸 — 끝을 한 번만 쓴다");
            Assert.AreEqual(16, s.NeededCells, "1.00초 × 16");
            Assert.AreEqual(16, s.Cells.Length);
            Assert.AreEqual(1.00f, s.ActualSeconds, 0.0001f, "반올림이 없다");
            Assert.IsFalse(s.HasWarning, s.Warning);

            // 전 칸 ×2 — 왕복 목록 0·1·2·3·4·3·2·1 이 두 번씩
            CollectionAssert.AreEqual(
                new[] { 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 3, 3, 2, 2, 1, 1 }, s.Cells);
        }

        /// <summary>대기 1.50초 = 24칸 · 전 칸 ×3. 왕복의 목표 초는 기본 칸의 배수에서만 고른다.</summary>
        [Test]
        public void Idle_OneAndHalfSecond_IsThreeTimesEachCell()
        {
            AnimSchedule s = AnimSchedule.Build(5, 1.50f, pingPong: true);

            Assert.AreEqual(24, s.NeededCells);
            Assert.AreEqual(24, s.Cells.Length);
            Assert.AreEqual(1.50f, s.ActualSeconds, 0.0001f);
            for (int i = 0; i < 24; i += 3)
                Assert.AreEqual(s.Cells[i], s.Cells[i + 2], "같은 칸이 세 번 이어져야 한다");
        }

        /// <summary>이동 6장 → 16칸 · 전 칸 ×2 + 칸 2·3·5·6에 +1. 배분은 계산 결과다.</summary>
        [Test]
        public void Move_SixFrames_OneSecond_SpreadsFourExtras()
        {
            AnimSchedule s = AnimSchedule.Build(6, 1.00f, pingPong: false);

            Assert.AreEqual(6, s.BaseCells);
            Assert.AreEqual(16, s.NeededCells);
            Assert.AreEqual(16, s.Cells.Length);
            Assert.AreEqual(1.00f, s.ActualSeconds, 0.0001f);

            CollectionAssert.AreEqual(new[] { 2, 3, 5, 6 }, AnimSchedule.SpreadCells(6, 4),
                "W01 4-5 가 손으로 센 배분 칸과 같아야 한다");

            // 칸 2·3·5·6 이 세 번, 나머지가 두 번
            int[] times = CountPerFrame(s.Cells, 6);
            CollectionAssert.AreEqual(new[] { 2, 3, 3, 2, 3, 3 }, times);
        }

        /// <summary>사망 9장 → 2.00초 = 32칸 · 전 칸 ×3 + 다섯 칸에 +1.</summary>
        [Test]
        public void Death_NineFrames_TwoSeconds_SpreadsFiveExtras()
        {
            AnimSchedule s = AnimSchedule.Build(9, 2.00f, pingPong: false);

            Assert.AreEqual(9, s.BaseCells);
            Assert.AreEqual(32, s.NeededCells);
            Assert.AreEqual(32, s.Cells.Length);
            Assert.AreEqual(2.00f, s.ActualSeconds, 0.0001f);
            Assert.AreEqual(5, AnimSchedule.SpreadCells(9, 5).Length, "나머지가 다섯이다");
        }

        /// <summary>태그 전환 9장 → 0.75초 = 12칸 · 칸 3·6·9에 +1.</summary>
        [Test]
        public void TagIn_NineFrames_ThreeQuarterSecond_ExtrasOnThreeSixNine()
        {
            AnimSchedule s = AnimSchedule.Build(9, 0.75f, pingPong: false);

            Assert.AreEqual(12, s.NeededCells);
            Assert.AreEqual(12, s.Cells.Length);
            Assert.AreEqual(0.75f, s.ActualSeconds, 0.0001f);
            CollectionAssert.AreEqual(new[] { 3, 6, 9 }, AnimSchedule.SpreadCells(9, 3),
                "W01 4-5 가 적은 칸 3·6·9");
        }

        // ---- 머무름 칸 ----

        /// <summary>
        /// 머무름 칸이 지정돼 있으면 거기부터 준다 — 「발이 닿는 프레임을 한 칸 더 머무르게 한다」.
        /// 지정이 나머지보다 적으면 남은 것은 고르게 뿌린다.
        /// </summary>
        [Test]
        public void DwellCells_TakeExtrasFirst()
        {
            AnimSchedule s = AnimSchedule.Build(6, 1.00f, pingPong: false, dwellCells: new[] { 1, 4 });

            int[] times = CountPerFrame(s.Cells, 6);
            Assert.AreEqual(3, times[0], "지정한 칸 1이 한 칸 더 받는다");
            Assert.AreEqual(3, times[3], "지정한 칸 4가 한 칸 더 받는다");
            Assert.AreEqual(16, s.Cells.Length, "총 칸수는 그대로다");
        }

        /// <summary>왕복 벌은 나머지를 쓰지 않는다 — 머무름 칸을 줘도 무시한다.</summary>
        [Test]
        public void PingPong_IgnoresDwellCells()
        {
            AnimSchedule s = AnimSchedule.Build(5, 1.00f, pingPong: true, dwellCells: new[] { 1, 2, 3 });

            int[] times = CountPerFrame(s.Cells, 5);
            Assert.AreEqual(2, times[0], "양 끝은 왕복 목록에 한 번뿐이라 ×2");
            Assert.AreEqual(4, times[1], "가운데 그림은 두 칸이 가리키므로 ×2가 넷");
        }

        // ---- 삭제 ----

        /// <summary>
        /// 남을 때는 짝수 칸을 지우되 <b>첫 칸과 마지막 칸은 남긴다</b>.
        /// 9칸을 6칸으로 줄이면 셋을 지우고, 지운 그림 번호가 보고에 남는다.
        /// </summary>
        [Test]
        public void Delete_KeepsFirstAndLast_AndReportsUnusedFrames()
        {
            AnimSchedule s = AnimSchedule.Build(9, 6f / 16f, pingPong: false);

            Assert.AreEqual(6, s.NeededCells);
            Assert.AreEqual(6, s.Cells.Length);
            Assert.AreEqual(3, s.DeletedCells);
            Assert.AreEqual(0, s.Cells[0], "첫 칸은 남는다");
            Assert.AreEqual(8, s.Cells[s.Cells.Length - 1], "마지막 칸은 남는다");
            Assert.AreEqual(3, s.UnusedFrames.Length, "화면에 안 나오는 그림을 적는다");
            foreach (int f in s.UnusedFrames)
                Assert.IsTrue(f % 2 == 1, "지운 것은 칸 번호가 짝수인 자리(0부터 세면 홀수 그림)다");
        }

        /// <summary>
        /// 후보가 모자라면 홀수 칸까지 지우지 않고 <b>멈추고 보고한다</b>(4-4 3).
        /// 5칸을 2칸으로 줄이려면 셋을 지워야 하는데 후보는 칸 2·4 둘뿐이다.
        /// </summary>
        [Test]
        public void Delete_TooFewCandidates_StopsAndWarns()
        {
            AnimSchedule s = AnimSchedule.Build(5, 2f / 16f, pingPong: false);

            Assert.IsTrue(s.HasWarning, "보고가 있어야 한다");
            Assert.AreEqual(5, s.Cells.Length, "지우지 못했으면 기본 칸을 그대로 돌려준다");
            Assert.AreEqual(0, s.DeletedCells);
        }

        /// <summary>왕복 벌은 지우지 않는다 — 한쪽만 지우면 올라갈 때와 내려올 때가 달라진다.</summary>
        [Test]
        public void PingPong_IsNeverShortened()
        {
            AnimSchedule s = AnimSchedule.Build(5, 4f / 16f, pingPong: true);

            Assert.IsTrue(s.HasWarning);
            Assert.AreEqual(8, s.Cells.Length, "여덟 칸 그대로다");
        }

        // ---- 실제 초 되돌리기 ----

        /// <summary>반올림이 생기면 실제 초가 목표 초와 다르다 — 그 값을 되돌려 적는다(4-2 7).</summary>
        [Test]
        public void ActualSeconds_ReportsRounding()
        {
            AnimSchedule s = AnimSchedule.Build(6, 0.70f, pingPong: false);

            Assert.AreEqual(11, s.NeededCells, "0.70 × 16 = 11.2 → 11칸");
            Assert.AreEqual(11f / 16f, s.ActualSeconds, 0.0001f, "실제는 0.6875초다");
        }

        // ---- 태그 진입 ----

        /// <summary>
        /// 태그 진입 이동은 <b>클립보다 먼저 끝난다</b>(`260907_W02` 2-3).
        /// 값이 아니라 순서가 확정된 것이므로 여기서 보는 것도 부등호다 —
        /// 0보다 크고 클립 초보다 작으면 된다. 꼬리 칸이 셋이면 0.75 − 3/16 = 0.5625초다.
        /// </summary>
        [Test]
        public void TagEntry_EndsBeforeClip_AndAfterZero()
        {
            float seconds = AnimSchedule.TagEntrySeconds(0.75f, 3, 1f / 16f);

            Assert.Greater(seconds, 0f, "이동이 아예 없어지면 순간이동이 된다");
            Assert.Less(seconds, 0.75f, "클립보다 먼저 끝나야 반동과 수렴이 제자리에서 보인다");
            Assert.AreEqual(0.5625f, seconds, 0.0001f, "0.75 − 3칸");
        }

        /// <summary>꼬리가 클립보다 길어도 이동이 0초가 되지는 않는다 — 한 칸은 남긴다.</summary>
        [Test]
        public void TagEntry_TrailLongerThanClip_KeepsOneCell()
        {
            float seconds = AnimSchedule.TagEntrySeconds(0.125f, 9, 1f / 16f);

            Assert.AreEqual(1f / 16f, seconds, 0.0001f);
        }

        private static int[] CountPerFrame(int[] cells, int frameCount)
        {
            var times = new int[frameCount];
            foreach (int c in cells) times[c]++;
            return times;
        }
    }
}
