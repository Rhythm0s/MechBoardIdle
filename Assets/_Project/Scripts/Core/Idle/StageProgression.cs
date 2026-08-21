namespace MBI.Core
{
    /// <summary>자동 진행 판단 입력(순수 값).</summary>
    public readonly struct ProgressionInput
    {
        public readonly int currentIndex;
        public readonly int maxClearedIndex; // 아직 하나도 못 깼으면 -1
        public readonly int stageCount;
        public readonly CombatResult result;

        public ProgressionInput(int currentIndex, int maxClearedIndex, int stageCount, CombatResult result)
        {
            this.currentIndex = currentIndex;
            this.maxClearedIndex = maxClearedIndex;
            this.stageCount = stageCount;
            this.result = result;
        }
    }

    /// <summary>자동 진행 결과.</summary>
    public readonly struct ProgressionDecision
    {
        public readonly int nextIndex;
        public readonly bool isFirstClear; // 최초 클리어 = 강화재료 지급 대상
        public readonly bool advanced;

        public ProgressionDecision(int nextIndex, bool isFirstClear, bool advanced)
        {
            this.nextIndex = nextIndex;
            this.isFirstClear = isFirstClear;
            this.advanced = advanced;
        }
    }

    /// <summary>
    /// 자동 전투의 스테이지 진행 판단(§5-7). 순수 함수.
    ///
    /// **게이트는 실제 클리어 성공/실패다** — 요구치 수치로 걸지 않는다(2026-08-19 개정).
    /// 문서가 정한 통과 조건은 "S1~S5 허들 전원처치 / S6 보스형"이고 요구치는 그 물리가 성립하는지
    /// 보는 검증선이다. 출력≥요구치로 진행을 막으면 판정이 이중화되어 §9 예산식과 드리프트한다.
    ///
    /// 이 판단이 S4 강화-only 벽에서 무한 자살 루프를 막는 유일한 장치다:
    /// 패배하면 다음으로 넘어가지 않고 **현재 스테이지를 반복**하므로, 강화 재료가 모일 때까지
    /// 그 자리에서 파밍이 돈다. 항상 진행(AlwaysAdvance)으로 두면 S4에서 계속 죽으며 수입이 멈춘다.
    /// </summary>
    public static class StageProgression
    {
        public static ProgressionDecision Decide(in ProgressionInput i)
        {
            int last = i.stageCount - 1;
            int current = Clamp(i.currentIndex, 0, last);

            if (i.result != CombatResult.Win)
            {
                // 패배·진행 중 → 현재 스테이지 유지(반복 = 파밍).
                return new ProgressionDecision(current, false, false);
            }

            bool firstClear = current > i.maxClearedIndex;
            int next = current < last ? current + 1 : current; // 마지막 스테이지는 제자리 반복
            return new ProgressionDecision(next, firstClear, next != current);
        }

        private static int Clamp(int v, int lo, int hi)
        {
            if (hi < lo) return lo;
            return v < lo ? lo : (v > hi ? hi : v);
        }
    }
}
