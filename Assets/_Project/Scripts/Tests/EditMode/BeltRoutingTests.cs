using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>면 자동연결(§5-4 L2, 순수) — 벨트↔노드·벨트↔벨트·노드↔노드, kind/면 불일치 거부.</summary>
    public sealed class BeltRoutingTests
    {
        private static BoardGrid Grid() => new BoardGrid(8, 8, 1f, Vector2.zero);

        private static NodeDefinition NodeWith(params NodePort[] ports)
        {
            var n = ScriptableObject.CreateInstance<NodeDefinition>();
            n.implemented = true;
            n.ports = new List<NodePort>(ports);
            return n;
        }

        private static bool HasLink(List<BeltLink> links, Vector2Int from, Vector2Int to)
        {
            foreach (BeltLink l in links) if (l.fromCell == from && l.toCell == to) return true;
            return false;
        }

        [Test]
        public void BeltToNode_Links()
        {
            var g = Grid();
            g.TryPlaceBelt(new Vector2Int(0, 0), PortFace.West, PortFace.East, FlowKind.Material, out _);
            g.TryPlace(new Vector2Int(1, 0), NodeWith(new NodePort(PortFace.West, PortIO.Input, FlowKind.Material)), out _);

            var links = BeltRouting.BuildLinks(g);
            Assert.AreEqual(1, links.Count);
            Assert.IsTrue(HasLink(links, new Vector2Int(0, 0), new Vector2Int(1, 0)));
            Assert.AreEqual(FlowKind.Material, links[0].kind);
        }

        [Test]
        public void NodeToBelt_Links()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(0, 0), NodeWith(new NodePort(PortFace.East, PortIO.Output, FlowKind.Material)), out _);
            g.TryPlaceBelt(new Vector2Int(1, 0), PortFace.West, PortFace.East, FlowKind.Material, out _);

            var links = BeltRouting.BuildLinks(g);
            Assert.IsTrue(HasLink(links, new Vector2Int(0, 0), new Vector2Int(1, 0)));
        }

        [Test]
        public void BeltToBelt_Chain()
        {
            var g = Grid();
            g.TryPlaceBelt(new Vector2Int(0, 0), PortFace.West, PortFace.East, FlowKind.Material, out _);
            g.TryPlaceBelt(new Vector2Int(1, 0), PortFace.West, PortFace.East, FlowKind.Material, out _);

            var links = BeltRouting.BuildLinks(g);
            Assert.IsTrue(HasLink(links, new Vector2Int(0, 0), new Vector2Int(1, 0)), "벨트 연쇄");
        }

        [Test]
        public void NodeToNode_DirectAdjacency()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(0, 0), NodeWith(new NodePort(PortFace.East, PortIO.Output, FlowKind.Ammo)), out _);
            g.TryPlace(new Vector2Int(1, 0), NodeWith(new NodePort(PortFace.West, PortIO.Input, FlowKind.Ammo)), out _);

            var links = BeltRouting.BuildLinks(g);
            Assert.IsTrue(HasLink(links, new Vector2Int(0, 0), new Vector2Int(1, 0)));
        }

        [Test]
        public void KindMismatch_NoLink()
        {
            var g = Grid();
            g.TryPlaceBelt(new Vector2Int(0, 0), PortFace.West, PortFace.East, FlowKind.Material, out _);
            g.TryPlace(new Vector2Int(1, 0), NodeWith(new NodePort(PortFace.West, PortIO.Input, FlowKind.Ammo)), out _);

            Assert.AreEqual(0, BeltRouting.BuildLinks(g).Count, "Material→Ammo 입력 불일치");
        }

        [Test]
        public void FaceMismatch_NoLink()
        {
            var g = Grid();
            g.TryPlaceBelt(new Vector2Int(0, 0), PortFace.West, PortFace.East, FlowKind.Material, out _);
            // 이웃 벨트 입력면이 North(맞닿는 West 아님) → 연결 안 됨.
            g.TryPlaceBelt(new Vector2Int(1, 0), PortFace.North, PortFace.South, FlowKind.Material, out _);

            Assert.IsFalse(HasLink(BeltRouting.BuildLinks(g), new Vector2Int(0, 0), new Vector2Int(1, 0)));
        }
    }
}
