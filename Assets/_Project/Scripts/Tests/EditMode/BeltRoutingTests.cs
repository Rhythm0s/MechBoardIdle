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

        // CreateInstance한 SO는 씬 소속이 아니라 자동 해제되지 않는다 → EditMode 누수 경고 방지.
        private readonly List<NodeDefinition> _created = new List<NodeDefinition>();

        [TearDown]
        public void TearDown()
        {
            foreach (NodeDefinition n in _created)
                if (n != null) Object.DestroyImmediate(n);
            _created.Clear();
        }

        private NodeDefinition NodeWith(params NodePort[] ports)
        {
            var n = ScriptableObject.CreateInstance<NodeDefinition>();
            n.implemented = true;
            n.ports = new List<NodePort>(ports);
            _created.Add(n);
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

        // ---- L3 병합기/분류기(§5-4) ----

        [Test]
        public void Merger_MultiIn_SingleOut()
        {
            var g = Grid();
            var merger = new Vector2Int(1, 1);
            // 병합기: 서·남 두 입력면 → 동 출력면.
            g.TryPlaceBeltElement(merger, BeltElementKind.Merger,
                new[] { PortFace.West, PortFace.South }, new[] { PortFace.East }, FlowKind.Material, out _);
            g.TryPlaceBelt(new Vector2Int(0, 1), PortFace.West, PortFace.East, FlowKind.Material, out _);  // 서쪽 유입
            g.TryPlaceBelt(new Vector2Int(1, 0), PortFace.South, PortFace.North, FlowKind.Material, out _); // 남쪽 유입
            g.TryPlace(new Vector2Int(2, 1), NodeWith(new NodePort(PortFace.West, PortIO.Input, FlowKind.Material)), out _);

            var links = BeltRouting.BuildLinks(g);
            Assert.IsTrue(HasLink(links, new Vector2Int(0, 1), merger), "서쪽 벨트 → 병합기");
            Assert.IsTrue(HasLink(links, new Vector2Int(1, 0), merger), "남쪽 벨트 → 병합기");
            Assert.IsTrue(HasLink(links, merger, new Vector2Int(2, 1)), "병합기 → 싱크 노드");
        }

        [Test]
        public void Sorter_SingleIn_MultiOut()
        {
            var g = Grid();
            var sorter = new Vector2Int(1, 1);
            // 분류기: 서 입력면 → 동·북 두 출력면.
            g.TryPlaceBeltElement(sorter, BeltElementKind.Sorter,
                new[] { PortFace.West }, new[] { PortFace.East, PortFace.North }, FlowKind.Material, out _);
            g.TryPlaceBelt(new Vector2Int(0, 1), PortFace.West, PortFace.East, FlowKind.Material, out _); // 유입
            g.TryPlace(new Vector2Int(2, 1), NodeWith(new NodePort(PortFace.West, PortIO.Input, FlowKind.Material)), out _);  // 동쪽 싱크
            g.TryPlace(new Vector2Int(1, 2), NodeWith(new NodePort(PortFace.South, PortIO.Input, FlowKind.Material)), out _); // 북쪽 싱크

            var links = BeltRouting.BuildLinks(g);
            Assert.IsTrue(HasLink(links, new Vector2Int(0, 1), sorter), "유입 벨트 → 분류기");
            Assert.IsTrue(HasLink(links, sorter, new Vector2Int(2, 1)), "분류기 → 동쪽 싱크");
            Assert.IsTrue(HasLink(links, sorter, new Vector2Int(1, 2)), "분류기 → 북쪽 싱크");
        }

        [Test]
        public void Merger_RejectsUnlistedFace()
        {
            var g = Grid();
            var merger = new Vector2Int(1, 1);
            // 입력면은 서·남만 — 동쪽에서 들어오려는 벨트는 거부.
            g.TryPlaceBeltElement(merger, BeltElementKind.Merger,
                new[] { PortFace.West, PortFace.South }, new[] { PortFace.North }, FlowKind.Material, out _);
            g.TryPlaceBelt(new Vector2Int(2, 1), PortFace.East, PortFace.West, FlowKind.Material, out _); // 동쪽→서로 배출, 병합기 East 입력면 없음

            Assert.IsFalse(HasLink(BeltRouting.BuildLinks(g), new Vector2Int(2, 1), merger), "미등록 입력면 거부");
        }

        // ---- ⑤ 벨트 끝단 미연결 경고(§5-4) ----
        // 사양: 양끝 미접촉 → 표시 없음 / 한쪽만 접촉 → 양끝 표시 / 양끝 접촉 → 표시 없음.

        /// <summary>서→동 직선 벨트 2칸을 (1,0),(2,0)에 깐다.</summary>
        private static void TwoBelts(BoardGrid g)
        {
            g.TryPlaceBelt(new Vector2Int(1, 0), PortFace.West, PortFace.East, FlowKind.Material, out _);
            g.TryPlaceBelt(new Vector2Int(2, 0), PortFace.West, PortFace.East, FlowKind.Material, out _);
        }

        [Test]
        public void Chain_BothEndsConnected_NoWarning()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(0, 0), NodeWith(new NodePort(PortFace.East, PortIO.Output, FlowKind.Material)), out _);
            TwoBelts(g);
            g.TryPlace(new Vector2Int(3, 0), NodeWith(new NodePort(PortFace.West, PortIO.Input, FlowKind.Material)), out _);

            List<BeltChain> chains = BeltRouting.BuildChains(g);
            Assert.AreEqual(1, chains.Count);
            Assert.AreEqual(2, chains[0].nodeSides);
            Assert.AreEqual(0, chains[0].openSides);
            Assert.IsFalse(chains[0].partiallyConnected);
            Assert.AreEqual(0, BeltRouting.DanglingWarningCells(g).Count);
        }

        [Test]
        public void Chain_OneEndConnected_WarnsBothEnds()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(0, 0), NodeWith(new NodePort(PortFace.East, PortIO.Output, FlowKind.Material)), out _);
            TwoBelts(g); // 동쪽 끝은 빈칸

            Assert.IsTrue(BeltRouting.BuildChains(g)[0].partiallyConnected);

            List<Vector2Int> warn = BeltRouting.DanglingWarningCells(g);
            Assert.AreEqual(2, warn.Count, "한쪽만 접촉 → 양끝 표시");
            Assert.Contains(new Vector2Int(1, 0), warn);
            Assert.Contains(new Vector2Int(2, 0), warn);
        }

        [Test]
        public void Chain_NoEndConnected_NoWarning()
        {
            var g = Grid();
            TwoBelts(g); // 공중에 뜬 벨트 = 작업 중 → 경고 없음

            Assert.IsFalse(BeltRouting.BuildChains(g)[0].partiallyConnected);
            Assert.AreEqual(0, BeltRouting.DanglingWarningCells(g).Count);
        }

        [Test]
        public void Chain_TouchingNodeWithoutPortMatch_NotConnected()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(0, 0), NodeWith(new NodePort(PortFace.East, PortIO.Output, FlowKind.Material)), out _);
            TwoBelts(g);
            // 닿기만 함: 입력면이 North라 맞닿는 West가 아니다 → 연결 아님.
            g.TryPlace(new Vector2Int(3, 0), NodeWith(new NodePort(PortFace.North, PortIO.Input, FlowKind.Material)), out _);

            BeltChain c = BeltRouting.BuildChains(g)[0];
            Assert.AreEqual(1, c.nodeSides, "서쪽 노드만 접속");
            Assert.AreEqual(1, c.openSides, "동쪽은 닿기만 했을 뿐 미접속");
            Assert.AreEqual(2, BeltRouting.DanglingWarningCells(g).Count);
        }

        [Test]
        public void Chain_Corner_GroupsAsSingleChain()
        {
            var g = Grid();
            g.TryPlaceBelt(new Vector2Int(1, 0), PortFace.West, PortFace.North, FlowKind.Material, out _);
            g.TryPlaceBelt(new Vector2Int(1, 1), PortFace.South, PortFace.East, FlowKind.Material, out _);

            List<BeltChain> chains = BeltRouting.BuildChains(g);
            Assert.AreEqual(1, chains.Count, "코너로 꺾여도 한 체인");
            Assert.AreEqual(2, chains[0].cells.Count);
        }

        [Test]
        public void Sorter_OneOutputDangling_Warns()
        {
            var g = Grid();
            var sorter = new Vector2Int(1, 1);
            g.TryPlaceBeltElement(sorter, BeltElementKind.Sorter,
                new[] { PortFace.West }, new[] { PortFace.East, PortFace.North }, FlowKind.Material, out _);
            g.TryPlace(new Vector2Int(0, 1), NodeWith(new NodePort(PortFace.East, PortIO.Output, FlowKind.Material)), out _);
            g.TryPlace(new Vector2Int(2, 1), NodeWith(new NodePort(PortFace.West, PortIO.Input, FlowKind.Material)), out _);
            // 북쪽 출구는 비어 있음 → 면 단위 판정이라 한 출구만 비어도 잡힌다.

            BeltChain c = BeltRouting.BuildChains(g)[0];
            Assert.AreEqual(2, c.nodeSides);
            Assert.AreEqual(1, c.openSides);
            Assert.IsTrue(c.partiallyConnected);
        }
    }
}
