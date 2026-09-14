using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **로봇 사거리 가정값 9.2** (2026-09-15 사용자 판정 · 설계 역기입 대기).
    ///
    /// ⚠️ **왜 재는가.** 구 값 100 은 사실상 무한이라 <see cref="AutoPilotPolicy"/> 의 규칙 둘 중
    /// 하나가 죽어 있었다 — 「사거리 밖 → 최근접 적을 향해 이동」이 **한 번도 안 걸렸다.**
    /// 사거리를 화면 안으로 줄이면 **걷기 시작하는 대신 멀리 스폰된 적을 못 친다** —
    /// 그 둘을 같이 재야 촬영에 쓸지 말지가 갈린다.
    ///
    /// ⚠️ **이 파일이 「S1 이 제한 시간 안에 깨지는가」를 재는 첫 장치다.** 그 전에는 그런 하네스가
    /// 없었고, 그래서 클리어 시간이 **산수로 돌다가 「하네스」라는 이름표가 붙었다**(§7 후보 2).
    /// </summary>
    public sealed class AttackRangeTests
    {
        /// <summary>가정값. `CombatTuning.robotAttackRangeTbd` 와 같아야 한다.</summary>
        private const float Range = 9.2f;

        private const float Dt = 0.05f;

        /// <summary>S1 구성 — 밸런스 제안표(§72-14): 보병 120 × HP30 · def 0.</summary>
        private static List<EnemySpawn> S1Infantry()
        {
            var list = new List<EnemySpawn>(120);
            for (int i = 0; i < 120; i++)
                list.Add(new EnemySpawn
                {
                    label = "보병", hp = 30f, def = 0f, atk = 5f,
                    moveSpeed = 1.5f, attackRange = 1f, attackInterval = 1f,
                    radius = 0f, projectileSpeed = 0f,
                });
            return list;
        }

        private static RobotSetup Robot(float attackRange) => new RobotSetup
        {
            hp = 3000f, mountCoef = 1f, moduleMult = 1f,
            attackRange = attackRange, radius = 0f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            // 시작 보드가 내는 것 — 표준탄 4발/초(도달 40).
            lines = new List<AmmoLine> { new AmmoLine(AmmoKind.Standard, 10f, 4f) },
            ammoCapacity = 40f,
            ammoStore = new AmmoInventory(40f),
            mountStackLimit = 10f,
        };

        private static CombatSimulation Sim(float attackRange)
        {
            var sim = new CombatSimulation(Robot(attackRange), S1Infantry(),
                arenaRadius: 6f, challengeTime: 100f, spawnCadence: 0.8f);
            sim.AmmoSupplyRate = 4f;   // 시작 보드 정상상태 4발/초
            return sim;
        }

        /// <summary>
        /// 몇 초에 승리하는가. 안 깨지면 <paramref name="limit"/> 를 돌려준다.
        ///
        /// ⚠️ **`Remaining == 0` 을 종료로 쓰지 않는다.** `Remaining` 은 **지금 살아 있는 수**이고
        /// `TotalEnemies` 는 **대기열**이라, 스폰 사이 빈 순간마다 0 이 된다 — 처음에 그렇게 재서
        /// **0.8초 클리어**라는 거짓 값이 나왔다. 승패는 시뮬이 `Result` 로 말한다.
        /// </summary>
        private static float ClearSeconds(CombatSimulation sim, float limit, out CombatResult result)
        {
            float t = 0f;
            while (t < limit)
            {
                sim.Tick(Dt);
                t += Dt;
                if (sim.Result != CombatResult.InProgress)
                {
                    result = sim.Result;
                    return t;
                }
            }
            result = sim.Result;
            return limit;
        }

        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// **자동 조종이 걷는가** — 사거리를 화면 안으로 줄인 이유가 이것이다.
        ///
        /// 적이 사거리 밖에 있으면 로봇은 최근접 적을 향해 움직여야 한다. 구 값 100 에서는
        /// 어떤 적도 사거리 밖이 아니라 **한 걸음도 안 걸었다.**
        /// </summary>
        [Test]
        public void OutOfRange_TheRobotWalksTowardTheNearestEnemy()
        {
            var enemies = new List<CombatEntity>
            {
                new CombatEntity { position = new Vector2(20f, 0f), hp = 30f, maxHp = 30f },
            };

            var ctx = new AutoPilotContext
            {
                robotPos = Vector2.zero, enemies = enemies,
                arenaRadius = 6f, attackRange = Range,
                moveSpeed = 4.5f, dt = Dt,
            };

            Vector2 next = AutoPilotPolicy.NextPosition(ctx);
            Assert.Greater(next.x, 0f, "사거리 밖이면 다가간다");

            // ⚠️ **구 값 100 이면 안 걷는다** — 그것이 고친 자리다.
            ctx.attackRange = 100f;
            Assert.AreEqual(Vector2.zero, AutoPilotPolicy.NextPosition(ctx),
                "사거리 100 에서는 20 유닛 떨어진 적도 사거리 안이라 제자리다");
        }

        /// <summary>
        /// ⚠️⚠️ **사거리를 줄여도 로봇은 여전히 안 걷는다 — 뿌리가 사거리가 아니었다.**
        ///
        /// 적은 **로봇을 중심으로 반경 `arenaRadius`(=6) 의 링 위**에 스폰된다
        /// (`CombatSimulation._spawnRingRadius = arenaRadius` · `SpawnRingRule`).
        /// 6 은 가정값 9.2 보다 **작으므로 스폰되는 순간 이미 사거리 안**이고,
        /// 「사거리 밖 → 이동」은 여전히 한 번도 안 걸린다.
        ///
        /// 실측도 같다 — S1 클리어가 **사거리 9.2 와 구 100 에서 똑같이 95.9초**다.
        ///
        /// **걷게 하려면 사거리 &lt; 스폰 링이어야 한다.** 스폰 링 반경은 **아직 미수신 값**이라
        /// (설계 판정 대기) 여기서 정하지 않는다. 이 시험은 그 자리를 **못 박아 두는 것**이다 —
        /// 링이 커지거나 사거리가 6 밑으로 내려가면 이 시험이 빨개지고, 그때가 답이 온 때다.
        /// </summary>
        [Test]
        public void SpawnRingIsInsideTheRange_SoTheRobotStillNeverWalks()
        {
            const float SpawnRing = 6f;   // = arenaRadiusTbd
            Assert.Less(SpawnRing, Range, "링이 사거리 안이다 — 스폰하자마자 사거리 안");

            var enemies = new List<CombatEntity>
            {
                new CombatEntity { position = new Vector2(SpawnRing, 0f), hp = 30f, maxHp = 30f },
            };

            var ctx = new AutoPilotContext
            {
                robotPos = Vector2.zero, enemies = enemies,
                arenaRadius = SpawnRing, attackRange = Range,
                moveSpeed = 4.5f, dt = Dt,
            };

            Assert.AreEqual(Vector2.zero, AutoPilotPolicy.NextPosition(ctx),
                "링 위에 스폰된 적은 이미 사거리 안이라 로봇이 안 걷는다");
        }

        /// <summary>사거리 안이면 제자리에서 쏜다 — 다가가지 않는다(카이팅 없음).</summary>
        [Test]
        public void InRange_TheRobotHoldsPosition()
        {
            var enemies = new List<CombatEntity>
            {
                new CombatEntity { position = new Vector2(5f, 0f), hp = 30f, maxHp = 30f },
            };

            var ctx = new AutoPilotContext
            {
                robotPos = Vector2.zero, enemies = enemies,
                arenaRadius = 6f, attackRange = Range,
                moveSpeed = 4.5f, dt = Dt,
            };

            Assert.AreEqual(Vector2.zero, AutoPilotPolicy.NextPosition(ctx), "사거리 안 → 제자리");
        }

        /// <summary>
        /// **S1 이 제한 시간 100초 안에 깨지는가** — 사거리를 줄인 뒤에도.
        ///
        /// ⚠️ **값을 단언하지 않고 기록한다.** 깨지는 시각은 밸런스가 정할 값이고
        /// (`compConfirmed: false`), 여기서는 **제한 시간 안에 서는가**만 본다.
        /// 실패하면 촬영 구성이 바뀌므로 그때 설계가 판정한다.
        /// </summary>
        [Test]
        public void S1_StillClearsWithinTheChallengeTime()
        {
            float shortRange = ClearSeconds(Sim(Range), 100f, out CombatResult shortResult);
            float oldRange = ClearSeconds(Sim(100f), 100f, out CombatResult oldResult);

            TestContext.WriteLine(
                $"S1 — 사거리 {Range} : {shortRange:F1}초 {shortResult} · 구 100 : {oldRange:F1}초 {oldResult}");

            Assert.AreEqual(CombatResult.Win, oldResult, "구 사거리에서는 이긴다(기준선)");
            Assert.AreEqual(CombatResult.Win, shortResult,
                $"사거리 {Range} 에서도 제한 시간 안에 이겨야 한다 (실측 {shortRange:F1}초 · {shortResult})");
        }
    }
}
