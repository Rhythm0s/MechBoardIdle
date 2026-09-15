using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MBI.UI;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 글자 크기 사다리 — **아틀라스가 넘치지 않는다는 것을 셈으로 지킨다**.
    ///
    /// ⚠️ **같은 병이 네 번 왔다.** 유니티 동적 폰트는 크기마다 글리프를 따로 굽고,
    /// 한글은 완성형이라 아틀라스가 금세 찬다. 차면 **에러 없이 글자만 사라진다** —
    /// · 09-14 「마운트」 → 「마으ㅌ」 (크기 가짓수)
    /// · 09-15 「팔R」 → 「판1」 (스냅을 안 씌운 자리)
    /// · 09-15 「멈춤」 → 「먹춤」 (상한이 없었다 → 64 를 넣음)
    /// · 09-15 넓은 창(1890)에서 **하단 글자가 통째로 소멸** (상한 64 로도 부족)
    ///
    /// 앞의 셋은 **증상을 따라갔다.** 이 시험은 **예산을 지킨다** — 문자열이 늘거나
    /// 사다리가 늘면 **빌드 전에** 빨개진다.
    /// </summary>
    public sealed class KoreanFontSnapTests
    {
        [Test]
        public void 사다리_위의_값으로만_올린다()
        {
            Assert.That(KoreanFont.Snap(1), Is.EqualTo(16));
            Assert.That(KoreanFont.Snap(16), Is.EqualTo(16));
            Assert.That(KoreanFont.Snap(17), Is.EqualTo(24));
            Assert.That(KoreanFont.Snap(24), Is.EqualTo(24));
            Assert.That(KoreanFont.Snap(25), Is.EqualTo(36));
            Assert.That(KoreanFont.Snap(37), Is.EqualTo(52));
            Assert.That(KoreanFont.Snap(52), Is.EqualTo(52));
        }

        [Test]
        public void 아무리_커도_사다리_밖으로_안_나간다()
        {
            Assert.That(KoreanFont.Snap(500), Is.EqualTo(52));
            Assert.That(KoreanFont.Snap(int.MaxValue), Is.EqualTo(52));
        }

        [Test]
        public void 크기_가짓수가_사다리_수와_같다()
        {
            var seen = new HashSet<int>();
            for (int px = -10; px <= 600; px++) seen.Add(KoreanFont.Snap(px));

            Assert.That(seen.Count, Is.EqualTo(KoreanFont.Ladder.Length),
                "가짓수가 사다리보다 많으면 어딘가 사다리를 안 타고 있다");
        }

        [Test]
        public void 아틀라스_예산_안에_든다()
        {
            // ⚠️ **글자 수를 다시 센다** — 상수를 믿지 않는다. 문자열이 늘면 여기서 걸린다.
            int glyphs = CountKoreanGlyphsInStrings();

            long needed = 0;
            foreach (int size in KoreanFont.Ladder)
            {
                // 글리프 한 장은 크기에 여백(characterPadding 1)이 붙는다 — 넉넉히 +2.
                long side = size + 2;
                needed += (long)glyphs * side * side;
            }

            long budget = (long)KoreanFont.AtlasSideAssumed * KoreanFont.AtlasSideAssumed;

            Assert.That(needed, Is.LessThan(budget),
                $"글자 {glyphs}자 × 사다리 {KoreanFont.Ladder.Length}단계 = {needed / 1e6:F2}M px 로 "
                + $"아틀라스 {budget / 1e6:F2}M 을 넘는다 — 넘으면 에러 없이 글자가 사라진다");
        }

        [Test]
        public void 실측_글자수가_크게_안_어긋났다()
        {
            int now = CountKoreanGlyphsInStrings();

            // 상수는 예산 주석의 근거다 — 크게 벌어지면 주석이 거짓말이 된다.
            Assert.That(now, Is.EqualTo(KoreanFont.MeasuredGlyphCount).Within(80),
                $"실측 {now}자 · 적어 둔 값 {KoreanFont.MeasuredGlyphCount}자 — "
                + "차이가 크면 `KoreanFont.MeasuredGlyphCount` 와 예산 주석을 다시 적는다");
        }

        /// <summary>
        /// **화면에 나올 수 있는** 한글 유일 글자 수. 문자열 리터럴만 센다 —
        /// 주석은 화면에 안 나오므로 아틀라스를 안 먹는다.
        /// </summary>
        private static int CountKoreanGlyphsInStrings()
        {
            string root = Path.Combine("Assets", "_Project", "Scripts");
            var chars = new HashSet<char>();
            var literal = new Regex("\"((?:[^\"\\\\\\n]|\\\\.)*)\"");

            foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string norm = path.Replace('\\', '/');
                // 시험과 에디터 도구는 빌드에 안 들어간다 — 화면에도 안 나온다.
                if (norm.Contains("/Tests/") || norm.Contains("/Editor/")) continue;

                foreach (Match m in literal.Matches(File.ReadAllText(path)))
                    foreach (char ch in m.Groups[1].Value)
                        if ((ch >= '가' && ch <= '힣') || (ch >= 'ㄱ' && ch <= 'ㆎ'))
                            chars.Add(ch);
            }
            return chars.Count;
        }
    }
}
