using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 보드 → 탄종별 노드 수 → 출력(260824_V02 §1 배선).
    ///
    /// 여기가 코어 명제의 인과가 코드로 닫히는 지점이다:
    /// **보드에 어떤 군수 노드를 몇 개 놓았는가**가 출력을 만든다.
    /// 종전 모델(145 × clamp01(총생산 ÷ 소비상한))에서는 탄종 구분이 없어
    /// 관통을 늘렸는지 폭발을 늘렸는지가 출력에 반영되지 않았다.
    /// </summary>
    public sealed class MunitionsAssignmentTests
    {
        private const float D = 0.001f;
        private const float PerNode = 1f;

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private NodeDefinition Node(NodeType type, float ammoProduce = 0f)
        {
            var n = ScriptableObject.CreateInstance<NodeDefinition>();
            n.type = type;
            n.implemented = true;
            n.resources = new NodeResourceProfile { ammoProduce = ammoProduce };
            n.ports = new List<NodePort>();
            _created.Add(n);
            return n;
        }

        private BoardGrid GridWith(params (Vector2Int cell, NodeType type, AmmoKind kind)[] items)
        {
            var grid = new BoardGrid(8, 8, 1f, Vector2.zero);
            foreach ((Vector2Int cell, NodeType type, AmmoKind kind) in items)
            {
                float produce = type == NodeType.Munitions ? PerNode : 0f;
                grid.TryPlace(cell, Node(type, produce), out NodeInstance placed);
                if (placed != null) placed.AmmoKind = kind;
            }
            return grid;
        }

        // ---- 집계가 탄종을 구분하는가 ----

        [Test]
        public void Aggregate_CountsMunitionsByKind()
        {
            BoardGrid grid = GridWith(
                (new Vector2Int(0, 0), NodeType.Core, AmmoKind.Pierce),
                (new Vector2Int(1, 0), NodeType.Munitions, AmmoKind.Pierce),
                (new Vector2Int(2, 0), NodeType.Munitions, AmmoKind.Split),
                (new Vector2Int(3, 0), NodeType.Munitions, AmmoKind.Explosive),
                (new Vector2Int(4, 0), NodeType.Munitions, AmmoKind.Explosive));

            NetworkAggregate agg = LogisticsNetwork.Aggregate(grid);

            Assert.AreEqual(1, agg.MuniCountOf(AmmoKind.Pierce));
            Assert.AreEqual(1, agg.MuniCountOf(AmmoKind.Split));
            Assert.AreEqual(2, agg.MuniCountOf(AmmoKind.Explosive));
            Assert.AreEqual(4f, agg.ammoProduce, D, "총 생산은 노드 수 × 노드당 생산");
        }

        /// <summary>군수가 아닌 노드는 탄종 카운트에 들어가지 않는다(코어의 기본값 관통에 오염되지 않게).</summary>
        [Test]
        public void Aggregate_IgnoresAmmoKindOnNonMunitionsNodes()
        {
            BoardGrid grid = GridWith(
                (new Vector2Int(0, 0), NodeType.Core, AmmoKind.Pierce),
                (new Vector2Int(1, 0), NodeType.Energy, AmmoKind.Pierce),
                (new Vector2Int(2, 0), NodeType.Storage, AmmoKind.Pierce));

            NetworkAggregate agg = LogisticsNetwork.Aggregate(grid);

            Assert.AreEqual(0, agg.MuniCountOf(AmmoKind.Pierce), "군수가 아닌 노드는 라인을 만들지 않는다");
        }

        // ---- 배치 → 출력 앵커 ----

        private static List<MunitionsLine> LinesFrom(NetworkAggregate agg) => new List<MunitionsLine>
        {
            new MunitionsLine(AmmoKind.Pierce, 5f, 20f, agg.MuniCountOf(AmmoKind.Pierce)),
            new MunitionsLine(AmmoKind.Split, 4f, 25f, agg.MuniCountOf(AmmoKind.Split)),
            new MunitionsLine(AmmoKind.Explosive, 2f, 50f, agg.MuniCountOf(AmmoKind.Explosive)),
        };

        /// <summary>대표 배치(관통1·분열1·폭발2 = 군수 4개) → 145. §9 s3Break 앵커.</summary>
        [Test]
        public void RepresentativeBoard_FourMunitionsNodes_Yields145()
        {
            BoardGrid grid = GridWith(
                (new Vector2Int(0, 0), NodeType.Core, AmmoKind.Pierce),
                (new Vector2Int(1, 0), NodeType.Munitions, AmmoKind.Pierce),
                (new Vector2Int(2, 0), NodeType.Munitions, AmmoKind.Split),
                (new Vector2Int(3, 0), NodeType.Munitions, AmmoKind.Explosive),
                (new Vector2Int(4, 0), NodeType.Munitions, AmmoKind.Explosive));

            NetworkAggregate agg = LogisticsNetwork.Aggregate(grid);
            Assert.AreEqual(145f, AmmoLineProduction.TotalOutput(LinesFrom(agg), PerNode), D);
        }

        /// <summary>시작 보드(폭발 한 칸 비움) → 95. 그 칸을 채우면 145 — 배치가 출력을 올린다.</summary>
        [Test]
        public void StartBoard_OneExplosiveMissing_Yields95_AndFillingItReaches145()
        {
            BoardGrid start = GridWith(
                (new Vector2Int(0, 0), NodeType.Core, AmmoKind.Pierce),
                (new Vector2Int(1, 0), NodeType.Munitions, AmmoKind.Pierce),
                (new Vector2Int(2, 0), NodeType.Munitions, AmmoKind.Split),
                (new Vector2Int(3, 0), NodeType.Munitions, AmmoKind.Explosive));

            NetworkAggregate before = LogisticsNetwork.Aggregate(start);
            Assert.AreEqual(95f, AmmoLineProduction.TotalOutput(LinesFrom(before), PerNode), D, "20 + 25 + 50");

            start.TryPlace(new Vector2Int(4, 0), Node(NodeType.Munitions, PerNode), out NodeInstance added);
            added.AmmoKind = AmmoKind.Explosive;

            NetworkAggregate after = LogisticsNetwork.Aggregate(start);
            Assert.AreEqual(145f, AmmoLineProduction.TotalOutput(LinesFrom(after), PerNode), D,
                "빈 칸을 채우면 대표 상태에 도달한다");
        }

        /// <summary>
        /// **같은 노드 수라도 탄종 조합이 다르면 출력이 다르다.** 종전 모델에서는 둘 다 같은 값이었다 —
        /// 총 생산량만 봤기 때문이다. 조합이 성장 축이라는 것이 여기서 성립한다.
        /// </summary>
        [Test]
        public void SameNodeCount_DifferentMix_ChangesOutput()
        {
            BoardGrid allPierce = GridWith(
                (new Vector2Int(0, 0), NodeType.Core, AmmoKind.Pierce),
                (new Vector2Int(1, 0), NodeType.Munitions, AmmoKind.Pierce),
                (new Vector2Int(2, 0), NodeType.Munitions, AmmoKind.Pierce),
                (new Vector2Int(3, 0), NodeType.Munitions, AmmoKind.Pierce),
                (new Vector2Int(4, 0), NodeType.Munitions, AmmoKind.Pierce));

            BoardGrid mixed = GridWith(
                (new Vector2Int(0, 0), NodeType.Core, AmmoKind.Pierce),
                (new Vector2Int(1, 0), NodeType.Munitions, AmmoKind.Pierce),
                (new Vector2Int(2, 0), NodeType.Munitions, AmmoKind.Split),
                (new Vector2Int(3, 0), NodeType.Munitions, AmmoKind.Explosive),
                (new Vector2Int(4, 0), NodeType.Munitions, AmmoKind.Explosive));

            float pierceOnly = AmmoLineProduction.TotalOutput(
                LinesFrom(LogisticsNetwork.Aggregate(allPierce)), PerNode);
            float mix = AmmoLineProduction.TotalOutput(
                LinesFrom(LogisticsNetwork.Aggregate(mixed)), PerNode);

            Assert.AreEqual(80f, pierceOnly, D, "관통 4노드 = 4발/초 × 20");
            Assert.AreEqual(145f, mix, D);
            Assert.Greater(mix, pierceOnly, "같은 4노드라도 조합이 출력을 바꾼다");
        }

        /// <summary>
        /// 회귀 방지: 군수 노드를 계속 늘리면 출력이 **계속** 올라야 한다.
        /// 소비 상한을 생산 자리에 넣던 시기에는 1개에서 이미 상수였다(CLAUDE.md §7).
        /// </summary>
        [Test]
        public void AddingMunitionsNodes_KeepsRaisingOutput_UntilSpec()
        {
            float Previous = -1f;
            for (int n = 1; n <= 5; n++)
            {
                var items = new List<(Vector2Int, NodeType, AmmoKind)>
                {
                    (new Vector2Int(0, 1), NodeType.Core, AmmoKind.Pierce),
                };
                for (int i = 0; i < n; i++)
                    items.Add((new Vector2Int(i, 0), NodeType.Munitions, AmmoKind.Pierce));

                NetworkAggregate agg = LogisticsNetwork.Aggregate(GridWith(items.ToArray()));
                float output = AmmoLineProduction.TotalOutput(LinesFrom(agg), PerNode);

                Assert.AreEqual(n * 20f, output, D, $"관통 {n}노드 → {n}발/초 × 20");
                Assert.Greater(output, Previous, "노드를 더 놓으면 출력이 오른다");
                Previous = output;
            }
        }
    }
}
