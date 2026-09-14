using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 노드 상태 표식 (2026-09-11 신설 · 플랜 §71-33 ③).
    ///
    /// **무엇을 지키는가.** 한 칸에 **하나만** 뜨는가, 그리고 **원인이 결과를 이기는가**다.
    /// 전력이 모자라면 여러 칸이 한꺼번에 느려지는데 칸마다 「느림」을 띄우면
    /// **원인 하나가 결과 여럿으로 보인다.**
    /// </summary>
    public sealed class NodeStatusIconTests
    {
        [Test]
        public void 전력이_느림을_이긴다()
        {
            // 전력 부족으로 깎여 돌아가는 칸 — 고칠 것은 이 노드가 아니라 전력이다.
            Assert.AreEqual(NodeIcon.PowerShort,
                NodeStatusIcon.Of(powerShort: true, notConnected: false, ratio: 0.5f));

            // 멈춰 있어도 마찬가지다.
            Assert.AreEqual(NodeIcon.PowerShort,
                NodeStatusIcon.Of(powerShort: true, notConnected: true, ratio: 0f));
        }

        [Test]
        public void 미연결이_정지를_이긴다()
        {
            // 끊긴 줄은 산출률이 0 이라 「정지」로도 읽히지만 고칠 것은 **줄**이다.
            Assert.AreEqual(NodeIcon.NotConnected,
                NodeStatusIcon.Of(false, notConnected: true, ratio: 0f));
        }

        /// <summary>
        /// ⚠️ **뒤집은 단언**(2026-09-15 사용자 확정 · §72-51 · UI 문서 3-4-1).
        ///
        /// 구 시험은 「표식은 밝기와 같은 눈금을 쓴다」였다 — 산출률이 낮으면 `Stopped`·`Slow`
        /// 표식이 떴다. 그런데 문서가 정한 **원인은 둘**이다: 전력 부족 · 미연결.
        /// 정지·감속은 원인이 아니라 **결과**이고, 그 결과는 **밝기가 이미 말한다.**
        ///
        /// 표식까지 같은 말을 하면 칸마다 아이콘이 하나씩 붙어 **원인 둘이 묻힌다** —
        /// 표식은 **고칠 수 있는 것**을 가리킬 때만 값이 있다.
        ///
        /// ⚠️ **밝기는 그대로다** — 아래 두 줄이 그것을 지킨다. 걷은 것은 표식뿐이다.
        /// </summary>
        [Test]
        public void 정지와_감속은_표식을_안_쓴다_밝기가_말한다()
        {
            Assert.AreEqual(NodeIcon.None, NodeStatusIcon.Of(false, false, 0f), "정지 → 표식 없음");
            Assert.AreEqual(NodeIcon.None, NodeStatusIcon.Of(false, false, 0.5f), "감속 → 표식 없음");
            Assert.AreEqual(NodeIcon.None, NodeStatusIcon.Of(false, false, 0.998f));

            // **밝기는 여전히 눈금을 쓴다** — 이쪽이 걷히면 결과가 화면에서 통째로 사라진다.
            Assert.AreEqual(NodeStatusTint.Stopped, NodeStatusTint.Of(0f));
            Assert.AreEqual(NodeStatusTint.Slow, NodeStatusTint.Of(0.5f));
        }

        /// <summary>
        /// **정상에는 안 뜬다**(⚠️ 가정). 시작 보드만 117칸이라 정상에도 얹으면
        /// 화면이 표식으로 덮이고 정작 봐야 할 것이 그 안에 묻힌다.
        /// </summary>
        [Test]
        public void 정상_칸에는_안_뜬다()
        {
            Assert.IsFalse(NodeStatusIcon.ShowWhenNormal, "⚠️ 가정 — 규격 문서에 절이 없다");
            Assert.AreEqual(NodeIcon.None, NodeStatusIcon.Of(false, false, 1f));
            Assert.AreEqual(NodeIcon.None, NodeStatusIcon.Of(false, false, 2f),
                "초과 산출도 정상이다 — 4번째 단계는 없다");
        }
    }
}
