using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 온보딩 시작 보드 — **시작 80, 빈 칸을 채우면 100**(260831_V11 정정).
    ///
    /// 이 파일이 있는 이유: 종전에는 배치가 씬 생성기 안 좌표 리터럴이라
    /// 「정말 80이 나오는가」를 확인할 방법이 **씬을 열어 보는 것뿐**이었다.
    /// 배치를 <see cref="StartingBoard"/>로 빼고 여기서 숫자를 잡아 두면,
    /// 좌표 하나가 어긋나는 순간 배치모드가 깨진다.
    ///
    /// ⚠️ 여기서 재는 것은 **물류 출력**이다(마운트계수 미적용). 전투가 마운트계수를 곱한다.
    /// </summary>
    public sealed class StartingBoardTests
    {
        private const float D = 0.01f;

        private BalanceConfig _bal;

        [SetUp]
        public void SetUp()
        {
            _bal = AssetDatabase.LoadAssetAtPath<BalanceConfig>(
                "Assets/_Project/ScriptableObjects/BalanceConfig.asset");
            if (_bal == null || Node(StartingBoard.MuniId) == null)
                Assert.Ignore("자산 없음 — 먼저 밸런스·노드 생성 메뉴를 실행해야 한다.");
        }

        private static NodeDefinition Node(string id) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>(
                $"Assets/_Project/ScriptableObjects/Nodes/Node_{id}.asset");

        /// <summary>씬 생성기와 **같은 데이터**로 격자를 깐다 — 배치를 여기서 다시 적지 않는다.</summary>
        private static BoardGrid Build(bool fillEmptySlot)
        {
            // ⚠️ **실제 유효 셀 마스크를 쓴다.** 전부 유효한 격자로 재면 실루엣 밖 칸에
            // 노드를 둬도 테스트가 통과하고, 씬에서만 조용히 빠진다.
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero,
                PartLayout.BuildMask());

            foreach (StartingBoard.Slot slot in StartingBoard.Nodes) Place(g, slot);
            if (fillEmptySlot) Place(g, StartingBoard.FillsEmptySlot);

            foreach (StartingBoard.Run run in StartingBoard.Belts)
            {
                if (run.merger)
                    g.TryPlaceBeltElement(run.cell, BeltElementKind.Merger,
                        new[] { run.inFace }, new[] { run.outFace }, FlowKind.None, out _);
                else
                    g.TryPlaceBelt(run.cell, run.inFace, run.outFace, FlowKind.None, out _);
            }

            // 실제 경로와 같은 순서로 푼다: 면 → 품목.
            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        private static void Place(BoardGrid g, StartingBoard.Slot slot)
        {
            NodeDefinition def = Node(slot.nodeId);
            Assert.NotNull(def, slot.nodeId);
            Assert.IsTrue(g.TryPlace(slot.cell, def, out NodeInstance placed),
                $"{slot.nodeId} @ {slot.cell} — 놓을 수 없는 칸이다");
            if (placed != null) placed.AmmoKind = slot.ammo;
        }

        /// <summary>보드가 만드는 물류 출력. 이어진 노드만 센다(260829_V03 A안).</summary>
        private float Output(BoardGrid g)
        {
            NetworkAggregate agg = LogisticsNetwork.Aggregate(g, LogisticsReach.ConnectedNodes(g));

            var lines = new List<MunitionsLine>
            {
                new MunitionsLine(AmmoKind.Pierce, _bal.LineSpecOf(AmmoKind.Pierce), 20f, agg.muniPierce),
                new MunitionsLine(AmmoKind.Split, _bal.LineSpecOf(AmmoKind.Split), 25f, agg.muniSplit),
                new MunitionsLine(AmmoKind.Explosive, _bal.LineSpecOf(AmmoKind.Explosive), 50f, agg.muniExplosive),
            };
            return AmmoLineProduction.TotalOutput(lines, _bal.muniPerNode);
        }

        // ---- 숫자 ----

        /// <summary>**시작 80.** 관통 4노드 × 1발/초 × 발당 20.</summary>
        [Test]
        public void StartsAtEighty()
        {
            Assert.AreEqual(80f, Output(Build(fillEmptySlot: false)), D);
        }

        /// <summary>
        /// **빈 칸을 채우면 100.** 이것이 온보딩의 내용물이다 — 배치가 출력을 올린다는 것이
        /// 보드 위에서 한 번에 보여야 한다.
        /// </summary>
        [Test]
        public void FillingTheEmptySlot_ReachesOneHundred()
        {
            Assert.AreEqual(100f, Output(Build(fillEmptySlot: true)), D);
        }

        /// <summary>
        /// 채우는 칸은 **팔레트 기본값과 같은 관통**이어야 한다. 기본이 다른 탄종이면
        /// 플레이어가 그냥 놓았을 때 100이 안 나온다.
        /// </summary>
        [Test]
        public void TheFillIsPierce_LikeTheDefault()
        {
            Assert.AreEqual(AmmoKind.Pierce, StartingBoard.FillsEmptySlot.ammo);
            Assert.AreEqual(StartingBoard.EmptySlot, StartingBoard.FillsEmptySlot.cell);
        }

        /// <summary>빈 칸은 **비어 있다** — 시작 배치에 그 칸이 들어 있으면 온보딩이 사라진다.</summary>
        [Test]
        public void TheEmptySlotIsActuallyEmpty()
        {
            foreach (StartingBoard.Slot slot in StartingBoard.Nodes)
                Assert.AreNotEqual(StartingBoard.EmptySlot, slot.cell, "비워 둔 칸에 노드가 있다");

            Assert.IsNull(Build(fillEmptySlot: false).GetAt(StartingBoard.EmptySlot));
        }

        // ---- 배선이 실제로 이어지는가 ----

        /// <summary>
        /// 관통 4노드가 **전부 이어져 있다.** 하나라도 새면 80이 아니라 60이 나오는데,
        /// 숫자만 보면 「스펙이 그런가 보다」로 지나칠 수 있어 개수로도 잡아 둔다.
        /// </summary>
        [Test]
        public void AllFourMunitionsAreConnected()
        {
            BoardGrid g = Build(fillEmptySlot: false);
            NetworkAggregate agg = LogisticsNetwork.Aggregate(g, LogisticsReach.ConnectedNodes(g));

            Assert.AreEqual(4, agg.muniPierce, "관통 4노드");
            Assert.IsTrue(agg.hasCore, "코어가 이어져 있다");
        }

        /// <summary>다섯째 줄까지 병합기가 받는다 — 채웠을 때 5노드가 다 세어져야 100이 나온다.</summary>
        [Test]
        public void TheFifthLineIsWired_NotJustPlaceable()
        {
            BoardGrid g = Build(fillEmptySlot: true);
            NetworkAggregate agg = LogisticsNetwork.Aggregate(g, LogisticsReach.ConnectedNodes(g));

            Assert.AreEqual(5, agg.muniPierce, "다섯째 줄도 이어진다");
        }

        // ---- 일감률·전력 ----

        /// <summary>
        /// 시작 보드는 **전부 일한다.** 관통 스펙 5에 4노드라 초과분이 없다 —
        /// 온보딩 첫 화면에 「노는 중」이 떠 있으면 안 되는 것을 고르라는 뜻이 된다.
        /// </summary>
        [Test]
        public void NothingIsIdleAtTheStart()
        {
            BoardGrid g = Build(fillEmptySlot: false);
            WorkloadRate.Result w = WorkloadRate.Compute(g, LogisticsReach.ConnectedNodes(g), _bal);

            Assert.AreEqual(1f, w.average, D, "일감률 평균 100%");
        }

        /// <summary>빈 칸을 채워도 스펙 5 안쪽이라 여전히 아무도 안 논다.</summary>
        [Test]
        public void StillNothingIdleAfterFilling()
        {
            BoardGrid g = Build(fillEmptySlot: true);
            WorkloadRate.Result w = WorkloadRate.Compute(g, LogisticsReach.ConnectedNodes(g), _bal);

            Assert.AreEqual(1f, w.average, D, "다섯째까지 스펙 안쪽");
        }

        /// <summary>
        /// **전력이 모자라지 않는다.** 모자라면 출력이 감쇠해 80이 80으로 안 보인다 —
        /// 온보딩 첫 화면에서 「왜 낮지」가 생기면 배치를 가르치는 판이 아니게 된다.
        /// </summary>
        [Test]
        public void PowerCovers_TheFilledBoard()
        {
            BoardGrid g = Build(fillEmptySlot: true);
            ICollection<Vector2Int> connected = LogisticsReach.ConnectedNodes(g);
            NetworkAggregate agg = LogisticsNetwork.Aggregate(g, connected,
                WorkloadRate.Compute(g, connected, _bal));

            Assert.GreaterOrEqual(agg.powerSupply, agg.powerDraw,
                $"공급 {agg.powerSupply} < 수요 {agg.powerDraw}");
        }
    }
}
