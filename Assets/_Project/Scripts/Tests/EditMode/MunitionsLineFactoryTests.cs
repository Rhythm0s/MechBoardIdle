using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 탄약 줄 짓기 — **재는 쪽과 노는 쪽이 같은 함수를 쓴다** (2026-09-16 · 사용자 결정 §74-6 ②).
    ///
    /// ⚠️⚠️ **왜 빼냈나.** 출력은 이 줄들에서 나오는데 짓는 코드가
    /// `LogisticsOutputProvider`(MonoBehaviour) 안에 private 으로 있었다. 그래서 EditMode
    /// 하네스가 **요구치 18(출력 축)을 못 쟀고**, 「못 잰다」고 적는 수밖에 없었다.
    ///
    /// 📌 여기서 지키는 것은 **「스펙 0 인 탄종은 줄을 안 만든다」**와 **「폴백 1 은 값이 아니다」**
    /// 둘이다. 둘 다 합계를 조용히 틀리게 만드는 자리다.
    /// </summary>
    public sealed class MunitionsLineFactoryTests
    {
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (Object o in _made) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private BalanceConfig Balance(float pierce, float standard, float explosive, float perNode)
        {
            var b = ScriptableObject.CreateInstance<BalanceConfig>();
            b.lineSpecShots = new Vector3(pierce, standard, explosive);
            b.muniPerNode = perNode;
            _made.Add(b);
            return b;
        }

        private RobotDefinition Robot(BalanceConfig bal)
        {
            var r = ScriptableObject.CreateInstance<RobotDefinition>();
            r.balanceRef = bal;
            r.weapons = new List<WeaponSpec>
            {
                new WeaponSpec { kind = AmmoKind.Pierce, damagePerShot = 10f, shotsPerSec = 2f },
                new WeaponSpec { kind = AmmoKind.Standard, damagePerShot = 5f, shotsPerSec = 2f },
                new WeaponSpec { kind = AmmoKind.Explosive, damagePerShot = 25f, shotsPerSec = 4f },
            };
            _made.Add(r);
            return r;
        }

        private static NetworkAggregate Agg(int pierce, int standard, int explosive)
            => new NetworkAggregate { muniPierce = pierce, muniSplit = standard, muniExplosive = explosive };

        [Test]
        public void 탄종마다_줄_하나씩_짓는다()
        {
            var into = new List<MunitionsLine>();
            MunitionsLineFactory.Build(Robot(Balance(5f, 6f, 2f, 1f)), Agg(1, 2, 3), into);

            Assert.That(into.Count, Is.EqualTo(3), "무기 셋이면 줄 셋이다");
        }

        [Test]
        public void 스펙이_0인_탄종은_줄을_안_만든다()
        {
            // ⚠️ 라인 스펙이 없다는 것은 **이 밸런스가 그 탄종을 안 쓴다**는 뜻이다.
            //    0 짜리 줄을 넣으면 합계에 0 이 섞여 평균이 흐려진다.
            var into = new List<MunitionsLine>();
            MunitionsLineFactory.Build(Robot(Balance(5f, 0f, 2f, 1f)), Agg(1, 9, 1), into);

            Assert.That(into.Count, Is.EqualTo(2), "표준 줄이 빠져야 한다");
            foreach (MunitionsLine l in into)
                Assert.That(l.kind, Is.Not.EqualTo(AmmoKind.Standard), "스펙 0 인 탄종이 들어왔다");
        }

        [Test]
        public void 부르기_전의_내용은_지운다()
        {
            // 같은 리스트를 매 프레임 다시 쓰는 것이 제공자의 방식이다 — 안 비우면 쌓인다.
            var into = new List<MunitionsLine> { new MunitionsLine(AmmoKind.Pierce, 1f, 1f, 1) };
            MunitionsLineFactory.Build(Robot(Balance(5f, 6f, 2f, 1f)), Agg(1, 1, 1), into);

            Assert.That(into.Count, Is.EqualTo(3), "앞의 것이 남아 쌓였다");
        }

        [Test]
        public void 노드가_없으면_출력이_0이다()
        {
            // 보드에 군수 노드를 하나도 안 놓으면 만들 것이 없다.
            float output = MunitionsLineFactory.BaseOutput(
                Robot(Balance(5f, 6f, 2f, 1f)), Agg(0, 0, 0), new List<MunitionsLine>());

            Assert.That(output, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void 노드가_늘면_출력이_는다()
        {
            RobotDefinition r = Robot(Balance(5f, 6f, 2f, 1f));
            var scratch = new List<MunitionsLine>();

            float one = MunitionsLineFactory.BaseOutput(r, Agg(1, 1, 1), scratch);
            float two = MunitionsLineFactory.BaseOutput(r, Agg(2, 2, 2), scratch);

            Assert.That(two, Is.GreaterThan(one), "노드를 늘렸는데 출력이 안 늘었다");
        }

        [Test]
        public void 밸런스가_없으면_대당_1로_본다()
        {
            // ⚠️ 폴백 1 은 **값이 아니라 안전장치**다 — 자산이 없을 때 출력이 0 이 되어
            //    「보드가 아무것도 안 만든다」로 잘못 읽히는 것을 막는다.
            var r = ScriptableObject.CreateInstance<RobotDefinition>();
            _made.Add(r);

            Assert.That(MunitionsLineFactory.PerNodeRate(r), Is.EqualTo(1f).Within(0.001f));
            Assert.That(MunitionsLineFactory.PerNodeRate(null), Is.EqualTo(1f).Within(0.001f));
        }
    }
}
