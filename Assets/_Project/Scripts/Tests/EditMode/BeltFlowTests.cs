using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 벨트가 무엇을 나르는가 — 상류 노드의 산출이 체인을 타고 내려간다.
    ///
    /// 이 계층이 없던 동안 벨트는 설치 시 임의 품목(Material)으로 깔렸고,
    /// 군수 노드(탄약)와 종류가 안 맞아 **링크가 아예 서지 않았다.**
    /// </summary>
    public sealed class BeltFlowTests
    {
        private const string NodesDir = "Assets/_Project/ScriptableObjects/Nodes";

        private NodeDefinition _muni, _core, _boost, _stor;

        [SetUp]
        public void SetUp()
        {
            _muni = Load("muni");
            _core = Load("core");
            _boost = Load("boost");
            _stor = Load("stor");
            if (_muni == null || _core == null || _boost == null || _stor == null)
                Assert.Ignore("노드 자산 없음 — 먼저 메뉴 'MBI/Generate Balance + Nodes' 실행.");
        }

        private static NodeDefinition Load(string id) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodesDir}/Node_{id}.asset");

        private static BoardGrid Grid() => new BoardGrid(10, 10, 1f, Vector2.zero);

        /// <summary>서→동으로 흐르는 직선 벨트 한 칸.</summary>
        private static void LayStraight(BoardGrid g, int x, int y) =>
            g.TryPlaceBelt(new Vector2Int(x, y), PortFace.West, PortFace.East, FlowKind.Material, out _);

        private static FlowKind KindAt(BoardGrid g, int x, int y) =>
            BeltFlow.KindAt(g, new Vector2Int(x, y));

        // ---- 산출의 출처 ----

        /// <summary>
        /// **포트가 아니라 조합표가 산출을 정한다.** 군수 노드의 출력 포트는 「탄약」 하나뿐인데
        /// 추진제 조합표를 돌리면 나오는 것은 추진제다. 포트만 보면 추진제 노드에서
        /// 탄약 벨트가 뻗어 나가 부스터에 안 붙는다.
        /// </summary>
        [Test]
        public void OutputKind_ComesFromRecipe_NotPort()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(1, 1), _muni, out NodeInstance muni);

            Assert.AreEqual(FlowKind.Ammo, BeltFlow.OutputKindOf(muni), "기본 조합표는 탄약");

            muni.SelectRecipe(RecipeKind.Propellant);
            Assert.AreEqual(FlowKind.Propellant, BeltFlow.OutputKindOf(muni), "조합표가 이긴다");

            muni.SelectRecipe(RecipeKind.DroneBody);
            Assert.AreEqual(FlowKind.Drone, BeltFlow.OutputKindOf(muni));
        }

        /// <summary>조합표가 없는 노드는 포트에 적힌 것이 그대로 산출이다.</summary>
        [Test]
        public void NodesWithoutRecipes_UseTheirPort()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(1, 1), _core, out NodeInstance core);

            Assert.AreEqual(FlowKind.Material, BeltFlow.OutputKindOf(core), "코어는 물류 품목을 낸다");
        }

        // ---- 전파 ----

        /// <summary>군수 노드 동쪽으로 깐 벨트는 **탄약**을 나른다 — 설치 시의 임의 품목이 덮인다.</summary>
        [Test]
        public void BeltNextToMunitions_CarriesAmmo()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(1, 1), _muni, out _);
            LayStraight(g, 2, 1);

            Assert.AreEqual(1, BeltFlow.Resolve(g));
            Assert.AreEqual(FlowKind.Ammo, KindAt(g, 2, 1));
        }

        /// <summary>체인 전체가 같은 것을 나른다 — 한 칸만 정해지면 라인이 끊긴다.</summary>
        [Test]
        public void WholeChain_CarriesTheSameThing()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(1, 1), _muni, out _);
            for (int x = 2; x <= 6; x++) LayStraight(g, x, 1);

            Assert.AreEqual(5, BeltFlow.Resolve(g));
            for (int x = 2; x <= 6; x++)
                Assert.AreEqual(FlowKind.Ammo, KindAt(g, x, 1), $"{x}칸");
        }

        /// <summary>
        /// 조합표를 바꾸면 **하류 라인이 통째로 바뀐다.** 이게 안 되면 추진제로 돌려도
        /// 벨트는 탄약을 나르고 있어 부스터에 안 붙는다.
        /// </summary>
        [Test]
        public void SwitchingRecipe_RepaintsTheWholeLine()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(1, 1), _muni, out NodeInstance muni);
            for (int x = 2; x <= 5; x++) LayStraight(g, x, 1);

            BeltFlow.Resolve(g);
            Assert.AreEqual(FlowKind.Ammo, KindAt(g, 5, 1));

            muni.SelectRecipe(RecipeKind.Propellant);
            BeltFlow.Resolve(g);

            for (int x = 2; x <= 5; x++)
                Assert.AreEqual(FlowKind.Propellant, KindAt(g, x, 1), $"{x}칸");
        }

        /// <summary>
        /// 상류 노드를 뽑으면 라인이 **비워진다.** 안 비우면 노드를 뽑아도 벨트가 계속
        /// 살아 있는 것처럼 보여 「보드가 결과를 바꾼다」가 화면에서 깨진다.
        /// </summary>
        [Test]
        public void RemovingTheSource_EmptiesTheLine()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(1, 1), _muni, out _);
            for (int x = 2; x <= 4; x++) LayStraight(g, x, 1);
            BeltFlow.Resolve(g);

            g.TryRemove(new Vector2Int(1, 1));
            Assert.AreEqual(0, BeltFlow.Resolve(g), "정해진 벨트가 없다");

            for (int x = 2; x <= 4; x++)
                Assert.AreEqual(FlowKind.None, KindAt(g, x, 1), $"{x}칸이 비었다");
        }

        /// <summary>상류가 없는 벨트는 비어 있다 — 「깔았는데 안 흐른다」가 값으로 남는다.</summary>
        [Test]
        public void OrphanBelt_IsEmpty()
        {
            var g = Grid();
            LayStraight(g, 4, 4);

            Assert.AreEqual(0, BeltFlow.Resolve(g));
            Assert.AreEqual(FlowKind.None, KindAt(g, 4, 4));
        }

        /// <summary>
        /// 벨트가 그 면을 **입력면으로** 가져야 받는다. 노드 서쪽에 붙은 서→동 벨트는
        /// 등을 대고 있는 것이라 닿아 있어도 안 받는다.
        /// </summary>
        [Test]
        public void TouchingWithoutAnInputFace_ReceivesNothing()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(3, 1), _muni, out _);
            LayStraight(g, 2, 1); // 노드 서쪽 — 이 벨트의 출력면이 노드를 향한다

            Assert.AreEqual(0, BeltFlow.Resolve(g));
            Assert.AreEqual(FlowKind.None, KindAt(g, 2, 1));
        }

        // ---- 연결 판정과의 관계 ----

        /// <summary>
        /// **흘리지 않으면 링크가 서지 않는다.** BuildLinks가 벨트↔벨트에 품목 일치를 요구하므로,
        /// 설치 시의 임의 품목으로는 군수 노드에서 나온 라인이 이어지지 않는다.
        /// 이 테스트가 BeltFlow가 존재하는 이유 자체다.
        /// </summary>
        [Test]
        public void WithoutResolve_TheLineDoesNotConnect()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(1, 1), _muni, out _);
            LayStraight(g, 2, 1);
            LayStraight(g, 3, 1);

            int before = BeltRouting.BuildLinks(g).Count;

            BeltFlow.Resolve(g);
            int after = BeltRouting.BuildLinks(g).Count;

            Assert.Greater(after, before, "흘린 뒤에 링크가 선다");
        }

        /// <summary>군수 → 벨트 → 코어가 실제로 이어진다(탄약 라인 전 구간).</summary>
        [Test]
        public void MunitionsToCore_ConnectsThroughTheBelt()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(1, 1), _muni, out _);   // East 출력(탄약)
            LayStraight(g, 2, 1);
            g.TryPlace(new Vector2Int(3, 1), _core, out _);   // West 입력(탄약)

            BeltFlow.Resolve(g);

            bool reachesCore = false;
            foreach (BeltLink l in BeltRouting.BuildLinks(g))
                if (l.toCell == new Vector2Int(3, 1) && l.kind == FlowKind.Ammo) reachesCore = true;

            Assert.IsTrue(reachesCore, "벨트가 탄약을 코어까지 나른다");
        }

        /// <summary>
        /// 추진제 라인이 부스터에 닿는다 — 조합표를 바꾸는 것만으로 목적지가 갈린다.
        /// 이게 부스터 노드가 보드에서 작동하는 전 구간이다.
        /// </summary>
        [Test]
        public void PropellantLine_ReachesTheBooster()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(1, 1), _muni, out NodeInstance muni);
            LayStraight(g, 2, 1);
            g.TryPlace(new Vector2Int(3, 1), _boost, out _); // West 입력(추진제)

            muni.SelectRecipe(RecipeKind.Propellant);
            BeltFlow.Resolve(g);

            bool reachesBooster = false;
            foreach (BeltLink l in BeltRouting.BuildLinks(g))
                if (l.toCell == new Vector2Int(3, 1) && l.kind == FlowKind.Propellant) reachesBooster = true;

            Assert.IsTrue(reachesBooster, "추진제가 부스터까지 간다");
        }

        /// <summary>탄약 라인은 부스터에 안 붙는다 — 품목이 다르면 연결이 아니다.</summary>
        [Test]
        public void AmmoLine_DoesNotFeedTheBooster()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(1, 1), _muni, out _);  // 기본 조합표 = 탄약
            LayStraight(g, 2, 1);
            g.TryPlace(new Vector2Int(3, 1), _boost, out _);

            BeltFlow.Resolve(g);

            foreach (BeltLink l in BeltRouting.BuildLinks(g))
                Assert.AreNotEqual(new Vector2Int(3, 1), l.toCell, "탄약은 부스터가 안 받는다");
        }

        // ---- 결정론 ----

        /// <summary>같은 보드는 언제나 같은 결과 — 두 상류가 겹쳐도 먼저 닿은 쪽이 이긴다.</summary>
        [Test]
        public void Deterministic_SameBoardSameResult()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(1, 1), _muni, out _);
            g.TryPlace(new Vector2Int(1, 2), _stor, out _);
            for (int x = 2; x <= 5; x++) { LayStraight(g, x, 1); LayStraight(g, x, 2); }

            BeltFlow.Resolve(g);
            FlowKind first = KindAt(g, 5, 1);

            BeltFlow.Resolve(g);
            Assert.AreEqual(first, KindAt(g, 5, 1), "다시 흘려도 같다");
        }
    }
}
