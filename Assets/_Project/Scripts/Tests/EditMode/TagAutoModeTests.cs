using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 자동 교대 스위치와 편집 탭 따라가기 (2026-09-16 사용자 육안 · 플랜 §74-21).
    ///
    /// ⚠️⚠️ **둘 다 내가 세운 가정을 사용자가 뒤집은 자리다.**
    /// ① 「길게 누르기는 켜기만 한다」 → **토글**.
    /// ② 「편집 축과 전투 축은 따로」 → **탭이 활성 로봇을 따라간다**.
    /// 규칙으로는 둘 다 깨끗했는데 **써 보니 틀렸다.** 그래서 시험으로 내려 둔다 —
    /// 다음에 「따로 두는 편이 깨끗하다」는 생각이 들 때 이 파일이 막는다.
    /// </summary>
    public sealed class TagAutoModeTests
    {
        [SetUp]
        public void Reset() => TagAutoMode.Reset();

        [TearDown]
        public void Cleanup() => TagAutoMode.Reset();

        // ── ① 길게 누르기 = 토글 ────────────────────────────────────────

        [Test]
        public void 기본값은_꺼짐이다()
        {
            // ⚠️ **가정**이다(설계 역기입 자리). 근거는 문구 「길게 = 자동 켬/끔」 —
            //    길게 눌러 켜는 것이 첫 동작이라면 처음 상태는 꺼짐이어야 말이 된다.
            Assert.IsFalse(TagAutoMode.DefaultEnabled);
            Assert.IsFalse(TagAutoMode.Enabled);
        }

        [Test]
        public void 길게_두_번이면_원상이다()
        {
            // **사용자가 잡은 결함 그 자체** — 켠 뒤 다시 길게 눌러도 안 꺼졌다.
            bool before = TagAutoMode.Enabled;

            Assert.AreEqual(!before, TagAutoMode.Toggle(), "한 번에 안 뒤집힌다");
            Assert.AreEqual(before, TagAutoMode.Toggle(), "두 번 눌렀는데 원상이 아니다");
            Assert.AreEqual(before, TagAutoMode.Enabled);
        }

        [Test]
        public void 뒤집는_자리는_한_곳이다()
        {
            // 길게 누르기와 토글 버튼이 **같은 값**을 만진다 — 둘이 각자 뒤집으면
            // 한쪽만 고쳐지는 날이 온다(지침 §7).
            TagAutoMode.Toggle();
            Assert.IsTrue(TagAutoMode.Enabled);

            TagAutoMode.Enabled = false;     // 토글 버튼이 하는 일
            Assert.IsFalse(TagAutoMode.Enabled);

            Assert.IsTrue(TagAutoMode.Toggle(), "길게 누르기가 다른 값을 보고 있다");
        }

        [Test]
        public void 안내_문구가_손짓과_같은_말을_한다()
        {
            // 문구가 손짓과 다르면 화면이 거짓말을 한다 — 켜기만 하던 시절의
            // 「길게 = 자동」이 그대로 남으면 끄는 길이 없는 것으로 읽힌다.
            StringAssert.Contains("켬", TagAutoMode.Hint);
            StringAssert.Contains("끔", TagAutoMode.Hint);
        }

        // ── ② 편집 탭이 활성 로봇을 따라간다 ────────────────────────────

        [Test]
        public void 교대하면_탭이_따라간다()
        {
            Assert.IsTrue(BoardTabFollow.ShouldFollow(
                MountOwner.RobotB, MountOwner.RobotA, boardOpen: true, wasBoardOpen: true),
                "교대했는데 탭이 안 따라간다 — 싸우는 로봇의 줄을 못 본다");
        }

        [Test]
        public void 조립에_막_들어오면_활성_로봇_판으로_연다()
        {
            Assert.IsTrue(BoardTabFollow.ShouldFollow(
                MountOwner.RobotB, MountOwner.RobotB, boardOpen: true, wasBoardOpen: false));
        }

        [Test]
        public void 열려_있는_동안에는_손을_안_이긴다()
        {
            // **이 한 줄이 「수동 전환이 그대로 유지된다」를 지킨다.** 매 프레임 맞추면
            // 손으로 B 를 열어 둔 채 A 로 싸울 수가 없다.
            Assert.IsFalse(BoardTabFollow.ShouldFollow(
                MountOwner.RobotA, MountOwner.RobotA, boardOpen: true, wasBoardOpen: true),
                "가만히 있는데 탭을 도로 끌어갔다");
        }

        [Test]
        public void 닫혀_있으면_안_맞춘다()
        {
            // 전투 화면에서는 맞출 이유가 없다 — 다음 진입에서 맞춘다.
            Assert.IsFalse(BoardTabFollow.ShouldFollow(
                MountOwner.RobotA, MountOwner.RobotA, boardOpen: false, wasBoardOpen: false));
        }

        [Test]
        public void 닫혀_있어도_교대는_잡는다()
        {
            // 전투 화면에서 교대가 일어나면 **그때 맞춰 둔다** — 다음 진입을 기다리면
            // 들어가는 순간 한 프레임 동안 옛 판이 보인다.
            Assert.IsTrue(BoardTabFollow.ShouldFollow(
                MountOwner.RobotB, MountOwner.RobotA, boardOpen: false, wasBoardOpen: false));
        }
    }
}
