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
                // 부하 열이 **전부** 확정된 노드만 Confirmed다(260829_V03).
                //   코어 — 고정비 0 하나뿐이고 그게 확정
                //   군수 — 생산 1발/초 + 고정비 2 둘 다 확정
                //   가공 — 고정비 1은 확정이나 **발열이 표에 없다**
                //   에너지 — 고정비 0 · 발열 1은 확정이나 **대당 발전량이 미확정**
                //   저장·부스터·쉴드 — 값 자체가 없다
                ConfirmState expected =
                    n.type == NodeType.Munitions || n.type == NodeType.Core
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
        /// <summary>
        /// 노드 **대당** 부하(260829_V03 §판정①). 종전에는 네트워크 합계(pw 66)를
        /// 코어 한 대가 통째로 졌다 — 그러면 노드를 늘려도 부하가 안 늘어
        /// 「비용을 내고 놓는다」가 성립하지 않는다.
        /// </summary>
        [Test]
        public void PerNodeLoad_IsPerNode_NotTheNetworkTotal()
        {
            Assert.AreEqual(0f, Node("core").resources.powerDraw, 0.001f, "코어 고정비 0");
            Assert.AreEqual(1f, Node("proc").resources.powerDraw, 0.001f, "가공 1/초");
            Assert.AreEqual(2f, Node("muni").resources.powerDraw, 0.001f, "군수 2/초");
            Assert.AreEqual(0f, Node("ener").resources.powerDraw, 0.001f, "에너지는 안 먹는다");
            Assert.AreEqual(1f, Node("ener").resources.heatGenerate, 0.001f, "발전이 열을 낸다");

            foreach (NodeDefinition n in _nodes)
                Assert.AreNotEqual(66f, n.resources.powerDraw,
                    $"{n.displayName}: 66은 **네트워크 합계**다 — 노드 한 대에 얹으면 안 된다");
        }

        /// <summary>
        /// 원점 구성(코어1 · 가공2 · 군수1 · 에너지1)의 고정비 합 = **4/초**.
        /// 66은 어디서도 나오지 않는 독립 placeholder였다.
        /// </summary>
        [Test]
        public void OriginLayout_DrawsFour()
        {
            float draw = Node("core").resources.powerDraw
                         + 2f * Node("proc").resources.powerDraw
                         + Node("muni").resources.powerDraw
                         + Node("ener").resources.powerDraw;

            Assert.AreEqual(4f, draw, 0.001f);
        }

        /// <summary>
        /// **냉각은 노드의 값이 아니다.** 구 냉각 노드가 2026-07-02에 모듈 F로 전환됐으므로,
        /// 노드에 냉각량을 두면 폐기된 노드가 이름만 바꿔 되살아난다(260829_V03).
        /// 필드 자체가 사라졌으니 발열원만 남는다 — 지금 발열을 내는 것은 에너지 하나뿐이다.
        /// </summary>
        [Test]
        public void Cooling_IsNotOwnedByNodes()
        {
            float totalHeat = 0f;
            foreach (NodeDefinition n in _nodes) totalHeat += n.resources.heatGenerate;

            Assert.AreEqual(1f, totalHeat, 0.001f, "발열원은 에너지 1/초 하나");
        }

        private NodeDefinition Node(string id) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodesDir}/Node_{id}.asset");

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
