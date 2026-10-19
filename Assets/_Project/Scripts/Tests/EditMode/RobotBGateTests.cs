using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **로봇 B 는 골드로 해금한다** (2026-10-19 사용자 확정 #173 · 구 #84 「S4 통과 = 해금」 폐기).
    ///
    /// ⚠️ 이 답을 묻는 곳이 셋이다 — 조립 화면 로봇 탭 · 전투 화면 태그 버튼 · 자동 교대.
    /// 셋이 각자 세면 한 곳만 고쳐지는 날이 온다(지침 §7). 여기가 그 하나를 지킨다.
    /// </summary>
    public sealed class RobotBGateTests
    {
        [TearDown]
        public void Clean() => RobotBGate.Reset();

        [Test]
        public void 처음에는_잠겨_있다()
        {
            RobotBGate.Reset();
            Assert.IsFalse(RobotBGate.IsUnlocked, "처음부터 B 가 열려 있다");
        }

        [Test]
        public void 골드로_사면_열린다()
        {
            Assert.IsTrue(RobotBGate.Unlocked(purchased: true, jumpUnlocked: false), "샀는데 안 열린다");
        }

        /// <summary>🗑️ 구 「S4 를 깨면 열린다」 — #173 에서 뒤집혔다. 판 번호만으로는 안 열린다.</summary>
        [Test]
        public void S4_통과나_자연_진행만으로는_안_열린다()
        {
            RobotBGate.CurrentStageNumber = 6;   // 자연 진행으로 S6 까지 와도
            Assert.IsFalse(RobotBGate.IsUnlocked, "산 적도 점프한 적도 없는데 열렸다");
        }

        /// <summary>✅ **바로가기 S5 · S6 점프만** 열린 것으로 본다(#103 — #173 에서도 남김).</summary>
        [Test]
        public void 바로가기_S5_S6_점프만_연다()
        {
            for (int n = 0; n <= 4; n++) Assert.IsFalse(RobotBGate.JumpOpens(n), $"S{n} 점프가 B 를 연다");
            Assert.IsTrue(RobotBGate.JumpOpens(5));
            Assert.IsTrue(RobotBGate.JumpOpens(6));
            Assert.IsTrue(RobotBGate.Unlocked(purchased: false, jumpUnlocked: true));
        }

        [Test]
        public void 해금_단추는_S4_파밍부터_미해금일_때만()
        {
            Assert.IsFalse(RobotBGate.ShowsUnlockButton(3, true, false), "S3 에서 단추");
            Assert.IsTrue(RobotBGate.ShowsUnlockButton(4, true, false));
            Assert.IsTrue(RobotBGate.ShowsUnlockButton(5, true, false), "S4 파밍 「부터」");
            Assert.IsFalse(RobotBGate.ShowsUnlockButton(4, false, false), "도전 층에서 단추");
            Assert.IsFalse(RobotBGate.ShowsUnlockButton(4, true, true), "해금 뒤에도 단추");
        }

        [Test]
        public void 단추는_골드가_값_이상일_때만_눌린다()
        {
            Assert.IsTrue(RobotBGate.CanUnlock(50d, 50d));
            Assert.IsFalse(RobotBGate.CanUnlock(49d, 50d));
            Assert.IsFalse(RobotBGate.CanUnlock(100d, 0d), "값이 안 실렸으면(0) 안 눌린다");
        }

        [Test]
        public void S4_튜토리얼_막은_튜토리얼_마침_S4_파밍_미해금_골드_충분일_때만()
        {
            Assert.IsTrue(RobotBGate.TutorialActive(true, 4, true, false, 50d, 50d));
            Assert.IsFalse(RobotBGate.TutorialActive(false, 4, true, false, 50d, 50d), "튜토리얼 전");
            Assert.IsFalse(RobotBGate.TutorialActive(true, 5, true, false, 50d, 50d), "S5 에서는 막 없음(S4 첫 진입)");
            Assert.IsFalse(RobotBGate.TutorialActive(true, 4, false, false, 50d, 50d), "도전 층");
            Assert.IsFalse(RobotBGate.TutorialActive(true, 4, true, true, 50d, 50d), "해금 뒤");
            Assert.IsFalse(RobotBGate.TutorialActive(true, 4, true, false, 49d, 50d), "골드 부족이면 막 안 띄움");
        }

        [Test]
        public void 튜토리얼_국면은_해금_단추만_연다()
        {
            TutorialGate.Phase p = TutorialGate.Resolve(true, false, false, false, false, false, false, false);
            Assert.AreEqual(TutorialGate.Phase.UnlockRobotB, p);
            Assert.IsTrue(TutorialGate.Allows(p, TutorialGate.Control.UnlockRobotB));
            Assert.IsTrue(TutorialGate.Allows(p, TutorialGate.Control.ExitBoard));
            Assert.IsFalse(TutorialGate.Allows(p, TutorialGate.Control.EnterChallenge));
            Assert.IsFalse(TutorialGate.Allows(p, TutorialGate.Control.EnterBoard));
            Assert.IsFalse(TutorialGate.Allows(p, TutorialGate.Control.StageList));
            // 다른 국면에서는 해금 단추가 열려 있다(국면 None)
            Assert.IsTrue(TutorialGate.Allows(TutorialGate.Phase.None, TutorialGate.Control.UnlockRobotB));
        }

        [Test]
        public void 여는_판은_UnlockStageId_가_정한다()
        {
            int gate = StageMapName.NumberOf(RobotBGate.UnlockStageId);
            Assert.Greater(gate, 0, "여는 판이 여섯에 안 드는 글자다");
            Assert.IsTrue(RobotBGate.ShowsUnlockButton(gate, true, false));
            Assert.IsFalse(RobotBGate.ShowsUnlockButton(gate - 1, true, false));
            Assert.IsFalse(RobotBGate.JumpOpens(gate));
            Assert.IsTrue(RobotBGate.JumpOpens(gate + 1));
        }

        /// <summary>처음부터(저장 초기화)면 다시 잠긴다.</summary>
        [Test]
        public void 초기화하면_다시_잠긴다()
        {
            RobotBGate.Purchased = true;
            RobotBGate.JumpUnlocked = true;
            Assert.IsTrue(RobotBGate.IsUnlocked);

            RobotBGate.Reset();

            Assert.IsFalse(RobotBGate.IsUnlocked, "초기화했는데 B 가 열린 채다");
        }
    }
}
