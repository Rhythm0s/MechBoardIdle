using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **면 화살표는 아직 다 안 이어진 노드에만 선다**
    /// (2026-09-16 신설 · 사용자 확정 · 플랜 §74-3 #34).
    ///
    /// ⚠️⚠️ **지키는 것은 「안 그리는 것」이다.** 다 이은 판에까지 화살표가 남으면
    /// 보드가 화살표 밭이 되고, 그때는 무엇을 봐야 하는지가 오히려 흐려진다.
    /// 「그린다」보다 **「이어지는 순간 사라진다」**가 깨지기 쉬운 쪽이다.
    /// </summary>
    public sealed class NodeWiringHintsTests
    {
        private const string NodeRoot = "Assets/_Project/ScriptableObjects/Nodes";

        private static NodeDefinition Node(string id)
            => AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodeRoot}/Node_{id}.asset");

        /// <summary>A 의 시작 보드 — 운반로가 이어진 판이다.</summary>
        private static BoardGrid StartingA(bool fillEmptySlot)
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero,
                PartLayout.BuildMask(), MountOwner.RobotA);

            foreach (StartingBoard.Slot slot in StartingBoard.Nodes)
            {
                NodeDefinition def = Node(slot.nodeId);
                Assert.IsNotNull(def, $"노드 자산이 없다 — Node_{slot.nodeId}.asset");
                Assert.IsTrue(g.TryPlace(slot.cell, def, out _));
            }
            foreach (StartingBoard.Run r in StartingBoard.Belts) Place(g, r);
            if (fillEmptySlot) Place(g, StartingBoard.FillsEmptySlot);

            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        private static void Place(BoardGrid g, StartingBoard.Run run)
        {
            if (run.merger)
                g.TryPlaceBeltElement(run.cell, BeltElementKind.Merger,
                    StartingBoard.MergerInFaces(run.outFace), new[] { run.outFace },
                    FlowKind.None, out _);
            else
                g.TryPlaceBelt(run.cell, run.inFace, run.outFace, FlowKind.None, out _);
        }

        [Test]
        public void 홀로_놓인_노드는_모든_면에_화살표가_선다()
        {
            // 아무것도 안 이어졌으니 「할 일이 남았다」가 가장 강한 자리다.
            var g = new BoardGrid(6, 6, 1f, Vector2.zero, null, MountOwner.RobotA);
            NodeDefinition proc = Node(StartingBoard.ProcId);
            Assert.IsNotNull(proc);
            Assert.IsTrue(g.TryPlace(new Vector2Int(2, 2), proc, out NodeInstance placed));

            List<PortHint> hints = NodeWiringHints.Collect(g);
            Assert.AreEqual(placed.Ports().Count, hints.Count,
                "면 수만큼 화살표가 안 섰다");
            Assert.IsFalse(NodeWiringHints.IsFullyWired(g, new Vector2Int(2, 2)));
        }

        [Test]
        public void 전력_노드에는_화살표가_안_선다()
        {
            // ⚠️ **전력망은 전역이라 에너지 노드는 벨트를 안 문다**(`StartingBoard` 확정).
            //    이것을 안 보면 에너지 셋에 화살표가 **영영** 남는다 — 첫 판이 그랬고
            //    「다 이은 판에 0 개」 시험이 그것을 잡았다.
            var g = new BoardGrid(6, 6, 1f, Vector2.zero, null, MountOwner.RobotA);
            Assert.IsTrue(g.TryPlace(new Vector2Int(2, 2), Node(StartingBoard.EnergyId), out _));

            Assert.AreEqual(0, NodeWiringHints.Collect(g).Count,
                "전력 노드에 화살표가 섰다 — 벨트로 이을 수 없는 면이라 영영 안 사라진다");
        }

        [Test]
        public void 출력은_하나만_이어져도_된다()
        {
            // ⚠️ 코어는 출력면이 넷인데 시작 보드가 **둘만** 쓴다 — 남은 둘은
            //    「플레이어가 줄을 더 놓을 자리」이지 **할 일이 아니다.**
            BoardGrid g = StartingA(fillEmptySlot: true);
            Assert.IsTrue(NodeWiringHints.IsFullyWired(g, new Vector2Int(5, 8)),
                "코어의 안 쓴 출력면 때문에 화살표가 남는다");
        }

        [Test]
        public void 입구는_안쪽_출구는_바깥을_본다()
        {
            var g = new BoardGrid(6, 6, 1f, Vector2.zero, null, MountOwner.RobotA);
            Assert.IsTrue(g.TryPlace(new Vector2Int(2, 2), Node(StartingBoard.ProcId),
                out NodeInstance placed));

            var byFace = new Dictionary<PortFace, bool>();
            foreach (PortHint h in NodeWiringHints.Collect(g)) byFace[h.face] = h.outward;

            foreach (NodePort p in placed.Ports())
            {
                Assert.IsTrue(byFace.ContainsKey(p.face), $"{p.face} 화살표가 없다");
                Assert.AreEqual(p.io == PortIO.Output, byFace[p.face],
                    $"{p.face} 의 방향이 포트와 다르다 — 화살이 거짓말을 한다");
            }
        }

        [Test]
        public void 회전하면_화살표도_따라_돈다()
        {
            // 면이 돌았는데 화살이 그대로면 **벨트를 어디 붙일지가 거짓으로 읽힌다**
            // (09-15 에 포트 탭이 겪은 것과 같은 병).
            var g = new BoardGrid(6, 6, 1f, Vector2.zero, null, MountOwner.RobotA);
            Assert.IsTrue(g.TryPlace(new Vector2Int(2, 2), Node(StartingBoard.ProcId),
                out NodeInstance placed));

            var before = new HashSet<PortFace>();
            foreach (PortHint h in NodeWiringHints.Collect(g)) before.Add(h.face);

            placed.Rotation = 1;
            var after = new HashSet<PortFace>();
            foreach (PortHint h in NodeWiringHints.Collect(g)) after.Add(h.face);

            Assert.AreNotEqual(before, after, "회전했는데 면이 그대로다");
        }

        [Test]
        public void 다_이어진_판에는_하나도_안_선다()
        {
            // **이 시험이 규칙의 핵심이다** — 「이어지는 순간 사라진다」.
            BoardGrid g = StartingA(fillEmptySlot: true);

            List<PortHint> hints = NodeWiringHints.Collect(g);
            var stuck = new List<string>();
            foreach (PortHint h in hints) stuck.Add($"{h.cell} {h.face}");

            Assert.AreEqual(0, hints.Count,
                "다 이은 시작 보드에 화살표가 남았다 — " + string.Join(" · ", stuck));
        }

        [Test]
        public void 튜토리얼_빈_칸으로는_화살표가_안_돌아온다()
        {
            // ⚠️⚠️ **첫 시험은 전제가 틀렸다.** 「빈 칸이 있으면 그 줄에 화살표가 돈다」로
            //    적었는데 **0 개가 나왔다.** 틀린 것은 코드가 아니라 내 예상이었다 —
            //    비워 둔 칸은 **벨트 줄 한가운데**이고, 그 양옆 노드의 **면은 여전히
            //    벨트와 맞물려 있다.** 노드는 「덜 이어진」 것이 아니다.
            //
            // 📌 **끊긴 벨트 줄은 다른 표시가 말한다** — 「나가는 곳이 없다」 경고와
            //    고스트 안내다. 화살표까지 그 일을 하면 **같은 것을 두 번 말한다.**
            //    화살표가 말하는 것은 **노드의 면**이다.
            BoardGrid g = StartingA(fillEmptySlot: false);

            Assert.AreEqual(0, NodeWiringHints.Collect(g).Count,
                "벨트 줄 가운데가 비었을 뿐인데 노드에 화살표가 섰다");
        }

        [Test]
        public void 노드에_붙은_벨트를_떼면_화살표가_돌아온다()
        {
            // **이쪽이 화살표의 자리다** — 노드의 면이 실제로 비는 경우.
            BoardGrid g = StartingA(fillEmptySlot: true);
            Assert.AreEqual(0, NodeWiringHints.Collect(g).Count, "시험 전제가 깨졌다");

            // 코어 남면에 물린 벨트(5,7)를 뗀다 — 그 줄의 가공 노드가 입력을 잃는다.
            var belt = new Vector2Int(5, 7);
            Assert.IsTrue(g.HasBelt(belt), "시험이 짚은 칸에 벨트가 없다");
            Assert.IsTrue(g.TryRemoveBelt(belt));
            BeltFlow.Resolve(g);

            List<PortHint> hints = NodeWiringHints.Collect(g);
            Assert.Greater(hints.Count, 0, "노드 면이 비었는데 화살표가 안 섰다");

            // **판 전체가 아니다** — 멀쩡한 노드는 빠져 있어야 한다.
            var cells = new HashSet<Vector2Int>();
            foreach (PortHint h in hints) cells.Add(h.cell);

            int nodeCount = 0;
            for (int x = 0; x < g.Columns; x++)
            for (int y = 0; y < g.Rows; y++)
                if (g.GetAt(new Vector2Int(x, y)) != null) nodeCount++;

            Assert.Less(cells.Count, nodeCount,
                "벨트 하나를 뗐는데 모든 노드에 화살표가 섰다 — 판정이 노드별이 아니다");
        }

        [Test]
        public void 마운트로_나가는_면은_이어진_것으로_센다()
        {
            // ⚠️ 마운트 고정 포트는 **격자 밖**을 향해 링크가 안 선다. 이것을 안 보면
            //    운반로 끝 노드가 영영 「덜 이어진」 것으로 남아 화살표가 안 사라진다.
            //
            // 📌 **입력이 없는 노드로 잰다**(코어 — 출력면 넷 · 입력 0). 그래야 이 시험이
            //    재는 것이 **마운트 면 하나**로 좁아진다. 입력이 있는 노드를 쓰면
            //    「입력이 비어서 섰다」와 「마운트를 안 셌다」가 안 갈린다 — 첫 판이
            //    그렇게 흐렸다.
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero,
                PartLayout.BuildMask(), MountOwner.RobotB);

            MountPort port = default;
            bool found = false;
            foreach (MountPort p in PartLayout.MountPortsFor(MountOwner.RobotB))
            {
                port = p;
                found = true;
                break;
            }
            Assert.IsTrue(found, "B 포트가 없다");

            NodeDefinition core = Node(StartingBoard.CoreId);
            Assert.IsNotNull(core);
            Assert.IsTrue(g.TryPlace(port.cell, core, out NodeInstance placed),
                $"{port.cell} 에 코어를 못 놓았다 — 실루엣 밖이면 시험 전제가 깨진다");

            bool aims = false;
            foreach (NodePort p in placed.Ports())
                if (p.io == PortIO.Output && p.face == port.face) { aims = true; break; }
            Assert.IsTrue(aims, "코어가 그 면으로 출력을 안 낸다 — 자산이 바뀌었다");

            Assert.IsTrue(NodeWiringHints.IsFullyWired(g, port.cell),
                "마운트로 나가는 면을 이어진 것으로 안 셌다 — 화살표가 영영 남는다");
            Assert.AreEqual(0, NodeWiringHints.Collect(g).Count);
        }

        [Test]
        public void 남의_마운트_면은_안_센다()
        {
            // 판이 자기 주인의 포트만 본다(2026-09-16 · 보드 로봇별 분리) —
            // A 판에 B 포트 자리를 놓아도 **이어진 것이 아니다.**
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero,
                PartLayout.BuildMask(), MountOwner.RobotA);

            MountPort bPort = default;
            foreach (MountPort p in PartLayout.MountPortsFor(MountOwner.RobotB)) { bPort = p; break; }

            Assert.IsTrue(g.TryPlace(bPort.cell, Node(StartingBoard.CoreId), out _));
            Assert.IsFalse(NodeWiringHints.IsFullyWired(g, bPort.cell),
                "A 판인데 B 포트를 나가는 곳으로 셌다");
        }

        private static Vector2Int Delta(PortFace face)
        {
            switch (face)
            {
                case PortFace.North: return new Vector2Int(0, 1);
                case PortFace.East: return new Vector2Int(1, 0);
                case PortFace.South: return new Vector2Int(0, -1);
                default: return new Vector2Int(-1, 0);
            }
        }

        private static PortFace Opposite(PortFace f) => f switch
        {
            PortFace.North => PortFace.South,
            PortFace.South => PortFace.North,
            PortFace.East => PortFace.West,
            _ => PortFace.East,
        };
    }
}
