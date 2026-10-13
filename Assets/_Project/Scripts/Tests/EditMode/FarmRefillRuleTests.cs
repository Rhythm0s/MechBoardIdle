using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **상주 파밍 보충 규칙** (2026-10-13 사용자 확정 #111·#112 · 순서 2).
    ///
    /// 규칙 한 줄 — 종류마다 「있어야 할 수 − 지금 산 수」만큼 채운다.
    /// 정원은 표의 `Weight` 합이다(따로 세지 않는다 · 지침 §7).
    /// </summary>
    public sealed class FarmRefillRuleTests
    {
        private static StageComposition E(string kind, int weight, float hp = 10f, float def = 0f) =>
            new StageComposition { enemyKey = kind, count = weight, hp = hp, def = def };

        /// <summary>명세의 S3 줄 — 보병 14 · 포격 2 · 장갑 1 = 정원 17.</summary>
        private static List<StageComposition> S3() => new List<StageComposition>
        {
            E("보병", 14, 35f, 3f), E("포격", 2, 105f, 3f), E("장갑", 1, 600f, 3f),
        };

        [Test]
        public void 정원은_Weight_합이다()
        {
            Assert.AreEqual(17, FarmRefillRule.CapacityOf(S3()));
            Assert.AreEqual(0, FarmRefillRule.CapacityOf(null));
            Assert.AreEqual(0, FarmRefillRule.CapacityOf(new List<StageComposition>()));
        }

        [Test]
        public void 빈_판이면_정원만큼_채운다()
        {
            var plan = new List<FarmRefillRule.Refill>();
            FarmRefillRule.Plan(S3(), null, plan);

            Assert.AreEqual(3, plan.Count, "종류 셋이 다 나와야 한다");
            Assert.AreEqual(17, FarmRefillRule.TotalOf(plan), "정원만큼 채운다");
        }

        [Test]
        public void 산_만큼_빼고_채운다()
        {
            var alive = new Dictionary<string, int> { { "보병", 10 }, { "포격", 2 } };
            var plan = new List<FarmRefillRule.Refill>();
            FarmRefillRule.Plan(S3(), alive, plan);

            // 보병 14−10 = 4 · 포격 2−2 = 0(안 나온다) · 장갑 1−0 = 1
            Assert.AreEqual(2, plan.Count, "다 찬 종류는 줄에서 빠진다");
            Assert.AreEqual(5, FarmRefillRule.TotalOf(plan));
        }

        [Test]
        public void 다_차_있으면_아무것도_안_넣는다()
        {
            var alive = new Dictionary<string, int> { { "보병", 14 }, { "포격", 2 }, { "장갑", 1 } };
            var plan = new List<FarmRefillRule.Refill>();
            FarmRefillRule.Plan(S3(), alive, plan);

            Assert.IsEmpty(plan);
            Assert.AreEqual(0, FarmRefillRule.TotalOf(plan));
        }

        /// <summary>
        /// ⚠️ **넘쳐도 안 지운다.** 표가 줄었거나 도전에서 돌아온 직후에 그럴 수 있다 —
        /// 채우는 규칙이지 맞추는 규칙이 아니다. 지우면 눈앞의 적이 까닭 없이 사라진다.
        /// </summary>
        [Test]
        public void 넘쳐도_안_지운다()
        {
            var alive = new Dictionary<string, int> { { "보병", 99 } };
            var plan = new List<FarmRefillRule.Refill>();
            FarmRefillRule.Plan(S3(), alive, plan);

            foreach (FarmRefillRule.Refill r in plan)
                Assert.Greater(r.count, 0, "음수를 넣으려 든다");

            Assert.AreEqual(3, FarmRefillRule.TotalOf(plan), "포격 2 + 장갑 1 만 채운다");
        }

        /// <summary>⚠️ **결정론** — 같은 상태면 같은 답이다. 난수가 없다.</summary>
        [Test]
        public void 같은_상태면_같은_답이다()
        {
            var alive = new Dictionary<string, int> { { "보병", 3 } };
            var a = new List<FarmRefillRule.Refill>();
            var b = new List<FarmRefillRule.Refill>();

            FarmRefillRule.Plan(S3(), alive, a);
            FarmRefillRule.Plan(S3(), alive, b);

            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].entry.enemyKey, b[i].entry.enemyKey, $"{i} 번째 종류");
                Assert.AreEqual(a[i].count, b[i].count, $"{i} 번째 수");
            }
        }

        /// <summary>체력·방어를 **그 줄에서 들고 간다** — 스폰하는 쪽이 다시 안 찾는다(§7).</summary>
        [Test]
        public void 체력과_방어를_들고_간다()
        {
            var plan = new List<FarmRefillRule.Refill>();
            FarmRefillRule.Plan(S3(), null, plan);

            FarmRefillRule.Refill armor = plan.Find(r => r.entry.enemyKey == "장갑");
            Assert.AreEqual(600f, armor.entry.hp, 0.001f);
            Assert.AreEqual(3f, armor.entry.def, 0.001f);
        }

        /// <summary>빈 줄·이름 없는 줄은 건너뛴다 — 표가 덜 채워져 있어도 안 죽는다.</summary>
        [Test]
        public void 빈_줄은_건너뛴다()
        {
            var roster = new List<StageComposition>
            {
                E("보병", 5), E("", 3), E("포격", 0), E(null, 2),
            };
            var plan = new List<FarmRefillRule.Refill>();
            FarmRefillRule.Plan(roster, null, plan);

            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(5, FarmRefillRule.TotalOf(plan));
            Assert.AreEqual(5, FarmRefillRule.CapacityOf(roster), "정원도 빈 줄을 안 센다");
        }
    }
}
