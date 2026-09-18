using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 회피할 때 **기체가 밀려난다** (2026-09-17 · `260917_W07` 3장 ·
    /// 사용자 「부스터 발생 시 이펙트 및 플레이어 로봇의 이동이 필요함」).
    ///
    /// 설계가 청한 시험 셋이 아래 셋이다 —
    /// ① 자동 회피가 **가장 가까운 적의 반대쪽**으로 **0.5칸** 가는가
    /// ② **막혔을 때 멈추고 무적은 살아 있는가**
    /// ③ **0.167초 뒤 이동이 끝나는가**
    ///
    /// ⚠️⚠️ **②가 이 파일의 핵심이다.** 「못 움직였으니 회피도 없던 일」로 두면
    /// 벽에 몰린 순간 생존층이 통째로 사라진다. 무적은 이동과 **무관**하다.
    /// </summary>
    public sealed class DodgeMotionTests
    {
        private const float D = 0.0001f;

        // ── 순수 로직 ────────────────────────────────────────────────────────

        [Test]
        public void 네_방향에_맞춘다()
        {
            // 보드도 전투도 4방향 문법이다 — 대각으로 밀면 그 문법이 깨진다.
            Assert.AreEqual(Vector2.right, DodgeMotion.SnapToFour(new Vector2(3f, 1f)));
            Assert.AreEqual(Vector2.up, DodgeMotion.SnapToFour(new Vector2(1f, 3f)));
            Assert.AreEqual(Vector2.left, DodgeMotion.SnapToFour(new Vector2(-2f, 0.5f)));
            Assert.AreEqual(Vector2.zero, DodgeMotion.SnapToFour(Vector2.zero),
                "0 벡터에 방향을 지어냈다");
        }

        [Test]
        public void 감속형이다()
        {
            // 처음에 빠르고 끝에서 멈춘다 — 앞 절반이 **뒤 절반보다 많이** 간다.
            float first = DodgeMotion.Ease(0.5f) - DodgeMotion.Ease(0f);
            float second = DodgeMotion.Ease(1f) - DodgeMotion.Ease(0.5f);

            Assert.Greater(first, second, "등속이거나 가속형이다 — 끝에서 뚝 끊긴다");
            Assert.AreEqual(0f, DodgeMotion.Ease(0f), D);
            Assert.AreEqual(1f, DodgeMotion.Ease(1f), D);
        }

        [Test]
        public void 다_가면_0_5칸이고_그_뒤로는_안_간다()
        {
            var m = new DodgeMotion();
            m.Begin(Vector2.right);

            Vector2 sum = Vector2.zero;
            for (int i = 0; i < 40; i++) sum += m.Step(0.01f);   // 0.4초 — 넉넉히

            Assert.AreEqual(DodgeMotion.DefaultDistance, sum.x, 0.001f, "정해진 거리가 아니다");
            Assert.AreEqual(0f, sum.y, D, "옆으로 샜다");
            Assert.IsFalse(m.IsMoving);
            Assert.AreEqual(Vector2.zero, m.Step(0.1f), "끝났는데 더 간다");
        }

        [Test]
        public void 시간으로_센다()
        {
            // ⚠️ 프레임으로 세면 30fps 기기에서 반 칸이 한 칸이 된다.
            var fine = new DodgeMotion();
            var coarse = new DodgeMotion();
            fine.Begin(Vector2.up);
            coarse.Begin(Vector2.up);

            Vector2 a = Vector2.zero, b = Vector2.zero;
            for (int i = 0; i < 20; i++) a += fine.Step(0.01f);    // 0.2초
            b += coarse.Step(0.2f);                                // 한 번에 0.2초

            Assert.AreEqual(a.y, b.y, 0.001f, "틱 간격이 달라지자 간 거리가 갈렸다");
        }

        // ── 값은 자산에서 온다 ───────────────────────────────────────────────

        [Test]
        public void 값_둘은_자산에서_오고_시간은_무적과_같다()
        {
            var bal = AssetDatabase.LoadAssetAtPath<BalanceConfig>(
                "Assets/_Project/ScriptableObjects/BalanceConfig.asset");
            Assert.IsNotNull(bal, "BalanceConfig 자산이 없다 — 생성기를 먼저 돌린다");

            Assert.AreEqual(1.25f, bal.dodgeMoveDistance, D,
                "params dodgeMoveDistance = 1.25 (2026-09-18 사용자 · 구 0.5 폐기)");
            Assert.AreEqual(0.167f, bal.dodgeMoveSeconds, D, "params dodgeMoveSeconds = 0.167");

            // ⚠️⚠️ **둘이 갈리면 무적이 끝난 뒤에도 밀리거나 그 반대가 된다.**
            //    설계가 「무적과 같이 시작하고 같이 끝난다」로 못 박은 자리다.
            Assert.AreEqual(DodgeSystem.InvincibleSeconds, bal.dodgeMoveSeconds, D,
                "이동 시간이 무적 시간과 갈렸다");

            Assert.AreEqual(bal.dodgeMoveDistance, DodgeMotion.DefaultDistance, D,
                "코드 기본값이 자산과 갈렸다");
            Assert.AreEqual(bal.dodgeMoveSeconds, DodgeMotion.DefaultSeconds, D);
        }

        // ── 전투에서 실제로 밀리는가 ─────────────────────────────────────────

        private static RobotSetup Robot() => new RobotSetup
        {
            hp = 100000f, mountCoef = 1f, moduleMult = 1f,
            attackRange = 0f, radius = 0.5f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = new List<AmmoLine>(),
            ammoCapacity = 0f, ammoStore = AmmoFixture.Pierce(0f, 0f),
            droneSlots = 0, droneReleaseRate = 0f, droneCharge = 0f, droneAttackRange = 0f,
        };

        /// <summary>제자리에서 때리는 적 — 두 번째는 길을 막는 몸으로 쓴다.</summary>
        private static List<EnemySpawn> Gunner(int count)
        {
            var list = new List<EnemySpawn>();
            for (int i = 0; i < count; i++)
                list.Add(new EnemySpawn { label = "포격", hp = 10000000f, def = 0f, atk = 10f,
                    moveSpeed = 0f, attackRange = 100f, attackInterval = 1f, });
            return list;
        }

        private static CombatSimulation Sim(bool blockers = false)
        {
            var sim = new CombatSimulation(Robot(), Gunner(blockers ? 2 : 1),
                arenaRadius: 6f, challengeTime: 1000f, spawnCadence: 0f);
            sim.BoosterCount = 2;
            sim.Dodge.AddStacks(8);     // 그릇을 채워 둔다 — 추진제 공급은 이 시험의 축이 아니다
            return sim;
        }

        private static void Run(CombatSimulation sim, float seconds, float dt = 0.01f)
        {
            int steps = Mathf.CeilToInt(seconds / dt);
            for (int i = 0; i < steps && sim.Result == CombatResult.InProgress; i++) sim.Tick(dt);
        }

        [Test]
        public void 자동_회피는_가장_가까운_적의_반대쪽으로_민다()
        {
            CombatSimulation sim = Sim();
            Vector2 before = sim.RobotPosition;

            Run(sim, 2f);

            Assert.Greater(sim.Dodge.TotalDodges, 0, "시험 전제가 깨졌다 — 회피가 한 번도 안 났다");

            Vector2 moved = sim.RobotPosition - before;
            Assert.Greater(moved.magnitude, 0f, "회피가 났는데 기체가 그대로다");

            // 적이 어느 쪽에 섰든 **적에게서 멀어지는 쪽**이어야 한다.
            Vector2 enemy = sim.Enemies[0].position;
            float wasAway = Vector2.Dot(moved.normalized, (before - enemy).normalized);
            Assert.Greater(wasAway, 0f, "적 쪽으로 밀렸다 — 반대쪽 규칙이 안 걸렸다");
        }

        [Test]
        public void 막히면_멈추고_무적은_그대로_성립한다()
        {
            // ⚠️⚠️ **이 시험이 생존층을 지킨다.** 벽(여기서는 적의 몸)에 막혀 한 칸도
            //    못 갔어도 회피는 성립해야 한다 — 「무적을 준 이유」가 이동이 아니기 때문이다.
            CombatSimulation sim = Sim(blockers: true);

            // 한 틱 돌려 적을 세운 뒤 자리를 잡는다 — 스폰 전에는 목록이 비어 있다.
            Run(sim, 0.02f);
            Assert.GreaterOrEqual(sim.Enemies.Count, 2, "시험 전제가 깨졌다 — 적이 둘이 아니다");

            Vector2 p = sim.RobotPosition;
            sim.Enemies[0].position = p + Vector2.left * 1.2f;    // 때리는 놈 — 왼쪽
            sim.Enemies[1].position = p + Vector2.right * 0.9f;   // 막는 놈 — 밀려날 쪽

            int before = sim.Dodge.TotalDodges;
            Run(sim, 1f);

            Assert.Greater(sim.Dodge.TotalDodges, before, "막혔다고 회피 자체가 안 났다");

            // **닿을 때까지 가고 거기서 멈춘다** — 뚫지도 밀어내지도 않는다.
            //    그래서 간 거리는 반 칸에 **못 미친다**(0 이 아니라 「덜 갔다」가 규칙이다).
            Assert.Less(sim.DodgeMovedDistance, DodgeMotion.DefaultDistance,
                "길이 막혔는데 반 칸을 다 갔다 — 뚫고 지나갔다");
        }

        [Test]
        public void 이동은_무적과_같이_끝난다()
        {
            CombatSimulation sim = Sim();
            Run(sim, 0.5f);
            Assert.Greater(sim.Dodge.TotalDodges, 0, "시험 전제가 깨졌다");

            // 무적이 끝난 순간에는 이동도 끝나 있어야 한다 — 값 둘이 같은 수이기 때문이다.
            var m = new DodgeMotion();
            m.Begin(Vector2.right);
            for (int i = 0; i < 17; i++) m.Step(0.01f);   // 0.17초 > 0.167초

            Assert.IsFalse(m.IsMoving, "0.167초가 지났는데 아직 밀리는 중이다");
        }
    }
}
