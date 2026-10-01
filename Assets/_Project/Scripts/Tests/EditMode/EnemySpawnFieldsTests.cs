using System.Collections.Generic;
using System.Reflection;
using MBI.Data;
using MBI.Core.Combat;
using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **적을 만드는 두 경로가 같은 칸을 베낀다** (2026-10-01).
    ///
    /// ⚠️⚠️ **이 시험이 없어서 ⑫ 가 조용히 안 돌았다.** 만드는 자리가 둘이었고
    /// 묶음 쪽은 <c>projectileSpeed</c> 를, 줄줄이 쪽은 <c>isBoss</c> 를 빠뜨렸다.
    /// 게임이 쓰는 경로가 줄줄이 쪽이라 **보스 밀기가 밀 대상을 못 찾았고**, 하네스는
    /// 「보스를 못 봤다」로 침묵했다 — 실패가 아니라 **무음**이었다.
    ///
    /// 📌 **반사로 훑는다.** 칸 이름을 손으로 적으면 나중에 더한 칸은 또 빠진다 —
    /// <c>EnemySpawn</c> 에 칸이 생기면 이 시험이 **자동으로** 그 칸을 묻는다.
    /// </summary>
    public sealed class EnemySpawnFieldsTests
    {
        private static RobotSetup IdleRobot() => new RobotSetup
        {
            hp = 1000f, mountCoef = 1f, moduleMult = 1f, attackRange = 100f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = new List<AmmoLine>(),
        };

        /// <summary>칸마다 **서로 다른** 값을 넣는다 — 같은 값이면 뒤바뀐 것을 못 본다.</summary>
        private static EnemySpawn Distinct() => new EnemySpawn
        {
            label = "표식",
            hp = 123f,
            def = 4f,
            atk = 5f,
            moveSpeed = 6f,
            attackRange = 7f,
            attackInterval = 8f,
            radius = 0.9f,
            projectileSpeed = 11f,
            isBoss = true,
        };

        /// <summary>
        /// <c>EnemySpawn</c> 의 칸들이 만들어진 적에게 **그대로** 건너갔는지 본다.
        /// 이름이 같은 칸만 본다(<c>maxHp</c> 처럼 이름이 다른 것은 따로 묻는다).
        /// </summary>
        private static void AssertCarried(EnemySpawn s, CombatEntity e, string path)
        {
            FieldInfo[] fields = typeof(EnemySpawn).GetFields(BindingFlags.Public | BindingFlags.Instance);
            Assert.Greater(fields.Length, 0, "EnemySpawn 에 공개 칸이 없다 — 시험 전제가 깨졌다");

            int checked_ = 0;
            foreach (FieldInfo f in fields)
            {
                FieldInfo t = typeof(CombatEntity).GetField(f.Name,
                    BindingFlags.Public | BindingFlags.Instance);
                if (t == null) continue;                // 적에게 없는 칸은 건너간 데가 없다
                Assert.AreEqual(f.GetValue(s), t.GetValue(e),
                    $"{path}: `{f.Name}` 가 적에게 안 건너갔다 — 만드는 자리에서 빠졌다");
                checked_++;
            }
            Assert.GreaterOrEqual(checked_, 9,
                $"{path}: 본 칸이 {checked_} 개뿐이다 — 이름이 갈려 시험이 헛돌고 있다");
        }

        /// <summary>**줄줄이 생성** — 게임이 실제로 쓰는 경로다(스테이지 전투).</summary>
        [Test]
        public void 줄줄이_생성이_모든_칸을_베낀다()
        {
            EnemySpawn s = Distinct();
            var sim = new CombatSimulation(IdleRobot(), new List<EnemySpawn> { s }, 6f, 120f, 0f);
            sim.Tick(0.1f);

            Assert.AreEqual(1, sim.Enemies.Count, "적이 안 나왔다");
            AssertCarried(s, sim.Enemies[0], "줄줄이(SpawnDue)");
            Assert.AreEqual(s.hp, sim.Enemies[0].maxHp, "최대 HP 가 시작 HP 와 갈렸다");
        }

        /// <summary>**묶음 생성** — 지금은 시험만 부르지만 같은 칸을 베껴야 한다.</summary>
        [Test]
        public void 묶음_생성이_모든_칸을_베낀다()
        {
            EnemySpawn s = Distinct();
            var sim = new CombatSimulation(IdleRobot(), new List<EnemySpawn>(), 6f, 120f, 0f)
            { Endless = true };
            sim.SpawnBatch(new List<EnemySpawn> { s });

            Assert.AreEqual(1, sim.Enemies.Count, "적이 안 나왔다");
            AssertCarried(s, sim.Enemies[0], "묶음(SpawnBatch)");
            Assert.AreEqual(s.hp, sim.Enemies[0].maxHp, "최대 HP 가 시작 HP 와 갈렸다");
        }

        /// <summary>
        /// ⚠️ **보스 표시가 정의에서 온다** — 하네스가 「보스를 못 봤다」로 침묵하던
        /// 자리다. 역할이 보스가 아니면 거짓이어야 한다(아무나 밀면 안 된다).
        /// </summary>
        [Test]
        public void 보스_표시가_역할에서_나온다()
        {
            var boss = ScriptableObject.CreateInstance<EnemyDefinition>();
            boss.role = EnemyRole.Boss;
            var mob = ScriptableObject.CreateInstance<EnemyDefinition>();
            mob.role = EnemyRole.Infantry;

            Assert.IsTrue(StageSpawnFactory.IsBossRole(boss), "보스가 보스로 안 읽힌다");
            Assert.IsFalse(StageSpawnFactory.IsBossRole(mob), "보병이 보스로 읽힌다");
            Assert.IsFalse(StageSpawnFactory.IsBossRole(null), "정의가 없는데 보스로 읽힌다");
        }
    }
}
