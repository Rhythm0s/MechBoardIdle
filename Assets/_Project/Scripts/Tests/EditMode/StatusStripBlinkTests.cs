using MBI.UI;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **경고 점멸** (2026-09-30 사용자 확정 ③ — 문제가 나면 빨갛게 깜빡인다).
    ///
    /// ⚠️ 지켜야 하는 것 — **꺼져도 완전히 안 사라진다.** 0 까지 내리면 글자가 있다 없다
    /// 해서 읽는 사람이 눈으로 쫓게 된다. 읽히는 것이 목적이지 눈길을 끄는 것이 목적이 아니다.
    /// </summary>
    public sealed class StatusStripBlinkTests
    {
        [Test]
        public void 완전히_안_사라진다()
        {
            // 한 주기를 촘촘히 훑는다 — 어느 순간에도 바닥이 0 이면 안 된다.
            for (int i = 0; i <= 200; i++)
            {
                float t = StatusStrip.BlinkSeconds * i / 200f;
                float a = StatusStrip.BlinkAlpha(t);
                Assert.Greater(a, 0.3f, $"t={t} 에서 글자가 거의 사라졌다");
                Assert.LessOrEqual(a, 1.0001f, $"t={t} 에서 1 을 넘었다");
            }
        }

        [Test]
        public void 한_주기_안에_밝고_어두운_때가_다_있다()
        {
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i <= 200; i++)
            {
                float a = StatusStrip.BlinkAlpha(StatusStrip.BlinkSeconds * i / 200f);
                lo = Mathf.Min(lo, a);
                hi = Mathf.Max(hi, a);
            }
            Assert.Greater(hi - lo, 0.3f, "밝기 차가 너무 작아 점멸로 안 읽힌다");
        }

        /// <summary>주기가 돌아온다 — 시간이 흘러도 같은 위상에서 같은 값이다.</summary>
        [Test]
        public void 주기가_돌아온다()
        {
            const float D = 1e-4f;
            for (int i = 0; i < 5; i++)
            {
                float t = 0.17f + StatusStrip.BlinkSeconds * i;
                Assert.AreEqual(StatusStrip.BlinkAlpha(0.17f), StatusStrip.BlinkAlpha(t), D,
                    $"{i} 주기 뒤에 값이 달라졌다");
            }
        }

        /// <summary>⚠️ 음수 시간에도 안 깨진다 — `unscaledTime` 이 0 에서 시작하지만 방어한다.</summary>
        [Test]
        public void 음수_시간에도_범위_안이다()
        {
            float a = StatusStrip.BlinkAlpha(-0.3f);
            Assert.Greater(a, 0.3f);
            Assert.LessOrEqual(a, 1.0001f);
        }

        /// <summary>
        /// **글자 크기는 사다리를 한 칸씩 내려간다** (2026-09-30 · 빌드 육안 두 번째).
        ///
        /// ⚠️⚠️ `KoreanFont.Snap` 은 **올려** 잡으므로 `Snap(px - 1)` 로는 안 내려간다
        /// (44 → Snap(43) = 44). 그래서 줄이는 길을 따로 뒀고, 이 시험이 그것을 지킨다.
        /// 안 지키면 긴 경고문이 칸 밖으로 흘러 옆의 마일스톤 카드를 다시 침범한다.
        /// </summary>
        [Test]
        public void 사다리를_한_칸씩_내려간다()
        {
            int[] ladder = KoreanFont.Ladder;

            // 위에서부터 훑으며 **반드시 작아지는지** 본다.
            int px = ladder[ladder.Length - 1];
            for (int i = ladder.Length - 1; i > 0; i--)
            {
                int next = StatusStrip.NextSmallerForTest(px);
                Assert.Less(next, px, $"{px} 에서 안 내려갔다 — Snap 이 다시 올린 것이다");
                Assert.Contains(next, ladder, $"{next} 는 사다리 위의 값이 아니다");
                px = next;
            }

            // 바닥에서는 제자리 — 부르는 쪽이 그것으로 멈춘다(무한 반복 방지).
            Assert.AreEqual(ladder[0], StatusStrip.NextSmallerForTest(ladder[0]),
                "바닥에서 더 내려갔다 — 부르는 쪽이 안 멈춘다");
        }

        /// <summary>사다리 밖의 수를 줘도 사다리 위로 떨어진다.</summary>
        [Test]
        public void 사다리_밖의_수도_사다리로_떨어진다()
        {
            int next = StatusStrip.NextSmallerForTest(40);   // 36 과 44 사이
            Assert.Contains(next, KoreanFont.Ladder);
            Assert.Less(next, 40);
        }
    }
}
