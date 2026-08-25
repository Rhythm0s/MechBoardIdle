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

        private const float SpecPierce = 5f, SpecSplit = 4f, SpecExplosive = 2f;
        private const float DmgPierce = 20f, DmgSplit = 25f, DmgExplosive = 50f;

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
            Assert.AreEqual(4, AmmoLineProduction.NodesForFullLine(SpecSplit, PerNode));
            Assert.AreEqual(2, AmmoLineProduction.NodesForFullLine(SpecExplosive, PerNode));
        }

        /// <summary>
        /// 등가선: 세 탄종 모두 100% 가동 시 DPS 100이다. 여기서 비용 배수가 나온다 —
        /// 같은 100 DPS에 관통 5 / 분열 4 / 폭발 2 노드가 드니 노드당 비용은 1 : 1.25 : 2.5여야 균형이다.
        /// 「폭발 = 관통의 2배」로 두면 폭발이 1.25배 저렴해져 폭발 편중이 자명한 최적해가 된다.
        /// </summary>
        [Test]
        public void EquivalenceLine_FullLineOfAnyKind_Yields100()
        {
            Assert.AreEqual(100f, AmmoLineProduction.LineOutput(SpecPierce, 5, PerNode) * DmgPierce, D);
            Assert.AreEqual(100f, AmmoLineProduction.LineOutput(SpecSplit, 4, PerNode) * DmgSplit, D);
            Assert.AreEqual(100f, AmmoLineProduction.LineOutput(SpecExplosive, 2, PerNode) * DmgExplosive, D);
        }

        // ---- 라인 조립 ----

        private static List<MunitionsLine> Representative() => new List<MunitionsLine>
        {
            new MunitionsLine(AmmoKind.Pierce, SpecPierce, DmgPierce, 1),
            new MunitionsLine(AmmoKind.Split, SpecSplit, DmgSplit, 1),
            new MunitionsLine(AmmoKind.Explosive, SpecExplosive, DmgExplosive, 2),
        };

        /// <summary>대표 상태 = 관통 1 + 분열 1 + 폭발 2 노드 → pA 1/1/2 재현 → 출력 145(§9 s3Break).</summary>
        [Test]
        public void Representative_FourNodes_Reproduces145()
        {
            var lines = new List<AmmoLine>();
            AmmoLineProduction.BuildLines(Representative(), PerNode, lines);

            Assert.AreEqual(3, lines.Count);
            Assert.AreEqual(1f, lines[0].shotsPerSec, D, "관통 1노드 → 1발/초 (pA0)");
            Assert.AreEqual(1f, lines[1].shotsPerSec, D, "분열 1노드 → 1발/초 (pA1)");
            Assert.AreEqual(2f, lines[2].shotsPerSec, D, "폭발 2노드 → 2발/초 (pA2)");

            Assert.AreEqual(145f, AmmoLineProduction.TotalOutput(Representative(), PerNode), D,
                "20 + 25 + 100 = 145 — 대표 상태 출력");
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
                new MunitionsLine(AmmoKind.Split, SpecSplit, DmgSplit, 1),
                new MunitionsLine(AmmoKind.Explosive, SpecExplosive, DmgExplosive, 1), // 2 → 1
            };

            Assert.AreEqual(95f, AmmoLineProduction.TotalOutput(reduced, PerNode), D, "20 + 25 + 50");
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
