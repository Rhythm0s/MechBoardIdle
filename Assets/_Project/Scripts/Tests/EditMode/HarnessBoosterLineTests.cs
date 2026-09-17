using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using MBI.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 하네스 전용 추진제 줄이 **실제로 도는가** (2026-09-17 · `260917_W02` 2-2).
    ///
    /// ⚠️⚠️ **이 시험이 지키는 것은 「측정이 뜻을 갖는가」다.** 줄을 얹었다고 적어 두고
    /// 실은 한 칸이 안 놓였거나 조합표가 표준탄으로 남아 있으면, 나 판은 가 판과 **같은 수**가
    /// 나오고 나는 그것을 「부스터를 놓아도 안 변한다」로 보고하게 된다.
    /// 09-16 에 겪은 「프로브가 사람이 지나는 문을 안 지난다」와 같은 자리다.
    ///
    /// 📌 그래서 보는 것은 배치가 아니라 **집계**다 — 부스터 대수와 추진제 산출.
    /// </summary>
    public sealed class HarnessBoosterLineTests
    {
        private const string NodeRoot = "Assets/_Project/ScriptableObjects/Nodes";

        private static NodeDefinition Node(string id)
            => AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodeRoot}/Node_{id}.asset");

        private static BoardGrid BaseA()
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero,
                PartLayout.BuildMask(), MountOwner.RobotA);

            foreach (StartingBoard.Slot s in StartingBoard.Nodes)
                Assert.IsTrue(g.TryPlace(s.cell, Node(s.nodeId), out _), $"{s.nodeId} {s.cell}");
            foreach (StartingBoard.Run r in StartingBoard.Belts) Place(g, r);
            Place(g, StartingBoard.FillsEmptySlot);

            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        private static BoardGrid BaseB()
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero,
                PartLayout.BuildMask(), MountOwner.RobotB);

            foreach (StartingBoardB.Slot s in StartingBoardB.Nodes)
            {
                Assert.IsTrue(g.TryPlace(s.cell, Node(s.nodeId), out NodeInstance placed),
                    $"{s.nodeId} {s.cell}");
                if (s.recipe != RecipeKind.None) Assert.IsTrue(placed.SelectRecipe(s.recipe));
            }
            foreach (StartingBoard.Run r in StartingBoardB.Belts) Place(g, r);

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

        private static NetworkAggregate Aggregate(BoardGrid g)
        {
            ICollection<Vector2Int> connected = LogisticsReach.ConnectedNodes(g);
            WorkloadRate.Result work = WorkloadRate.Compute(g, connected, null);
            return LogisticsNetwork.Aggregate(g, connected, work);
        }

        [Test]
        public void A_판에_줄이_다_놓인다()
        {
            BoardGrid g = BaseA();
            Assert.IsTrue(HarnessBoosterLine.ApplyToA(g, out string why), why);
        }

        [Test]
        public void B_판에_줄이_다_놓인다()
        {
            BoardGrid g = BaseB();
            Assert.IsTrue(HarnessBoosterLine.ApplyToB(g, out string why), why);
        }

        [Test]
        public void A_판이_부스터_둘과_추진제를_집계에_낸다()
        {
            BoardGrid g = BaseA();
            Assert.IsTrue(HarnessBoosterLine.ApplyToA(g, out string why), why);

            NetworkAggregate a = Aggregate(g);
            Assert.AreEqual(HarnessBoosterLine.BoosterCount, a.boosterCount,
                "부스터가 집계에 안 잡혔다 — 안 이어졌으면 `connectedOnly` 에서 빠진다");
            Assert.Greater(a.propellantProduce, 0f,
                "추진제 산출이 0 이다 — 기초 군수가 기본값(표준탄)으로 돌고 있다");
        }

        [Test]
        public void B_판이_부스터_둘과_추진제를_집계에_낸다()
        {
            BoardGrid g = BaseB();
            Assert.IsTrue(HarnessBoosterLine.ApplyToB(g, out string why), why);

            NetworkAggregate a = Aggregate(g);
            Assert.AreEqual(HarnessBoosterLine.BoosterCount, a.boosterCount);
            Assert.Greater(a.propellantProduce, 0f);
        }

        [Test]
        public void 줄을_얹어도_전력이_모자라지_않는다()
        {
            // ⚠️ 설계가 물은 것이 이것이다 — 「에너지 노드를 더 놓았는지 적어 달라」.
            //    모자라면 **판 전체가 느려져** 가 판과 나 판이 다른 이유가 둘이 된다.
            BoardGrid g = BaseA();
            Assert.IsTrue(HarnessBoosterLine.ApplyToA(g, out string why), why);

            NetworkAggregate a = Aggregate(g);
            Assert.GreaterOrEqual(a.powerSupply, a.powerDraw,
                $"전력이 모자란다 — 공급 {a.powerSupply} · 수요 {a.powerDraw}. "
                + "에너지 노드를 더 놓아야 하고, 그 사실을 보고에 적어야 한다");
        }

        [Test]
        public void 줄을_안_얹으면_부스터가_없다()
        {
            // 가 판의 전제 — **회피가 한 번도 안 나야 한다**(W02 2-2 「0이어야 한다」).
            NetworkAggregate a = Aggregate(BaseA());
            Assert.AreEqual(0, a.boosterCount, "현행 A 시작 보드에 부스터가 있다");
            Assert.AreEqual(0f, a.propellantProduce, 1e-6f);
        }
    }
}
