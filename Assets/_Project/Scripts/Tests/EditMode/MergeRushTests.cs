using MBI.Core;
using MBI.Core.Combat;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **도전 층 합체 연출 — 게이지 차오름 · 합체 원형 펄스** (사용자 확정 #160 · 2026-10-16).
    /// </summary>
    public sealed class MergeRushTests
    {
        [Test]
        public void 합체가_있는_도전_층에만_건다()
        {
            Assert.IsTrue(MergeRushRule.AppliesTo(true, true));
            Assert.IsFalse(MergeRushRule.AppliesTo(false, true), "파밍 층은 90초 규칙 그대로");
            Assert.IsFalse(MergeRushRule.AppliesTo(true, false), "로봇 B 해금 전(합체 없음)은 연출도 없다");
        }

        [Test]
        public void 차오름은_3초에_만충이고_자연_충전보다_낮지_않다()
        {
            float full = MergeSystem.GaugeFullSeconds;
            Assert.AreEqual(9f, MergeRushRule.ChargeAt(9f, 0f, 9f), 1e-3f);
            Assert.AreEqual(9f + (full - 9f) * 0.5f, MergeRushRule.ChargeAt(9f, 1.5f, 10f), 1e-3f);
            Assert.AreEqual(full, MergeRushRule.ChargeAt(9f, 3f, 12f), 1e-3f);
            Assert.AreEqual(full, MergeRushRule.ChargeAt(0f, 10f, 0f), 1e-3f);
            Assert.AreEqual(50f, MergeRushRule.ChargeAt(0f, 0.1f, 50f), 1e-3f);
        }

        [Test]
        public void 펄스는_1과_115_사이를_06초에_한_번_오간다()
        {
            Assert.AreEqual(1f, MergeRushRule.PulseScale(0f), 1e-4f);
            Assert.AreEqual(1.15f, MergeRushRule.PulseScale(0.3f), 1e-4f);
            Assert.AreEqual(1f, MergeRushRule.PulseScale(0.6f), 1e-3f);
            for (float t = 0f; t < 2f; t += 0.05f)
            {
                float k = MergeRushRule.PulseScale(t);
                Assert.That(k, Is.InRange(1f - 1e-4f, 1.15f + 1e-4f));
            }
        }

        [Test]
        public void 키운_자리는_중심이_그대로다()
        {
            var r = new Rect(100f, 200f, 80f, 80f);
            Rect s = MergeRushRule.Scaled(r, 1.15f);
            Assert.AreEqual(r.center.x, s.center.x, 1e-3f);
            Assert.AreEqual(r.center.y, s.center.y, 1e-3f);
            Assert.AreEqual(92f, s.width, 1e-3f);
        }

        [Test]
        public void 러너는_판이_설_때_차오름을_걸고_합체_원형에_펄스를_건다()
        {
            string src = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/StageRunner.cs");
            StringAssert.Contains("MergeRushRule.AppliesTo(challengeLayer, _sim.Merge != null)", src);
            StringAssert.Contains("TickMergeRush(Time.deltaTime);", src);
            StringAssert.Contains("if (MergePulseOn) mergeRect = MergeRushRule.Scaled(mergeRect", src);
        }
    }
}
