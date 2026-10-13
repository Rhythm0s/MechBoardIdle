using MBI.Core;
using MBI.UI;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **파밍 파괴 안내 띠** (2026-10-13 · #113 · 빌드 실측에서 난 자리).
    ///
    /// 파밍에서 로봇이 부서지면 시뮬이 `LoseDead` 를 낸다(맞는 거동 — `Endless` 는
    /// 전멸·제한시간만 멈춘다). 그래서 **「패배 — 로봇 파괴」 결과 배너가 파밍 화면에
    /// 그대로 떴고**, 「다시 도전」·「스테이지로」까지 붙어 있었다. 파밍에는 승패가 없고
    /// 고를 것도 없다 — 그 자리에는 **무슨 일이 났고 무엇이 일어날 것인가**만 적는다.
    /// </summary>
    public sealed class FarmBrokenNoticeTests
    {
        /// <summary>실제로 쓰는 창 몇 — 배너 시험과 같은 표본.</summary>
        private static readonly Vector2[] Screens =
        {
            new Vector2(1440f, 2560f),   // 기준 캔버스
            new Vector2(470f, 834f),
            new Vector2(360f, 640f),
            new Vector2(280f, 498f),
        };

        [Test]
        public void 결과_배너와_폭과_왼끝이_같다()
        {
            // 📌 둘은 **같은 자리에 번갈아** 뜬다 — 폭이 다르면 화면이 들썩인다.
            foreach (Vector2 s in Screens)
            {
                float line = 40f * UiLayout.Scale(s.y);
                Rect notice = UiLayout.FarmBrokenNoticeRect(s.x, s.y, line);
                Rect banner = UiLayout.ResultBannerRect(s.x, s.y, line);

                Assert.AreEqual(banner.width, notice.width, 0.01f, "폭이 다르다 " + s);
                Assert.AreEqual(banner.x, notice.x, 0.01f, "왼끝이 다르다 " + s);
                Assert.AreEqual(banner.y, notice.y, 0.01f, "윗변이 다르다 " + s);
            }
        }

        [Test]
        public void 단추_줄만큼_낮다()
        {
            // ⚠️ 고를 것이 없는 안내다 — 단추 높이를 남겨 두면 **빈 칸**이 생긴다.
            foreach (Vector2 s in Screens)
            {
                float line = 40f * UiLayout.Scale(s.y);
                Rect notice = UiLayout.FarmBrokenNoticeRect(s.x, s.y, line);
                Rect banner = UiLayout.ResultBannerRect(s.x, s.y, line);

                Assert.Less(notice.height, banner.height, "안내가 배너보다 낮아야 한다 " + s);
            }
        }

        [Test]
        public void 글자_칸이_띠_안에_든다()
        {
            foreach (Vector2 s in Screens)
            {
                float line = 40f * UiLayout.Scale(s.y);
                Rect box = UiLayout.FarmBrokenNoticeRect(s.x, s.y, line);
                Rect text = UiLayout.FarmBrokenNoticeTextRect(s.x, s.y, line);

                Assert.GreaterOrEqual(text.x, box.x - 0.01f, "글자가 왼쪽으로 샌다 " + s);
                Assert.LessOrEqual(text.xMax, box.xMax + 0.01f, "글자가 오른쪽으로 샌다 " + s);
                Assert.GreaterOrEqual(text.y, box.y - 0.01f, "글자가 위로 샌다 " + s);
                Assert.LessOrEqual(text.yMax, box.yMax + 0.01f, "글자가 아래로 샌다 " + s);
                Assert.Greater(text.width, 0f, "글자 칸이 비었다 " + s);
            }
        }

        [Test]
        public void 글자_칸이_줄이는_기준_폭을_안_넘는다()
        {
            // ⚠️⚠️ **여기가 잘림을 막는 자리다.** 화면은 `ResultBannerInnerWidth` 에
            //    맞춰 글자를 줄인 뒤 이 칸에 그린다 — 칸이 그보다 좁으면 줄여 놓고도 잘린다.
            foreach (Vector2 s in Screens)
            {
                float line = 40f * UiLayout.Scale(s.y);
                Rect text = UiLayout.FarmBrokenNoticeTextRect(s.x, s.y, line);
                float innerW = UiLayout.ResultBannerInnerWidth(s.x, s.y);

                Assert.AreEqual(innerW, text.width, 0.01f,
                    "줄이는 기준 폭과 그리는 칸이 다르다 — 한 값이 두 곳에 산다 " + s);
            }
        }

        [Test]
        public void 돌아갈_판을_규칙이_정한다()
        {
            // 화면 문구가 쓰는 수 — 「파괴 — S4 파밍으로 돌아간다」의 4 다.
            Assert.AreEqual(4, FarmDefeatRule.NextStageNumber(5));
            Assert.AreEqual(1, FarmDefeatRule.NextStageNumber(2));
            Assert.AreEqual(1, FarmDefeatRule.NextStageNumber(1), "첫 판에는 앞 판이 없다");
            Assert.AreEqual(0, FarmDefeatRule.NextStageNumber(0), "번호를 못 읽으면 0");
        }
    }
}
