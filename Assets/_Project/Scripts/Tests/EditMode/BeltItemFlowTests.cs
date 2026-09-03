using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 벨트 위 개별 아이템의 이동(2026-09-03 · `260903_W01` 4장).
    ///
    /// 여기서 고정하는 것은 **거동**이지 값이 아니다 — 속도와 간격은 잠정치이고
    /// 설계가 실측 후 확정한다. 그래서 테스트는 상수를 그대로 읽어 쓴다.
    /// </summary>
    public sealed class BeltItemFlowTests
    {
        private const float Delta = 1e-3f;

        // 서→동 직선 벨트를 x=0..n-1에 깐다. 마지막 칸의 출력면은 동쪽이므로 그 앞이 라인의 끝이다.
        private static BoardGrid StraightLine(int length)
        {
            var grid = new BoardGrid(length + 2, 3, 1f, Vector2.zero);
            for (int x = 0; x < length; x++)
            {
                grid.TryPlaceBelt(new Vector2Int(x, 1),
                    PortFace.West, PortFace.East, FlowKind.Ammo, out _);
            }
            return grid;
        }

        private static float SecondsPerCell => 1f / BeltItemFlow.CellsPerSecondTbd;

        [Test]
        public void Insert_ThenTick_MovesToNextCell()
        {
            BoardGrid grid = StraightLine(2);
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            Assert.IsTrue(flow.TryInsert(new Vector2Int(0, 1), FlowKind.Ammo));
            Assert.AreEqual(1, flow.ItemsAt(new Vector2Int(0, 1)).Count);

            flow.Tick(SecondsPerCell);

            Assert.AreEqual(0, flow.ItemsAt(new Vector2Int(0, 1)).Count, "앞 칸을 떠나야 한다");
            Assert.AreEqual(1, flow.ItemsAt(new Vector2Int(1, 1)).Count, "다음 칸에 들어와야 한다");
        }

        [Test]
        public void EndOfLine_CountsAsDelivered()
        {
            BoardGrid grid = StraightLine(1);
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            flow.TryInsert(new Vector2Int(0, 1), FlowKind.Ammo);
            Assert.AreEqual(0, flow.DeliveredCount);

            flow.Tick(SecondsPerCell);

            Assert.AreEqual(1, flow.DeliveredCount, "다음 칸이 없으면 배출로 센다");
            Assert.AreEqual(0, flow.ItemsAt(new Vector2Int(0, 1)).Count);
        }

        /// <summary>
        /// **이 테스트가 이 개정의 핵심이다.** 종전 모델에는 「정체」라는 상태가 없었다 —
        /// 벨트는 무엇을 나르는지만 알았지 몇 개가 어디에 있는지 몰랐다.
        /// </summary>
        [Test]
        public void Blocked_ItemsStackWithMinimumGap()
        {
            // 두 칸 라인이어야 한다. 한 칸이면 맨 앞이 곧바로 배출돼 칸이 영영 안 찬다.
            BoardGrid grid = StraightLine(2);
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            var cell = new Vector2Int(0, 1);
            float gapTick = BeltItemFlow.MinGapCells / BeltItemFlow.CellsPerSecondTbd;

            for (int i = 0; i < BeltItemFlow.MaxPerCell; i++)
            {
                Assert.IsTrue(flow.TryInsert(cell, FlowKind.Ammo), $"{i}번째는 들어가야 한다");

                // 마지막 삽입 뒤에는 밀지 않는다 — 한 번 더 밀면 맨 앞이 출력면에 닿아
                // 다음 칸으로 빠지고, 그러면 자리가 생겨 이 테스트가 뜻을 잃는다.
                if (i < BeltItemFlow.MaxPerCell - 1) flow.Tick(gapTick);
            }

            Assert.IsFalse(flow.TryInsert(cell, FlowKind.Ammo),
                "칸당 최대를 넘으면 못 들어간다 — 이것이 상류 정지 신호다");
        }

        [Test]
        public void EmptyLane_IsNotBlocked()
        {
            BoardGrid grid = StraightLine(2);
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            Assert.IsFalse(flow.IsBlocked(new Vector2Int(0, 1)));
        }

        /// <summary>
        /// 속도와 간격은 잠정치지만 **둘의 비는 벨트 단일 규격 12/초와 맞아야 한다**
        /// (밸런스 문서「수치 산정 대상 카탈로그」). 값을 바꿀 때 이 관계가 깨지면 여기서 걸린다.
        /// </summary>
        [Test]
        public void SpeedOverGap_MatchesBeltThroughput()
        {
            float throughput = BeltItemFlow.CellsPerSecondTbd / BeltItemFlow.MinGapCells;
            Assert.AreEqual(12f, throughput, Delta, "벨트 한 줄 처리량은 12/초 고정이다");
        }

        [Test]
        public void RemovingBelt_DropsItemsOnIt()
        {
            BoardGrid grid = StraightLine(2);
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            var cell = new Vector2Int(0, 1);
            flow.TryInsert(cell, FlowKind.Ammo);
            Assert.AreEqual(1, flow.ItemsAt(cell).Count);

            grid.TryRemoveBelt(cell);
            flow.Rebuild(grid);

            Assert.AreEqual(0, flow.ItemsAt(cell).Count, "벨트를 걷으면 그 위의 물건도 사라진다");
        }
    }
}
