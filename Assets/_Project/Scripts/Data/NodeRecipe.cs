using System;
using System.Collections.Generic;
using UnityEngine;

namespace MBI.Data
{
    /// <summary>
    /// 노드가 돌릴 수 있는 조합표의 종류. 군수 노드 = { 탄약, 드론 몸체, 쉴드 재료, 추진제 }.
    ///
    /// **노드 한 대가 만드는 것은 이 중 하나뿐이다**(260827_V01 §3). 세 갈래를 동시에 뽑지 않는다 —
    /// 갈래를 늘리는 방법은 노드를 더 놓는 것이지 노드 하나를 넓히는 것이 아니다.
    /// </summary>
    public enum RecipeKind
    {
        None = 0,
        /// <summary>탄약 — 탄종은 노드 인스턴스가 따로 지정한다(관통·분열·폭발).</summary>
        Ammo,
        /// <summary>드론 몸체 → 로봇 B 사출. 2026-08-27 범위.</summary>
        DroneBody,
        /// <summary>쉴드 재료 → 쉴드 발생 노드. 범위 밖.</summary>
        ShieldMaterial,
        /// <summary>추진제 → 부스터 노드(회피). 기초 군수 소관 (2026-09-04 개정).</summary>
        Propellant,

        // ─── 레시피 전면 개정 (2026-09-04 · `260904_W01` 3장) ───
        //
        // 종전에는 조합표가 다섯이었고 **가공 노드의 것이 하나도 없었다.** 밸런스 문서가
        // 「기초재료·부품」을 확정치로 갖고 있는데 코드에 레시피가 0개였던 자리다.
        //
        // ⚠️ 맨 뒤에 붙인다 — 위 다섯의 정수 값이 자산에 박혀 있다.

        /// <summary>코어 에너지 — 코어의 산출. 먹는 것이 없다.</summary>
        CoreEnergy,

        /// <summary>기초재료·부품 ← 코어 에너지 (가공).</summary>
        BasicParts,

        /// <summary>발전재료 ← 코어 에너지 (가공).</summary>
        PowerMaterial,

        /// <summary>배터리 ← **발전재료** (가공). 코어 에너지가 아니다 — 그래서 다툼이 생긴다.</summary>
        Battery,

        /// <summary>표준탄 ← 기초재료·부품 (기초 군수). 구 분열탄 자리이며 특수탄의 재료다.</summary>
        StandardAmmo,

        /// <summary>방어 재료 ← 기초재료·부품 (기초 군수).</summary>
        DefenseMaterial,

        /// <summary>관통탄 ← 표준탄 + 기초재료·부품 (복합 군수).</summary>
        PierceAmmo,

        /// <summary>폭발탄 ← 표준탄 + 발전재료 (복합 군수).</summary>
        ExplosiveAmmo,

        /// <summary>누적형 드론 ← 배터리 + 드론 몸체 부품 (복합 군수).</summary>
        StackDrone,

        /// <summary>광역형 드론 ← 배터리 + 드론 몸체 부품 (복합 군수).</summary>
        AoeDrone,
    }

    /// <summary>
    /// 조합표가 산출 1개를 내기 위해 먹는 재료 한 줄 (2026-09-04 신설 · `260904_W01` 3장).
    ///
    /// **품목과 양만 적고 어느 면으로 들어오는지는 적지 않는다.** 입력면은 재료 종류를 가리지
    /// 않으며 노드가 안에서 맞춘다 — 조립 시스템 문서「연결 규칙」이 연결을 면과 방향의 물리로만
    /// 판정하는 것과 같은 결이다.
    /// </summary>
    [Serializable]
    public struct RecipeInput
    {
        [Tooltip("먹는 품목.")]
        public FlowKind kind;

        [Tooltip("산출 1개당 먹는 개수.")]
        public float perOutput;
    }

    /// <summary>
    /// 조합표 한 행. **노드 코드를 건드리지 않고 데이터만 늘려 레시피를 추가**할 수 있어야 하므로
    /// 입력·출력·주기를 전부 여기에 둔다(260827_V01 §3-1-2).
    ///
    /// ⚠️ **입력이 2026-09-04까지 이 구조체에 없었다.** 위 문장이 처음부터 있었는데 필드가
    /// 없었고, 그래서 군수 노드가 아무것도 안 먹고 돌았다(`260904_V01` 2-1). 문서가 있다고
    /// 말해 온 것이 코드에 없던 자리이며, 입력이 없으니 검사도 실패도 없어 아무 신호가 없었다.
    ///
    /// 레퍼런스: 새티스팩토리 제작기 · 쉐이퍼즈 2 가공 플랫폼 — 기계가 조합표를 여러 개 갖되
    /// 한 번에 하나만 돌린다.
    /// </summary>
    [Serializable]
    public struct NodeRecipe
    {
        [Tooltip("조합표 종류.")]
        public RecipeKind kind;
        [Tooltip("표시명.")]
        public string displayName;

        [Tooltip("먹는 재료. 비어 있으면 아무것도 안 먹는다(코어처럼 원천에서 나는 것).")]
        public List<RecipeInput> inputs;

        [Tooltip("산출 흐름 종류 — 포트 연결 판정에 쓴다.")]
        public FlowKind output;
        [Tooltip("산출 속도(개/초). 군수 탄약은 muniPerNode = 1 확정치.")]
        public float outputPerSec;

        [Tooltip("출력 버퍼 상한(개) = 그 품목의 최대 스택. ⚠️ 미확정 — 조립 「품목과 재고」 장 신설 중, 수치는 검증 대장.")]
        public float stackLimitTbd;

        [Tooltip("false = 조합표 자리만 있고 이번 범위가 아니다(쉴드 재료·추진제).")]
        public bool implemented;

        /// <summary>돌릴 수 있는 조합표인가.</summary>
        public bool IsRunnable => implemented && kind != RecipeKind.None && outputPerSec > 0f;
    }
}
