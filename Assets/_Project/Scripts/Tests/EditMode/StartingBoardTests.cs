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
        /// 전력 수요·공급을 **숫자로 못 박는다.** 대당 전력 7종과 발전량 10이 확정되면서
        /// (260901_V02 §2층) 전력이 처음으로 실제 제약이 됐다.
        ///
        /// 시작 보드에는 **가공도 저장도 없다** — 코어 1 + 에너지 1 + 군수 4뿐이다.
        /// 수요 = 군수 4 × 2 + 에너지 1 × 1 = 9. 공급 = 에너지 1대 × 10 = 10.
        /// </summary>
        [Test]
        public void PowerCovers_TheStartingBoard()
        {
            NetworkAggregate agg = Aggregate(Build(fillEmptySlot: false));

            Assert.AreEqual(9f, agg.powerDraw, D, "군수 4×2 + 에너지 1×1");
            Assert.AreEqual(10f, agg.powerSupply, D, "에너지 1대 × 10");
            Assert.GreaterOrEqual(agg.powerSupply, agg.powerDraw, "시작은 전력이 남는다");
        }

        /// <summary>
        /// ⚠️ **빈 칸을 채우면 전력이 모자란다.** 수요 11 > 공급 10.
        ///
        /// 260901_V02 §3층은 「시작 66.7 → 발전 늘려 80 → 채워서 100」을 의도했는데,
        /// 그 산술은 **가공 2 · 저장 1을 가정한 값**이다. 실제 시작 보드에는 둘 다 없어
        /// 순서가 뒤집힌다: **시작은 충분하고, 채운 순간 모자라진다.**
        ///
        /// 여기서는 판단하지 않고 현실만 기록한다 — 시작 보드를 어떻게 할지는 설계 판정이다.
        /// </summary>
        [Test]
        public void FillingTheEmptySlot_MakesPowerShort()
        {
            NetworkAggregate agg = Aggregate(Build(fillEmptySlot: true));

            Assert.AreEqual(11f, agg.powerDraw, D, "군수 5×2 + 에너지 1×1");
            Assert.AreEqual(10f, agg.powerSupply, D);
            Assert.Less(agg.powerSupply, agg.powerDraw, "채우면 모자란다");
        }

        /// <summary>
        /// 전력이 모자란 만큼 출력이 깎인다. 100 × (10 ÷ 11) ≈ **90.9**.
        ///
        /// ⚠️ S1 요구치가 90이므로 **발전소를 늘리지 않아도 겨우 통과한다.**
        /// 설계가 의도한 「세 동작을 다 마쳐야 90을 넘는다」가 이 배치에서는 성립하지 않는다.
        /// </summary>
        [Test]
        public void FilledOutput_IsThrottledToAboutNinetyOne()
        {
            NetworkAggregate agg = Aggregate(Build(fillEmptySlot: true));

            float efficiency = Mathf.Clamp01(agg.powerSupply / agg.powerDraw);
            float throttled = 100f * efficiency;

            Assert.AreEqual(90.9f, throttled, 0.1f, "100 × 10/11");
            Assert.Greater(throttled, 90f, "요구 90을 발전 증설 없이 넘는다 — 설계 보고 대상");
        }

        private NetworkAggregate Aggregate(BoardGrid g)
        {
            ICollection<Vector2Int> connected = LogisticsReach.ConnectedNodes(g);
            return LogisticsNetwork.Aggregate(g, connected, WorkloadRate.Compute(g, connected, _bal));
        }

        /// <summary>
        /// 시작 보드에 **가공·저장이 없다**(260901_V02 §3층 산술의 입력).
        /// 설계는 가공 2 · 저장 1을 가정했다 — 그 차이가 위 두 테스트의 원인이다.
        /// </summary>
        [Test]
        public void StartingBoard_HasNoProcessingOrStorage()
        {
            int proc = 0, stor = 0;
            foreach (StartingBoard.Slot slot in StartingBoard.Nodes)
            {
                if (slot.nodeId == "proc") proc++;
                if (slot.nodeId == "stor") stor++;
            }

            Assert.AreEqual(0, proc, "가공 노드 없음");
            Assert.AreEqual(0, stor, "저장 노드 없음");
        }
    }
}
