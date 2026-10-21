using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **T3 — S1 돌파 도전 튜토리얼** (튜토리얼 기획서 4장 · 사용자 확정 #115 · 2026-10-14).
    ///
    /// 📌 **지키는 것은 기획서 문장이다** — 무대 S1 · 안내 수단 「강제 버튼(도전 들어가기)」 ·
    /// 끝나는 조건 「S1 통과」. 여기 값 중 지어낸 것은 없다.
    /// </summary>
    public sealed class ChallengeTutorialTests
    {
        // ── 언제 도는가 ────────────────────────────────────────────────

        [Test]
        public void 튜토리얼을_마치고_S1_파밍에_서면_돈다()
        {
            Assert.IsTrue(ChallengeTutorialRule.IsActive(
                tutorialCleared: true, stage1Cleared: false, stageNumber: 1, isFarming: true));
        }

        [Test]
        public void 바로가기_점프_저장에서도_선다_254()
        {
            // ✅ #254 — 튜토리얼 통과 조건 걷음(🗑️ 「튜토리얼 전이면 안 돈다」). 자연 진행은 T1 을 마쳐야 S1 에 닿는다.
            Assert.IsTrue(ChallengeTutorialRule.IsActive(
                tutorialCleared: false, stage1Cleared: false, stageNumber: 1, isFarming: true));
        }

        [Test]
        public void S1_을_통과했으면_끝이다()
        {
            // ⚠️⚠️ **끝나는 조건이 곧 저장이다.** T3 완료를 따로 안 적고
            //    `clearedStageIds` 에 S1 이 있는가로 본다(지침 §7 · 새 장부를 안 만든다).
            Assert.IsFalse(ChallengeTutorialRule.IsActive(
                tutorialCleared: true, stage1Cleared: true, stageNumber: 1, isFarming: true));
        }

        [Test]
        public void 다른_판에서는_안_돈다()
        {
            // 무대는 S1 이다 — S2 에서 「도전에 들어가라」를 가리키면 엉뚱한 곳을 가리킨다.
            Assert.IsFalse(ChallengeTutorialRule.IsActive(
                tutorialCleared: true, stage1Cleared: false, stageNumber: 2, isFarming: true));
            Assert.IsFalse(ChallengeTutorialRule.IsActive(
                tutorialCleared: true, stage1Cleared: false, stageNumber: 0, isFarming: true),
                "튜토리얼 전용 판(번호 0)에서도 안 돈다");
        }

        [Test]
        public void 도전_안에서는_안_돈다()
        {
            // 이미 들어와 있는데 「들어가라」를 빛내면 말이 안 된다.
            Assert.IsFalse(ChallengeTutorialRule.IsActive(
                tutorialCleared: true, stage1Cleared: false, stageNumber: 1, isFarming: false));
        }

        [Test]
        public void 무대는_S1_이다()
        {
            Assert.AreEqual(1, ChallengeTutorialRule.StageNumber);
        }

        // ── 무엇이 눌리는가 (기획서 2장 「강제 버튼」) ──────────────────

        [Test]
        public void T3_국면이_선다()
        {
            Assert.AreEqual(TutorialGate.Phase.Challenge,
                TutorialGate.Resolve(urgeChallenge: true, urgeEnterBoard: false,
                    urgeBuildMode: false, ghostWaiting: false, inBuildMode: false));
        }

        [Test]
        public void 도전만_열린다()
        {
            // 「지금 눌러야 할 버튼만 빛나게 하고 다른 입력은 잠근다」(기획서 2장).
            TutorialGate.Phase p = TutorialGate.Phase.Challenge;

            Assert.IsTrue(TutorialGate.Allows(p, TutorialGate.Control.EnterChallenge),
                "가리키는 그 버튼이 잠기면 국면을 빠져나갈 수 없다");

            Assert.IsFalse(TutorialGate.Allows(p, TutorialGate.Control.StageList), "스테이지");
            Assert.IsFalse(TutorialGate.Allows(p, TutorialGate.Control.EnterBoard), "조립");
            Assert.IsFalse(TutorialGate.Allows(p, TutorialGate.Control.Module), "모듈");
            Assert.IsFalse(TutorialGate.Allows(p, TutorialGate.Control.PaletteOther), "팔레트");
        }

        [Test]
        public void 전투로는_예외다()
        {
            // ⚠️ 2026-09-16 사용자 지시 · 기획서 2장에 명시된 유일한 예외.
            //    들어갈 수도 나올 수도 없으면 멈춘 화면이 된다(09-15·09-16 에 두 번 겪었다).
            Assert.IsTrue(TutorialGate.Allows(
                TutorialGate.Phase.Challenge, TutorialGate.Control.ExitBoard));
        }

        [Test]
        public void T3_가_아니면_전부_눌린다()
        {
            // 끝난 뒤에는 잠그는 것이 없다 — 기본 원칙(「시스템은 개입하지 않는다」)으로 돌아간다.
            Assert.AreEqual(TutorialGate.Phase.None,
                TutorialGate.Resolve(urgeChallenge: false, urgeEnterBoard: false,
                    urgeBuildMode: false, ghostWaiting: false, inBuildMode: false));

            Assert.IsTrue(TutorialGate.Allows(
                TutorialGate.Phase.None, TutorialGate.Control.StageList));
            Assert.IsTrue(TutorialGate.Allows(
                TutorialGate.Phase.None, TutorialGate.Control.EnterBoard));
        }

        [Test]
        public void 조립_화면_국면이_T3_보다_먼저다()
        {
            // ⚠️ 겹칠 일은 없지만(튜토리얼을 마쳐야 T3 가 켜진다), 겹친다면 **앞 수업이 먼저**다.
            //    T3 가 조립 국면을 덮으면 빈 칸을 못 채운 채 도전으로 떠밀린다.
            Assert.AreEqual(TutorialGate.Phase.Challenge,
                TutorialGate.Resolve(true, false, false, false, false),
                "조립 국면이 없으면 T3 가 선다");
        }

        [Test]
        public void 네_인자_덧씌움은_T3_를_안_묻는다()
        {
            // 기존 시험·진단이 쓰는 길 — 조립 화면 국면만 본다.
            Assert.AreEqual(TutorialGate.Phase.None,
                TutorialGate.Resolve(false, false, false, false));
        }
    }
}
