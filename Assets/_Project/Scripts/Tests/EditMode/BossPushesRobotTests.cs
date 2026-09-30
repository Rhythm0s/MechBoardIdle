using System.Collections.Generic;
using MBI.Core;
using MBI.Core.Combat;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **보스만 로봇을 민다** (2026-09-30 사용자 확정 ⑫).
    ///
    /// ⚠️⚠️ 이 시험이 지켜야 하는 것은 **둘**이다 — 보스가 민다는 것과, **일반 몹은
    /// 여전히 못 민다**는 것. 뒤쪽을 안 지키면 09-21 에 사용자가 확정한
    /// 「로봇은 안 밀린다」가 조용히 뒤집힌다.
    /// </summary>
    public sealed class BossPushesRobotTests
    {
        private const float Dt = 0.1f;
        private const float Strength = 6f;   // 자산의 enemyPushStrengthTbd 와 같은 값

        private static CombatEntity Robot(Vector2 at) => new CombatEntity
        {
            faction = Faction.Robot, hp = 1000f, maxHp = 1000f, radius = 0.5f, position = at,
        };

        private static CombatEntity Enemy(Vector2 at, bool boss) => new CombatEntity
        {
            faction = Faction.Enemy, hp = 100f, maxHp = 100f, radius = 0.5f,
            position = at, isBoss = boss,
        };

        /// <summary>겹쳐 세우면 로봇이 **밀려난다**.</summary>
        [Test]
        public void 보스는_로봇을_민다()
        {
            CombatEntity robot = Robot(new Vector2(0.2f, 0f));
            var enemies = new List<CombatEntity> { Enemy(Vector2.zero, boss: true) };

            CrowdSeparation.PushRobot(robot, enemies, Strength, Dt);

            Assert.Greater(robot.position.x, 0.2f,
                "보스와 겹쳐 있는데 로봇이 안 밀렸다");
        }

        /// <summary>⚠️ **일반 몹은 그대로 못 민다** — 09-21 확정이 살아 있어야 한다.</summary>
        [Test]
        public void 일반_몹은_로봇을_못_민다()
        {
            CombatEntity robot = Robot(new Vector2(0.2f, 0f));
            var enemies = new List<CombatEntity> { Enemy(Vector2.zero, boss: false) };

            CrowdSeparation.PushRobot(robot, enemies, Strength, Dt);

            Assert.AreEqual(0.2f, robot.position.x, 1e-5f,
                "보스가 아닌데 로봇을 밀었다 — 09-21 확정이 뒤집혔다");
        }

        /// <summary>**보스는 안 물러난다** — 겹친 몫을 로봇 혼자 진다.</summary>
        [Test]
        public void 보스는_제자리에_있는다()
        {
            CombatEntity robot = Robot(new Vector2(0.2f, 0f));
            CombatEntity boss = Enemy(Vector2.zero, boss: true);

            CrowdSeparation.PushRobot(robot, new List<CombatEntity> { boss }, Strength, Dt);

            Assert.AreEqual(Vector2.zero, boss.position,
                "보스가 로봇에 밀려 물러났다 — 덩치가 밀어붙인다가 안 읽힌다");
        }

        /// <summary>
        /// ⚠️ **세기 0 이면 아무 일도 안 난다** — 되돌리는 값 하나가 정말 되돌리는지 본다.
        /// </summary>
        [Test]
        public void 세기_0_이면_구_거동이다()
        {
            CombatEntity robot = Robot(new Vector2(0.2f, 0f));
            var enemies = new List<CombatEntity> { Enemy(Vector2.zero, boss: true) };

            CrowdSeparation.PushRobot(robot, enemies, 0f, Dt);

            Assert.AreEqual(0.2f, robot.position.x, 1e-5f, "세기 0 인데 밀었다");
        }

        /// <summary>안 겹쳤으면 안 민다 — 미는 것은 이동이 아니라 **겹침 풀기**다.</summary>
        [Test]
        public void 안_겹치면_안_민다()
        {
            CombatEntity robot = Robot(new Vector2(5f, 0f));
            var enemies = new List<CombatEntity> { Enemy(Vector2.zero, boss: true) };

            CrowdSeparation.PushRobot(robot, enemies, Strength, Dt);

            Assert.AreEqual(5f, robot.position.x, 1e-5f, "멀리 있는 보스가 밀었다");
        }

        /// <summary>
        /// **한 틱 상한을 지킨다** — 없으면 깊이 겹친 순간 로봇이 튀어 날아간다.
        /// </summary>
        [Test]
        public void 한_틱_상한을_안_넘는다()
        {
            CombatEntity robot = Robot(Vector2.zero);          // 완전히 포개 놓는다
            var enemies = new List<CombatEntity> { Enemy(Vector2.zero, boss: true) };

            CrowdSeparation.PushRobot(robot, enemies, 1000f, 1f);   // 터무니없이 센 값

            Assert.LessOrEqual(robot.position.magnitude, CrowdSeparation.MaxPerTick + 1e-5f,
                $"한 틱에 {CrowdSeparation.MaxPerTick} 칸을 넘게 밀렸다");
        }

        /// <summary>죽은 보스는 안 민다.</summary>
        [Test]
        public void 죽은_보스는_안_민다()
        {
            CombatEntity robot = Robot(new Vector2(0.2f, 0f));
            CombatEntity boss = Enemy(Vector2.zero, boss: true);
            boss.hp = 0f;

            CrowdSeparation.PushRobot(robot, new List<CombatEntity> { boss }, Strength, Dt);

            Assert.AreEqual(0.2f, robot.position.x, 1e-5f, "시체가 밀었다");
        }
    }
}
