using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **튜토리얼에서 보드는 설치(조립) 모드로 열린다** (2026-10-15 사용자 확정 #135).
    /// </summary>
    public sealed class BoardOpenModeRuleTests
    {
        [Test]
        public void 튜토리얼_판에서는_조립_모드로_열린다()
        {
            Assert.IsTrue(BoardOpenModeRule.OpensInBuildMode(isTutorialStage: true));
        }

        [Test]
        public void 튜토리얼_밖에서는_종전대로_이동_모드다()
        {
            // ⚠️ 09-28 사용자 확정 「보드 기본 모드는 이동」 — 튜토리얼 밖은 안 바꾼다.
            Assert.IsFalse(BoardOpenModeRule.OpensInBuildMode(isTutorialStage: false));
        }

        [Test]
        public void 조립_모드면_조립_모드로_바꿔라_국면이_안_선다()
        {
            // 고스트가 떠 있고(벨트 깔 칸) 조립 모드로 열렸으면 바로 「놓기」 국면이다 —
            // 「조립 모드로 바꿔라」 한 계단이 저절로 사라진다(게이트가 모드를 입력으로 받는다).
            Assert.AreEqual(TutorialGate.Phase.PlaceNode,
                TutorialGate.Resolve(urgeEnterBoard: false, urgeBuildMode: true,
                                     ghostWaiting: true, inBuildMode: true));
            // 이동 모드로 되돌리면 그 국면이 다시 서서 안내한다.
            Assert.AreEqual(TutorialGate.Phase.BuildMode,
                TutorialGate.Resolve(urgeEnterBoard: false, urgeBuildMode: true,
                                     ghostWaiting: true, inBuildMode: false));
        }
    }
}
