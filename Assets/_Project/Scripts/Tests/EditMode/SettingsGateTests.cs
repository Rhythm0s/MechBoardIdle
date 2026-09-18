using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **설정 패널의 빗장** (2026-09-18 사용자 확정 · 설정 칩 되살리기).
    ///
    /// ⚠️⚠️ 여기서 지키는 것은 **결함 하나가 다시 안 나는 것**이다 —
    /// 설정을 메뉴 빗장에 물렸더니 **누를 때마다 HUD 가 통째로 사라졌다**(09-18 육안).
    /// 메뉴 빗장은 `OnGUI` 일곱을 억제하고, 그 일곱에 HUD 가 전부 들어 있다.
    /// </summary>
    public sealed class SettingsGateTests
    {
        [SetUp]
        public void Setup()
        {
            SettingsGate.Reset();
            MainMenuGate.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            SettingsGate.Reset();
            MainMenuGate.Reset();
        }

        [Test]
        public void 설정이_열려도_HUD_는_산다()
        {
            // 📌 **이것이 이 파일이 생긴 까닭이다.** 화면을 억제하는 것은 메뉴 빗장 하나이고,
            //    설정은 **얹기만** 한다 — 열어도 메뉴 빗장이 서면 안 된다.
            SettingsGate.Open();

            Assert.IsTrue(SettingsGate.IsOpen);
            Assert.IsFalse(MainMenuGate.IsOpen, "설정은 OnGUI 를 억제하지 않는다");
        }

        [Test]
        public void 설정은_눌릴_때마다_뒤집힌다()
        {
            Assert.IsFalse(SettingsGate.IsOpen, "기본은 닫힘이다");

            SettingsGate.Toggle();
            Assert.IsTrue(SettingsGate.IsOpen);

            SettingsGate.Toggle();
            Assert.IsFalse(SettingsGate.IsOpen);
        }

        [Test]
        public void 메인_메뉴로_가면_게이트가_잠긴다()
        {
            // 게임이 한 번 시작된 상태에서 메뉴로 돌아간다.
            Assert.IsTrue(MainMenuGate.TryStart(), "첫 시작은 참이다");
            SettingsGate.Open();

            SettingsGate.ReturnToMainMenu();

            Assert.IsFalse(SettingsGate.IsOpen, "설정은 닫힌다");
            Assert.IsTrue(MainMenuGate.IsOpen, "메뉴 빗장이 선다 — 러너가 이것을 보고 전투를 세운다");
        }

        [Test]
        public void 되돌아온_메뉴는_다시_시작이_아니다()
        {
            // ⚠️ 시작 깃발을 내리면 전투가 **처음부터 다시 잡힌다** — 창고·마운트·배분까지.
            //    되돌아온 메뉴는 「보고 있는 중」이다(`MainMenuGate.TryStart` 주석).
            MainMenuGate.TryStart();
            SettingsGate.ReturnToMainMenu();

            Assert.IsTrue(MainMenuGate.HasStarted, "이미 시작한 사실은 남는다");
            Assert.IsFalse(MainMenuGate.CanStart, "다시 시작되지 않는다");

            MainMenuGate.Close();
            Assert.IsFalse(MainMenuGate.TryStart(), "메뉴를 닫아도 두 번째 시작은 없다");
        }
    }
}
