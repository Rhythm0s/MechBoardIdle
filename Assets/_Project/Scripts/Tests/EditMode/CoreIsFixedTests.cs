using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **코어는 시작 보드에 박힌 한 대뿐이다** (2026-09-30 사용자 확정 ① ②).
    ///
    /// 문이 **셋**이고 셋 다 필요하다 —
    ///   · 팔레트에 **안 보인다**(<see cref="PaletteCategories.Shows"/>)
    ///   · 판에 **한 대만 선다**(<c>BoardGrid.TryPlace</c> · `OneCorePerBoardTests`)
    ///   · **못 돌린다**(팝오버에 회전 버튼이 안 뜬다)
    ///
    /// ⚠️ 보이는 것과 놓을 수 있는 것은 **다른 일**이다. 감추기만 하면 고른 번호가
    /// 감춘 칸을 가리켜 「버튼은 없는데 놓이는」 일이 난다.
    /// </summary>
    public sealed class CoreIsFixedTests
    {
        private static NodeDefinition Def(NodeType type)
        {
            var d = ScriptableObject.CreateInstance<NodeDefinition>();
            d.type = type;
            return d;
        }

        /// <summary>⚠️ **어느 탭에서도 안 보인다** — 「전체」도 예외가 아니다.</summary>
        [Test]
        public void 코어는_어느_탭에도_안_보인다()
        {
            NodeDefinition core = Def(NodeType.Core);
            foreach (PaletteCategory tab in System.Enum.GetValues(typeof(PaletteCategory)))
                Assert.IsFalse(PaletteCategories.Shows(tab, core),
                    $"{tab} 탭에 코어가 보인다 — 플레이어가 놓는 것이 아니다");
        }

        /// <summary>
        /// ⚠️ **다른 노드는 그대로 보인다** — 규칙이 너무 넓게 걸리면 팔레트가 빈다.
        /// </summary>
        [Test]
        public void 코어가_아닌_것은_전체_탭에_보인다()
        {
            foreach (NodeType t in System.Enum.GetValues(typeof(NodeType)))
            {
                if (t == NodeType.Core) continue;
                Assert.IsTrue(PaletteCategories.Shows(PaletteCategory.All, Def(t)),
                    $"{t} 가 「전체」 탭에서 사라졌다");
            }
        }

        /// <summary>빈 칸은 어느 탭에서도 안 보인다 — 자리도 안 먹는다.</summary>
        [Test]
        public void 빈_칸은_안_보인다()
        {
            Assert.IsFalse(PaletteCategories.Shows(PaletteCategory.All, null));
        }

        /// <summary>
        /// ⚠️ **미지정 종류는 코어로 읽힌다** — `NodeType.Core` 가 **0** 이라
        /// <c>type</c> 을 안 적은 정의는 말없이 코어가 된다(2026-09-30 확정 · 갈래 (나)).
        ///
        /// 📌 정수 값은 **보존한다**(저장된 자산이 통째로 밀린다). 대신 **막는 문**을 둔다 —
        /// 미지정은 팔레트에 안 뜨고, 판에는 한 대 규칙에 걸려 둘째부터 거절된다.
        /// 이 시험은 그 사실을 **적어 두는 자리**이기도 하다 — 다음 사람이 「왜 안 뜨지」를
        /// 다시 파헤치지 않게.
        /// </summary>
        [Test]
        public void 종류를_안_적은_정의는_코어로_읽힌다()
        {
            var unset = ScriptableObject.CreateInstance<NodeDefinition>();   // type 을 안 적었다
            Assert.AreEqual(NodeType.Core, unset.type,
                "NodeType.Core 가 0 이 아니게 됐다 — 이 시험의 전제가 바뀌었다");
            Assert.IsFalse(PaletteCategories.Shows(PaletteCategory.All, unset),
                "미지정 정의가 팔레트에 떴다");
        }

        /// <summary>
        /// **저장이 코어를 다른 칸에 적고 있어도 제자리에 선다** (확정 ② · 저장 왕복).
        ///
        /// 복원은 시작 배치를 **아예 안 깔고** 저장된 판을 그대로 세운다. 그래서 코어를
        /// **먼저** 제자리에 세워 두고, 저장 쪽 코어는 「한 대」 규칙이 거절하게 한다.
        /// 여기서 재는 것은 그 **순서**다 — 먼저 선 것이 이긴다.
        /// </summary>
        [Test]
        public void 먼저_선_코어가_이긴다()
        {
            var g = new BoardGrid(12, 14, 1f, Vector2.zero);
            NodeDefinition core = Def(NodeType.Core);

            // 시작 보드 자리에 먼저 세운다(복원이 하는 일과 같은 차례).
            Assert.IsTrue(g.TryPlace(StartingBoard.CoreCell, core, out _));

            // 저장이 적어 둔 엉뚱한 칸 — 거절되어야 한다.
            var saved = new Vector2Int(2, 2);
            Assert.AreNotEqual(StartingBoard.CoreCell, saved, "시험이 같은 칸을 보고 있다");
            Assert.IsFalse(g.TryPlace(saved, core, out _), "저장 쪽 코어가 들어왔다");

            Assert.IsNotNull(g.GetAt(StartingBoard.CoreCell), "제자리 코어가 사라졌다");
            Assert.IsNull(g.GetAt(saved), "엉뚱한 칸에 코어가 섰다");
        }

        /// <summary>⚠️ 자리를 두 곳에 안 적는다 — 판 둘이 같은 칸을 쓴다.</summary>
        [Test]
        public void 코어_자리는_한_곳이_든다()
        {
            Assert.AreEqual(new Vector2Int(5, 8), StartingBoard.CoreCell,
                "StartingBoard.CoreCell 이 시작 배치와 갈렸다");
        }

        /// <summary>
        /// **코어는 못 돌린다** (확정 ②). 종전에는 이 규칙이 `OnGUI` 안의 지역 변수라
        /// **시험이 닿지 못했다** — `CoreNodeRule` 로 빼면서 닿는다.
        /// </summary>
        [Test]
        public void 코어는_못_돌린다()
        {
            var g = new BoardGrid(6, 6, 1f, Vector2.zero);
            g.TryPlace(new Vector2Int(1, 1), Def(NodeType.Core), out NodeInstance core);
            g.TryPlace(new Vector2Int(2, 1), Def(NodeType.MunitionsBasic), out NodeInstance muni);

            Assert.IsFalse(CoreNodeRule.CanRotate(core), "코어가 돌아간다");
            Assert.IsTrue(CoreNodeRule.CanRotate(muni), "코어가 아닌 것까지 막혔다");
        }

        /// <summary>**코어는 못 지운다** (2026-09-15 확정) — 되돌릴 수 없는 유일한 칸이다.</summary>
        [Test]
        public void 코어는_못_지운다()
        {
            var g = new BoardGrid(6, 6, 1f, Vector2.zero);
            g.TryPlace(new Vector2Int(1, 1), Def(NodeType.Core), out NodeInstance core);
            g.TryPlace(new Vector2Int(2, 1), Def(NodeType.MunitionsBasic), out NodeInstance muni);

            Assert.IsFalse(CoreNodeRule.CanRemove(core), "코어가 지워진다");
            Assert.IsTrue(CoreNodeRule.CanRemove(muni), "코어가 아닌 것까지 막혔다");
        }

        /// <summary>
        /// ⚠️ **노드가 없으면 막지 않는다** — 빈 칸에 「못 돌린다」고 답하면 부르는 쪽이
        /// 코어와 빈 칸을 못 가린다.
        /// </summary>
        [Test]
        public void 빈_칸은_코어가_아니다()
        {
            Assert.IsFalse(CoreNodeRule.IsCore((NodeInstance)null));
            Assert.IsTrue(CoreNodeRule.CanRotate(null));
            Assert.IsTrue(CoreNodeRule.CanRemove(null));
        }

        /// <summary>복원이 쓰는 자리도 같은 곳에서 나온다 — 좌표를 두 곳에 안 적는다.</summary>
        [Test]
        public void 복원_자리는_시작_보드_값이다()
        {
            Assert.AreEqual(StartingBoard.CoreCell, CoreNodeRule.FixedCell);
        }

        /// <summary>판에는 여전히 한 대만 선다 — 감추기가 놓기 규칙을 대신하지 않는다.</summary>
        [Test]
        public void 감췄어도_판에는_한_대만_선다()
        {
            var g = new BoardGrid(6, 6, 1f, Vector2.zero);
            NodeDefinition core = Def(NodeType.Core);

            Assert.IsTrue(g.TryPlace(new Vector2Int(1, 1), core, out _));
            Assert.IsFalse(g.TryPlace(new Vector2Int(3, 3), core, out _));
        }
    }
}
