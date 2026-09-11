using System.Collections.Generic;
using MBI.Core;
using MBI.Core.Combat;
using NUnit.Framework;
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
            float hpBefore = sim.Enemies[0].hp;

            int moved = sim.RespawnUnreachable();

            Assert.AreEqual(1, moved, "하나를 되돌렸다");
            Assert.AreEqual(before, sim.Remaining, "개체 수가 안 변한다 — 지우지 않는다");
            Assert.AreEqual(hpBefore, sim.Enemies[0].hp, D,
                "HP 를 유지한다(가정) — 때려 놓은 것이 되살아나면 플레이어가 한 일이 사라진다");

            float d = (sim.Enemies[0].position - sim.RobotPosition).magnitude;
            Assert.Less(d, 500f, "로봇 쪽으로 당겨 왔다");
        }

        [Test]
        public void 잣대는_가정_20초다()
        {
            // ⚠️ 문서에 절이 없다 — 설계 역기입 자리. 짧으면 잠깐 뒤처진 적까지
            // 순간이동해 화면에서 적이 튄다.
            Assert.AreEqual(20f, OffscreenRespawnRule.UnreachableSeconds, D);

            // 경계는 **초과**다 — 딱 닿는 거리는 안 되돌린다.
            Assert.IsFalse(OffscreenRespawnRule.NeedsRespawn(20f, 1f), "20초에 정확히 닿는다");
            Assert.IsTrue(OffscreenRespawnRule.NeedsRespawn(20.1f, 1f));
        }
    }
}
