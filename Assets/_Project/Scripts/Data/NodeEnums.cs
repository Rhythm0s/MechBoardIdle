namespace MBI.Data
{
    /// <summary>
    /// 물류 노드 **7종**(2026-08-29 부스터 신설). S1~S4 등장 5종(Core~Storage) + 부스터가 구현분이다.
    /// Shield(쉴드 발생)는 스키마 자리만 두고 구현 보류(NodeDefinition.implemented=false).
    /// CLAUDE.md §5-2 · §4(MVP 스코프 가드레일).
    /// </summary>
    public enum NodeType
    {
        Core = 0,           // 코어
        Processing = 1,     // 가공
        MunitionsBasic = 2, // 기초 군수 (2026-09-04 개명 — 구 Munitions. **정수 값을 보존한다**)
        Energy = 3,         // 에너지
        Storage = 4,        // 저장
        Shield = 5,         // 쉴드 발생 (스텁 — 구현 보류)
        Booster = 6,        // 부스터 (2026-08-29 신설) — 추진제를 받아 회피 스택을 공급하는 무형 자원 노드

        /// <summary>
        /// 복합 군수 (2026-09-04 신설 · `260904_W01` 3장).
        ///
        /// **입력면 수는 노드에 고정된다** — 레시피를 바꿔도 면이 늘거나 줄지 않는다.
        /// 그래서 군수 노드 하나가 1종 레시피와 2종 레시피를 같이 가질 수 없어 둘로 갈랐다.
        /// 기초는 입력면 1개, 복합은 2개다.
        ///
        /// ⚠️ 맨 뒤에 붙였다. 앞에 끼우면 이미 저장된 노드 자산의 종류가 통째로 밀린다.
        /// </summary>
        MunitionsComplex = 7,
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
        // ⚠️ **폐기**(2026-09-05 · W01 3-2 품목 개정). 이 자리를 `StandardAmmo`가 잇는다.
        // 열거값을 지우지 않는 이유는 직렬화된 자산·씬이 정수로 참조하기 때문이다 —
        // 지우면 뒤 항목이 한 칸씩 밀려 **다른 품목으로 조용히 바뀐다.**
        // 새 코드에서 쓰지 않는다.
        Ammo,     // 탄약 — 폐기
        Power,    // 전력
        Heat,     // 발열
        Drone,    // 드론 몸체 — 군수 노드 「드론 몸체」 레시피 산출 (2026-08-27)
        Propellant, // 추진제 — 군수 노드 산출, 부스터 노드가 받아 회피 스택으로 바꾼다 (2026-08-29)
        // ⚠️ 맨 뒤에 붙인다 — 중간에 끼우면 이미 저장된 자산의 포트 품목이 통째로 밀린다.
        None,     // 아무것도 안 흐름. 상류가 없는 벨트가 이 값이다(BeltFlow) — 「비어 있다」를 값으로 남긴다

        // ─── 레시피 전면 개정으로 실체가 생긴 품목들 (2026-09-04 · `260904_W01` 3장) ───
        //
        // 종전에는 물류 품목이 전부 Material 하나였다. 그러면 벨트가 「가공 대상」이라는
        // 한 덩어리만 나르므로 **부품 라인과 발전재료 라인이 화면에서 갈리지 않는다** —
        // W01 3-4의 「발전재료를 어디에 얼마나 보낼지가 보드 속도를 정한다」가 성립하려면
        // 둘이 서로 다른 품목이어야 한다.
        //
        // ⚠️ **위 일곱은 그대로 둔다.** 값이 자산에 정수로 박혀 있어 옮기면 전부 밀린다.
        // Material·Ammo·Drone은 개정 대상이지만 폐기는 각 참조를 옮긴 뒤에 한다.

        CoreEnergy,      // 코어 에너지 — 코어의 산출. 가공이 먹는다
        BasicParts,      // 기초재료·부품 — 가공 산출. 부품 갈래의 뿌리
        PowerMaterial,   // 발전재료 — 가공 산출. 전력·배터리·추진제·폭발탄으로 갈린다
        Battery,         // 배터리 — 가공 산출. 입력은 코어 에너지가 아니라 **발전재료**다
        StandardAmmo,    // 표준탄 — 기초 군수 산출이며 **특수탄의 재료**다 (구 분열탄 자리)
        DroneBodyParts,  // 드론 몸체 부품 — 기초 군수 산출
        DefenseMaterial, // 방어 재료 — 기초 군수 산출
        PierceAmmo,      // 관통탄 — 복합 군수. 표준탄 + 기초재료·부품
        ExplosiveAmmo,   // 폭발탄 — 복합 군수. 표준탄 + 발전재료
        StackDrone,      // 누적형 드론 — 복합 군수. 배터리 + 드론 몸체 부품
        AoeDrone,        // 광역형 드론 — 복합 군수. 배터리 + 드론 몸체 부품
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
