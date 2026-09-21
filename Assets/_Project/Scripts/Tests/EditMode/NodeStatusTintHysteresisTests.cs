using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **경계에서 떨어도 단계가 안 바뀐다** (2026-09-21 사용자 육안 — 「작동 중인 노드가 깜빡인다」).
    ///
    /// 설계대로 도는 노드도 산출률이 **1.000 언저리에서 미세하게 떨린다**(벨트 도착이
    /// 이산이라 틱마다 조금씩 다르다). 문턱이 하나면 그 떨림이 그대로 **밝기 튐**이 된다.
    ///
    /// 📌 이 리포에 같은 병을 고친 자리가 있다 — `DirectionHysteresis`
    /// (「대각에서 얼굴이 빠르게 뒤집힌다」). 같은 것을 다른 축에서 한 번 더 한다.
    /// </summary>
    public sealed class NodeStatusTintHysteresisTests
    {
        private const float D = 0.0001f;

        /// <summary>
        /// 화면에서 난 그 일 — 1.000 을 사이에 두고 오르내려도 **한 번도 안 바뀌어야** 한다.
        /// </summary>
        [Test]
        public void 정상_언저리에서_떨어도_안_깜빡인다()
        {
            float tint = NodeStatusTint.Normal;
            float[] jitter = { 1.000f, 0.9985f, 1.0002f, 0.998f, 1.001f, 0.9991f, 0.9975f };

            foreach (float r in jitter)
            {
                tint = NodeStatusTint.Of(r, tint);
                Assert.AreEqual(NodeStatusTint.Normal, tint, D,
                    $"산출률 {r} 에서 단계가 바뀌었다 — 이것이 깜빡임이다");
            }
        }

        /// <summary>
        /// ⚠️ **둔해지기만 하면 안 된다** — 정말로 깎여 돌면 내려와야 한다.
        /// 안 그러면 진단이 「다 잘 돈다」고 거짓말을 한다.
        /// </summary>
        [Test]
        public void 정말_깎이면_내려온다()
        {
            float tint = NodeStatusTint.Of(0.5f, NodeStatusTint.Normal);
            Assert.AreEqual(NodeStatusTint.Slow, tint, D, "반만 도는데 정상으로 남았다");

            Assert.AreEqual(NodeStatusTint.Stopped,
                NodeStatusTint.Of(0f, NodeStatusTint.Normal), D, "멈췄는데 정상이다");
        }

        /// <summary>
        /// **올라가는 문턱은 안 느슨해졌다** — 느슨하게 하면 「깎여 도는데 정상」이 된다.
        /// 벌린 것은 내려오는 쪽뿐이다.
        /// </summary>
        [Test]
        public void 올라가는_문턱은_그대로다()
        {
            Assert.AreEqual(NodeStatusTint.Slow,
                NodeStatusTint.Of(0.99f, NodeStatusTint.Slow), D,
                "0.99 는 설계대로가 아니다 — 정상으로 올리면 안 된다");

            Assert.AreEqual(NodeStatusTint.Normal,
                NodeStatusTint.Of(1f, NodeStatusTint.Slow), D, "설계대로인데 감속이다");
        }

        [Test]
        public void 정지에서_조금이라도_돌면_올라온다()
        {
            Assert.AreEqual(NodeStatusTint.Slow,
                NodeStatusTint.Of(0.3f, NodeStatusTint.Stopped), D);
            Assert.AreEqual(NodeStatusTint.Stopped,
                NodeStatusTint.Of(0.001f, NodeStatusTint.Stopped), D,
                "거의 안 도는데 감속으로 올라갔다");
        }

        /// <summary>구 한 인자 판은 그대로 둔다 — 부르는 곳이 남아 있고 뜻도 안 바뀌었다.</summary>
        [Test]
        public void 구_판은_그대로다()
        {
            Assert.AreEqual(NodeStatusTint.Normal, NodeStatusTint.Of(1f), D);
            Assert.AreEqual(NodeStatusTint.Slow, NodeStatusTint.Of(0.5f), D);
            Assert.AreEqual(NodeStatusTint.Stopped, NodeStatusTint.Of(0f), D);
        }
    }
}
