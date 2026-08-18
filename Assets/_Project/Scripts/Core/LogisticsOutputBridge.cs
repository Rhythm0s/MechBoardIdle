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
        /// <summary>롤링 적용된 물류 산출 결과 일체(expected/actual/gap + 분해 3항 + 배율·overCeiling).</summary>
        public static LogisticsResult Result;

        /// <summary>라이브 네트워크 군수 생산율(발/초). 전투 HUD의 저장고 표시용(라이브 없으면 0).</summary>
        public static float AmmoProduce;

        /// <summary>전역 병목 원인(변수 패널 아이콘·점멸용). Power → Heat 우선(§3-4-1). None = 정상.</summary>
        public static ConstraintCause GlobalCause;

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
            Result = default;
            AmmoProduce = 0f;
            GlobalCause = ConstraintCause.None;
        }
    }
}
