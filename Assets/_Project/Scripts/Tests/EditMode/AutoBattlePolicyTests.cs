using System.Collections.Generic;
using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 자동 전투 정책(§5-7) — 카이팅과 스테이지 진행. 둘 다 순수 함수라 전투 없이 검증한다.
    /// 거리·속도는 CombatTuning TBD라 여기 값은 규칙 확인용이며 밸런스 단정이 아니다.
    /// </summary>
    public sealed class AutoBattlePolicyTests
    {
        private const float D = 0.001f;

        private static CombatEntity Enemy(float x, float y, float hp = 100f) => new CombatEntity
        {
            faction = Faction.Enemy, label = "e", position = new Vector2(x, y), hp = hp, maxHp = hp,
        };

        private static AutoPilotContext Ctx(Vector2 pos, List<CombatEntity> enemies,
            float gap = 3f, float speed = 4f, float dt = 0.1f, float radius = 6f) => new AutoPilotContext
        {
            robotPos = pos, enemies = enemies, arenaRadius = radius,
            desiredGap = gap, moveSpeed = speed, dt = dt,
        };

        // ---- 카이팅 ----

        [Test]
        public void MovesAwayFromNearestEnemy()
        {
            // 적이 오른쪽 가까이 → 왼쪽으로 물러난다.
            var enemies = new List<CombatEntity> { Enemy(1f, 0f) };
            Vector2 next = AutoPilotPolicy.NextPosition(Ctx(Vector2.zero, enemies));

            Assert.Less(next.x, 0f, "위협 반대 방향으로 이동");
            Assert.AreEqual(0f, next.y, D);
        }

        [Test]
        public void DoesNotMoveWhenThreatIsFarEnough()
        {
            var enemies = new List<CombatEntity> { Enemy(5f, 0f) }; // gap 3보다 멀다
            Vector2 next = AutoPilotPolicy.NextPosition(Ctx(Vector2.zero, enemies));

            Assert.AreEqual(Vector2.zero, next, "충분히 멀면 굳이 움직이지 않는다");
        }

        [Test]
        public void PicksNearestAmongMany_Deterministically()
        {
            var enemies = new List<CombatEntity> { Enemy(4f, 0f), Enemy(1f, 0f), Enemy(3f, 0f) };
            Vector2 dir = AutoPilotPolicy.ThreatDirection(Vector2.zero, enemies);

            Assert.AreEqual(Vector2.right, dir, "가장 가까운 (1,0) 방향");
            Assert.AreEqual(dir, AutoPilotPolicy.ThreatDirection(Vector2.zero, enemies), "같은 입력 = 같은 결과");
        }

        [Test]
        public void IgnoresDeadEnemies()
        {
            var enemies = new List<CombatEntity> { Enemy(1f, 0f, hp: 0f), Enemy(5f, 0f) };
            Vector2 dir = AutoPilotPolicy.ThreatDirection(Vector2.zero, enemies);

            Assert.AreEqual(Vector2.right, dir);
            Assert.AreEqual(1f, dir.magnitude, D, "죽은 적은 위협이 아니다");
        }

        [Test]
        public void SlidesAlongBoundary_InsteadOfSticking()
        {
            // 경계에 붙어 있고 적이 안쪽에서 밀어붙이는 상황 — 밖으로 나갈 수 없다.
            var enemies = new List<CombatEntity> { Enemy(5f, 0f) };
            Vector2 start = new Vector2(6f, 0f); // 반경 6 경계
            Vector2 next = AutoPilotPolicy.NextPosition(Ctx(start, enemies));

            Assert.LessOrEqual(next.magnitude, 6f + D, "아레나 밖으로 나가지 않는다");
            Assert.Greater(Mathf.Abs(next.y), D, "접선 방향으로 미끄러진다 — 제자리에 박히지 않는다");
        }

        [Test]
        public void StaysInsideArena()
        {
            var enemies = new List<CombatEntity> { Enemy(0f, 0f) };
            Vector2 pos = new Vector2(5.9f, 0f);
            for (int i = 0; i < 50; i++)
                pos = AutoPilotPolicy.NextPosition(Ctx(pos, enemies));

            Assert.LessOrEqual(pos.magnitude, 6f + D, "반복해도 경계를 넘지 않는다");
        }

        [Test]
        public void NoEnemies_ReturnsTowardOrigin()
        {
            Vector2 next = AutoPilotPolicy.NextPosition(Ctx(new Vector2(3f, 0f), new List<CombatEntity>()));

            Assert.Less(next.x, 3f, "원점 쪽으로 돌아온다");
            Assert.GreaterOrEqual(next.x, 0f);
        }

        [Test]
        public void Deterministic_SameInputSameResult()
        {
            var enemies = new List<CombatEntity> { Enemy(1f, 1f) };
            AutoPilotContext c = Ctx(new Vector2(0.5f, -0.2f), enemies);

            Assert.AreEqual(AutoPilotPolicy.NextPosition(c), AutoPilotPolicy.NextPosition(c), "난수 0");
        }

        // ---- 스테이지 진행 ----

        [Test]
        public void Win_AdvancesToNextStage()
        {
            ProgressionDecision d = StageProgression.Decide(
                new ProgressionInput(currentIndex: 0, maxClearedIndex: -1, stageCount: 6, CombatResult.Win));

            Assert.AreEqual(1, d.nextIndex);
            Assert.IsTrue(d.isFirstClear, "처음 깬 스테이지");
            Assert.IsTrue(d.advanced);
        }

        [Test]
        public void Win_Replay_IsNotFirstClear()
        {
            // 이미 깬 스테이지를 다시 깨면 강화재료 재지급 대상이 아니다(닫힌 곡선 보호).
            ProgressionDecision d = StageProgression.Decide(
                new ProgressionInput(currentIndex: 0, maxClearedIndex: 2, stageCount: 6, CombatResult.Win));

            Assert.IsFalse(d.isFirstClear);
        }

        [Test]
        public void Lose_RepeatsSameStage_S4Wall()
        {
            // S4(index 3)는 강화-only 벽이다. 패배 시 진행하지 않고 그 자리에서 반복 = 파밍.
            // 항상 진행이면 계속 죽으며 수입이 멈춘다.
            foreach (CombatResult lose in new[] { CombatResult.LoseDead, CombatResult.LoseTimeout })
            {
                ProgressionDecision d = StageProgression.Decide(
                    new ProgressionInput(3, maxClearedIndex: 2, stageCount: 6, lose));

                Assert.AreEqual(3, d.nextIndex, $"{lose}: 현재 스테이지 유지");
                Assert.IsFalse(d.advanced);
                Assert.IsFalse(d.isFirstClear);
            }
        }

        [Test]
        public void InProgress_DoesNotChangeStage()
        {
            ProgressionDecision d = StageProgression.Decide(
                new ProgressionInput(2, 1, 6, CombatResult.InProgress));

            Assert.AreEqual(2, d.nextIndex);
            Assert.IsFalse(d.advanced);
        }

        [Test]
        public void LastStage_Win_StaysAtLast()
        {
            ProgressionDecision d = StageProgression.Decide(
                new ProgressionInput(5, maxClearedIndex: 4, stageCount: 6, CombatResult.Win));

            Assert.AreEqual(5, d.nextIndex, "마지막 스테이지는 제자리 반복");
            Assert.IsTrue(d.isFirstClear);
            Assert.IsFalse(d.advanced);
        }

        [Test]
        public void IndexOutOfRange_IsClamped()
        {
            ProgressionDecision d = StageProgression.Decide(
                new ProgressionInput(99, 0, 6, CombatResult.Win));

            Assert.AreEqual(5, d.nextIndex, "범위 밖 인덱스는 잘린다");
        }
    }
}
