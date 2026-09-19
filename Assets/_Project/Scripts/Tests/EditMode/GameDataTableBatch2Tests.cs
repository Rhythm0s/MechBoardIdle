using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using MBI.Editor;
using NUnit.Framework;
using UnityEditor;

namespace MBI.Tests
{
    /// <summary>
    /// **둘째 묶음 아홉 장이 지금 사실과 같은가** (2026-09-19 사용자 확정 · 플랜 §86-4).
    ///
    /// ⚠️⚠️ **이 표들은 아직 아무도 안 읽는다.** 배선은 영상 이후다(사용자 정정). 그런데도
    /// 시험을 붙이는 까닭은 **첫 관문이 「아무것도 안 바뀐다」이기 때문**이다 —
    /// 옮기는 도중에 한 칸이라도 흔들리면, 나중에 값이 달라졌을 때 **표를 고쳐서인지
    /// 옮기다 샌 것인지** 못 가른다. 읽는 쪽이 생긴 날 그 구분이 없으면 늦는다.
    ///
    /// 📌 **자산·json 을 읽는다 — 생성기를 다시 돌리지 않는다.** 시험이 구우면
    /// 「구운 것과 구운 것」을 견주게 되어 아무것도 안 지킨다.
    ///
    /// ⚠️ 표가 없으면 **죽는다**(건너뛰지 않는다).
    /// </summary>
    public sealed class GameDataTableBatch2Tests
    {
        private const string SoRoot = "Assets/_Project/ScriptableObjects";
        private const float D = 0.0001f;

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(a, path + " 자산이 없다");
            return a;
        }

        [Test]
        public void 아홉_장이_열리고_줄이_있다()
        {
            foreach (string n in new[]
            {
                "NODE_DATA", "RECIPE_DATA", "MODULE_DATA", "ROBOT_DATA", "STAGE_DATA",
                "COMBAT_RULE_DATA", "ECONOMY_DATA", "LOGISTICS_DATA", "BALANCE_PARAM_DATA",
            })
            {
                CsvTable t = GameDataTables.Load(n);
                Assert.Greater(t.Fields.Count, 0, n + " 에 열이 없다");
                Assert.Greater(t.Rows.Count, 0, n + " 에 줄이 없다");
                foreach (string f in t.Fields)
                    Assert.IsFalse(f.StartsWith("Dev_"), n + " 에 " + f + " 가 남아 있다");
            }
        }

        // ── 자산 한 장 = 표 한 장 (칸마다 한 줄) ─────────────────────────

        /// <summary>
        /// 칸 이름으로 자산을 직접 물어본다.
        ///
        /// 📌 <see cref="SerializedObject"/> 를 쓰는 까닭 — 표가 **자산의 칸 이름 그대로**를
        /// 들고 있으므로, 이름으로 물어보면 **칸이 늘어도 시험을 안 고쳐도 된다.**
        /// 반대로 칸 이름이 바뀌면 여기서 바로 걸린다(그게 이 시험의 값어치다).
        /// </summary>
        private static void 칸마다_같다(string table, string assetPath)
        {
            CsvTable t = GameDataTables.Load(table);
            t.Require("FieldName", "Value");

            var so = new SerializedObject(Load<UnityEngine.Object>(assetPath));
            int seen = 0;

            foreach (CsvTable.Row r in t.Rows)
            {
                string name = r.Text("FieldName");
                SerializedProperty p = so.FindProperty(name);
                Assert.IsNotNull(p, $"{table}: 자산에 '{name}' 칸이 없다 — 이름이 바뀌었는가");

                string at = $"{table} · {name}";
                switch (p.propertyType)
                {
                    case SerializedPropertyType.Float:
                        Assert.AreEqual(p.floatValue, r.Num("Value"), D, at);
                        break;
                    case SerializedPropertyType.Integer:
                        Assert.AreEqual(p.intValue, r.Int("Value"), at);
                        break;
                    case SerializedPropertyType.Boolean:
                        Assert.AreEqual(p.boolValue, r.Int("Value") != 0, at);
                        break;
                    case SerializedPropertyType.String:
                        Assert.AreEqual(p.stringValue, r.Text("Value"), at);
                        break;
                    default:
                        // 스프라이트 배열 같은 칸은 값이 아니라 배선이라 표에 안 실린다.
                        Assert.Fail($"{at}: 표에 실릴 수 없는 종류다 — {p.propertyType}");
                        break;
                }
                seen++;
            }
            Assert.Greater(seen, 0, table + " 에서 아무 칸도 안 봤다");
        }

        [Test]
        public void 전투_규칙이_조율_자산과_같다()
            => 칸마다_같다("COMBAT_RULE_DATA", $"{SoRoot}/CombatTuning.asset");

        [Test]
        public void 경제가_경제_자산과_같다()
            => 칸마다_같다("ECONOMY_DATA", $"{SoRoot}/EconomyConfig.asset");

        [Test]
        public void 물류_총량이_물류_자산과_같다()
            => 칸마다_같다("LOGISTICS_DATA", $"{SoRoot}/LogisticsConfig.asset");

        // ── 여러 장짜리 ─────────────────────────────────────────────────

        [Test]
        public void 노드가_자산과_같다()
        {
            CsvTable t = GameDataTables.Load("NODE_DATA");
            int seen = 0;
            foreach (CsvTable.Row r in t.Rows)
            {
                string id = r.Text("NodeID");
                var d = Load<NodeDefinition>($"{SoRoot}/Nodes/Node_{id}.asset");

                Assert.AreEqual(d.displayName, r.Text("TID_Name"), id + " 이름");
                Assert.AreEqual((int)d.type, r.Int("Type"), id + " 종류");
                Assert.AreEqual(d.implemented, r.Int("Implemented") != 0, id + " 구현 여부");
                Assert.AreEqual(d.resources.powerDraw, r.Num("PowerDraw"), D, id + " 전력 소비");
                Assert.AreEqual(d.resources.powerSupply, r.Num("PowerSupply"), D, id + " 전력 공급");
                Assert.AreEqual(d.resources.ammoProduce, r.Num("AmmoProduce"), D, id + " 탄약 생산");
                Assert.AreEqual(d.resources.ammoConsume, r.Num("AmmoConsume"), D, id + " 탄약 소비");
                Assert.AreEqual(d.resources.heatGenerate, r.Num("HeatGenerate"), D, id + " 발열");
                seen++;
            }
            Assert.AreEqual(8, seen, "노드는 여덟이다");
        }

        [Test]
        public void 조합이_자산과_같다()
        {
            // ⚠️ **차례까지 본다** — 같은 노드의 조합 차례가 바뀌면 기본 조합이 달라진다.
            CsvTable t = GameDataTables.Load("RECIPE_DATA");

            var byNode = new Dictionary<string, List<CsvTable.Row>>();
            foreach (CsvTable.Row r in t.Rows)
            {
                string id = r.Text("NodeID");
                if (!byNode.TryGetValue(id, out List<CsvTable.Row> list))
                    byNode[id] = list = new List<CsvTable.Row>();
                list.Add(r);
            }

            foreach (KeyValuePair<string, List<CsvTable.Row>> kv in byNode)
            {
                var d = Load<NodeDefinition>($"{SoRoot}/Nodes/Node_{kv.Key}.asset");
                Assert.AreEqual(d.recipes.Count, kv.Value.Count, kv.Key + " 조합 줄 수");

                for (int i = 0; i < kv.Value.Count; i++)
                {
                    CsvTable.Row r = kv.Value[i];
                    NodeRecipe c = d.recipes[i];
                    string at = $"{kv.Key} {i}번째";

                    Assert.AreEqual((int)c.kind, r.Int("Kind"), at + " 갈래");
                    Assert.AreEqual(c.displayName, r.Text("TID_Name"), at + " 이름");
                    Assert.AreEqual((int)c.output, r.Int("Output"), at + " 산출");
                    Assert.AreEqual(c.outputPerSec, r.Num("OutputPerSec"), D, at + " 초당 산출");
                    Assert.AreEqual(c.requiredProduction, r.Num("RequiredProduction"), D,
                        at + " 필요 생산치");
                    Assert.AreEqual(c.stackLimitTbd, r.Num("StackLimit"), D, at + " 쌓임 상한");
                    Assert.AreEqual(c.implemented, r.Int("Implemented") != 0, at + " 구현 여부");

                    Assert.LessOrEqual(c.inputs.Count, 2, at + " 입력이 셋 이상이면 열을 늘려야 한다");
                    if (c.inputs.Count > 0)
                    {
                        Assert.AreEqual((int)c.inputs[0].kind, r.Int("Input1Kind"), at + " 입력1");
                        Assert.AreEqual(c.inputs[0].perOutput, r.Num("Input1PerOutput"), D,
                            at + " 입력1 개당");
                    }
                    if (c.inputs.Count > 1)
                    {
                        Assert.AreEqual((int)c.inputs[1].kind, r.Int("Input2Kind"), at + " 입력2");
                        Assert.AreEqual(c.inputs[1].perOutput, r.Num("Input2PerOutput"), D,
                            at + " 입력2 개당");
                    }
                }
            }
        }

        [Test]
        public void 모듈이_자산과_같다()
        {
            CsvTable t = GameDataTables.Load("MODULE_DATA");
            int seen = 0;
            foreach (CsvTable.Row r in t.Rows)
            {
                string file = r.Text("Symbol") == "M" ? "Module_M" : "Module_R";
                var d = Load<ModuleDefinition>($"{SoRoot}/Modules/{file}.asset");

                Assert.AreEqual(d.moduleId, r.Text("ModuleID"), file + " 키");
                Assert.AreEqual(d.displayName, r.Text("TID_Name"), file + " 이름");
                Assert.AreEqual((int)d.kind, r.Int("Kind"), file + " 종류");
                Assert.AreEqual(d.outputMultiplier, r.Num("OutputMultiplier"), D, file + " 생산 배수");
                Assert.AreEqual(d.inputMultiplier, r.Num("InputMultiplier"), D, file + " 입력 배수");
                Assert.AreEqual(d.powerLoadMultiplier, r.Num("PowerLoadMultiplier"), D,
                    file + " 전력 부하 배수");
                seen++;
            }
            Assert.AreEqual(2, seen, "모듈은 둘이다");
        }

        [Test]
        public void 로봇이_자산과_같다()
        {
            CsvTable t = GameDataTables.Load("ROBOT_DATA");
            int seen = 0;
            foreach (CsvTable.Row r in t.Rows)
            {
                string id = r.Text("RobotID");
                string file = id == "robotA" ? "Robot_A" : id == "robotB" ? "Robot_B" : "Robot_Fusion";
                var d = Load<RobotDefinition>($"{SoRoot}/Robots/{file}.asset");

                Assert.AreEqual(d.displayName, r.Text("TID_Name"), id + " 이름");
                Assert.AreEqual(d.consumptionCap, r.Num("ConsumptionCap"), D, id + " 소비 상한");
                Assert.AreEqual(d.mountCoef, r.Num("MountCoef"), D, id + " 마운트 계수");
                Assert.AreEqual(d.enhancedMountCoef, r.Num("EnhancedMountCoef"), D, id + " 강화 계수");
                Assert.AreEqual(d.moduleMult, r.Num("ModuleMult"), D, id + " 모듈 배수");

                // ⚠️ **두 표가 어긋나면 여기서 걸린다** — 무기 줄 자체는 WEAPON_DATA 가 든다.
                Assert.AreEqual(d.weapons.Count, r.Int("WeaponCount"), id + " 무기 줄 수");
                seen++;
            }
            Assert.AreEqual(3, seen, "로봇은 셋이다");
        }

        [Test]
        public void 스테이지가_자산과_같다()
        {
            CsvTable t = GameDataTables.Load("STAGE_DATA");
            int seen = 0;
            foreach (CsvTable.Row r in t.Rows)
            {
                string id = r.Text("StageID");
                var d = Load<StageDefinition>($"{SoRoot}/Stages/Stage_{id}.asset");

                Assert.AreEqual(d.topic, r.Text("TID_Topic"), id + " 주제");
                Assert.AreEqual(d.req, r.Num("Req"), D, id + " 요구치");
                seen++;
            }
            Assert.AreEqual(6, seen, "스테이지는 여섯이다");
        }

        [Test]
        public void 밸런스_칸이_밸런스_자산과_같다()
        {
            // ⚠️ json params 마흔일곱 중 **자산으로 구워지는 것만** 견줄 수 있다 —
            //    나머지는 아직 코드·문서가 쓰는 값이라 대조할 상대가 없다.
            //    그래서 여기서는 **구워지는 몇 개**를 짚어 표가 안 흔들렸음을 본다.
            CsvTable t = GameDataTables.Load("BALANCE_PARAM_DATA");
            var c = Load<BalanceConfig>($"{SoRoot}/BalanceConfig.asset");

            var byKey = new Dictionary<string, CsvTable.Row>();
            foreach (CsvTable.Row r in t.Rows) byKey[r.Text("ParamKey")] = r;

            Assert.AreEqual(47, t.Rows.Count, "params 는 마흔일곱이다");
            Assert.IsTrue(byKey.ContainsKey("origin"), "origin 칸이 없다");
            Assert.AreEqual(c.origin, byKey["origin"].Num("Value"), D, "원점 출력");
            Assert.AreEqual(1, byKey["origin"].Int("Confirmed"), "origin 은 확정이다");
        }
    }
}
