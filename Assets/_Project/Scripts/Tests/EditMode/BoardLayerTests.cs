using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 전투 화면에서는 보드를 안 그린다 (2026-09-16 · 육안 ②).
    ///
    /// ⚠️⚠️ **왜 이것이 필요했나.** 보드를 숨기는 수단이 「멀리 두기」 하나였다 —
    /// 보드 중심 `y = -20` · 전투 중심 `y = 0` 으로 20 칸. 그런데 09-11 에 이동 클램프가
    /// 폐기되어 전장이 무한해졌고 09-15 에 카메라가 로봇을 따라가게 되면서,
    /// 전투 카메라 반높이 8 로 **로봇이 `y = -7` 아래로 가면 보드가 화면에 들어왔다.**
    ///
    /// 📌 여기서 지키는 것은 **「나머지 층을 안 건드린다」**이다. 마스크를 통째로 쓰면
    /// 씬이 정해 둔 다른 층까지 코드가 정하게 되고, 그것은 다음 결함이 된다.
    /// </summary>
    public sealed class BoardLayerTests
    {
        /// <summary>씬이 정해 둔 것으로 가정하는 다른 층들 — 이 시험이 지켜야 할 「남의 것」.</summary>
        private const int Others = (1 << 0) | (1 << 5);   // Default · UI

        [Test]
        public void 보드_화면이면_보드_층을_켠다()
        {
            int mask = BoardLayer.MaskFor(Others, true);
            Assert.That((mask & BoardLayer.Mask), Is.Not.Zero, "보드 층이 꺼져 있다");
        }

        [Test]
        public void 전투_화면이면_보드_층을_끈다()
        {
            int mask = BoardLayer.MaskFor(Others | BoardLayer.Mask, false);
            Assert.That((mask & BoardLayer.Mask), Is.Zero, "보드 층이 켜져 있다 — 전투 화면에 비친다");
        }

        [Test]
        public void 나머지_층은_그대로_둔다()
        {
            Assert.That(BoardLayer.MaskFor(Others, true) & Others, Is.EqualTo(Others), "켤 때");
            Assert.That(BoardLayer.MaskFor(Others, false) & Others, Is.EqualTo(Others), "끌 때");

            // 보드 층 말고는 한 비트도 안 달라져야 한다.
            Assert.That(BoardLayer.MaskFor(Others, false), Is.EqualTo(Others),
                "보드 층을 끄면서 다른 비트가 달라졌다");
        }

        [Test]
        public void 껐다_켜도_같은_자리로_돌아온다()
        {
            // 이 메서드는 매 프레임 돈다 — 쌓이거나 새면 안 된다.
            int on = BoardLayer.MaskFor(Others, true);
            int off = BoardLayer.MaskFor(on, false);
            int onAgain = BoardLayer.MaskFor(off, true);

            Assert.That(off, Is.EqualTo(Others), "끈 뒤가 원래와 달라졌다");
            Assert.That(onAgain, Is.EqualTo(on), "다시 켠 뒤가 처음과 달라졌다");
        }

        [Test]
        public void 층_번호는_이름이_아니라_상수다()
        {
            // ⚠️ `LayerMask.NameToLayer` 는 이름이 안 맞으면 조용히 -1 을 준다. -1 로 만든
            //    마스크는 전부 켜거나 전부 끄므로, 오타 하나가 화면을 통째로 바꾼다.
            //    그래서 번호를 한 곳에 적고(BoardLayer.Index) 여기서 못 박는다.
            Assert.That(BoardLayer.Index, Is.InRange(8, 31),
                "8~31 은 사용자 층이다. 0~7 은 유니티가 쓴다");
            Assert.That(BoardLayer.Mask, Is.EqualTo(1 << BoardLayer.Index), "마스크와 번호가 어긋났다");
        }
    }
}
