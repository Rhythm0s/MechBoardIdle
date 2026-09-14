using System.Collections.Generic;
using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 제거 제스처의 판정 (2026-09-15 사용자 확정 · 육안 ⑧).
    ///
    /// 벨트만 지났으면 즉시, 노드가 하나라도 섞였으면 묻는다.
    /// </summary>
    public sealed class RemovalRulesTests
    {
        private static List<Vector2Int> Path(params (int x, int y)[] cells)
        {
            var list = new List<Vector2Int>();
            foreach (var c in cells) list.Add(new Vector2Int(c.x, c.y));
            return list;
        }

        // (5,5) 한 칸만 노드인 판.
        private static bool NodeAt55(Vector2Int c) => c == new Vector2Int(5, 5);

        [Test]
        public void 벨트만_지나면_안_묻는다()
        {
            Assert.That(RemovalRules.NeedsConfirm(Path((1, 1), (2, 1), (3, 1)), NodeAt55), Is.False);
        }

        [Test]
        public void 노드를_하나라도_지나면_묻는다()
        {
            Assert.That(RemovalRules.NeedsConfirm(Path((4, 5), (5, 5), (6, 5)), NodeAt55), Is.True,
                "노드는 조합표·모듈·방향을 지고 있어 다시 끄는 것으로 안 돌아온다");
        }

        [Test]
        public void 노드가_경로_끝에_있어도_묻는다()
        {
            Assert.That(RemovalRules.NeedsConfirm(Path((1, 1), (2, 1), (5, 5)), NodeAt55), Is.True);
        }

        [Test]
        public void 노드_수를_센다()
        {
            bool TwoNodes(Vector2Int c) => c.y == 5;
            Assert.That(RemovalRules.NodeCount(Path((1, 5), (2, 5), (3, 4)), TwoNodes), Is.EqualTo(2),
                "물음 문구가 「노드 n대를 포함해」라고 말한다");
        }

        [Test]
        public void 빈_경로는_안_묻는다()
        {
            Assert.That(RemovalRules.NeedsConfirm(Path(), NodeAt55), Is.False);
            Assert.That(RemovalRules.NeedsConfirm(null, NodeAt55), Is.False);
            Assert.That(RemovalRules.NeedsConfirm(Path((5, 5)), null), Is.False);
            Assert.That(RemovalRules.NodeCount(null, NodeAt55), Is.EqualTo(0));
        }

        [Test]
        public void 한_칸짜리_벨트도_즉시다()
        {
            Assert.That(RemovalRules.NeedsConfirm(Path((1, 1)), NodeAt55), Is.False,
                "벨트 한 칸을 지우자고 묻는 것은 지우는 값보다 물음이 비싸다");
        }
    }
}
