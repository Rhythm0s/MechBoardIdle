using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>노드 네트워크 집계(§5-5) — 코어 존재·자원 합산·스텁 제외. 순수 로직.</summary>
    public sealed class LogisticsNetworkTests
    {
        private static BoardGrid Grid() => new BoardGrid(8, 8, 1f, Vector2.zero);

        private static NodeDefinition Node(NodeType type, NodeResourceProfile res, bool implemented = true)
        {
            var n = ScriptableObject.CreateInstance<NodeDefinition>();
            n.implemented = implemented;
            n.type = type;
            n.resources = res;
            return n;
        }

        [Test]
        public void EmptyGrid_ZeroAggregate()
        {
            NetworkAggregate a = LogisticsNetwork.Aggregate(Grid());
            Assert.IsFalse(a.hasCore);
            Assert.AreEqual(0, a.nodeCount);
            Assert.AreEqual(0f, a.powerDraw, 0.001f);
            Assert.AreEqual(0f, a.ammoProduce, 0.001f);
        }

        [Test]
        public void Core_SetsHasCore_AndDraw()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(1, 1), Node(NodeType.Core, new NodeResourceProfile { powerDraw = 66f }), out _);

            NetworkAggregate a = LogisticsNetwork.Aggregate(g);
            Assert.IsTrue(a.hasCore);
            Assert.AreEqual(1, a.nodeCount);
            Assert.AreEqual(66f, a.powerDraw, 0.001f);
        }

        [Test]
        public void SumsAcrossNodes()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(0, 0), Node(NodeType.Core, new NodeResourceProfile { powerDraw = 66f }), out _);
            g.TryPlace(new Vector2Int(1, 0), Node(NodeType.Energy, new NodeResourceProfile { powerSupply = 80f }), out _);
            g.TryPlace(new Vector2Int(2, 0), Node(NodeType.Munitions, new NodeResourceProfile { ammoProduce = 6f }), out _);

            NetworkAggregate a = LogisticsNetwork.Aggregate(g);
            Assert.IsTrue(a.hasCore);
            Assert.AreEqual(3, a.nodeCount);
            Assert.AreEqual(66f, a.powerDraw, 0.001f);
            Assert.AreEqual(80f, a.powerSupply, 0.001f);
            Assert.AreEqual(6f, a.ammoProduce, 0.001f);
        }

        [Test]
        public void StubNode_Excluded()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(3, 3),
                Node(NodeType.Shield, new NodeResourceProfile { powerDraw = 99f }, implemented: false), out _);

            NetworkAggregate a = LogisticsNetwork.Aggregate(g);
            Assert.AreEqual(0, a.nodeCount, "implemented=false 제외");
            Assert.AreEqual(0f, a.powerDraw, 0.001f);
        }
    }
}
