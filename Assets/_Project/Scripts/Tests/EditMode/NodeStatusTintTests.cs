using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 노드 상태 표시(UI 문서「노드 상태 표시」· 260825_V02 §1 개정).
    ///
    /// 개정의 핵심은 **색 축이 겹치지 않게 하는 것**이다. 노드 종류를 색상으로 구분하기로 하면서
    /// 산출률까지 색상으로 내면, 빨간 노드가 「정지」인지 「군수 노드」인지 구분되지 않는다.
    /// → 종류 = 색상(아트) / 산출률 = 밝기(틴트 곱셈).
    /// </summary>
    public sealed class NodeStatusTintTests
    {
        private const float D = 0.0001f;

        [Test]
        public void ConfirmedMultipliers()
        {
            Assert.AreEqual(1.0f, NodeStatusTint.Normal, D, "정상 = 원본");
            Assert.AreEqual(0.7f, NodeStatusTint.Slow, D, "감속");
            Assert.AreEqual(0.4f, NodeStatusTint.Stopped, D, "정지");
        }

        [Test]
        public void Ratio_MapsToThreeSteps()
        {
            Assert.AreEqual(NodeStatusTint.Stopped, NodeStatusTint.Of(0f), D, "완전 정지");
            Assert.AreEqual(NodeStatusTint.Slow, NodeStatusTint.Of(0.5f), D, "깎여서 돌아감");
            Assert.AreEqual(NodeStatusTint.Normal, NodeStatusTint.Of(1f), D, "설계대로");
        }

        /// <summary>단계는 셋뿐이다 — 4번째는 없다(모듈 과부하는 MVP 밖).</summary>
        [Test]
        public void OnlyThreeDistinctValues()
        {
            var seen = new System.Collections.Generic.HashSet<float>();
            for (int i = 0; i <= 20; i++) seen.Add(NodeStatusTint.Of(i / 20f));

            Assert.AreEqual(3, seen.Count);
        }

        /// <summary>
        /// 배율은 **단조 감소**해야 한다. 산출률이 낮을수록 어두워야 「밝고 선명 / 중간 /
        /// 어둡고 탁함」이라는 문서의 인상이 성립한다.
        /// </summary>
        [Test]
        public void DarkerAsOutputDrops()
        {
            Assert.Greater(NodeStatusTint.Of(1f), NodeStatusTint.Of(0.5f));
            Assert.Greater(NodeStatusTint.Of(0.5f), NodeStatusTint.Of(0f));
        }

        /// <summary>
        /// 모든 배율이 **1.0 이하**여야 한다. 곱셈 틴트는 어둡게만 할 수 있고 밝게는 못 한다 —
        /// 1을 넘는 값을 쓰면 클램프되어 단계가 뭉개진다. 이 제약이 채도를 축에서 뺀 이유이기도 하다.
        /// </summary>
        [Test]
        public void AllMultipliers_AreWithinMultiplyTintRange()
        {
            foreach (float m in new[] { NodeStatusTint.Normal, NodeStatusTint.Slow, NodeStatusTint.Stopped })
            {
                Assert.LessOrEqual(m, 1f, "곱셈 틴트는 밝게 만들지 못한다");
                Assert.Greater(m, 0f, "0이면 검게 뭉개져 종류 색이 사라진다");
            }
        }

        /// <summary>
        /// 정지도 완전히 검지는 않다. 0으로 두면 아트의 종류 색이 사라져
        /// 「정지한 군수 노드」가 「정지한 에너지 노드」와 구분되지 않는다.
        /// </summary>
        [Test]
        public void StoppedKeepsEnoughBrightness_ToReadNodeType()
        {
            Assert.GreaterOrEqual(NodeStatusTint.Stopped, 0.3f, "종류 색이 읽힐 만큼은 남긴다");
        }
    }
}
