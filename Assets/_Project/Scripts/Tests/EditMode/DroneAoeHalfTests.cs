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

            // ✅ **배수는 이제 파생값이다**(2026-09-19 · `260918_W02` 5장 이관).
            //    표의 좌표 둘(누적형 100 · 광역형 **50**)을 나눠 나온다 — 0.5 는 그 몫이고
            //    **이 수 자체를 적은 자리는 어디에도 없다.** 🗑️ json `droneAoeDamageFactor` 폐기.
            //    ⚠️ 그래서 이 단언은 **거동 불변의 증거**다 — 이관 전후로 같아야 한다.
            Assert.AreEqual(0.5f, bal.droneAoeDamageFactor, D,
                "광역형 좌표 50 ÷ 누적형 100 = 0.5 (이관 전 값과 같아야 한다)");

            // ⚠️⚠️ **수를 박지 않는다**(2026-09-21 · 09-20 「낡은 표」와 같은 병).
            //
            // 종전에는 <c>Assert.AreEqual(3f, ...)</c> 였다. 09-21 에 사용자가 **5칸**으로
            // 뒤집자 이 시험이 **틀린 수를 지키며** 붉은불을 냈다 — 지켜야 할 것은
            // 「3 이다」가 아니라 **「자산이 json 을 따라온다」**이다.
            //
            // 📌 값이 바뀔 자리를 시험에 베껴 두면, 값이 바뀔 때마다 **시험이 두 번째로
            //    고쳐야 할 곳**이 된다 — 그러다 한 번 안 고치면 초록불이 거짓말을 한다.
            //    json 을 읽어 견주면 고칠 곳이 하나로 준다.
            float inJson = JsonRadius();
            Assert.AreEqual(inJson, bal.droneAoeJudgeRadius, D,
                $"자산 {bal.droneAoeJudgeRadius} 인데 json 은 {inJson} 다 — 생성기를 다시 돌려라");
        }

        /// <summary>
        /// `balance_v4.json` 의 `droneAoeJudgeRadius` 를 **파일에서** 읽는다.
        ///
        /// ⚠️ 파서를 들이지 않는다 — 열쇠 뒤의 `"value"` 하나만 집는다.
        /// 못 찾으면 **죽는다**(0 을 돌려주면 자산이 0 일 때 통과해 버린다).
        /// </summary>
        private static float JsonRadius()
        {
            const string path = "balance_v4.json";
            Assert.IsTrue(System.IO.File.Exists(path), $"{path} 이 없다");

            string text = System.IO.File.ReadAllText(path);
            int at = text.IndexOf("\"droneAoeJudgeRadius\"", System.StringComparison.Ordinal);
            Assert.Greater(at, 0, "json 에 droneAoeJudgeRadius 가 없다");

            var m = System.Text.RegularExpressions.Regex.Match(
                text.Substring(at), "\"value\"\\s*:\\s*(-?[0-9.]+)");
            Assert.IsTrue(m.Success, "droneAoeJudgeRadius 뒤에 value 가 없다");
            return float.Parse(m.Groups[1].Value,
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
