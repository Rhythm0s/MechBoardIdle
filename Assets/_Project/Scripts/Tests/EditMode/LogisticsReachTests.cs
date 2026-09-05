using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **이어진 노드만 센다**(260829_V03 §판정① A안).
    ///
    /// 종전에는 격자에 놓인 노드를 전부 세고 벨트를 아예 안 봤다 —
    /// 코어까지 한 칸도 안 이어져도 출력이 그대로 나왔고, 벨트를 전부 지워도 같았다.
    /// 그 상태에서는 「물류 **라인**을 최적화하는 행위가 재미있는가」에서 최적화할 라인이 없다.
    ///
    /// 회귀 확인 2건(80 / 145)의 전제가 **「전부 이어져 있음」** 으로 바뀐다.
    /// </summary>
    public sealed class LogisticsReachTests
    {
        private const float D = 0.001f;

        private NodeDefinition _muni, _core, _boost, _stor;
        private BalanceConfig _bal;

        [SetUp]
        public void SetUp()
        {
            _muni = Node("muni");
            _core = Node("core");
            _boost = Node("boost");
            _stor = Node("stor");
            _bal = AssetDatabase.LoadAssetAtPath<BalanceConfig>(
                "Assets/_Project/ScriptableObjects/BalanceConfig.asset");
            if (_muni == null || _core == null || _boost == null || _stor == null || _bal == null)
                Assert.Ignore("자산 없음 — 먼저 메뉴 'MBI/Generate Balance + Nodes' 실행.");
        }

        private static NodeDefinition Node(string id) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>(
                $"Assets/_Project/ScriptableObjects/Nodes/Node_{id}.asset");

        private static BoardGrid Grid() => new BoardGrid(12, 12, 1f, Vector2.zero);

        /// <summary>보드가 바뀌면 실제 경로와 같은 순서로 푼다: 면 → 품목.</summary>
        private static NetworkAggregate Settle(BoardGrid g)
        {
            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return LogisticsNetwork.Aggregate(g, LogisticsReach.ConnectedNodes(g));
        }

        private float Output(NetworkAggregate agg)
        {
            var lines = new List<MunitionsLine>
            {
                new MunitionsLine(AmmoKind.Pierce, _bal.LineSpecOf(AmmoKind.Pierce), 20f, agg.muniPierce),
                new MunitionsLine(AmmoKind.Split, _bal.LineSpecOf(AmmoKind.Split), 25f, agg.muniSplit),
                new MunitionsLine(AmmoKind.Explosive, _bal.LineSpecOf(AmmoKind.Explosive), 50f, agg.muniExplosive),
            };
            return AmmoLineProduction.TotalOutput(lines, _bal.muniPerNode);
        }

        /// <summary>
        /// 저장(5,5) 서쪽에 병합기를 두고, 그 서·북·남 세 면에서 군수 라인을 받는다.
        /// 저장의 탄약 입구가 **한 면뿐**이라 여러 라인을 모으려면 병합기가 있어야 한다.
        ///
        /// ⚠️ **도착지가 코어에서 저장으로 바뀌었다**(2026-09-05 · `260904_W03` 1장).
        /// 코어가 탄약을 받던 동안에는 여기가 코어였는데, 「코어는 시작이다」 판정으로
        /// 코어에서 탄약 입력면이 사라졌다. **값은 하나도 안 건드렸다** — 20·45·80·145는
        /// 「이어진 군수 노드의 공칭 출력 합」이라 도착지가 무엇이든 같은 뜻이다.
        /// 바뀐 것은 라인이 어디서 끝나는가뿐이다.
        /// </summary>
        private void BuildHub(BoardGrid g, out Vector2Int merger)
        {
            g.TryPlace(new Vector2Int(5, 5), _stor, out _);
            merger = new Vector2Int(4, 5);
            g.TryPlaceBeltElement(merger, BeltElementKind.Merger,
                new[] { PortFace.West }, new[] { PortFace.East }, FlowKind.None, out _);
        }

        /// <summary>병합기 서쪽 직결 라인 하나.</summary>
        private void AddWestLine(BoardGrid g, AmmoKind kind)
        {
            g.TryPlace(new Vector2Int(3, 5), _muni, out NodeInstance m);
            m.AmmoKind = kind;
        }

        /// <summary>병합기 북쪽 라인(군수 → 벨트 → 병합기).</summary>
        private void AddNorthLine(BoardGrid g, AmmoKind kind)
        {
            g.TryPlace(new Vector2Int(3, 6), _muni, out NodeInstance m);
            m.AmmoKind = kind;
            g.TryPlaceBelt(new Vector2Int(4, 6), PortFace.West, PortFace.South, FlowKind.None, out _);
        }

        /// <summary>
        /// 병합기 남쪽 라인. 여기도 병합기를 쓴다 — 직선 벨트는 입구가 하나뿐이라
        /// 넷째 줄이 물릴 자리가 없다. **대역을 늘리려면 병렬 경로**의 실제 모습이다.
        /// </summary>
        private void AddSouthLine(BoardGrid g, AmmoKind kind)
        {
            g.TryPlace(new Vector2Int(3, 4), _muni, out NodeInstance m);
            m.AmmoKind = kind;
            g.TryPlaceBeltElement(new Vector2Int(4, 4), BeltElementKind.Merger,
                new[] { PortFace.West }, new[] { PortFace.East }, FlowKind.None, out _);
        }

        // ---- 게이트가 실제로 작동하는가 ----

        /// <summary>
        /// **안 이어진 군수 노드는 0이다.** 이것이 안 되면 배선이 게임이 아니다 —
        /// 종전에는 여기서 145가 나왔다.
        /// </summary>
        [Test]
        public void UnwiredMunitions_ContributeNothing()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(5, 5), _core, out _);
            for (int i = 0; i < 4; i++)
            {
                g.TryPlace(new Vector2Int(1, i), _muni, out NodeInstance m);
                m.AmmoKind = AmmoKind.Pierce;
            }

            NetworkAggregate agg = Settle(g);

            Assert.AreEqual(0, agg.muniPierce, "코어에 안 닿는다");
            Assert.AreEqual(0f, Output(agg), D, "출력 0");
            Assert.IsTrue(agg.hasCore, "코어는 허브라 언제나 센다");
        }

        /// <summary>이으면 센다 — 같은 노드가 벨트 한 칸으로 살아난다.</summary>
        [Test]
        public void WiringTheSameNode_TurnsItOn()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(5, 5), _stor, out _);
            g.TryPlace(new Vector2Int(3, 5), _muni, out NodeInstance m);
            m.AmmoKind = AmmoKind.Pierce;

            Assert.AreEqual(0f, Output(Settle(g)), D, "떨어져 있으면 0");

            g.TryPlaceBelt(new Vector2Int(4, 5), PortFace.West, PortFace.East, FlowKind.None, out _);

            Assert.AreEqual(20f, Output(Settle(g)), D, "이으면 20");
        }

        /// <summary>
        /// **끊으면 값이 떨어진다**(V03이 요청한 새 테스트).
        /// 「벨트를 전부 지워도 출력이 그대로」가 통과하던 상태의 정반대를 고정한다.
        /// </summary>
        [Test]
        public void CuttingTheLine_DropsTheOutput()
        {
            var g = Grid();
            BuildHub(g, out _);
            AddWestLine(g, AmmoKind.Pierce);
            AddNorthLine(g, AmmoKind.Split);

            Assert.AreEqual(45f, Output(Settle(g)), D, "20 + 25");

            g.TryRemoveBelt(new Vector2Int(4, 6)); // 북쪽 라인의 벨트 한 칸을 끊는다

            Assert.AreEqual(20f, Output(Settle(g)), D, "분열이 떨어져 나갔다");
        }

        // ---- 회귀 확인 2건 (전제 = 전부 이어져 있음) ----

        /// <summary>
        /// 군수 4노드를 관통에 몰면 **80**. 관통 스펙이 5라 4노드는 4발/초에서 멈춘다.
        /// 병합기 하나가 여는 입구가 셋이라 넷째 줄은 벨트로 앞 라인에 물려 넣는다.
        /// </summary>
        [Test]
        public void Regression_FourWiredPierceNodes_Output80()
        {
            var g = Grid();
            BuildHub(g, out _);
            AddWestLine(g, AmmoKind.Pierce);
            AddNorthLine(g, AmmoKind.Pierce);
            AddSouthLine(g, AmmoKind.Pierce);

            // 넷째 줄: 남쪽 라인의 벨트에 병합기를 하나 더 물린다.
            g.TryPlace(new Vector2Int(3, 3), _muni, out NodeInstance fourth);
            fourth.AmmoKind = AmmoKind.Pierce;
            g.TryPlaceBeltElement(new Vector2Int(4, 3), BeltElementKind.Merger,
                new[] { PortFace.West }, new[] { PortFace.East }, FlowKind.None, out _);

            NetworkAggregate agg = Settle(g);

            Assert.AreEqual(4, agg.muniPierce, "넷 다 이어졌다");
            Assert.AreEqual(80f, Output(agg), D, "관통 4노드 = 80");
        }

        /// <summary>
        /// 관통1 · 분열1 · 폭발2 = **145**(대표 배치 · s3Break).
        /// 전제가 「전부 이어져 있음」으로 바뀌었을 뿐 값은 그대로다.
        /// </summary>
        [Test]
        public void Regression_WiredRepresentativeMix_Output145()
        {
            var g = Grid();
            BuildHub(g, out _);
            AddWestLine(g, AmmoKind.Pierce);
            AddNorthLine(g, AmmoKind.Split);
            AddSouthLine(g, AmmoKind.Explosive);

            g.TryPlace(new Vector2Int(3, 3), _muni, out NodeInstance fourth);
            fourth.AmmoKind = AmmoKind.Explosive;
            g.TryPlaceBeltElement(new Vector2Int(4, 3), BeltElementKind.Merger,
                new[] { PortFace.West }, new[] { PortFace.East }, FlowKind.None, out _);

            NetworkAggregate agg = Settle(g);

            Assert.AreEqual(1, agg.muniPierce);
            Assert.AreEqual(1, agg.muniSplit);
            Assert.AreEqual(2, agg.muniExplosive);
            Assert.AreEqual(145f, Output(agg), D, "대표 배치 = 145 = s3Break");
        }

        // ---- 시작 보드(온보딩) ----

        /// <summary>
        /// **한 칸을 비운 배치가 실제로 도는지**를 잰다 — 「빈 벨트가 회색으로 남고, 채우면 색이 들며
        /// 출력이 오른다」는 온보딩의 뼈대다.
        ///
        /// ⚠️ 여기 깔린 것은 **예시 배치이지 출하되는 시작 보드가 아니다.**
        /// 실제 시작 보드는 <c>StartingBoard</c>가 쥐고 있고 관통 4 + 빈 칸 = 80 → 100이며,
        /// 그 숫자는 <c>StartingBoardTests</c>가 지킨다(260831_V11).
        /// 종전에는 이 테스트가 시작 보드를 자처했는데, 보드가 바뀐 뒤에도 이름만 남아 있었다.
        /// </summary>
        [Test]
        public void OneEmptyCell_LeavesTheBeltGrey_AndFillingItRaisesOutput()
        {
            var g = Grid();
            NodeDefinition ener = Node("ener");

            g.TryPlace(new Vector2Int(5, 7), _stor, out _);
            g.TryPlaceBeltElement(new Vector2Int(4, 7), BeltElementKind.Merger,
                new[] { PortFace.West }, new[] { PortFace.East }, FlowKind.None, out _);

            g.TryPlace(new Vector2Int(3, 7), _muni, out NodeInstance pierce);
            pierce.AmmoKind = AmmoKind.Pierce;

            g.TryPlace(new Vector2Int(3, 8), _muni, out NodeInstance split);
            split.AmmoKind = AmmoKind.Split;
            g.TryPlaceBelt(new Vector2Int(4, 8), PortFace.West, PortFace.South, FlowKind.None, out _);

            g.TryPlaceBelt(new Vector2Int(4, 6), PortFace.West, PortFace.North, FlowKind.None, out _);

            // 전력 라인은 **코어**로 간다 — 저장은 전력 입력이 없다. 탄약 도착지가 저장으로
            // 옮겨 가면서 둘을 갈라 놓아야 했다(2026-09-05). 코어는 남쪽으로만 받으므로
            // 바로 아래 칸에서 북으로 꺾어 올린다.
            g.TryPlace(new Vector2Int(4, 5), ener, out _);
            g.TryPlaceBelt(new Vector2Int(5, 5), PortFace.West, PortFace.East, FlowKind.None, out _);
            g.TryPlaceBelt(new Vector2Int(6, 5), PortFace.West, PortFace.North, FlowKind.None, out _);
            g.TryPlace(new Vector2Int(6, 6), _core, out _);

            NetworkAggregate start = Settle(g);
            Assert.AreEqual(45f, Output(start), D, "관통 + 분열 = 20 + 25");
            Assert.AreEqual(FlowKind.None, BeltFlow.KindAt(g, new Vector2Int(4, 6)),
                "빈칸의 벨트는 비어 있다 — 그것이 다음에 할 일의 표시다");
            // 고정비도 **이어진 노드만** 센다: 코어 0 + 에너지 1 + 군수 2대 × 2 = 5.
            // 에너지가 대당 1을 먹게 되면서 4에서 5로 올랐다(260901_V02 §2층).
            Assert.AreEqual(5f, start.powerDraw, D, "군수 2대 + 에너지 1대분");

            // 빈칸을 채운다.
            g.TryPlace(new Vector2Int(3, 6), _muni, out NodeInstance expl);
            expl.AmmoKind = AmmoKind.Explosive;

            NetworkAggregate filled = Settle(g);
            Assert.AreEqual(95f, Output(filled), D, "+ 폭발 50");
            Assert.AreEqual(FlowKind.StandardAmmo, BeltFlow.KindAt(g, new Vector2Int(4, 6)),
                "벨트에 색이 든다");
        }

        // ---- 부스터 ----

        /// <summary>
        /// **추진제가 안 오는 부스터는 안 센다.** 받기만 하는 노드라 이어진 공급원이 닿아야 한다 —
        /// 놓기만 해서 회피 상한이 늘면 「보드가 생존을 만든다」가 배선을 건너뛴다.
        /// </summary>
        [Test]
        public void UnfedBooster_IsNotCounted()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(5, 5), _core, out _);
            g.TryPlace(new Vector2Int(1, 1), _boost, out _);

            Assert.AreEqual(0, Settle(g).boosterCount, "추진제가 안 온다");
        }

        /// <summary>이어 주면 센다 — 군수(추진제) → 벨트 → 부스터.</summary>
        [Test]
        public void FedBooster_Counts()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(5, 5), _core, out _);
            g.TryPlace(new Vector2Int(1, 1), _muni, out NodeInstance m);
            m.SelectRecipe(RecipeKind.Propellant);
            g.TryPlaceBelt(new Vector2Int(2, 1), PortFace.West, PortFace.East, FlowKind.None, out _);
            g.TryPlace(new Vector2Int(3, 1), _boost, out _);

            NetworkAggregate agg = Settle(g);

            Assert.AreEqual(1, agg.boosterCount, "추진제가 닿는다");
            Assert.AreEqual(1f / 15f, agg.propellantProduce, D, "그 군수 노드도 함께 센다");
        }

        // ---- 대역 ----

        /// <summary>
        /// 총 대역 = **경로 수 × 한 줄 처리량**. 길게 늘여도 경로는 하나다 —
        /// 길이는 대역이 아니라 지연을 늘린다.
        /// </summary>
        [Test]
        public void LengtheningALine_DoesNotAddBandwidth()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(8, 5), _stor, out _);
            g.TryPlace(new Vector2Int(3, 5), _muni, out _);
            for (int x = 4; x <= 7; x++)
                g.TryPlaceBelt(new Vector2Int(x, 5), PortFace.West, PortFace.East, FlowKind.None, out _);

            Assert.AreEqual(1, Settle(g).ammoPaths, "네 칸을 깔아도 경로는 하나");
        }

        /// <summary>병렬 경로를 놓아야 대역이 는다 — 그래서 병합기·분류기가 대역의 수단이다.</summary>
        [Test]
        public void ParallelPaths_AddBandwidth()
        {
            var g = Grid();
            BuildHub(g, out _);
            AddWestLine(g, AmmoKind.Pierce);
            AddNorthLine(g, AmmoKind.Split);

            int one = Settle(g).ammoPaths;

            AddSouthLine(g, AmmoKind.Explosive);
            int two = Settle(g).ammoPaths;

            Assert.Greater(two, 0);
            Assert.GreaterOrEqual(two, one, "경로를 더 놓으면 줄지 않는다");
        }

        /// <summary>벨트 없이 직접 붙여도 경로다 — 벨트 0칸짜리 경로다.</summary>
        [Test]
        public void DirectAdjacency_CountsAsAPath()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(5, 5), _stor, out _);
            g.TryPlace(new Vector2Int(4, 5), _muni, out _); // 군수 East → 저장 West

            Assert.AreEqual(1, Settle(g).ammoPaths, "직결도 한 경로");
        }
    }
}
