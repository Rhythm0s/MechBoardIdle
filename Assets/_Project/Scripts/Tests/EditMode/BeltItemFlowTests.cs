using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

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
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _created) Object.DestroyImmediate(o);
            _created.Clear();
        }

        // 서→동 직선 벨트를 x=0..n-1에 깐다. 마지막 칸의 출력면은 동쪽이므로 그 앞이 라인의 끝이다.
        // **끝에 소비처가 없다** — 그래서 물건이 거기 쌓인다(2026-09-03 개정).
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

        // 서쪽 면으로 탄약을 받는 소비 노드. 벨트 끝에 두면 도착지가 된다.
        private NodeDefinition MakeConsumer()
        {
            var def = ScriptableObject.CreateInstance<NodeDefinition>();
            def.type = NodeType.Storage;
            def.implemented = true;
            def.ports = new List<NodePort>
            {
                new NodePort(PortFace.West, PortIO.Input, FlowKind.Ammo),
            };
            def.recipes = new List<NodeRecipe>();
            _created.Add(def);
            return def;
        }

        // 위 StraightLine에 소비처를 하나 붙인 것. 마지막 벨트의 동쪽 이웃이 노드다.
        private BoardGrid StraightLineIntoConsumer(int length)
        {
            BoardGrid grid = StraightLine(length);
            grid.TryPlace(new Vector2Int(length, 1), MakeConsumer(), out _);
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

        /// <summary>소비처(노드)에 닿으면 도착으로 센다. 품목별로도 센다.</summary>
        [Test]
        public void EndOfLine_WithConsumer_CountsAsArrived()
        {
            BoardGrid grid = StraightLineIntoConsumer(1);
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            flow.TryInsert(new Vector2Int(0, 1), FlowKind.Ammo);
            Assert.AreEqual(0, flow.DeliveredCount);

            flow.Tick(SecondsPerCell);

            Assert.AreEqual(1, flow.DeliveredCount, "소비처가 받았으면 도착이다");
            Assert.AreEqual(1, flow.ArrivedOf(FlowKind.Ammo), "품목별로도 세어야 한다");
            Assert.AreEqual(0, flow.ItemsAt(new Vector2Int(0, 1)).Count, "벨트에서 빠졌다");
            Assert.AreEqual(0, flow.ItemsAt(new Vector2Int(1, 1)).Count,
                "노드 칸에는 아이템을 얹지 않는다 — 노드는 벨트가 아니다");
        }

        /// <summary>
        /// **이 테스트가 4번 덩어리의 본체다.** 소비처 없는 라인 끝에서는 물건이 사라지지 않는다.
        ///
        /// 종전에는 다음 칸이 없으면 무조건 배출로 세고 지웠다. 그러면 벨트를 허공으로 뻗어
        /// 놓아도 물건이 계속 빠져나가, 조립 시스템 문서가 말하는 **「단절 — 출력 면에 붙어
        /// 멈춰 있다」가 화면에 성립하지 않았다.**
        /// </summary>
        [Test]
        public void EndOfLine_WithoutConsumer_ItemStopsAndStacks()
        {
            BoardGrid grid = StraightLine(1);
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            var cell = new Vector2Int(0, 1);
            flow.TryInsert(cell, FlowKind.Ammo);
            flow.Tick(SecondsPerCell * 4f); // 끝까지 밀고도 남을 시간

            Assert.AreEqual(0, flow.DeliveredCount, "가져가는 곳이 없으면 도착이 아니다");
            Assert.AreEqual(1, flow.ItemsAt(cell).Count, "그 자리에 남아 있어야 한다");
            Assert.IsTrue(flow.IsBlocked(cell), "출력면에 붙어 멈춘 상태다 — 이것이 단절이다");
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

            // **계속** 서 있는지까지 본다 (2026-09-03 · 4번 덩어리로 검사를 늘렸다).
            // 종전에는 라인 끝에서 무조건 배출해 병합기가 곧 비워졌고, 그래서 이 검사를 둘 수 없었다.
            flow.Tick(SecondsPerCell * 10f);

            Assert.AreEqual(0, flow.DeliveredCount, "동쪽에 소비처가 없으므로 나간 것이 없다");
            Assert.IsFalse(flow.TryInsert(merger, FlowKind.Ammo),
                "시간이 지나도 자리가 안 생긴다 — 이것이 상류가 계속 서는 이유다");

            // 상류 벨트도 같이 선다. 신호를 보내서가 아니라 앞이 안 비어서다.
            var upstream = new Vector2Int(1, 2);
            for (int i = 0; i < BeltItemFlow.MaxPerCell; i++)
            {
                flow.TryInsert(upstream, FlowKind.Ammo);
                flow.Tick(BeltItemFlow.MinGapCells / BeltItemFlow.CellsPerSecondTbd);
            }
            flow.Tick(SecondsPerCell * 10f);

            Assert.IsTrue(flow.IsBlocked(upstream), "상류도 출력면에 붙어 멈춘다");
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
