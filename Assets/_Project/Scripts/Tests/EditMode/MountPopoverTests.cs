using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **마운트 팝오버** (사용자 확정 #171 · 2026-10-19) — 마운트 칸 판정 · 글자 · 단추 · 보드 입력 배선.
    /// </summary>
    public sealed class MountPopoverTests
    {
        [Test]
        public void A_마운트_칸은_x1_y0부터_3과_포트_1_4다()
        {
            for (int y = 0; y < 4; y++)
                Assert.IsTrue(MountPopover.IsSlotCell(new Vector2Int(1, y), MountOwner.RobotA), $"(1,{y})");
            Assert.IsTrue(MountPopover.IsPortCell(new Vector2Int(1, 4), MountOwner.RobotA));
            Assert.IsTrue(MountPopover.IsMountCell(new Vector2Int(1, 4), MountOwner.RobotA));

            Assert.IsFalse(MountPopover.IsMountCell(new Vector2Int(1, 5), MountOwner.RobotA));
            Assert.IsFalse(MountPopover.IsMountCell(new Vector2Int(0, 0), MountOwner.RobotA));
            Assert.IsFalse(MountPopover.IsMountCell(new Vector2Int(2, 3), MountOwner.RobotA));
            // 판 주인이 다르면 아니다 — B 판에서 (1,0) 은 마운트 칸이 아니다.
            Assert.IsFalse(MountPopover.IsMountCell(new Vector2Int(1, 0), MountOwner.RobotB));
        }

        [Test]
        public void B_마운트_칸은_x3_x8_y10부터_13과_포트_둘이다()
        {
            for (int y = 10; y < 14; y++)
            {
                Assert.IsTrue(MountPopover.IsSlotCell(new Vector2Int(3, y), MountOwner.RobotB), $"(3,{y})");
                Assert.IsTrue(MountPopover.IsSlotCell(new Vector2Int(8, y), MountOwner.RobotB), $"(8,{y})");
            }
            Assert.IsTrue(MountPopover.IsPortCell(new Vector2Int(2, 10), MountOwner.RobotB));
            Assert.IsTrue(MountPopover.IsPortCell(new Vector2Int(9, 10), MountOwner.RobotB));
            Assert.IsFalse(MountPopover.IsMountCell(new Vector2Int(3, 9), MountOwner.RobotB));
            Assert.IsFalse(MountPopover.IsMountCell(new Vector2Int(5, 11), MountOwner.RobotB));
            Assert.IsFalse(MountPopover.IsMountCell(new Vector2Int(1, 4), MountOwner.RobotB));
        }

        [Test]
        public void 묶음_칸은_그림이_서는_칸과_같다_MountDisplay_에서_읽는다()
        {
            foreach (MountOwner owner in new[] { MountOwner.RobotA, MountOwner.RobotB })
                foreach (MountPort p in PartLayout.MountPortsFor(owner))
                    for (int i = 0; i < MountDisplay.SlotsPerPort; i++)
                    {
                        var c = new Vector2Int(MountDisplay.GroupColumn(p.cell, p.face, owner),
                                               MountDisplay.SlotRowOf(owner, i));
                        Assert.IsTrue(MountPopover.IsSlotCell(c, owner), $"{owner} {c}");
                    }
        }

        [Test]
        public void 묶음_칸은_마스크_밖이고_포트_칸은_안이다()
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero, PartLayout.BuildMask());
            for (int y = 0; y < 4; y++) Assert.IsFalse(g.IsInside(new Vector2Int(1, y)), "A 슬롯 칸은 마스크 밖");
            Assert.IsTrue(g.IsInside(new Vector2Int(1, 4)), "A 포트 칸은 팔R 안");
        }

        [Test]
        public void 글자_제목_적재_재료_단추()
        {
            Assert.AreEqual("마운트 A", MountPopover.Title(MountOwner.RobotA));
            Assert.AreEqual("마운트 B", MountPopover.Title(MountOwner.RobotB));
            Assert.AreEqual("적재 39/40 · 표준·폭발", MountPopover.LoadLine(39.6f, 40f, "표준·폭발"));
            Assert.AreEqual("적재 0/40 · 비어 있음", MountPopover.LoadLine(0f, 40f, ""));
            Assert.AreEqual("강화 재료 63/100", MountPopover.MaterialLine(63d, 100d));
            Assert.AreEqual(MountEnhanceRule.ButtonLabel, MountPopover.ButtonLabel(false, 1.45f));
            Assert.AreEqual(MountEnhanceRule.AppliedLabel(1.45f), MountPopover.ButtonLabel(true, 1.45f));
        }

        [Test]
        public void 단추는_고칠_수_있는_층에서_재료가_차고_안_켰을_때만_눌린다()
        {
            Assert.IsTrue(MountPopover.ButtonEnabled(true, false, 100d, 100d));
            Assert.IsFalse(MountPopover.ButtonEnabled(true, false, 99d, 100d), "재료 모자람");
            Assert.IsFalse(MountPopover.ButtonEnabled(true, true, 200d, 100d), "이미 켬");
            Assert.IsFalse(MountPopover.ButtonEnabled(false, false, 100d, 100d), "도전 층 = 보기만(#148 결)");
            Assert.IsFalse(MountPopover.ButtonEnabled(true, false, 100d, 0d), "비용 0 = 안 실렸다");
        }

        [Test]
        public void 러너가_실은_값을_주인별로_돌려준다()
        {
            MountPopover.Publish(MountOwner.RobotA, 12f, 40f, "표준");
            MountPopover.Publish(MountOwner.RobotB, 3f, 40f, "드론");
            Assert.AreEqual(12f, MountPopover.LoadOf(MountOwner.RobotA));
            Assert.AreEqual("드론", MountPopover.KindsOf(MountOwner.RobotB));
            Assert.AreEqual(40f, MountPopover.CapacityOf(MountOwner.RobotB));
        }

        /// <summary>
        /// 보드 입력 배선 — 촬영 동선(벨트 드래그 · 노드 놓기 · 팝오버 닫기)을 건드리므로 자리를 고정한다.
        /// </summary>
        [Test]
        public void 보드_입력_배선_소스_고정()
        {
            string bc = System.IO.File.ReadAllText("Assets/_Project/Scripts/Logistics/BoardController.cs");
            // 마스크 밖 칸 = 마운트 묶음이면 연다 · 아니면 종전대로 돌아선다(드래그 안 시작)
            StringAssert.Contains("if (MountPopover.IsSlotCell(cell, _editing)) OpenMountPopover(cell);", bc);
            // 이동 모드 탭
            StringAssert.Contains("if (tapped && MountPopover.IsMountCell(_panPressCell, _editing) && !_grid.IsOccupied(_panPressCell))", bc);
            // 조립 모드: 벨트 깔린 포트 칸만 연다 — 빈 칸 탭(놓기)은 종전 길
            StringAssert.Contains("else if ((_grid.HasBelt(cell) && MountPopover.IsPortCell(cell, _editing))", bc);
            StringAssert.Contains("if (PopoverOpen) Deselect();\n                    else if (canEdit) Place(cell);",
                                  bc.Replace("\r\n", "\n"));
            // 노드 고르기와 동시에 안 선다
            StringAssert.Contains("_mountCell = null;   // #171 — 노드 팝오버와 마운트 팝오버는 동시에 안 선다", bc);
            StringAssert.Contains("DrawMountPanel();", bc);
            // 같은 강화 문
            StringAssert.Contains("IdleSignals.RequestMountEnhance(cost);", bc);
            // 노드 고르기 · 벨트 깔기 · 지우기 길은 그대로
            StringAssert.Contains("else if (_grid.IsOccupied(cell)) Select(cell);", bc);
            StringAssert.Contains("if (canEdit) LayBelts(_dragCells);", bc);
            StringAssert.Contains("FinishRemovalDrag();", bc);
        }

        [Test]
        public void 러너가_팝오버_숫자를_싣고_안내_창은_글자에서_잰다_소스_고정()
        {
            string sr = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            StringAssert.Contains("PublishMountPopover();", sr);
            StringAssert.Contains("MountPopover.Publish(MountOwner.RobotA,", sr);
            StringAssert.Contains("EntryNoticeLayout l = MeasureEntryNotice();", sr);
            StringAssert.DoesNotContain("float h = (showB ? 630f : 470f) * sc;", sr);
        }
    }
}
