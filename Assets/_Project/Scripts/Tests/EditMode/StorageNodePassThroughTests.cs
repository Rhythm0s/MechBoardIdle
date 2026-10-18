using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **저장 노드가 받은 품목을 다시 내보내는가 — 거동 기록 시험** (2026-10-18 · 조립 기획서 점검 ❓ ④ · 코드 0).
    ///
    /// 라인: 생산(0,1) → 벨트 (1,1)(2,1) → **저장(3,1)**(서 입력 · 동 출력 · 실제 `stor` 와 같은 포트 꼴) → 벨트 (4,1)(5,1) → 받는 곳(6,1).
    /// 낱개 흐름(`BoardItemTick`)을 20초 돌리고 저장의 입력 버퍼 · 출력 버퍼 · 하류 도착을 잰다.
    ///
    /// ⚠️ **이 시험은 지금 거동을 그대로 적는다(결함 후보 · 고치지 않음 — 태그 뒤 규칙 ①).**
    ///    고치는 날 이 시험의 단언을 뒤집는다(삼킴 → 통과).
    /// </summary>
    public sealed class StorageNodePassThroughTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _created) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private NodeDefinition Def(NodeType type, List<NodePort> ports, List<NodeRecipe> recipes)
        {
            var def = ScriptableObject.CreateInstance<NodeDefinition>();
            def.type = type;
            def.implemented = true;
            def.ports = ports;
            def.recipes = recipes;
            _created.Add(def);
            return def;
        }

        [Test]
        public void 저장_노드는_받은_품목을_하류로_내보내는가_지금_거동()
        {
            const FlowKind K = FlowKind.StandardAmmo;
            var grid = new BoardGrid(8, 3, 1f, Vector2.zero);

            NodeDefinition producer = Def(NodeType.MunitionsBasic,
                new List<NodePort> { new NodePort(PortFace.East, PortIO.Output, K) },
                new List<NodeRecipe>
                {
                    new NodeRecipe { kind = RecipeKind.Ammo, displayName = "표준탄", output = K,
                                     outputPerSec = 2f, stackLimitTbd = 5f, implemented = true },
                });
            grid.TryPlace(new Vector2Int(0, 1), producer, out NodeInstance src);
            src.SelectRecipe(RecipeKind.Ammo);

            for (int x = 1; x <= 2; x++)
                grid.TryPlaceBelt(new Vector2Int(x, 1), PortFace.West, PortFace.East, K, out _);

            NodeDefinition storage = Def(NodeType.Storage,
                new List<NodePort>
                {
                    new NodePort(PortFace.West, PortIO.Input, K),
                    new NodePort(PortFace.East, PortIO.Output, K),
                },
                new List<NodeRecipe>());
            grid.TryPlace(new Vector2Int(3, 1), storage, out NodeInstance stor);

            for (int x = 4; x <= 5; x++)
                grid.TryPlaceBelt(new Vector2Int(x, 1), PortFace.West, PortFace.East, K, out _);

            NodeDefinition sink = Def(NodeType.Storage,
                new List<NodePort> { new NodePort(PortFace.West, PortIO.Input, K) },
                new List<NodeRecipe>());
            grid.TryPlace(new Vector2Int(6, 1), sink, out NodeInstance end);

            var flow = new BeltItemFlow();
            flow.Rebuild(grid);
            for (int i = 0; i < 200; i++) BoardItemTick.Step(grid, flow, 0.1f);

            stor.InputBuffer.TryGetValue(K, out float storIn);
            end.InputBuffer.TryGetValue(K, out float endIn);
            TestContext.WriteLine($"[저장 거동] 저장 입력 버퍼 {storIn:F1} · 저장 출력 버퍼 {stor.OutputBuffer:F1} · "
                                  + $"하류 받는 곳 입력 {endIn:F1} · 도착 총계 {flow.DeliveredCount}");

            Assert.Greater(storIn, 0f, "저장에 품목이 닿아야 시험이 성립한다");
            // ⚠️ 지금 거동(결함 후보) — 받은 것은 입력 버퍼에만 쌓이고 하류로 안 나온다.
            Assert.AreEqual(0f, stor.OutputBuffer, 1e-4f, "지금은 저장 출력 버퍼가 비어 있다(입력 → 출력으로 옮기는 코드 없음)");
            Assert.AreEqual(0f, endIn, 1e-4f, "지금은 하류에 아무것도 안 닿는다(저장이 삼킨다)");
        }
    }
}
