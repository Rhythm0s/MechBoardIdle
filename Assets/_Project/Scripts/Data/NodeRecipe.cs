using System;
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
        /// <summary>추진제 → 부스터 노드(회피). 착수 금지 — 노드 6종→7종 개편이 선행.</summary>
        Propellant,
    }

    /// <summary>
    /// 조합표 한 행. **노드 코드를 건드리지 않고 데이터만 늘려 레시피를 추가**할 수 있어야 하므로
    /// 입력·출력·주기를 전부 여기에 둔다(260827_V01 §3-1-2).
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
