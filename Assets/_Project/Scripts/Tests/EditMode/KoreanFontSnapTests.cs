using MBI.UI;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 글자 크기 스냅 — **가짓수와 크기를 둘 다 막는다**.
    ///
    /// ⚠️ **같은 병이 세 번 왔다.** 유니티 동적 폰트는 크기마다 글리프를 따로 굽고,
    /// 한글은 완성형이라 아틀라스가 금세 찬다. 차면 굽지 못한 글리프가 사라지거나
    /// 엉뚱하게 찍힌다 —
    /// · 09-14 「마운트」 → 「마으ㅌ」 (크기 **가짓수**)
    /// · 09-15 아침 「팔R」 → 「판1」 (스냅을 안 씌운 자리)
    /// · 09-15 조립 화면 실측 — 구역 이름표가 배율 따라 거대해지자 **팔레트 버튼 글자와
    ///   카테고리 탭 글자가 통째로 사라지고** 「다리L」이 「다리I」로 찍혔다 (**크기 자체**)
    /// </summary>
    public sealed class KoreanFontSnapTests
    {
        [Test]
        public void 네_칸으로_올린다()
        {
            Assert.That(KoreanFont.Snap(17), Is.EqualTo(20));
            Assert.That(KoreanFont.Snap(20), Is.EqualTo(20));
            Assert.That(KoreanFont.Snap(21), Is.EqualTo(24));
        }

        [Test]
        public void 최소보다_작으면_최소다()
        {
            Assert.That(KoreanFont.Snap(3), Is.EqualTo(KoreanFont.MinSize));
            Assert.That(KoreanFont.Snap(0), Is.EqualTo(KoreanFont.MinSize));
            Assert.That(KoreanFont.Snap(-5), Is.EqualTo(KoreanFont.MinSize));
        }

        [Test]
        public void 상한보다_크면_상한이다()
        {
            // ⚠️ 이것이 09-15 조립 화면에서 글자가 사라진 자리를 막는다.
            // 스냅만으로는 **가짓수**만 줄고 한 글리프의 **크기**는 안 줄어든다.
            Assert.That(KoreanFont.Snap(200), Is.EqualTo(KoreanFont.MaxSize));
            Assert.That(KoreanFont.Snap(KoreanFont.MaxSize), Is.EqualTo(KoreanFont.MaxSize));
            Assert.That(KoreanFont.Snap(KoreanFont.MaxSize + 1), Is.EqualTo(KoreanFont.MaxSize));
        }

        [Test]
        public void 배율을_키워도_가짓수가_유한하다()
        {
            // 칸이 커질수록 구역 이름표가 커진다 — 어디까지 커지든 **결과는 몇 가지뿐**이어야
            // 아틀라스가 안 찬다.
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int px = 1; px <= 400; px++) seen.Add(KoreanFont.Snap(px));

            Assert.That(seen.Count, Is.LessThanOrEqualTo(KoreanFont.MaxSize / KoreanFont.Step + 2),
                "크기 가짓수가 상한까지로 묶여야 한다");
            foreach (int size in seen)
                Assert.That(size, Is.LessThanOrEqualTo(KoreanFont.MaxSize));
        }
    }
}
