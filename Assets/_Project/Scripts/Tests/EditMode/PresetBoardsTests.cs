using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **바로가기 S5 · S6 = 폭발탄 프리셋** (사용자 확정 #167 · #168 · 2026-10-18).
    /// </summary>
    public sealed class PresetBoardsTests
    {
        private static NodeDefinition Node(string id) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/_Project/ScriptableObjects/Nodes/Node_{id}.asset");

        private static BoardGrid PresetBoard(out bool ok)
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero, PartLayout.BuildMask());
            StartingBoard.Apply(g, Node);
            StartingBoard.Run f = StartingBoard.FillsEmptySlot;
            g.TryPlaceBelt(f.cell, f.inFace, f.outFace, FlowKind.None, out _);
            ok = PresetBoards.ApplyExplosive(g, Node);
            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        [Test]
        public void S5와_S6_점프만_프리셋을_건다()
        {
            for (int n = 0; n <= 4; n++) Assert.IsFalse(PresetBoards.ExplosiveOnJump(n), $"S{n}");
            Assert.IsFalse(PresetBoards.ExplosiveOnJump(5), "#250 — S5 점프 = 튜토리얼 시작판(🗑️ 프리셋)");
            Assert.IsTrue(PresetBoards.ExplosiveOnJump(6));
        }

        [Test]
        public void 프리셋은_시작_보드_위에_다_서고_폭발탄이_마운트에_닿는다()
        {
            if (Node(StartingBoardB.ComplexId) == null) Assert.Ignore("자산 없음");
            BoardGrid g = PresetBoard(out bool ok);
            Assert.IsTrue(ok, "프리셋 칸이 다 서야 한다");

            NodeInstance x = g.GetAt(PresetBoards.ExplosiveNodeCell);
            Assert.AreEqual(RecipeKind.ExplosiveAmmo, x.CurrentRecipe.kind);
            Assert.AreEqual(RecipeKind.PowerMaterial, g.GetAt(PresetBoards.PowerMaterialCell).CurrentRecipe.kind);

            var flow = new BeltItemFlow();
            flow.Rebuild(g);
            for (int i = 0; i < 1200; i++) BoardItemTick.Step(g, flow, 0.05f, 1f);
            TestContext.WriteLine($"[#167 프리셋 60초] 폭발탄 {flow.MountArrivedOf(FlowKind.ExplosiveAmmo)} · 표준탄 {flow.MountArrivedOf(FlowKind.StandardAmmo)}");
            Assert.Greater(flow.MountArrivedOf(FlowKind.ExplosiveAmmo), 0, "폭발탄이 마운트에 닿아야 한다");
            Assert.Greater(flow.MountArrivedOf(FlowKind.StandardAmmo), 0, "표준탄도 마운트에 닿아야 한다(사용자 확정 #169 · 분류기 동 갈래)");
            Assert.AreEqual(1, x.Rotation, "복합 가공소는 한 번 돌려 북 = 표준탄 · 서 = 발전재료 · 남 = 산출");
        }

        [Test]
        public void 바로가기와_보드가_프리셋_요청을_주고받는다_소스_고정()
        {
            string sc = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/ReviewerShortcuts.cs");
            StringAssert.Contains("PresetBoards.ExplosiveRequested = true;", sc);
            string bc = System.IO.File.ReadAllText("Assets/_Project/Scripts/Logistics/BoardController.cs");
            StringAssert.Contains("if (PresetBoards.ExplosiveRequested)", bc);
            StringAssert.Contains("PresetBoards.ApplyExplosive(g, FindStartingNode);", bc);
        }
    }
}
