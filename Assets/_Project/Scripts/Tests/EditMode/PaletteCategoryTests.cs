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
            // ⚠️ 이것이 이 규칙의 값이다 — 「기초 가공」라는 **이름**을 달고 있어도
            // 입력이 둘이면 복합이다. 나중에 입력 둘짜리가 생겨도 표를 안 고쳐도 된다.
            Assert.That(PaletteCategories.Of(Node(NodeType.MunitionsBasic, 2)),
                Is.EqualTo(PaletteCategory.Complex));

            // 반대도 성립한다 — 「복합 가공」인데 입력이 하나면 기초로 간다.
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

            // ✅ **부스터 · 쉴드는 「군수」다**(2026-09-17 사용자 확정 · `260917_W08` 5-1).
            //    🗑️ 구 「부스터 = 전력」 폐기 — 전력 탭은 **전력을 내고 관리하는 것만** 담는다.
            //    ⚠️⚠️ **쉴드가 이 시험의 값이다** — 입력면이 **하나**라
            //    규칙(입력 수)대로 두면 **기초 가공**으로 간다. 명시 그룹이 먼저라 군수로 온다.
            //    생존 두 층(회피 · 쉴드)이 다른 탭에 서던 것을 사용자가 고친 자리다.
            Assert.That(PaletteCategories.Of(Node(NodeType.Booster, 1)),
                Is.EqualTo(PaletteCategory.Munitions));
            Assert.That(PaletteCategories.Of(Node(NodeType.Shield, 1)),
                Is.EqualTo(PaletteCategory.Munitions),
                "쉴드가 입력면 하나라 기초 가공으로 갔다 — 명시 그룹이 규칙보다 먼저다");
        }

        [Test]
        public void 전력_탭은_전력만_담는다()
        {
            // 「전력을 내거나 전력으로 무형 자원을 내는 것」에서 **전력만**으로 좁혔다.
            foreach (NodeType t in new[] { NodeType.Booster, NodeType.Shield })
                Assert.That(PaletteCategories.Of(Node(t, 1)),
                    Is.Not.EqualTo(PaletteCategory.Power), $"{t} 가 아직 전력 탭에 있다");
        }

        [Test]
        public void 탭은_일곱이고_군수는_전력_앞에_선다()
        {
            Assert.That(PaletteCategories.Order.Length, Is.EqualTo(7), "탭 수가 일곱이 아니다");

            int munitions = System.Array.IndexOf(PaletteCategories.Order, PaletteCategory.Munitions);
            int power = System.Array.IndexOf(PaletteCategories.Order, PaletteCategory.Power);
            Assert.That(munitions, Is.GreaterThanOrEqualTo(0), "군수 탭이 차례에 없다");
            Assert.That(munitions, Is.LessThan(power), "군수가 전력 뒤에 선다 — 만드는 것이 먼저다");

            // ⚠️ **열거값 차례가 아니라 이 배열이 화면 차례다** — 군수는 값이 6 인데 앞에 선다.
            Assert.That((int)PaletteCategory.Munitions, Is.GreaterThan((int)PaletteCategory.Power),
                "새 값을 앞에 끼웠다 — 앞의 정수는 저장·자산에 박혀 있다");

            Assert.That(PaletteCategories.LabelOf(PaletteCategory.Munitions), Is.EqualTo("군수"));
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
            // 🗑️ 구 「여섯」 폐기 — 2026-09-17 에 「군수」 탭이 생겨 **일곱**이다.
            //    (수는 바로 위 시험이 뜻과 함께 지킨다 — 여기서는 차례와 이름만 본다.)
            Assert.That(PaletteCategories.Order.Length, Is.EqualTo(7));
            Assert.That(PaletteCategories.Order[0], Is.EqualTo(PaletteCategory.All));
            foreach (PaletteCategory c in PaletteCategories.Order)
                Assert.That(PaletteCategories.LabelOf(c), Is.Not.Empty, "이름 없는 탭이 있으면 안 된다");
        }
    }
}
