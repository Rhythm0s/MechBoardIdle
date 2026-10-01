using System.Collections.Generic;
using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **튜토리얼 첫 목표는 「흐르는가」로 센다** (2026-10-02 · `260928_W01` 3장 이행).
    ///
    /// ⚠️ 종전에는 「그 칸이 채워졌는가」였고, 그래서 벨트 대신 변환기를 놓아도 첫 목표가
    /// 켜진 뒤 **둘째 목표에서 영영 막혔다**(10-02 재현). 그 틈을 막는 자리다.
    /// </summary>
    public sealed class TutorialLinkTests
    {
        /// <summary>
        /// **고스트 칸에 변환기를 놓으면 첫 목표가 안 켜진다.**
        /// 칸은 채워져도 줄이 끊겨 있어 생산이 0 이다 — 화면에서 본 「가동률 0%」가 이 값이다.
        /// </summary>
        [Test]
        public void 칸만_채우면_첫_목표가_안_켜진다()
        {
            Assert.IsFalse(TutorialLinkRule.LinkIsLive(0f), "생산 0 인데 이어졌다고 읽었다");

            var goal = new Stage0Goal();
            // 칸은 채웠지만(화면의 막은 걷힌다) 흐르지는 않는다.
            for (int i = 0; i < 10; i++)
                goal.Observe(TutorialLinkRule.LinkIsLive(0f), mountIsFull: false);

            Assert.IsFalse(goal.SlotFilled, "칸만 채웠는데 첫 목표가 켜졌다");
            Assert.IsFalse(goal.IsComplete, "튜토리얼이 끝나 버렸다");
        }

        /// <summary>**벨트로 이으면 켜진다** — 물건이 흐르기 시작하는 그 순간이다.</summary>
        [Test]
        public void 이으면_첫_목표가_켜진다()
        {
            Assert.IsTrue(TutorialLinkRule.LinkIsLive(4f), "흐르는데 안 이어졌다고 읽었다");

            var goal = new Stage0Goal();
            goal.Observe(TutorialLinkRule.LinkIsLive(4f), mountIsFull: false);

            Assert.IsTrue(goal.SlotFilled, "흐르는데 첫 목표가 안 켜졌다");
        }

        /// <summary>
        /// ⚠️ **순서는 그대로다** — 이은 **뒤**에 차야 둘째가 켜진다. 먼저 차 있어도
        /// 안 센다(그 순서가 이 스테이지의 수업이다).
        /// </summary>
        [Test]
        public void 잇기_전에_차_있어도_안_센다()
        {
            var goal = new Stage0Goal();
            goal.Observe(TutorialLinkRule.LinkIsLive(0f), mountIsFull: true);
            Assert.IsFalse(goal.MountFilled, "잇기 전에 둘째 목표가 켜졌다");

            goal.Observe(TutorialLinkRule.LinkIsLive(4f), mountIsFull: true);
            Assert.IsTrue(goal.SlotFilled);
            Assert.IsTrue(goal.MountFilled, "이은 뒤에도 둘째가 안 켜졌다");
        }

        /// <summary>
        /// ⚠️ **없는 값을 참으로 읽지 않는다** — 음수는 나올 자리가 없지만 나온다면
        /// 그것은 「돈다」가 아니다.
        /// </summary>
        [Test]
        public void 음수는_흐르는_것이_아니다()
        {
            Assert.IsFalse(TutorialLinkRule.LinkIsLive(-1f));
        }

        /// <summary>
        /// **막다르지 않다** — 잘못 놓은 변환기를 지울 수 있다. 제거는 튜토리얼에
        /// 잠겨 있지 않고, 막는 것은 **코어뿐**이다.
        ///
        /// ⚠️ 이 시험이 없으면 「첫 목표를 엄격하게」가 곧 「가둔다」가 될 수 있다.
        /// </summary>
        [Test]
        public void 잘못_놓은_것을_지울_수_있다()
        {
            var cells = new List<Vector2Int> { new Vector2Int(5, 9) };
            List<Vector2Int> targets = RemovalRules.Removable(cells, _ => false);

            Assert.AreEqual(1, targets.Count, "튜토리얼 칸을 못 지운다 — 막다른 길이 된다");
            Assert.AreEqual(new Vector2Int(5, 9), targets[0]);
        }

        /// <summary>⚠️ **코어는 여전히 못 지운다** — 넓게 풀리지 않았다는 대조.</summary>
        [Test]
        public void 코어는_그래도_못_지운다()
        {
            var cells = new List<Vector2Int> { new Vector2Int(5, 8) };
            List<Vector2Int> targets = RemovalRules.Removable(cells, c => c == new Vector2Int(5, 8));

            Assert.AreEqual(0, targets.Count, "코어가 지워진다");
        }
    }
}
