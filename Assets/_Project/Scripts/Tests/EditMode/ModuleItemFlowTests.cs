using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **모듈 배율이 실제 흐름에 든다** (사용자 확정 #166 · 2026-10-18 · 「모듈을 설치했지만 전투력이 안 오른다」).
    /// 🗑️ 구 거동 「모듈 배율은 집계에만」 폐기 — 낱개 흐름(`BoardItemTick`)도 모듈을 곱한다.
    /// </summary>
    public sealed class ModuleItemFlowTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _created) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private static ModuleDefinition Module(string id) =>
            AssetDatabase.LoadAssetAtPath<ModuleDefinition>($"Assets/_Project/ScriptableObjects/Modules/Module_{id}.asset");

        // 동쪽으로 내보내는 생산 노드 — 재료를 안 먹는다(산출 배율만 본다).
        private NodeInstance PlaceProducer(BoardGrid grid, Vector2Int cell, float perSec)
        {
            var def = ScriptableObject.CreateInstance<NodeDefinition>();
            def.type = NodeType.MunitionsBasic;
            def.implemented = true;
            def.ports = new List<NodePort> { new NodePort(PortFace.East, PortIO.Output, FlowKind.Ammo) };
            def.recipes = new List<NodeRecipe>
            {
                new NodeRecipe { kind = RecipeKind.Ammo, displayName = "탄약", output = FlowKind.Ammo,
                                 outputPerSec = perSec, stackLimitTbd = 1000f, implemented = true },
            };
            _created.Add(def);
            grid.TryPlace(cell, def, out NodeInstance node);
            node.SelectRecipe(RecipeKind.Ammo);
            return node;
        }

        private float ProducedIn(NodeInstance node, BoardGrid grid, float seconds)
        {
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);
            for (int i = 0; i < Mathf.RoundToInt(seconds / 0.1f); i++) BoardItemTick.Step(grid, flow, 0.1f);
            return node.OutputBuffer;   // 벨트가 없어 나간 것 없이 버퍼에 쌓인다
        }

        [Test]
        public void 생산량_모듈을_붙이면_같은_시간에_1_5배_만든다()
        {
            ModuleDefinition m = Module("M");
            if (m == null) Assert.Ignore("모듈 자산 없음");

            var g0 = new BoardGrid(3, 3, 1f, Vector2.zero);
            NodeInstance bare = PlaceProducer(g0, new Vector2Int(0, 1), 2f);
            var g1 = new BoardGrid(3, 3, 1f, Vector2.zero);
            NodeInstance modded = PlaceProducer(g1, new Vector2Int(0, 1), 2f);
            Assert.IsTrue(modded.TryAttachModule(m));

            float a = ProducedIn(bare, g0, 10f);
            float b = ProducedIn(modded, g1, 10f);
            Assert.AreEqual(20f, a, 0.01f, "모듈 없음 = 2/초 × 10초");
            Assert.AreEqual(a * m.outputMultiplier, b, 0.01f, "생산량 모듈 = 산출 배율만큼 더");
        }

        [Test]
        public void 생산량_모듈은_재료를_그대로_먹고_생산속도_모듈은_배율만큼_먹는다()
        {
            ModuleDefinition m = Module("M"), r = Module("R");
            if (m == null || r == null) Assert.Ignore("모듈 자산 없음");

            var recipe = new NodeRecipe
            {
                kind = RecipeKind.Ammo, output = FlowKind.Ammo, outputPerSec = 1f, stackLimitTbd = 1000f, implemented = true,
                inputs = new List<RecipeInput> { new RecipeInput { kind = FlowKind.CoreEnergy, perOutput = 1f } },
            };
            var stock = new Dictionary<FlowKind, float> { { FlowKind.CoreEnergy, 1000f } };

            // M: 산출 ×1.5 · 재료 ×1 → 산출 하나당 재료 1/1.5
            float rateM = m.outputMultiplier, perM = m.inputMultiplier / rateM;
            float madeM = NodeProduction.ProduceScaled(recipe, 0f, 10f, stock, rateM, perM);
            Assert.AreEqual(15f, madeM, 0.01f);
            Assert.AreEqual(10f, madeM * perM, 0.01f, "M 은 모듈 없을 때와 같은 재료(10)로 15개");

            // R: 산출 ×1.5 · 재료 ×1.5 → 산출 하나당 재료 1
            float rateR = r.outputMultiplier, perR = r.inputMultiplier / rateR;
            float madeR = NodeProduction.ProduceScaled(recipe, 0f, 10f, stock, rateR, perR);
            Assert.AreEqual(15f, madeR, 0.01f);
            Assert.AreEqual(15f, madeR * perR, 0.01f, "R 은 산출만큼 재료도 1.5배");
        }

        [Test]
        public void 합체만이면_종전_식과_같다()
        {
            var recipe = new NodeRecipe
            {
                kind = RecipeKind.Ammo, output = FlowKind.Ammo, outputPerSec = 1f, stackLimitTbd = 1000f, implemented = true,
                inputs = new List<RecipeInput> { new RecipeInput { kind = FlowKind.CoreEnergy, perOutput = 1f } },
            };
            var stock = new Dictionary<FlowKind, float> { { FlowKind.CoreEnergy, 3f } };
            float old = NodeProduction.Produce(recipe, 0f, 10f, stock, 2f);
            float now = NodeProduction.ProduceScaled(recipe, 0f, 10f, stock, 2f, 1f / 2f);
            Assert.AreEqual(old, now, 1e-4f, "재료 한도 · 산출 배율이 종전 합체 겹과 같다");
        }

        private static BoardGrid StartBoard(ModuleDefinition[] mods)
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero, PartLayout.BuildMask());
            StartingBoard.Apply(g, id => AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/_Project/ScriptableObjects/Nodes/Node_{id}.asset"));
            StartingBoard.Run f = StartingBoard.FillsEmptySlot;
            g.TryPlaceBelt(f.cell, f.inFace, f.outFace, FlowKind.None, out _);
            NodeInstance t4 = g.GetAt(new Vector2Int(7, 8));
            if (mods != null && t4 != null) foreach (var m in mods) t4.TryAttachModule(m);
            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        private static (int arrived, float power) Measure(BoardGrid g, float seconds)
        {
            var flow = new BeltItemFlow();
            flow.Rebuild(g);
            var delivery = new MountDelivery();
            int arrived = 0;
            for (int i = 0; i < Mathf.RoundToInt(seconds / 0.05f); i++)
            {
                BoardItemTick.Step(g, flow, 0.05f, 1f);
                arrived += flow.PendingMountArrivals.Count;
                delivery.Observe(flow.PendingMountArrivals, k => 1f, 0.05f);
                flow.ClearPendingMountArrivals();
            }
            return (arrived, delivery.TotalPower);
        }

        /// <summary>
        /// **시작 보드 T4 칸(7,8 · 기초 가공소)에 모듈을 붙이면 마운트 도착이 는다** — 「물류 실제」 = 전투력의 원천.
        /// </summary>
        [Test]
        public void 시작_보드_T4_칸에_모듈을_붙이면_마운트_도착이_는다()
        {
            ModuleDefinition m = Module("M"), r = Module("R");
            if (m == null || r == null || AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/_Project/ScriptableObjects/Nodes/Node_{StartingBoard.MuniId}.asset") == null)
                Assert.Ignore("자산 없음");
            var a0 = Measure(StartBoard(null), 60f);
            var a1 = Measure(StartBoard(new[] { m }), 60f);
            var a2 = Measure(StartBoard(new[] { m, r }), 60f);
            TestContext.WriteLine($"[#166 시작 보드 60초 마운트 도착] 모듈 0 = {a0.arrived} · M = {a1.arrived} · M+R = {a2.arrived}");
            Assert.Greater(a1.arrived, a0.arrived, "생산량 모듈 하나로 도착이 늘어야 한다");
            Assert.GreaterOrEqual(a2.arrived, a1.arrived, "둘이면 하나보다 적지 않다");
        }

        [Test]
        public void 낱개_흐름이_모듈을_본다_소스_고정()
        {
            string src = System.IO.File.ReadAllText("Assets/_Project/Scripts/Core/BoardItemTick.cs");
            StringAssert.Contains("float rate = node.ModuleOutputMultiplier * merge;", src);
            StringAssert.Contains("float perOutput = node.ModuleInputMultiplier / Mathf.Max(1e-6f, rate);", src);
            StringAssert.Contains("NodeProduction.ConsumeFor(recipe, made * perOutput, node.InputBuffer);", src);
        }
    }
}
