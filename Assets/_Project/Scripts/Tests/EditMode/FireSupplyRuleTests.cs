using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **발사 규칙 (가)** — 재고가 있으면 스펙대로, 비면 공급이 상한
    /// (2026-09-15 사용자 확정 · 육안 ①).
    ///
    /// ⚠️ **왜 이 시험이 있는가.** 09-15 육안에서 창고 40/40 · 마운트 40 · 적 96 기인데
    /// 78 초 동안 한 발도 안 나갔다. 원인이 둘이었고 둘 다 여기서 못 박는다 —
    /// ① 배분이 **도착률(흐름)만** 보아 실탄을 지고도 줄이 안 섰다.
    /// ② 재배분 **게이트**가 배율만 보아 도착률이 올라도 다시 안 섰다(`StageRunnerFireGateTests`).
    /// </summary>
    public sealed class FireSupplyRuleTests
    {
        // 라인 스펙(lineSpecShots) — 관통 5 · 표준 6 · 폭발 2.
        private static List<WeaponSpec> Spec() => new List<WeaponSpec>
        {
            new WeaponSpec(AmmoKind.Pierce, 20f, 5f),
            new WeaponSpec(AmmoKind.Standard, 10f, 6f),
            new WeaponSpec(AmmoKind.Explosive, 50f, 2f),
        };

        private static float Rate(List<AmmoLine> lines, AmmoKind kind)
        {
            foreach (AmmoLine l in lines)
                if (l.kind == kind) return l.shotsPerSec;
            return 0f;
        }

        [Test]
        public void 재고가_있으면_도착이_멎어도_스펙대로_쏜다()
        {
            var into = new List<AmmoLine>();

            // 마운트에 표준탄이 쌓여 있고 벨트는 지금 쉰다 — 육안에서 본 그 상태다.
            ShotAllocator.AllocateRates(Spec(), 6f,
                _ => 0f,                                          // 도착률 0
                k => k == AmmoKind.Standard ? 40f : 0f,           // 표준 재고 40 발
                into);

            Assert.That(Rate(into, AmmoKind.Standard), Is.EqualTo(6f).Within(0.001f),
                "재고가 있으면 라인 스펙(6)까지 쏴야 한다");
            Assert.That(Rate(into, AmmoKind.Pierce), Is.EqualTo(0f),
                "재고도 공급도 없는 탄종은 줄이 서면 안 된다");
        }

        [Test]
        public void 재고가_비면_도착률이_상한이다()
        {
            var into = new List<AmmoLine>();

            ShotAllocator.AllocateRates(Spec(), 6f,
                k => k == AmmoKind.Standard ? 1.5f : 0f,          // 표준이 1.5 발/초로 온다
                _ => 0f,                                          // 재고는 비었다
                into);

            Assert.That(Rate(into, AmmoKind.Standard), Is.EqualTo(1.5f).Within(0.001f),
                "재고가 없으면 버는 만큼만 쏜다");
        }

        [Test]
        public void 공급이_스펙을_넘어도_스펙을_안_넘는다()
        {
            var into = new List<AmmoLine>();

            ShotAllocator.AllocateRates(Spec(), 99f,
                _ => 100f,                                        // 넘치게 온다
                _ => 0f,
                into);

            Assert.That(Rate(into, AmmoKind.Explosive), Is.EqualTo(2f).Within(0.001f));
            Assert.That(Rate(into, AmmoKind.Pierce), Is.EqualTo(5f).Within(0.001f));
            Assert.That(Rate(into, AmmoKind.Standard), Is.EqualTo(6f).Within(0.001f));
        }

        [Test]
        public void 재고도_공급도_없으면_줄이_아예_안_선다()
        {
            var into = new List<AmmoLine>();
            ShotAllocator.AllocateRates(Spec(), 6f, _ => 0f, _ => 0f, into);

            Assert.That(into.Count, Is.EqualTo(0),
                "0 발짜리 줄을 넣으면 HUD 가 「쏘는 중」으로 읽는다");
        }

        [Test]
        public void 소비_상한은_그대로_고효율_우선이다()
        {
            var into = new List<AmmoLine>();

            // 전부 재고가 있으니 셋 다 스펙을 원한다(5+6+2 = 13 > cap 6).
            ShotAllocator.AllocateRates(Spec(), 6f, _ => 0f, _ => 40f, into);

            float sum = 0f;
            foreach (AmmoLine l in into) sum += l.shotsPerSec;
            Assert.That(sum, Is.EqualTo(6f).Within(0.001f), "소비 상한을 넘으면 안 된다");

            // 발당피해가 큰 순서(폭발 50 → 관통 20 → 표준 10)로 채운다.
            Assert.That(Rate(into, AmmoKind.Explosive), Is.EqualTo(2f).Within(0.001f));
            Assert.That(Rate(into, AmmoKind.Pierce), Is.EqualTo(4f).Within(0.001f));
            Assert.That(Rate(into, AmmoKind.Standard), Is.EqualTo(0f));
        }

        [Test]
        public void 넘겨받은_함수가_없으면_비운다()
        {
            var into = new List<AmmoLine> { new AmmoLine(AmmoKind.Pierce, 20f, 1f) };
            ShotAllocator.AllocateRates(Spec(), 6f, null, _ => 40f, into);
            Assert.That(into.Count, Is.EqualTo(0));

            into.Add(new AmmoLine(AmmoKind.Pierce, 20f, 1f));
            ShotAllocator.AllocateRates(Spec(), 6f, _ => 1f, null, into);
            Assert.That(into.Count, Is.EqualTo(0));
        }

        // ── 마운트 재고 신호 ────────────────────────────────────────────────

        [Test]
        public void 마운트_재고는_슬롯을_합해서_센다()
        {
            SupplySignals.Reset();
            SupplySignals.EnsureSlots(4);
            SupplySignals.MountSlotItem[0] = MountItem.Standard;
            SupplySignals.MountSlotAmount[0] = 10f;
            SupplySignals.MountSlotItem[1] = MountItem.Standard;   // 같은 탄이 두 슬롯에 나뉜다
            SupplySignals.MountSlotAmount[1] = 7f;
            SupplySignals.MountSlotItem[2] = MountItem.Pierce;
            SupplySignals.MountSlotAmount[2] = 3f;

            Assert.That(SupplySignals.MountStockOf(AmmoKind.Standard), Is.EqualTo(17f).Within(0.001f),
                "슬롯 하나만 보면 모자라게 센다");
            Assert.That(SupplySignals.MountStockOf(AmmoKind.Pierce), Is.EqualTo(3f).Within(0.001f));
            Assert.That(SupplySignals.MountStockOf(AmmoKind.Explosive), Is.EqualTo(0f));

            SupplySignals.Reset();
        }
    }
}
