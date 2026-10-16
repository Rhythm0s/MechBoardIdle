using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **도전 직전 물류 유지 · 들어가기 전 안내** (2026-10-16 사용자 직접 지시 ②③).
    /// </summary>
    public sealed class ChallengeEntryNoticeTests
    {
        [Test]
        public void 줄은_마운트_양을_내림으로_적는다()
        {
            Assert.AreEqual("로봇 A 마운트 탄환 39/40", ChallengeEntryNotice.LineA(39.6f, 40f));
            Assert.AreEqual("로봇 B 마운트 드론 0/40", ChallengeEntryNotice.LineB(0f, 40f));
            Assert.AreEqual("로봇 A 마운트 탄환 40/40", ChallengeEntryNotice.LineA(40f, 40f));
        }

        [Test]
        public void 문구는_가정_글자_그대로다()
        {
            Assert.AreEqual("도전 시작", ChallengeEntryNotice.Title);
            Assert.AreEqual("지금 마운트에 실린 물류를 그대로 가지고 도전합니다", ChallengeEntryNotice.Body);
            Assert.AreEqual("도전 시작", ChallengeEntryNotice.Confirm);
            Assert.AreEqual("취소", ChallengeEntryNotice.Cancel);
        }

        [Test]
        public void 도전_단추와_다시_도전은_안내를_거치고_마운트는_러너가_든다()
        {
            string src = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            StringAssert.Contains("if (hit && allowed) RequestChallenge();", src, "「도전」이 안내 없이 바로 들어간다");
            StringAssert.Contains("if (i == 0) { RequestChallenge(); break; }", src, "「다시 도전」이 안내 없이 바로 들어간다");
            StringAssert.Contains("ChallengeEntryNotice.Confirm, btn)) EnterChallenge();", src);
            // 지시 ② — 판을 다시 세워도 마운트를 새로 만들지 않는다(있으면 그것을 쓴다)
            StringAssert.Contains("_mountA ?? (_mountA = new MountLoad(", src);
            StringAssert.Contains("_mountB ?? (_mountB = new MountLoad(", src);
            StringAssert.Contains("setup.mount = MountA(stack);", src);
        }
    }
}
