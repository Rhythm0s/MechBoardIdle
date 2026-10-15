using MBI.Core;
using MBI.UI;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **「메인 메뉴로」 → 메뉴가 실제로 뜬다** (2026-10-15 사용자 확정 #134).
    ///
    /// 결함: 그 단추가 빗장(`MainMenuGate`)만 열고 메뉴는 제 깃발이 꺼진 채라
    /// **모든 화면이 그리기를 멈추고 메뉴도 안 떠** 로봇·HP 바만 남았다(09-18 `b0bf9db` 부터).
    /// </summary>
    public sealed class MainMenuReturnTests
    {
        [SetUp]
        public void SetUp()
        {
            MainMenuGate.Reset();
            SettingsGate.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            MainMenuGate.Reset();
            SettingsGate.Reset();
        }

        [Test]
        public void 메인_메뉴로를_누르면_메뉴가_보인다()
        {
            // 처음 켤 때 — 메뉴가 떠 있다.
            MainMenuGate.Open();
            Assert.IsTrue(MainMenuOverlay.Visible);

            // 「게임 시작」 — 메뉴가 빗장을 닫고(`MainMenuOverlay.StartGame`), 그다음 러너가
            // 시작 문을 한 번 쓴다(`StageRunner` → `TryStart` — 빗장이 닫혀야 참이다).
            MainMenuGate.Close();
            Assert.IsFalse(MainMenuOverlay.Visible);
            Assert.IsTrue(MainMenuGate.TryStart());

            // ⚠️⚠️ **이것이 결함의 시험이다.** 설정 → 「메인 메뉴로」.
            //    빗장이 열렸는데 메뉴가 안 보이면 모든 화면이 그리기를 멈춘 빈 화면이 된다.
            SettingsGate.ReturnToMainMenu();
            Assert.IsTrue(MainMenuGate.IsOpen, "빗장이 열려야 한다");
            Assert.IsTrue(MainMenuOverlay.Visible,
                "빗장이 열렸는데 메뉴가 안 보이면 단추 하나 없는 화면이 된다");
        }

        [Test]
        public void 메뉴와_빗장은_언제나_같은_답을_한다()
        {
            // 한 값이 두 곳에 살면 답이 둘이 된다 — 이제 메뉴는 빗장을 따라간다.
            MainMenuGate.Open();
            Assert.AreEqual(MainMenuGate.IsOpen, MainMenuOverlay.Visible);
            MainMenuGate.Close();
            Assert.AreEqual(MainMenuGate.IsOpen, MainMenuOverlay.Visible);
            SettingsGate.ReturnToMainMenu();
            Assert.AreEqual(MainMenuGate.IsOpen, MainMenuOverlay.Visible);
        }

        [Test]
        public void 메인_메뉴로는_설정_판을_닫는다()
        {
            SettingsGate.Open();
            SettingsGate.ReturnToMainMenu();
            Assert.IsFalse(SettingsGate.IsOpen, "메뉴 위에 설정 판이 남으면 안 된다");
        }
    }
}
