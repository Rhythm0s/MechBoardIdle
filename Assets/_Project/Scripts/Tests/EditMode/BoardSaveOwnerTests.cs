using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>#197 — B 판을 편집해도 A 저장 칸이 안 바뀐다 · 0x0 구 칸은 무시 (2026-10-19 결함 수정).</summary>
    public sealed class BoardSaveOwnerTests
    {
        [SetUp] public void SetUp() => IdleSignals.Reset();
        [TearDown] public void TearDown() => IdleSignals.Reset();

        private static BoardStateV1 State(MountOwner o, int cols = 12) =>
            new BoardStateV1 { owner = (int)o, columns = cols, rows = 14 };

        [Test]
        public void B_편집_뒤_A_저장_칸_불변()
        {
            var data = new SaveDataV1();
            BoardStateV1 a = State(MountOwner.RobotA);
            data.SetBoard(MountOwner.RobotA, a);
            data.SetBoard(MountOwner.RobotA, State(MountOwner.RobotB));   // 옛 결함 길 — 남의 판
            Assert.AreSame(a, data.BoardOf(MountOwner.RobotA), "A 항목이 B 판으로 갈아 끼워지면 안 된다");
            Assert.IsNull(data.BoardOf(MountOwner.RobotB));

            string bc = System.IO.File.ReadAllText("Assets/_Project/Scripts/Logistics/BoardController.cs");
            StringAssert.Contains("IdleSignals.SetBoardState(_grid.Owner, BoardStateCodec.Capture(_grid));", bc);
            StringAssert.DoesNotContain("            IdleSignals.BoardState = BoardStateCodec.Capture(_grid);", bc);
        }

        [Test]
        public void 빈_구_칸은_A_판으로_안_들어간다()
        {
            var data = new SaveDataV1 { board = new BoardStateV1() };   // JsonUtility 가 되살린 빈 칸 꼴(0x0 · owner 0)
            data.MigrateLegacyBoard();
            Assert.IsNull(data.BoardOf(MountOwner.RobotA), "0x0 구 칸 = 무시 → 시작 보드가 아니라 저장 그대로(없음)");
            Assert.IsNull(data.board);

            var real = new SaveDataV1 { board = State(MountOwner.RobotA) };
            real.MigrateLegacyBoard();
            Assert.IsNotNull(real.BoardOf(MountOwner.RobotA), "진짜 구 판은 그대로 옮긴다");
        }
    }
}
