using System.Collections.Generic;
using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 자동 전투 정책 — 접근 이동과 스테이지 진행. 둘 다 순수 함수라 전투 없이 검증한다.
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
            float range = 3f, float speed = 4f, float dt = 0.1f, float radius = 6f) => new AutoPilotContext
        {
            robotPos = pos, enemies = enemies, arenaRadius = radius,
            attackRange = range, moveSpeed = speed, dt = dt,
        };

        // ---- 이동: 접근만 (카이팅 폐기 — 2026-08-26 판정) ----

        /// <summary>
        /// **사거리 안이면 제자리 사격.** 물러나지 않는다.
        /// 카이팅은 회피 시스템(부스터 노드)과 중복이고, 자원을 쓰지 않아 추진제의 존재 이유를
        /// 없애며, 전투력 출처를 물류에서 이동 로직으로 옮긴다.
        /// </summary>
        [Test]
        public void InRange_HoldsPosition_DoesNotRetreat()
        {
            var enemies = new List<CombatEntity> { Enemy(1f, 0f) }; // 사거리 3 안

            Vector2 next = AutoPilotPolicy.NextPosition(Ctx(Vector2.zero, enemies));

            Assert.AreEqual(Vector2.zero, next, "적이 코앞이어도 물러나지 않는다");
        }

        /// <summary>사거리 밖이면 최근접 적을 향해 **다가간다.**</summary>
        [Test]
        public void OutOfRange_ApproachesNearestEnemy()
        {
            var enemies = new List<CombatEntity> { Enemy(5f, 0f) }; // 사거리 3 밖

            Vector2 next = AutoPilotPolicy.NextPosition(Ctx(Vector2.zero, enemies));

            Assert.Greater(next.x, 0f, "적 쪽으로 간다");
            Assert.AreEqual(0f, next.y, D, "4방향 — 대각선으로 가지 않는다");
        }

        /// <summary>이동은 4방향이다. 대각선 표적에도 한 축씩만 민다.</summary>
        [Test]
        public void Approach_IsFourDirectional()
        {
            var enemies = new List<CombatEntity> { Enemy(5f, 5f) };

            Vector2 next = AutoPilotPolicy.NextPosition(Ctx(Vector2.zero, enemies));

            bool movedX = Mathf.Abs(next.x) > D;
            bool movedY = Mathf.Abs(next.y) > D;
            Assert.IsTrue(movedX ^ movedY, "한 축으로만 움직인다");
        }

        [Test]
        public void PicksNearestAmongMany_Deterministically()
        {
            var enemies = new List<CombatEntity> { Enemy(5f, 0f), Enemy(2f, 0f), Enemy(9f, 0f) };

            Vector2 dir = AutoPilotPolicy.ThreatDirection(Vector2.zero, enemies);

            Assert.AreEqual(Vector2.right, dir, "가장 가까운 (2,0)을 고른다");
            Assert.AreEqual(dir, AutoPilotPolicy.ThreatDirection(Vector2.zero, enemies), "같은 입력 = 같은 결과");
        }

        [Test]
        public void IgnoresDeadEnemies()
        {
            var enemies = new List<CombatEntity> { Enemy(1f, 0f, hp: 0f), Enemy(0f, 5f) };

            Vector2 dir = AutoPilotPolicy.ThreatDirection(Vector2.zero, enemies);

            Assert.AreEqual(Vector2.up, dir, "죽은 적은 위협이 아니다");
        }

        /// <summary>적이 없으면 **가만히 있는다.** 원점 복귀도 이동 판단이므로 넣지 않는다.</summary>
        [Test]
        public void NoEnemies_HoldsPosition()
        {
            var start = new Vector2(3f, 0f);

            Vector2 next = AutoPilotPolicy.NextPosition(Ctx(start, new List<CombatEntity>()));

            Assert.AreEqual(start, next);
        }

        [Test]
        public void StaysInsideArena()
        {
            var enemies = new List<CombatEntity> { Enemy(20f, 0f) }; // 아레나 밖 표적
            Vector2 pos = new Vector2(5.9f, 0f);

            for (int i = 0; i < 50; i++)
                pos = AutoPilotPolicy.NextPosition(Ctx(pos, enemies));

            Assert.LessOrEqual(pos.magnitude, 6f + D, "아레나를 벗어나지 않는다");
        }

        [Test]
        public void Deterministic_SameInputSameResult()
        {
            var enemies = new List<CombatEntity> { Enemy(4f, 1f), Enemy(-3f, 2f) };
            AutoPilotContext c = Ctx(new Vector2(0.5f, -0.2f), enemies);

            Assert.AreEqual(AutoPilotPolicy.NextPosition(c), AutoPilotPolicy.NextPosition(c));
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
