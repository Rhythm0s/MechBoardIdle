using MBI.Data;
using MBI.Editor;
using NUnit.Framework;
using UnityEditor;

namespace MBI.Tests
{
    /// <summary>
    /// 생성된 StageDefinition/EnemyDefinition SO가 balance_v4.json을 미러함을 검증(드리프트 감시 §7).
    /// 재도출이 아니라 원천 미러 — CombatAssetGenerator 산출이 계약과 일치하는지 확인.
    ///
    /// 실행 전 메뉴 MBI/Generate Combat Data 로 자산 생성 필요(없으면 Ignore).
    /// </summary>
    public sealed class StageDataTests
    {
        private const string StagesDir = "Assets/_Project/ScriptableObjects/Stages";
        private const string EnemiesDir = "Assets/_Project/ScriptableObjects/Enemies";
        private const float Delta = 0.001f;

        private BalanceJson _json;

        [SetUp]
        public void SetUp() => _json = BalanceJsonLoader.Load();

        private static StageDefinition LoadStage(string id) =>
            AssetDatabase.LoadAssetAtPath<StageDefinition>($"{StagesDir}/Stage_{id}.asset");

        [Test]
        public void AllStages_MirrorSource_Composition()
        {
            if (LoadStage("S1") == null)
                Assert.Ignore("Stage SO 없음 — 먼저 메뉴 'MBI/Generate Combat Data' 실행.");

            foreach (StageEntry s in _json.stages)
            {
                StageDefinition so = LoadStage(s.id);
                Assert.IsNotNull(so, $"{s.id} SO 존재");
                Assert.AreEqual(s.id, so.stageId, "stageId");
                Assert.AreEqual(s.challengeTime, so.challengeTime, Delta, $"{s.id} challengeTime");
                Assert.AreEqual(s.bossHp, so.bossHp, Delta, $"{s.id} bossHp");

                int jsonCount = s.composition != null ? s.composition.Length : 0;
                Assert.AreEqual(jsonCount, so.composition.Count, $"{s.id} 구성 수");

                for (int i = 0; i < jsonCount; i++)
                {
                    CompEntry c = s.composition[i];
                    StageComposition m = so.composition[i];
                    Assert.AreEqual(c.enemy, m.enemyKey, $"{s.id}[{i}] enemy");
                    Assert.AreEqual(c.count, m.count, $"{s.id}[{i}] count");
                    Assert.AreEqual(c.hp, m.hp, Delta, $"{s.id}[{i}] hp");
                    Assert.AreEqual(c.def, m.def, Delta, $"{s.id}[{i}] def");
                }
            }
        }

        /// <summary>
        /// S1 구성 — **보병 120 × HP 30 · 방어 0** (2026-09-14 · §72-14 사용자 확정).
        ///
        /// ⚠️ **구판은 40 × 270 · 방어 1 이었다.** 지우지 않고 `balance_v4.json` 의
        /// `compositionPrev` 에 폐기 표기로 남겼다.
        ///
        /// **왜 이 값인가.** 총 HP 가 **3600** 이고 시작 보드 출력 **36** 으로 나누면
        /// **정확히 100초** — 제한 시간 120초 안에 든다. 제안표는 S1~S4 가 전부
        /// 「요구치 DPS 로 100~107초」가 되게 짜여 있다(하네스로 확인).
        ///
        /// ⚠️ **값은 가정이다**(`compConfirmed` false · `compBasis` = 「가정 · §72-14 ·
        /// 촬영 뒤 설계 역기입」). 여기 숫자를 고칠 때는 json 이 먼저다.
        /// </summary>
        [Test]
        public void S1_Infantry100_Hp20_Def0()
        {
            StageDefinition so = LoadStage("S1");
            if (so == null) Assert.Ignore("Stage SO 없음 — 메뉴 'MBI/Generate Combat Data' 실행.");

            Assert.AreEqual(1, so.composition.Count);
            StageComposition c = so.composition[0];
            Assert.AreEqual("infantry", c.enemyKey);
            // 🗑️ **구 120 기 x HP 30 은 폐기**(2026-09-16 · `260916_W02` 2-4 갈래 3).
            //
            // 120 x 30 으로는 **못 깼다** — 하네스 실측 LoseTimeout 120초 · 남은 83/120.
            // 설계가 미리 정해 둔 갈래대로 **100 기 x HP 20** 으로 내려 다시 쟀다.
            // ⚠️ `compConfirmed` 는 여전히 false 다 — 이것도 가정이며 구판은
            //    `compositionPrev` 에 그대로 남아 있다.
            Assert.AreEqual(100, c.count);
            Assert.AreEqual(20f, c.hp, Delta);
            Assert.AreEqual(0f, c.def, Delta);

            // **총 HP ÷ 시작 보드 출력 = 제한 시간 안** — 이것이 이 값의 근거다.
            //
            // ⚠️ **나누는 수도 같이 바뀌었다.** 구 36 은 네 줄 보드의 출력이었고,
            //    09-15 에 시작 보드가 **두 줄**이 되면서 도달이 20 이다(요구치 18 의 출처).
            //    구 식(3600 / 36 = 100초)을 그대로 두면 **없는 보드로 나누는 셈**이 된다.
            float totalHp = c.count * c.hp;
            Assert.AreEqual(2000f, totalHp, Delta, "총 HP");
            Assert.LessOrEqual(totalHp / 20f, so.challengeTime,
                "두 줄 보드 도달 20 으로 제한 시간 안에 못 깬다");

            // 📌 **산수는 산수다** — 이 시험이 지키는 것은 「제한 시간 안에 들 수 있는 값인가」
            //    하나이고, **실제로 깨지는가는 하네스가 잰다.** 2026-09-16 실측으로는
            //    이 구성에서도 **LoseDead 49.6초 · 남은 41/100** 으로 못 깬다 —
            //    막는 것은 총 HP 가 아니라 로봇이 먼저 죽는 것이다(설계 판정 대기).
        }

        [Test]
        public void S6_Boss_Hp36000_Def12_AndBossHpField()
        {
            StageDefinition so = LoadStage("S6");
            if (so == null) Assert.Ignore("Stage SO 없음 — 메뉴 'MBI/Generate Combat Data' 실행.");

            Assert.AreEqual(StageReqType.Budget, so.reqType, "S6 reqType");
            Assert.AreEqual(StagePowerModel.Burst, so.powerModel, "S6 powerModel");
            Assert.AreEqual(36000f, so.bossHp, Delta, "S6 bossHp 필드");
            Assert.AreEqual(1, so.composition.Count);
            StageComposition c = so.composition[0];
            Assert.AreEqual("boss", c.enemyKey);
            Assert.AreEqual(1, c.count);
            Assert.AreEqual(36000f, c.hp, Delta);
            Assert.AreEqual(12f, c.def, Delta);
        }

        [Test]
        public void Enemies_MirrorAtk()
        {
            EnemyDefinition infantry =
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"{EnemiesDir}/Enemy_infantry.asset");
            if (infantry == null) Assert.Ignore("Enemy SO 없음 — 메뉴 'MBI/Generate Combat Data' 실행.");

            Assert.AreEqual(_json.Enemy("infantry").atk, infantry.atk, Delta, "보병 atk");
            Assert.AreEqual(EnemyRole.Infantry, infantry.role);
            Assert.IsFalse(infantry.atkConfirmed, "atk 미확정(confirmed:false) 표기 유지");
        }
    }
}
