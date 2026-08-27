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
    /// balance.json의 confirmed 플래그 대응. Tbd = 미확정치(밴드 placeholder).
    /// §3: 미확정치는 TBD 밴드 상수로 placeholder 처리, "검증 완료" 오표기 금지(§7).
    /// </summary>
    public enum ConfirmState
    {
        Confirmed,
        Tbd
    }
}
