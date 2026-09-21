using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **두 화면이 서로를 안 그린다** (2026-09-21 사용자 육안 ③).
    ///
    /// 09-16 에 「전투 화면에 보드가 비친다」를 <see cref="BoardLayer"/> 로 갈랐는데
    /// **한쪽만** 갈랐다. 이 시험은 반대 방향을 지킨다.
    /// </summary>
    public sealed class CombatLayerTests
    {
        private const int Others = (1 << 0) | (1 << 5);   // Default · UI

        /// <summary>두 층이 겹치면 한쪽을 끄는 순간 다른 쪽도 꺼진다 — 가장 먼저 볼 것.</summary>
        [Test]
        public void 보드_층과_다른_번호다()
        {
            Assert.AreNotEqual(BoardLayer.Index, CombatLayer.Index,
                "전투 층과 보드 층이 같은 번호다 — 하나를 끄면 둘 다 꺼진다");
        }

        [Test]
        public void 보드_화면이면_전투_층을_끈다()
        {
            int mask = CombatLayer.MaskFor(Others | CombatLayer.Mask, false);
            Assert.That(mask & CombatLayer.Mask, Is.Zero,
                "보드 화면인데 전투 층이 켜져 있다 — 보드 위에 몬스터가 그려진다");
        }

        [Test]
        public void 전투_화면이면_전투_층을_켠다()
        {
            int mask = CombatLayer.MaskFor(Others, true);
            Assert.That(mask & CombatLayer.Mask, Is.Not.Zero, "전투 층이 꺼져 있다");
        }

        [Test]
        public void 나머지_층은_그대로_둔다()
        {
            Assert.That(CombatLayer.MaskFor(Others, true) & Others, Is.EqualTo(Others), "켤 때");
            Assert.That(CombatLayer.MaskFor(Others, false) & Others, Is.EqualTo(Others), "끌 때");
        }

        /// <summary>
        /// **뒤늦게 붙는 자식도 옮긴다** — 쉴드 막대처럼 필요할 때 생기는 것들이 있다.
        ///
        /// ⚠️ 처음에 「부모가 이미 맞으면 그 아래는 안 본다」로 썼다가 이 시험에 걸렸다.
        /// </summary>
        [Test]
        public void 나중에_붙은_자식도_옮긴다()
        {
            var root = new GameObject("StageRunner");
            var child = new GameObject("Enemy");
            child.transform.SetParent(root.transform, false);

            CombatLayer.Apply(root.transform);
            Assert.AreEqual(CombatLayer.Index, child.layer, "첫 훑기에서 안 옮겼다");

            // 한 프레임 뒤에 붙는 것 — 부모는 이미 전투 층이다.
            var late = new GameObject("ShieldBar");
            late.transform.SetParent(child.transform, false);
            CombatLayer.Apply(root.transform);

            Assert.AreEqual(CombatLayer.Index, late.layer,
                "나중에 붙은 자식이 기본 층에 남았다 — 보드 위에 그것만 뜬다");

            Object.DestroyImmediate(root);
        }
    }
}
