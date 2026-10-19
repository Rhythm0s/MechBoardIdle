using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>CID_214 — 튜토리얼 시야 이동은 조립 카메라가 선 뒤에 한 번(A · B 강화 · S5 폭발탄 같은 규칙).</summary>
    public sealed class BoardFocusRuleTests
    {
        [Test]
        public void MidSlide_NotSettled() =>
            Assert.IsFalse(BoardFocusRule.CameraSettled(new Vector2(0f, -5f), new Vector2(0f, -20f), 6f, 9f),
                "들어간 첫 프레임 = 전투 자리 근처 · 이때 재면 시야가 엉뚱하게 밀린다(8133 S6 B 마운트 화면 밖)");

        [Test]
        public void AtTarget_Settled() =>
            Assert.IsTrue(BoardFocusRule.CameraSettled(new Vector2(0f, -19.98f), new Vector2(0f, -20f), 8.99f, 9f));

        [Test]
        public void SizeStillLerping_NotSettled() =>
            Assert.IsFalse(BoardFocusRule.CameraSettled(new Vector2(0f, -20f), new Vector2(0f, -20f), 8.5f, 9f));

        [Test]
        public void FocusOnce_OnlyAfterSettled()
        {
            Assert.IsFalse(BoardFocusRule.ShouldFocus(false, true, false), "미끄러지는 중엔 기다린다");
            Assert.IsTrue(BoardFocusRule.ShouldFocus(false, true, true));
            Assert.IsFalse(BoardFocusRule.ShouldFocus(true, true, true), "한 번 옮긴 뒤엔 손으로 끈 시야를 안 빼앗는다");
            Assert.IsFalse(BoardFocusRule.ShouldFocus(false, false, true), "전투 화면이면 안 옮긴다");
        }
    }
}
