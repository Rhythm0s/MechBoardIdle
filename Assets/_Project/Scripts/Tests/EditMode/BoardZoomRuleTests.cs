using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **줌인·줌아웃이 번갈아 뛰던 자리** (2026-10-14 사용자 육안).
    /// </summary>
    public sealed class BoardZoomRuleTests
    {
        [Test]
        public void 배율이_1이면_설계값_그대로다()
        {
            Assert.AreEqual(5f, BoardZoomRule.TargetOrtho(true, 5f, 8f, 1f), 0.0001f);
        }

        [Test]
        public void 배율이_크면_시야가_좁아진다()
        {
            // 2배로 당겨 보면 보이는 높이는 절반이다.
            Assert.AreEqual(2.5f, BoardZoomRule.TargetOrtho(true, 5f, 8f, 2f), 0.0001f);
        }

        [Test]
        public void 전투_화면에는_배율이_안_걸린다()
        {
            // ⚠️ 조립을 확대해 둔 채 나가면 전투가 통째로 확대된 채 찍힌다.
            Assert.AreEqual(8f, BoardZoomRule.TargetOrtho(false, 5f, 8f, 2.5f), 0.0001f);
        }

        [Test]
        public void 배율이_0이어도_안_터진다()
        {
            // 0 으로 나누면 시야가 무한대가 되어 화면이 통째로 사라진다.
            Assert.IsTrue(BoardZoomRule.TargetOrtho(true, 5f, 8f, 0f) > 0f);
            Assert.IsFalse(float.IsInfinity(BoardZoomRule.TargetOrtho(true, 5f, 8f, 0f)));
        }

        [Test]
        public void 기본_배율은_1이다()
        {
            BoardZoomSignals.Reset();
            Assert.AreEqual(1f, BoardZoomSignals.Factor, 0.0001f);
        }
    }
}
