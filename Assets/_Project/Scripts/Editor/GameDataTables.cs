using System.Collections.Generic;
using System.IO;
using MBI.Core;
using MBI.Data;
using UnityEditor;
using UnityEngine;

namespace MBI.Editor
{
    /// <summary>
    /// **밸런스 표(CSV)를 읽어 주는 한 자리** (2026-09-18 사용자 확정 · 플랜 §85-12).
    ///
    /// 흐름 — 사람이 고치는 것은 `Tables/*.xlsx`, 변환기가 뜨는 것은
    /// `Assets/_Project/GameData/*.csv`, 그것을 읽어 SO 로 굽는 것이 생성기다.
    /// 이 파일은 **생성기가 표를 여는 문**이다.
    ///
    /// ⚠️⚠️ **없으면 멈춘다.** 표가 없다고 json 으로 되돌아가면 **어느 쪽이 값을 냈는지**를
    /// 아무도 모르게 된다 — 그것이 이 옮김이 없애려던 상태다. 없으면 **경로를 들고 죽는다.**
    ///
    /// ⚠️ **읽기 전용이다** — 여기서 값을 고치거나 채우지 않는다.
    /// </summary>
    public static class GameDataTables
    {
        public const string Dir = "Assets/_Project/GameData";

        /// <summary>표 하나를 연다. 못 열면 **경로와 까닭을 들고** 죽는다.</summary>
        public static CsvTable Load(string name)
        {
            string path = $"{Dir}/{name}.csv";
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"밸런스 표가 없다 — {path}\n"
                    + "  Tables/ 의 xlsx 를 고친 뒤 `python Tables/xlsx_to_csv.py` 를 돌렸는가", path);

            return CsvTable.Parse(name, File.ReadAllText(path, System.Text.Encoding.UTF8));
        }

        /// <summary>
        /// 표의 `EnemyKey`(수치) → 자산 키 글자.
        ///
        /// ⚠️ **사전이 표와 코드 두 곳에 있다.** 표는 `Designer_Data` 시트가 들고 코드는 여기다 —
        /// 값이 갈리면 **다른 적을 굽는다.** 그래서 `EnemyKeyId` 가 모르는 수를 받으면 죽는다.
        /// (열거형을 코드에 들이는 대신 글자로 두는 까닭은 자산 파일 이름이 글자이기 때문이다.)
        /// </summary>
        public static string EnemyKeyOf(int id)
        {
            switch (id)
            {
                case 1: return "infantry";
                case 2: return "artillery";
                case 3: return "armor";
                case 4: return "boss";
                default:
                    throw new System.FormatException(
                        $"모르는 EnemyKey — {id} (표의 Designer_Data 와 어긋난다)");
            }
        }

        /// <summary>표의 `AmmoKind`(수치) → 코드 열거형. 드론 둘(4·5)은 탄종이 아니라 `null`.</summary>
        public static AmmoKind? AmmoKindOf(int id)
        {
            switch (id)
            {
                case 1: return AmmoKind.Pierce;
                case 2: return AmmoKind.Standard;
                case 3: return AmmoKind.Explosive;
                case 4:
                case 5: return null;   // 드론 — 탄종 줄이 아니다
                default:
                    throw new System.FormatException(
                        $"모르는 AmmoKind — {id} (표의 Designer_Data 와 어긋난다)");
            }
        }

        /// <summary>
        /// 스테이지별 구성 줄들 — `StageID` 로 묶어 준다.
        /// ⚠️ **표의 차례를 지킨다** — 차례가 바뀌면 같은 판이 다른 순서로 스폰된다.
        /// </summary>
        public static Dictionary<string, List<CsvTable.Row>> CompositionByStage(CsvTable t)
        {
            t.Require("StageID", "EnemyKey", "Count", "Hp", "Def");

            var map = new Dictionary<string, List<CsvTable.Row>>();
            foreach (CsvTable.Row r in t.Rows)
            {
                string sid = r.Text("StageID");
                if (sid.Length == 0) continue;
                if (!map.TryGetValue(sid, out List<CsvTable.Row> list))
                    map[sid] = list = new List<CsvTable.Row>();
                list.Add(r);
            }
            return map;
        }

        /// <summary>
        /// `WEAPON_DATA` 에서 **밸런스 자산이 쓰는 값들**을 꺼낸 것 (2026-09-18).
        ///
        /// ⚠️ **두 생성기가 같은 문을 지난다** — 전투 생성기는 무기·조율을, 밸런스 생성기는
        /// 라인 스펙·광역 배수를 가져간다. 각자 표를 따로 파면 **읽는 법이 둘**이 되고
        /// 한쪽만 고쳐지는 날이 온다.
        /// </summary>
        public struct WeaponTableValues
        {
            /// <summary>라인 스펙 셋 — **관통 · 표준 · 폭발** 차례(BalanceConfig.LineSpecOf 와 같다).</summary>
            public float[] lineSpec;

            /// <summary>
            /// 광역형 드론의 표적당 피해 비 — **파생값이다.**
            ///
            /// ✅ **좌표에서 나온다**(2026-09-19 · `260918_W02` 5장 문안).
            /// 문서(무기 스펙트럼)가 가진 것은 **광역형 기당 피해 50** 이라는 좌표이고,
            /// 배수는 그것을 누적형 100 으로 나눈 몫이다. 종전에는 **같은 값이 두 자리**에
            /// 살았다 — 문서에 50, 코드에 0.5.
            ///
            /// ⚠️ **여기서 0.5 를 적지 않는다.** 적는 순간 다시 두 자리가 된다.
            /// </summary>
            public float aoeDamageFactor;

            /// <summary>
            /// 광역형 **기당 피해 좌표**(문서의 50). 배수는 이것에서 나온다.
            ///
            /// ⚠️ **충전량이 아니다.** 충전량(= 수명)은 두 종이 같은 100 이고 표의 `Charge`
            /// 열이 든다 — 이 둘을 한 수로 두었다가 **광역형 수명이 두 배가 된** 적이 있다
            /// (`260918_W02` 3장 · `260918_V03` 9장).
            /// </summary>
            public float aoeChargeCoord;
        }

        /// <summary>
        /// 무기 표를 읽어 위 값들을 낸다.
        ///
        /// ⚠️ **탄종으로 골라 담는다** — 줄 차례가 바뀌어도 안 흔들린다.
        /// ⚠️ **없으면 죽는다** — 라인 스펙이 한 칸이라도 비면 그 탄종이 안 쏘게 된다.
        /// </summary>
        public static WeaponTableValues ReadWeapons()
        {
            CsvTable t = Load("WEAPON_DATA");
            // 🗑️ `AoeDamageFactor` 열은 **안 읽는다**(2026-09-19 이관) — 좌표에서 파생시킨다.
            //    열은 표에 남겨 둔다(폐기는 삭제가 아니다 · 사람이 견줄 수 있어야 한다).
            t.Require("RobotID", "AmmoKind", "LineSpec", "DamagePerUnit");

            var v = new WeaponTableValues { lineSpec = new float[3] };
            var got = new bool[3];
            bool gotAoe = false;
            float baseCharge = 0f;   // 누적형 기당 피해 — 배수의 분모다

            foreach (CsvTable.Row r in t.Rows)
            {
                AmmoKind? kind = AmmoKindOf(r.Int("AmmoKind"));
                if (r.Int("RobotID") == 1 && kind != null)
                {
                    int i = (int)kind.Value;
                    if (i < 0 || i >= 3)
                        throw new System.FormatException($"[WEAPON_DATA] 모르는 탄종 자리 — {kind}");
                    v.lineSpec[i] = r.Num("LineSpec");
                    got[i] = true;
                }

                // 드론 두 줄의 **기당 피해 좌표**를 줍는다 — 누적형 100 · 광역형 50.
                //    배수는 아래에서 **나눠서** 낸다(⚠️ 여기서 0.5 를 적으면 이관이 무효다).
                // ⚠️ **`Charge`(충전량)가 아니라 `DamagePerUnit`(기당 피해 좌표)다** —
                //    둘은 다른 축이고, 광역형은 좌표만 절반이고 충전량은 100 그대로다
                //    (`260918_W02` 3장 · 09-18 에 둘을 한 수로 두어 수명이 두 배가 됐다).
                if (r.Int("RobotID") == 2 && r.Int("AmmoKind") == 4)
                    baseCharge = r.Num("DamagePerUnit");
                if (r.Int("RobotID") == 2 && r.Int("AmmoKind") == 5)
                {
                    v.aoeChargeCoord = r.Num("DamagePerUnit");
                    gotAoe = true;
                }
            }

            for (int i = 0; i < 3; i++)
                if (!got[i])
                    throw new System.FormatException(
                        $"[WEAPON_DATA] 로봇A 의 {(AmmoKind)i} 줄이 없다 — 그 탄종이 안 쏘게 된다");
            if (!gotAoe)
                throw new System.FormatException("[WEAPON_DATA] 광역형 드론 줄(AmmoKind 5)이 없다");
            if (baseCharge <= 0f)
                throw new System.FormatException(
                    "[WEAPON_DATA] 누적형 드론 줄(AmmoKind 4)의 DamagePerUnit 이 없다 — 배수의 분모다");

            // ⚠️⚠️ **여기가 이관의 전부다.** 배수는 좌표 둘의 몫이고, 표에는 좌표만 산다.
            //    50 ÷ 100 = 0.5 — 이관 전 값과 같다(거동 불변 시험이 그것을 지킨다).
            v.aoeDamageFactor = v.aoeChargeCoord / baseCharge;

            return v;
        }

        /// <summary>표를 다시 읽게 한다 — 변환기를 돌린 뒤 유니티가 파일을 다시 보게 한다.</summary>
        [MenuItem("MBI/밸런스 표 다시 읽기")]
        /// <summary>
        /// **FieldName / Value 꼴 표를 자산에 그대로 붓는다** (2026-10-13 사용자 확정 · 미확정치 표 셋).
        ///
        /// ⚠️⚠️ **구 규칙 「미확정치는 안 덮어쓴다」가 폐기된 자리다.** 조정하는 자리가
        /// 인스펙터에서 **표로** 옮겨 갔으므로, 생성기가 덮는 것이 맞다(#62 CSV 가 원천).
        ///
        /// 📌 **형은 자산이 정한다.** `SerializedProperty` 의 종류를 보고 넣으므로
        /// 표에 수를 적어 두면 float 든 int 든 bool 이든 그 칸이 알아서 받는다 —
        /// 표에 형을 적지 않는다(적으면 형이 두 곳에 산다 · 지침 §7).
        ///
        /// ⚠️ **모르는 이름은 경고 한 줄로 넘긴다.** 표에 남아 있는 폐기된 줄
        /// (예: `COMBAT_RULE_DATA` 의 `robotHp` — 임자는 `BALANCE_PARAM_DATA` 다)이
        /// 생성을 멈추면 안 된다. 멈추면 표 한 줄 때문에 자산 전부가 안 선다.
        /// </summary>
        /// <param name="ignore">표에 있어도 **일부러 안 넣는** 이름(폐기된 줄).</param>
        /// <returns>실제로 넣은 칸 수.</returns>
        public static int ApplyFieldTable(Object target, string tableName,
            params string[] ignore)
        {
            if (target == null) return 0;

            CsvTable table = Load(tableName);
            var so = new SerializedObject(target);
            var skip = new HashSet<string>(ignore ?? System.Array.Empty<string>());

            int applied = 0, unknown = 0, skipped = 0;
            foreach (CsvTable.Row row in table.Rows)
            {
                string name = row.Text("FieldName");
                if (string.IsNullOrEmpty(name)) continue;

                if (skip.Contains(name)) { skipped++; continue; }

                SerializedProperty p = so.FindProperty(name);
                if (p == null) { unknown++; continue; }

                string raw = row.Text("Value");
                if (!Assign(p, raw)) { unknown++; continue; }
                applied++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);

            Debug.Log($"[MBI] {tableName} → {target.name}: 넣음 {applied}"
                      + (skipped > 0 ? $" · 폐기 줄 건너뜀 {skipped}" : "")
                      + (unknown > 0 ? $" · ⚠️ 모르는 칸 {unknown}" : ""));
            return applied;
        }

        /// <summary>표의 글자를 그 칸의 형으로 넣는다. 못 넣으면 거짓.</summary>
        private static bool Assign(SerializedProperty p, string raw)
        {
            raw = raw == null ? "" : raw.Trim();
            switch (p.propertyType)
            {
                case SerializedPropertyType.Float:
                    if (!float.TryParse(raw, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float f)) return false;
                    p.floatValue = f;
                    return true;

                case SerializedPropertyType.Integer:
                    if (!int.TryParse(raw, System.Globalization.NumberStyles.Integer,
                            System.Globalization.CultureInfo.InvariantCulture, out int i)) return false;
                    p.intValue = i;
                    return true;

                case SerializedPropertyType.Boolean:
                    // 표는 0/1 로 적는다 — true/false 글자도 받는다.
                    if (raw == "0") { p.boolValue = false; return true; }
                    if (raw == "1") { p.boolValue = true; return true; }
                    if (bool.TryParse(raw, out bool b)) { p.boolValue = b; return true; }
                    return false;

                case SerializedPropertyType.String:
                    p.stringValue = raw;
                    return true;

                default:
                    return false;   // 벡터·참조 따위는 이 꼴의 표가 담을 것이 아니다
            }
        }

        public static void Refresh()
        {
            AssetDatabase.Refresh();
            foreach (string n in new[] { "STAGE_COMP_DATA", "ENEMY_DATA", "WEAPON_DATA" })
            {
                CsvTable t = Load(n);
                Debug.Log($"[MBI] 표 {n} — 열 {t.Fields.Count} · 줄 {t.Rows.Count}");
            }
        }
    }
}
