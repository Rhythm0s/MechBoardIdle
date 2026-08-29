using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 집계가 조합표를 읽는다(260829_V02 §③ 착수 승인).
    ///
    /// 집계는 2026-08-27 레시피 선택형 이전 모델이라 군수 노드를 **탄종으로만** 세고 있었다.
    /// 그래서 추진제·드론 몸체를 돌리는 노드가 탄약을 만드는 것으로 집계됐다.
    ///
    /// ⚠️ 착수 조건은 **탄약 경로 회귀 확인 2건**이다(80 / 145). 그 경로에는
    /// 「라인 가동률과 군수 노드 생산 단위」가 걸려 있어 모르고 손대면 실측값이 깨진다.
    /// 두 값을 이 파일 맨 앞에 둔 이유가 그것이다.
    /// </summary>
    public sealed class RecipeAggregateTests
    {
        private const float D = 0.001f;
        private const string MuniPath = "Assets/_Project/ScriptableObjects/Nodes/Node_muni.asset";
        private const string BoostPath = "Assets/_Project/ScriptableObjects/Nodes/Node_boost.asset";

        private NodeDefinition _muni;
        private NodeDefinition _boost;

        [SetUp]
        public void SetUp()
        {
            _muni = AssetDatabase.LoadAssetAtPath<NodeDefinition>(MuniPath);
            _boost = AssetDatabase.LoadAssetAtPath<NodeDefinition>(BoostPath);
            if (_muni == null || _boost == null)
                Assert.Ignore("노드 자산 없음 — 먼저 메뉴 'MBI/Generate Balance + Nodes' 실행.");
        }

        private static BoardGrid Grid() => new BoardGrid(8, 8, 1f, Vector2.zero);

        /// <summary>군수 노드 한 대를 놓고 탄종·조합표를 지정한다.</summary>
        private NodeInstance PlaceMuni(BoardGrid g, int x, AmmoKind kind, RecipeKind recipe = RecipeKind.Ammo)
        {
            g.TryPlace(new Vector2Int(x, 0), _muni, out NodeInstance inst);
            inst.AmmoKind = kind;
            inst.SelectRecipe(recipe);
            return inst;
        }

        /// <summary>
        /// 집계 → 발사 라인 → 명목 출력. Provider가 하는 것과 같은 경로다
        /// (스펙은 BalanceConfig, 노드당 생산은 muniPerNode).
        /// </summary>
        private static float Output(NetworkAggregate agg)
        {
            var bal = AssetDatabase.LoadAssetAtPath<BalanceConfig>(
                "Assets/_Project/ScriptableObjects/BalanceConfig.asset");
            Assert.NotNull(bal, "BalanceConfig");

            // 로봇 A의 세 탄종. 발당피해는 대표 상태(20 / 25 / 50).
            var lines = new List<MunitionsLine>
            {
                new MunitionsLine(AmmoKind.Pierce, bal.LineSpecOf(AmmoKind.Pierce), 20f, agg.muniPierce),
                new MunitionsLine(AmmoKind.Split, bal.LineSpecOf(AmmoKind.Split), 25f, agg.muniSplit),
                new MunitionsLine(AmmoKind.Explosive, bal.LineSpecOf(AmmoKind.Explosive), 50f, agg.muniExplosive),
            };
            return AmmoLineProduction.TotalOutput(lines, bal.muniPerNode);
        }

        // ---- 회귀 확인 2건 (260829_V02 착수 조건) ----

        /// <summary>
        /// 군수 4노드를 관통에 몰면 **80**. 관통 스펙이 5라 4노드는 4발/초에서 멈추고 4 × 20 = 80이다.
        /// 한 탄종에 몰아넣는 것이 언제나 최적이 되지 않는 이유가 이 상한이다.
        /// </summary>
        [Test]
        public void Regression_FourPierceNodes_Output80()
        {
            var g = Grid();
            for (int i = 0; i < 4; i++) PlaceMuni(g, i, AmmoKind.Pierce);

            NetworkAggregate agg = LogisticsNetwork.Aggregate(g);

            Assert.AreEqual(4, agg.muniPierce);
            Assert.AreEqual(80f, Output(agg), D, "관통 4노드 = 80");
        }

        /// <summary>
        /// 관통1 · 분열1 · 폭발2 = **145**(대표 배치). 20 + 25 + 100.
        /// 2026-08-25 실측이자 s3Break이다 — 이 값이 흔들리면 밸런스 전체가 흔들린다.
        /// </summary>
        [Test]
        public void Regression_RepresentativeMix_Output145()
        {
            var g = Grid();
            PlaceMuni(g, 0, AmmoKind.Pierce);
            PlaceMuni(g, 1, AmmoKind.Split);
            PlaceMuni(g, 2, AmmoKind.Explosive);
            PlaceMuni(g, 3, AmmoKind.Explosive);

            NetworkAggregate agg = LogisticsNetwork.Aggregate(g);

            Assert.AreEqual(1, agg.muniPierce);
            Assert.AreEqual(1, agg.muniSplit);
            Assert.AreEqual(2, agg.muniExplosive);
            Assert.AreEqual(145f, Output(agg), D, "대표 배치 = 145 = s3Break");
        }

        /// <summary>
        /// 조합표를 안 고른 노드도 탄약을 만든다 — 놓자마자 아무것도 안 하는 상태를 피한다.
        /// 이것이 없으면 위 두 회귀값이 0이 된다(기존 보드는 아무도 조합표를 고르지 않았다).
        /// </summary>
        [Test]
        public void UnselectedRecipe_StillCountsAsAmmo()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(0, 0), _muni, out NodeInstance inst);
            inst.AmmoKind = AmmoKind.Pierce;

            Assert.AreEqual(RecipeKind.None, inst.SelectedRecipe, "고른 적이 없다");

            NetworkAggregate agg = LogisticsNetwork.Aggregate(g);

            Assert.AreEqual(1, agg.muniPierce, "그래도 탄약으로 센다");
        }

        // ---- 조합표를 바꾸면 산출이 갈린다 ----

        /// <summary>
        /// **노드 하나는 조합표 하나를 돌린다.** 추진제로 돌린 노드는 탄약을 만들지 않는다 —
        /// 두 곳에 다 세면 조합표를 바꾸는 것이 순이득이 되어 선택이 선택이 아니게 된다.
        /// </summary>
        [Test]
        public void PropellantNode_MakesNoAmmo()
        {
            var g = Grid();
            PlaceMuni(g, 0, AmmoKind.Pierce, RecipeKind.Propellant);

            NetworkAggregate agg = LogisticsNetwork.Aggregate(g);

            Assert.AreEqual(0, agg.muniPierce, "탄종 수에 안 들어간다");
            Assert.AreEqual(0f, agg.ammoProduce, D, "탄약 생산에도 안 들어간다");
            Assert.AreEqual(1f / 15f, agg.propellantProduce, D, "추진제로 나간다");
        }

        [Test]
        public void DroneNode_MakesNoAmmo_AndFeedsTheBay()
        {
            var g = Grid();
            PlaceMuni(g, 0, AmmoKind.Pierce, RecipeKind.DroneBody);

            NetworkAggregate agg = LogisticsNetwork.Aggregate(g);

            Assert.AreEqual(0, agg.muniPierce);
            Assert.AreEqual(0f, agg.ammoProduce, D);
            Assert.AreEqual(1f, agg.droneProduce, D, "params pB = 1.0 기/초");
        }

        /// <summary>
        /// 조합표를 바꾸면 그 노드 몫이 탄약에서 빠진다 — **보드가 바뀌면 출력이 바뀐다**가
        /// 조합표 축에서도 성립해야 한다. 대표 배치에서 폭발 하나를 추진제로 돌리면 145 → 95.
        /// </summary>
        [Test]
        public void SwitchingRecipe_MovesOutputOffTheAmmoLine()
        {
            var g = Grid();
            PlaceMuni(g, 0, AmmoKind.Pierce);
            PlaceMuni(g, 1, AmmoKind.Split);
            PlaceMuni(g, 2, AmmoKind.Explosive);
            NodeInstance fourth = PlaceMuni(g, 3, AmmoKind.Explosive);

            Assert.AreEqual(145f, Output(LogisticsNetwork.Aggregate(g)), D);

            fourth.SelectRecipe(RecipeKind.Propellant);
            NetworkAggregate after = LogisticsNetwork.Aggregate(g);

            Assert.AreEqual(95f, Output(after), D, "폭발 한 대가 빠져 20 + 25 + 50");
            Assert.AreEqual(1f / 15f, after.propellantProduce, D, "그 대신 추진제가 나온다");
        }

        /// <summary>돌릴 수 없는 조합표는 거절되므로 산출이 안 바뀐다 — 착수 금지가 데이터로 지켜진다.</summary>
        [Test]
        public void OutOfScopeRecipe_IsRejected_AndOutputHolds()
        {
            var g = Grid();
            NodeInstance inst = PlaceMuni(g, 0, AmmoKind.Pierce);

            Assert.IsFalse(inst.SelectRecipe(RecipeKind.ShieldMaterial), "쉴드 재료는 못 돌린다");
            Assert.AreEqual(1, LogisticsNetwork.Aggregate(g).muniPierce, "탄약 그대로");
        }

        // ---- 부스터 ----

        /// <summary>
        /// 부스터 대수가 집계된다. **회피 스택 상한 = 대수 × 2**라 이 값이 없으면
        /// 보드를 아무리 고쳐도 생존이 안 바뀐다.
        /// </summary>
        [Test]
        public void BoosterNodes_AreCounted()
        {
            var g = Grid();
            g.TryPlace(new Vector2Int(0, 1), _boost, out _);
            g.TryPlace(new Vector2Int(1, 1), _boost, out _);

            NetworkAggregate agg = LogisticsNetwork.Aggregate(g);

            Assert.AreEqual(2, agg.boosterCount);
            Assert.AreEqual(0f, agg.ammoProduce, D, "부스터는 탄약을 만들지 않는다");
        }

        /// <summary>
        /// 보드 → 회피 상한의 전 구간. 부스터 3대 = 6칸이고, 그 여섯 칸을 채우는 것은
        /// 추진제를 만드는 군수 노드다 — 그릇과 채우는 속도가 분리돼 있어 무한히 세지지 않는다.
        /// </summary>
        [Test]
        public void BoardToDodgeCapacity_GrowsWithBoosters_ButFillRateDoesNot()
        {
            var g = Grid();
            for (int i = 0; i < 3; i++) g.TryPlace(new Vector2Int(i, 1), _boost, out _);
            PlaceMuni(g, 0, AmmoKind.Pierce, RecipeKind.Propellant);

            NetworkAggregate agg = LogisticsNetwork.Aggregate(g);
            var dodge = new DodgeSystem { BoosterCount = agg.boosterCount };

            Assert.AreEqual(6, dodge.Capacity, "3대 × 2칸");

            // 여섯 칸을 채우려면 15초짜리 추진제가 여섯 개 = 90초다. 그릇만 키우면 빈 그릇이 는다.
            float secondsToFill = dodge.Capacity / agg.propellantProduce;
            Assert.AreEqual(90f, secondsToFill, 0.01f);
        }
    }
}
