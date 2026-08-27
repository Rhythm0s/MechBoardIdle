namespace MBI.Data
{
    /// <summary>
    /// 물류 노드 6종. S1~S4 등장 5종(Core~Storage)만 이번에 구현.
    /// Shield(쉴드 발생)는 스키마 자리만 두고 구현 보류(NodeDefinition.implemented=false).
    /// CLAUDE.md §5-2 · §4(MVP 스코프 가드레일).
    /// </summary>
    public enum NodeType
    {
        Core,        // 코어
        Processing,  // 가공
        Munitions,   // 군수
        Energy,      // 에너지
        Storage,     // 저장
        Shield       // 쉴드 발생 (스텁 — 구현 보류)
    }

    /// <summary>노드의 네 면. 격자 인접 연결 판정의 기준(§5-3 그리드에서 사용).</summary>
    public enum PortFace
    {
        North,
        East,
        South,
        West
    }

    /// <summary>포트의 방향. 출력↔입력이 맞물려야 연결 성립.</summary>
    public enum PortIO
    {
        Input,
        Output
    }

    /// <summary>
    /// 포트를 흐르는 자원 종류. 같은 종류끼리만 연결된다.
    /// 노드 공통 변수(전력/탄약/발열)와 물류 품목(Material)을 아우른다(§3).
    /// </summary>
    public enum FlowKind
    {
        Material, // 물류 품목(가공 대상)
        Ammo,     // 탄약
        Power,    // 전력
        Heat,     // 발열
        Drone     // 드론 몸체 — 군수 노드 「드론 몸체」 레시피 산출 (2026-08-27)
    }

    /// <summary>
    /// 노드가 멈춘 사유(260827_V02 §2-1). 플레이어가 할 행동은 같아도 **읽히는 의미가 다르다** —
    /// 하나는 물류 실패의 신호이고 하나는 방금 자기가 한 조작의 정상적 결과다.
    /// 화면 표현은 UI 문서 소관이고, 여기서는 사유를 구분해 들고만 있는다.
    /// </summary>
    public enum NodeStallReason
    {
        /// <summary>멈추지 않았다.</summary>
        None,
        /// <summary>출력 버퍼가 가득 찼는데 가져가는 쪽이 없다 — **물류 실패**.</summary>
        OutputBlocked,
        /// <summary>이전 조합표의 산출물이 버퍼에 남아 있다 — **조작의 정상적 결과**.</summary>
        RecipeChangedResidue
    }

    /// <summary>
    /// balance.json의 confirmed 플래그 대응. Tbd = 미확정치(밴드 placeholder).
    /// §3: 미확정치는 TBD 밴드 상수로 placeholder 처리, "검증 완료" 오표기 금지(§7).
    /// </summary>
    public enum ConfirmState
    {
        Confirmed,
        Tbd
    }
}
