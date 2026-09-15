using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 팔레트 카테고리 여섯 (2026-09-15 사용자 확정 · 하단 개편 ②).
    ///
    /// ⚠️ **가공 계열은 이름이 아니라 입력면 수로 갈린다.** 이 시험이 지키는 것은
    /// 「나중에 입력 둘짜리 노드가 생기면 **표를 안 고쳐도** 복합으로 간다」이다.
    /// </summary>
    public sealed class PaletteCategoryTests
    {
        private static NodeDefinition Node(NodeType type, int inputs)
        {
            var def = ScriptableObject.CreateInstance<NodeDefinition>();
            def.type = type;
            def.ports = new List<NodePort>();
            for (int i = 0; i < inputs; i++)
                def.ports.Add(new NodePort
                {
                    io = PortIO.Input,
                    face = (PortFace)(i % 4),
                    kind = FlowKind.Material,
                });
            def.ports.Add(new NodePort
            {
                io = PortIO.Output, face = PortFace.East, kind = FlowKind.Material,
            });
            return def;
        }

        [Test]
        public void 입력_하나면_기초_가공이다()
        {
            Assert.That(PaletteCategories.Of(Node(NodeType.Processing, 1)),
                Is.EqualTo(PaletteCategory.Basic));
            Assert.That(PaletteCategories.Of(Node(NodeType.MunitionsBasic, 1)),
                Is.EqualTo(PaletteCategory.Basic));
        }

        [Test]
        public void 입력_둘이면_복합_가공이다()
        {
            Assert.That(PaletteCategories.Of(Node(NodeType.MunitionsComplex, 2)),
                Is.EqualTo(PaletteCategory.Complex));
        }

        [Test]
        public void 이름이_아니라_수가_가른다()
        {
            // ⚠️ 이것이 이 규칙의 값이다 — 「기초 군수」라는 **이름**을 달고 있어도
            // 입력이 둘이면 복합이다. 나중에 입력 둘짜리가 생겨도 표를 안 고쳐도 된다.
            Assert.That(PaletteCategories.Of(Node(NodeType.MunitionsBasic, 2)),
                Is.EqualTo(PaletteCategory.Complex));

            // 반대도 성립한다 — 「복합 군수」인데 입력이 하나면 기초로 간다.
            Assert.That(PaletteCategories.Of(Node(NodeType.MunitionsComplex, 1)),
                Is.EqualTo(PaletteCategory.Basic));
        }

        [Test]
        public void 명시_그룹이_입력_수보다_먼저다()
        {
            // 저장은 입력이 하나지만 **물류**다 — 「무엇을 받는가」가 아니라
            // 「무엇을 하는 물건인가」로 정해진 그룹이다.
            Assert.That(PaletteCategories.Of(Node(NodeType.Storage, 1)),
                Is.EqualTo(PaletteCategory.Logistics));
            Assert.That(PaletteCategories.Of(Node(NodeType.Energy, 0)),
                Is.EqualTo(PaletteCategory.Power));
            Assert.That(PaletteCategories.Of(Node(NodeType.Booster, 1)),
                Is.EqualTo(PaletteCategory.Power));
        }

        [Test]
        public void 전체_탭은_전부_보여_준다()
        {
            NodeDefinition any = Node(NodeType.Energy, 0);
            Assert.That(PaletteCategories.Shows(PaletteCategory.All, any), Is.True);
            Assert.That(PaletteCategories.ShowsBeltElement(PaletteCategory.All), Is.True);
            Assert.That(PaletteCategories.ShowsModule(PaletteCategory.All), Is.True);
        }

        [Test]
        public void 벨트_요소는_물류_탭에만_선다()
        {
            Assert.That(PaletteCategories.ShowsBeltElement(PaletteCategory.Logistics), Is.True);
            Assert.That(PaletteCategories.ShowsBeltElement(PaletteCategory.Power), Is.False);
        }

        [Test]
        public void 모듈은_모듈_탭에만_선다()
        {
            // ⚠️ 복합 가공이 아니다(2026-09-15 사용자 정정) — 모듈은 놓는 것이 아니라
            // 붙이는 것이라, 「놓을 것」 목록에 섞이면 안 된다.
            Assert.That(PaletteCategories.ShowsModule(PaletteCategory.Module), Is.True);
            Assert.That(PaletteCategories.ShowsModule(PaletteCategory.Complex), Is.False);
        }

        [Test]
        public void 탭_차례는_전체가_맨_앞이다()
        {
            Assert.That(PaletteCategories.Order.Length, Is.EqualTo(6));
            Assert.That(PaletteCategories.Order[0], Is.EqualTo(PaletteCategory.All));
            foreach (PaletteCategory c in PaletteCategories.Order)
                Assert.That(PaletteCategories.LabelOf(c), Is.Not.Empty, "이름 없는 탭이 있으면 안 된다");
        }
    }
}
