namespace MBI.Core
{
    /// <summary>
    /// 물류 출력 ↔ 전투력 공유 지점(§5-6). 물류측(LogisticsOutputProvider·MockLogisticsSource)이 쓰고,
    /// 전투측(StageRunner)과 UI(변수패널)가 읽는다. Combat↔Logistics 직접 참조를 피하는 중립 채널.
    ///
    /// 게시 단위는 **물류 단위 = 마운트계수 미적용**이다(마운트계수는 판정식 내부 항 = 전투 측).
    /// 전투력이 필요한 쪽이 마운트계수·모듈배율을 곱한다.
    ///
    /// 게시값은 전부 **같은 롤링 창을 통과한 값**이다(RollingWindow 다채널). 그래서
    /// gapPower+gapHeat+gapBelt == expected−actual 이 롤링 후에도 성립한다 —
    /// 예전처럼 '즉시 expected − 롤링 actual'을 섞으면 변수패널의 분해 합이 총갭과 어긋난다.
    ///
    /// 기본값은 default(전부 0). 145 같은 상수를 여기 두지 않는다 — 출력의 원천은
    /// RobotDefinition.weapons(<see cref="RobotOutput.Nominal"/>) 하나뿐이다(§3 수치 하드코딩 금지).
    /// </summary>
    public static class LogisticsOutputBridge
    {
        /// <summary>롤링 적용된 물류 산출 결과 일체(expected/actual/gap + 분해 3항 + 배율).</summary>
        public static LogisticsResult Result;

        /// <summary>라이브 네트워크 군수 생산율(발/초). 전투 HUD의 저장고 표시용(라이브 없으면 0).</summary>
        public static float AmmoProduce;

        /// <summary>라이브 드론 몸체 산출(기/초). 로봇 B가 사출대에 넣는 유입이다.</summary>
        public static float DroneProduce;

        /// <summary>라이브 추진제 산출(개/초). 부스터가 받아 회피 스택으로 바꾼다.</summary>
        public static float PropellantProduce;

        /// <summary>
        /// 보드에 놓인 부스터 대수. **회피 스택 상한 = 이 값 × 2**(260829_V02).
        /// 상한이 상수가 아니라 대수의 파생값이라, 이 채널이 없으면 보드가 생존을 못 바꾼다.
        /// </summary>
        public static int BoosterCount;

        /// <summary>전역 병목 원인(변수 패널 아이콘·점멸용). Power → Heat 우선(§3-4-1). None = 정상.</summary>
        public static ConstraintCause GlobalCause;

        // ── 전력 사용률의 재료 (2026-09-09 · UI 문서 3-3) ──────────────────────────────
        //
        // ⚠️ **화면에 뿌리는 축은 사용률(수요÷공급)이고 전력 효율이 아니다**(2026-09-06 사용자 확정).
        // 효율은 min(1, 공급÷수요)라 **모자라기 전까지 꽉 찬 채 움직이지 않아**
        // 「한 대 더 놓을 수 있나」에 답하지 못한다. 사용률은 상한이 없어 100% 초과가 실제로 뜬다.
        //
        // ⚠️ **비율을 여기서 만들지 않는다.** 0으로 나뉘는 경우가 셋이고(UI 문서 3-3)
        // 그때 화면이 숫자를 `—`로 적어야 하는데, 미리 나눠 두면 **없는 값과 0이 구분되지 않는다.**
        // 그래서 **분자와 분모를 그대로** 넘기고 나누는 일은 그리는 쪽이 한다.
        /// <summary>Σ 발전(에너지 노드). 사용률의 분모.</summary>
        public static float PowerSupply;

        /// <summary>Σ 변동비(대당 전력 × 일감률). 사용률의 분자 — 노는 노드는 0을 먹는다.</summary>
        public static float PowerDraw;

        /// <summary>
        /// 노드별 일감률(260831_V07 승인분). 보드가 **어느 노드가 놀고 있는지**를 여기서 읽는다 —
        /// 초과분을 노는 것으로 몰아 두었으므로 화면에서 뺄 노드가 그대로 지목된다.
        /// </summary>
        public static WorkloadRate.Result Workload;

        /// <summary>실제 출력(전투력 산출의 입력) = 병목 반영된 실측치.</summary>
        public static float Output => Result.actual;

        /// <summary>명목 출력 = 병목 미적용. HUD 이중표시의 '예상'.</summary>
        public static float Expected => Result.expected;

        /// <summary>총 손실 = expected − actual. 분해 3항의 합과 정확히 같다.</summary>
        public static float Gap => Result.gap;

        /// <summary>
        /// static 상태 초기화. 에디터에서 도메인 리로드를 끄면 Play 종료 후에도 값이 남아
        /// 다음 Play의 첫 프레임이 이전 세션 값을 읽는다 — 씬 진입 시 호출한다.
        /// </summary>
        public static void Reset()
        {
            Workload = default;
            Result = default;
            AmmoProduce = 0f;
            DroneProduce = 0f;
            PropellantProduce = 0f;
            BoosterCount = 0;
            GlobalCause = ConstraintCause.None;
            PowerSupply = 0f;
            PowerDraw = 0f;
        }
    }
}
