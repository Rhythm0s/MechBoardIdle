using MBI.UI;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 레터박스 — **내용은 언제나 9:16** (2026-09-15 사용자 확정 A안 · 육안 8차 ④).
    ///
    /// ⚠️⚠️ **왜 이것이 필요했나.** `UiLayout.Scale` 은 **화면 높이 하나**로 정해진다.
    /// 그 전제는 화면이 기준 캔버스(1440×2560 · 9:16)와 **같은 비율**이라는 것이었는데,
    /// 웹에서는 창이 아무 비율이나 된다 — **가로로 긴 창**에서는 높이가 작아
    /// **UI 전체가 같이 쪼그라들었다.** 실측에서 노드 버튼이 문서 최소(150)의
    /// 4분의 1 남짓으로 찍혔고, 사용자 화면에서는 **띠가 비어 보였다.**
    ///
    /// 📌 **띠 안에서 버튼만 키울 수는 없다** — 띠 자체가 그만큼 얇아지기 때문이다.
    /// 그래서 **바깥에서** 푼다: 그리는 면을 9:16 으로 잘라 두면 `Scale` 이 늘 같은 뜻을
    /// 갖고, 창이 어떻게 생기든 **버튼 크기가 화면에서 차지하는 비율이 같다.**
    ///
    /// ⚠️ **자르는 일은 `WebGLBuilder` 가 만든 `index.html` 이 한다**(캔버스를 9:16 으로
    /// 두고 가운데 정렬 · 남는 자리는 검정). 여기서 지키는 것은 **그 면을 받았을 때
    /// 자리 셈이 성립하는가**다 — 자르는 쪽은 브라우저에서 눈으로 확인한다.
    /// </summary>
    public sealed class LetterboxLayoutTests
    {
        /// <summary>기준 캔버스의 가로세로 비 — 9:16.</summary>
        private const float DesignAspect = 1440f / 2560f;

        /// <summary>레터박스가 잘라 주는 면. 창이 어떻든 이 비율로 들어온다.</summary>
        private static Vector2 Surface(float winW, float winH)
        {
            float k = Mathf.Min(winW / 1440f, winH / 2560f);
            return new Vector2(1440f * k, 2560f * k);
        }

        private static readonly Vector2[] Windows =
        {
            new Vector2(1920f, 1080f),   // 가로로 긴 데스크톱 — 여기서 UI 가 쪼그라들었다
            new Vector2(1280f, 800f),
            new Vector2(740f, 1000f),    // 사용자 육안 8차 창
            new Vector2(540f, 960f),     // 세로 — 거의 기준 비율
            new Vector2(2560f, 1440f),
            new Vector2(400f, 1600f),    // 아주 좁고 긴 창
        };

        [Test]
        public void 어떤_창에서도_내용은_9대16이다()
        {
            foreach (Vector2 win in Windows)
            {
                Vector2 s = Surface(win.x, win.y);
                Assert.That(s.x / s.y, Is.EqualTo(DesignAspect).Within(0.001f),
                    $"{win.x}x{win.y} — 내용 비율이 9:16 이 아니다");

                // 창 밖으로 안 나간다 — 나가면 가장자리가 잘려 띠가 안 보인다(육안 8차 ④).
                Assert.That(s.x, Is.LessThanOrEqualTo(win.x + 0.01f), $"{win.x}x{win.y} 가로 넘침");
                Assert.That(s.y, Is.LessThanOrEqualTo(win.y + 0.01f), $"{win.x}x{win.y} 세로 넘침");
            }
        }

        [Test]
        public void 버튼은_언제나_기준값_곱하기_배율이다()
        {
            foreach (Vector2 win in Windows)
            {
                Vector2 s = Surface(win.x, win.y);
                float scale = UiLayout.Scale(s.y);

                // ⚠️ **이것이 A안의 값이다** — 창이 어떻게 생기든 버튼은 기준 216 에
                // 배율만 곱한 크기다. 레터박스가 없으면 같은 식이 **화면마다 다른 뜻**이 된다.
                float expected = UiLayout.PaletteButtonSize * scale;

                // 값은 09-16 에 216 → 182 로 바뀌었다. **지키는 것은 값이 아니라 비**다 —
                // 창이 어떻게 생기든 버튼이 화면 높이에서 차지하는 비율이 같아야 한다.
                Assert.That(expected / s.y,
                    Is.EqualTo(UiLayout.PaletteButtonSize / 2560f).Within(0.0001f),
                    $"{win.x}x{win.y} — 버튼이 화면 높이에서 차지하는 비율이 달라졌다");
            }
        }

        [Test]
        public void 띠_다섯의_합이_기준_높이와_같다()
        {
            // 레터박스가 성립하려면 띠 다섯이 기준 2560 을 정확히 채워야 한다 —
            // 남거나 넘치면 검은 자리가 띠 사이에 생기거나 마지막 띠가 잘린다.
            float sum = UiLayout.CombatHeight + UiLayout.BoardHeight
                        + UiLayout.FloatBandHeight + UiLayout.VariablePanelHeight
                        + UiLayout.ActionBarHeight;

            Assert.That(sum, Is.EqualTo(2560f).Within(0.01f),
                $"띠 다섯의 합이 {sum} 이다 — 기준 높이 2560 과 달라지면 레터박스가 어긋난다");
        }

        [Test]
        public void 세로로_긴_창에서는_위아래가_남는다()
        {
            // 모바일처럼 기준보다 **더 긴** 창에서는 가로가 꽉 차고 위아래가 검게 남는다.
            Vector2 s = Surface(400f, 1600f);

            Assert.That(s.x, Is.EqualTo(400f).Within(0.01f), "가로가 꽉 차야 한다");
            Assert.That(s.y, Is.LessThan(1600f), "위아래에 남는 자리가 있어야 한다");
        }
    }
}
