using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 병합기·분류기의 면을 이웃에서 다시 잡는다(260829_V03 §판정③).
    ///
    /// **왜 배치 시점에 안 정하는가**: 요소를 먼저 놓고 나중에 이웃을 붙이는 순서가 자연스러운데,
    /// 그때 방향을 고정해 두면 조용히 안 이어진 채로 남는다. 벨트 품목과 같은 이유다.
    /// </summary>
    public sealed class BeltAutoOrientTests
    {
        private NodeDefinition _muni, _core, _stor;

        [SetUp]
        public void SetUp()
        {
            _muni = Load("muni");
            _core = Load("core");
            _stor = Load("stor");
            if (_muni == null || _core == null || _stor == null)
                Assert.Ignore("노드 자산 없음 — 먼저 메뉴 'MBI/Generate Balance + Nodes' 실행.");
        }

        private static NodeDefinition Load(string id) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>(
                $"Assets/_Project/ScriptableObjects/Nodes/Node_{id}.asset");

        private static BoardGrid Grid() => new BoardGrid(10, 10, 1f, Vector2.zero);

        /// <summary>면을 안 정한 채 놓는다 — 실제 배치 경로와 같다.</summary>
        private static BeltInstance Place(BoardGrid g, Vector2Int cell, BeltElementKind element)
        {
            g.TryPlaceBeltElement(cell, element,
                new[] { PortFace.West }, new[] { PortFace.East }, FlowKind.None, out BeltInstance b);
            return b;
        }

        private static bool Has(PortFace[] faces, PortFace f)
        {
            foreach (PortFace x in faces) if (x == f) return true;
            return false;
        }

        // ---- 병합기 ----

        /// <summary>
        /// 병합기는 **여러 면으로 받아 한 면으로 낸다.** 출력면은 받아 줄 이웃이 있는 쪽이다.
        /// 코어의 서쪽 면에 붙이면 코어 쪽(동쪽)이 출구가 된다.
        /// </summary>
        [Test]
        public void Merger_PointsAtWhoeverCanReceive()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(3, 3), _core, out _);       // 코어 West 입력(탄약)
            BeltInstance m = Place(g, new Vector2Int(2, 3), BeltElementKind.Merger);

            BeltAutoOrient.Resolve(g);

            Assert.AreEqual(1, m.OutFaces.Length, "출구는 하나");
            Assert.AreEqual(PortFace.East, m.OutFaces[0], "코어 쪽으로 낸다");
            Assert.AreEqual(3, m.InFaces.Length, "나머지 세 면으로 받는다");
            Assert.IsFalse(Has(m.InFaces, PortFace.East), "출구는 입구가 아니다");
        }

        /// <summary>
        /// **세 방향에서 받는다.** 받는 쪽의 탄약 입력이 한 면뿐이라, 병합기가 없으면
        /// 군수 노드 하나만 닿을 수 있다 — 145를 만들 방법이 없어진다.
        ///
        /// ⚠️ 2026-09-05에 받는 쪽이 **코어에서 저장으로** 바뀌었다(`260904_W03` 1장 —
        /// 「코어는 시작이다」). 병합기가 왜 필요한가는 그대로다: 입구가 한 면뿐인 것은 같다.
        /// </summary>
        [Test]
        public void Merger_CollectsFromThreeSides_IntoOneStorageInput()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(3, 3), _stor, out _);
            BeltInstance m = Place(g, new Vector2Int(2, 3), BeltElementKind.Merger);

            // 병합기의 북·남·서에 군수 노드를 붙인다. 셋 다 East 출력이므로
            // 실제로 링크가 서는 것은 서쪽 하나지만, **면이 세 개 열려 있다**는 것이 요점이다.
            g.TryPlace(new Vector2Int(1, 3), _muni, out _);
            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);

            Assert.IsTrue(Has(m.InFaces, PortFace.West), "서쪽 입구가 열려 있다");
            Assert.AreEqual(FlowKind.StandardAmmo, m.Kind, "군수가 밀어 넣은 것이 흐른다");

            bool reachesCore = false;
            foreach (BeltLink l in BeltRouting.BuildLinks(g))
                if (l.toCell == new Vector2Int(3, 3) && l.kind == FlowKind.StandardAmmo) reachesCore = true;

            Assert.IsTrue(reachesCore, "군수 → 병합기 → 저장이 이어진다");
        }

        // ---- 분류기 ----

        /// <summary>분류기는 **한 면으로 받아 여러 면으로 낸다.** 입력면은 내보낼 이웃이 있는 쪽이다.</summary>
        [Test]
        public void Sorter_TakesFromWhoeverCanSend()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(1, 3), _muni, out _);       // 군수 East 출력
            BeltInstance s = Place(g, new Vector2Int(2, 3), BeltElementKind.Sorter);

            BeltAutoOrient.Resolve(g);

            Assert.AreEqual(1, s.InFaces.Length, "입구는 하나");
            Assert.AreEqual(PortFace.West, s.InFaces[0], "군수 쪽에서 받는다");
            Assert.AreEqual(3, s.OutFaces.Length, "나머지 세 면으로 낸다");
        }

        // ---- 순서 자유 ----

        /// <summary>
        /// **요소를 먼저 놓고 이웃을 나중에 붙여도 된다.** 이것이 배치 시점에 면을 안 정하는 이유다 —
        /// 고정했다면 여기서 영영 안 이어진 채로 남았을 것이다.
        /// </summary>
        [Test]
        public void PlacingTheElementFirst_StillConnects()
        {
            var g = Grid();
            BeltInstance m = Place(g, new Vector2Int(2, 3), BeltElementKind.Merger);

            BeltAutoOrient.Resolve(g);
            Assert.AreEqual(PortFace.East, m.OutFaces[0], "아직 아무것도 없으면 동쪽(기본)");

            // 이제 북쪽에 코어를 붙인다 — 코어의 남쪽 면이 전력 입력이다.
            g.TryPlace(new Vector2Int(2, 4), _core, out _);
            BeltAutoOrient.Resolve(g);

            Assert.AreEqual(PortFace.North, m.OutFaces[0], "붙이고 나면 그쪽을 가리킨다");
            Assert.IsTrue(Has(m.InFaces, PortFace.East), "동쪽이 이제 입구로 돌아섰다");
        }

        /// <summary>등을 대고 있는 노드는 이웃이 아니다 — 그 면에 포트가 없으면 지나친다.</summary>
        [Test]
        public void NodeWithoutAMatchingFace_IsNotAnOutlet()
        {
            var g = Grid();
            // 군수의 서쪽 면은 **입력**(Material)이다. 병합기를 군수 동쪽에 두면
            // 그 면에서 군수가 받아 줄 것이 없다 — 군수의 서쪽만 입력이기 때문이다.
            g.TryPlace(new Vector2Int(3, 3), _muni, out _);
            BeltInstance m = Place(g, new Vector2Int(4, 3), BeltElementKind.Merger);

            BeltAutoOrient.Resolve(g);

            Assert.AreNotEqual(PortFace.West, m.OutFaces[0], "군수의 동쪽은 출력면이라 받지 않는다");
        }

        // ---- 직선·코너는 건드리지 않는다 ----

        /// <summary>직선·코너는 드래그 경로가 방향을 정한다 — 여기서 뒤집으면 깐 대로 안 흐른다.</summary>
        [Test]
        public void StraightBelts_AreLeftAlone()
        {
            var g = Grid();
            g.TryPlaceBelt(new Vector2Int(2, 3), PortFace.West, PortFace.East, FlowKind.Material,
                out BeltInstance straight);
            g.TryPlace(new Vector2Int(2, 4), _core, out _);

            BeltAutoOrient.Resolve(g);

            Assert.AreEqual(PortFace.East, straight.OutFace, "이웃이 생겨도 그대로");
            Assert.AreEqual(PortFace.West, straight.InFace);
        }

        // ---- 결정론 ----

        [Test]
        public void Deterministic_SameBoardSameFaces()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(2, 4), _core, out _);
            g.TryPlace(new Vector2Int(3, 3), _core, out _);
            BeltInstance m = Place(g, new Vector2Int(2, 3), BeltElementKind.Merger);

            BeltAutoOrient.Resolve(g);
            PortFace first = m.OutFaces[0];

            BeltAutoOrient.Resolve(g);
            Assert.AreEqual(first, m.OutFaces[0], "다시 잡아도 같다");
            Assert.AreEqual(PortFace.North, first, "면 우선순위는 북 → 동 → 남 → 서");
        }
    }
}
