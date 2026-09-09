using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 모듈 시스템 틀 (2026-09-09 착수 · 조립 시스템 문서「모듈 재정의」· MVP 문서 11장).
    ///
    /// **여기서 지키는 것은 값이 아니라 관계다** — 값은 SO에 있고 문서가 소스다.
    /// 지켜야 하는 관계는 하나다: <b>부하는 산출보다 가파르게 오른다</b>(지침 §3).
    /// 그것이 무너지면 모듈은 공짜 강화가 되어 「물류 무개입」이 깨진다.
    /// </summary>
    public sealed class ModuleFrameTests
    {
        private static ModuleDefinition Module(float output, float input, float load)
        {
            var m = ScriptableObject.CreateInstance<ModuleDefinition>();
            m.outputMultiplier = output;
            m.inputMultiplier = input;
            m.powerLoadMultiplier = load;
            m.symbol = "M";
            return m;
        }

        private static NodeInstance Node()
        {
            var def = ScriptableObject.CreateInstance<NodeDefinition>();
            def.type = NodeType.Processing;
            def.displayName = "가공";
            return new NodeInstance(def, new Vector2Int(1, 1));
        }

        [Test]
        public void ANodeWithoutModules_ChangesNothing()
        {
            NodeInstance node = Node();

            Assert.AreEqual(0, node.ModuleCount);
            Assert.AreEqual(1f, node.ModuleOutputMultiplier, 1e-4f);
            Assert.AreEqual(1f, node.ModuleInputMultiplier, 1e-4f);
            Assert.AreEqual(1f, node.ModulePowerLoadMultiplier, 1e-4f, "모듈이 없으면 부하도 그대로다");
        }

        [Test]
        public void ANodeTakesTwoModules_AndRefusesTheThird()
        {
            NodeInstance node = Node();

            Assert.IsTrue(node.TryAttachModule(Module(1.5f, 1f, 2f)));
            Assert.IsTrue(node.TryAttachModule(Module(1.5f, 1.5f, 1.5f)));
            Assert.AreEqual(NodeInstance.ModuleSlots, node.ModuleCount);

            ModuleDefinition third = Module(1.5f, 1f, 2f);
            Assert.IsFalse(node.TryAttachModule(third), "칸은 둘이다(MVP 문서 11장)");
            Assert.AreNotSame(third, node.ModuleAt(0), "거절할 때는 아무것도 안 바꾼다");
            Assert.AreNotSame(third, node.ModuleAt(1));
        }

        [Test]
        public void DetachingFreesTheSlot()
        {
            NodeInstance node = Node();
            node.TryAttachModule(Module(1.5f, 1f, 2f));

            Assert.IsTrue(node.DetachModuleAt(0));
            Assert.AreEqual(0, node.ModuleCount);
            Assert.IsFalse(node.DetachModuleAt(0), "이미 빈 칸은 뗄 것이 없다");
            Assert.IsTrue(node.TryAttachModule(Module(1.5f, 1f, 2f)), "뗀 자리에 다시 붙는다");
        }

        /// <summary>
        /// **이 시험이 이 파일의 이유다.** 어떤 조합으로 붙이든 부하가 산출보다 더 올라야 한다 —
        /// 그래야 모듈이 「좁은 공간을 뚫는 수단」이 되고 공짜 강화가 되지 않는다.
        /// </summary>
        [Test]
        public void LoadAlwaysClimbsFasterThanOutput()
        {
            ModuleDefinition[] kinds =
            {
                Module(1.5f, 1f, 2.0f),   // M — 문서 값
                Module(1.5f, 1.5f, 1.5f), // R — 문서 값
            };

            foreach (ModuleDefinition a in kinds)
            {
                NodeInstance one = Node();
                one.TryAttachModule(a);
                Assert.GreaterOrEqual(one.ModulePowerLoadMultiplier, one.ModuleOutputMultiplier,
                    "한 칸에서도 부하가 산출보다 덜 오르면 그 모듈은 공짜다");

                foreach (ModuleDefinition b in kinds)
                {
                    NodeInstance two = Node();
                    two.TryAttachModule(a);
                    two.TryAttachModule(b);
                    Assert.GreaterOrEqual(two.ModulePowerLoadMultiplier, two.ModuleOutputMultiplier,
                        "두 칸이 겹쳐도 뒤집히면 안 된다");
                }
            }
        }

        [Test]
        public void TwoSlotsCompound()
        {
            // ⚠️ 곱은 **가정이다** — 소스에 두 칸이 겹칠 때의 셈법이 없다(NodeInstance 주석).
            // 그 가정이 바뀌면 이 시험이 먼저 빨개져 자리를 지목한다.
            NodeInstance node = Node();
            node.TryAttachModule(Module(1.5f, 1f, 2f));
            node.TryAttachModule(Module(1.5f, 1f, 2f));

            Assert.AreEqual(2.25f, node.ModuleOutputMultiplier, 1e-4f);
            Assert.AreEqual(4.00f, node.ModulePowerLoadMultiplier, 1e-4f);
        }

        // ── 집계에 실제로 반영되는가 (지침 §7 「존재하는가가 아니라 불리는가」) ──────────

        private static BoardGrid Grid()
            => new BoardGrid(4, 4, 1f, Vector2.zero, null);

        private static NodeDefinition EnergyEater()
        {
            var def = ScriptableObject.CreateInstance<NodeDefinition>();
            def.type = NodeType.Processing;
            def.displayName = "가공";
            def.resources = new NodeResourceProfile { powerDraw = 10f, ammoProduce = 2f };
            return def;
        }

        [Test]
        public void ModuleLoad_ReachesThePowerDemand()
        {
            BoardGrid grid = Grid();
            NodeDefinition def = EnergyEater();
            grid.TryPlace(new Vector2Int(1, 1), def, out NodeInstance node);

            NetworkAggregate before = LogisticsNetwork.Aggregate(grid);
            Assert.AreEqual(10f, before.powerDraw, 1e-3f);

            node.TryAttachModule(Module(1.5f, 1f, 2f));

            NetworkAggregate after = LogisticsNetwork.Aggregate(grid);
            Assert.AreEqual(20f, after.powerDraw, 1e-3f,
                "부하가 집계에 안 닿으면 모듈은 화면에서 공짜로 보인다");
        }

        [Test]
        public void ModuleGain_ReachesTheOutput()
        {
            BoardGrid grid = Grid();
            NodeDefinition def = EnergyEater();
            grid.TryPlace(new Vector2Int(2, 2), def, out NodeInstance node);

            NetworkAggregate before = LogisticsNetwork.Aggregate(grid);
            Assert.AreEqual(2f, before.ammoProduce, 1e-3f);

            node.TryAttachModule(Module(1.5f, 1f, 2f));

            NetworkAggregate after = LogisticsNetwork.Aggregate(grid);
            Assert.AreEqual(3f, after.ammoProduce, 1e-3f);
        }
    }
}
