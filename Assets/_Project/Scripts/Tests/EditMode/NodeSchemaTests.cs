using System.Collections.Generic;
using System.Linq;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;

namespace MBI.Tests
{
    /// <summary>
    /// 노드 SO 스키마 구조 검증(CLAUDE.md §4).
    /// balance.json에 노드별 실측치가 없어 값 검증은 불가 — 구조/연결 규칙만 검증(§7 오표기 방지).
    /// 실행 전 메뉴 MBI/Generate Balance + Nodes 로 자산 생성 필요.
    /// </summary>
    public sealed class NodeSchemaTests
    {
        private const string NodesDir = "Assets/_Project/ScriptableObjects/Nodes";
        private const string CorePath = NodesDir + "/Node_core.asset";
        private const string MuniPath = NodesDir + "/Node_muni.asset";
        private const string ShieldPath = NodesDir + "/Node_shield.asset";

        private List<NodeDefinition> _nodes;

        [SetUp]
        public void SetUp()
        {
            _nodes = AssetDatabase.FindAssets("t:NodeDefinition", new[] { NodesDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<NodeDefinition>)
                .Where(n => n != null)
                .ToList();

            if (_nodes.Count == 0)
                Assert.Ignore($"노드 자산 없음: {NodesDir} — 먼저 메뉴 'MBI/Generate Balance + Nodes' 실행.");
        }

        // ---- (a) NodeType 7종 자산 존재(2026-08-29 부스터 신설) ----
        [Test]
        public void AllNodeTypes_Exist()
        {
            var present = _nodes.Select(n => n.type).Distinct().ToList();
            foreach (NodeType t in System.Enum.GetValues(typeof(NodeType)))
                Assert.Contains(t, present, $"노드 타입 {t} 자산 누락");
        }

        // ---- (b) 쉴드발생만 implemented=false ----
        [Test]
        public void OnlyShield_IsStub()
        {
            foreach (NodeDefinition n in _nodes)
            {
                bool expectedImplemented = n.type != NodeType.Shield;
                Assert.AreEqual(expectedImplemented, n.implemented,
                    $"{n.displayName}({n.type}) implemented 기대={expectedImplemented}");
            }
        }

        // ---- (c) 구현 노드는 포트를 갖고, 노드별 수치는 확정분 외에는 Tbd로 표기 ----
        [Test]
        public void ImplementedNodes_HavePorts_AndValuesAreTbd()
        {
            foreach (NodeDefinition n in _nodes)
            {
                // 군수만 확정: 노드 1개당 생산 1발/초(params muniPerNode, 2026-08-25 확정).
                // 나머지 6종의 전력·발열은 노드 카탈로그와 함께 확정되므로 아직 Tbd다.
                ConfirmState expected = n.type == NodeType.Munitions
                    ? ConfirmState.Confirmed
                    : ConfirmState.Tbd;

                Assert.AreEqual(expected, n.resources.confirm,
                    $"{n.displayName}: 확정분 외 노드별 수치는 Tbd 표기 필요(§7)");

                if (n.implemented)
                    Assert.Greater(n.ports.Count, 0, $"{n.displayName}: 구현 노드는 포트 필요");
            }
        }

        /// <summary>
        /// 군수 노드 생산량은 **소비 상한(capA 6)이 아니라** 노드당 생산(muniPerNode 1)이다.
        /// capA를 여기에 넣던 시기에는 노드 1개가 상한을 다 채워 두 번째 노드부터 출력 영향이
        /// 0이었고, 격자를 넓혀도 출력이 상수였다(CLAUDE.md §7 등재분의 회귀 방지).
        /// </summary>
        [Test]
        public void MunitionsNode_ProducesOneRoundPerSecond_NotConsumptionCap()
        {
            NodeDefinition muni = AssetDatabase.LoadAssetAtPath<NodeDefinition>(MuniPath);
            Assert.NotNull(muni, "군수 자산");

            Assert.AreEqual(1f, muni.resources.ammoProduce, 0.001f,
                "군수 노드 1개 = 1발/초 (params muniPerNode)");
            Assert.AreNotEqual(6f, muni.resources.ammoProduce,
                "6은 마운트 소비 상한(capA)이다 — 생산 자리에 들어가면 안 된다");
        }

        // ---- (d) 연결 규칙: 군수 출력(Ammo) → 코어 입력(Ammo) 대칭 성립 ----
        [Test]
        public void ConnectionRule_MunitionsAmmo_ConnectsToCore()
        {
            NodeDefinition core = AssetDatabase.LoadAssetAtPath<NodeDefinition>(CorePath);
            NodeDefinition muni = AssetDatabase.LoadAssetAtPath<NodeDefinition>(MuniPath);
            Assert.NotNull(core, "코어 자산");
            Assert.NotNull(muni, "군수 자산");

            // 군수 East(Output, Ammo) ↔ 코어 West(Input, Ammo). Opposite(East)=West.
            bool ok = NodeConnectionRules.TryConnect(muni, PortFace.East, core, out FlowKind kind);
            Assert.IsTrue(ok, "군수→코어 탄약 연결 성립");
            Assert.AreEqual(FlowKind.Ammo, kind);
        }

        // ---- (d') 반대 방향/스텁은 연결 불가 ----
        [Test]
        public void ConnectionRule_RejectsMismatchAndStub()
        {
            NodeDefinition core = AssetDatabase.LoadAssetAtPath<NodeDefinition>(CorePath);
            NodeDefinition muni = AssetDatabase.LoadAssetAtPath<NodeDefinition>(MuniPath);
            NodeDefinition shield = AssetDatabase.LoadAssetAtPath<NodeDefinition>(ShieldPath);

            // 코어 East에는 출력 Ammo 포트가 없음 → 반대 방향 불성립.
            Assert.IsFalse(NodeConnectionRules.TryConnect(core, PortFace.East, muni),
                "코어→군수 역방향은 성립하면 안 됨");

            // 스텁(쉴드)은 연결 대상에서 제외.
            Assert.IsFalse(NodeConnectionRules.TryConnect(muni, PortFace.East, shield),
                "스텁 노드는 연결 제외");
        }
    }
}
