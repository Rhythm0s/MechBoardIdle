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
    }
}
