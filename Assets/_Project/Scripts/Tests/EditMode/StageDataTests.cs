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

        [Test]
        public void S1_Infantry40_Hp270_Def1()
        {
            StageDefinition so = LoadStage("S1");
            if (so == null) Assert.Ignore("Stage SO 없음 — 메뉴 'MBI/Generate Combat Data' 실행.");

            Assert.AreEqual(1, so.composition.Count);
            StageComposition c = so.composition[0];
            Assert.AreEqual("infantry", c.enemyKey);
            Assert.AreEqual(40, c.count);
            Assert.AreEqual(270f, c.hp, Delta);
            Assert.AreEqual(1f, c.def, Delta);
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
