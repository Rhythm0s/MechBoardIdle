using MBI.UI;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **결과 단추 글자 = 「▼ 조립」 단추 규격** (2026-10-15 사용자 확정 #136 · 8127 재확인).
    /// 「도전」(스킨 기본 글자) 쪽을 따라가 결과 단추가 여전히 아주 작던 자리.
    /// </summary>
    public sealed class ResultButtonSpecTests
    {
        [Test]
        public void 글자는_조립_칸_높이의_0_28_을_사다리에_맞춘_값이다()
        {
            Rect enter = UiLayout.EnterBoardRect(576f, 1024f);
            int expect = KoreanFont.Snap(Mathf.Max(12, Mathf.RoundToInt(enter.height * 0.28f)));
            Assert.AreEqual(expect, UiLayout.EnterBoardFontSize(576f, 1024f));
        }

        [Test]
        public void 보통_세로_창에서는_사다리_바닥보다_크다()
        {
            // 바닥(16)에 머물던 것이 사용자가 본 「아주 작다」였다.
            Assert.Greater(UiLayout.EnterBoardFontSize(1440f, 2560f), KoreanFont.Ladder[0]);
        }

        [Test]
        public void 아주_작은_창에서도_바닥_밑으로_안_내려간다()
        {
            Assert.GreaterOrEqual(UiLayout.EnterBoardFontSize(462f, 410f), KoreanFont.Ladder[0]);
        }
    }
}
