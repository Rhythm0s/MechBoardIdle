using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 세대 표식은 **주인별이다** (2026-09-17 사용자 결정 · `260917_V01` 6-1).
    ///
    /// ⚠️⚠️ **지키는 것은 「버려지는 것」이다.** 표식이 A 하나였을 때는
    /// `StartingBoardB` 를 고쳐도 표식이 안 바뀌어 **옛 B 판이 새 B 시작 보드 위에
    /// 조용히 올라왔다** — 크기도 주인도 같으니 아무 경고도 안 난다.
    /// 09-15 에 A 에서 났던 사고와 같은 모양이고, 화면에서는
    /// 「벨트를 이었는데 마운트 적재 0」으로 보였다.
    ///
    /// 📌 그래서 보는 것은 「표식이 있는가」가 아니라 **「다른 판이면 다른 표식인가」**다.
    /// </summary>
    public sealed class BoardGenerationTests
    {
        private static BoardGrid Empty(MountOwner owner)
            => new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero,
                PartLayout.BuildMask(), owner);

        [Test]
        public void 두_판의_표식이_서로_다르다()
        {
            // 같으면 한쪽 저장이 다른 쪽 판 위에 올라갈 수 있다.
            Assert.That(StartingBoardB.Generation,
                Is.Not.EqualTo(StartingBoard.Generation),
                "A 와 B 의 세대 표식이 같다 — 배치가 다른데 표식이 같으면 규칙 5 가 못 거른다");
        }

        [Test]
        public void 주인으로_고른_표식이_그_판의_것이다()
        {
            Assert.AreEqual(StartingBoard.Generation, BoardGeneration.Of(MountOwner.RobotA));
            Assert.AreEqual(StartingBoardB.Generation, BoardGeneration.Of(MountOwner.RobotB));
        }

        [Test]
        public void 표식은_배치가_바뀌면_바뀐다()
        {
            // 손으로 올리는 번호가 아니라 내용에서 뽑는다는 것이 이 표식의 전제다.
            var one = new[] { new BoardGeneration.NodeKey(new Vector2Int(1, 1), "core") };
            var two = new[] { new BoardGeneration.NodeKey(new Vector2Int(2, 1), "core") };

            Assert.That(BoardGeneration.Of(12, 14, one, null),
                Is.Not.EqualTo(BoardGeneration.Of(12, 14, two, null)),
                "칸이 달라졌는데 표식이 그대로다");
        }

        [Test]
        public void 조합표만_달라도_표식이_바뀐다()
        {
            // ⚠️ **B 판이 이것을 꼭 필요로 한다** — 가공 노드 셋이 같은 자산이고
            //    조합표만 다르다. 안 섞으면 둘을 맞바꿔도 표식이 그대로다.
            var battery = new[]
            {
                new BoardGeneration.NodeKey(new Vector2Int(1, 1), "proc", RecipeKind.Battery),
            };
            var power = new[]
            {
                new BoardGeneration.NodeKey(new Vector2Int(1, 1), "proc", RecipeKind.PowerMaterial),
            };

            Assert.That(BoardGeneration.Of(12, 14, battery, null),
                Is.Not.EqualTo(BoardGeneration.Of(12, 14, power, null)),
                "조합표가 달라졌는데 표식이 그대로다");
        }

        [Test]
        public void A_표식은_이_개정으로_안_바뀐다()
        {
            // ⚠️⚠️ **이 개정이 하기로 한 일은 B 하나다.** A 의 표식까지 바뀌면
            //    사용자가 감수하기로 한 것보다 **한 판을 더** 버리게 된다.
            //    그래서 A 는 조합표를 안 섞는다 — 옛 셈을 여기 다시 적어 대조한다.
            unchecked
            {
                uint h = 2166136261u;
                void Mix(int v)
                {
                    for (int b = 0; b < 4; b++) { h ^= (uint)((v >> (b * 8)) & 0xFF); h *= 16777619u; }
                }

                Mix(PartLayout.Columns);
                Mix(PartLayout.Rows);
                foreach (StartingBoard.Slot n in StartingBoard.Nodes)
                {
                    foreach (char c in n.nodeId ?? string.Empty) Mix(c);
                    Mix(n.cell.x); Mix(n.cell.y);
                }
                foreach (StartingBoard.Run r in StartingBoard.Belts)
                {
                    Mix(r.cell.x); Mix(r.cell.y);
                    Mix((int)r.inFace); Mix((int)r.outFace);
                    Mix(r.merger ? 1 : 0);
                }

                Assert.AreEqual(h.ToString("x8"), StartingBoard.Generation,
                    "A 의 표식이 이 개정 때문에 바뀌었다 — A 저장까지 버려진다");
            }
        }

        [Test]
        public void B_저장에_B_표식이_찍힌다()
        {
            BoardStateV1 state = BoardStateCodec.Capture(Empty(MountOwner.RobotB));

            Assert.AreEqual(StartingBoardB.Generation, state.generation,
                "B 판을 떴는데 A 의 표식이 찍혔다 — 그러면 B 를 고쳐도 저장이 안 버려진다");
        }

        [Test]
        public void B_옛_세대_저장은_버려진다()
        {
            // 「B 를 고치면 B 저장이 버려진다」 — 사용자가 감수하기로 한 그 동작이다.
            BoardStateV1 state = BoardStateCodec.Capture(Empty(MountOwner.RobotB));
            state.generation = "deadbeef";   // 다른 세대에서 나온 저장

            BoardGrid grid = Empty(MountOwner.RobotB);
            Assert.IsFalse(BoardStateCodec.Fits(state, grid), "옛 세대 저장이 받아들여졌다");
            Assert.That(BoardStateCodec.WhyNotFit(state, grid), Does.Contain("세대"),
                "버린 이유가 세대라고 안 적힌다 — 조용히 버리지 않는 것이 규칙이다");
        }

        [Test]
        public void B_같은_세대_저장은_복원된다()
        {
            BoardStateV1 state = BoardStateCodec.Capture(Empty(MountOwner.RobotB));

            Assert.IsTrue(BoardStateCodec.Fits(state, Empty(MountOwner.RobotB)),
                "방금 뜬 저장이 같은 판에 안 맞는다 — 뜰 때와 견줄 때가 다른 판을 본다");
        }

        [Test]
        public void A_저장은_B_판에_안_올라간다()
        {
            // 주인 검사와 세대 검사가 **둘 다** 걸러야 한다 — 하나가 죽어도 다른 하나가 잡는다.
            BoardStateV1 state = BoardStateCodec.Capture(Empty(MountOwner.RobotA));

            Assert.IsFalse(BoardStateCodec.Fits(state, Empty(MountOwner.RobotB)),
                "A 판이 B 자리에 들어앉는다");
        }
    }
}
