using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **합체가 공장에 거는 것** (2026-09-17 · `260917_W03` 7-2 · 사용자 결정 5·6).
    ///
    /// 확정: 합체 지속 중 **두 보드 모든 노드의 산출량 ×2**.
    /// 늘어나는 것은 **나오는 개수뿐**이고 재료 · 전력 · 생산 주기는 그대로이며
    /// **벨트는 배율을 안 받는다**(한 줄 12/초).
    ///
    /// ⚠️⚠️ **가장 깨지기 쉬운 것은 「끝나면 돌아온다」이다.** 켜는 코드는 눈에 띄지만
    /// 끄는 코드는 안 띈다 — 한 번 안 끄면 그 판의 공장이 **영영 두 배**로 돈다.
    /// </summary>
    public sealed class MergeFactoryTests
    {
        private const float D = 0.0001f;
        private const string SoRoot = "Assets/_Project/ScriptableObjects";

        private static NodeDefinition Node(string id)
            => AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{SoRoot}/Nodes/Node_{id}.asset");

        private static BalanceConfig Balance()
            => AssetDatabase.LoadAssetAtPath<BalanceConfig>($"{SoRoot}/BalanceConfig.asset");

        [SetUp]
        public void 판마다_되돌린다() => MergeSignals.Reset();

        [TearDown]
        public void 다음_시험에_안_흘린다() => MergeSignals.Reset();

        // ── ② 표준탄 산출이 ×2 이고 부품 소비는 그대로인가 ──────────────────

        [Test]
        public void 합체_중_기초_군수_산출이_두_배이고_재료_소비는_그대로다()
        {
            NodeDefinition muni = Node(StartingBoard.MuniId);
            Assert.IsNotNull(muni, "기초 가공소 자산이 없다");

            NodeRecipe recipe = default;
            foreach (NodeRecipe r in muni.recipes)
                if (r.kind == RecipeKind.Ammo || r.output == FlowKind.StandardAmmo) recipe = r;
            Assert.IsTrue(recipe.IsRunnable, "표준탄 조합표를 못 찾았다");

            // 재료를 넉넉히 둔다 — 재료 한도가 아니라 **배율**을 보는 시험이다.
            var stock = new Dictionary<FlowKind, float>();
            foreach (RecipeInput i in recipe.inputs) stock[i.kind] = 100f;

            float plain = NodeProduction.Produce(recipe, 0f, 1f, stock, 1f);
            float merged = NodeProduction.Produce(recipe, 0f, 1f, stock, 2f);

            Assert.AreEqual(plain * 2f, merged, D, "합체 중 산출이 두 배가 아니다");

            // 재료 — **같은 한 벌로 두 개가 나온다.** 배율만큼 나눠 먹는 것이 그 뜻이다.
            var a = new Dictionary<FlowKind, float>(stock);
            var b = new Dictionary<FlowKind, float>(stock);
            NodeProduction.ConsumeFor(recipe, plain, a);
            NodeProduction.ConsumeFor(recipe, merged / 2f, b);

            foreach (FlowKind k in new List<FlowKind>(stock.Keys))
                Assert.AreEqual(a[k], b[k], D,
                    $"{k} 소비가 달라졌다 — 합체는 재료를 더 먹지 않는다");
        }

        [Test]
        public void 배율은_전력을_안_올린다()
        {
            // ⚠️ 「재료 · 전력 · 생산 주기는 그대로」의 전력 쪽이다.
            BoardGrid g = Board();
            ICollection<Vector2Int> connected = LogisticsReach.ConnectedNodes(g);
            WorkloadRate.Result work = WorkloadRate.Compute(g, connected, null);

            NetworkAggregate plain = LogisticsNetwork.Aggregate(g, connected, work, 1f);
            NetworkAggregate merged = LogisticsNetwork.Aggregate(g, connected, work, 2f);

            Assert.AreEqual(plain.powerDraw, merged.powerDraw, D, "합체가 전기를 더 먹는다");
            Assert.AreEqual(plain.powerSupply, merged.powerSupply, D);
            Assert.AreEqual(plain.ammoProduce * 2f, merged.ammoProduce, D,
                "집계 산출이 두 배가 아니다");
        }

        // ── ③ 벨트는 배율을 안 받는다 ────────────────────────────────────────

        [Test]
        public void 벨트_처리량은_배율을_안_받는다()
        {
            // ⚠️⚠️ **여기가 갈리는 자리다.** 산출이 두 배가 되어도 **나르는 양은 그대로**여서
            //    남는 것은 그 자리에 쌓인다 — 그것이 결정 7 「상류 정체를 보여도 된다」이다.
            //    벨트까지 두 배가 되면 합체가 물류 설계를 무의미하게 만든다.
            int plain = MountArrivalsIn(60f, 1f);
            int merged = MountArrivalsIn(60f, 2f);

            Assert.Greater(plain, 0, "시험 전제가 깨졌다 — 배율 없이도 안 닿는다");
            Assert.LessOrEqual(merged, Mathf.RoundToInt(60f * BeltCap),
                $"한 줄 처리량 {BeltCap}/초를 넘겼다 — 벨트가 배율을 받고 있다");
        }

        /// <summary>벨트 한 줄 처리량 — 조립 시스템 문서 9장 확정치 12/초.</summary>
        private const float BeltCap = 12f;

        // ── ④ 끝나면 즉시 1.0 ────────────────────────────────────────────────

        [Test]
        public void 기본값은_1이고_되돌리면_1이다()
        {
            Assert.AreEqual(1f, MergeSignals.None, D, "기본값이 1 이 아니다");

            MergeSignals.OutputMultiplier = 2f;
            MergeSignals.Reset();
            Assert.AreEqual(MergeSignals.None, MergeSignals.OutputMultiplier, D,
                "되돌렸는데 배율이 남아 있다 — 다음 판의 공장이 두 배로 돈다");
        }

        [Test]
        public void 배율_값은_밸런스_자산에서_온다()
        {
            // ⚠️ **하드코딩 금지**(`260917_W03` 7-2 #2). 원천은 `balance_v4.json` 이고
            //    `BalanceConfig` 가 그것을 미러한다. 코드에 2 를 적어 두면 원천이 둘이 된다.
            BalanceConfig bal = Balance();
            Assert.IsNotNull(bal, "BalanceConfig 자산이 없다 — 생성기를 먼저 돌린다");
            Assert.AreEqual(2f, bal.mergeOutputMult, D,
                "params mergeOutputMult = 2 (밸런스 「합체 예외」)");
        }

        // ── ⑤ 합체 중 보드 편집을 막지 않는 것이 **의도**다 ──────────────────

        [Test]
        public void 합체_중에도_보드를_고칠_수_있다()
        {
            // ⚠️⚠️ **이 시험은 「아무것도 안 하는 것」을 지킨다**(사용자 결정 2 · W03 7-2).
            //    나중에 누가 「합체 중엔 잠그자」로 막으면 여기서 걸린다.
            //    막지 않는 것이 **의도**이지 빠뜨린 것이 아니다.
            MergeSignals.OutputMultiplier = 2f;

            BoardGrid g = Board();
            var cell = new Vector2Int(3, 4);   // 몸통 안 빈 칸
            Assert.IsNull(g.GetAt(cell), "시험 전제가 깨졌다 — 그 칸이 이미 찼다");

            Assert.IsTrue(g.TryPlace(cell, Node(StartingBoard.EnergyId), out _),
                "합체 중이라고 배치가 막혔다 — 막지 않는 것이 사용자 결정이다");
            Assert.IsTrue(g.TryRemove(cell), "합체 중이라고 제거가 막혔다");
        }

        // ── 판 세우기 ────────────────────────────────────────────────────────

        private static BoardGrid Board()
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero,
                PartLayout.BuildMask());
            StartingBoard.Apply(g, Node);
            StartingBoard.Place(g, StartingBoard.FillsEmptySlot);
            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        private static int MountArrivalsIn(float seconds, float multiplier)
        {
            BoardGrid g = Board();
            var flow = new BeltItemFlow();
            flow.Rebuild(g);

            int arrived = 0;
            int steps = Mathf.RoundToInt(seconds / 0.05f);
            for (int i = 0; i < steps; i++)
            {
                BoardItemTick.Step(g, flow, 0.05f, 1f, multiplier);
                arrived += flow.PendingMountArrivals.Count;
                flow.ClearPendingMountArrivals();
            }
            return arrived;
        }
    }
}
