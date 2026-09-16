using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **판은 자기 로봇의 마운트만 안다** (2026-09-16 신설 · 사용자 확정 · 플랜 §74-16 ①).
    ///
    /// **무엇을 지키는가.** 보드가 로봇별로 갈렸다. 실루엣은 같은 12×14 지만 A 판에는
    /// A 포트만, B 판에는 B 포트만 살아 있어야 한다. 안 그러면 A 판에 깐 운반로가
    /// **B 마운트로 흘러드는 것으로 읽히고**, 그림은 멀쩡한데 도착이 엉뚱한 로봇에게
    /// 세어진다 — 실패하지 않는 결함이다(지침 §7).
    /// </summary>
    public sealed class BoardOwnerTests
    {
        private static BoardGrid Board(MountOwner owner) =>
            new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero,
                PartLayout.BuildMask(), owner);

        /// <summary>A 의 포트 — (1,4) 남면. `PartLayout` 이 주인이므로 여기서 다시 안 적는다.</summary>
        private static MountPort PortOf(MountOwner owner)
        {
            foreach (MountPort p in PartLayout.MountPortsFor(owner)) return p;
            Assert.Fail($"{owner} 의 포트가 하나도 없다");
            return default;
        }

        [Test]
        public void 주인을_안_주면_로봇_A_다()
        {
            // 지어낸 기본값이 아니라 **시작 보드가 A 의 것**이라서다 —
            // 하네스·프로브·구 시험이 세우는 판이 전부 그 판이다.
            var g = new BoardGrid(4, 4, 1f, Vector2.zero);
            Assert.AreEqual(MountOwner.RobotA, g.Owner);
        }

        [Test]
        public void 포트_수는_A_하나_B_둘이다()
        {
            Assert.AreEqual(1, new List<MountPort>(PartLayout.MountPortsFor(MountOwner.RobotA)).Count);
            Assert.AreEqual(2, new List<MountPort>(PartLayout.MountPortsFor(MountOwner.RobotB)).Count);
        }

        [Test]
        public void 자기_포트는_찾고_남의_포트는_못_찾는다()
        {
            MountPort a = PortOf(MountOwner.RobotA);
            MountPort b = PortOf(MountOwner.RobotB);

            Assert.IsTrue(PartLayout.TryGetMountPort(a.cell, a.face, MountOwner.RobotA, out _),
                "A 판이 A 포트를 못 본다");
            Assert.IsFalse(PartLayout.TryGetMountPort(b.cell, b.face, MountOwner.RobotA, out _),
                "A 판이 B 포트를 본다 — 도착이 엉뚱한 로봇에게 세어진다");

            Assert.IsTrue(PartLayout.TryGetMountPort(b.cell, b.face, MountOwner.RobotB, out _));
            Assert.IsFalse(PartLayout.TryGetMountPort(a.cell, a.face, MountOwner.RobotB, out _));
        }

        [Test]
        public void 주인을_안_묻는_옛_길은_남의_포트도_찾는다()
        {
            // 🗑️ 이 과부하는 **그림을 깔 때만** 쓴다. 판 하나를 읽는 자리에서 부르면
            // 위 시험이 막는 결함이 그대로 돌아온다 — 그래서 무엇이 다른지를 박아 둔다.
            MountPort b = PortOf(MountOwner.RobotB);
            Assert.IsTrue(PartLayout.TryGetMountPort(b.cell, b.face, out _));
        }

        [Test]
        public void B_포트로_향한_벨트는_A_판에서_도착지가_안_된다()
        {
            // **결함 그 자체다.** 같은 칸·같은 면에 벨트를 깔고 판의 주인만 바꾼다.
            MountPort b = PortOf(MountOwner.RobotB);
            PortFace inFace = Opposite(b.face);

            BoardGrid aBoard = Board(MountOwner.RobotA);
            Assert.IsTrue(aBoard.TryPlaceBelt(b.cell, inFace, b.face, FlowKind.None, out _),
                "A 판에 벨트를 못 깔았다 — 시험 전제가 깨졌다");
            var aFlow = new BeltItemFlow();
            aFlow.Rebuild(aBoard);
            Assert.IsFalse(aFlow.HasMountExit(b.cell),
                "A 판인데 B 포트 칸이 도착지로 섰다");

            BoardGrid bBoard = Board(MountOwner.RobotB);
            Assert.IsTrue(bBoard.TryPlaceBelt(b.cell, inFace, b.face, FlowKind.None, out _));
            var bFlow = new BeltItemFlow();
            bFlow.Rebuild(bBoard);
            Assert.IsTrue(bFlow.HasMountExit(b.cell),
                "B 판인데 B 포트 칸이 도착지가 아니다");
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
