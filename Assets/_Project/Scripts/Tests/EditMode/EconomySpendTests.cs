using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;

namespace MBI.Tests
{
    /// <summary>
    /// **고철 소비 · 강화 고철 · 로봇 B 해금 · 초기 지급** (2026-10-19 사용자 확정 #172 · #173 · #174).
    /// </summary>
    public sealed class EconomySpendTests
    {
        [TearDown]
        public void Clean()
        {
            BuildCost.Reset();
            IdleSignals.WalletScrap = 0d;
        }

        [Test]
        public void 확정값이_경제_자산에_있다()
        {
            var e = AssetDatabase.LoadAssetAtPath<EconomyConfig>("Assets/_Project/ScriptableObjects/EconomyConfig.asset");
            Assert.IsNotNull(e);
            Assert.AreEqual(20, e.nodeInstallScrapCost, "노드 설치 고철");
            Assert.AreEqual(30, e.moduleInstallScrapCost, "모듈 장착 고철");
            Assert.AreEqual(300, e.enhanceScrapCost, "강화 고철");
            Assert.AreEqual(50, e.robotBUnlockGoldCost, "로봇 B 해금 골드");
            Assert.AreEqual(300, e.startingScrap, "초기 고철");
            Assert.AreEqual(50, e.startingGold, "초기 골드");
        }

        [Test]
        public void 대기_지출을_빼고_잰다_한_프레임에_여러_번_놓아도_잔액을_안_넘는다()
        {
            IdleSignals.WalletScrap = 50d;
            Assert.IsTrue(BuildCost.TryReserveScrap(20d));
            Assert.IsTrue(BuildCost.TryReserveScrap(20d));
            Assert.IsFalse(BuildCost.CanAffordScrap(20d), "50 에서 40 을 적었는데 20 이 또 된다");
            Assert.IsFalse(BuildCost.TryReserveScrap(20d));
            Assert.AreEqual(40d, BuildCost.DrainPendingScrap(), 1e-9);
            Assert.AreEqual(0d, BuildCost.DrainPendingScrap(), 1e-9, "가져가면 비어야 한다");
        }

        [Test]
        public void 비용_0_은_공짜다_런타임_없는_판_종전_거동()
        {
            IdleSignals.WalletScrap = 0d;
            Assert.IsTrue(BuildCost.CanAffordScrap(0d));
            Assert.IsTrue(BuildCost.TryReserveScrap(0d));
            Assert.AreEqual(0d, BuildCost.DrainPendingScrap(), 1e-9);
        }

        [Test]
        public void 고철_부족_줄은_노드_하나도_못_놓을_때만()
        {
            Assert.IsTrue(BuildCost.ScrapShortForNode(19d, 20d));
            Assert.IsFalse(BuildCost.ScrapShortForNode(20d, 20d));
            Assert.IsFalse(BuildCost.ScrapShortForNode(0d, 0d), "비용 0 이면 부족도 없다");
            Assert.AreEqual("고철 부족", BuildCost.ScrapShortLine);
            Assert.AreEqual("고철 20", BuildCost.ScrapLabel(20d));
            Assert.AreEqual("설치 비용 고철 20 · 환불 없음", BuildCost.NodeInstallLine(20d));
        }

        [Test]
        public void 강화는_재료와_고철_둘_다_있어야_한다()
        {
            Assert.IsTrue(MountEnhanceRule.CanEnhance(false, 100d, 100d, 300d, 300d));
            Assert.IsFalse(MountEnhanceRule.CanEnhance(false, 100d, 100d, 299d, 300d), "고철 모자람");
            Assert.IsFalse(MountEnhanceRule.CanEnhance(false, 99d, 100d, 300d, 300d), "재료 모자람");
            Assert.IsFalse(MountEnhanceRule.CanEnhance(true, 100d, 100d, 300d, 300d), "이미 켬");
            Assert.IsTrue(MountEnhanceRule.CanEnhance(false, 100d, 100d, 0d, 0d), "고철 비용 0 = 종전(재료만)");
        }

        [Test]
        public void 팝오버_줄은_재료와_고철을_한_줄에()
        {
            Assert.AreEqual("강화 재료 100/100 · 고철 250/300", MountPopover.MaterialLine(100d, 100d, 250d, 300d));
            Assert.AreEqual("강화 재료 100/100", MountPopover.MaterialLine(100d, 100d, 250d, 0d));
            Assert.IsFalse(MountPopover.ButtonEnabled(true, false, 100d, 100d, 250d, 300d));
            Assert.IsTrue(MountPopover.ButtonEnabled(true, false, 100d, 100d, 300d, 300d));
        }

        [Test]
        public void 새_저장_칸은_미해금_미지급으로_선다()
        {
            var d = new SaveDataV1();
            Assert.IsFalse(d.robotBUnlocked);
            Assert.IsFalse(d.startingGrantGiven);
        }

        // ── #175 (2026-10-19 사용자 확정) ──────────────────────────────

        [Test]
        public void 아이콘_꼴_글자_조각과_떠오름()
        {
            Assert.AreEqual("20", BuildCost.Amount(20d));
            Assert.AreEqual("설치 비용 ", BuildCost.NodeInstallPrefix);
            Assert.AreEqual("20 · 환불 없음", BuildCost.NodeInstallSuffix(20d));
            Assert.AreEqual("-20", BuildCost.SpendPopText(20d));
            Assert.AreEqual("-30", BuildCost.SpendPopText(30d));
            Assert.AreEqual("강화 재료 100/100 · ", MountPopover.MaterialPrefix(100d, 100d));
            Assert.AreEqual("250/300", MountPopover.ScrapSuffix(250d, 300d));
        }

        [Test]
        public void 잠긴_B_탭은_여는_길을_말하고_열_수_있으면_누르면_연다()
        {
            Assert.AreEqual("로봇 B", RobotBGate.TabLabel(true, 1, true, 50d));
            Assert.AreEqual("로봇 B · S4 해금", RobotBGate.TabLabel(false, 3, true, 50d));
            Assert.AreEqual("로봇 B · 골드 50", RobotBGate.TabLabel(false, 4, true, 50d));
            Assert.AreEqual("로봇 B · S4 해금", RobotBGate.TabLabel(false, 4, false, 50d), "도전 층은 단추와 같이 안 연다");
            Assert.IsTrue(RobotBGate.TabUnlocks(false, 4, true, 50d, 50d));
            Assert.IsFalse(RobotBGate.TabUnlocks(false, 4, true, 49d, 50d), "골드 부족");
            Assert.IsFalse(RobotBGate.TabUnlocks(false, 3, true, 50d, 50d), "S4 전");
            Assert.IsFalse(RobotBGate.TabUnlocks(true, 4, true, 50d, 50d), "이미 열림");
            Assert.IsTrue(RobotBGate.TabShowsGoldCost(false, 4, true), "#177 골드 아이콘 꼴");
            Assert.IsFalse(RobotBGate.TabShowsGoldCost(false, 3, true), "S4 전은 「S4 해금」 글자 그대로");
            Assert.IsFalse(RobotBGate.TabShowsGoldCost(true, 4, true));
            Assert.AreEqual("로봇 B · ", RobotBGate.TabPrefix);
            Assert.AreEqual("변환기 · ", BuildCost.PickPrefix("변환기"));
            Assert.AreEqual("20", BuildCost.PickSuffix(20d, false));
            Assert.AreEqual("30 — 놓인 노드를 탭하면 붙는다", BuildCost.PickSuffix(30d, true));
        }

        [Test]
        public void 팔레트_고르기_토글_놓으면_풀림_안_골랐으면_안_놓임_배선_고정()
        {
            string bc = System.IO.File.ReadAllText("Assets/_Project/Scripts/Logistics/BoardController.cs");
            StringAssert.Contains("private int _selectedNode = -1;", bc);
            StringAssert.Contains("_selectedNode = sel ? -1 : i;", bc);                 // ⓕ 재탭 = 해제
            StringAssert.Contains("_selectedModule = on ? -1 : m;", bc);                // ⓕ 모듈도 토글(종전 그대로)
            StringAssert.Contains("SpawnSpendPop(cell, BuildCost.NodeScrap);   // #175 ⓓ", bc);
            StringAssert.Contains("_selectedNode = -1;                          // #175 ⓔ", bc);
            StringAssert.Contains("SpawnSpendPop(cell, BuildCost.ModuleScrap);", bc);
            StringAssert.Contains("if (palette != null && palette.Count > 0 && _selectedNode < 0) return null;", bc);
            StringAssert.Contains("if (scrapShort) DrawScrapShortBadge(new Rect(row.xMax - badgeW, row.y, badgeW, row.height));", bc);                     // ⓑ 탭 줄 부족 아이콘
            StringAssert.Contains("GUI.enabled = wasEnabled && allowed && affordable;", bc);   // ⓑ 단추 Disable
            StringAssert.Contains("GUI.enabled = wasM && moduleAllowed && mAffordable;", bc);
            StringAssert.Contains("if (bTabUnlocks) IdleSignals.RequestRobotBUnlock(BuildCost.RobotBGold);", bc);
            StringAssert.Contains("if (st.CalcSize(new GUIContent(label)).x > width + 0.5f) st.wordWrap = true;", bc);   // 바닥 글자에서도 넘칠 때만 두 줄
            // #176 — 「● 」 접두 폐기 · 고른 표시 = 테두리 · 이름 한 줄
            StringAssert.DoesNotContain("(sel ? \"● \" : \"\") + palette[i].displayName", bc);
            StringAssert.DoesNotContain("(on ? \"● \" : \"\") + ElementLabel(e)", bc);
            StringAssert.DoesNotContain("(on ? \"●\" : \"\") + ModuleTutorialRule.DisplayName", bc);
            StringAssert.Contains("if (sel) DrawSelectedOutline(rect);", bc);
            StringAssert.Contains("if (on) DrawSelectedOutline(eRect);", bc);
            StringAssert.Contains("if (on) DrawSelectedOutline(mRect);", bc);
            // #213 — 이름은 낱말 자리 두 줄(🗑️ OneLineStyle 직접 부름)
            StringAssert.Contains("PaletteLabel(costStyle, palette[i].displayName, rect.width, out GUIStyle nStyle)", bc);
            // #178 — 회피 눈금 바 왼쪽 부스터 아이콘(그림 없으면 눈금만)
            string hud = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.Hud.cs");
            StringAssert.Contains("if (UiIcons.Booster != null)", hud);
            StringAssert.Contains("HudBars.Ticks(new Rect(tickX, y + 4f * sc, tickW, tickH),", hud);
            StringAssert.Contains("if (art != null) UiIcons.Booster = art.NodeSprite(NodeType.Booster);", bc);
            // #218 — 배지 목표 줄은 「목표: {주제}」만(🗑️ #180 「전투력 n 넘기기」)
            StringAssert.DoesNotContain("\"  ·  전투력 \" + stage.req.ToString(\"F0\") + \" 넘기기\"", hud);
            StringAssert.DoesNotContain("\"  ·  출력 \" + stage.req.ToString(\"F0\") + \" 넘기기\"", hud);
            // #181 — 줌아웃 하한 1/1.5
            StringAssert.Contains("private const float ZoomMin = 1f / 1.5f, ZoomMax = 2.5f, ZoomStep = 0.25f;", bc);
            // #177 — B 탭 골드 아이콘
            StringAssert.Contains("RobotBGate.TabPrefix, UiIcons.Gold,", bc);

            string sr = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            StringAssert.Contains("UiIcons.Scrap = tuning.iconScrapSprite;", sr);
        }

        /// <summary>지갑을 만지는 자리 · 비용을 무는 자리 · 해금 · 초기 지급 배선을 고정한다.</summary>
        [Test]
        public void 배선_소스_고정()
        {
            string bc = System.IO.File.ReadAllText("Assets/_Project/Scripts/Logistics/BoardController.cs").Replace("\r\n", "\n");
            StringAssert.Contains("if (!BuildCost.CanAffordScrap(BuildCost.NodeScrap)) return;\n            if (!_grid.TryPlace(cell, node, out _)) return;\n            BuildCost.TryReserveScrap(BuildCost.NodeScrap);", bc);
            StringAssert.Contains("if (!BuildCost.CanAffordScrap(BuildCost.ModuleScrap)) return false;", bc);
            StringAssert.Contains("BuildCost.TryReserveScrap(BuildCost.ModuleScrap);", bc);
            // #176 정정 — 팔레트 단추 안에는 값을 안 그린다 · 고른 것의 값은 안내 줄
            StringAssert.DoesNotContain("DrawPaletteCost(rect, BuildCost.NodeScrap, affordable);", bc);
            StringAssert.DoesNotContain("DrawPaletteCost(mRect, BuildCost.ModuleScrap, mAffordable);", bc);
            StringAssert.Contains("BuildCost.PickPrefix(pickName), UiIcons.Scrap, UiIcons.ScrapFallback,", bc);
            // #213 개정 — 그림 = 단추 가득 · 하단 라벨 띠(🗑️ PaletteThumbScale 0.52)
            StringAssert.Contains("private const float PaletteThumbFill = 0.88f;", bc);
            StringAssert.Contains("DrawPaletteNameBand(rect, nLabel, nStyle);", bc);
            StringAssert.Contains("BuildCost.NodeInstallLine(BuildCost.NodeScrap)", bc);   // 폭 재기용 글자(그리는 것은 아이콘 줄)
            StringAssert.Contains("BuildCost.NodeInstallPrefix, UiIcons.Scrap,", bc);

            string ir = System.IO.File.ReadAllText("Assets/_Project/Scripts/Idle/IdleRuntime.cs");
            StringAssert.Contains("double buildSpend = BuildCost.DrainPendingScrap();", ir);
            StringAssert.Contains("MountEnhanceRule.CanEnhance(Data.mountEnhanced, Wallet.EnhMaterial, enhCost, Wallet.Scrap, enhScrap)", ir);
            StringAssert.Contains("if (enhScrap > 0d) Wallet.TrySpendScrap(enhScrap);", ir);
            StringAssert.Contains("IdleSignals.TryDrainRobotBUnlock(out double bGold)", ir);
            StringAssert.Contains("RobotBGate.Purchased = Data.robotBUnlocked;", ir);
            StringAssert.Contains("if (!hadSave) GrantStarting();", ir);
            StringAssert.Contains("GrantStarting();   // #174 — 「처음부터」도 새 저장이다", ir);
            StringAssert.DoesNotContain("RobotBGate.S4Cleared", ir);

            string sr = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            StringAssert.Contains("TutorialSignals.HighlightUnlockRobotB = RobotBGate.TutorialActive(", sr);
            StringAssert.Contains("DrawRobotBUnlockButton();", sr);
            StringAssert.Contains("ChallengeTutorialOverlay.DrawRobotBUnlock(TutorialSignals.HighlightUnlockRobotB);", sr);

            string rs = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/ReviewerShortcuts.cs");
            StringAssert.Contains("RobotBGate.JumpUnlocked = RobotBGate.JumpOpens(StageMapName.NumberOf(s.stageId));", rs);
            StringAssert.Contains("IdleSignals.WalletScrap, BuildCost.EnhanceScrap", rs);

            string ss = System.IO.File.ReadAllText("Assets/_Project/Scripts/UI/StatusStrip.cs");
            StringAssert.Contains("list.Add(BuildCost.ScrapShortLine);", ss);
        }
    }
}
