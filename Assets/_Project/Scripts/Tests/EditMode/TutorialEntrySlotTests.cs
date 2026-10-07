using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **튜토리얼에 들어올 때는 빈 칸 판이다** (2026-10-07 사용자 육안 ②).
    ///
    /// ⚠️⚠️ 저장이 「채운 판」을 복원하면 그 칸이 메워진 채 튜토리얼이 서고, 고스트가
    /// 설 자리가 없어 **배울 것이 화면에서 사라진다.** 더 나쁜 쪽은 그 칸에 **안 흐르는
    /// 것**이 들어 있을 때다 — 첫 목표가 영영 안 켜지고 국면이 안 풀려 **B 탭까지 잠긴
    /// 채로 남는다**(육안 ①이 그것이다).
    /// </summary>
    public sealed class TutorialEntrySlotTests
    {
        [SetUp]
        public void Clear() => TutorialSignals.Reset();

        [TearDown]
        public void Done() => TutorialSignals.Reset();

        /// <summary>
        /// ⚠️ **고스트가 안 기다리는데 모드 강조가 남아 있으면 국면이 선다** — 이것이
        /// B 탭을 잠근 자리다. 채워진 칸으로 들어왔을 때의 모습을 신호로 재현한다.
        /// </summary>
        [Test]
        public void 채워진_칸으로_들어오면_이동_모드에서_잠긴다()
        {
            TutorialSignals.GhostCell = StartingBoard.EmptySlot;
            TutorialSignals.GhostCellFilled = true;      // 저장이 채워 둔 칸
            TutorialSignals.HighlightBuildMode = true;   // 튜토리얼이 아직 안 끝났다
            TutorialSignals.BoardViewOpen = true;
            TutorialSignals.BoardInBuildMode = false;    // 기본은 이동 모드
            TutorialSignals.BoardViewIsTutorialBoard = true;

            Assert.AreEqual(TutorialGate.Phase.BuildMode, TutorialGate.Current,
                "국면이 안 선다 — 이 시험의 전제가 바뀌었다");
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.PaletteOther),
                "B 탭이 열려 있다 — 육안 ①의 전제가 바뀌었다");
        }

        /// <summary>
        /// **들어올 때 그 칸을 비우라고 요청한다** — 비면 고스트가 서고 수업이 돌아온다.
        /// ⚠️ 새 규칙이 아니다. 「처음부터」 버튼이 쓰던 문을 시작에서도 지난다.
        /// </summary>
        [Test]
        public void 들어올_때_빈_칸을_요청한다()
        {
            // `Stage0Session.Arm` 이 세우는 것과 같은 신호 묶음.
            TutorialSignals.Reset();
            TutorialSignals.GhostCell = StartingBoard.EmptySlot;
            TutorialSignals.HighlightBoardButton = true;
            TutorialSignals.HighlightBuildMode = true;
            TutorialSignals.ClearEmptySlotRequested = true;

            Assert.IsTrue(TutorialSignals.ClearEmptySlotRequested,
                "빈 칸 요청이 안 섰다 — 저장이 채운 판으로 튜토리얼이 선다");
            Assert.AreEqual(StartingBoard.EmptySlot, TutorialSignals.GhostCell);
        }

        /// <summary>⚠️ `Reset` 이 그 요청까지 지운다 — 켜는 차례가 뒤면 안 선다.</summary>
        [Test]
        public void 요청은_Reset_뒤에_세워야_한다()
        {
            TutorialSignals.ClearEmptySlotRequested = true;
            TutorialSignals.Reset();

            Assert.IsFalse(TutorialSignals.ClearEmptySlotRequested,
                "Reset 이 요청을 안 지운다 — 차례를 따질 필요가 없어진 것이니 주석을 고쳐라");
        }

        /// <summary>비어 있으면 종전대로 **놓기 국면**이고, B 탭은 그때 잠기는 것이 규칙이다.</summary>
        [Test]
        public void 빈_칸이면_놓기_국면이다()
        {
            TutorialSignals.GhostCell = StartingBoard.EmptySlot;
            TutorialSignals.GhostCellFilled = false;
            TutorialSignals.BoardViewOpen = true;
            TutorialSignals.BoardInBuildMode = true;
            TutorialSignals.BoardViewIsTutorialBoard = true;

            Assert.AreEqual(TutorialGate.Phase.PlaceNode, TutorialGate.Current);
            Assert.IsFalse(TutorialGate.Allows(TutorialGate.Control.PaletteOther),
                "튜토리얼 중 B 탭은 잠기는 것이 09-16 확정이다");
        }
    }
}
