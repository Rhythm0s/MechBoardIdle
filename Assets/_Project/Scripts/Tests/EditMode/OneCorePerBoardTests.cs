using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **코어는 판에 한 대** (2026-09-30 사용자 확정 ③ · 조립 문서 11장).
    ///
    /// ⚠️ 종전에는 제한이 **없었다** — `TryPlace` 가 보는 것이 「빈 칸인가」 하나뿐이라
    /// 팔레트의 코어를 몇 개든 놓을 수 있었다. 문서는 처음부터 한 대라고 적고 있었다.
    /// </summary>
    public sealed class OneCorePerBoardTests
    {
        private static NodeDefinition Def(NodeType type)
        {
            var d = ScriptableObject.CreateInstance<NodeDefinition>();
            d.type = type;
            return d;
        }

        private static BoardGrid Board() => new BoardGrid(6, 6, 1f, Vector2.zero);

        [Test]
        public void 코어는_한_대만_놓인다()
        {
            BoardGrid g = Board();
            NodeDefinition core = Def(NodeType.Core);

            Assert.IsTrue(g.TryPlace(new Vector2Int(1, 1), core, out _), "첫 코어가 안 놓였다");
            Assert.IsFalse(g.TryPlace(new Vector2Int(3, 3), core, out _),
                "둘째 코어가 놓였다 — 조립 문서 11장은 한 대다");
        }

        /// <summary>⚠️ **다른 종류는 여러 개 그대로다** — 코어만 막는다.</summary>
        [Test]
        public void 코어가_아닌_것은_여러_개_놓인다()
        {
            BoardGrid g = Board();
            NodeDefinition muni = Def(NodeType.MunitionsBasic);

            Assert.IsTrue(g.TryPlace(new Vector2Int(1, 1), muni, out _));
            Assert.IsTrue(g.TryPlace(new Vector2Int(2, 1), muni, out _),
                "군수를 둘째부터 못 놓는다 — 코어 규칙이 너무 넓게 걸렸다");
        }

        /// <summary>
        /// **지우면 다시 놓을 수 있다** — 「한 대」는 개수 제한이지 일회용이 아니다.
        /// </summary>
        [Test]
        public void 지우면_다시_놓을_수_있다()
        {
            BoardGrid g = Board();
            NodeDefinition core = Def(NodeType.Core);
            var first = new Vector2Int(1, 1);

            Assert.IsTrue(g.TryPlace(first, core, out _));
            Assert.IsTrue(g.TryRemove(first), "코어를 격자에서 못 지웠다");
            Assert.IsTrue(g.TryPlace(new Vector2Int(4, 4), core, out _),
                "지웠는데도 새 코어를 못 놓는다");
        }

        /// <summary>판마다 따로 센다 — A 와 B 는 각자 한 대씩 갖는다.</summary>
        [Test]
        public void 판마다_한_대씩이다()
        {
            BoardGrid a = Board();
            BoardGrid b = Board();
            NodeDefinition core = Def(NodeType.Core);

            Assert.IsTrue(a.TryPlace(new Vector2Int(1, 1), core, out _));
            Assert.IsTrue(b.TryPlace(new Vector2Int(1, 1), core, out _),
                "A 에 놓았다고 B 에 못 놓는다 — 판마다 세야 한다");
        }

        [Test]
        public void 빈_판은_코어가_없다()
        {
            Assert.IsFalse(Board().HasCore);
        }
    }
}
