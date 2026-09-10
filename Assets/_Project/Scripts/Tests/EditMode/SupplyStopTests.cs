using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 공급 정지 표시의 규칙 (UI 문서 12장 · `260909_W01` 3장).
    ///
    /// 여기서 재는 것은 **자리와 참·거짓**이다. 색과 픽셀은 화면에서 사람이 본다.
    /// </summary>
    public sealed class SupplyStopTests
    {
        private const float D = 0.001f;

        // ---- 상단 경고 띠의 자리 (12-2) ----

        /// <summary>
        /// 기준 해상도(세로 2560)에서는 문서 값이 그대로 나온다 — 768에서 시작해 96 높이.
        /// </summary>
        [Test]
        public void BandRect_AtDesignResolution_IsDocumentValues()
        {
            Rect band = SupplyStopRules.BandRect(1440f, 2560f);

            Assert.AreEqual(768f, band.y, D, "세로 768부터");
            Assert.AreEqual(96f, band.height, D, "높이 96 — 격자 반 칸");
            Assert.AreEqual(1440f, band.width, D, "가로는 창 전체");
            Assert.AreEqual(0f, band.x, D);
        }

        /// <summary>
        /// **창이 작아지면 띠도 같이 줄어든다.** 픽셀 값을 박아 두면 웹빌드 기본 창(600)에서
        /// 띠가 화면 밖으로 나가거나 화면을 다 덮는다 — 768은 2560 캔버스의 값이다.
        /// </summary>
        [Test]
        public void BandRect_ScalesWithWindow_NotFixedPixels()
        {
            Rect small = SupplyStopRules.BandRect(960f, 600f);

            Assert.AreEqual(600f * 768f / 2560f, small.y, D, "비율로 앉는다");
            Assert.AreEqual(600f * 96f / 2560f, small.height, D);
            Assert.Less(small.yMax, 600f, "띠가 창 안에 들어온다");
        }

        // ---- 무엇이 경고인가 ----

        /// <summary>
        /// **없는 값과 0을 가른다.** 전투가 아직 안 돌면 마운트도 창고도 0인데, 그것을
        /// 「탄약이 떨어졌다」로 읽으면 씬을 열자마자 경고가 뜬다.
        /// </summary>
        [Test]
        public void NoCombat_IsNotEmpty_EvenThoughValuesAreZero()
        {
            Assert.IsFalse(SupplyStopRules.MountIsEmpty(hasCombat: false, mountTotal: 0f));
            Assert.IsFalse(SupplyStopRules.StorageIsEmpty(hasCombat: false, storageStock: 0f));

            Assert.IsTrue(SupplyStopRules.MountIsEmpty(hasCombat: true, mountTotal: 0f),
                "전투가 돌고 있으면 0은 사건이다");
            Assert.IsTrue(SupplyStopRules.StorageIsEmpty(hasCombat: true, storageStock: 0f));
        }

        [Test]
        public void Stock_AboveZero_IsNotEmpty()
        {
            Assert.IsFalse(SupplyStopRules.MountIsEmpty(true, 0.5f));
            Assert.IsFalse(SupplyStopRules.StorageIsEmpty(true, 1f));
        }

        /// <summary>
        /// 전력 부족 = 사용률 100% 초과. **변수 패널의 빨간 점멸과 같은 판정**이라
        /// 두 화면이 서로 다른 순간에 켜지지 않는다.
        /// </summary>
        [Test]
        public void PowerIsShort_MatchesUsageOverBand()
        {
            Assert.IsFalse(SupplyStopRules.PowerIsShort(supply: 10f, draw: 5f), "50%");
            Assert.IsFalse(SupplyStopRules.PowerIsShort(supply: 10f, draw: 10f), "100%는 아직 아니다");
            Assert.IsTrue(SupplyStopRules.PowerIsShort(supply: 10f, draw: 11f), "110%");
        }

        /// <summary>
        /// **공급이 없는데 수요가 있으면 부족이다.** 이 자리는 나눌 수 없어 사용률이 `NaN`인데,
        /// NaN을 그냥 흘리면 어느 띠에도 안 걸려 **조용히 안 뜬다.**
        /// </summary>
        [Test]
        public void NoSupply_WithDemand_IsShort_ButIdleBoardIsNot()
        {
            Assert.IsTrue(SupplyStopRules.PowerIsShort(supply: 0f, draw: 3f),
                "발전이 없는데 먹는 노드가 있다");
            Assert.IsFalse(SupplyStopRules.PowerIsShort(supply: 0f, draw: 0f),
                "둘 다 0 — 노드를 놓기 전이다. 경고가 아니다");
        }

        // ---- 띠가 뜨는가 · 무엇을 적는가 ----

        [Test]
        public void Band_ShowsOnlyWhenThereIsAWarning()
        {
            Assert.IsFalse(SupplyStopRules.BandIsVisible(false, false), "없으면 자리를 안 먹는다");
            Assert.IsTrue(SupplyStopRules.BandIsVisible(true, false));
            Assert.IsTrue(SupplyStopRules.BandIsVisible(false, true));
        }

        /// <summary>
        /// **문구는 하나다** (`260910_W01` 4장 2번). 경고가 무엇이든 같은 말을 적는다 —
        /// **0차는 원인을 말하지 않는다.**
        ///
        /// ⚠️ 구 시험 폐기 — 「둘 다일 때 둘 다 적는다」를 붙들고 있었는데,
        /// **문구가 하나가 되면서 그 물음 자체가 사라졌다.**
        /// </summary>
        [Test]
        public void BandText_SaysOnlyThatItStopped_NeverWhy()
        {
            StringAssert.Contains("멈췄", SupplyStopRules.BandText);

            // 원인을 말하는 낱말이 들어가면 0차가 1차에서 3차의 일을 대신하게 된다.
            StringAssert.DoesNotContain("전력", SupplyStopRules.BandText);
            StringAssert.DoesNotContain("탄약", SupplyStopRules.BandText);
            StringAssert.DoesNotContain("재고", SupplyStopRules.BandText);
        }

        // ---- 탄약 줄 0 표기 (3-3) ----

        /// <summary>
        /// **0인 탄종도 칸을 유지한다.** 값이 있는 칸은 남은 폭을 나눠 갖는다.
        /// </summary>
        [Test]
        public void EmptySegments_ReserveWidth_LeavingTheRestToFilled()
        {
            float filled = HudMeters.FilledWidth(300f, emptyCount: 1);

            Assert.AreEqual(300f - 300f * HudMeters.EmptySegmentShare, filled, D);
            Assert.Less(filled, 300f, "0인 칸이 자리를 떼어 간다");
            Assert.AreEqual(300f * HudMeters.EmptySegmentShare,
                HudMeters.EmptySegmentWidth(300f, 1, anyFilled: true), D);
        }

        /// <summary>
        /// **셋 다 0이면 고르게 나눈다.** 떼어 갈 몫이 폭을 넘는 자리이며, 안 막으면
        /// 남는 폭이 음수가 되어 칸이 뒤집힌다.
        /// </summary>
        [Test]
        public void AllEmpty_SplitsEvenly_AndNeverGoesNegative()
        {
            Assert.AreEqual(0f, HudMeters.FilledWidth(300f, emptyCount: 10), D,
                "떼어 갈 몫이 폭을 넘어도 0에서 멈춘다");
            Assert.GreaterOrEqual(HudMeters.FilledWidth(300f, 100), 0f);

            Assert.AreEqual(100f, HudMeters.EmptySegmentWidth(300f, 3, anyFilled: false), D,
                "값이 있는 칸이 없으면 셋이 고르게 나눈다");
        }

        [Test]
        public void NoEmptySegments_ChangesNothing()
        {
            Assert.AreEqual(300f, HudMeters.FilledWidth(300f, emptyCount: 0), D);
            Assert.AreEqual(0f, HudMeters.EmptySegmentWidth(300f, 0, anyFilled: true), D);
        }

        // ---- 점멸 박자 ----

        /// <summary>
        /// 점멸은 **한 박자만 있다** — 변수 패널 · 전투 배지 · 마운트 그리드가 같이 깜빡인다.
        /// 갈리면 같은 「안 된다」가 서로 다른 사건으로 읽힌다.
        /// </summary>
        [Test]
        public void Blink_HasOneRhythm_AndActuallyToggles()
        {
            Assert.IsTrue(HudMeters.BlinkOn(0f));
            Assert.IsFalse(HudMeters.BlinkOn(0.5f), "초당 2.5번 — 0.4초에 한 번 뒤집힌다");
            Assert.IsTrue(HudMeters.BlinkOn(0.9f));
        }

        // ---- 다리 ----

        /// <summary>
        /// 신호 채널은 **숫자를 나를 뿐 판정하지 않는다.** 되돌리면 「전투가 없다」로 돌아간다.
        /// </summary>
        [Test]
        public void Signals_ResetGoesBackToNoCombat()
        {
            SupplySignals.HasCombat = true;
            SupplySignals.MountTotal = 12f;
            SupplySignals.StorageStock = 34f;

            SupplySignals.Reset();

            Assert.IsFalse(SupplySignals.HasCombat);
            Assert.AreEqual(0f, SupplySignals.MountTotal, D);
            Assert.AreEqual(0f, SupplySignals.StorageStock, D);
        }
    }
}
