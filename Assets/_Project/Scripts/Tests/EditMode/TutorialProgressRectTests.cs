using MBI.UI;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 튜토리얼 진행 두 줄의 자리 (2026-09-15 · 육안 ④ 결함).
    ///
    /// ⚠️ **막는 것** — 날 픽셀 자리(`Rect(12, Screen.height − 96, 420, 84)`)가 띠 체계 밖이라
    /// 화면이 낮으면 HUD 첫 줄과 겹쳤다. 09-14 하단 넷과 같은 병이다.
    /// </summary>
    public sealed class TutorialProgressRectTests
    {
        [Test]
        public void 부유_띠_안에_앉는다()
        {
            const float w = 1440f, h = 2560f;
            Rect band = UiLayout.BandRect(UiLayout.Band.FloatBand, w, h);
            Rect r = UiLayout.TutorialProgressRect(w, h);

            Assert.That(r.y, Is.GreaterThanOrEqualTo(band.y), "띠 위로 삐져나가면 보드를 가린다");
            Assert.That(r.yMax, Is.LessThanOrEqualTo(band.yMax + 0.001f), "띠 아래로 넘치면 변수 패널을 덮는다");
        }

        [Test]
        public void 배율_막대와_안_겹친다()
        {
            const float w = 1440f, h = 2560f;
            Rect zoom = UiLayout.ZoomBarRect(w, h);
            Rect r = UiLayout.TutorialProgressRect(w, h);

            Assert.That(r.xMax, Is.LessThanOrEqualTo(zoom.x + 0.001f),
                "부유 띠 오른쪽은 배율 막대가 쓴다");
        }

        [Test]
        public void 창이_낮아도_전투_띠를_안_덮는다()
        {
            // 겹침이 실제로 났던 쪽 — 낮고 넓은 창이다.
            const float w = 1920f, h = 900f;
            Rect combat = UiLayout.BandRect(UiLayout.Band.Combat, w, h);
            Rect r = UiLayout.TutorialProgressRect(w, h);

            Assert.That(r.y, Is.GreaterThanOrEqualTo(combat.yMax),
                "전투 띠를 덮으면 HUD 첫 줄과 겹친다 — 이것이 09-15 육안 ④ 다");
        }

        [Test]
        public void 창_크기를_따라간다()
        {
            Rect small = UiLayout.TutorialProgressRect(720f, 1280f);
            Rect big = UiLayout.TutorialProgressRect(1440f, 2560f);

            Assert.That(big.height, Is.GreaterThan(small.height),
                "날 픽셀이면 창이 커져도 높이가 그대로다");
        }

        [Test]
        public void 폭은_음수가_안_된다()
        {
            // 배율 막대가 창보다 넓어지는 극단 — 폭이 음수면 GUILayout 이 던진다.
            Rect r = UiLayout.TutorialProgressRect(200f, 2560f);
            Assert.That(r.width, Is.GreaterThanOrEqualTo(0f));
            Assert.That(r.height, Is.GreaterThanOrEqualTo(0f));
        }
    }
}
