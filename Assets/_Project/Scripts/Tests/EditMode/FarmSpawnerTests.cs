using System.Collections.Generic;
using MBI.Core;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 상주 파밍 스폰 규칙(스테이지 기획서「파밍 규칙」). 원천 규칙:
    /// N초마다 스폰 수 = 정원 − 생존 수, **한 틱 전량 보충** → 한 바퀴 = N초 고정.
    /// 수치(정원·간격)는 전부 TBD라 여기서는 규칙만 검증한다 — 값은 단정하지 않는다.
    /// </summary>
    public sealed class FarmSpawnerTests
    {
        // ---- 규칙 ----

        [Test]
        public void Refill_FillsOnlyEmptySlots()
        {
            Assert.AreEqual(4, FarmSpawnRule.RefillCount(10, 6), "잡아서 생긴 빈 자리만큼만");
            Assert.AreEqual(10, FarmSpawnRule.RefillCount(10, 0), "전멸시켜 두면 정원만큼 한 번에");
            Assert.AreEqual(0, FarmSpawnRule.RefillCount(10, 10), "꽉 차 있으면 0");
        }

        [Test]
        public void Refill_OverCap_DoesNotRemove()
        {
            // 정원보다 많아도 줄이지는 않는다 — 보충 규칙이지 정리 규칙이 아니다.
            Assert.AreEqual(0, FarmSpawnRule.RefillCount(10, 14));
        }

        [Test]
        public void Refill_UnconfiguredCap_IsZero()
        {
            Assert.AreEqual(0, FarmSpawnRule.RefillCount(0, 0), "정원 TBD(0)면 스폰하지 않는다");
        }

        [Test]
        public void SaturationPower_IsCapTimesHpOverInterval()
        {
            // 정원 10 · 체력 270 · 간격 20초 → 135. 이 화력에 닿으면 수입이 천장.
            Assert.AreEqual(135f, FarmSpawnRule.SaturationPower(10, 270f, 20f), 0.001f);
            Assert.AreEqual(0f, FarmSpawnRule.SaturationPower(10, 270f, 0f), 0.001f, "간격 TBD면 0");
        }

        [Test]
        public void HourlyRate_IncludesSecondsToHourConversion()
        {
            // 20초 창에 10마리 × 2고철 = 20고철 → 시간당 3,600고철.
            Assert.AreEqual(3600d, FarmSpawnRule.HourlyRate(10, 2d, 20f), 0.001d,
                "×3600을 빠뜨리면 1/60이 된다");
            Assert.AreEqual(0d, FarmSpawnRule.HourlyRate(0, 2d, 20f), 0.001d, "처치 0이면 0");
        }

        [Test]
        public void RingPosition_IsDeterministicAndOnRadius()
        {
            var a = FarmSpawnRule.RingPosition(1, 4, 6f);
            var b = FarmSpawnRule.RingPosition(1, 4, 6f);
            Assert.AreEqual(a, b, "같은 입력 = 같은 위치(난수 0)");
            Assert.AreEqual(6f, a.magnitude, 0.001f, "아레나 경계 위");
        }

        // ---- 스포너 ----

        [Test]
        public void Spawner_TicksOncePerInterval_AndClosesOneLap()
        {
            var s = new FarmSpawner(cap: 10, intervalSeconds: 20f);

            Assert.IsFalse(s.Tick(19f, 10, out int r0), "간격 전에는 보충 없음");
            Assert.AreEqual(0, r0);
            Assert.AreEqual(0, s.Laps);

            Assert.IsTrue(s.Tick(1f, 3, out int r1), "20초에 도달 = 보충 틱");
            Assert.AreEqual(7, r1, "정원 10 − 생존 3");
            Assert.AreEqual(1, s.Laps, "이 틱이 곧 한 바퀴의 경계");
        }

        [Test]
        public void Spawner_LapIsFixedToInterval()
        {
            // 한 틱 전량 보충이므로 바퀴 길이는 화력과 무관하게 N초로 고정된다.
            var s = new FarmSpawner(10, 15f);
            s.Tick(15f, 10, out _);   // 아무도 안 잡은 바퀴
            s.Tick(15f, 0, out int r); // 전멸시킨 바퀴
            Assert.AreEqual(10, r, "전멸이면 정원만큼 한 번에");
            Assert.AreEqual(2, s.Laps, "두 바퀴 모두 같은 길이로 닫힌다");
        }

        [Test]
        public void Spawner_Unconfigured_NeverTicks()
        {
            var s = new FarmSpawner(cap: 0, intervalSeconds: 0f);
            Assert.IsFalse(s.IsConfigured);
            Assert.IsFalse(s.Tick(1000f, 0, out int r));
            Assert.AreEqual(0, r, "정원·간격 TBD면 파밍이 돌지 않는다");
            Assert.AreEqual(0, s.Laps);
        }

        [Test]
        public void Spawner_HugeDelta_CountsLapsButRefillsOnce()
        {
            // 창 여러 개를 한 프레임에 넘겨도 바퀴 수는 지난 만큼 세고, 보충은 현재 상태 기준 1회.
            var s = new FarmSpawner(10, 20f);
            Assert.IsTrue(s.Tick(65f, 2, out int r));
            Assert.AreEqual(3, s.Laps);
            Assert.AreEqual(8, r);
        }

        // ---- 전투 엔진의 상주 층 지원 ----

        private static RobotSetup IdleRobot() => new RobotSetup
        {
            hp = 1000f, mountCoef = 1f, moduleMult = 1f, attackRange = 100f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = new List<AmmoLine>(), // 무장 없음 — 스폰/종료 조건만 본다
        };

        private static List<EnemySpawn> Batch(int n)
        {
            var l = new List<EnemySpawn>();
            for (int i = 0; i < n; i++)
                l.Add(new EnemySpawn { label = $"e{i}", hp = 100f, def = 0f, atk = 0f,
                    moveSpeed = 0f, attackRange = 0.5f, attackInterval = 1f });
            return l;
        }

        [Test]
        public void Endless_NeverWinsWhenBoardIsEmpty()
        {
            // 도전 층이라면 전원 스폰 후 전멸 = 승리. 상주 파밍은 끝나지 않는다.
            var sim = new CombatSimulation(IdleRobot(), new List<EnemySpawn>(), 6f, 120f, 0f) { Endless = true };
            sim.Tick(0.1f);
            Assert.AreEqual(CombatResult.InProgress, sim.Result);
        }

        [Test]
        public void Endless_IgnoresChallengeTimeout()
        {
            var sim = new CombatSimulation(IdleRobot(), new List<EnemySpawn>(), 6f, 120f, 0f) { Endless = true };
            for (int i = 0; i < 1300; i++) sim.Tick(0.1f); // 130초 — 도전 제한(120초) 초과
            Assert.AreEqual(CombatResult.InProgress, sim.Result, "파밍 층에는 제한시간이 없다");
        }

        [Test]
        public void SpawnBatch_AddsEnemiesOnRing()
        {
            var sim = new CombatSimulation(IdleRobot(), new List<EnemySpawn>(), 6f, 120f, 0f) { Endless = true };
            sim.SpawnBatch(Batch(4));

            Assert.AreEqual(4, sim.Remaining);
            foreach (CombatEntity e in sim.Enemies)
                Assert.AreEqual(6f, e.position.magnitude, 0.001f, "아레나 경계에 배치");
        }

        [Test]
        public void Endless_StillLosesWhenRobotDies()
        {
            // 끝나지 않는 층이라도 로봇 파괴는 그대로 패배다(자동 전투가 재시작을 맡는다).
            RobotSetup robot = IdleRobot();
            robot.hp = 1f;
            var sim = new CombatSimulation(robot, new List<EnemySpawn>(), 6f, 120f, 0f) { Endless = true };
            sim.SpawnBatch(new List<EnemySpawn> { new EnemySpawn { label = "적", hp = 999f, def = 0f, atk = 50f,
                moveSpeed = 10f, attackRange = 100f, attackInterval = 0.1f } });

            for (int i = 0; i < 100 && sim.Result == CombatResult.InProgress; i++) sim.Tick(0.1f);
            Assert.AreEqual(CombatResult.LoseDead, sim.Result);
        }
    }
}
