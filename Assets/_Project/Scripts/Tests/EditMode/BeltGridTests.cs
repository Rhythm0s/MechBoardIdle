using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 벨트 격자 점유(§5-4 L1) — 벨트 설치·해제, 노드와의 배타(겹침 방지). 순수 로직.
    /// </summary>
    public sealed class BeltGridTests
    {
        private static BoardGrid Grid() => new BoardGrid(8, 8, 1f, Vector2.zero);

        private static NodeDefinition Node()
        {
            var n = ScriptableObject.CreateInstance<NodeDefinition>();
            n.implemented = true;
            return n;
        }

        [Test]
        public void PlaceBelt_OnFreeCell_Succeeds()
        {
            var g = Grid();
            var cell = new Vector2Int(2, 3);
            bool ok = g.TryPlaceBelt(cell, PortFace.West, PortFace.East, FlowKind.Material, out BeltInstance belt);

            Assert.IsTrue(ok);
            Assert.IsNotNull(belt);
            Assert.AreSame(belt, g.GetBeltAt(cell));
            Assert.IsTrue(g.HasBelt(cell));
            Assert.IsFalse(g.IsFree(cell));
        }

        [Test]
        public void BeltAndNode_MutuallyExclusive()
        {
            var g = Grid();
            var cell = new Vector2Int(1, 1);

            Assert.IsTrue(g.TryPlace(cell, Node(), out _), "노드 배치");
            Assert.IsFalse(g.TryPlaceBelt(cell, PortFace.West, PortFace.East, FlowKind.Material, out _),
                "노드 셀엔 벨트 불가");

            var cell2 = new Vector2Int(4, 4);
            Assert.IsTrue(g.TryPlaceBelt(cell2, PortFace.West, PortFace.East, FlowKind.Material, out _), "벨트 배치");
            Assert.IsFalse(g.TryPlace(cell2, Node(), out _), "벨트 셀엔 노드 불가");
        }

        [Test]
        public void RemoveBelt_FreesCell()
        {
            var g = Grid();
            var cell = new Vector2Int(5, 5);
            g.TryPlaceBelt(cell, PortFace.South, PortFace.North, FlowKind.Material, out _);

            Assert.IsTrue(g.TryRemoveBelt(cell));
            Assert.IsFalse(g.HasBelt(cell));
            Assert.IsTrue(g.IsFree(cell));
            Assert.IsFalse(g.TryRemoveBelt(cell), "이미 없음");
        }

        [Test]
        public void Belt_OutOfBounds_Fails()
        {
            var g = Grid();
            Assert.IsFalse(g.TryPlaceBelt(new Vector2Int(-1, 0), PortFace.West, PortFace.East, FlowKind.Material, out _));
            Assert.IsFalse(g.TryPlaceBelt(new Vector2Int(8, 8), PortFace.West, PortFace.East, FlowKind.Material, out _));
        }

        [Test]
        public void Straight_And_Corner_Classified()
        {
            var g = Grid();
            g.TryPlaceBelt(new Vector2Int(0, 0), PortFace.West, PortFace.East, FlowKind.Material, out BeltInstance straight);
            g.TryPlaceBelt(new Vector2Int(1, 0), PortFace.West, PortFace.North, FlowKind.Material, out BeltInstance corner);

            Assert.IsTrue(straight.IsStraight, "West→East = 직선");
            Assert.IsFalse(corner.IsStraight, "West→North = 코너");
        }
    }
}
