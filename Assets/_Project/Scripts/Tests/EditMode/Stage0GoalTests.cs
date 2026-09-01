using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 스테이지 0 종료 판정 — **전투가 없는 첫 스테이지**(260901_V05 §3층).
    ///
    /// 목표는 하나다: 벨트를 이으면 물건이 만들어진다는 것을 안다.
    /// 종료 = 빈 칸에 노드를 놓았고 + 마운트가 가득 찼다.
    /// </summary>
    public sealed class Stage0GoalTests
    {
        // ---- 조건이 둘이다 ----

        [Test]
        public void StartsIncomplete()
        {
            var goal = new Stage0Goal();

            Assert.IsFalse(goal.NodePlaced);
            Assert.IsFalse(goal.MountFilled);
            Assert.IsFalse(goal.IsComplete);
        }

        /// <summary>
        /// **마운트만 차서는 안 끝난다.** 이것이 조건을 둘로 둔 이유다 —
        /// 관통 4노드가 이미 정상 작동 중이라, 아무것도 안 하고 기다려도 마운트는 찬다.
        /// 그 하나만 걸면 배우는 것이 없다.
        ///
        /// ⚠️ 놓기 전의 만충은 **세지도 않는다**(순서 규칙). 세어 두면 플레이어가 노드를 놓는
        /// 순간 곧바로 끝나 「쌓인다」를 한 번도 못 본다 — 브라우저 실측에서 실제로 그랬다.
        /// </summary>
        [Test]
        public void MountBeforePlacement_IsNotCounted()
        {
            var goal = new Stage0Goal();

            for (int i = 0; i < 100; i++) goal.Observe(emptySlotFilled: false, mountIsFull: true);

            Assert.IsFalse(goal.MountFilled, "놓기 전의 만충은 세지 않는다");
            Assert.IsFalse(goal.IsComplete, "기다리기만 해서는 통과하지 못한다");
        }

        /// <summary>
        /// **놓은 뒤에 차야 끝난다.** 놓기 전에 아무리 차 있었어도 다시 차야 한다 —
        /// 그 기다림이 「이어지면 쌓인다」를 보는 시간이다(약 8초).
        /// </summary>
        [Test]
        public void MountAfterPlacement_Finishes()
        {
            var goal = new Stage0Goal();

            goal.Observe(emptySlotFilled: false, mountIsFull: true); // 놓기 전 — 안 센다
            goal.Observe(emptySlotFilled: true, mountIsFull: false); // 놓는다
            Assert.IsFalse(goal.IsComplete, "놓은 직후에는 아직 아니다");

            goal.Observe(emptySlotFilled: true, mountIsFull: true);  // 다시 찬다
            Assert.IsTrue(goal.IsComplete);
        }

        /// <summary>놓기만 하고 아직 안 찼으면 끝나지 않는다 — 「이어지면 쌓인다」를 봐야 한다.</summary>
        [Test]
        public void PlacementAlone_DoesNotFinish()
        {
            var goal = new Stage0Goal();

            goal.Observe(emptySlotFilled: true, mountIsFull: false);

            Assert.IsTrue(goal.NodePlaced);
            Assert.IsFalse(goal.IsComplete);
        }

        /// <summary>같은 프레임에 둘 다 서도 성립한다 — 놓는 순간 이미 차 있던 경우.</summary>
        [Test]
        public void BothInOneFrame_Finishes()
        {
            var goal = new Stage0Goal();

            goal.Observe(emptySlotFilled: true, mountIsFull: true);

            Assert.IsTrue(goal.IsComplete);
        }

        // ---- 걸쇠 ----

        /// <summary>
        /// **놓았다는 사실은 안 풀린다.** 놓은 뒤 마운트가 차기까지 약 8초가 걸리는데,
        /// 그 사이 이 값이 내려가면 조건이 영영 동시에 서지 않는다.
        /// </summary>
        [Test]
        public void Placement_Latches()
        {
            var goal = new Stage0Goal();

            goal.Observe(emptySlotFilled: true, mountIsFull: false);
            goal.Observe(emptySlotFilled: false, mountIsFull: false); // 관측이 끊겨도

            Assert.IsTrue(goal.NodePlaced, "한 번 놓았으면 놓은 것이다");
        }

        /// <summary>
        /// **만충도 걸쇠다.** 마운트는 찬 다음 프레임에 소비로 다시 내려간다 —
        /// 걸어 두지 않으면 그 한 프레임을 놓쳤는지 여부에 통과가 걸린다.
        /// </summary>
        [Test]
        public void Fullness_Latches()
        {
            var goal = new Stage0Goal();

            goal.Observe(emptySlotFilled: true, mountIsFull: true);
            goal.Observe(emptySlotFilled: true, mountIsFull: false); // 곧바로 소비돼도

            Assert.IsTrue(goal.MountFilled, "가득 찬 적이 있으면 본 것이다");
        }

        // ---- 다시 시작 ----

        [Test]
        public void Reset_ClearsBothLatches()
        {
            var goal = new Stage0Goal();
            goal.Observe(emptySlotFilled: true, mountIsFull: true);

            goal.Reset();

            Assert.IsFalse(goal.NodePlaced);
            Assert.IsFalse(goal.MountFilled);
            Assert.IsFalse(goal.IsComplete);
        }

        // ---- 신호 채널 ----

        /// <summary>
        /// 튜토리얼 신호는 **꺼진 상태가 기본**이다. 그래야 스테이지 0을 떼어냈을 때
        /// 보드가 평소대로 그린다(9월 4일 되돌림 지점).
        /// </summary>
        [Test]
        public void Signals_AreOffAfterReset()
        {
            TutorialSignals.GhostCell = new UnityEngine.Vector2Int(3, 8);
            TutorialSignals.HighlightBoardButton = true;
            TutorialSignals.GhostCellFilled = true;

            TutorialSignals.Reset();

            Assert.IsNull(TutorialSignals.GhostCell, "고스트가 꺼진다");
            Assert.IsFalse(TutorialSignals.HighlightBoardButton);
            Assert.IsFalse(TutorialSignals.GhostCellFilled);
        }

        /// <summary>고스트가 가리키는 곳은 **시작 보드가 비워 둔 그 칸**이다. 둘이 갈리면 안 된다.</summary>
        [Test]
        public void GhostPointsAtTheEmptySlot()
        {
            TutorialSignals.Reset();
            TutorialSignals.GhostCell = StartingBoard.EmptySlot;

            Assert.AreEqual(StartingBoard.EmptySlot, TutorialSignals.GhostCell.Value);
            Assert.AreEqual(StartingBoard.FillsEmptySlot.cell, TutorialSignals.GhostCell.Value);

            TutorialSignals.Reset();
        }
    }
}
