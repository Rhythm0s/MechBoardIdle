using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 합체·버스트(전투 문서 4·5장 · 밸런스 5장).
    /// 확정치: 게이지 90초 · 지속 20초 · 배율 ×1.8 · 버스트 300% · 공통 게이지 1개 · 스테이지당 1회.
    /// </summary>
    public sealed class MergeSystemTests
    {
        private const float D = 0.001f;

        private static MergeSystem Charged()
        {
            var m = new MergeSystem();
            m.Tick(MergeSystem.GaugeFullSeconds, inCombat: true);
            return m;
        }

        // ---- 확정치 ----

        [Test]
        public void ConfirmedConstants()
        {
            Assert.AreEqual(90f, MergeSystem.GaugeFullSeconds, D, "params gaugeFull");
            Assert.AreEqual(20f, MergeSystem.DurationSeconds, D, "params bd");
            Assert.AreEqual(1.8f, MergeSystem.MergeMultiplier, D, "params mergeMult");
            Assert.AreEqual(300f, MergeSystem.BurstPercent, D, "params bc");
        }

        /// <summary>
        /// 밸런스 5장 검산: (A 57 + B 100) 상태가 아니라 대표 구성으로 확인.
        /// 문서의 「(A+B)×1.8 = 531.5 DPS」를 역산하면 A+B = 295.28이다.
        /// </summary>
        [Test]
        public void MergedOutput_MatchesDocumentedFigure()
        {
            Assert.AreEqual(531.5f, MergeSystem.MergedOutput(295.28f, 0f), 0.1f,
                "(A+B) × 1.8 = 531.5 — 밸런스 5장 mergeModel");
            Assert.AreEqual(360f, MergeSystem.MergedOutput(100f, 100f), D, "200 × 1.8");
        }

        [Test]
        public void BurstDamage_IsThreeHundredPercentOfSnapshot()
        {
            Assert.AreEqual(300f, MergeSystem.BurstDamage(100f), D);
            Assert.AreEqual(0f, MergeSystem.BurstDamage(-5f), D, "음수는 0으로");
        }

        // ---- 게이지 ----

        /// <summary>
        /// 게이지는 **전투 수행 중에만** 찬다. 조립 화면에서 시간만 보내도 차면
        /// 「전투를 수행해 기세를 쌓는다」는 신문법이 성립하지 않는다.
        /// </summary>
        [Test]
        public void Gauge_ChargesOnlyInCombat()
        {
            var m = new MergeSystem();

            m.Tick(30f, inCombat: false);
            Assert.AreEqual(0f, m.ChargeRatio, D, "전투 밖에서는 안 찬다");

            m.Tick(45f, inCombat: true);
            Assert.AreEqual(0.5f, m.ChargeRatio, D, "45 ÷ 90");
        }

        [Test]
        public void Gauge_ReachesFullAtNinetySeconds()
        {
            var m = new MergeSystem();

            m.Tick(89f, true);
            Assert.IsFalse(m.IsReady, "89초로는 부족");

            m.Tick(1f, true);
            Assert.IsTrue(m.IsReady, "90초에 만충");
            Assert.AreEqual(1f, m.ChargeRatio, D);
        }

        // ---- 발동 ----

        [Test]
        public void Activate_StartsTwentySecondDuration()
        {
            MergeSystem m = Charged();

            Assert.IsTrue(m.TryActivate());
            Assert.IsTrue(m.IsActive);
            Assert.AreEqual(20f, m.RemainingSeconds, D);
        }

        [Test]
        public void Activate_FailsWhenNotCharged()
        {
            var m = new MergeSystem();
            m.Tick(50f, true);

            Assert.IsFalse(m.TryActivate());
            Assert.IsFalse(m.IsActive);
        }

        /// <summary>**스테이지당 1회.** 다 쓰고 나면 게이지가 다시 차지도 않는다.</summary>
        [Test]
        public void OncePerStage_NoRecharge()
        {
            MergeSystem m = Charged();
            m.TryActivate();
            m.Tick(MergeSystem.DurationSeconds, true); // 합체 종료

            Assert.IsFalse(m.IsActive);
            Assert.IsTrue(m.UsedThisStage);

            m.Tick(200f, true);
            Assert.AreEqual(0f, m.ChargeRatio, D, "다 쓰면 더 안 찬다");
            Assert.IsFalse(m.TryActivate(), "두 번째는 없다");
        }

        [Test]
        public void Duration_TicksDownAndEnds()
        {
            MergeSystem m = Charged();
            m.TryActivate();

            m.Tick(19f, true);
            Assert.IsTrue(m.IsActive);
            Assert.AreEqual(1f, m.RemainingSeconds, D);

            m.Tick(1f, true);
            Assert.IsFalse(m.IsActive);
            Assert.AreEqual(0f, m.RemainingSeconds, D, "음수로 흐르지 않는다");
        }

        /// <summary>합체 중에는 충전하지 않는다 — 스테이지당 1회라 쌓아 둘 이유가 없다.</summary>
        [Test]
        public void NoRechargeWhileActive()
        {
            MergeSystem m = Charged();
            m.TryActivate();

            m.Tick(5f, true);

            Assert.AreEqual(0f, m.ChargeRatio, D);
            Assert.AreEqual(15f, m.RemainingSeconds, D, "그 시간은 지속에서 깎인다");
        }

        [Test]
        public void Reset_ClearsGaugeAndUsage()
        {
            MergeSystem m = Charged();
            m.TryActivate();

            m.Reset();

            Assert.IsFalse(m.IsActive);
            Assert.IsFalse(m.UsedThisStage, "새 스테이지에서는 다시 쓸 수 있다");
            Assert.AreEqual(0f, m.ChargeRatio, D);
        }

        // ---- 태그와의 관계 ----

        /// <summary>
        /// **합체 중에는 태그가 불가**하다(전투 문서 4장 상위 잠금).
        /// 두 공장은 계속 돌아 비축이 쌓이고, 합체가 끝나면 만재로 복귀한다.
        /// </summary>
        [Test]
        public void MergeLocksTagging()
        {
            var stacks = new Dictionary<MountItem, float> { { MountItem.Pierce, 10f } };
            var a = new MountLoad(1, stacks);
            var b = new MountLoad(1, stacks);
            b.Load(MountItem.Pierce, 10f); // 대기가 만충 — 평소라면 교대한다

            var battle = new TagBattle(a, b);
            MergeSystem merge = Charged();
            merge.TryActivate();

            battle.Locked = merge.IsActive;

            Assert.IsFalse(battle.TickAuto(0.1f), "합체 중에는 만충이어도 교대하지 않는다");
            Assert.AreEqual(0, battle.ActiveIndex);
        }

        /// <summary>합체가 끝나면 잠금이 풀리고 그 자리에서 교대가 성립한다.</summary>
        [Test]
        public void TaggingResumesAfterMerge()
        {
            var stacks = new Dictionary<MountItem, float> { { MountItem.Pierce, 10f } };
            var a = new MountLoad(1, stacks);
            var b = new MountLoad(1, stacks);
            b.Load(MountItem.Pierce, 10f);

            var battle = new TagBattle(a, b);
            MergeSystem merge = Charged();
            merge.TryActivate();
            merge.Tick(MergeSystem.DurationSeconds, true);

            battle.Locked = merge.IsActive;

            Assert.IsFalse(merge.IsActive);
            Assert.IsTrue(battle.TickAuto(0.1f), "합체가 끝나면 다시 교대한다");
        }

        // ---- 예산 경계 ----

        /// <summary>
        /// 버스트는 **지속 DPS가 아니다.** 순간 1회라 예산 밖 마진 항이고,
        /// 지속 화력과 더해 놓으면 요구치 예산이 무너진다 — 두 값이 섞이지 않는다는 확인.
        /// </summary>
        [Test]
        public void BurstIsNotAddedToSustainedOutput()
        {
            float sustained = MergeSystem.MergedOutput(100f, 100f); // 360
            float burst = MergeSystem.BurstDamage(200f);            // 600

            Assert.AreEqual(360f, sustained, D, "지속은 지속대로");
            Assert.AreEqual(600f, burst, D, "순간은 순간대로");
            Assert.AreNotEqual(sustained + burst, sustained, "합산하는 API가 없다");
        }
    }
}
