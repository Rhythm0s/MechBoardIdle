using System.Collections.Generic;
using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **스테이지 목록과 해금** (2026-10-13 · 순서 5).
    ///
    /// 규칙 한 줄 — 앞 판의 **도전을 통과**하면 다음 판이 열린다. 첫 판은 늘 열린다.
    /// 해금은 **저장하지 않는다** — 저장의 통과 기록에서 매번 계산한다(지침 §7).
    /// </summary>
    public sealed class StageUnlockRuleTests
    {
        private static readonly string[] Six = { "S1", "S2", "S3", "S4", "S5", "S6" };

        private static List<StageListEntry> Build(params string[] cleared)
        {
            var into = new List<StageListEntry>();
            StageUnlockRule.Build(Six, new HashSet<string>(cleared), into);
            return into;
        }

        [Test]
        public void 아무것도_안_깼으면_첫_판만_열린다()
        {
            List<StageListEntry> list = Build();

            Assert.AreEqual(6, list.Count);
            Assert.IsTrue(list[0].unlocked, "첫 판은 앞 판이 없으므로 조건이 없다");
            for (int i = 1; i < list.Count; i++)
                Assert.IsFalse(list[i].unlocked, "S" + (i + 1) + " 가 열려 있다");
            Assert.AreEqual(1, StageUnlockRule.UnlockedCount(list));
        }

        [Test]
        public void 앞_판을_통과하면_다음_판이_열린다()
        {
            List<StageListEntry> list = Build("S1", "S2");

            Assert.IsTrue(list[0].unlocked);   // S1
            Assert.IsTrue(list[1].unlocked);   // S2 — S1 통과
            Assert.IsTrue(list[2].unlocked);   // S3 — S2 통과
            Assert.IsFalse(list[3].unlocked);  // S4 — S3 미통과
            Assert.AreEqual(3, StageUnlockRule.UnlockedCount(list));
        }

        [Test]
        public void 건너뛴_통과_기록은_그_뒤만_연다()
        {
            // ⚠️ S3 만 깬 이상한 저장 — 규칙은 **앞 판만** 본다.
            List<StageListEntry> list = Build("S3");

            Assert.IsTrue(list[0].unlocked);    // S1 늘 열림
            Assert.IsFalse(list[1].unlocked);   // S2 — S1 미통과
            Assert.IsFalse(list[2].unlocked);   // S3 — S2 미통과
            Assert.IsTrue(list[3].unlocked);    // S4 — S3 통과
        }

        [Test]
        public void 튜토리얼_판은_목록_밖이다()
        {
            var into = new List<StageListEntry>();
            StageUnlockRule.Build(new[] { "S0", "S1", "S2" },
                                  new HashSet<string>(), into);

            Assert.AreEqual(2, into.Count, "S0 은 목록에 안 든다");
            foreach (StageListEntry e in into)
                Assert.AreNotEqual("S0", e.stageId);
            Assert.AreEqual(1, into[0].number);
        }

        [Test]
        public void 번호_순으로_적는다()
        {
            // 자산 차례가 섞여 있어도 목록은 번호 순이다.
            var into = new List<StageListEntry>();
            StageUnlockRule.Build(new[] { "S3", "S1", "S2" },
                                  new HashSet<string>(), into);

            Assert.AreEqual(3, into.Count);
            Assert.AreEqual(1, into[0].number);
            Assert.AreEqual(2, into[1].number);
            Assert.AreEqual(3, into[2].number);
        }

        [Test]
        public void 자산이_없는_번호는_안_적는다()
        {
            // ⚠️ 빈 줄을 지어내지 않는다 — 자산이 덜 구워진 동안에도 목록은 있는 것만 적는다.
            var into = new List<StageListEntry>();
            StageUnlockRule.Build(new[] { "S1", "S3" }, new HashSet<string>(), into);

            Assert.AreEqual(2, into.Count);
            Assert.AreEqual(1, into[0].number);
            Assert.AreEqual(3, into[1].number);
            // S3 는 앞 판(S2) 자산이 목록에 없으므로 **열어 준다**(가정).
            Assert.IsTrue(into[1].unlocked, "앞 판 자산이 없으면 잠그지 않는다");
        }

        [Test]
        public void 통과_표시를_그대로_옮긴다()
        {
            List<StageListEntry> list = Build("S1");

            Assert.IsTrue(list[0].cleared);
            Assert.IsFalse(list[1].cleared);
        }

        [Test]
        public void 빈_입력에도_죽지_않는다()
        {
            var into = new List<StageListEntry> { default };
            StageUnlockRule.Build(null, null, into);
            Assert.AreEqual(0, into.Count, "비우고 채운다");

            StageUnlockRule.Build(Six, null, into);
            Assert.AreEqual(6, into.Count);
            Assert.IsTrue(into[0].unlocked);
            Assert.IsFalse(into[1].unlocked, "통과 기록이 없으면 첫 판만 열린다");

            Assert.AreEqual(0, StageUnlockRule.UnlockedCount(null));
        }
    }
}
