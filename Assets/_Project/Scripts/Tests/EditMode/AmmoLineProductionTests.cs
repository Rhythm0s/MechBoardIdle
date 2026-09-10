using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 탄종별 생산(260824_V02 §1). 확정치: 노드 1개 = 1발/초(muniPerNode),
    /// 스펙 5 / 4 / 2(specA0~2), 발당피해 20 / 25 / 50(dA0~2) — 전부 balance_v4 confirmed:true.
    ///
    /// 이 파일이 지키는 것은 **보드가 출력을 바꾼다**는 코어 명제다.
    /// capA(소비 상한 6)를 생산 자리에 넣던 시기에는 군수 노드 1개가 상한을 다 채워
    /// 두 번째 노드부터 출력 영향이 0이었다(CLAUDE.md §7).
    /// </summary>
    public sealed class AmmoLineProductionTests
    {
        private const float D = 0.001f;
        private const float PerNode = 1f; // muniPerNode 확정치

        // `260909_W01` 2-1 확정 좌표 — 관통 20×5 · **표준 10×6** · 폭발 50×2.
        // ⚠️ 구 분열탄 25×4는 폐기됐다. 값이 바뀐 근거는 2-2에 있다.
        private const float SpecPierce = 5f, SpecStandard = 6f, SpecExplosive = 2f;
        private const float DmgPierce = 20f, DmgStandard = 10f, DmgExplosive = 50f;

        // ---- 라인 가동률 = min(1, 보유 노드 ÷ 필요 노드) ----

        [Test]
        public void LineOutput_ScalesWithNodeCount_UpToSpec()
        {
            Assert.AreEqual(0f, AmmoLineProduction.LineOutput(SpecPierce, 0, PerNode), D, "노드가 없으면 생산 0");
            Assert.AreEqual(1f, AmmoLineProduction.LineOutput(SpecPierce, 1, PerNode), D);
            Assert.AreEqual(4f, AmmoLineProduction.LineOutput(SpecPierce, 4, PerNode), D);
            Assert.AreEqual(5f, AmmoLineProduction.LineOutput(SpecPierce, 5, PerNode), D, "5노드 = 100% 가동");
        }

        /// <summary>
        /// **노드를 더 박아도 스펙을 넘지 못한다.** 상한이 없으면 한 탄종에 몰아넣는 것이
        /// 언제나 최적이 되어 조합 축이 죽는다.
        /// </summary>
        [Test]
        public void LineOutput_CapsAtSpec_MoreNodesDoNothing()
        {
            Assert.AreEqual(5f, AmmoLineProduction.LineOutput(SpecPierce, 5, PerNode), D);
            Assert.AreEqual(5f, AmmoLineProduction.LineOutput(SpecPierce, 50, PerNode), D, "10배를 박아도 5발/초");
            Assert.AreEqual(2f, AmmoLineProduction.LineOutput(SpecExplosive, 9, PerNode), D, "폭발은 2에서 멈춘다");
        }

        /// <summary>
        /// 회귀 방지 — capA(6)를 생산 자리에 넣으면 노드 1개로 상한이 차서
        /// 노드 수가 출력을 바꾸지 못한다. 그 상태를 다시 만들지 않는다는 고정.
        /// </summary>
        [Test]
        public void SecondNode_ActuallyIncreasesOutput_NotConstant()
        {
            float one = AmmoLineProduction.LineOutput(SpecPierce, 1, PerNode);
            float two = AmmoLineProduction.LineOutput(SpecPierce, 2, PerNode);

            Assert.Greater(two, one, "두 번째 노드가 출력을 올려야 한다 — 보드 크기가 의미를 갖는 조건");
            Assert.AreEqual(2f, two, D);
        }

        [Test]
        public void Utilization_IsRatioClampedToOne()
        {
            Assert.AreEqual(0.4f, AmmoLineProduction.Utilization(SpecPierce, 2, PerNode), D, "2 ÷ 5");
            Assert.AreEqual(1f, AmmoLineProduction.Utilization(SpecPierce, 5, PerNode), D);
            Assert.AreEqual(1f, AmmoLineProduction.Utilization(SpecPierce, 99, PerNode), D, "넘어도 1");
            Assert.AreEqual(0f, AmmoLineProduction.Utilization(SpecPierce, 0, PerNode), D);
        }

        /// <summary>노드 1개 = 1발/초면 필요 노드 수가 곧 스펙이다 — 비용 배수 1 : 1.25 : 2.5의 밑.</summary>
        [Test]
        public void NodesForFullLine_EqualsSpec_WhenOneRoundPerNode()
        {
            Assert.AreEqual(5, AmmoLineProduction.NodesForFullLine(SpecPierce, PerNode));
            Assert.AreEqual(6, AmmoLineProduction.NodesForFullLine(SpecStandard, PerNode));
            Assert.AreEqual(2, AmmoLineProduction.NodesForFullLine(SpecExplosive, PerNode));
        }

        /// <summary>
        /// ⚠️ **구 등가선 「셋 다 100」은 폐기됐다** (`260909_W01` 2-2). 표준탄이 특수탄의
        /// 재료가 된 순간 그 선은 특수탄을 지배해 버린다 — 같은 출력을 더 긴 체인으로 얻는
        /// 자리가 생기기 때문이다. **새 축은 노드당 출력**이고, 여기서 재는 것은 그것이다.
        ///
        /// 이 시험이 남은 이유: 구 선이 실제로 깨졌다는 것을 값으로 붙들어 둔다.
        /// 되살리려는 사람이 있으면 여기가 빨개진다.
        /// </summary>
        [Test]
        public void OldEquivalenceLine_IsBroken_StandardYields60()
        {
            Assert.AreEqual(100f, AmmoLineProduction.LineOutput(SpecPierce, 5, PerNode) * DmgPierce, D,
                "관통은 아직 100이다");
            Assert.AreEqual(100f, AmmoLineProduction.LineOutput(SpecExplosive, 2, PerNode) * DmgExplosive, D,
                "폭발도 아직 100이다");

            Assert.AreEqual(60f, AmmoLineProduction.LineOutput(SpecStandard, 6, PerNode) * DmgStandard, D,
                "표준만 60 — 구 등가선이 깨진 자리(W01 2-1)");
        }

        /// <summary>
        /// **새 축 — 노드당 출력** (`260909_W01` 2-3 표).
        ///
        /// ⚠️ **여기서 세는 노드는 체인 전체다** — 군수 노드만이 아니라 앞에 붙는 가공·표준탄
        /// 라인까지다. 그래서 이 시험은 `AmmoLineProduction`(군수 노드만 센다)을 부르지 않고
        /// W01 2-3의 회계를 그대로 옮겨 적는다. **두 축을 한 함수로 재면 섞인다.**
        ///
        /// 폭발탄이 12.5로 튀는 것은 결함이 아니다 — 라인 스펙 2발/초가 상한이라
        /// **싸지만 더 못 쓴다.**
        /// </summary>
        [Test]
        public void OutputPerNode_MatchesW01Accounting()
        {
            // 탄종 | 1발/초에 드는 노드 | 최대 발사수 | 최대 출력 | 그때 노드 | 노드당
            AssertPerNode("표준", nodesPerShot: 2, maxShots: 6, damage: DmgStandard, expectedPerNode: 5f);
            AssertPerNode("관통", nodesPerShot: 4, maxShots: 5, damage: DmgPierce, expectedPerNode: 5f);
            AssertPerNode("폭발", nodesPerShot: 4, maxShots: 2, damage: DmgExplosive, expectedPerNode: 12.5f);
        }

        private static void AssertPerNode(string label, int nodesPerShot, int maxShots,
            float damage, float expectedPerNode)
        {
            float output = maxShots * damage;
            int nodes = nodesPerShot * maxShots;
            Assert.AreEqual(expectedPerNode, output / nodes, D,
                $"{label} — 최대 출력 {output} ÷ 노드 {nodes}");
        }

        // ---- 라인 조립 ----

        private static List<MunitionsLine> Representative() => new List<MunitionsLine>
        {
            new MunitionsLine(AmmoKind.Pierce, SpecPierce, DmgPierce, 1),
            new MunitionsLine(AmmoKind.Standard, SpecStandard, DmgStandard, 1),
            new MunitionsLine(AmmoKind.Explosive, SpecExplosive, DmgExplosive, 2),
        };

        /// <summary>
        /// 대표 상태 = 관통 1 + 표준 1 + 폭발 2 노드 → pA 1/1/2 재현 → 출력 **130**.
        /// ⚠️ **대표 상태 출력이 145에서 130으로 내려갔다** (`260909_W01` 2-1 · 표준탄 25 → 10).
        /// ⚠️ **구 서술 폐기** — 「`s3Break`(145)는 그대로다」로 적혀 있었는데,
        /// 그 앵커 자체가 **없어졌다**(2026-09-10 · `260910_W02` 2-2). **여기서 따라갈 값이 없다** —
        /// 요구치는 밸런스가 재산출할 값이지 대표 상태가 끌고 다닐 값이 아니다(W01 2-5).
        /// </summary>
        [Test]
        public void Representative_FourNodes_Reproduces130()
        {
            var lines = new List<AmmoLine>();
            AmmoLineProduction.BuildLines(Representative(), PerNode, lines);

            Assert.AreEqual(3, lines.Count);
            Assert.AreEqual(1f, lines[0].shotsPerSec, D, "관통 1노드 → 1발/초 (pA0)");
            Assert.AreEqual(1f, lines[1].shotsPerSec, D, "표준 1노드 → 1발/초 (pA1)");
            Assert.AreEqual(2f, lines[2].shotsPerSec, D, "폭발 2노드 → 2발/초 (pA2)");

            Assert.AreEqual(130f, AmmoLineProduction.TotalOutput(Representative(), PerNode), D,
                "20 + 10 + 100 = 130 — 대표 상태 출력(구 145)");
        }

        /// <summary>원점 100 = 관통 라인만 100% 가동(노드 5개). 밸런스 2장 origin의 basis 그대로.</summary>
        [Test]
        public void Origin_PierceLineOnly_FiveNodes_Yields100()
        {
            var only = new List<MunitionsLine>
            {
                new MunitionsLine(AmmoKind.Pierce, SpecPierce, DmgPierce, 5),
            };
            Assert.AreEqual(100f, AmmoLineProduction.TotalOutput(only, PerNode), D);
        }

        /// <summary>노드 한 칸을 비우면 그 라인만 줄어든다 — 전체가 0으로 접히지 않는다(V05 §1-3).</summary>
        [Test]
        public void RemovingOneNode_ReducesOnlyThatLine_NotToZero()
        {
            var reduced = new List<MunitionsLine>
            {
                new MunitionsLine(AmmoKind.Pierce, SpecPierce, DmgPierce, 1),
                new MunitionsLine(AmmoKind.Standard, SpecStandard, DmgStandard, 1),
                new MunitionsLine(AmmoKind.Explosive, SpecExplosive, DmgExplosive, 1), // 2 → 1
            };

            Assert.AreEqual(80f, AmmoLineProduction.TotalOutput(reduced, PerNode), D, "20 + 10 + 50");
            Assert.Greater(AmmoLineProduction.TotalOutput(reduced, PerNode), 0f, "0으로 접히지 않는다");
        }

        [Test]
        public void BuildLines_SkipsKindsWithNoNodes()
        {
            var mixed = new List<MunitionsLine>
            {
                new MunitionsLine(AmmoKind.Pierce, SpecPierce, DmgPierce, 0),
                new MunitionsLine(AmmoKind.Explosive, SpecExplosive, DmgExplosive, 2),
            };

            var lines = new List<AmmoLine>();
            AmmoLineProduction.BuildLines(mixed, PerNode, lines);

            Assert.AreEqual(1, lines.Count, "생산 0인 탄종은 라인을 만들지 않는다");
            Assert.AreEqual(AmmoKind.Explosive, lines[0].kind);
        }

        [Test]
        public void BuildLines_NullInputs_ClearsAndDoesNotThrow()
        {
            var lines = new List<AmmoLine> { new AmmoLine(AmmoKind.Pierce, 20f, 5f) };
            AmmoLineProduction.BuildLines(null, PerNode, lines);
            Assert.AreEqual(0, lines.Count);
        }
    }
}
