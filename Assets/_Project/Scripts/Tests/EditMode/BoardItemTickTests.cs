using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 생산 → 출력 버퍼 → 벨트 (2026-09-03 · `260903_W01` 4-3).
    ///
    /// **문서와 구현 불일치 3번을 고정한다.** `NodeInstance.OutputBuffer`와 `NodeProduction`은
    /// 전부터 있었으나 부르는 곳이 0건이었다 — 되돌아가면 여기서 걸린다.
    /// </summary>
    public sealed class BoardItemTickTests
    {
        private const float Delta = 1e-3f;
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _created) Object.DestroyImmediate(o);
            _created.Clear();
        }

        // 동쪽으로 탄약을 내보내는 생산 노드. 버퍼 상한을 받아 「가득 참」을 만들 수 있게 둔다.
        private NodeDefinition MakeProducer(float perSec, float stackLimit)
        {
            var def = ScriptableObject.CreateInstance<NodeDefinition>();
            def.type = NodeType.Munitions;
            def.implemented = true;
            def.ports = new List<NodePort>
            {
                new NodePort(PortFace.East, PortIO.Output, FlowKind.Ammo),
            };
            def.recipes = new List<NodeRecipe>
            {
                new NodeRecipe
                {
                    kind = RecipeKind.Ammo,
                    displayName = "탄약",
                    output = FlowKind.Ammo,
                    outputPerSec = perSec,
                    stackLimitTbd = stackLimit,
                    implemented = true,
                },
            };
            _created.Add(def);
            return def;
        }

        private NodeInstance PlaceProducer(BoardGrid grid, Vector2Int cell, float perSec, float stackLimit)
        {
            grid.TryPlace(cell, MakeProducer(perSec, stackLimit), out NodeInstance node);
            node.SelectRecipe(RecipeKind.Ammo);
            return node;
        }

        // 서쪽 면으로 탄약을 받기만 하는 노드. 라인 끝에 두면 도착지가 된다.
        private void PlaceConsumer(BoardGrid grid, Vector2Int cell)
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
            grid.TryPlace(cell, def, out _);
        }

        /// <summary>
        /// **가져가는 곳이 없으면 버퍼가 차고 그 노드가 멈춘다.**
        /// 이것이 상류 전파의 본체다 — 막힘이 거슬러 오르는 것이 아니라
        /// 각자 자기 앞만 보는데 결과적으로 거슬러 오른다.
        /// </summary>
        [Test]
        public void NoBelt_BufferFillsAndNodeStalls()
        {
            var grid = new BoardGrid(4, 3, 1f, Vector2.zero);
            NodeInstance node = PlaceProducer(grid, new Vector2Int(0, 1), perSec: 10f, stackLimit: 5f);

            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            for (int i = 0; i < 10; i++) BoardItemTick.Step(grid, flow, 0.1f);

            Assert.AreEqual(5f, node.OutputBuffer, Delta, "상한에서 멈춰야 한다");
            Assert.IsTrue(NodeProduction.IsStalled(node.CurrentRecipe, node.OutputBuffer),
                "가득 찬 버퍼는 정지 상태다");
        }

        /// <summary>가져가는 곳이 있으면 버퍼가 차지 않는다. 위 테스트와 짝이다.</summary>
        [Test]
        public void WithBelt_BufferDrains()
        {
            var grid = new BoardGrid(6, 3, 1f, Vector2.zero);
            NodeInstance node = PlaceProducer(grid, new Vector2Int(0, 1), perSec: 2f, stackLimit: 5f);

            // 노드 동쪽에 벨트를 두 칸 깔고 **그 끝에 소비처를 둔다.**
            for (int x = 1; x <= 2; x++)
            {
                grid.TryPlaceBelt(new Vector2Int(x, 1),
                    PortFace.West, PortFace.East, FlowKind.Ammo, out _);
            }
            PlaceConsumer(grid, new Vector2Int(3, 1));

            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            for (int i = 0; i < 20; i++) BoardItemTick.Step(grid, flow, 0.1f);

            Assert.Less(node.OutputBuffer, 5f, "흘러 나가므로 상한까지 차지 않는다");
            Assert.Greater(flow.DeliveredCount, 0, "소비처로 도착한 것이 있어야 한다");
            Assert.AreEqual(flow.DeliveredCount, flow.ArrivedOf(FlowKind.Ammo),
                "탄약만 흘렀으므로 총계와 품목별이 같다");
        }

        // 코어 에너지를 먹어 탄약을 내는 노드. 재료 체인의 최소 단위다.
        private NodeInstance PlaceEater(BoardGrid grid, Vector2Int cell, float perSec, float perOutput)
        {
            var def = ScriptableObject.CreateInstance<NodeDefinition>();
            def.type = NodeType.Munitions;
            def.implemented = true;
            def.ports = new List<NodePort>
            {
                new NodePort(PortFace.West, PortIO.Input, FlowKind.Power),
                new NodePort(PortFace.East, PortIO.Output, FlowKind.Ammo),
            };
            def.recipes = new List<NodeRecipe>
            {
                new NodeRecipe
                {
                    kind = RecipeKind.Ammo,
                    displayName = "탄약",
                    inputs = new List<RecipeInput>
                    {
                        new RecipeInput { kind = FlowKind.Power, perOutput = perOutput },
                    },
                    output = FlowKind.Ammo,
                    outputPerSec = perSec,
                    stackLimitTbd = 100f,
                    implemented = true,
                },
            };
            _created.Add(def);
            grid.TryPlace(cell, def, out NodeInstance node);
            node.SelectRecipe(RecipeKind.Ammo);
            return node;
        }

        /// <summary>
        /// **재료가 없으면 안 돈다** (2026-09-04 · `260904_W01` 3장).
        ///
        /// 어제까지 `NodeRecipe`에는 입력 항목이 아예 없어서 군수 노드가 아무것도 안 먹고
        /// 돌았다(`260904_V01` 2-1). 이 테스트가 그 자리를 막는다.
        /// </summary>
        [Test]
        public void NoInput_DoesNotProduce()
        {
            var grid = new BoardGrid(4, 3, 1f, Vector2.zero);
            NodeInstance node = PlaceEater(grid, new Vector2Int(1, 1), perSec: 10f, perOutput: 1f);

            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            for (int i = 0; i < 10; i++) BoardItemTick.Step(grid, flow, 0.1f);

            Assert.AreEqual(0f, node.OutputBuffer, Delta, "재료가 없으면 한 개도 못 만든다");
            Assert.IsTrue(node.IsStarved, "정지 사유는 만충이 아니라 재료 없음이다");
        }

        /// <summary>재료를 넣은 만큼만 만들고, 먹은 만큼 재고가 준다.</summary>
        [Test]
        public void WithInput_ProducesUpToStock_AndConsumes()
        {
            var grid = new BoardGrid(4, 3, 1f, Vector2.zero);
            NodeInstance node = PlaceEater(grid, new Vector2Int(1, 1), perSec: 10f, perOutput: 2f);

            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            node.TakeInput(FlowKind.Power, 6f); // 2개당 1산출 → 최대 3개

            for (int i = 0; i < 20; i++) BoardItemTick.Step(grid, flow, 0.1f);

            Assert.AreEqual(3f, node.OutputBuffer, Delta, "재고 6 ÷ 개당 2 = 3개까지만");
            Assert.AreEqual(0f, node.InputBuffer[FlowKind.Power], Delta, "먹은 만큼 줄었다");
            Assert.IsTrue(node.IsStarved, "다 먹었으면 다시 재료 없음이다");
        }

        /// <summary>
        /// **벨트로 도착한 재료가 실제로 생산에 쓰인다.** 도착과 소비를 잇는 자리(`DrainArrivals`).
        /// </summary>
        [Test]
        public void ArrivalsFeedProduction()
        {
            var grid = new BoardGrid(6, 3, 1f, Vector2.zero);

            // 전력을 흘리는 벨트 두 칸 → 그 동쪽에 먹는 노드.
            for (int x = 0; x <= 1; x++)
            {
                grid.TryPlaceBelt(new Vector2Int(x, 1),
                    PortFace.West, PortFace.East, FlowKind.Power, out _);
            }
            NodeInstance node = PlaceEater(grid, new Vector2Int(2, 1), perSec: 1f, perOutput: 1f);

            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            Assert.IsTrue(flow.TryInsert(new Vector2Int(0, 1), FlowKind.Power));
            for (int i = 0; i < 20; i++) BoardItemTick.Step(grid, flow, 0.1f);

            Assert.AreEqual(1, flow.ArrivedOf(FlowKind.Power), "한 개가 노드에 닿았다");
            Assert.Greater(node.OutputBuffer, 0f, "닿은 재료로 실제로 만들었다");
            Assert.AreEqual(0, flow.PendingArrivals.Count, "옮긴 뒤 비워야 두 번 안 들어간다");
        }

        /// <summary>
        /// **벨트가 허공으로 뻗어 있으면 결국 생산까지 멈춘다** (2026-09-03 · 4번 덩어리).
        ///
        /// 위 테스트에서 소비처만 뺀 것이다. 벨트가 차고 → 버퍼가 안 비고 → 노드가 정지한다.
        /// 종전 모델에서는 라인 끝이 무조건 배출해서 **이 상태에 영영 도달하지 못했다.**
        /// </summary>
        [Test]
        public void BeltIntoNowhere_FillsUp_ThenStallsProducer()
        {
            var grid = new BoardGrid(6, 3, 1f, Vector2.zero);
            NodeInstance node = PlaceProducer(grid, new Vector2Int(0, 1), perSec: 10f, stackLimit: 5f);

            for (int x = 1; x <= 2; x++)
            {
                grid.TryPlaceBelt(new Vector2Int(x, 1),
                    PortFace.West, PortFace.East, FlowKind.Ammo, out _);
            }
            // 소비처를 두지 않는다.

            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            for (int i = 0; i < 60; i++) BoardItemTick.Step(grid, flow, 0.1f);

            Assert.AreEqual(0, flow.DeliveredCount, "가져가는 곳이 없으므로 나간 것이 없다");
            Assert.AreEqual(BeltItemFlow.MaxPerCell, flow.ItemsAt(new Vector2Int(2, 1)).Count,
                "끝 칸이 가득 찬다");
            Assert.AreEqual(5f, node.OutputBuffer, Delta, "벨트가 안 받으니 버퍼가 상한까지 찬다");
            Assert.IsTrue(NodeProduction.IsStalled(node.CurrentRecipe, node.OutputBuffer),
                "그리고 생산이 멈춘다 — 막힘이 상류로 거슬러 올랐다");
        }

        /// <summary>생산이 실제로 버퍼를 채운다 — 호출자 0건이던 자리가 이제 불린다.</summary>
        [Test]
        public void Produce_FillsBuffer()
        {
            var grid = new BoardGrid(4, 3, 1f, Vector2.zero);
            NodeInstance node = PlaceProducer(grid, new Vector2Int(0, 1), perSec: 1f, stackLimit: 100f);

            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            Assert.AreEqual(0f, node.OutputBuffer, Delta);
            BoardItemTick.Step(grid, flow, 1f);

            Assert.AreEqual(1f, node.OutputBuffer, Delta, "1개/초 × 1초 = 1개");
        }

        /// <summary>
        /// **미선택 기본값은 이름으로 정해진다 — 목록 순서가 아니다** (`260903_W03` 3-2).
        ///
        /// 종전에는 「돌릴 수 있는 첫 후보」였다. 그러면 조합표를 하나 더했을 때 기본값이
        /// 조용히 바뀐다. 이 테스트는 관통탄이 목록의 **둘째**여도 골리는지를 본다.
        /// </summary>
        [Test]
        public void UnselectedRecipe_UsesNamedDefault_NotListOrder()
        {
            var def = ScriptableObject.CreateInstance<NodeDefinition>();
            def.type = NodeType.Munitions;
            def.implemented = true;
            def.ports = new List<NodePort>
            {
                new NodePort(PortFace.East, PortIO.Output, FlowKind.Ammo),
            };
            def.recipes = new List<NodeRecipe>
            {
                // 추진제를 앞에 둔다 — 「첫 후보」 규칙이었다면 이것이 골렸다.
                new NodeRecipe
                {
                    kind = RecipeKind.Propellant, displayName = "추진제",
                    output = FlowKind.Propellant, outputPerSec = 1f,
                    stackLimitTbd = 10f, implemented = true,
                },
                new NodeRecipe
                {
                    kind = RecipeKind.Ammo, displayName = "탄약",
                    output = FlowKind.Ammo, outputPerSec = 1f,
                    stackLimitTbd = 10f, implemented = true,
                },
            };
            _created.Add(def);

            var node = new NodeInstance(def, Vector2Int.zero);

            Assert.AreEqual(RecipeKind.Ammo, node.CurrentRecipe.kind,
                "군수 노드의 기본값은 관통탄이다 — 목록 순서와 무관하다");
            Assert.AreEqual(AmmoKind.Pierce, node.AmmoKind, "탄종 기본값도 관통이다");
        }

        /// <summary>
        /// **돌릴 수 있는 조합표가 하나도 없는 노드**는 아무것도 만들지 않는다.
        ///
        /// 「고르지 않았으면 안 만든다」가 아니다 — `NodeInstance.CurrentRecipe`는 미선택일 때
        /// 돌릴 수 있는 첫 후보를 쓴다(「노드를 놓자마자 아무것도 안 하는 상태를 피한다」).
        /// 처음에 그것을 모르고 반대로 기대했다가 이 테스트가 걸렸다.
        /// </summary>
        [Test]
        public void NoRunnableRecipe_ProducesNothing()
        {
            var def = ScriptableObject.CreateInstance<NodeDefinition>();
            def.type = NodeType.Munitions;
            def.implemented = true;
            def.ports = new List<NodePort>
            {
                new NodePort(PortFace.East, PortIO.Output, FlowKind.Ammo),
            };
            def.recipes = new List<NodeRecipe>(); // 후보가 없다
            _created.Add(def);

            var grid = new BoardGrid(4, 3, 1f, Vector2.zero);
            grid.TryPlace(new Vector2Int(0, 1), def, out NodeInstance node);

            var flow = new BeltItemFlow();
            flow.Rebuild(grid);
            BoardItemTick.Step(grid, flow, 1f);

            Assert.AreEqual(0f, node.OutputBuffer, Delta);
        }
    }
}
