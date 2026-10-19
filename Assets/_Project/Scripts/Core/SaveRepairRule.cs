using MBI.Data;

namespace MBI.Core
{
    /// <summary>
    /// **옛 저장 자동 복구** (사용자 확정 #209 · 2026-10-19 · 플랜 추천).
    ///
    /// #197 결함(B 판을 연 뒤 새로고침하면 A 칸이 시작 보드로 덮임) 때 저장된 A 판은
    /// 튜토리얼 운반로 칸이 빈 채라 줄이 끊겨 HUD 전투력 0 으로 섰다. 불러온 판이
    /// **A · 튜토리얼 마침 · 그 칸 빔** 이면 채운다. B 판은 튜토리얼 칸이 없어 안 건드린다.
    /// </summary>
    public static class SaveRepairRule
    {
        public static bool ShouldFillAfterRestore(MountOwner owner, bool tutorialCleared, bool fillCellEmpty) =>
            owner == MountOwner.RobotA && tutorialCleared && fillCellEmpty;
    }
}
