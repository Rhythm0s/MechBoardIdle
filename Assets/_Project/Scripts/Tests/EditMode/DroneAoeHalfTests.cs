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
    /// **광역형은 표적 하나에 절반만 준다** (2026-09-18 사용자 확정 — 「문서대로 코드를
    /// 절반으로」 · `260918_W01` 3-1 의 「표적 하나당 50 대 100」).
    ///
    /// ⚠️⚠️ **여기 전까지 코드에는 그 규칙이 아예 없었다.** 두 드론이 같은 `droneCharge` 를
    /// 써서, 광역형이 **여럿을 치면서 한 체당 피해도 같았다** — 「누적형 = S5의 해답자」라는
    /// 문서의 근거가 화면에서 성립하지 않았다.
    ///
    /// 📌 **줄인 것은 타격당 피해뿐이다** — 충전량도 수명도 그대로다(구현 가정 · 자산 basis).
    /// 문서가 둘 중 어느 쪽인지 안 적었고, W01 3-1 의 본전 계산이 **한 대 때릴 때의 수**를
    /// 맞대고 있어 그쪽으로 닫았다.
    /// </summary>
    public sealed class DroneAoeHalfTests
    {
        private const float D = 0.001f;

        private static RobotSetup DroneRobot(DroneKind kind, float factor)
        {
            var setup = new RobotSetup
            {
                hp = 100000f, mountCoef = 1f, moduleMult = 1f,
                attackRange = 0f, radius = 0f,
                multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
                lines = new List<AmmoLine>(),
                ammoCapacity = 40f,
                droneSlots = 1, droneReleaseRate = 10f, droneCharge = 100f,
                droneDamagePerHit = 100f,          // 한 번에 다 쓴다 — 나눠 쓰기는 이 시험의 축이 아니다
                droneAttackRange = 100f,
                droneAoeJudgeRadius = 2f,
                droneAoeDamageFactor = factor,
            };
            return setup;
        }

        private static List<EnemySpawn> One() => new List<EnemySpawn>
        {
            new EnemySpawn { label = "표적", hp = 1000000f, def = 0f, atk = 0f,
                moveSpeed = 0f, attackRange = 0.5f, attackInterval = 1f },
        };

        /// <summary>
        /// **첫 한 대의 피해**를 돌려준다.
        ///
        /// ⚠️ 판 전체의 합으로 재면 안 된다 — 두 종은 **모는 법이 다르다**(광역형은 돌고
        /// 누적형은 붙는다). 합은 타격 횟수까지 섞어 버려서 **비를 못 잰다**
        /// (첫 판에서 0.5 대신 2.75 가 나왔던 자리다).
        /// </summary>
        private static float DamageOnce(DroneKind kind, float factor)
        {
            var sim = new CombatSimulation(DroneRobot(kind, factor), One(),
                arenaRadius: 6f, challengeTime: 120f, spawnCadence: 0f)
            {
                StackDroneArrivalRate = kind == DroneKind.Stack ? 10f : 0f,
                AoeDroneArrivalRate = kind == DroneKind.Aoe ? 10f : 0f,
            };

            float before = 0f;
            for (int i = 0; i < 400 && sim.Result == CombatResult.InProgress; i++)
            {
                sim.Tick(0.05f);
                float now = sim.DroneDamageDealt;
                if (now > before) return now - before;   // 첫 타격의 몫
                before = now;
            }
            return 0f;
        }

        [Test]
        public void 광역형은_누적형의_절반을_준다()
        {
            float stack = DamageOnce(DroneKind.Stack, 0.5f);
            float aoe = DamageOnce(DroneKind.Aoe, 0.5f);

            Assert.Greater(stack, 0f, "시험 전제가 깨졌다 — 누적형이 한 번도 안 때렸다");
            Assert.Greater(aoe, 0f, "광역형이 한 번도 안 때렸다");
            Assert.AreEqual(0.5f, aoe / stack, 0.02f, "표적 하나당 피해가 절반이 아니다");
        }

        [Test]
        public void 비가_0_이면_구_거동이다()
        {
            // ⚠️ 값을 안 넣은 옛 판이 **조용히 0 피해**가 되지 않게 하는 자리다.
            float stack = DamageOnce(DroneKind.Stack, 0f);
            float aoe = DamageOnce(DroneKind.Aoe, 0f);

            Assert.AreEqual(stack, aoe, stack * 0.02f, "비가 0 인데 둘이 갈렸다 — 구 거동이 아니다");
        }

        [Test]
        public void 값은_자산에서_온다()
        {
            var bal = AssetDatabase.LoadAssetAtPath<BalanceConfig>(
                "Assets/_Project/ScriptableObjects/BalanceConfig.asset");
            Assert.IsNotNull(bal, "BalanceConfig 자산이 없다 — 생성기를 먼저 돌린다");

            Assert.AreEqual(0.5f, bal.droneAoeDamageFactor, D,
                "params.droneAoeDamageFactor = 0.5 (⚠️ 가정 표기)");
            Assert.AreEqual(2f, bal.droneAoeJudgeRadius, D,
                "params.droneAoeJudgeRadius = 2 (⚠️ 잠정 점값)");
        }
    }
}
