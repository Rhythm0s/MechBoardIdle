using MBI.UI;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **설정 판 소리 줄 여백 줄이기** (#159 · 2026-10-16 사용자 육안 「너무 크다」).
    /// 여백만 줄이고 글자 크기는 그대로 둔다.
    /// </summary>
    public sealed class SoundRowPaddingTests
    {
        [Test]
        public void 줄_높이는_종전의_약_절반이다()
        {
            float old = 30f + 26f + 14f;
            float now = AudioOptionsPanel.ChannelRowHeight(1f);
            Assert.AreEqual(39f, now, 1e-4f);
            Assert.Less(now, old * 0.6f);
            Assert.AreEqual(now * 3f, AudioOptionsPanel.ChannelsHeight(1f), 1e-4f);
        }

        [Test]
        public void 글자_크기는_종전_식_그대로다()
        {
            foreach (float sc in new[] { 0.5f, 1f, 1.6f, 2.4f })
                Assert.AreEqual(
                    MBI.UI.KoreanFont.Snap(UnityEngine.Mathf.Max(11, UnityEngine.Mathf.RoundToInt(30f * sc * 0.62f))),
                    AudioOptionsPanel.LabelFontSize(sc), $"배율 {sc}");
        }
    }
}
