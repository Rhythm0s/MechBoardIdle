using System.Collections.Generic;
using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 이동·충돌 규칙(전투 시스템 문서「자동 전투 구현 사양」, 2026-08-26 신설).
    ///
    /// 이 규칙들은 취향이 아니라 **밸런스 장치**다. 경로 탐색을 넣으면 장갑형 길막이 사라지고,
    /// 표적 선택에 판단을 넣으면 전투력 출처가 물류에서 판단 로직으로 옮겨간다.
    /// </summary>
    public sealed class GridMovementTests
    {
        private const float D = 0.0001f;

        // ---- 4방향 이동 ----

        [Test]
        public void Step_MovesOnOneAxisOnly()
        {
            Vector2 next = GridMovement.Step(Vector2.zero, new Vector2(3f, 1f), 1f);

            Assert.AreEqual(1f, next.x, D, "델타가 큰 가로축을 민다");
            Assert.AreEqual(0f, next.y, D, "대각선으로 가지 않는다");
        }

        /// <summary>대각선은 **두 방향을 번갈아** 낸다 — 한 축을 끝까지 밀면 ㄱ자가 된다.</summary>
        [Test]
        public void Step_AlternatesAxes_OnDiagonal()
        {
            Vector2 pos = Vector2.zero;
            var target = new Vector2(4f, 4f);
            var axes = new List<string>();

            for (int i = 0; i < 6; i++)
            {
                Vector2 next = GridMovement.Step(pos, target, 1f);
                axes.Add(Mathf.Abs(next.x - pos.x) > D ? "x" : "y");
                pos = next;
            }

            Assert.Contains("x", axes);
            Assert.Contains("y", axes);
            // 정확히 번갈아야 계단이 된다 — 같은 축이 연속 두 번 나오면 ㄱ자다.
            for (int i = 1; i < axes.Count; i++)
                Assert.AreNotEqual(axes[i - 1], axes[i], $"{i}번째 걸음에서 축이 연속됐다");
        }

        [Test]
        public void Step_DoesNotOvershoot()
        {
            Vector2 next = GridMovement.Step(Vector2.zero, new Vector2(0.3f, 0f), 10f);
            Assert.AreEqual(0.3f, next.x, D, "남은 거리보다 크게 밀지 않는다");
        }

        [Test]
        public void Step_IsIdentityWhenAlreadyThere_OrNoDistance()
        {
            Assert.AreEqual(Vector2.zero, GridMovement.Step(Vector2.zero, Vector2.zero, 1f));
            Assert.AreEqual(Vector2.one, GridMovement.Step(Vector2.one, new Vector2(5f, 5f), 0f));
        }

        /// <summary>같은 입력이면 같은 걸음 — 상태를 들지 않아 결정론이 유지된다.</summary>
        [Test]
        public void Step_IsDeterministic()
        {
            var from = new Vector2(1.5f, -2.5f);
            var to = new Vector2(7f, 3f);

            Assert.AreEqual(GridMovement.Step(from, to, 0.4f), GridMovement.Step(from, to, 0.4f));
        }

        // ---- 충돌: 막히면 멈춘다 ----

        private static CombatEntity At(float x, float y, float radius) => new CombatEntity
        {
            faction = Faction.Enemy, label = "e", position = new Vector2(x, y),
            hp = 10f, maxHp = 10f, radius = radius,
        };

        [Test]
        public void Overlaps_UsesRadiusSum()
        {
            Assert.IsTrue(GridMovement.Overlaps(Vector2.zero, 0.5f, new Vector2(0.9f, 0f), 0.5f));
            Assert.IsFalse(GridMovement.Overlaps(Vector2.zero, 0.5f, new Vector2(1.1f, 0f), 0.5f));
        }

        /// <summary>반경 0이면 겹침 판정을 하지 않는다 — 기존 테스트들이 반경을 안 쓰기 때문.</summary>
        [Test]
        public void Overlaps_IsFalseWhenNoRadius()
        {
            Assert.IsFalse(GridMovement.Overlaps(Vector2.zero, 0f, Vector2.zero, 0f));
        }

        [Test]
        public void IsBlocked_ByAnotherLivingEntity()
        {
            CombatEntity mover = At(0f, 0f, 0.5f);
            var others = new List<CombatEntity> { mover, At(1f, 0f, 0.5f) };

            Assert.IsTrue(GridMovement.IsBlocked(new Vector2(0.9f, 0f), 0.5f, mover, others, null),
                "겹치는 자리로는 못 간다");
            Assert.IsFalse(GridMovement.IsBlocked(new Vector2(-1f, 0f), 0.5f, mover, others, null),
                "반대쪽은 비어 있다");
        }

        [Test]
        public void IsBlocked_IgnoresSelfAndDead()
        {
            CombatEntity mover = At(0f, 0f, 0.5f);
            CombatEntity corpse = At(0.2f, 0f, 0.5f);
            corpse.hp = 0f;

            var others = new List<CombatEntity> { mover, corpse };

            Assert.IsFalse(GridMovement.IsBlocked(new Vector2(0.1f, 0f), 0.5f, mover, others, null),
                "자기 자신과 시체는 막지 않는다");
        }

        [Test]
        public void IsBlocked_ByRobot()
        {
            CombatEntity mover = At(0f, 0f, 0.5f);
            var robot = new CombatEntity
            {
                faction = Faction.Robot, label = "로봇",
                position = new Vector2(0.8f, 0f), hp = 100f, maxHp = 100f, radius = 0.5f,
            };

            Assert.IsTrue(GridMovement.IsBlocked(new Vector2(0.7f, 0f), 0.5f, mover, null, robot));
        }

        /// <summary>
        /// **밀어내지 않는다.** 막힌 개체는 제자리에 남을 뿐 상대를 옮기지 않는다 —
        /// 밀어내기가 있으면 뭉친 적들이 서로를 밀어 로봇 쪽으로 새어 들어간다.
        /// </summary>
        [Test]
        public void Blocking_DoesNotDisplaceTheBlocker()
        {
            CombatEntity mover = At(0f, 0f, 0.5f);
            CombatEntity blocker = At(0.8f, 0f, 0.5f);
            Vector2 blockerBefore = blocker.position;

            var others = new List<CombatEntity> { mover, blocker };
            bool blocked = GridMovement.IsBlocked(new Vector2(0.5f, 0f), 0.5f, mover, others, null);

            Assert.IsTrue(blocked);
            Assert.AreEqual(blockerBefore, blocker.position, "막은 쪽은 움직이지 않는다");
        }
    }
}
