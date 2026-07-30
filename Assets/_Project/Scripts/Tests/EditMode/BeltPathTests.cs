using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>드래그 경로 → 벨트 세그먼트 배향(§5-4 L1, 순수). 직선/코너/역방향/단일.</summary>
    public sealed class BeltPathTests
    {
        private static List<Vector2Int> Path(params (int x, int y)[] cs)
        {
            var list = new List<Vector2Int>();
            foreach (var c in cs) list.Add(new Vector2Int(c.x, c.y));
            return list;
        }

        [Test]
        public void StraightHorizontal_AllWestToEast()
        {
            var segs = BeltPath.Build(Path((0, 0), (1, 0), (2, 0)));
            Assert.AreEqual(3, segs.Count);
            foreach (BeltSegmentSpec s in segs)
            {
                Assert.AreEqual(PortFace.West, s.inFace);
                Assert.AreEqual(PortFace.East, s.outFace);
            }
        }

        [Test]
        public void Corner_MiddleCell_IsBent()
        {
            // (0,0)→(1,0)→(1,1): 중간 칸은 West 입력 → North 출력(코너).
            var segs = BeltPath.Build(Path((0, 0), (1, 0), (1, 1)));
            Assert.AreEqual(PortFace.West, segs[1].inFace);
            Assert.AreEqual(PortFace.North, segs[1].outFace);
            Assert.AreNotEqual(NodeConnectionRules.Opposite(segs[1].inFace), segs[1].outFace, "코너 = 입출력 비반대");
            // 끝 칸: South 입력(이전 (1,0)) → North 출력.
            Assert.AreEqual(PortFace.South, segs[2].inFace);
            Assert.AreEqual(PortFace.North, segs[2].outFace);
        }

        [Test]
        public void SingleCell_DefaultStraight()
        {
            var segs = BeltPath.Build(Path((3, 3)));
            Assert.AreEqual(1, segs.Count);
            Assert.AreEqual(PortFace.West, segs[0].inFace);
            Assert.AreEqual(PortFace.East, segs[0].outFace);
        }

        [Test]
        public void Reverse_AllEastToWest()
        {
            var segs = BeltPath.Build(Path((2, 0), (1, 0), (0, 0)));
            foreach (BeltSegmentSpec s in segs)
            {
                Assert.AreEqual(PortFace.East, s.inFace);
                Assert.AreEqual(PortFace.West, s.outFace);
            }
        }

        [Test]
        public void FaceTo_MapsDeltas()
        {
            Assert.AreEqual(PortFace.East, BeltPath.FaceTo(new Vector2Int(1, 0)));
            Assert.AreEqual(PortFace.West, BeltPath.FaceTo(new Vector2Int(-1, 0)));
            Assert.AreEqual(PortFace.North, BeltPath.FaceTo(new Vector2Int(0, 1)));
            Assert.AreEqual(PortFace.South, BeltPath.FaceTo(new Vector2Int(0, -1)));
        }
    }
}
