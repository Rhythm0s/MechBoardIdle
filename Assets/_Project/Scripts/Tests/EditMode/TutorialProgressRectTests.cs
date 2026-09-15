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
        /// <summary>
        /// ⚠️ **구 시험 둘을 여기서 갈았다**(2026-09-15 · 사용자 육안 7차 ②).
        ///
        /// 「부유 띠 안에 앉는다」·「배율 막대와 안 겹친다」는 **자리가 부유 띠였을 때**의
        /// 규칙이다. 그 띠를 탭 줄 여섯과 노드 버튼이 함께 쓰게 되면서 **좁은 창에서 셋이
        /// 겹쳤고**, 자리를 보드 띠로 내보냈다 — 그러면 저 둘은 **뜻이 사라진 시험**이다.
        ///
        /// 📌 **지우지 않고 갈아 끼운다** — 무엇을 지키던 시험이었는지가 함께 사라지면,
        /// 다음에 누가 자리를 되돌릴 때 같은 겹침을 다시 만든다.
        /// 지키던 것(「다른 것을 덮지 않는다」)은 아래 `좁은_창에서도_탭_줄과_안_겹친다` 가 잇는다.
        /// </summary>
        [Test]
        public void 배율_막대와_안_겹친다()
        {
            const float w = 1440f, h = 2560f;
            Rect zoom = UiLayout.ZoomBarRect(w, h);
            Rect r = UiLayout.TutorialProgressRect(w, h);

            // 배율 막대는 부유 띠에 있고 진행 줄은 보드 띠에 있으므로 이제 층이 다르다 —
            // 그래도 **겹치지 않는다**는 결론은 그대로여야 한다.
            Assert.IsFalse(r.Overlaps(zoom), "배율 막대를 덮으면 안 된다");
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
    
        [Test]
        public void 좁은_창에서도_탭_줄과_안_겹친다()
        {
            // ⚠️⚠️ **좁은 창이 이 자리를 깨뜨렸다**(2026-09-15 · 사용자 육안 7차 ②).
            //
            // 종전 자리는 **부유 띠 왼쪽**이었는데, 그 띠는 탭 줄 여섯과 노드 버튼이
            // 함께 쓴다. 창이 좁아지면 셋이 같은 폭을 다투고 — 실측(약 730px)에서
            // 튜토리얼 세 줄이 **탭 위에 그대로 겹쳐** 탭이 「전」 한 글자만 보였다.
            //
            // 📌 **띠 안에서 나누는 것으로는 못 푼다** — 탭 여섯·버튼 여섯은 이미
            // 띠를 꽉 쓰기로 정한 값이다. 그래서 **띠 밖**인지를 여기서 못 박는다.
            foreach (float w in new[] { 480f, 730f, 1080f, 1440f, 1920f })
            foreach (float h in new[] { 800f, 960f, 1280f, 2560f })
            {
                Rect prog = UiLayout.TutorialProgressRect(w, h);
                Rect tabs = UiLayout.CategoryTabRect(w, h);
                Rect palette = UiLayout.PaletteRect(w, h);

                Assert.IsFalse(prog.Overlaps(tabs),
                    $"{w}x{h} — 진행 줄이 카테고리 탭을 덮는다");
                Assert.IsFalse(prog.Overlaps(palette),
                    $"{w}x{h} — 진행 줄이 노드 버튼을 덮는다");
            }
        }

        [Test]
        public void 보드_띠_안에_있다()
        {
            // 띠 밖으로 내보낸 자리는 **보드 띠**다 — 목표 두 줄은 보드에서 할 일을
            // 말하므로 보드 안이 제자리이고, 모드 판(오른쪽 아래)의 반대 구석이다.
            foreach (float w in new[] { 480f, 730f, 1440f })
            foreach (float h in new[] { 800f, 1280f, 2560f })
            {
                Rect prog = UiLayout.TutorialProgressRect(w, h);
                Rect board = UiLayout.BandRect(UiLayout.Band.Board, w, h);

                Assert.GreaterOrEqual(prog.y, board.y - 0.01f, $"{w}x{h} — 보드 띠 위로 나갔다");
                Assert.LessOrEqual(prog.yMax, board.yMax + 0.01f, $"{w}x{h} — 보드 띠 아래로 나갔다");
                Assert.LessOrEqual(prog.xMax, board.xMax + 0.01f, $"{w}x{h} — 오른쪽으로 나갔다");
            }
        }
}
}
