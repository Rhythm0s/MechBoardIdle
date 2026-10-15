using MBI.UI;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **튜토리얼 어둠막 불투명도 0.75** (2026-10-15 사용자 확정 · 2차 육안
    /// 「DIM 처리의 오퍼시티를 조절 — 아래 레이어가 더 안 보이게」).
    /// </summary>
    public sealed class TutorialDimTests
    {
        [Test]
        public void 불투명도는_사용자_확정_075다()
        {
            // ⚠️ 구 0.55 는 가정이었고 사용자가 「아래가 더 안 보이게」로 고쳤다.
            //    여기가 바뀌면 T1(보드 빈 칸 둘레)·T3(「도전」 둘레)가 **같이** 바뀐다 — 둘 다 이 값을 쓴다.
            Assert.AreEqual(0.75f, TutorialDim.Color.a, 0.0001f);
        }
    }
}
