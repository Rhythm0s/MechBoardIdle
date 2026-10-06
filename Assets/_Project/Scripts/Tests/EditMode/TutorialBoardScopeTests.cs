using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **튜토리얼 잠금은 제 판에만 걸린다** (2026-10-06 사용자 육안 결함).
    ///
    /// ⚠️⚠️ 고스트 좌표에는 **주인이 없다.** 보드는 둘인데 좌표는 하나라, 보드가
    /// 「지금 보는 판」에 그 좌표를 걸었다. 그래서 튜토리얼이 안 끝난 채 **B 탭을 열면
    /// A 의 고스트가 B 판 위에 걸려 막이 덮이고 국면이 「놓기」에 머물러 보드가
    /// 통째로 잠겼다** — 사용자가 촬영 중에 본 그림이 그것이다.
    /// </summary>
    public sealed class TutorialBoardScopeTests
    {
        [SetUp]
        public void Clear() => TutorialSignals.Reset();

        [TearDown]
        public void Done() => TutorialSignals.Reset();

        /// <summary>제 판에서는 종전대로 **놓기 국면**에 머문다 — 좁히다 죽이지 않았다.</summary>
        [Test]
        public void 제_판에서는_놓기_국면이다()
        {
            TutorialSignals.GhostCell = new Vector2Int(6, 5);
            TutorialSignals.BoardViewOpen = true;
            TutorialSignals.BoardInBuildMode = true;
            TutorialSignals.BoardViewIsTutorialBoard = true;

            Assert.AreEqual(TutorialGate.Phase.PlaceNode, TutorialGate.Current);
        }

        /// <summary>**다른 판에서는 안 잠근다** — 이것이 이번에 고친 자리다.</summary>
        [Test]
        public void 다른_판에서는_안_잠근다()
        {
            TutorialSignals.GhostCell = new Vector2Int(6, 5);
            TutorialSignals.BoardViewOpen = true;
            TutorialSignals.BoardInBuildMode = true;
            TutorialSignals.BoardViewIsTutorialBoard = false;   // B 탭

            Assert.AreNotEqual(TutorialGate.Phase.PlaceNode, TutorialGate.Current,
                "다른 판인데 놓기 국면에 머문다 — 보드가 잠긴다");
        }

        /// <summary>
        /// ⚠️ **기본값은 참이다** — 보드가 아직 한 번도 안 알렸을 때 거짓으로 두면
        /// 튜토리얼이 **시작부터** 안 걸린다. 없는 것을 「아니다」로 읽지 않는다.
        /// </summary>
        [Test]
        public void 아직_안_알렸으면_제_판으로_본다()
        {
            Assert.IsTrue(TutorialSignals.BoardViewIsTutorialBoard,
                "Reset 뒤 기본값이 거짓이다 — 튜토리얼이 시작부터 안 걸린다");
        }

        /// <summary>
        /// ⚠️ **모드 국면은 그대로다** — 제 판이고 이동 모드면 여전히 「조립 모드로」다.
        /// 좁히기가 그 앞 국면까지 건드리지 않았는지 본다.
        /// </summary>
        [Test]
        public void 제_판에서_이동_모드면_모드_국면이다()
        {
            TutorialSignals.GhostCell = new Vector2Int(6, 5);
            TutorialSignals.BoardViewOpen = true;
            TutorialSignals.BoardInBuildMode = false;
            TutorialSignals.BoardViewIsTutorialBoard = true;

            Assert.AreEqual(TutorialGate.Phase.BuildMode, TutorialGate.Current);
        }

        /// <summary>
        /// 채우고 나면 제 판에서도 안 기다린다 — 좁히기가 종전 걷힘을 안 깼다.
        /// </summary>
        [Test]
        public void 채우면_제_판에서도_안_기다린다()
        {
            TutorialSignals.GhostCell = new Vector2Int(6, 5);
            TutorialSignals.GhostCellFilled = true;
            TutorialSignals.BoardViewOpen = true;
            TutorialSignals.BoardInBuildMode = true;
            TutorialSignals.BoardViewIsTutorialBoard = true;

            Assert.AreNotEqual(TutorialGate.Phase.PlaceNode, TutorialGate.Current);
        }
    }
}
