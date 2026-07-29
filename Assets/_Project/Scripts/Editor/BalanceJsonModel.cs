using System;
using UnityEngine;

namespace MBI.Editor
{
    /// <summary>
    /// balance_v4.json(밸런스 1차 계약·schemaVersion 4.0)의 JsonUtility 미러 타입.
    /// 주의: JSON 최상위 키 "params"는 C# 예약어라 매핑 불가 → 로더가 "paramList"로 치환 후 파싱한다.
    /// 이 파일은 export 원천을 읽기 위한 것일 뿐, 수치 원천 자체가 아니다(§9 흐름 유지).
    /// JsonUtility는 미지 필드를 무시하고 누락 필드는 기본값으로 둔다 — v4의 부가 필드는 필요분만 미러.
    /// </summary>
    [Serializable]
    public sealed class BalanceJson
    {
        public MetaBlock meta;
        public ParamEntry[] paramList; // JSON의 "params" (치환됨)
        public EnemyEntry[] enemies;   // v4: 몬스터 카탈로그(atk만; hp/def는 스테이지 composition)
        public StageEntry[] stages;
        public EnhanceBlock enhance;

        /// <summary>params 배열에서 key로 value 조회(없으면 예외 — 앵커 누락을 조기 노출).</summary>
        public float Param(string key)
        {
            if (paramList != null)
            {
                foreach (ParamEntry p in paramList)
                    if (p != null && p.key == key) return p.value;
            }
            throw new Exception($"[MBI] balance.json params에 '{key}' 없음 — 스키마 드리프트 의심(§7).");
        }

        /// <summary>stages 배열에서 id로 스테이지 조회.</summary>
        public StageEntry Stage(string id)
        {
            if (stages != null)
            {
                foreach (StageEntry s in stages)
                    if (s != null && s.id == id) return s;
            }
            throw new Exception($"[MBI] balance.json stages에 '{id}' 없음 — 스키마 드리프트 의심(§7).");
        }

        /// <summary>enemies 배열에서 key로 몬스터 조회(없으면 예외 — 카탈로그 드리프트 조기 노출).</summary>
        public EnemyEntry Enemy(string key)
        {
            if (enemies != null)
            {
                foreach (EnemyEntry e in enemies)
                    if (e != null && e.key == key) return e;
            }
            throw new Exception($"[MBI] balance.json enemies에 '{key}' 없음 — 스키마 드리프트 의심(§7).");
        }
    }

    [Serializable] public sealed class MetaBlock
    {
        public string schemaVersion;
        public string exportedAt;
    }

    [Serializable] public sealed class ParamEntry
    {
        public string key;
        public float value;
        public bool confirmed;
    }

    [Serializable] public sealed class EnemyEntry
    {
        public string key;
        public string label;
        public string role;
        public float atk;          // v4 confirmed:false — 미확정 표기 대상
        public bool confirmed;
    }

    [Serializable] public sealed class StageEntry
    {
        public string id;
        public string topic;
        public string reqType;     // "fixed" | "band" | "formula" | "budget"
        public float req;          // fixed 스테이지만 유효
        public float[] reqBand;    // band 스테이지만 유효
        public float challengeTime;
        public string powerModel;  // logistics | enhanced | tag | burst
        public float enhMaterialReward;
        public float bossHp;       // S6만 유효(그 외 0)
        public CompEntry[] composition; // 스테이지별 몬스터 인스턴스(enemy·count·hp·def)
    }

    [Serializable] public sealed class CompEntry
    {
        public string enemy;       // EnemyEntry.key 참조
        public int count;
        public float hp;
        public float def;
    }

    [Serializable] public sealed class EnhanceBlock
    {
        public float s4Cost;
        public float s3Break;
        public float enhPoint;     // v4: 강화 확정점 1.45(= params.enh)
        public float[] s4Band;
        public float[] enhBand;
        public float snapBand;
    }
}
