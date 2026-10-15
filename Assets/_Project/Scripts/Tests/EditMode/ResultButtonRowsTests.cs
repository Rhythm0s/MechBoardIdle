using System.Collections.Generic;
using MBI.UI;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **결과 단추 줄 바꿈** (2026-10-15 사용자 확정 #136 · QA CID_055).
    /// 아주 작은 창에서 셋을 한 줄에 눌러 셋째가 「음 스테이지」로 잘리던 자리.
    /// </summary>
    public sealed class ResultButtonRowsTests
    {
        private static readonly float[] Three = { 90f, 90f, 130f };   // 다시 도전 · 스테이지로 · 다음 스테이지로

        [Test]
        public void 넉넉하면_한_줄이다()
        {
            List<List<int>> rows = ResultButtonRows.Pack(Three, 6f, 400f);
            Assert.AreEqual(1, rows.Count);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, rows[0]);
        }

        [Test]
        public void 셋이_안_들면_둘_하나로_나뉜다()
        {
            // ⚠️ 이것이 사용자가 본 결함의 시험이다 — 한 줄은 316 이 필요한데 230 뿐.
            //    차례를 지켜 [다시 도전][스테이지로] / [다음 스테이지로] 가 된다(기획서 7장 「하단」).
            List<List<int>> rows = ResultButtonRows.Pack(Three, 6f, 230f);
            Assert.AreEqual(2, rows.Count);
            CollectionAssert.AreEqual(new[] { 0, 1 }, rows[0]);
            CollectionAssert.AreEqual(new[] { 2 }, rows[1]);
        }

        [Test]
        public void 둘도_안_들면_하나씩_쌓인다()
        {
            List<List<int>> rows = ResultButtonRows.Pack(Three, 6f, 150f);
            Assert.AreEqual(3, rows.Count);
            foreach (List<int> r in rows) Assert.AreEqual(1, r.Count);
        }

        [Test]
        public void 혼자서도_넘치는_단추는_혼자_한_줄을_쓴다()
        {
            // 더 줄일 방법이 없다 — 빠뜨리거나 앞 줄에 억지로 끼우면 안 된다.
            List<List<int>> rows = ResultButtonRows.Pack(new[] { 300f, 50f }, 6f, 200f);
            Assert.AreEqual(2, rows.Count);
            CollectionAssert.AreEqual(new[] { 0 }, rows[0]);
            CollectionAssert.AreEqual(new[] { 1 }, rows[1]);
        }

        [Test]
        public void 줄_폭은_단추_합에_틈을_더한_것이다()
        {
            Assert.AreEqual(90f + 6f + 90f, ResultButtonRows.RowWidth(Three, new List<int> { 0, 1 }, 6f), 0.001f);
            Assert.AreEqual(130f, ResultButtonRows.RowWidth(Three, new List<int> { 2 }, 6f), 0.001f);
        }

        [Test]
        public void 단추가_없으면_줄도_없다()
        {
            Assert.AreEqual(0, ResultButtonRows.Pack(new float[0], 6f, 100f).Count);
        }
    }
}
