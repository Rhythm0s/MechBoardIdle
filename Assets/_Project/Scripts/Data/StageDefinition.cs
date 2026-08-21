using System;
using System.Collections.Generic;
using UnityEngine;

namespace MBI.Data
{
    /// <summary>스테이지 요구치 유형. balance_v4 stages[].reqType.</summary>
    public enum StageReqType
    {
        Fixed,    // 고정 요구치(req) — S1~S3
        Band,     // 요구치 밴드(reqBand) — S4
        Formula,  // 공식(태그 필연) — S5
        Budget    // 예산식(보스 HP) — S6
    }

    /// <summary>스테이지 전투력 모델. balance_v4 stages[].powerModel.</summary>
    public enum StagePowerModel
    {
        Logistics, // 물류 단독(S1~S3)
        Enhanced,  // 강화-only 벽(S4) → 마운트계수 enh 적용
        Tag,       // 태그 필연(S5)
        Burst      // 버스트/보스(S6)
    }

    /// <summary>
    /// 스테이지 내 몬스터 인스턴스. balance_v4 stages[].composition[].
    /// hp·def·수는 스테이지별 확정치(§9 몬스터 곡선). enemyKey는 EnemyDefinition 참조.
    /// </summary>
    [Serializable]
    public struct StageComposition
    {
        [Tooltip("EnemyDefinition.enemyKey 참조. infantry/artillery/armor/boss.")]
        public string enemyKey;
        [Tooltip("등장 수. composition[].count.")]
        public int count;
        [Tooltip("체력. composition[].hp.")]
        public float hp;
        [Tooltip("방어력(히트당 뺄셈). composition[].def.")]
        public float def;
    }

    /// <summary>
    /// 스테이지 정의(단일 원천 SO). CLAUDE.md §5-8·09 스테이지 문서 / §9 곡선.
    ///
    /// S1~S6: 요구치(req/reqBand)·도전 제한시간(120초)·전투력 모델·강화재료 보상·몬스터 구성.
    /// 수치는 CombatAssetGenerator가 balance_v4.json에서 주입 — 코드 리터럴 금지(§3).
    /// </summary>
    [CreateAssetMenu(fileName = "Stage", menuName = "MBI/Stage Definition", order = 12)]
    public sealed class StageDefinition : ScriptableObject
    {
        [Header("정체")]
        [Tooltip("스테이지 id. S1~S6.")]
        public string stageId;
        [Tooltip("주제(09 문서). stages[].topic.")]
        public string topic;

        [Header("요구치 / 모델")]
        [Tooltip("요구치 유형.")]
        public StageReqType reqType;
        [Tooltip("고정 요구치(Fixed). S1 90 / S2 105 / S3 130. Band/Formula/Budget에선 미사용(0).")]
        public float req;
        [Tooltip("요구치 밴드 [lo,hi](Band). S4 [186,215]. 그 외 미사용.")]
        public Vector2 reqBand;
        [Tooltip("전투력 모델. Logistics/Enhanced/Tag/Burst.")]
        public StagePowerModel powerModel;

        [Header("도전 / 보상")]
        [Tooltip("도전 제한시간(초). 전 스테이지 120.")]
        public float challengeTime = 120f;
        [Tooltip("클리어 강화재료 보상(도전 한정 재화, E2). S1 30 / S2 33 / S3 37 / S4~ 0.")]
        public float enhMaterialReward;

        [Header("상주 파밍 (⚠️ TBD — 스테이지 기획서「파밍 규칙」)")]
        [Tooltip("TBD — 맵 정원 M(동시 생존 상한). N초마다 이 수까지 한 번에 보충한다. 0 = 미확정(파밍 미가동).")]
        public int spawnCap;
        [Tooltip("TBD — 스폰 간격 N(초, 15/20/30 격자). 한 바퀴 길이와 같다. 0 = 미확정.")]
        public float spawnInterval;
        [Tooltip("정원·간격이 검증 대장에서 확정됐는가. false면 위 두 값은 placeholder다.")]
        public bool spawnConfirmed;

        [Header("몬스터 구성 (§9 곡선)")]
        [Tooltip("등장 몬스터 인스턴스. hp/def/수 확정치.")]
        public List<StageComposition> composition = new List<StageComposition>();
        [Tooltip("보스 HP(Budget/S6만). 그 외 0. bossHp = 36000.")]
        public float bossHp;
    }
}
