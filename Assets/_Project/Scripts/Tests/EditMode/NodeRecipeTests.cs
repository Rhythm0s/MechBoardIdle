using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 레시피 선택형 노드(260827_V01 §3).
    /// 레퍼런스는 새티스팩토리 제작기 — 기계가 조합표를 여러 개 갖되 **한 번에 하나만 돌린다.**
    ///
    /// 구조 요구 넷을 그대로 계약으로 박는다:
    ///   ① 노드는 단일 출력  ② 후보는 노드 종류가 가짐  ③ 언제든 교체 가능  ④ 버퍼 상한 = 정지 조건
    /// </summary>
    public sealed class NodeRecipeTests
    {
        private const float D = 0.0001f;
        private const string MuniPath = "Assets/_Project/ScriptableObjects/Nodes/Node_muni.asset";

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        // 산출 종류는 조합표마다 다르다 — 드론 몸체가 탄약을 뱉으면 잔여물 판정이 성립하지 않는다.
        private static FlowKind OutputOf(RecipeKind kind) =>
            kind == RecipeKind.DroneBody ? FlowKind.Drone : FlowKind.Ammo;

        private static NodeRecipe Recipe(RecipeKind kind, float rate, float stack, bool impl = true) =>
            new NodeRecipe
            {
                kind = kind, displayName = kind.ToString(), output = OutputOf(kind),
                outputPerSec = rate, stackLimitTbd = stack, implemented = impl,
            };

        private NodeDefinition Node(params NodeRecipe[] recipes)
        {
            var n = ScriptableObject.CreateInstance<NodeDefinition>();
            n.type = NodeType.MunitionsBasic;
            n.implemented = true;
            n.ports = new List<NodePort>();
            n.recipes = new List<NodeRecipe>(recipes);
            _created.Add(n);
            return n;
        }

        // ---- ① 노드는 단일 출력 ----

        /// <summary>
        /// 노드 한 대가 돌리는 것은 **하나**다. 출력 갈래 배열도, 갈래별 배분 비율도 없다 —
        /// 갈래를 늘리는 방법은 노드를 더 놓는 것이지 노드 하나를 넓히는 것이 아니다.
        /// </summary>
        [Test]
        public void Node_RunsExactlyOneRecipe()
        {
            var inst = new NodeInstance(Node(Recipe(RecipeKind.Ammo, 1f, 0f),
                                             Recipe(RecipeKind.DroneBody, 1f, 0f)), Vector2Int.zero);

            inst.SelectRecipe(RecipeKind.DroneBody);

            Assert.AreEqual(RecipeKind.DroneBody, inst.CurrentRecipe.kind, "지금 돌리는 것은 하나뿐이다");
        }

        // ---- ② 후보는 노드 종류가 가짐 ----

        /// <summary>
        /// 군수 노드의 조합표 후보가 **자산 데이터**에 있어야 한다 —
        /// 레시피 추가가 데이터 한 행이고 노드 코드는 건드리지 않는다는 요구가 이것이다.
        /// </summary>
        [Test]
        public void MunitionsAsset_CarriesFourRecipeCandidates()
        {
            var muni = AssetDatabase.LoadAssetAtPath<NodeDefinition>(MuniPath);
            Assert.NotNull(muni, "군수 자산");

            var kinds = new HashSet<RecipeKind>();
            foreach (NodeRecipe r in muni.recipes) kinds.Add(r.kind);

            // 2026-09-05 개명(`260904_W01` 3-2): 탄약 → **표준탄** · 쉴드 재료 → **방어 재료** ·
            // 드론 몸체 → 드론 몸체 부품. 넷이라는 수와 「자산이 후보를 갖는다」는 요점은 그대로다.
            Assert.IsTrue(kinds.Contains(RecipeKind.StandardAmmo), "표준탄");
            Assert.IsTrue(kinds.Contains(RecipeKind.DroneBody), "드론 몸체 부품");
            Assert.IsTrue(kinds.Contains(RecipeKind.DefenseMaterial), "방어 재료");
            Assert.IsTrue(kinds.Contains(RecipeKind.Propellant), "추진제(2026-08-29 가동)");
        }

        /// <summary>
        /// 기초 군수의 조합표는 **넷 다 돈다**(2026-09-05).
        ///
        /// 종전에는 쉴드 재료 하나가 「자리만 있고 안 도는」 범위 밖이었는데, W01 3-2 개정에서
        /// 그 자리를 **방어 재료**가 이어받으며 가동으로 넘어갔다. 쉴드 발생 노드 자체는
        /// 여전히 MVP 밖이지만, 그 노드가 없다는 것과 재료가 안 나온다는 것은 다른 이야기다.
        ///
        /// ⚠️ 그래서 이 테스트는 지금 **범위 밖이 하나도 없음**을 고정한다. 나중에 안 도는
        /// 조합표를 다시 넣으면 여기가 먼저 깨져서 알려 준다.
        /// </summary>
        [Test]
        public void EveryMunitionsRecipe_IsRunnable()
        {
            var muni = AssetDatabase.LoadAssetAtPath<NodeDefinition>(MuniPath);

            foreach (NodeRecipe r in muni.recipes)
                Assert.IsTrue(r.IsRunnable, $"{r.kind} 가동 여부");
        }

        // ---- ③ 언제든 교체 ----

        [Test]
        public void SelectRecipe_SwitchesAnytime_ButRejectsUnavailable()
        {
            var inst = new NodeInstance(Node(Recipe(RecipeKind.Ammo, 1f, 0f),
                                             Recipe(RecipeKind.Propellant, 0f, 0f, impl: false)), Vector2Int.zero);

            Assert.IsTrue(inst.SelectRecipe(RecipeKind.Ammo));
            Assert.IsFalse(inst.SelectRecipe(RecipeKind.Propellant), "돌릴 수 없는 조합표는 거절");
            Assert.IsFalse(inst.SelectRecipe(RecipeKind.ShieldMaterial), "후보에 없는 것도 거절");
            Assert.AreEqual(RecipeKind.Ammo, inst.CurrentRecipe.kind, "거절돼도 이전 선택이 유지된다");
        }

        /// <summary>
        /// **교체가 플레이어의 물건을 없애지 않는다.** 출력 버퍼에 남은 이전 산출물은 그대로다 —
        /// 지우면 몰수이고, 벨트로 밀어내면 창고에 원치 않는 품목이 섞인다.
        /// </summary>
        [Test]
        public void SwitchingRecipe_KeepsOutputBuffer()
        {
            var inst = new NodeInstance(Node(Recipe(RecipeKind.Ammo, 1f, 10f),
                                             Recipe(RecipeKind.DroneBody, 1f, 10f)), Vector2Int.zero)
            { OutputBuffer = 7f };

            inst.SelectRecipe(RecipeKind.DroneBody);

            Assert.AreEqual(7f, inst.OutputBuffer, D, "남은 산출물은 그대로 둔다");
        }

        /// <summary>
        /// 안 고른 노드도 **이름 붙은 기본값**으로 돈다 — 놓자마자 아무것도 안 하는 상태를 피한다.
        ///
        /// ⚠️ **「첫 후보」가 아니다.** 이 테스트는 2026-09-05까지 이름이
        /// `UnselectedNode_FallsBackToFirstRunnableRecipe`였는데, 규칙은 이미 이름 붙은
        /// 기본값으로 바뀐 뒤였다(`260903_W03` 3장 · <see cref="NodeInstance.CurrentRecipe"/>).
        /// 목록에 항목을 하나 더했을 때 기본값이 조용히 바뀌는 것을 막는 것이 그 개정의 이유다.
        /// 여기서 기본값을 **목록 둘째**에 두어, 순서로 고르면 틀리게 만들어 둔다.
        /// </summary>
        [Test]
        public void UnselectedNode_UsesTheNamedDefault_NotListOrder()
        {
            var inst = new NodeInstance(Node(Recipe(RecipeKind.DroneBody, 1f, 0f),
                                             Recipe(RecipeKind.StandardAmmo, 1f, 0f)), Vector2Int.zero);

            Assert.AreEqual(RecipeKind.StandardAmmo, inst.CurrentRecipe.kind,
                "기초 군수의 기본값은 표준탄이다 — 목록 순서와 무관하다");
        }

        // ---- ④ 버퍼 상한 = 생산 정지 조건 ----

        [Test]
        public void Production_StopsWhenBufferIsFull()
        {
            NodeRecipe r = Recipe(RecipeKind.Ammo, rate: 2f, stack: 10f);

            Assert.AreEqual(2f, NodeProduction.Produce(r, bufferNow: 0f, dt: 1f), D);
            Assert.AreEqual(0f, NodeProduction.Produce(r, bufferNow: 10f, dt: 1f), D, "가득 차면 멈춘다");
            Assert.IsTrue(NodeProduction.IsStalled(r, 10f));
        }

        /// <summary>
        /// 넘치는 분을 **버리지 않는다.** 버리면 「막히면 멈춘다」가 아니라
        /// 「막혀도 돌면서 버린다」가 되어 병목이 눈에 안 보인다.
        /// </summary>
        [Test]
        public void Production_ClampsToRemainingRoom_NeverOverflows()
        {
            NodeRecipe r = Recipe(RecipeKind.Ammo, rate: 100f, stack: 10f);

            Assert.AreEqual(3f, NodeProduction.Produce(r, bufferNow: 7f, dt: 1f), D, "남은 자리 3개만");
        }

        /// <summary>스택 상한은 미확정이다 — 0 = 미설정 센티넬이고 하드코딩한 상한을 끼우지 않는다.</summary>
        [Test]
        public void UnsetStackLimit_MeansUnlimited_NotZero()
        {
            NodeRecipe r = Recipe(RecipeKind.Ammo, rate: 5f, stack: 0f);

            Assert.AreEqual(float.PositiveInfinity, NodeProduction.FreeSpace(r, 999f));
            Assert.AreEqual(5f, NodeProduction.Produce(r, bufferNow: 999f, dt: 1f), D, "상한 미설정이면 계속 돈다");
            Assert.IsFalse(NodeProduction.IsStalled(r, 999f));
        }

        [Test]
        public void NonRunnableRecipe_ProducesNothing()
        {
            NodeRecipe r = Recipe(RecipeKind.Propellant, rate: 5f, stack: 10f, impl: false);

            Assert.AreEqual(0f, NodeProduction.Produce(r, 0f, 1f), D);
            Assert.IsFalse(NodeProduction.IsStalled(r, 0f), "안 도는 노드는 막힌 것이 아니다");
        }

        [Test]
        public void Withdraw_TakesWhatIsThere()
        {
            Assert.AreEqual(4f, NodeProduction.Withdraw(10f, 4f, out float after), D);
            Assert.AreEqual(6f, after, D);

            Assert.AreEqual(2f, NodeProduction.Withdraw(2f, 99f, out float after2), D, "있는 만큼만");
            Assert.AreEqual(0f, after2, D);

            Assert.AreEqual(0f, NodeProduction.Withdraw(0f, 5f, out _), D);
        }

        // ---- 정지 사유 구분(§2-1) ----

        /// <summary>
        /// 같은 「정지」인데 의미가 다르다. 출력 막힘은 **물류 실패의 신호**이고,
        /// 교체 잔여물은 **방금 자기가 한 조작의 정상적 결과**다.
        /// 둘을 똑같이 보여주면 레시피를 바꿀 때마다 공장이 고장 난 줄 안다 —
        /// 그리고 레시피 교체는 상시 조작이다.
        /// </summary>
        [Test]
        public void StallReason_DistinguishesBlockedFromResidue()
        {
            NodeRecipe ammo = Recipe(RecipeKind.Ammo, rate: 1f, stack: 10f);

            Assert.AreEqual(NodeStallReason.OutputBlocked,
                NodeProduction.StallReason(ammo, bufferNow: 10f, bufferKind: FlowKind.Ammo),
                "지금 조합표의 산출물이 가득 참 = 가져가는 쪽이 없다");

            Assert.AreEqual(NodeStallReason.RecipeChangedResidue,
                NodeProduction.StallReason(ammo, bufferNow: 3f, bufferKind: FlowKind.Drone),
                "이전 조합표의 산출물이 남아 있음 = 방금 바꿨다");
        }

        /// <summary>잔여물은 **가득 차지 않아도** 정지 사유다 — 다른 품목을 섞을 수 없다.</summary>
        [Test]
        public void Residue_StallsEvenWhenBufferHasRoom()
        {
            NodeRecipe ammo = Recipe(RecipeKind.Ammo, rate: 1f, stack: 100f);

            Assert.AreEqual(NodeStallReason.RecipeChangedResidue,
                NodeProduction.StallReason(ammo, bufferNow: 1f, bufferKind: FlowKind.Drone));
        }

        [Test]
        public void StallReason_IsNoneWhenRunningNormally()
        {
            NodeRecipe ammo = Recipe(RecipeKind.Ammo, rate: 1f, stack: 10f);

            Assert.AreEqual(NodeStallReason.None,
                NodeProduction.StallReason(ammo, bufferNow: 0f, bufferKind: FlowKind.Ammo), "빈 버퍼");
            Assert.AreEqual(NodeStallReason.None,
                NodeProduction.StallReason(ammo, bufferNow: 5f, bufferKind: FlowKind.Ammo), "여유 있음");
        }

        /// <summary>노드 인스턴스가 사유를 들고 있어야 UI가 구분해 그릴 수 있다.</summary>
        [Test]
        public void NodeInstance_ExposesStallReason()
        {
            var inst = new NodeInstance(Node(Recipe(RecipeKind.Ammo, 1f, 10f),
                                             Recipe(RecipeKind.DroneBody, 1f, 10f)), Vector2Int.zero)
            { OutputBuffer = 4f, BufferKind = FlowKind.Ammo };

            inst.SelectRecipe(RecipeKind.Ammo);
            Assert.AreEqual(NodeStallReason.None, inst.StallReason);

            inst.SelectRecipe(RecipeKind.DroneBody); // 버퍼엔 아직 탄약이 남아 있다
            Assert.AreEqual(NodeStallReason.RecipeChangedResidue, inst.StallReason);
        }

        // ---- §3-3 재고 층위 ----

        /// <summary>
        /// **노드 출력 버퍼는 만충 판정에 세지 않는다.** 태그가 보는 창고는 저장 노드다.
        /// 노드 버퍼까지 세면 보드에 노드를 늘리는 것만으로 만충이 앞당겨져 태그 주기가 무너진다.
        /// </summary>
        [Test]
        public void NodeBuffer_IsNotCountedInWarehouseFullness()
        {
            var warehouse = new AmmoInventory(40f);
            warehouse.Add(AmmoKind.Pierce, 10f);

            var node = new NodeInstance(Node(Recipe(RecipeKind.Ammo, 1f, 100f)), Vector2Int.zero)
            { OutputBuffer = 90f }; // 노드에 잔뜩 쌓여 있어도

            Assert.AreEqual(10f, warehouse.Total, D, "창고 재고는 10 그대로다");
            Assert.IsFalse(warehouse.IsFull, "노드 버퍼가 만충을 앞당기지 않는다");
            Assert.AreEqual(90f, node.OutputBuffer, D);
        }
    }
}
