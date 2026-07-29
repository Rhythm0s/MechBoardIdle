// =============================================================================
//  MECH BOARD IDLE — balance.json Exporter (스텁)
//  단일 원천(SO) → balance.json (읽기 전용 시뮬 계약). 시뮬은 되쓰기 없음(드리프트 0).
//  스키마 = balance.json schemaVersion "4.0" 와 1:1 (2026-07-23 합체/버스트 분리 반영).
//  합체/버스트 파라미터(mergeMult·gaugeFull·bc·bd)는 ParamDef 리스트로 운반 — 별도 클래스 불필요.
//
//  사용:
//   1) Assets/_Project/ScriptableObjects/Balance/ 에 Create > MBI > Balance Config 로 자산 생성
//   2) 인스펙터에서 값 입력(= 진실 원천)
//   3) 자산 우클릭 컨텍스트 메뉴 [Export balance.json] 또는 인스펙터 톱니 메뉴
//   4) Assets/_Project/_Export/balance.json 생성 → 시뮬(HTML)에 「balance.json 로드」
//
//  주의: JsonUtility는 Dictionary를 직렬화하지 못하므로 composition은 배열형({enemy,count})으로 둔다.
//        중첩 배열/객체는 [Serializable] 클래스로 표현하면 JsonUtility가 처리한다.
//        더 유연한 출력이 필요하면 Newtonsoft(com.unity.nuget.newtonsoft-json)로 교체(하단 주석).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MBI.Balance
{
    // ---------- 스키마 (balance.json 과 1:1) ----------
    [Serializable] public class Meta {
        public string schemaVersion = "4.0";
        public string project = "MECH BOARD IDLE";
        public string role = "Tier-1 밸런스/경제 페이싱 검증 계약";
        public string source = "Unity ScriptableObject export (읽기 전용)";
        public string exportedAt = "";      // Export 시 스탬프
        public string note = "확정치는 balance_v4.json 참조해 SO에 손 입력 (confirmed 21/29 — 2026-07-23). 잔여 TBD: 병목·경제·쉴드·공격력.";
    }

    [Serializable] public class ParamDef {
        public string key;
        public string label;
        public string group;
        public float value;
        public float min;
        public float max;
        public float step;
        public string basis;   // 근거
        public string intent;  // 의도
        public string docRef;  // 원천 링크
    }

    [Serializable] public class EnemyDef {
        public string key;
        public string label;
        public string role;    // 전투 거동(설명용)
        public float atk;      // 공격력(쉴드/HP 압박) — 07 6장. hp/def는 스테이지 곡선이므로 CompItem에
    }

    [Serializable] public class CompItem {
        public string enemy;   // EnemyDef.key
        public int count;
        public float hp;       // 스테이지별 몬스터 체력 (07 6장 곡선)
        public float def;      // 스테이지별 방어력 (히트당 뺄셈, 07 1장 판정식)
    }

    [Serializable] public class StageDef {
        public string id;
        public string topic;
        public float time;
        public string powerModel;   // logistics | enhanced | tag | burst
        public float enhMaterialReward;
        public List<CompItem> composition = new List<CompItem>();
    }

    [Serializable] public class Check7 {
        public string mode = "exact";
        public float tolerance = 1f;     // A안: 잉여=0, ±tolerance 반올림 오차만 허용
        public string target = "Sum(S1..S3 enhMaterialReward) == s4Cost";
        public string rationale = "A안: 기본 잉여=0, +10% 마진은 물류에서만";
    }

    [Serializable] public class EnhanceDef {
        public float s4Cost = 100f;
        public float s3Break = 145f;      // 실측 확정 (2026-07-17)
        public float enhPoint = 1.45f;    // S4 강화배율 점값 확정 (2026-07-17)
        public float[] enhBand = { 1.3f, 1.5f };
        public float[] s4Band = { 186f, 215f };
        public Check7 check7 = new Check7();
    }

    [Serializable] public class EconomyDef {
        public string enhMaterialSource = "clearRewardOnly";
        public string scrapSource = "farming + challenge + offline";
        public bool twoCurrencySeparation = true;
    }

    // JsonUtility 직렬화 루트
    [Serializable] public class BalancePayload {
        public Meta meta = new Meta();
        public List<ParamDef> @params = new List<ParamDef>();
        public List<EnemyDef> enemies = new List<EnemyDef>();
        public List<StageDef> stages = new List<StageDef>();
        public EconomyDef economy = new EconomyDef();
        public EnhanceDef enhance = new EnhanceDef();
    }

    // ---------- ScriptableObject (진실 원천) ----------
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "MBI/Balance Config", order = 0)]
    public class MbiBalanceExporter : ScriptableObject
    {
        [Header("진실 원천 — 이 값이 balance.json 이 된다")]
        public List<ParamDef> parameters = new List<ParamDef>();
        public List<EnemyDef> enemies = new List<EnemyDef>();
        public List<StageDef> stages = new List<StageDef>();
        public EconomyDef economy = new EconomyDef();
        public EnhanceDef enhance = new EnhanceDef();

        [Header("Export 경로 (Assets 기준 상대경로)")]
        public string exportPath = "Assets/_Project/_Export/balance.json";

        public BalancePayload BuildPayload()
        {
            var p = new BalancePayload();
            p.meta.exportedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            p.@params = parameters;
            p.enemies = enemies;
            p.stages  = stages;
            p.economy = economy;
            p.enhance = enhance;
            return p;
        }

        // JsonUtility 는 "params" 를 필드명으로 못 쓰므로(@params 로 회피) 직렬화 후 키를 보정한다.
        public string ToJson()
        {
            string json = JsonUtility.ToJson(BuildPayload(), true);
            // @params → "params" (JsonUtility 는 필드명을 그대로 쓰므로 실제로는 "params" 로 나온다.
            //  혹시 컴파일러/버전에 따라 "@params" 로 나오면 아래 한 줄이 방어)
            json = json.Replace("\"@params\":", "\"params\":");
            return json;
        }

#if UNITY_EDITOR
        [ContextMenu("Export balance.json")]
        public void ExportJson()
        {
            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, exportPath);
            string dir = Path.GetDirectoryName(full);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(full, ToJson());
            AssetDatabase.Refresh();
            Debug.Log($"[MBI] balance.json exported → {exportPath}  (params:{parameters.Count} enemies:{enemies.Count} stages:{stages.Count})");
        }

        [ContextMenu("Validate ⑦ (A안: Σ S1~S3 == s4Cost)")]
        public void ValidateCheck7()
        {
            float sum = 0f;
            foreach (var s in stages)
                if (s.id == "S1" || s.id == "S2" || s.id == "S3") sum += s.enhMaterialReward;
            float diff = Mathf.Abs(sum - enhance.s4Cost);
            bool pass = diff <= enhance.check7.tolerance;
            Debug.Log($"[MBI] ⑦ 결정치: Σ(S1~S3)={sum} vs s4Cost={enhance.s4Cost} → 잉여 {sum - enhance.s4Cost} / {(pass ? "PASS" : "FAIL")} (A안, ±{enhance.check7.tolerance})");
        }
#endif
    }
}

// -----------------------------------------------------------------------------
// Newtonsoft 대안(선택): JsonUtility 한계(주석/누락필드/유연성)가 걸리면 아래로 교체.
//   using Newtonsoft.Json;
//   string json = JsonConvert.SerializeObject(BuildPayload(), Formatting.Indented);
// 그러면 @params 보정 불필요(직렬화 속성 [JsonProperty("params")] 로 지정 가능).
// -----------------------------------------------------------------------------
