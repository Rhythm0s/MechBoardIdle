using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 물류 보드 격자 계산 증명(§5-3, §7 손계산 습관). 씬·MonoBehaviour 없이 BoardGrid만으로 검증:
    /// 월드↔셀 round-trip, 손계산 셀 중심, 스냅, 경계, 점유/겹침 방지.
    /// </summary>
    public sealed class BoardGridTests
    {
        private const float Delta = 0.0001f;

        private static NodeDefinition MakeDef(string name)
        {
            var def = ScriptableObject.CreateInstance<NodeDefinition>();
            def.displayName = name;
            def.implemented = true;
            return def;
        }

        // ---- (1) 월드→셀→월드 round-trip: 셀 중심은 정확히 자기 셀로 복귀 ----
        [Test]
        public void WorldToCell_CellToWorld_RoundTrip()
        {
            foreach (float cellSize in new[] { 1f, 0.5f, 2f })
            {
                var origin = new Vector2(-3.25f, 1.75f); // 임의의 비정렬 원점
                var grid = new BoardGrid(8, 8, cellSize, origin);

                for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++)
                {
                    var cell = new Vector2Int(x, y);
                    Vector2Int back = grid.WorldToCell(grid.CellToWorld(cell));
                    Assert.AreEqual(cell, back, $"cellSize {cellSize}: 셀 {cell} round-trip");
                }
                TestContext.WriteLine($"[재현] cellSize {cellSize}: 64셀 전부 round-trip 일치.");
            }
        }

        // ---- (2) 셀 중심 손계산: origin + (cell + 0.5) * cellSize ----
        [Test]
        public void CellCenter_MatchesHandCalc()
        {
            // 8×8·cellSize 1·중앙 정렬 origin = (-4,-4). 셀(2,3) 중심 = (-4+2.5, -4+3.5) = (-1.5, -0.5).
            var grid = new BoardGrid(8, 8, 1f, new Vector2(-4f, -4f));
            Vector2 c = grid.CellToWorld(new Vector2Int(2, 3));
            TestContext.WriteLine($"[재현] 셀(2,3) 중심 = {c} (기대 (-1.5, -0.5)).");
            Assert.AreEqual(-1.5f, c.x, Delta);
            Assert.AreEqual(-0.5f, c.y, Delta);
        }

        // ---- (3) 스냅: 셀 내부 임의 점 → 그 셀 중심 ----
        [Test]
        public void SnapToCell_ReturnsCellCenter()
        {
            var grid = new BoardGrid(8, 8, 1f, new Vector2(-4f, -4f));
            // 셀(5,5) 중심 = (1.5,1.5). 중심에서 (+0.3,-0.2) 벗어난 점도 같은 셀로 스냅.
            Vector2 snapped = grid.SnapToCell(new Vector2(1.8f, 1.3f));
            TestContext.WriteLine($"[재현] (1.8,1.3) 스냅 = {snapped} (기대 (1.5,1.5)).");
            Assert.AreEqual(1.5f, snapped.x, Delta);
            Assert.AreEqual(1.5f, snapped.y, Delta);
        }

        // ---- (4) 경계 넘는 두 점은 인접 셀로 판정 ----
        [Test]
        public void WorldToCell_CrossingBoundary_FlipsCell()
        {
            var grid = new BoardGrid(8, 8, 1f, Vector2.zero); // 셀 경계 x=1.0
            Vector2Int lo = grid.WorldToCell(new Vector2(0.9f, 0.5f)); // 셀 x=0
            Vector2Int hi = grid.WorldToCell(new Vector2(1.1f, 0.5f)); // 셀 x=1
            TestContext.WriteLine($"[재현] x=0.9 → {lo.x}열, x=1.1 → {hi.x}열.");
            Assert.AreEqual(0, lo.x);
            Assert.AreEqual(1, hi.x);
        }

        // ---- (5) 경계 판정: 범위 밖 셀 거부 ----
        [Test]
        public void IsInside_RejectsOutOfBounds()
        {
            var grid = new BoardGrid(8, 8, 1f, Vector2.zero);
            Assert.IsTrue(grid.IsInside(new Vector2Int(0, 0)));
            Assert.IsTrue(grid.IsInside(new Vector2Int(7, 7)));
            Assert.IsFalse(grid.IsInside(new Vector2Int(8, 0)), "열 상한 초과");
            Assert.IsFalse(grid.IsInside(new Vector2Int(0, 8)), "행 상한 초과");
            Assert.IsFalse(grid.IsInside(new Vector2Int(-1, 0)), "음수 열");

            // 격자 밖 월드점 → 계산된 셀은 범위 밖.
            Assert.IsFalse(grid.IsInside(grid.WorldToCell(new Vector2(100f, 100f))));
        }

        // ---- (6) 겹침 방지: 같은 셀 재배치 실패, 점유자 불변 ----
        [Test]
        public void TryPlace_SameCellTwice_SecondFails()
        {
            var grid = new BoardGrid(8, 8, 1f, Vector2.zero);
            var cell = new Vector2Int(3, 3);
            NodeDefinition first = MakeDef("첫노드");
            NodeDefinition second = MakeDef("둘째노드");

            Assert.IsTrue(grid.TryPlace(cell, first, out NodeInstance placed), "최초 배치 성립");
            Assert.NotNull(placed);
            Assert.IsTrue(grid.IsOccupied(cell));

            Assert.IsFalse(grid.TryPlace(cell, second, out NodeInstance blocked), "점유 셀 재배치 거부");
            Assert.IsNull(blocked);
            Assert.AreSame(first, grid.GetAt(cell).Definition, "점유자 불변");
            TestContext.WriteLine("[재현] 셀(3,3) 겹침 배치 차단, 점유자 유지.");
        }

        // ---- (7) 경계 밖/null 배치 거부 · 제거 후 재배치 ----
        [Test]
        public void TryPlace_OutOfBoundsAndNull_Fail_AndRemoveFreesCell()
        {
            var grid = new BoardGrid(8, 8, 1f, Vector2.zero);
            NodeDefinition def = MakeDef("노드");

            Assert.IsFalse(grid.TryPlace(new Vector2Int(99, 99), def, out _), "경계 밖 배치 거부");
            Assert.IsFalse(grid.TryPlace(new Vector2Int(2, 2), null, out _), "null 노드 배치 거부");

            var cell = new Vector2Int(2, 2);
            Assert.IsTrue(grid.TryPlace(cell, def, out _));
            Assert.IsTrue(grid.TryRemove(cell), "제거 성공");
            Assert.IsFalse(grid.IsOccupied(cell), "제거 후 빈 셀");
            Assert.IsFalse(grid.TryRemove(cell), "빈 셀 재제거는 false");
            Assert.IsTrue(grid.TryPlace(cell, def, out _), "제거 후 재배치 가능");
            TestContext.WriteLine("[재현] 경계밖/null 거부 + 제거→재배치 순환 확인.");
        }
    }
}
