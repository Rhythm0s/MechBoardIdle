using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 놓는 순간 양 끝을 이웃 노드에 맞춘다 (2026-09-16 사용자 확정 ⓑ+ⓒ · 플랜 §74-12 A).
    ///
    /// ⚠️⚠️ **왜 필요했나.** 코어(사면 출력) 위 칸에 **가로로** 벨트를 끌면 받는 면이
    /// 동/서라 영영 안 받는다. 붙어 있는데 안 흐르고, 왜 안 되는지가 화면에 안 적혔다.
    ///
    /// 📌 지키는 것 셋 — ① 코어 위 가로 드래그가 이어진다 ② **놓은 뒤 회전은 안 건드린다**
    /// ③ 저장이 싣는 면이 달라지지 않는다.
    /// </summary>
    public sealed class BeltDropSnapTests
    {
        private readonly List<NodeDefinition> _made = new List<NodeDefinition>();

        [TearDown]
        public void Cleanup()
        {
            foreach (NodeDefinition d in _made) Object.DestroyImmediate(d);
            _made.Clear();
        }

        private static BoardGrid Grid() => new BoardGrid(12, 14, 1f, Vector2.zero);

        /// <summary>네 면 모두 출력인 노드 — 코어와 같은 꼴이다(실측: ports 넷이 io 1).</summary>
        private NodeDefinition AllOutputNode()
        {
            var d = ScriptableObject.CreateInstance<NodeDefinition>();
            d.nodeId = "N_core_like";
            d.ports = new List<NodePort>
            {
                new NodePort { face = PortFace.North, io = PortIO.Output, kind = FlowKind.CoreEnergy },
                new NodePort { face = PortFace.East,  io = PortIO.Output, kind = FlowKind.CoreEnergy },
                new NodePort { face = PortFace.South, io = PortIO.Output, kind = FlowKind.CoreEnergy },
                new NodePort { face = PortFace.West,  io = PortIO.Output, kind = FlowKind.CoreEnergy },
            };
            _made.Add(d);
            return d;
        }

        [Test]
        public void 코어_위로_가로_드래그하면_아래에서_받는다()
        {
            BoardGrid g = Grid();
            var core = new Vector2Int(5, 8);
            g.TryPlace(core, AllOutputNode(), out _);

            // 코어 **바로 위** 칸을 지나는 가로 드래그 — 경로가 주는 면은 서→동이다.
            var segs = BeltPath.Build(new List<Vector2Int>
            {
                new Vector2Int(5, 9), new Vector2Int(6, 9), new Vector2Int(7, 9),
            });

            Assert.That(segs[0].inFace, Is.EqualTo(PortFace.West), "전제 — 경로는 서에서 받는다");

            int snapped = BeltDropSnap.Snap(g, segs);

            Assert.That(snapped, Is.EqualTo(1), "시작 한쪽만 맞춰야 한다");
            Assert.That(segs[0].inFace, Is.EqualTo(PortFace.South),
                "코어가 아래에 있으므로 남쪽에서 받아야 한다 — 이것이 안 되면 영영 안 흐른다");
            Assert.That(segs[0].outFace, Is.EqualTo(PortFace.East), "출구는 경로가 정한 대로 둔다");
        }

        [Test]
        public void 가운데_칸은_안_건드린다()
        {
            BoardGrid g = Grid();
            g.TryPlace(new Vector2Int(6, 8), AllOutputNode(), out _);   // 가운데 칸 아래

            var segs = BeltPath.Build(new List<Vector2Int>
            {
                new Vector2Int(5, 9), new Vector2Int(6, 9), new Vector2Int(7, 9),
            });
            PortFace midIn = segs[1].inFace, midOut = segs[1].outFace;

            BeltDropSnap.Snap(g, segs);

            // ⚠️ 가운데를 건드리면 줄이 끊긴다 — 양 끝만이다.
            Assert.That(segs[1].inFace, Is.EqualTo(midIn), "가운데 입구가 바뀌었다");
            Assert.That(segs[1].outFace, Is.EqualTo(midOut), "가운데 출구가 바뀌었다");
        }

        [Test]
        public void 이웃_노드가_없으면_아무것도_안_바꾼다()
        {
            BoardGrid g = Grid();
            var segs = BeltPath.Build(new List<Vector2Int>
            {
                new Vector2Int(5, 9), new Vector2Int(6, 9),
            });
            PortFace inFace = segs[0].inFace, outFace = segs[segs.Count - 1].outFace;

            Assert.That(BeltDropSnap.Snap(g, segs), Is.EqualTo(0));
            Assert.That(segs[0].inFace, Is.EqualTo(inFace));
            Assert.That(segs[segs.Count - 1].outFace, Is.EqualTo(outFace));
        }

        [Test]
        public void 놓은_뒤_회전은_안_건드린다()
        {
            // ⚠️ **이것이 ⓑ 의 뜻이다.** 스냅은 **놓는 순간 한 번**이고, 그 뒤 플레이어가
            //    돌려 둔 면은 코드가 덮지 않는다. 격자에 이미 선 벨트를 다시 잡는 것은
            //    `BeltAutoOrient` 이고 그쪽은 병합기·분류기만 본다.
            BoardGrid g = Grid();
            g.TryPlace(new Vector2Int(5, 8), AllOutputNode(), out _);

            var cell = new Vector2Int(5, 9);
            g.TryPlaceBelt(cell, PortFace.West, PortFace.East, FlowKind.None, out _);

            BeltAutoOrient.Resolve(g);

            BeltInstance b = g.GetBeltAt(cell);
            Assert.That(b.InFace, Is.EqualTo(PortFace.West),
                "이미 놓인 직선 벨트의 면이 바뀌었다 — 스냅이 놓는 순간을 넘어 따라가고 있다");
        }

        [Test]
        public void 이웃은_있는데_면이_다르면_사유가_선다()
        {
            BoardGrid g = Grid();
            g.TryPlace(new Vector2Int(5, 8), AllOutputNode(), out _);
            g.TryPlaceBelt(new Vector2Int(5, 9), PortFace.West, PortFace.East, FlowKind.None, out _);
            BeltFlow.Resolve(g);

            List<Vector2Int> bad = BeltRouting.FaceMismatchCells(g);

            Assert.That(bad, Has.Member(new Vector2Int(5, 9)),
                "붙어 있는데 안 이어진 칸을 사유로 못 잡았다");
        }

        [Test]
        public void 아무것도_안_붙은_벨트는_사유가_아니다()
        {
            // 「아직 안 지은 것」과 「지어 놓고 안 되는 것」은 다른 일이다.
            BoardGrid g = Grid();
            g.TryPlaceBelt(new Vector2Int(2, 2), PortFace.West, PortFace.East, FlowKind.None, out _);
            BeltFlow.Resolve(g);

            Assert.That(BeltRouting.FaceMismatchCells(g), Has.No.Member(new Vector2Int(2, 2)));
        }
    }
}
