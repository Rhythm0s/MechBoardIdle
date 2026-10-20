using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>#190 S5 폭발탄 튜토리얼 (2026-10-19 사용자 확정).</summary>
    public sealed class ExplosiveTutorialRuleTests
    {
        [SetUp] public void SetUp() => TutorialSignals.Reset();
        [TearDown] public void TearDown() => TutorialSignals.Reset();

        [Test]
        public void 걸음은_차례대로_판_상태에서_나온다()
        {
            Assert.AreEqual(ExplosiveTutorialRule.Step.PowerMaterial, ExplosiveTutorialRule.Next(false, false, false, false, false));
            Assert.AreEqual(ExplosiveTutorialRule.Step.Belt, ExplosiveTutorialRule.Next(true, false, false, false, false));
            Assert.AreEqual(ExplosiveTutorialRule.Step.Sorter, ExplosiveTutorialRule.Next(true, true, false, false, false));
            Assert.AreEqual(ExplosiveTutorialRule.Step.Complex, ExplosiveTutorialRule.Next(true, true, true, false, false));
            Assert.AreEqual(ExplosiveTutorialRule.Step.ExplosiveRecipe, ExplosiveTutorialRule.Next(true, true, true, true, false));
            Assert.AreEqual(ExplosiveTutorialRule.Step.Done, ExplosiveTutorialRule.Next(true, true, true, true, true));
            Assert.AreEqual(new Vector2Int(6, 7), ExplosiveTutorialRule.CellOf(ExplosiveTutorialRule.Step.PowerMaterial));
            Assert.AreEqual(new Vector2Int(7, 7), ExplosiveTutorialRule.CellOf(ExplosiveTutorialRule.Step.Belt));
            Assert.AreEqual(new Vector2Int(8, 8), ExplosiveTutorialRule.CellOf(ExplosiveTutorialRule.Step.Sorter));
            Assert.AreEqual(new Vector2Int(8, 7), ExplosiveTutorialRule.CellOf(ExplosiveTutorialRule.Step.Complex));
        }

        [Test]
        public void 안내는_S5_파밍_끝_전_고철_모자라면_안_뜸()
        {
            var s = ExplosiveTutorialRule.Step.Complex;
            Assert.IsTrue(ExplosiveTutorialRule.IsActive(true, 5, true, s, true, 20d, 20d));
            Assert.IsFalse(ExplosiveTutorialRule.IsActive(true, 5, true, s, true, 19d, 20d), "고철 부족 = 막 없음");
            Assert.IsTrue(ExplosiveTutorialRule.IsActive(true, 5, true, ExplosiveTutorialRule.Step.Belt, false, 0d, 20d));
            Assert.IsFalse(ExplosiveTutorialRule.IsActive(true, 5, true, ExplosiveTutorialRule.Step.Done, false, 99d, 20d));
            Assert.IsFalse(ExplosiveTutorialRule.IsActive(true, 5, true, ExplosiveTutorialRule.Step.None, false, 99d, 20d), "모름 = 안 뜸");
            Assert.IsTrue(ExplosiveTutorialRule.IsActive(false, 5, true, s, false, 99d, 20d), "#245 — 바로가기(튜토리얼 안 마침)도 뜸 · 🗑️ 「안 뜸」");
            Assert.IsFalse(ExplosiveTutorialRule.IsActive(true, 6, true, s, false, 99d, 20d));
            Assert.IsFalse(ExplosiveTutorialRule.IsActive(true, 5, false, s, false, 99d, 20d));
        }

        [Test]
        public void 팔레트는_그_걸음_것만()
        {
            Assert.IsTrue(ExplosiveTutorialRule.AllowsNode(ExplosiveTutorialRule.Step.Complex, NodeType.MunitionsComplex));
            Assert.IsFalse(ExplosiveTutorialRule.AllowsNode(ExplosiveTutorialRule.Step.Complex, NodeType.Processing));
            Assert.IsFalse(ExplosiveTutorialRule.AllowsNode(ExplosiveTutorialRule.Step.Sorter, NodeType.MunitionsComplex));
            Assert.IsTrue(ExplosiveTutorialRule.AllowsElement(ExplosiveTutorialRule.Step.Sorter, BeltElementKind.Sorter));
            Assert.IsFalse(ExplosiveTutorialRule.AllowsElement(ExplosiveTutorialRule.Step.Sorter, BeltElementKind.Merger));

            TutorialSignals.ExplosiveActive = true;
            Assert.AreEqual(TutorialGate.Phase.Explosive, TutorialGate.Current);
            TutorialSignals.BoardViewOpen = true;
            TutorialSignals.BoardViewIsTutorialBoard = true;
            Assert.AreEqual(TutorialGate.Phase.ExplosiveBoard, TutorialGate.Current);
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.Remove), "지우기 닫힘 — 대상 칸은 탭 한 번에 덮어쓴다");
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.Module));
            Assert.AreEqual("복합 가공소가 열렸습니다", ExplosiveTutorialRule.GrantNotice);

            string bc = System.IO.File.ReadAllText("Assets/_Project/Scripts/Logistics/BoardController.cs");
            StringAssert.Contains("PublishExplosiveStep();", bc);
            StringAssert.Contains("if (target && replaceStep && ExplosiveReplace(xs, tap)) { }", bc);
        }
    }
}
