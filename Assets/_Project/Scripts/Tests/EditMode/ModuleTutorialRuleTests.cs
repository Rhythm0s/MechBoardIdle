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
            Assert.AreEqual("모듈 M·R을 받았습니다", ModuleTutorialRule.GrantNotice);
            Assert.AreEqual("조립 버튼을 눌러 모듈을 붙여보세요", ModuleTutorialRule.CombatPrompt);
            Assert.AreEqual("모듈 탭에서 M 또는 R을 고른 뒤 이 노드를 탭하세요", ModuleTutorialRule.GhostHint);
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
        public void S1_에서_S2_로_넘어오면_알림이_한_번_뜬다()
        {
            var n = new ModuleGrantNotice();
            Assert.IsFalse(n.Observe(1, false, 0f));
            Assert.IsTrue(n.Observe(2, true, 1f));
            Assert.IsTrue(n.IsVisible(1f + ModuleTutorialRule.NoticeSeconds - 0.01f));
            Assert.IsFalse(n.IsVisible(1f + ModuleTutorialRule.NoticeSeconds + 0.01f));
            Assert.IsFalse(n.Observe(2, true, 2f), "한 번만");
        }

        [Test]
        public void 저장에서_곧장_S2_로_돌아오면_알림이_안_뜬다()
        {
            // 켜는 중 판 번호 0(아직 안 실림)은 「다른 판」으로 안 센다.
            var n = new ModuleGrantNotice();
            Assert.IsFalse(n.Observe(0, false, 0f));
            Assert.IsFalse(n.Observe(2, true, 0.1f));
            Assert.IsFalse(n.IsVisible(0.1f));
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
