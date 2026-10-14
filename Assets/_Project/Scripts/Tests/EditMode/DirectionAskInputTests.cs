using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **패드가 안 눌린 까닭의 시험** (#93 · 2026-10-14 사용자 육안 「이거 안 눌리는데?」).
    ///
    /// 콘솔이 남긴 짝이 증거였다 — 「배치: 변환기 @ 셀(3,6)」 바로 뒤에 「제거 @ 셀(3,6)」.
    /// 제거를 부른 것은 <c>CancelAskedNode</c>, 곧 **바깥 누름**으로 판정된 것이다.
    /// </summary>
    public sealed class DirectionAskInputTests
    {
        [Test]
        public void 패드_안은_UI_가림막보다_앞이다()
        {
            // ⚠️⚠️ **이것이 결함의 시험이다.** 패드는 자기 자리를 가림막에 등록하므로
            //    overUi 가 **참**인 채로 들어온다. 그때 UI 쪽이 이기면 누름이 버려진다.
            Assert.AreEqual(DirectionAskPress.Answer,
                DirectionAskInput.Route(asking: true, insidePad: true, overUi: true),
                "패드 안을 눌렀는데 UI 가림막이 먹었다 — 화면에서는 「안 눌린다」가 된다");
        }

        [Test]
        public void 빈_자리_누름은_물음에_닿는다()
        {
            // 종전에 **유일하게** 동작했던 길이다(= 설치 취소).
            Assert.AreEqual(DirectionAskPress.Answer,
                DirectionAskInput.Route(asking: true, insidePad: false, overUi: false));
        }

        [Test]
        public void 다른_UI_위는_그_UI_것이다()
        {
            // 팔레트·나가기를 누른 것을 「바깥 누름」으로 읽으면 **노드가 조용히 사라진다.**
            Assert.AreEqual(DirectionAskPress.OtherUi,
                DirectionAskInput.Route(asking: true, insidePad: false, overUi: true));
        }

        [Test]
        public void 안_묻는_중이면_보드가_받는다()
        {
            Assert.AreEqual(DirectionAskPress.NotAsking,
                DirectionAskInput.Route(asking: false, insidePad: false, overUi: false));
            Assert.AreEqual(DirectionAskPress.NotAsking,
                DirectionAskInput.Route(asking: false, insidePad: true, overUi: true),
                "묻지도 않는데 패드 자리를 들고 있으면 안 된다");
        }
    }
}
