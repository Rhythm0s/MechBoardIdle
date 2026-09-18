using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using MBI.Editor;
using NUnit.Framework;
using UnityEditor;

namespace MBI.Tests
{
    /// <summary>
    /// **표(CSV)가 낸 값 = 지금 자산의 값** (2026-09-18 사용자 확정 · 밸런스 표 이관의 첫 관문).
    ///
    /// ⚠️⚠️ **이 시험이 서야 그 뒤의 차이가 표의 뜻이 된다.** 옮기는 도중에 값이 한 칸이라도
    /// 흔들리면, 나중에 밸런스가 달라졌을 때 **표를 고쳐서인지 옮기다 샌 것인지** 못 가른다.
    /// 그래서 첫 관문은 「아무것도 안 바뀐다」이다.
    ///
    /// 📌 **자산을 읽는다 — 생성기를 다시 돌리지 않는다.** 시험이 구우면 「구운 것과 구운 것」을
    /// 견주게 되어 아무것도 안 지킨다. 지켜야 할 것은 **리포에 든 자산**이 표와 같다는 사실이다.
    ///
    /// ⚠️ 표가 없으면 **죽는다**(건너뛰지 않는다) — 표가 원천인데 없는 것은 결함이다.
    /// </summary>
    public sealed class GameDataTableTests
    {
        private const string Enemies = "Assets/_Project/ScriptableObjects/Enemies";
        private const string Stages = "Assets/_Project/ScriptableObjects/Stages";
        private const string RobotA = "Assets/_Project/ScriptableObjects/Robots/Robot_A.asset";
        private const string Tuning = "Assets/_Project/ScriptableObjects/CombatTuning.asset";

        private const float D = 0.0001f;

        [Test]
        public void 표_셋이_열리고_줄이_있다()
        {
            foreach (string n in new[] { "STAGE_COMP_DATA", "ENEMY_DATA", "WEAPON_DATA" })
            {
                CsvTable t = GameDataTables.Load(n);
                Assert.Greater(t.Fields.Count, 0, n + " 에 열이 없다");
                Assert.Greater(t.Rows.Count, 0, n + " 에 줄이 없다");
            }
        }

        [Test]
        public void Dev_열은_CSV_에_안_실린다()
        {
            // 📌 컨버팅 제외 규약 — 사람이 보는 칸이 게임으로 새면 안 된다.
            foreach (string n in new[] { "STAGE_COMP_DATA", "ENEMY_DATA", "WEAPON_DATA" })
                foreach (string f in GameDataTables.Load(n).Fields)
                    Assert.IsFalse(f.StartsWith("Dev_"), n + " 에 " + f + " 가 남아 있다");
        }

        [Test]
        public void 적_표의_값이_자산과_같다()
        {
            CsvTable t = GameDataTables.Load("ENEMY_DATA");

            int seen = 0;
            foreach (CsvTable.Row r in t.Rows)
            {
                string key = GameDataTables.EnemyKeyOf(r.Int("EnemyKey"));
                var d = AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"{Enemies}/Enemy_{key}.asset");
                Assert.IsNotNull(d, key + " 자산이 없다");

                Assert.AreEqual(r.Num("Atk"), d.atk, D, key + " 공격력");
                Assert.AreEqual(r.Num("MoveSpeed"), d.moveSpeed, D, key + " 이동 속도");
                Assert.AreEqual(r.Num("AttackRange"), d.attackRange, D, key + " 사거리");
                Assert.AreEqual(r.Num("AttackInterval"), d.attackInterval, D, key + " 공격 주기");
                Assert.AreEqual(r.Num("ProjectileSpeed"), d.projectileSpeed, D, key + " 투사체 속도");
                Assert.AreEqual(r.Int("ViewScale", 1), d.viewScale, key + " 그림 배율");
                seen++;
            }
            Assert.AreEqual(4, seen, "적은 넷이다");
        }

        [Test]
        public void 스테이지_구성이_자산과_같다()
        {
            Dictionary<string, List<CsvTable.Row>> byStage =
                GameDataTables.CompositionByStage(GameDataTables.Load("STAGE_COMP_DATA"));

            foreach (KeyValuePair<string, List<CsvTable.Row>> kv in byStage)
            {
                var d = AssetDatabase.LoadAssetAtPath<StageDefinition>($"{Stages}/Stage_{kv.Key}.asset");
                Assert.IsNotNull(d, kv.Key + " 자산이 없다");
                Assert.AreEqual(kv.Value.Count, d.composition.Count, kv.Key + " 구성 줄 수");

                for (int i = 0; i < kv.Value.Count; i++)
                {
                    CsvTable.Row r = kv.Value[i];
                    StageComposition c = d.composition[i];
                    string at = $"{kv.Key} {i}번째";

                    // ⚠️ **차례까지 본다** — 스폰 차례가 바뀌면 같은 값이라도 다른 판이다.
                    Assert.AreEqual(GameDataTables.EnemyKeyOf(r.Int("EnemyKey")), c.enemyKey, at + " 종류");
                    Assert.AreEqual(r.Int("Count"), c.count, at + " 마리 수");
                    Assert.AreEqual(r.Num("Hp"), c.hp, D, at + " 체력");
                    Assert.AreEqual(r.Num("Def"), c.def, D, at + " 방어");
                }
            }
        }

        [Test]
        public void 무기_표가_로봇A_자산과_같다()
        {
            CsvTable t = GameDataTables.Load("WEAPON_DATA");
            var robot = AssetDatabase.LoadAssetAtPath<RobotDefinition>(RobotA);
            Assert.IsNotNull(robot, "로봇A 자산이 없다");

            var rows = new List<CsvTable.Row>();
            foreach (CsvTable.Row r in t.Rows)
                if (r.Int("RobotID") == 1 && GameDataTables.AmmoKindOf(r.Int("AmmoKind")) != null)
                    rows.Add(r);

            Assert.AreEqual(rows.Count, robot.weapons.Count, "탄종 줄 수");
            for (int i = 0; i < rows.Count; i++)
            {
                AmmoKind kind = GameDataTables.AmmoKindOf(rows[i].Int("AmmoKind")).Value;
                Assert.AreEqual(kind, robot.weapons[i].kind, i + "번째 탄종");
                Assert.AreEqual(rows[i].Num("Damage"), robot.weapons[i].damagePerShot, D, i + "번째 발당 피해");
                Assert.AreEqual(rows[i].Num("ShotsPerSec"), robot.weapons[i].shotsPerSec, D, i + "번째 초당 발사");
            }
        }

        [Test]
        public void 무기_표의_네_칸이_조율_자산과_같다()
        {
            // ⚠️ 이 넷은 `CombatTuning` 의 「안 덮는다」 계약이 갈린 자리다 —
            //    표가 원천이 됐으므로 생성기가 덮고, 그 사실을 이 시험이 지킨다.
            CsvTable t = GameDataTables.Load("WEAPON_DATA");
            var tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>(Tuning);
            Assert.IsNotNull(tuning, "조율 자산이 없다");

            foreach (CsvTable.Row r in t.Rows)
            {
                if (r.Int("RobotID") == 1 && GameDataTables.AmmoKindOf(r.Int("AmmoKind")) != null)
                {
                    Assert.AreEqual(r.Int("ShotsPerRound", 1), tuning.shotsPerRound, "한 발당 나가는 수");
                    Assert.AreEqual(r.Num("ShotDamageFactor", 1f), tuning.shotDamageFactor, D, "한 발 피해 배수");
                }
                if (r.Int("RobotID") == 2)
                {
                    Assert.AreEqual(r.Num("HitInterval"), tuning.droneHitIntervalTbd, D, "드론 타격 간격");
                    Assert.AreEqual(r.Num("DamageFraction"), tuning.droneDamageFractionTbd, D, "드론 피해 몫");
                }
            }
        }

        [Test]
        public void 라인_스펙과_광역_배수가_밸런스_자산과_같다()
        {
            // ⚠️ 이 둘은 **밸런스 생성기**가 굽는다 — 전투 생성기와 다른 문이라 따로 지킨다.
            //    표 → BalanceConfig 로 옮긴 첫날의 관문이다(값이 안 바뀌어야 한다).
            GameDataTables.WeaponTableValues w = GameDataTables.ReadWeapons();
            var c = AssetDatabase.LoadAssetAtPath<BalanceConfig>(
                "Assets/_Project/ScriptableObjects/BalanceConfig.asset");
            Assert.IsNotNull(c, "밸런스 자산이 없다");

            Assert.AreEqual(w.lineSpec[0], c.LineSpecOf(AmmoKind.Pierce), D, "관통 라인 스펙");
            Assert.AreEqual(w.lineSpec[1], c.LineSpecOf(AmmoKind.Standard), D, "표준 라인 스펙");
            Assert.AreEqual(w.lineSpec[2], c.LineSpecOf(AmmoKind.Explosive), D, "폭발 라인 스펙");
            Assert.AreEqual(w.aoeDamageFactor, c.droneAoeDamageFactor, D, "광역 피해 배수");
        }

        [Test]
        public void 모르는_enum_이면_죽는다()
        {
            // 📌 조용히 첫 항목으로 떨어지면 「보병이 왜 이렇게 많지」가 된다.
            Assert.Throws<System.FormatException>(() => GameDataTables.EnemyKeyOf(9));
            Assert.Throws<System.FormatException>(() => GameDataTables.AmmoKindOf(9));
        }
    }
}
