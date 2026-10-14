using MBI.UI;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **「…」는 숫자만** (2026-10-14 사용자 확정 ⑥).
    /// </summary>
    public sealed class TextClipPolicyTests
    {
        [Test]
        public void 숫자는_줄여도_된다()
        {
            Assert.IsTrue(TextClipPolicy.MayEllipsize("1,109"));
            Assert.IsTrue(TextClipPolicy.MayEllipsize("12.5%"));
            Assert.IsTrue(TextClipPolicy.MayEllipsize("-3"));
            Assert.IsTrue(TextClipPolicy.MayEllipsize("1 109"));
        }

        [Test]
        public void 글자는_못_줄인다()
        {
            // ⚠️ 이 넷은 실제로 화면에서 잘렸던 글자다 — 자르면 뜻이 바뀐다.
            Assert.IsFalse(TextClipPolicy.MayEllipsize("다음 스테이지로"));
            Assert.IsFalse(TextClipPolicy.MayEllipsize("승리 — 적 전멸"));
            Assert.IsFalse(TextClipPolicy.MayEllipsize("효과음"));
            Assert.IsFalse(TextClipPolicy.MayEllipsize("다시 (Restart)"));
        }

        [Test]
        public void 숫자가_섞인_글자도_못_줄인다()
        {
            // 「골드 20」은 글자다 — 줄이려면 숫자 부분만 따로 떼어 물어야 한다.
            Assert.IsFalse(TextClipPolicy.MayEllipsize("골드 20"));
            Assert.IsFalse(TextClipPolicy.MayEllipsize("맵 2/10"));
        }

        [Test]
        public void 수가_아닌_기호만으로는_안_된다()
        {
            Assert.IsFalse(TextClipPolicy.MayEllipsize("%"));
            Assert.IsFalse(TextClipPolicy.MayEllipsize("--"));
            Assert.IsFalse(TextClipPolicy.MayEllipsize(""));
            Assert.IsFalse(TextClipPolicy.MayEllipsize(null));
        }
    }
}
