using MBI.UI;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>#212 — 메인 메뉴 소리 안내 문구는 사용자 문구 그대로(2026-10-19).</summary>
    public sealed class MainMenuNoticeTests
    {
        [Test]
        public void SoundNotice_IsUserTextVerbatim() =>
            Assert.AreEqual("플레이 시, BGM 및 사운드가 자동 재생이 진행되니, 이 점 유의해주시면 감사하겠습니다.",
                MainMenuOverlay.SoundAutoplayNotice);
    }
}
