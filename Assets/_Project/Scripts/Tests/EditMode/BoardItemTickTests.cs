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

            // 노드 동쪽에 벨트를 두 칸 깐다. 끝 칸은 라인의 끝이라 배출된다.
            for (int x = 1; x <= 2; x++)
            {
                grid.TryPlaceBelt(new Vector2Int(x, 1),
                    PortFace.West, PortFace.East, FlowKind.Ammo, out _);
            }

            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            for (int i = 0; i < 20; i++) BoardItemTick.Step(grid, flow, 0.1f);

            Assert.Less(node.OutputBuffer, 5f, "흘러 나가므로 상한까지 차지 않는다");
            Assert.Greater(flow.DeliveredCount, 0, "라인 끝으로 나간 것이 있어야 한다");
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
