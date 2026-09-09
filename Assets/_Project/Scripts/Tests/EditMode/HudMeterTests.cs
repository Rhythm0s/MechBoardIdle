using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// HUD 막대 규칙(2026-09-09 · UI 문서 3-3 · 11-3).
    ///
    /// **여기서 지키는 것은 「값이 없는 자리」다.** 0으로 나뉘는 경우가 셋이고,
    /// 그때 0%나 100%를 적으면 **없는 값이 있는 값처럼 읽힌다** — 화면이 거짓말을 한다.
    /// </summary>
    public sealed class HudMeterTests
    {
        // ── 사용률 ────────────────────────────────────────────────────────────────

        [Test]
        public void PowerUsage_IsDemandOverSupply_NotEfficiency()
        {
            // 수요 130 · 공급 100이면 **130%**다. 효율이었다면 100%로 잘려 여유가 안 보인다.
            Assert.AreEqual(1.3f, HudMeters.PowerUsage(100f, 130f), 1e-4f);
            Assert.AreEqual(0.5f, HudMeters.PowerUsage(100f, 50f), 1e-4f);
        }

        [Test]
        public void PowerUsage_WithoutSupply_IsUndefined_NotZero()
        {
            float usage = HudMeters.PowerUsage(0f, 12f);
            Assert.IsNaN(usage, "공급이 0이면 나눌 수가 없다 — 0으로 덮으면 「안 쓰는 중」과 같아진다");
            Assert.AreEqual(HudMeters.UsageBand.Undefined, HudMeters.BandOf(usage));
        }

        [Test]
        public void UsageText_ShowsDashWhenThereIsNoValue()
        {
            Assert.AreEqual("—", HudMeters.UsageText(HudMeters.PowerUsage(0f, 0f)));
            Assert.AreEqual("—", HudMeters.UsageText(HudMeters.PowerUsage(0f, 8f)));
            Assert.AreEqual("85%", HudMeters.UsageText(HudMeters.PowerUsage(100f, 85f)));
        }

        [Test]
        public void UsageBands_SplitAtEightyAndNinetyAndOneHundred()
        {
            Assert.AreEqual(HudMeters.UsageBand.Normal, HudMeters.BandOf(0.799f));
            Assert.AreEqual(HudMeters.UsageBand.Caution, HudMeters.BandOf(0.80f));
            Assert.AreEqual(HudMeters.UsageBand.Caution, HudMeters.BandOf(0.899f));
            Assert.AreEqual(HudMeters.UsageBand.Warning, HudMeters.BandOf(0.90f));
            Assert.AreEqual(HudMeters.UsageBand.Warning, HudMeters.BandOf(1.00f), "딱 100%는 아직 초과가 아니다");
            Assert.AreEqual(HudMeters.UsageBand.Over, HudMeters.BandOf(1.001f));
        }

        [Test]
        public void UsageFill_EmptyWhenNothingIsKnown_FullWhenSupplyIsGone()
        {
            float none = HudMeters.PowerUsage(0f, 0f);
            Assert.AreEqual(0f, HudMeters.UsageFill(none, 0f), 1e-4f, "공급도 수요도 없으면 빈 막대다");

            float starved = HudMeters.PowerUsage(0f, 20f);
            Assert.AreEqual(1f, HudMeters.UsageFill(starved, 20f), 1e-4f,
                "공급이 0인데 수요가 있으면 모자람이 극에 달한 것이다 — 가득 채운다");

            Assert.AreEqual(1f, HudMeters.UsageFill(2.5f, 250f), 1e-4f, "250%도 길이로는 가득까지다");
        }

        // ── 회피 눈금 ─────────────────────────────────────────────────────────────

        [Test]
        public void TickCount_FollowsCapacity_WhichIsTwoPerBooster()
        {
            Assert.AreEqual(0, HudMeters.TickCount(0), "부스터가 없으면 칸도 없다");
            Assert.AreEqual(2, HudMeters.TickCount(2));
            Assert.AreEqual(10, HudMeters.TickCount(10));
        }

        [Test]
        public void TickCount_StopsAtTen_AndSaysSoWithATag()
        {
            Assert.AreEqual(10, HudMeters.TickCount(14));
            Assert.AreEqual("10+", HudMeters.OverflowTag(14));
            Assert.AreEqual(string.Empty, HudMeters.OverflowTag(10), "딱 열이면 넘친 것이 아니다");
        }

        [Test]
        public void FilledTicks_NeverExceedTheTicksDrawn()
        {
            Assert.AreEqual(3, HudMeters.FilledTicks(3, 6));
            Assert.AreEqual(6, HudMeters.FilledTicks(99, 6), "스택이 상한을 넘겨도 칸은 다 찬 데까지다");
            Assert.AreEqual(0, HudMeters.FilledTicks(2, 0), "칸이 없으면 채울 것도 없다");
        }

        // ── 탄약 줄 ───────────────────────────────────────────────────────────────

        [Test]
        public void AmmoSegment_WithNoStock_TakesNoCell()
        {
            Assert.IsFalse(HudMeters.SegmentIsVisible(0f), "재고 0인 탄종은 칸을 차지하지 않는다");
            Assert.IsTrue(HudMeters.SegmentIsVisible(0.4f), "한 발도 안 되는 양이라도 있으면 칸이 있다");
        }

        [Test]
        public void AmmoSpan_IsTheCapacitySoAnEmptyTailStays()
        {
            // 40 중 12발이면 막대의 셋 중 하나만 찬다 — 남은 자리가 보여야 한다.
            Assert.AreEqual(40f, HudMeters.SegmentSpan(12f, 40f), 1e-4f);
            // 상한이 없으면 있는 것끼리 나눈다.
            Assert.AreEqual(12f, HudMeters.SegmentSpan(12f, 0f), 1e-4f);
            // 합이 상한을 넘으면 넘은 쪽을 쓴다 — 잘라 내면 넘쳤다는 사실이 사라진다.
            Assert.AreEqual(50f, HudMeters.SegmentSpan(50f, 40f), 1e-4f);
        }

        [Test]
        public void FilledTicks_FoldsProportionallyWhenCapacityExceedsTen()
        {
            // 상한 20에 스택 10이면 절반이라 눈금 다섯이다 — 열 칸을 그리지 않는다.
            Assert.AreEqual(5, HudMeters.FilledTicks(10, 20));
            Assert.AreEqual(10, HudMeters.FilledTicks(20, 20));
            Assert.AreEqual(0, HudMeters.FilledTicks(0, 20));
        }
    }
}
