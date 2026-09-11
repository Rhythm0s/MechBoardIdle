using System.Collections.Generic;
using MBI.Core;
using MBI.Core.Combat;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 병종별 공격 — **포격만 날아온다** (2026-09-11 신설 · 플랜 §71-33 ② 공격 패턴 (가)).
    ///
    /// **무엇을 지키는가.** 09-11 실측에서 적 넷이 이동 속도·사거리·주기를 **똑같이** 썼고
    /// 공격은 전부 즉발이었다 — 화면에서 **날아오는 것이 하나도 없었다.**
    /// 여기서 보는 것은 셋이다: 포격만 갈라지는가 · 투사체가 **쏜 틱에 안 깎는가** ·
    /// **비키면 지나가는가.**
    ///
    /// ⚠️ 값은 전부 가정이라 **수치 자체를 못 박지 않는다** — 관계만 본다
    /// (포격이 보병보다 멀다 · 투사체가 적보다 빠르다). `260911_W03` 이 값을 주면
    /// `EnemyDefinition` 칸에 적히고 이 시험은 그대로 선다.
    /// </summary>
    public sealed class EnemyAttackTests
    {
        private const float D = 0.001f;

        /// <summary>아무것도 못 쏘는 로봇 — 쏘면 적이 죽어 「맞았는가」와 섞인다.</summary>
        private static RobotSetup RobotFixture() => new RobotSetup
        {
            hp = 1000f,
            mountCoef = 1f,
            moduleMult = 1f,
            attackRange = 0f,
            multiShotCount = 1,
            aoeRadius = 0f,
            aoeSplashFactor = 1f,
            lines = new List<AmmoLine>(),
            ammoCapacity = 0f,
            radius = 0.5f,
        };

        private static CombatSimulation SimWith(EnemySpawn e) =>
            new CombatSimulation(RobotFixture(), new List<EnemySpawn> { e },
                arenaRadius: 5f, challengeTime: 999f, spawnCadence: 0f);

        private static EnemySpawn Shooter(float projectileSpeed, float range) => new EnemySpawn
        {
            label = "포격",
            hp = 100f,
            def = 0f,
            atk = 10f,
            moveSpeed = 0f,          // 제자리에서 쏜다 — 이동이 사거리 판정에 안 섞인다
            attackRange = range,
            attackInterval = 100f,   // 한 발만 쏜다
            projectileSpeed = projectileSpeed,
        };

        [Test]
        public void 포격만_투사체를_쓴다()
        {
            Assert.IsTrue(EnemyAttackRule.UsesProjectile(EnemyRole.Artillery));

            // ⚠️ (가)는 **포격 하나만** 바꾼다 — 셋은 현행이다.
            Assert.IsFalse(EnemyAttackRule.UsesProjectile(EnemyRole.Infantry));
            Assert.IsFalse(EnemyAttackRule.UsesProjectile(EnemyRole.Armor));
            Assert.IsFalse(EnemyAttackRule.UsesProjectile(EnemyRole.Boss));
        }

        [Test]
        public void 포격_사거리가_다른_병종보다_멀다()
        {
            // 값이 아니라 **관계**를 본다 — 6~8 안 어디에 앉아도 이 시험은 선다.
            Assert.Greater(EnemyAttackRule.ArtilleryRangeAssumed, 1f,
                "보병 사거리(1)와 갈라져야 「멀리서 쏘는 적」으로 읽힌다");

            // 셋은 0 을 돌려준다 = 「현행 유지」. 0 이라는 사거리가 아니다.
            Assert.AreEqual(0f, EnemyAttackRule.RangeOrZero(EnemyRole.Infantry), D);
            Assert.AreEqual(0f, EnemyAttackRule.ProjectileSpeedOrZero(EnemyRole.Boss), D);
        }

        /// <summary>**현행이 안 바뀌었는가** — 즉발은 그 틱에 그 자리에서 깎는다.</summary>
        [Test]
        public void 즉발은_그_틱에_깎는다()
        {
            var sim = SimWith(Shooter(projectileSpeed: 0f, range: 100f));
            sim.Tick(0.02f);

            Assert.AreEqual(0, sim.EnemyProjectiles.Count, "날아가는 것이 없다");
            Assert.Less(sim.Robot.hp, 1000f, "그 자리에서 맞았다");
        }

        /// <summary>
        /// **쏜 틱에는 안 깎인다** — 이것이 (가)의 뜻이다. 여기가 서지 않으면
        /// 투사체는 그림만 날고 피해는 즉발인 **거짓 연출**이 된다.
        /// </summary>
        [Test]
        public void 투사체는_쏜_틱에_안_깎는다()
        {
            var sim = SimWith(Shooter(projectileSpeed: 6f, range: 100f));
            sim.Tick(0.02f);

            Assert.AreEqual(1, sim.EnemyProjectiles.Count, "한 발이 날고 있다");
            Assert.AreEqual(1000f, sim.Robot.hp, D, "아직 안 맞았다");
        }

        [Test]
        public void 날아와_닿으면_깎는다()
        {
            var sim = SimWith(Shooter(projectileSpeed: 6f, range: 100f));
            sim.Tick(0.02f);
            Assert.AreEqual(1000f, sim.Robot.hp, D);

            // 링 반경만큼 떨어져 스폰되므로 몇 초면 닿는다. 고정 dt 라 재현된다.
            for (int i = 0; i < 600 && sim.EnemyProjectiles.Count > 0; i++) sim.Tick(0.02f);

            Assert.AreEqual(0, sim.EnemyProjectiles.Count, "사라졌다 — 맞았거나 지나쳤다");
            Assert.Less(sim.Robot.hp, 1000f, "가만히 서 있었으니 맞는다");
        }

        /// <summary>
        /// **비키면 지나간다** — 조준점이 쏜 순간으로 굳어 있기 때문이다.
        /// 유도로 만들면 사거리를 벌린 의미가 없어진다(피할 수 없는 공격이 된다).
        /// </summary>
        [Test]
        public void 비키면_지나간다()
        {
            var sim = SimWith(Shooter(projectileSpeed: 6f, range: 100f));
            sim.Tick(0.02f);
            Assert.AreEqual(1, sim.EnemyProjectiles.Count);

            // 조준점에서 크게 벗어난다. 시뮬은 로봇을 안 움직이므로 이 한 줄이 곧 회피다.
            sim.Robot.position += new Vector2(0f, 50f);

            for (int i = 0; i < 600 && sim.EnemyProjectiles.Count > 0; i++) sim.Tick(0.02f);

            Assert.AreEqual(0, sim.EnemyProjectiles.Count, "조준점을 지나 사라진다");
            Assert.AreEqual(1000f, sim.Robot.hp, D, "안 맞았다");
        }

        [Test]
        public void 명중은_접촉이다()
        {
            Assert.IsTrue(EnemyAttackRule.Hits(Vector2.zero, new Vector2(0.4f, 0f), 0.5f));
            Assert.IsFalse(EnemyAttackRule.Hits(Vector2.zero, new Vector2(0.6f, 0f), 0.5f));

            // 경계는 **이하**다 — 딱 닿으면 맞는다.
            Assert.IsTrue(EnemyAttackRule.Hits(Vector2.zero, new Vector2(0.5f, 0f), 0.5f));
        }
    }
}
