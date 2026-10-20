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

        /// <summary>#252 — 설치 걸음 전부(튜토리얼 판 · T4 · 고스트 칸 · S5 폭발탄 걸음)에서 설치 모드.</summary>
        [Test]
        public void PlaceSteps_AllOpenInBuildMode()
        {
            Assert.IsTrue(BoardOpenModeRule.PlaceStepActive(true, false, false, false), "T1");
            Assert.IsTrue(BoardOpenModeRule.PlaceStepActive(false, true, false, false), "T4");
            Assert.IsTrue(BoardOpenModeRule.PlaceStepActive(false, false, true, false), "고스트 칸(T3 등)");
            Assert.IsTrue(BoardOpenModeRule.PlaceStepActive(false, false, false, true), "S5 폭발탄 걸음");
            Assert.IsFalse(BoardOpenModeRule.PlaceStepActive(false, false, false, false), "걸음 없음 = 종전 이동 모드");
        }

        /// <summary>#252 — 이미 설치 모드면 「바꿔라」 국면은 안 서고 바로 놓기 국면(자동 통과).</summary>
        [Test]
        public void BuildModePhase_SkippedWhenAlreadyBuild()
        {
            Assert.AreEqual(TutorialGate.Phase.PlaceNode,
                TutorialGate.Resolve(false, false, false, false, false, false, false, true, true));
            Assert.AreEqual(TutorialGate.Phase.BuildMode,
                TutorialGate.Resolve(false, false, false, false, false, false, false, true, false));
            string bc = System.IO.File.ReadAllText("Assets/_Project/Scripts/Logistics/BoardController.cs");
            StringAssert.Contains("if (boardOpen && placeStep && (!_wasBoardOpenForMode || !_wasPlaceStepForMode))", bc);
        }
    }
}
