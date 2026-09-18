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

        /// <summary>표를 다시 읽게 한다 — 변환기를 돌린 뒤 유니티가 파일을 다시 보게 한다.</summary>
        [MenuItem("MBI/밸런스 표 다시 읽기")]
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
