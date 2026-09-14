using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **재배분 게이트** (2026-09-15 · 육안 ① 결함).
    ///
    /// ⚠️ **이 시험이 막는 것.** 게이트가 전역 배율 하나만 보던 동안, 배분 함수를 탄종별로
    /// 바꿔도 **그 함수가 안 불렸다.** 전투 시작 시점 도착률이 0 이라 라인 0 줄로 굳었고
    /// 60 초 동안 **0 발**이 나갔다(매 틱 재배분 대조군은 같은 판에서 54 발).
    /// </summary>
    public sealed class FireRateGateTests
    {
        private const float Eps = 0.001f;

        private static float Zero(AmmoKind k) => 0f;

        [Test]
        public void 배율이_평평해도_도착률이_오르면_다시_배분한다()
        {
            var gate = new FireRateGate();
            gate.Reset(1f);

            // 전투 시작 — 아직 아무것도 안 왔다.
            Assert.That(gate.ShouldReallocate(1f, Zero, Zero, Eps), Is.False,
                "변한 것이 없으면 다시 안 배분한다");

            // 벨트가 닿기 시작했다. **배율은 그대로 1 이다** — 만공급이라 평평하다.
            float arrival = 4f;
            Assert.That(gate.ShouldReallocate(1f, k => k == AmmoKind.Standard ? arrival : 0f, Zero, Eps),
                Is.True, "도착률이 0 → 4 로 올랐으면 줄이 다시 서야 한다");
        }

        [Test]
        public void 같은_값을_다시_물으면_거짓이다()
        {
            var gate = new FireRateGate();
            gate.Reset(1f);

            System.Func<AmmoKind, float> arrival = k => k == AmmoKind.Standard ? 4f : 0f;
            Assert.That(gate.ShouldReallocate(1f, arrival, Zero, Eps), Is.True);
            Assert.That(gate.ShouldReallocate(1f, arrival, Zero, Eps), Is.False,
                "참을 돌려준 순간 기준을 새로 새긴다 — 안 그러면 매 프레임 재배분이다");
        }

        [Test]
        public void 재고가_생기거나_떨어지는_순간을_잡는다()
        {
            var gate = new FireRateGate();
            gate.Reset(1f);

            float stock = 0f;
            System.Func<AmmoKind, float> stockOf = k => k == AmmoKind.Standard ? stock : 0f;

            Assert.That(gate.ShouldReallocate(1f, Zero, stockOf, Eps), Is.False);

            stock = 40f;   // 창고에서 실렸다
            Assert.That(gate.ShouldReallocate(1f, Zero, stockOf, Eps), Is.True,
                "재고가 생기면 스펙 상한으로 올라가야 한다(발사 규칙 (가))");

            stock = 39f;   // 한 발 썼다
            Assert.That(gate.ShouldReallocate(1f, Zero, stockOf, Eps), Is.False,
                "재고는 있다/없다만 규칙에 든다 — 줄어드는 것마다 배분하면 매 프레임이다");

            stock = 0f;    // 다 썼다
            Assert.That(gate.ShouldReallocate(1f, Zero, stockOf, Eps), Is.True,
                "재고가 마르면 공급율 상한으로 내려가야 한다");
        }

        [Test]
        public void 배율_변화는_종전대로_잡는다()
        {
            var gate = new FireRateGate();
            gate.Reset(1f);

            Assert.That(gate.ShouldReallocate(1f, Zero, Zero, Eps), Is.False);
            Assert.That(gate.ShouldReallocate(0.5f, Zero, Zero, Eps), Is.True,
                "노드를 뽑아 출력이 떨어지면 종전처럼 다시 배분한다");
        }

        [Test]
        public void 문턱보다_작은_흔들림은_무시한다()
        {
            var gate = new FireRateGate();
            gate.Reset(1f);

            Assert.That(gate.ShouldReallocate(1f, Zero, Zero, Eps), Is.False);
            Assert.That(gate.ShouldReallocate(1.0005f, Zero, Zero, Eps), Is.False,
                "0.001 미만은 값이 변한 것으로 안 본다");
        }

        [Test]
        public void 전투가_새로_서면_기준도_새로_선다()
        {
            var gate = new FireRateGate();
            System.Func<AmmoKind, float> arrival = k => 4f;

            Assert.That(gate.ShouldReallocate(1f, arrival, Zero, Eps), Is.True);
            Assert.That(gate.ShouldReallocate(1f, arrival, Zero, Eps), Is.False);

            gate.Reset(1f);
            Assert.That(gate.ShouldReallocate(1f, arrival, Zero, Eps), Is.True,
                "새 전투는 도착률 0 에서 시작한다 — 기준이 남아 있으면 첫 줄이 안 선다");
        }
    }
}
