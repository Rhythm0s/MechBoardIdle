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

        /// <summary>
        /// **분류기가 품목에 따라 갈래를 나눈다.** 이것이 없으면 조립 시스템 문서
        /// 「분류기가 태그 스킬의 구성을 정한다」가 화면에서 성립하지 않는다.
        /// </summary>
        [Test]
        public void Sorter_RoutesByKind()
        {
            var grid = new BoardGrid(5, 5, 1f, Vector2.zero);

            // 분류기 (2,2): 서에서 받아 동·북으로 가른다.
            grid.TryPlaceBeltElement(new Vector2Int(2, 2), BeltElementKind.Sorter,
                new[] { PortFace.West }, new[] { PortFace.East, PortFace.North },
                FlowKind.Ammo, out _);

            // 동쪽 갈래는 탄약, 북쪽 갈래는 물류 품목.
            grid.TryPlaceBelt(new Vector2Int(3, 2), PortFace.West, PortFace.East, FlowKind.Ammo, out _);
            grid.TryPlaceBelt(new Vector2Int(2, 3), PortFace.South, PortFace.North, FlowKind.Material, out _);

            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            var sorter = new Vector2Int(2, 2);
            Assert.IsTrue(flow.TryInsert(sorter, FlowKind.Ammo));
            flow.Tick(SecondsPerCell);

            Assert.AreEqual(1, flow.ItemsAt(new Vector2Int(3, 2)).Count, "탄약은 탄약 갈래로 간다");
            Assert.AreEqual(0, flow.ItemsAt(new Vector2Int(2, 3)).Count, "품목이 다른 갈래로 새지 않는다");
        }

        /// <summary>
        /// **병합기 정체.** 합류 지점이 차면 상류가 그 앞에서 선다.
        ///
        /// 조립 시스템 문서「벨트와 병목 시각화」의 「대역을 늘리려면 병렬 경로를 늘려야 한다」가
        /// 지금까지 숫자로만 참이었다. 이 거동이 그것을 화면에 올리는 근거다.
        /// </summary>
        [Test]
        public void Merger_UpstreamStallsWhenFull()
        {
            var grid = new BoardGrid(5, 5, 1f, Vector2.zero);

            // 병합기 (2,2): 서·남에서 받아 동으로 낸다. 동쪽에 하류를 두지 않아 끝이 된다.
            grid.TryPlaceBeltElement(new Vector2Int(2, 2), BeltElementKind.Merger,
                new[] { PortFace.West, PortFace.South }, new[] { PortFace.East },
                FlowKind.Ammo, out _);

            // 상류 한 갈래.
            grid.TryPlaceBelt(new Vector2Int(1, 2), PortFace.West, PortFace.East, FlowKind.Ammo, out _);

            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            var merger = new Vector2Int(2, 2);

            // 병합기를 꽉 채운다.
            for (int i = 0; i < BeltItemFlow.MaxPerCell; i++)
            {
                Assert.IsTrue(flow.TryInsert(merger, FlowKind.Ammo));
                if (i < BeltItemFlow.MaxPerCell - 1)
                    flow.Tick(BeltItemFlow.MinGapCells / BeltItemFlow.CellsPerSecondTbd);
            }

            // 합류 지점이 더 못 받는다 — 이것이 상류가 서는 원인이다.
            Assert.IsFalse(flow.TryInsert(merger, FlowKind.Ammo),
                "합류량이 한 줄 용량을 넘으면 더 받지 못한다");

            // ⚠️ 상류가 **계속** 서 있는 것까지는 아직 검사할 수 없다.
            //    지금 모델은 라인의 끝에서 아이템을 무조건 배출하므로(소비처가 없어도)
            //    병합기가 곧 비워지고 자리가 다시 생긴다. 「소비처가 안 가져가면 쌓인다」는
            //    마운트 도착(4번 덩어리)이 들어와야 성립하며, 그때 이 검사를 늘린다.
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
