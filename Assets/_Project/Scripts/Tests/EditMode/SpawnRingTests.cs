using System.Collections.Generic;
using MBI.Core;
using MBI.Core.Combat;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 전장 구조 — **로봇 기준 링과 되돌리기** (2026-09-11 신설 · 플랜 §71-28 1·2).
    ///
    /// **무엇을 지키는가.** 적이 **화면 밖에서** 나타나는가, 그리고 못 닿는 적을 되돌릴 때
    /// **개체 수가 안 변하는가**다. 둘째가 통과 조건으로 못 박힌 것은, 지우는 쪽이
    /// 훨씬 쉬운데 그러면 **난이도가 조용히 낮아지기** 때문이다.
    /// </summary>
    public sealed class SpawnRingTests
    {
        private const float D = 0.001f;

        /// <summary>
        /// 되돌리기만 보는 최소 로봇 — **아무것도 못 쏜다**(사거리 0 · 재고 0).
        /// 쏘면 적이 죽어 「개체 수 불변」을 죽음과 못 가른다.
        /// </summary>
        private static RobotSetup RobotFixture() => new RobotSetup
        {
            hp = 100000f,
            mountCoef = 1f,
            moduleMult = 1f,
            attackRange = 0f,
            multiShotCount = 1,
            aoeRadius = 0f,
            aoeSplashFactor = 1f,
            lines = new List<AmmoLine>(),
            ammoCapacity = 0f,
        };

        /// <summary>
        /// **스폰 띠 안쪽은 8 이고, 코드 기본값과 자산 값이 같다**
        /// (2026-09-16 사용자 확정 · 플랜 §74-15).
        ///
        /// ⚠️ **두 곳에 사는 값이다** — C# 기본값과 `CombatTuning.asset` 에 저장된 값.
        /// 자산이 이기므로 코드만 고치면 **화면은 안 바뀌고 시험만 통과한다.**
        /// 09-14 에 `spawnCadenceTbd` 가 정확히 그렇게 갈라져 있었다(기본 0.35 · 자산 0.15).
        /// 그래서 값이 아니라 **둘이 같은가**를 먼저 박는다.
        ///
        /// 🗑️ 구 값 **4 는 폐기** — 로봇 바로 옆이라 시작 3.4초에 적이 겹쳐 났다(육안 10차).
        /// </summary>
        [Test]
        public void 스폰_띠_안쪽은_8_이고_자산과_같다()
        {
            var defaults = ScriptableObject.CreateInstance<CombatTuning>();
            Assert.AreEqual(8f, defaults.spawnRingMinTbd, D, "코드 기본값이 8 이 아니다");
            Assert.AreEqual(14f, defaults.spawnRingMaxTbd, D, "바깥 14 는 그대로다");
            Assert.Less(defaults.spawnRingMinTbd, defaults.robotAttackRangeTbd,
                "띠 안쪽이 사거리를 넘으면 가까운 적이 없어져 로봇이 늘 걷는다");

            var asset = AssetDatabase.LoadAssetAtPath<CombatTuning>(
                "Assets/_Project/ScriptableObjects/CombatTuning.asset");
            Assert.IsNotNull(asset, "CombatTuning.asset 이 없다 — 생성기를 먼저 돌린다");
            Assert.AreEqual(defaults.spawnRingMinTbd, asset.spawnRingMinTbd, D,
                "자산 값이 코드 기본값과 다르다 — 화면에 서는 것은 자산 쪽이다");
            Assert.AreEqual(defaults.spawnRingMaxTbd, asset.spawnRingMaxTbd, D,
                "자산 값이 코드 기본값과 다르다 — 화면에 서는 것은 자산 쪽이다");
        }

        [Test]
        public void 링은_로봇을_따라간다()
        {
            var robot = new Vector2(30f, -12f);
            Vector2 p = SpawnRingRule.Position(robot, 0, 4, 10f);

            Assert.AreEqual(10f, (p - robot).magnitude, D, "로봇에서 반경만큼 떨어진다");

            // ⚠️ **원점 기준이면 로봇이 움직인 만큼 적이 화면 안에서 튀어나온다.**
            Assert.Greater(p.magnitude, 10f + D, "원점 기준이 아니다");
        }

        [Test]
        public void 링_자리는_균등하고_결정론이다()
        {
            var robot = Vector2.zero;
            Vector2 a = SpawnRingRule.Position(robot, 0, 4, 5f);
            Vector2 b = SpawnRingRule.Position(robot, 1, 4, 5f);
            Vector2 c = SpawnRingRule.Position(robot, 2, 4, 5f);

            Assert.AreEqual(new Vector2(5f, 0f), a);
            Assert.AreEqual(5f, b.magnitude, D, "반경은 다 같다");
            Assert.AreEqual(-5f, c.x, D, "넷이면 반대편이 셋째다");

            // 같은 입력은 같은 자리 — 난수 0.
            Assert.AreEqual(a, SpawnRingRule.Position(robot, 0, 4, 5f));
        }

        [Test]
        public void 반경은_대각선_반에_여유를_더한다()
        {
            // **모서리가 화면에서 가장 먼 점**이라 대각선을 쓴다 — 가로나 세로 반만 쓰면
            // 모서리 쪽에서 적이 화면 안에 선다.
            float r = SpawnRingRule.RadiusFromView(16f, 12f, 1f);
            Assert.AreEqual(new Vector2(16f, 12f).magnitude * 0.5f + 1f, r, D);
            Assert.Greater(r, 16f * 0.5f, "가로 반보다 크다");
            Assert.Greater(r, 12f * 0.5f, "세로 반보다 크다");
        }

        [Test]
        public void 못_닿는_적만_되돌린다()
        {
            // 잣대는 **시간**이다 — 느린 적에게는 같은 거리가 더 멀다.
            Assert.IsTrue(OffscreenRespawnRule.NeedsRespawn(distance: 100f, moveSpeed: 1f));
            Assert.IsFalse(OffscreenRespawnRule.NeedsRespawn(distance: 100f, moveSpeed: 10f),
                "빠른 적은 20초 안에 닿는다");
            Assert.IsFalse(OffscreenRespawnRule.NeedsRespawn(distance: 5f, moveSpeed: 1f));
        }

        [Test]
        public void 속도_0_은_안_되돌린다()
        {
            // 못 움직이는 적은 「멀어진」 것이 아니라 **그렇게 놓인 것**이다.
            Assert.IsFalse(OffscreenRespawnRule.NeedsRespawn(distance: 9999f, moveSpeed: 0f));
            Assert.IsFalse(OffscreenRespawnRule.NeedsRespawn(distance: 9999f, moveSpeed: -1f));
        }

        /// <summary>
        /// **통과 조건 — 개체 수 불변** (2026-09-11 사용자 확정).
        ///
        /// 되돌리는 대신 지우는 쪽이 훨씬 쉬운데, 그러면 **스테이지 난이도가 조용히
        /// 낮아진다.** 살아 있는 적이 사라지면 통과가 쉬워지고 그것이 화면에 안 보인다.
        /// </summary>
        [Test]
        public void 되돌려도_개체_수가_안_변한다()
        {
            var spawns = new List<EnemySpawn>
            {
                new EnemySpawn { label = "느림", hp = 100f, def = 0f, atk = 0f,
                    moveSpeed = 0.1f, attackRange = 0.5f, attackInterval = 1f },
            };
            var sim = new CombatSimulation(RobotFixture(), spawns,
                arenaRadius: 5f, challengeTime: 999f, spawnCadence: 0f);

            sim.Tick(0.02f);                       // 스폰이 돈다
            int before = sim.Remaining;
            Assert.AreEqual(1, before, "하나 스폰됐다");

            // 20초에 0.1 속도면 2 밖에 못 간다 — 저 멀리 두면 못 닿는다.
            sim.Enemies[0].position = new Vector2(500f, 0f);

            // 때려 놓은 상태를 만든다 — 리셋이 실제로 도는지 보려면 깎여 있어야 한다.
            sim.Enemies[0].hp = 30f;

            int moved = sim.RespawnUnreachable();

            Assert.AreEqual(1, moved, "하나를 되돌렸다");
            Assert.AreEqual(before, sim.Remaining, "개체 수가 안 변한다 — 지우지 않는다");

            float d = (sim.Enemies[0].position - sim.RobotPosition).magnitude;
            Assert.Less(d, 500f, "로봇 쪽으로 당겨 왔다");
        }

        /// <summary>
        /// **HP 는 리셋한다** (2026-09-14 · `260911_W03` 1-1 설계 확정).
        ///
        /// 09-11 에는 반대로 **유지**를 단언했다 — 「때려 놓은 것이 되살아나면 플레이어가 한 일이
        /// 사라진다」가 근거였다. 설계가 뒤집은 근거는 **같은 개체라는 보장이 없다**는 것이다:
        /// 로봇 기준으로 멀어져 되돌아온 것은 화면 밖에서 새로 걸어 들어오는 것과 구분되지 않는다.
        ///
        /// ⚠️ **리셋은 지우는 것이 아니다** — 개체 수 불변은 그대로 선다.
        /// </summary>
        [Test]
        public void 되돌리면_HP_가_리셋된다()
        {
            var spawns = new List<EnemySpawn>
            {
                new EnemySpawn { label = "느림", hp = 100f, def = 0f, atk = 0f,
                    moveSpeed = 0.1f, attackRange = 0.5f, attackInterval = 1f },
            };
            var sim = new CombatSimulation(RobotFixture(), spawns,
                arenaRadius: 5f, challengeTime: 999f, spawnCadence: 0f);

            sim.Tick(0.02f);
            sim.Enemies[0].hp = 30f;
            sim.Enemies[0].position = new Vector2(500f, 0f);

            Assert.AreEqual(1, sim.RespawnUnreachable());

            Assert.AreEqual(100f, sim.Enemies[0].hp, D, "만피로 돌아온다");
            Assert.AreEqual(sim.Enemies[0].maxHp, sim.Enemies[0].hp, D);
            Assert.AreEqual(1, sim.Remaining, "리셋은 지우는 것이 아니다");
        }

        /// <summary>
        /// **기준은 로봇이다** (W03 1-1). 스폰 지점 기준으로 두면 **쫓아오는 적이 사라진다** —
        /// 로봇이 계속 움직이면 스폰 지점과의 거리가 저절로 벌어지기 때문이다.
        /// </summary>
        [Test]
        public void 되돌리는_기준은_로봇이다()
        {
            var spawns = new List<EnemySpawn>
            {
                new EnemySpawn { label = "느림", hp = 100f, def = 0f, atk = 0f,
                    moveSpeed = 0.1f, attackRange = 0.5f, attackInterval = 1f },
            };
            var sim = new CombatSimulation(RobotFixture(), spawns,
                arenaRadius: 5f, challengeTime: 999f, spawnCadence: 0f);

            sim.Tick(0.02f);

            // 로봇을 원점에서 멀리 옮긴다 — 스폰 지점 기준이면 여기서 갈린다.
            sim.Robot.position = new Vector2(200f, 0f);
            sim.Enemies[0].position = new Vector2(-300f, 0f);

            Assert.AreEqual(1, sim.RespawnUnreachable());

            float toRobot = (sim.Enemies[0].position - sim.RobotPosition).magnitude;
            Assert.AreEqual(sim.SpawnRingRadius, toRobot, 0.01f,
                "로봇에서 링 반경만큼 떨어진 자리로 온다");

            // ⚠️ 원점 기준이면 여기가 선다 — 그래서 원점과의 거리로는 안 잰다.
            Assert.Greater(sim.Enemies[0].position.magnitude, 0f);
        }

        [Test]
        public void 잣대는_확정_20초다()
        {
            // ✅ **사용자 확정**(§71-27) — 가정이 아니다. 설계가 문서에 역기입한다.
            // 짧으면 잠깐 뒤처진 적까지 순간이동해 화면에서 적이 튄다.
            Assert.AreEqual(20f, OffscreenRespawnRule.UnreachableSeconds, D);

            // 경계는 **초과**다 — 딱 닿는 거리는 안 되돌린다.
            Assert.IsFalse(OffscreenRespawnRule.NeedsRespawn(20f, 1f), "20초에 정확히 닿는다");
            Assert.IsTrue(OffscreenRespawnRule.NeedsRespawn(20.1f, 1f));
        }
    }
}
