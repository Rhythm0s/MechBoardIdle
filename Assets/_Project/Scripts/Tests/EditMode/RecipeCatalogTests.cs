using System.Collections.Generic;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 레시피 표가 `260904_W01` 3-2와 어긋나지 않게 붙든다.
    ///
    /// **값이 아니라 구조를 고정한다.** 개당 몇 개인지는 아직 미확정이고, 여기서 보는 것은
    /// 「어느 노드가 무엇을 먹어 무엇을 내는가」와 그 관계가 스스로 모순되지 않는가이다.
    /// </summary>
    public sealed class RecipeCatalogTests
    {
        [Test]
        public void Catalog_HasTwelveRecipes()
        {
            Assert.AreEqual(12, RecipeCatalog.All.Count, "레시피는 열둘이다 (W01 3-2)");
        }

        /// <summary>노드별 조합표 수 — 코어 1 · 가공 3 · 기초 군수 4 · 복합 군수 4.</summary>
        [Test]
        public void Catalog_RecipesPerNode()
        {
            Assert.AreEqual(1, RecipeCatalog.For(NodeType.Core).Count);
            Assert.AreEqual(3, RecipeCatalog.For(NodeType.Processing).Count);
            Assert.AreEqual(4, RecipeCatalog.For(NodeType.MunitionsBasic).Count);
            Assert.AreEqual(4, RecipeCatalog.For(NodeType.MunitionsComplex).Count);
        }

        /// <summary>
        /// **입력면 수는 노드에 고정된다.** 같은 노드의 조합표가 서로 다른 개수를 먹으면
        /// 면이 남거나 모자라므로, 그것이 곧 군수를 둘로 가른 근거다.
        /// </summary>
        [Test]
        public void InputCount_IsUniformWithinNode()
        {
            foreach (NodeType type in new[]
                     {
                         NodeType.Core, NodeType.Processing,
                         NodeType.MunitionsBasic, NodeType.MunitionsComplex,
                     })
            {
                List<RecipeCatalog.Row> rows = RecipeCatalog.For(type);
                int expected = RecipeCatalog.InputFacesOf(type);
                foreach (RecipeCatalog.Row r in rows)
                {
                    Assert.AreEqual(expected, r.inputs.Length,
                        $"{type}의 「{r.displayName}」이 다른 개수를 먹는다 — 입력면이 어긋난다");
                }
            }
        }

        [Test]
        public void InputFaces_CoreZero_BasicOne_ComplexTwo()
        {
            Assert.AreEqual(0, RecipeCatalog.InputFacesOf(NodeType.Core));
            Assert.AreEqual(1, RecipeCatalog.InputFacesOf(NodeType.Processing));
            Assert.AreEqual(1, RecipeCatalog.InputFacesOf(NodeType.MunitionsBasic));
            Assert.AreEqual(2, RecipeCatalog.InputFacesOf(NodeType.MunitionsComplex));
        }

        /// <summary>
        /// **배터리만 발전재료를 먹는다** — 나머지 가공 둘은 코어 에너지다.
        /// 이 한 줄이 W01 3-4의 다툼을 만든다: 배터리를 늘리면 전력·추진제·폭발탄이 같이 준다.
        /// </summary>
        [Test]
        public void Battery_EatsPowerMaterial_NotCoreEnergy()
        {
            RecipeCatalog.Row battery = Find(RecipeKind.Battery);
            CollectionAssert.AreEqual(new[] { FlowKind.PowerMaterial }, battery.inputs);
        }

        /// <summary>**표준탄이 특수탄의 재료다** — 로봇 A는 기초 군수를 반드시 거친다.</summary>
        [Test]
        public void SpecialAmmo_RequiresStandardAmmo()
        {
            CollectionAssert.Contains(Find(RecipeKind.PierceAmmo).inputs, FlowKind.StandardAmmo);
            CollectionAssert.Contains(Find(RecipeKind.ExplosiveAmmo).inputs, FlowKind.StandardAmmo);
        }

        /// <summary>
        /// 체인이 끊기지 않는다 — 코어 에너지 말고는 **모든 입력이 누군가의 산출**이다.
        /// 하나라도 아무도 안 만드는 것을 먹으면 그 라인은 영영 안 돈다.
        /// </summary>
        [Test]
        public void EveryInput_IsSomeonesOutput()
        {
            var made = new HashSet<FlowKind>();
            foreach (RecipeCatalog.Row r in RecipeCatalog.All) made.Add(r.output);

            foreach (RecipeCatalog.Row r in RecipeCatalog.All)
            foreach (FlowKind need in r.inputs)
            {
                Assert.IsTrue(made.Contains(need),
                    $"「{r.displayName}」이 먹는 {need}를 아무도 만들지 않는다");
            }
        }

        private static RecipeCatalog.Row Find(RecipeKind kind)
        {
            foreach (RecipeCatalog.Row r in RecipeCatalog.All)
                if (r.kind == kind) return r;
            Assert.Fail($"{kind}가 표에 없다");
            return default;
        }
    }
}
