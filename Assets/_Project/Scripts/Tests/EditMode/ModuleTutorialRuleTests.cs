using MBI.Data;
using System.Collections.Generic;
using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **T4 — S2 모듈 배치 튜토리얼** (2026-10-15 사용자 확정 #138).
    /// </summary>
    public sealed class ModuleTutorialRuleTests
    {
        // ── 서는 조건 ──

        [Test]
        public void S1_깨고_S2_파밍에서_대상에_모듈이_없으면_선다()
        {
            Assert.IsTrue(ModuleTutorialRule.IsActive(true, true, false, 2, true, false));
        }

        [Test]
        public void 대상에_모듈을_붙이면_끝난다()
        {
            Assert.IsFalse(ModuleTutorialRule.IsActive(true, true, false, 2, true, targetHasModule: true));
        }

        [Test]
        public void S2_를_깼으면_안_선다()
        {
            Assert.IsFalse(ModuleTutorialRule.IsActive(true, true, stage2Cleared: true, 2, true, false));
        }

        [Test]
        public void S1_을_못_깼으면_안_선다_T3_가_먼저다()
        {
            Assert.IsFalse(ModuleTutorialRule.IsActive(true, stage1Cleared: false, false, 2, true, false));
        }

        [Test]
        public void 다른_판이나_도전_층에서는_안_선다()
        {
            Assert.IsFalse(ModuleTutorialRule.IsActive(true, true, false, 1, true, false));
            Assert.IsFalse(ModuleTutorialRule.IsActive(true, true, false, 2, isFarming: false, false));
        }

        [Test]
        public void 문안은_사용자_확정_글자_그대로다()
        {
            // #147 — 글자 기호 M/R 대신 「생산량·생산속도 모듈」.
            Assert.AreEqual("생산량·생산속도 모듈을 받았습니다", ModuleTutorialRule.GrantNotice);
            Assert.AreEqual("조립 버튼을 눌러 모듈을 붙여보세요", ModuleTutorialRule.CombatPrompt);
            Assert.AreEqual("모듈 탭에서 생산량 또는 생산속도 모듈을 고른 뒤 이 노드를 탭하세요", ModuleTutorialRule.GhostHint);
        }

        [Test]
        public void 모듈_화면_이름은_자산_이름_뒤에_모듈을_붙인다()
        {
            Assert.AreEqual("생산량 모듈", ModuleTutorialRule.DisplayName("생산량"));
            Assert.AreEqual("생산속도 모듈", ModuleTutorialRule.DisplayName("생산속도"));
        }

        // ── 대상 노드 ──

        [Test]
        public void 대상은_놓인_첫_자리다()
        {
            var placed = new HashSet<Vector2Int> { new Vector2Int(7, 8), new Vector2Int(7, 7) };
            Assert.AreEqual(new Vector2Int(7, 8), ModuleTutorialRule.PickTarget(placed.Contains));
        }

        [Test]
        public void 앞_자리를_지웠으면_다음_자리다()
        {
            var placed = new HashSet<Vector2Int> { new Vector2Int(7, 7) };
            Assert.AreEqual(new Vector2Int(7, 7), ModuleTutorialRule.PickTarget(placed.Contains));
        }

        [Test]
        public void 둘_다_없으면_대상이_없다()
        {
            Assert.IsNull(ModuleTutorialRule.PickTarget(_ => false));
        }

        // ── 지급 알림 ──

        [Test]
        public void T4_가_서는_순간_한_번_뜬다_진입_경로와_무관()
        {
            var n = new ModuleGrantNotice();
            Assert.IsTrue(n.Observe(true, true, 1f));
            Assert.IsTrue(n.IsVisible(1f + ModuleTutorialRule.NoticeSeconds - 0.01f));
            Assert.IsFalse(n.IsVisible(1f + ModuleTutorialRule.NoticeSeconds + 0.01f));
            Assert.IsFalse(n.Observe(true, true, 2f), "한 번만");
        }

        [Test]
        public void 처음부터_로_튜토리얼_판에_서면_다음_T4_에서_다시_뜬다()
        {
            // 사용자 결함(10-15) — 세션에 한 번이라 같은 창에서 다시 돌면 둘째 T4 에서 안 떴다.
            var n = new ModuleGrantNotice();
            Assert.IsTrue(n.Observe(true, true, 1f));
            n.Rearm();
            Assert.IsTrue(n.Observe(true, true, 50f));
        }

        [Test]
        public void 보드가_끝_조건을_아직_안_알렸으면_안_센다()
        {
            var n = new ModuleGrantNotice();
            Assert.IsFalse(n.Observe(true, targetKnown: false, 0f));
            Assert.IsTrue(n.Observe(true, targetKnown: true, 0.1f));
        }

        [Test]
        public void 탭하면_바로_닫힌다()
        {
            var n = new ModuleGrantNotice();
            n.Observe(true, true, 1f);
            n.Dismiss();
            Assert.IsFalse(n.IsVisible(1.1f));
        }

        [Test]
        public void 목표_줄_수치는_지금과_가득이고_걸리면_가득으로_멈춘다()
        {
            Assert.AreEqual(" (12/40)", MilestoneCard.MountProgressText(12.7f, 40f, false));
            Assert.AreEqual(" (40/40)", MilestoneCard.MountProgressText(5f, 40f, true));
            Assert.AreEqual(string.Empty, MilestoneCard.MountProgressText(5f, 0f, false));
        }

        [Test]
        public void 마운트_목표_수는_전_슬롯_기준이다()
        {
            // 「(0/10)」 — 임자 있는 슬롯만 세는 Capacity 를 쓴 탓(10-15 8123 실측).
            var m = new MountLoad(MountLoad.SlotsRobotA, MountLoad.StandardStacks(10f));
            Assert.AreEqual(40f, m.FullCapacity, 0.001f);
            m.Load(MountItem.Standard, 3f);
            Assert.AreEqual(40f, m.FullCapacity, 0.001f);
        }

        // ── 게이트 ──

        [Test]
        public void 전투_화면에서는_조립만_열리고_도전_스테이지는_잠긴다()
        {
            TutorialGate.Phase p = TutorialGate.Resolve(false, true, false, false, false, false, false);
            Assert.AreEqual(TutorialGate.Phase.ModuleEnterBoard, p);
            Assert.IsTrue(TutorialGate.Allows(p, TutorialGate.Control.EnterBoard));
            Assert.IsFalse(TutorialGate.Allows(p, TutorialGate.Control.EnterChallenge));
            Assert.IsFalse(TutorialGate.Allows(p, TutorialGate.Control.StageList));
        }

        [Test]
        public void 조립_화면에서는_모듈_탭_단추와_나가기만_열린다()
        {
            TutorialGate.Phase p = TutorialGate.Resolve(false, true, true, false, false, false, true);
            Assert.AreEqual(TutorialGate.Phase.PlaceModule, p);
            Assert.IsTrue(TutorialGate.Allows(p, TutorialGate.Control.ModuleTab));
            Assert.IsTrue(TutorialGate.Allows(p, TutorialGate.Control.Module));
            Assert.IsTrue(TutorialGate.Allows(p, TutorialGate.Control.ExitBoard));
            Assert.IsFalse(TutorialGate.Allows(p, TutorialGate.Control.PaletteOther));
            Assert.IsFalse(TutorialGate.Allows(p, TutorialGate.Control.PaletteMunitions));
            Assert.IsFalse(TutorialGate.Allows(p, TutorialGate.Control.BeltElement));
            Assert.IsFalse(TutorialGate.Allows(p, TutorialGate.Control.Remove));
        }

        [Test]
        public void T3_가_서_있으면_T3_가_먼저다()
        {
            Assert.AreEqual(TutorialGate.Phase.Challenge,
                TutorialGate.Resolve(true, true, false, false, false, false, false));
        }

        [Test]
        public void 다른_국면에서는_모듈_탭도_잠긴다()
        {
            // 「모듈」 탭을 따로 묻게 했어도 T1·T3 국면의 답은 종전(PaletteOther)과 같아야 한다.
            foreach (TutorialGate.Phase p in new[]
                     {
                         TutorialGate.Phase.EnterBoard, TutorialGate.Phase.BuildMode,
                         TutorialGate.Phase.PlaceNode, TutorialGate.Phase.Challenge,
                     })
                Assert.AreEqual(TutorialGate.Allows(p, TutorialGate.Control.PaletteOther),
                                TutorialGate.Allows(p, TutorialGate.Control.ModuleTab), p.ToString());
            Assert.IsTrue(TutorialGate.Allows(TutorialGate.Phase.None, TutorialGate.Control.ModuleTab));
        }

        [Test]
        public void T4_동안_보드는_조립_모드로_열린다()
        {
            Assert.IsTrue(BoardOpenModeRule.OpensInBuildMode(false, moduleTutorialActive: true));
            Assert.IsFalse(BoardOpenModeRule.OpensInBuildMode(false, moduleTutorialActive: false));
        }
    }
}
