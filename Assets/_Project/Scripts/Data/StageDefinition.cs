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

        [Header("파밍 층 구성 (2026-10-13 사용자 확정 #111·#112)")]
        /// <summary>
        /// **상주 파밍 층에 서는 것들** — 원천은 `FARM_COMP_DATA` 표다.
        ///
        /// ⚠️⚠️ **도전 판(<see cref="composition"/>)과 다른 줄이다.** 도전은 「이 판을 깨려면
        /// 무엇을 몇 마리 잡아야 하는가」이고, 파밍은 「화면에 늘 몇 마리가 서 있는가」다 —
        /// 같은 종류라도 수도 체력도 다를 수 있다(S5·S6 이 실제로 그렇다).
        /// 한 줄을 둘로 쓰면 한쪽을 고칠 때 다른 쪽이 조용히 따라 움직인다.
        ///
        /// 📌 **`count` 는 마릿수다**(표의 `Weight`). 그 합이 곧 정원이며
        /// <see cref="MBI.Core.FarmRefillRule.CapacityOf"/> 가 그렇게 센다 —
        /// <see cref="spawnCap"/> 과 **따로 세지 않는다**(지침 §7).
        ///
        /// ⚠️ 비면 그 판에는 파밍 층이 **안 선다**. 지어내지 않는다 — 표가 비었다는 뜻이다.
        /// </summary>
        [Tooltip("파밍 층에 상주하는 구성. count = 마릿수이고 그 합이 정원이다. 원천 = FARM_COMP_DATA.")]
        public List<StageComposition> farmComposition = new List<StageComposition>();
    }
}
