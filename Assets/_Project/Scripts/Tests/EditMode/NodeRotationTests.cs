using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **노드 회전** (2026-09-15 사용자 확정 · §72-42).
    ///
    /// ⚠️ **왜 넣는가.** 노드가 **서면으로 받아 동면으로만 내므로** 산물이 늘 동쪽으로 흐른다.
    /// 도착지(마운트)가 보드 **서쪽 끝**이라 운반로가 동→서 **가로 한 줄**이 되고, 그 줄이
    /// 코어와 아래 절반을 **가로로 가른다** — 그래서 S3 프리셋이 못 섰다(`260914_V03` 7장).
    /// 돌릴 수 있으면 **산물이 서·남으로도 나가** 그 벽이 없어진다.
    ///
    /// ⚠️ **자산은 안 건드린다.** 자산의 포트 면은 **0도일 때의 면**이고, 돌린 면은
    /// `NodeInstance.Ports()` 가 낸다 — 같은 자산 하나로 네 방향을 다 쓴다.
    /// </summary>
    public sealed class NodeRotationTests
    {
        private const string NodesDir = "Assets/_Project/ScriptableObjects/Nodes";

        private NodeDefinition _core, _proc, _muni;

        [SetUp]
        public void SetUp()
        {
            _core = Load("core");
            _proc = Load("proc");
            _muni = Load("muni");
            if (_core == null || _proc == null || _muni == null)
                Assert.Ignore("노드 자산 없음 — 먼저 메뉴 'MBI/Generate Balance + Nodes' 실행.");
        }

        private static NodeDefinition Load(string id) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodesDir}/Node_{id}.asset");

        private static BoardGrid Grid() => new BoardGrid(10, 10, 1f, Vector2.zero);

        private static bool HasLink(BoardGrid g, Vector2Int from, Vector2Int to)
        {
            foreach (BeltLink l in BeltRouting.BuildLinks(g))
                if (l.fromCell == from && l.toCell == to) return true;
            return false;
        }

        // ────────────────────────────────────────────────────────────────

        /// <summary>면은 **시계 방향**으로 돈다 — 북 → 동 → 남 → 서.</summary>
        [Test]
        public void FacesTurnClockwise()
        {
            Assert.AreEqual(PortFace.East, NodeConnectionRules.Rotate(PortFace.North, 1));
            Assert.AreEqual(PortFace.South, NodeConnectionRules.Rotate(PortFace.North, 2));
            Assert.AreEqual(PortFace.West, NodeConnectionRules.Rotate(PortFace.North, 3));
            Assert.AreEqual(PortFace.North, NodeConnectionRules.Rotate(PortFace.North, 4));

            // 왼쪽으로 돌리기(음수)도 받는다.
            Assert.AreEqual(PortFace.West, NodeConnectionRules.Rotate(PortFace.North, -1));
        }

        /// <summary>0도면 **자산의 목록 그대로**다 — 안 돌린 노드에 사본을 만들지 않는다.</summary>
        [Test]
        public void UnrotatedNode_ReturnsTheAssetListItself()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(1, 1), _proc, out NodeInstance proc);

            Assert.AreEqual(0, proc.Rotation);
            Assert.AreSame(_proc.ports, proc.Ports(), "사본을 안 만든다");
        }

        /// <summary>
        /// **돌리면 면이 따라 돈다** — 가공은 서면 입력 · 동면 출력인데,
        /// 180도 돌리면 **동면 입력 · 서면 출력**이 된다.
        /// </summary>
        [Test]
        public void RotatingTurnsTheFaces()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(1, 1), _proc, out NodeInstance proc);
            proc.Rotation = 2;

            bool westOut = false, eastIn = false;
            foreach (NodePort p in proc.Ports())
            {
                if (p.io == PortIO.Output && p.face == PortFace.West) westOut = true;
                if (p.io == PortIO.Input && p.face == PortFace.East) eastIn = true;
            }

            Assert.IsTrue(westOut, "180도면 **서쪽으로 낸다** — 가로벽을 여는 자리다");
            Assert.IsTrue(eastIn, "180도면 동쪽으로 받는다");
        }

        /// <summary>
        /// ✅ **돌린 노드가 서쪽으로 산물을 보낸다** — 링크가 실제로 선다.
        ///
        /// 코어(2,1) → 가공(1,1 · 180도) → **서쪽** 벨트(0,1). 구조상 종전에는 못 하던 방향이다.
        /// </summary>
        [Test]
        public void ARotatedNode_FeedsWestward()
        {
            var g = Grid();

            // 코어는 네 면 다 출력이라 어느 쪽으로도 먹인다.
            g.TryPlace(new Vector2Int(2, 1), _core, out _);
            g.TryPlace(new Vector2Int(1, 1), _proc, out NodeInstance proc);
            proc.Rotation = 2;   // 동면 입력(코어 쪽) · 서면 출력

            g.TryPlaceBelt(new Vector2Int(0, 1), PortFace.East, PortFace.West, FlowKind.None, out _);

            BeltFlow.Resolve(g);

            Assert.IsTrue(HasLink(g, new Vector2Int(2, 1), new Vector2Int(1, 1)),
                "코어가 동쪽에서 가공에 먹인다");
            Assert.IsTrue(HasLink(g, new Vector2Int(1, 1), new Vector2Int(0, 1)),
                "가공이 **서쪽으로** 낸다");
            Assert.AreEqual(FlowKind.BasicParts, BeltFlow.KindAt(g, new Vector2Int(0, 1)),
                "서쪽 벨트가 기초재료·부품을 나른다");
        }

        /// <summary>
        /// ⚠️ **시작 보드는 안 변한다** — 아무것도 안 돌렸으므로 회전이 들어와도 그대로여야 한다.
        /// 회전을 넣으면서 기본값이 흔들리면 튜토리얼이 조용히 깨진다.
        /// </summary>
        [Test]
        public void TheStartingBoardIsUnchanged()
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f,
                Vector2.zero, PartLayout.BuildMask());

            foreach (StartingBoard.Slot slot in StartingBoard.Nodes)
            {
                g.TryPlace(slot.cell, Load(slot.nodeId), out NodeInstance node);
                Assert.AreEqual(0, node.Rotation, $"{slot.cell} 은 안 돌아 있어야 한다");
            }
        }

        /// <summary>네 바퀴 돌리면 제자리 — 저장 왕복에서 값이 커지지 않는다.</summary>
        [Test]
        public void FourTurnsIsIdentity()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(1, 1), _muni, out NodeInstance muni);

            muni.Rotation = 4;
            Assert.AreEqual(0, muni.Rotation, "0~3 으로 접힌다");

            muni.Rotation = -1;
            Assert.AreEqual(3, muni.Rotation, "음수도 접힌다");
        }
    }
}
