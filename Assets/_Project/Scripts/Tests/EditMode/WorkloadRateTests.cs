using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 일감률(260831_V07 승인분) — **그 노드가 지금 실제로 일하고 있는 비율.**
    ///
    /// `일감률 = 연결성(0/1) × 라인 가동률`이고 노드별 예외가 붙는다:
    /// 가공·저장 = 연결성 / 군수 = 연결성 × 라인 가동률 / **에너지 = 항상 1** /
    /// 부스터 = 추진제 스택이 차면 0(지금은 못 읽어 잠정 1) / **코어는 대상이 아니다.**
    ///
    /// 쓰이는 곳은 변동비 전력이다: 수요 = Σ(대당 전력 × 일감률).
    /// </summary>
    public sealed class WorkloadRateTests
    {
        private const float D = 0.001f;

        private NodeDefinition _muni, _core, _ener, _stor, _boost;
        private BalanceConfig _bal;

        [SetUp]
        public void SetUp()
        {
            _muni = Node("muni"); _core = Node("core"); _ener = Node("ener");
            _stor = Node("stor"); _boost = Node("boost");
            _bal = AssetDatabase.LoadAssetAtPath<BalanceConfig>(
                "Assets/_Project/ScriptableObjects/BalanceConfig.asset");
            if (_muni == null || _core == null || _ener == null || _stor == null ||
                _boost == null || _bal == null)
                Assert.Ignore("자산 없음 — 먼저 밸런스·노드 생성 메뉴를 실행해야 한다.");
        }

        private static NodeDefinition Node(string id) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>(
                $"Assets/_Project/ScriptableObjects/Nodes/Node_{id}.asset");

        private static BoardGrid Grid() => new BoardGrid(12, 12, 1f, Vector2.zero);

        /// <summary>연결성을 논점에서 빼고 노드별 예외만 본다(connected = null → 전부 이어진 것으로).</summary>
        private WorkloadRate.Result All(BoardGrid g) => WorkloadRate.Compute(g, null, _bal);

        private Vector2Int PlaceMuni(BoardGrid g, int x, int y, AmmoKind kind)
        {
            var cell = new Vector2Int(x, y);
            g.TryPlace(cell, _muni, out NodeInstance m);
            m.AmmoKind = kind;
            return cell;
        }

        /// <summary>놓인 칸 전부. 연결성을 논점에서 빼고 일감률만 보게 한다.</summary>
        private static HashSet<Vector2Int> Everything(BoardGrid g)
        {
            var set = new HashSet<Vector2Int>();
            for (int x = 0; x < g.Columns; x++)
            for (int y = 0; y < g.Rows; y++)
            {
                var cell = new Vector2Int(x, y);
                if (g.GetAt(cell) != null) set.Add(cell);
            }
            return set;
        }

        // ---- 노드별 예외 ----

        /// <summary>**코어는 대상이 아니다.** 평균에도 안 들어간다 — 늘 1이라 평균을 위로 띄운다.</summary>
        [Test]
        public void Core_IsNotCounted()
        {
            BoardGrid g = Grid();
            g.TryPlace(new Vector2Int(5, 5), _core, out _);

            WorkloadRate.Result w = All(g);

            Assert.IsFalse(w.perNode.ContainsKey(new Vector2Int(5, 5)), "코어는 표에 없다");
            Assert.AreEqual(0f, w.average, D, "코어뿐이면 셀 것이 없다");
        }

        /// <summary>
        /// **에너지는 늘 1이다.** 아무것도 안 이어졌다고 발전을 놀린 것으로 잡으면
        /// 전력이 0이 되어 보드 전체가 멈춘다 — 고치라는 안내가 아니라 벽이 된다.
        /// </summary>
        [Test]
        public void Energy_IsAlwaysOne_EvenWhenUnconnected()
        {
            BoardGrid g = Grid();
            var cell = new Vector2Int(2, 2);
            g.TryPlace(cell, _ener, out _);

            WorkloadRate.Result w = WorkloadRate.Compute(g, new HashSet<Vector2Int>(), _bal);

            Assert.AreEqual(1f, w.perNode[cell], D, "이어진 것이 하나도 없어도 1");
        }

        /// <summary>저장은 **연결성만** 본다 — 라인 가동률에 해당하는 축이 없다.</summary>
        [Test]
        public void Storage_FollowsConnectivityOnly()
        {
            BoardGrid g = Grid();
            var cell = new Vector2Int(3, 3);
            g.TryPlace(cell, _stor, out _);

            Assert.AreEqual(1f, All(g).perNode[cell], D, "이어져 있으면 1");
            Assert.AreEqual(0f,
                WorkloadRate.Compute(g, new HashSet<Vector2Int>(), _bal).perNode[cell], D,
                "안 이어져 있으면 0");
        }

        /// <summary>
        /// 부스터는 **잠정 1**이다. 추진제 스택은 CombatSimulation이 쥐고 있어 보드에서 못 읽는다
        /// (260831_V08 판정 요청 3). 값이 들어오면 0이 되는 길은 열어 둔다.
        /// </summary>
        [Test]
        public void Booster_IsProvisionallyOne_ButCanGoIdle()
        {
            BoardGrid g = Grid();
            var cell = new Vector2Int(4, 4);
            g.TryPlace(cell, _boost, out _);

            Assert.AreEqual(1f, All(g).perNode[cell], D, "스택을 못 읽으면 잠정 1");
            Assert.AreEqual(0f,
                WorkloadRate.Compute(g, null, _bal, boosterPropellantFull: true).perNode[cell], D,
                "스택이 차면 논다");
        }

        // ---- 군수: 초과분을 몰아서 0 ----

        /// <summary>스펙 안쪽이면 전원 1이다. 관통 스펙 5 → 3대는 전부 일한다.</summary>
        [Test]
        public void Munitions_UnderSpec_AllWorking()
        {
            BoardGrid g = Grid();
            for (int i = 0; i < 3; i++) PlaceMuni(g, 1, i, AmmoKind.Pierce);

            WorkloadRate.Result w = All(g);

            for (int i = 0; i < 3; i++)
                Assert.AreEqual(1f, w.perNode[new Vector2Int(1, i)], D, $"{i}번");
            Assert.AreEqual(1f, w.average, D);
        }

        /// <summary>
        /// **초과분은 노는 것으로 몰린다**(승인 원문). 관통 스펙 5에 7대면 5대가 1, 2대가 0이다.
        /// 0.71씩 골고루 나눠 주면 화면에서 어느 노드를 빼야 할지가 사라진다.
        /// </summary>
        [Test]
        public void Munitions_OverSpec_ExcessNodesGoIdle_NotSpreadEvenly()
        {
            BoardGrid g = Grid();
            for (int i = 0; i < 7; i++) PlaceMuni(g, 1, i, AmmoKind.Pierce);

            WorkloadRate.Result w = All(g);

            int working = 0, idle = 0;
            for (int i = 0; i < 7; i++)
            {
                float r = w.perNode[new Vector2Int(1, i)];
                Assert.IsTrue(r == 0f || r == 1f, $"{i}번은 0 아니면 1이다 — 나눠 주지 않는다 ({r})");
                if (r > 0f) working++; else idle++;
            }

            Assert.AreEqual(5, working, "관통 스펙 5대까지 일한다");
            Assert.AreEqual(2, idle, "나머지는 논다");
        }

        /// <summary>**총합은 평균이다**(승인 원문). 관통 7대 중 5대가 일하면 5/7.</summary>
        [Test]
        public void Average_IsTheMeanAcrossNodes()
        {
            BoardGrid g = Grid();
            for (int i = 0; i < 7; i++) PlaceMuni(g, 1, i, AmmoKind.Pierce);

            Assert.AreEqual(5f / 7f, All(g).average, D);
        }

        /// <summary>
        /// 탄종마다 **따로** 센다. 관통 5 · 폭발 2가 각자의 상한이라
        /// 관통을 넘겨 박아도 폭발 노드가 덩달아 놀지 않는다.
        /// </summary>
        [Test]
        public void EachAmmoKind_HasItsOwnCap()
        {
            BoardGrid g = Grid();
            for (int i = 0; i < 6; i++) PlaceMuni(g, 1, i, AmmoKind.Pierce);   // 스펙 5 → 1대 논다
            for (int i = 0; i < 3; i++) PlaceMuni(g, 2, i, AmmoKind.Explosive); // 스펙 2 → 1대 논다

            WorkloadRate.Result w = All(g);

            int pierceWorking = 0, explWorking = 0;
            for (int i = 0; i < 6; i++) if (w.perNode[new Vector2Int(1, i)] > 0f) pierceWorking++;
            for (int i = 0; i < 3; i++) if (w.perNode[new Vector2Int(2, i)] > 0f) explWorking++;

            Assert.AreEqual(5, pierceWorking, "관통 스펙 5");
            Assert.AreEqual(2, explWorking, "폭발 스펙 2 — 관통을 넘겼다고 같이 놀지 않는다");
        }

        /// <summary>안 이어진 군수는 스펙 안쪽이어도 0이다 — 연결성이 먼저 곱해진다.</summary>
        [Test]
        public void Munitions_Unconnected_IsZero_EvenUnderSpec()
        {
            BoardGrid g = Grid();
            Vector2Int cell = PlaceMuni(g, 1, 1, AmmoKind.Pierce);

            Assert.AreEqual(0f,
                WorkloadRate.Compute(g, new HashSet<Vector2Int>(), _bal).perNode[cell], D);
        }

        /// <summary>같은 보드면 **같은 노드가** 초과분이 된다. 집계마다 갈리면 화면이 흔들린다.</summary>
        [Test]
        public void Deterministic_SameBoardSameIdleNodes()
        {
            BoardGrid g = Grid();
            for (int i = 0; i < 7; i++) PlaceMuni(g, 1, i, AmmoKind.Pierce);

            WorkloadRate.Result a = All(g), b = All(g);

            foreach (KeyValuePair<Vector2Int, float> kv in a.perNode)
                Assert.AreEqual(kv.Value, b.perNode[kv.Key], D, kv.Key.ToString());
        }

        // ---- 변동비 전력 ----

        /// <summary>
        /// **노는 노드는 전력을 안 먹는다.** 이것이 일감률을 만든 이유다 —
        /// 초과분이 일하는 노드와 같은 전력을 먹으면 「덜어내라」가 해답이 되지 않는다.
        ///
        /// ⚠️ 대기 전력은 **0 센티넬**이다. 「유휴 시 대당의 3~5%」는 미승인이다.
        /// </summary>
        [Test]
        public void Power_IsVariable_IdleNodesDrawNothing()
        {
            BoardGrid g = Grid();
            for (int i = 0; i < 7; i++) PlaceMuni(g, 1, i, AmmoKind.Pierce);

            HashSet<Vector2Int> all = Everything(g);
            NetworkAggregate fixedCost = LogisticsNetwork.Aggregate(g, all);
            NetworkAggregate variable = LogisticsNetwork.Aggregate(g, all, WorkloadRate.Compute(g, all, _bal));

            if (fixedCost.powerDraw <= 0f) Assert.Ignore("군수 대당 전력이 TBD(0) — 비교할 값이 없다");

            Assert.Less(variable.powerDraw, fixedCost.powerDraw, "노는 2대만큼 수요가 줄었다");
            Assert.AreEqual(fixedCost.powerDraw * 5f / 7f, variable.powerDraw, 0.01f,
                "7대 중 5대분 — 나머지는 0 센티넬");
        }

        /// <summary>일감률을 **안 주면** 종전대로 전부 만가동이다(비파괴).</summary>
        [Test]
        public void WithoutWorkload_NothingChanges()
        {
            BoardGrid g = Grid();
            for (int i = 0; i < 7; i++) PlaceMuni(g, 1, i, AmmoKind.Pierce);

            NetworkAggregate agg = LogisticsNetwork.Aggregate(g, Everything(g));

            Assert.AreEqual(1f, agg.workloadAverage, D, "안 주면 1로 본다");
        }
    }
}
