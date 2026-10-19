using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **#205 ① — 튜토리얼 막 동안 편집 탭 고정**(CID_204 · 2026-10-19).
    /// 8133 실측: S5 폭발탄 막 + Auto 교대 → 탭이 B 판으로 따라가 안내가 꺼지고 B 판 탭이 보통 조작으로 샜다.
    /// </summary>
    public sealed class TutorialBoardPinTests
    {
        [Test]
        public void Explosive_PinsRobotA() =>
            Assert.AreEqual(MountOwner.RobotA, BoardTabFollow.TutorialPin(true, false, false));

        [Test]
        public void EnhanceA_PinsRobotA() =>
            Assert.AreEqual(MountOwner.RobotA, BoardTabFollow.TutorialPin(false, true, false));

        [Test]
        public void EnhanceB_PinsRobotB() =>
            Assert.AreEqual(MountOwner.RobotB, BoardTabFollow.TutorialPin(false, false, true));

        [Test]
        public void EnhanceAAndB_ATakesFirst() =>
            Assert.AreEqual(MountOwner.RobotA, BoardTabFollow.TutorialPin(false, true, true),
                "A 안내가 서 있으면 B 로 안 넘긴다(EnhanceOwner 와 같은 순서)");

        [Test]
        public void NoPrompt_NoPin_FollowAsBefore()
        {
            Assert.IsNull(BoardTabFollow.TutorialPin(false, false, false));
            Assert.IsTrue(BoardTabFollow.ShouldFollow(MountOwner.RobotB, MountOwner.RobotA, true, true),
                "막 밖 = 종전대로 교대를 따라간다");
        }
    }
}
