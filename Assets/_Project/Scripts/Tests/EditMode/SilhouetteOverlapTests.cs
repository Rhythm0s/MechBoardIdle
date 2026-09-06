using System;
using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 실루엣 측정 도구의 정답 케이스 (2026-09-06 신설 · 사용자 확정).
    ///
    /// **정답은 합성 케이스로만 고정한다.** 승인본 PNG의 실제 값을 박아 두면
    /// 재생성할 때마다 깨져서, 곧 아무도 안 보는 테스트가 된다.
    ///
    /// 이 파일이 막으려는 것은 하나다 — **측정법이 조용히 바뀌는 것.**
    /// 2026-09-06에 알파 bbox로 잘라 정사각으로 늘여 재던 값이 판정 셋에 들어갔고,
    /// 아무 테스트도 실패하지 않았다. <see cref="HorizontalAndVerticalBars_AreNotTheSameShape"/>가
    /// 그 방식이 돌아오면 깨지는 자리다.
    /// </summary>
    public sealed class SilhouetteOverlapTests
    {
        /// <summary>중심을 맞춘 직사각형 하나를 그린 마스크.</summary>
        private static AlphaMask CenteredRect(int canvas, int w, int h)
        {
            var bits = new bool[canvas * canvas];
            int x0 = (canvas - w) / 2, y0 = (canvas - h) / 2;
            for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                bits[y * canvas + x] = true;
            return new AlphaMask(canvas, canvas, bits);
        }

        private static AlphaMask RectAt(int canvas, int x0, int y0, int w, int h)
        {
            var bits = new bool[canvas * canvas];
            for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                bits[y * canvas + x] = true;
            return new AlphaMask(canvas, canvas, bits);
        }

        // ---- (가) 같은 것은 1.0 ----

        [Test]
        public void SameMask_IsFullyOverlapping()
        {
            AlphaMask m = CenteredRect(64, 20, 30);
            Assert.AreEqual(1f, SilhouetteOverlap.Ratio(m, m), 1e-6f);
        }

        // ---- (나) 안 겹치면 0.0 ----

        [Test]
        public void DisjointMasks_DoNotOverlap()
        {
            AlphaMask a = RectAt(64, 0, 0, 10, 10);
            AlphaMask b = RectAt(64, 40, 40, 10, 10);
            Assert.AreEqual(0f, SilhouetteOverlap.Ratio(a, b), 1e-6f);
        }

        // ---- (다) 이 케이스가 2026-09-06 결함을 잡는다 ----

        /// <summary>
        /// **가로로 긴 것과 세로로 긴 것은 다른 모양이다.**
        ///
        /// 가로 120×40과 세로 40×120을 같은 중심에 놓으면 겹치는 곳은 가운데 40×40뿐이다.
        ///   교집합 1,600 ÷ 합집합 (4,800 + 4,800 − 1,600) = 8,000 → **0.20**
        ///
        /// ⚠️ **구 측정법(알파 bbox로 잘라 정사각으로 리사이즈)이면 1.0이 나온다** —
        /// 둘 다 정사각으로 늘어나 완전히 같은 모양이 되기 때문이다.
        /// 이 한 줄이 그 방식이 돌아오는 것을 막는다.
        /// </summary>
        [Test]
        public void HorizontalAndVerticalBars_AreNotTheSameShape()
        {
            AlphaMask wide = CenteredRect(200, 120, 40);
            AlphaMask tall = CenteredRect(200, 40, 120);

            Assert.AreEqual(0.20f, SilhouetteOverlap.Ratio(wide, tall), 1e-4f,
                "가로 직사각과 세로 직사각이 1.0으로 나오면 종횡비를 지우는 측정법으로 되돌아간 것이다");

            // 가로세로비도 함께 남는다 — 구 방식은 둘 다 1.0으로 만들었다.
            Assert.AreEqual(3f, SilhouetteOverlap.AspectRatio(wide), 1e-4f);
            Assert.AreEqual(1f / 3f, SilhouetteOverlap.AspectRatio(tall), 1e-4f);
        }

        // ---- (라) 크기도 정보다 ----

        /// <summary>
        /// 같은 모양을 크기만 다르게 두면 **1.0이 아니어야 한다.**
        ///
        /// 품목 I-3의 해소 방법이 「폭발탄을 크기로 더 벌린다」이므로,
        /// 크기 차이를 지우는 측정법은 그 해법을 무효로 만든다.
        /// </summary>
        [Test]
        public void SameShapeDifferentSize_IsNotIdentical()
        {
            AlphaMask small = CenteredRect(100, 20, 20);
            AlphaMask big = CenteredRect(100, 40, 40);

            float r = SilhouetteOverlap.Ratio(small, big);
            Assert.AreEqual(400f / 1600f, r, 1e-4f, "작은 쪽이 큰 쪽에 통째로 들어가므로 400 ÷ 1600");
            Assert.Less(r, 1f, "크기가 다르면 1.0일 수 없다 — 크기는 정보다");
        }

        // ---- (마) 캔버스가 다르면 예외 ----

        [Test]
        public void DifferentCanvas_Throws()
        {
            AlphaMask a = CenteredRect(64, 20, 20);
            AlphaMask b = CenteredRect(192, 20, 20);

            Assert.Throws<ArgumentException>(() => SilhouetteOverlap.Ratio(a, b),
                "늘이거나 잘라 맞추지 않는다 — 맞출 수 없으면 재지 않는다");
        }

        // ---- 여백 ----

        [Test]
        public void Margins_AreCountedFromEachEdge()
        {
            AlphaMask m = RectAt(100, 4, 7, 10, 10);   // x 4~13 · y 7~16
            MarginPx g = SilhouetteOverlap.Margins(m);

            Assert.AreEqual(4, g.left);
            Assert.AreEqual(100 - 1 - 13, g.right);
            Assert.AreEqual(7, g.top);
            Assert.AreEqual(100 - 1 - 16, g.bottom);
            Assert.AreEqual(4, g.Min, "판정은 가장 좁은 변으로 한다 — 한 변만 붙어도 붙은 것이다");
        }

        [Test]
        public void EmptyMask_HasNoMarginsToMeasure()
        {
            var empty = new AlphaMask(32, 32, new bool[32 * 32]);

            Assert.Throws<ArgumentException>(() => SilhouetteOverlap.Margins(empty));
            Assert.Throws<ArgumentException>(() => SilhouetteOverlap.AspectRatio(empty));
            Assert.AreEqual(0f, SilhouetteOverlap.Ratio(empty, empty),
                "나눌 것이 없는 것을 1.0(완전히 같다)으로 적으면 거짓말이 된다");
        }
    }
}
