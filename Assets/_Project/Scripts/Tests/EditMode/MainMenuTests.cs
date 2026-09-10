using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 메인 메뉴의 **코어 규칙**과 자산 자리 (2026-09-10 사용자 확정 · 플랜 §66-10).
    ///
    /// 여기서 재는 것은 **언제 시작하는가**와 **주소가 없을 때 어떻게 되는가**다.
    /// 자리·글씨·색은 화면에서 사람이 본다.
    /// </summary>
    public sealed class MainMenuTests
    {
        [SetUp]
        public void SetUp() => MainMenuGate.Reset();

        [TearDown]
        public void TearDown() => MainMenuGate.Reset();

        // ---- 언제 시작하는가 ----

        /// <summary>
        /// **메뉴가 떠 있으면 시작하지 않는다.** 매 프레임 물어도 계속 거짓이다 —
        /// 한 번이라도 참을 내면 메뉴 뒤에서 전투가 돌기 시작한다.
        /// </summary>
        [Test]
        public void WhileTheMenuIsOpen_TheGameNeverStarts()
        {
            MainMenuGate.Open();

            Assert.IsFalse(MainMenuGate.CanStart);
            for (int frame = 0; frame < 10; frame++)
                Assert.IsFalse(MainMenuGate.TryStart(), $"{frame}번째 프레임에 시작해 버렸다");

            Assert.IsFalse(MainMenuGate.HasStarted);
        }

        /// <summary>**닫히면 시작한다 — 딱 한 번.** 두 번 참이면 <c>Begin</c> 이 두 번 돈다.</summary>
        [Test]
        public void OnceClosed_ItStartsExactlyOnce()
        {
            MainMenuGate.Open();
            MainMenuGate.Close();

            Assert.IsTrue(MainMenuGate.TryStart(), "닫혔으면 시작한다");
            Assert.IsTrue(MainMenuGate.HasStarted);

            for (int frame = 0; frame < 10; frame++)
                Assert.IsFalse(MainMenuGate.TryStart(), "두 번째부터는 거짓이다");
        }

        /// <summary>
        /// **메뉴가 없는 씬은 곧바로 시작한다.** 기본이 「열림」이면 격리 전투 씬과 시험에서
        /// **아무도 닫아 주지 않아 영원히 시작하지 않는다.**
        /// </summary>
        [Test]
        public void WithNoMenuAtAll_TheGameStartsImmediately()
        {
            Assert.IsFalse(MainMenuGate.IsOpen, "기본은 열려 있지 않다");
            Assert.IsTrue(MainMenuGate.TryStart());
        }

        /// <summary>
        /// **다시 연 메뉴는 「다시 시작」이 아니다.** 시작한 뒤 메뉴를 열었다 닫아도
        /// 전투를 처음부터 다시 깔지 않는다 — 그것은 이어하기가 아니라 초기화다.
        /// </summary>
        [Test]
        public void ReopeningTheMenuLater_DoesNotRestartTheGame()
        {
            MainMenuGate.Close();
            Assert.IsTrue(MainMenuGate.TryStart());

            MainMenuGate.Open();
            MainMenuGate.Close();
            Assert.IsFalse(MainMenuGate.TryStart(), "이미 시작했다");
        }

        // ---- 주소가 없을 때 ----

        /// <summary>
        /// **빈 주소와 공백만 든 주소를 같게 본다.** 인스펙터에서 지우다 남은 공백 하나가
        /// 버튼을 살려 두면 심사자가 눌러 **빈 탭**이 열린다.
        /// </summary>
        [Test]
        public void BlankUrls_AreNotUsable()
        {
            Assert.IsFalse(PortfolioLinks.IsUsable(null));
            Assert.IsFalse(PortfolioLinks.IsUsable(""));
            Assert.IsFalse(PortfolioLinks.IsUsable("   "));
            Assert.IsFalse(PortfolioLinks.IsUsable("\t"));

            Assert.IsTrue(PortfolioLinks.IsUsable("https://example.com"));
        }

        /// <summary>
        /// 자산 자리가 있고 **주소는 아직 비어 있다**(사용자가 준다).
        ///
        /// ⚠️ **이 시험은 주소가 채워지면 빨개진다.** 그것이 목적이다 — 값이 들어온 날
        /// 여기서 멈춰 「이제 버튼이 산다」는 것을 확인하고 이 시험을 고친다.
        /// </summary>
        [Test]
        public void TheAssetExists_AndItsUrlsAreStillEmpty()
        {
            var links = AssetDatabase.LoadAssetAtPath<PortfolioLinks>(
                "Assets/_Project/ScriptableObjects/PortfolioLinks.asset");

            Assert.NotNull(links, "자산 자리 — 먼저 'MBI/Generate Combat Data' 실행");
            Assert.IsFalse(string.IsNullOrWhiteSpace(links.notice), "문구는 자리표시라도 있어야 한다");

            Assert.IsFalse(links.HasDocument, "포트폴리오 문서 주소는 아직 미기재다");
            Assert.IsFalse(links.HasNotion, "노션 주소는 아직 미기재다");
        }

        // ---- 볼륨 패널은 메뉴 뒤로 들어가면 안 된다 (2026-09-10 · 플랜 §66-35 ①) ----

        /// <summary>
        /// **메뉴가 열려 있어도 볼륨 패널은 그려지고, 메뉴보다 앞에 선다.**
        ///
        /// 메뉴의 「볼륨」이 여는 것이 **메뉴 뒤로 들어가면 열려도 못 읽고 못 만진다** —
        /// 실제로 그랬다(리허설 1바퀴 · 결함 ①).
        ///
        /// 재는 것은 둘이다. ① 볼륨 패널이 **메뉴가 억제하는 목록에 없다**(있으면 아무것도
        /// 안 여는 버튼이 된다). ② `GUI.depth` 가 **메뉴보다 낮다** — IMGUI 는 낮을수록 앞이다.
        ///
        /// ⚠️ 실제로 보이는지는 화면에서 사람이 본다. 여기서 재는 것은 **차례**뿐이다.
        /// </summary>
        [Test]
        public void TheVolumePanelStaysInFrontOfTheMenu()
        {
            Assert.Less(MBI.UI.AudioOptionsPanel.MenuDepth - 1, MBI.UI.AudioOptionsPanel.MenuDepth,
                "볼륨 패널이 메뉴보다 앞이다 — GUI.depth 는 낮을수록 앞");

            // 메뉴가 억제하는 일곱을 소스에서 센다 — 볼륨 패널이 끼어들면 여기서 빨개진다.
            string panel = System.IO.File.ReadAllText(
                "Assets/_Project/Scripts/UI/AudioOptionsPanel.cs");
            StringAssert.DoesNotContain("if (MainMenuGate.IsOpen) return;", panel,
                "볼륨 패널을 억제하면 메뉴의 「볼륨」이 아무것도 안 여는 버튼이 된다");
        }

        /// <summary>
        /// **바탕 판은 글자 자리만 덮는다.** 위쪽 30% 를 통째로 덮으면 전투를 그린 뜻이 사라진다.
        /// </summary>
        [Test]
        public void TheBackingPlateCoversTheTextOnly_NotTheWholeInset()
        {
            var hud = new Rect(12f, 10f, 560f, 280f);
            Rect plate = MBI.UI.UiPlate.Padded(hud);

            Assert.Less(plate.width, 1440f * 0.6f, "판이 가로를 절반 넘게 먹으면 전투가 가린다");
            Assert.Less(plate.yMax, MBI.Core.CombatInsetView.BottomPixels(2560f),
                "판이 인셋 아래로 넘치면 보드까지 어두워진다");

            Assert.Greater(MBI.UI.UiPlate.Alpha, 0f, "0 이면 대비가 안 돌아온다");
            Assert.Less(MBI.UI.UiPlate.Alpha, 1f, "1 이면 전투가 통째로 가려진다");
        }
    }
}
