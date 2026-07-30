using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 탄종 히트 패턴(순수). 관통=단일 / 분열=멀티샷(최근접 N 직격) / 폭발=AoE(직격+반경 스플래시).
    /// HitResolver는 표적·배율 선정만(데미지 적용은 시뮬).
    /// </summary>
    public sealed class HitResolverTests
    {
        private static CombatEntity Enemy(float x, float y, float hp = 100f) => new CombatEntity
        {
            faction = Faction.Enemy,
            position = new Vector2(x, y),
            hp = hp,
            maxHp = hp,
            def = 0f,
        };

        private static HitTarget Find(List<HitTarget> hits, CombatEntity e)
        {
            foreach (HitTarget h in hits) if (ReferenceEquals(h.entity, e)) return h;
            return default;
        }

        private static bool Has(List<HitTarget> hits, CombatEntity e)
        {
            foreach (HitTarget h in hits) if (ReferenceEquals(h.entity, e)) return true;
            return false;
        }

        [Test]
        public void Pierce_SingleTarget()
        {
            var list = new List<CombatEntity> { Enemy(0, 0), Enemy(1, 0), Enemy(2, 0) };
            var hits = HitResolver.Resolve(AmmoKind.Pierce, list[0], list, multiShotCount: 3, aoeRadius: 5f, aoeSplashFactor: 1f);
            Assert.AreEqual(1, hits.Count);
            Assert.AreSame(list[0], hits[0].entity);
            Assert.AreEqual(1f, hits[0].damageFactor, 0.001f);
        }

        [Test]
        public void Split_MultiShot_NearestN_FullDamageEach()
        {
            var list = new List<CombatEntity> { Enemy(0, 0), Enemy(0.5f, 0), Enemy(1, 0), Enemy(5, 0) };
            var hits = HitResolver.Resolve(AmmoKind.Split, list[0], list, multiShotCount: 3, aoeRadius: 0f, aoeSplashFactor: 0.5f);
            Assert.AreEqual(3, hits.Count, "최근접 3기");
            Assert.IsTrue(Has(hits, list[0]) && Has(hits, list[1]) && Has(hits, list[2]));
            Assert.IsFalse(Has(hits, list[3]), "먼 표적 제외");
            foreach (HitTarget h in hits)
                Assert.AreEqual(1f, h.damageFactor, 0.001f, "멀티샷은 전부 풀 데미지");
        }

        [Test]
        public void Explosive_Aoe_DirectPlusSplashWithinRadius()
        {
            // 착탄점=list[0]. 반경 1.5 내 (0,0)직격·(1,0)스플래시, (3,0)은 밖.
            var list = new List<CombatEntity> { Enemy(0, 0), Enemy(1, 0), Enemy(3, 0) };
            var hits = HitResolver.Resolve(AmmoKind.Explosive, list[0], list, multiShotCount: 1, aoeRadius: 1.5f, aoeSplashFactor: 0.5f);

            Assert.AreEqual(2, hits.Count);
            Assert.AreEqual(1f, Find(hits, list[0]).damageFactor, 0.001f, "직격 풀");
            Assert.AreEqual(0.5f, Find(hits, list[1]).damageFactor, 0.001f, "스플래시 감쇠");
            Assert.IsFalse(Has(hits, list[2]), "반경 밖 제외");
        }

        [Test]
        public void Explosive_RadiusZero_DirectOnly()
        {
            var list = new List<CombatEntity> { Enemy(0, 0), Enemy(0.1f, 0) };
            var hits = HitResolver.Resolve(AmmoKind.Explosive, list[0], list, multiShotCount: 1, aoeRadius: 0f, aoeSplashFactor: 0.5f);
            Assert.AreEqual(1, hits.Count);
            Assert.AreSame(list[0], hits[0].entity);
        }

        [Test]
        public void RobotAmmo_AllSingleTarget_WhenMultiShot1AndSplash0()
        {
            // 앵커(07 5장 스테이징): 등가선은 단일 표적 기준 → 로봇A 관통/분열/폭발 전부 단일 표적.
            // multiShotCount=1, aoeSplashFactor=0 이면 세 탄종 모두 표적 1기만 타격.
            var list = new List<CombatEntity> { Enemy(0, 0), Enemy(0.4f, 0), Enemy(0.8f, 0) };
            foreach (AmmoKind kind in new[] { AmmoKind.Pierce, AmmoKind.Split, AmmoKind.Explosive })
            {
                var hits = HitResolver.Resolve(kind, list[0], list,
                    multiShotCount: 1, aoeRadius: 1.5f, aoeSplashFactor: 0f);
                Assert.AreEqual(1, hits.Count, $"{kind} = 단일 표적(로봇A)");
                Assert.AreSame(list[0], hits[0].entity, $"{kind} 표적 = 착탄 대상");
            }
        }

        [Test]
        public void SkipsDeadEnemies()
        {
            var dead = Enemy(0.3f, 0, hp: 0f);
            var list = new List<CombatEntity> { Enemy(0, 0), dead, Enemy(1, 0) };
            var hits = HitResolver.Resolve(AmmoKind.Explosive, list[0], list, multiShotCount: 1, aoeRadius: 2f, aoeSplashFactor: 1f);
            Assert.IsFalse(Has(hits, dead), "사망 표적 제외");
        }
    }
}
