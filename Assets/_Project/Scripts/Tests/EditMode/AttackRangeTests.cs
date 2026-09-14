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

        /// <summary>스폰 띠 — 사용자 확정 4~14(가정). 사거리 9.2 를 **가운데서 가른다**.</summary>
        private const float BandMin = 4f;
        private const float BandMax = 14f;

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
            // ⚠️ **런타임과 같은 띠를 넣는다**(§72-40). 안 넣으면 생성자 기본값
            // `arenaRadius` 한 점이 되어 **게임과 다른 판을 재게 된다.**
            sim.SetSpawnBand(BandMin, BandMax);
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
        /// <summary>
        /// **띠가 사거리를 가른다** — 안쪽에 난 적은 제자리 사격, 바깥에 난 적에는 걸어간다
        /// (2026-09-15 사용자 확정 · 4 ~ 14 · 사거리 9.2).
        ///
        /// ⚠️ 반경 하나였을 때는 **모든 적이 같은 거리**에서 나서 둘 중 하나만 일어났다.
        /// </summary>
        [Test]
        public void TheBandStraddlesTheRange()
        {
            Assert.Less(BandMin, Range, "띠 안쪽은 사거리 안 — 제자리 사격");
            Assert.Greater(BandMax, Range, "띠 바깥은 사거리 밖 — 걸어간다");

            var ctx = new AutoPilotContext
            {
                robotPos = Vector2.zero, arenaRadius = 6f, attackRange = Range,
                moveSpeed = 4.5f, dt = Dt,
                enemies = new List<CombatEntity>
                {
                    new CombatEntity { position = new Vector2(BandMin, 0f), hp = 30f, maxHp = 30f },
                },
            };
            Assert.AreEqual(Vector2.zero, AutoPilotPolicy.NextPosition(ctx), "안쪽 → 제자리");

            ctx.enemies = new List<CombatEntity>
            {
                new CombatEntity { position = new Vector2(BandMax, 0f), hp = 30f, maxHp = 30f },
            };
            Assert.Greater(AutoPilotPolicy.NextPosition(ctx).x, 0f, "바깥 → 다가간다");
        }

        /// <summary>
        /// **S1 을 돌리면 로봇이 실제로 걷는 틱이 있는가** — 사용자 요청(§72-40).
        ///
        /// 규칙이 서 있다는 것과 **판에서 일어난다**는 것은 다르다. 사거리를 줄이고도
        /// 로봇이 한 걸음도 안 걸었던 것이(§72-38) 그 차이였다.
        /// </summary>
        [Test]
        public void S1_TheRobotActuallyWalks()
        {
            CombatSimulation sim = Sim(Range);

            Vector2 last = sim.RobotPosition;
            int movedTicks = 0;
            float t = 0f;

            while (t < 100f && sim.Result == CombatResult.InProgress)
            {
                sim.Tick(Dt);
                t += Dt;
                if ((sim.RobotPosition - last).sqrMagnitude > 1e-6f) movedTicks++;
                last = sim.RobotPosition;
            }

            TestContext.WriteLine($"S1 — 걸은 틱 {movedTicks} / 총 {(int)(t / Dt)} · {t:F1}초 {sim.Result}");

            // ⚠️⚠️ **실측: 0 이다 — 띠를 넣어도 로봇은 안 걷는다.**
            //
            // 자동 조종은 **최근접 적**을 본다(`AutoPilotPolicy.NextPosition`). S1 은 적이 120 기라
            // 띠(4~14) 어딘가에 **거의 항상 사거리 9.2 안쪽인 적이 하나는 있다** —
            // 그러면 「사거리 안 → 제자리 사격」이 걸려 한 걸음도 안 뗀다.
            //
            // ⇒ **뿌리는 링도 띠도 아니라 「최근접」 + 적 수**다. 걷게 하려면 셋 중 하나다:
            //    ① 사거리를 띠 안쪽(4) 밑으로 · ② 표적 규칙을 최근접이 아닌 것으로
            //    · ③ 그대로 둔다(제자리 사격이 이 설계의 귀결이다).
            //
            // **이 단언은 지금 사실을 못 박는 것**이다 — 위 셋 중 하나가 정해져 로봇이 걷기
            // 시작하면 여기가 빨개지고, 그때가 규칙이 바뀐 때다. 기대를 단언해 초록으로
            // 만들어 두면 **안 걷는다는 사실이 문서에서 사라진다.**
            Assert.AreEqual(0, movedTicks,
                "적이 많으면 최근접이 늘 사거리 안이라 안 걷는다 — 규칙이 바뀌면 이 줄이 빨개진다");
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
