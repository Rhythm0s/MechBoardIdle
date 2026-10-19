using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// #182 S4 강화 최소 안내 · #183 T1 팔레트 노드 잠금 (2026-10-19 사용자 확정).
    /// </summary>
    public class EnhanceMountTutorialTests
    {
        [SetUp]
        public void SetUp() => TutorialSignals.Reset();

        [TearDown]
        public void TearDown() => TutorialSignals.Reset();

        // #188 — S3 · B 해금 조건 없음 · #196 비용 50
        private static bool Active(bool cleared = true, int stage = 3, bool farming = true,
                                   bool enhanced = false, double mat = 50d, double scrap = 300d) =>
            MountEnhanceRule.TutorialActive(cleared, stage, farming, enhanced, mat, 50d, scrap, 300d);

        [Test]
        public void 강화_안내는_S4_파밍_해금_뒤_재료와_고철이_다_찰_때만_선다()
        {
            Assert.IsTrue(Active());
            Assert.IsFalse(Active(mat: 49d), "재료가 모자라면 막 없음(갇힘 방지)");
            Assert.IsFalse(Active(scrap: 299d), "고철이 모자라면 막 없음");
            Assert.IsTrue(Active(mat: 63d), "S3 파밍 진입 재료 63(S1 30 + S2 33) ≥ 50 — 막이 선다");
            Assert.IsFalse(Active(enhanced: true), "강화하면 풀린다 · 바로가기 S3+ 점프(#162 · #188)도 여기서 안 선다");
            Assert.IsFalse(Active(farming: false), "도전 층에서는 안 선다");
            Assert.IsFalse(Active(stage: 4));
            Assert.IsFalse(Active(stage: 2));
            Assert.IsFalse(Active(cleared: false));
        }

        [Test]
        public void 강화_국면은_전투에서_조립_하나_보드에서_마운트_탭()
        {
            Assert.AreEqual(TutorialGate.Phase.EnhanceMount,
                TutorialGate.Resolve(true, false, false, false, false, false, false, false, false));
            Assert.AreEqual(TutorialGate.Phase.EnhanceMountBoard,
                TutorialGate.Resolve(true, false, false, false, true, false, false, false, false));

            var p = TutorialGate.Phase.EnhanceMount;
            Assert.IsTrue(TutorialGate.Allows(p, TutorialGate.Control.EnterBoard));
            Assert.IsTrue(TutorialGate.Allows(p, TutorialGate.Control.ExitBoard));
            Assert.IsFalse(TutorialGate.Allows(p, TutorialGate.Control.EnterChallenge));
            Assert.IsFalse(TutorialGate.Allows(p, TutorialGate.Control.StageList));

            var b = TutorialGate.Phase.EnhanceMountBoard;
            Assert.IsTrue(TutorialGate.Allows(b, TutorialGate.Control.ModeToggle), "조립 모드 전환은 안 막는다");
            Assert.IsTrue(TutorialGate.Allows(b, TutorialGate.Control.ExitBoard));
            Assert.IsFalse(TutorialGate.Allows(b, TutorialGate.Control.PaletteOther));
            Assert.IsFalse(TutorialGate.Allows(b, TutorialGate.Control.Module));
            Assert.IsFalse(TutorialGate.Allows(b, TutorialGate.Control.Remove));
        }

        [Test]
        public void 강화_국면은_B_판에서는_안_잡는다()
        {
            TutorialSignals.HighlightEnhanceMount = true;
            TutorialSignals.BoardViewOpen = true;
            TutorialSignals.BoardViewIsTutorialBoard = true;
            Assert.AreEqual(TutorialGate.Phase.EnhanceMountBoard, TutorialGate.Current);
            TutorialSignals.BoardViewIsTutorialBoard = false;
            Assert.AreEqual(TutorialGate.Phase.None, TutorialGate.Current);
        }

        [Test]
        public void 강화_안내_문안_과_자리()
        {
            Assert.AreEqual("조립에서 마운트를 탭해 강화해보세요", MountEnhanceRule.CombatPrompt);
            Assert.AreEqual("마운트를 탭한 뒤 강화를 누르세요", MountEnhanceRule.BoardHint);
            Assert.AreEqual("S3", MountEnhanceRule.TutorialStageId);   // #188

            string sr = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            StringAssert.Contains("TutorialSignals.HighlightEnhanceMount = MountEnhanceRule.TutorialActive(", sr);
            StringAssert.Contains("ChallengeTutorialOverlay.DrawEnhanceMount(TutorialSignals.HighlightEnhanceMount);", sr);
            string bc = System.IO.File.ReadAllText("Assets/_Project/Scripts/Logistics/BoardController.cs");
            StringAssert.Contains("&& TutorialGate.Current == TutorialGate.Phase.EnhanceMountBoard)", bc);
            StringAssert.Contains("if (MountPopover.IsMountCell(tap, _editing) && !_grid.IsOccupied(tap)) OpenMountPopover(tap);", bc);
        }

        [Test]
        public void T1_전용판_동안_노드_단추_전부_잠금()
        {
            Assert.IsTrue(TutorialGate.LocksPaletteNodes(TutorialGate.Phase.None, true), "고스트를 채운 뒤에도 전용 판이면 잠근다");
            Assert.IsTrue(TutorialGate.LocksPaletteNodes(TutorialGate.Phase.PlaceNode, false));
            Assert.IsFalse(TutorialGate.LocksPaletteNodes(TutorialGate.Phase.None, false), "전용 판 밖 · 국면 없음 = 열림");

            string bc = System.IO.File.ReadAllText("Assets/_Project/Scripts/Logistics/BoardController.cs");
            StringAssert.Contains("bool allowed = !TutorialGate.LocksPaletteNodes(StageLayerSignals.IsTutorialStage);", bc);
            StringAssert.DoesNotContain("? TutorialGate.Allows(TutorialGate.Control.PaletteMunitions)", bc);
        }
    }
}
