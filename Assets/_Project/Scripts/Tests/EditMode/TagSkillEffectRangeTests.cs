using System.Collections.Generic;
using MBI.Combat;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **태그 스킬 연출이 판정만큼 넓은가** (2026-09-18 사용자 확정 · `260917_W09` 3장 2번).
    ///
    /// 판정은 09-08 부터 「화면 안의 적 전부」였는데, 연출은 09-16 육안 뒤
    /// 「적 무리 방향 한 번」으로 좁혀져 있었다 — **맞은 줄 모르는 적이 남았다.**
    /// 사용자가 **연출을 판정 쪽으로 넓히는 것**으로 닫았다.
    ///
    /// ⚠️⚠️ **여기서 지키는 것은 한 가지다 — 연출이 화면을 다시 재지 않는 것.**
    /// 연출이 제 잣대로 범위를 재면 어제 광역형 드론에서 난 일(그림만 고쳐지고
    /// 판정은 옛 수를 쓰던 것)이 방향만 바꿔 되풀이된다. 그래서 연출은
    /// **시뮬이 판정에 쓴 목록**(<c>LastTagSkillTargets</c>)을 그대로 읽는다.
    ///
    /// 🗑️ 형태(360도 탄환비 · 표적마다 빔 하나)는 **구현 가정**이다 —
    /// 설계 연출 문안이 서면 그때 바뀔 수 있고, 값 둘은 `CombatTuning` 에 가정 표기로 있다.
    /// </summary>
    public sealed class TagSkillEffectRangeTests
    {
        private const float D = 0.001f;

        // ── 각을 고르는 법 ───────────────────────────────────────────────────

        [Test]
        public void 한_바퀴를_고르게_나눈다()
        {
            float[] a = TagSkillEffect.FullCircleAngles(30);

            Assert.AreEqual(30, a.Length);
            Assert.AreEqual(0f, a[0], D);
            for (int i = 1; i < a.Length; i++)
                Assert.AreEqual(12f, a[i] - a[i - 1], D, "간격이 고르지 않다");

            // ⚠️ 0도와 360도가 겹치면 그 자리만 탄환이 둘이 된다.
            Assert.Less(a[a.Length - 1], 360f, "끝 각이 첫 각과 겹친다");
        }

        [Test]
        public void 표적마다_한_각이다()
        {
            var origin = new Vector2(1f, 1f);
            var targets = new List<Vector2>
            {
                origin + Vector2.right * 3f,   // 0도
                origin + Vector2.up * 2f,      // 90도
                origin + Vector2.left * 5f,    // 180도
            };

            float[] a = TagSkillEffect.AnglesToward(origin, targets, maxBeams: 16);

            Assert.AreEqual(3, a.Length, "표적 수만큼 안 나왔다");
            Assert.AreEqual(0f, a[0], D);
            Assert.AreEqual(90f, a[1], D);
            Assert.AreEqual(180f, Mathf.Abs(a[2]), D);
        }

        [Test]
        public void 표적이_상한보다_많으면_고르지_않고_고르게_편다()
        {
            // 어느 표적을 버릴지 고를 잣대가 없다 — 그럴 때는 한 바퀴를 고르게 나눈다.
            var origin = Vector2.zero;
            var targets = new List<Vector2>();
            for (int i = 0; i < 40; i++) targets.Add(new Vector2(i + 1, 0f));

            float[] a = TagSkillEffect.AnglesToward(origin, targets, maxBeams: 16);

            Assert.AreEqual(16, a.Length, "상한을 넘겼다");
            Assert.AreEqual(TagSkillEffect.FullCircleAngles(16), a, "한 바퀴를 고르게 나눈 각이 아니다");
        }

        [Test]
        public void 표적이_없으면_빈_목록이다()
        {
            // 지어낸 방향을 주지 않는다 — 부르는 쪽이 옛 길로 떨어지는 것을 보고 정한다.
            Assert.AreEqual(0, TagSkillEffect.AnglesToward(Vector2.zero, null, 16).Length);
            Assert.AreEqual(0, TagSkillEffect.AnglesToward(Vector2.zero, new List<Vector2>(), 16).Length);
        }

        // ── 연출이 읽는 목록이 판정이 쓴 목록인가 ────────────────────────────

        private static Dictionary<MountItem, float> Stacks() =>
            new Dictionary<MountItem, float>
            {
                { MountItem.Pierce, 10f }, { MountItem.Standard, 10f },
                { MountItem.Explosive, 10f }, { MountItem.Drone, 10f },
            };

        private static RobotSetup Robot() => new RobotSetup
        {
            hp = 100000f, mountCoef = 1f, moduleMult = 1f,
            attackRange = 0f, radius = 0f,   // 평상시 사격은 안 닿게 둔다
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = new List<AmmoLine> { new AmmoLine(AmmoKind.Pierce, 20f, 1f) },
            ammoCapacity = 40f,
            droneSlots = 0, droneReleaseRate = 0f, droneCharge = 0f, droneAttackRange = 0f,
        };

        private static List<EnemySpawn> Dummies(int count)
        {
            var list = new List<EnemySpawn>();
            for (int i = 0; i < count; i++)
                list.Add(new EnemySpawn { label = "표적", hp = 1000000f, def = 0f, atk = 0f,
                    moveSpeed = 0f, attackRange = 0.5f, attackInterval = 1f });
            return list;
        }

        [Test]
        public void 연출은_판정이_쓴_목록을_그대로_읽는다()
        {
            var mountA = new MountLoad(1, Stacks());
            var mountB = new MountLoad(1, Stacks());
            var sim = new CombatSimulation(Robot(), Robot(), mountA, mountB,
                Dummies(2), arenaRadius: 6f, challengeTime: 120f, spawnCadence: 0f);

            mountA.Load(MountItem.Pierce, 5f);
            sim.AmmoSupplyRate = 0f;
            sim.StandbyAmmoSupplyRate = 20f;

            // 적은 첫 틱에 스폰된다 — 한 체를 화면 밖으로 밀어 두려면 그 뒤라야 한다.
            for (int i = 0; i < 2; i++) sim.Tick(0.05f);
            Assert.AreEqual(2, sim.Enemies.Count, "시험 전제가 깨졌다 — 둘이 안 섰다");
            sim.Enemies[1].position = new Vector2(100f, 100f);
            sim.SetVisibleBounds(new Rect(-10f, -10f, 20f, 20f));

            for (int i = 0; i < 40 && sim.Result == CombatResult.InProgress; i++) sim.Tick(0.05f);

            Assert.IsTrue(sim.Tag.LastTagFiredSkill, "시험 전제가 깨졌다 — 스킬이 안 나갔다");

            // 화면 밖 한 체는 판정에서 빠졌으므로 **연출 목록에도 없어야 한다.**
            Assert.AreEqual(1, sim.LastTagSkillTargets.Count,
                "연출 목록이 판정 목록과 갈렸다 — 화면 밖 적까지 세었다");
            Assert.AreEqual(sim.Enemies[0].position, sim.LastTagSkillTargets[0],
                "목록에 든 자리가 맞은 적의 자리가 아니다");
        }
    }
}
