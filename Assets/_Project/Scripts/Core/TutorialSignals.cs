using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 튜토리얼이 화면에 거는 신호 — **보드와 전투가 읽는 한 방향 채널**.
    ///
    /// <see cref="LogisticsOutputBridge"/>와 같은 패턴이다. 스테이지 0 세션이 쓰고
    /// 보드가 읽는다. 반대 방향은 없다 — 보드가 튜토리얼을 몰라야
    /// 튜토리얼을 걷어낼 때 보드를 건드리지 않는다(9월 4일 되돌림 지점).
    ///
    /// ⚠️ 신호가 꺼져 있으면(<c>GhostCell == null</c>) 보드는 평소대로 그린다.
    /// 그래서 스테이지 0을 떼어내는 것이 진입 경로 한 줄로 끝난다.
    /// </summary>
    public static class TutorialSignals
    {
        /// <summary>
        /// 고스트를 그릴 칸. null이면 안 그린다.
        ///
        /// **고스트를 뺄 수 없는 이유**(튜토리얼 기획서 2·3장): 안내가 없으면 플레이어가
        /// 빈 칸을 못 찾고, 못 찾으면 놓지 못하고, 그러면 목표가 달성되지 않는다.
        /// </summary>
        public static Vector2Int? GhostCell;

        /// <summary>
        /// 지금 눌러야 할 버튼을 빛나게 한다 — 강제 버튼(튜토리얼 기획서 2장).
        /// 스테이지 0에서는 조립 보드로 가는 버튼이다.
        /// </summary>
        public static bool HighlightBoardButton;

        /// <summary>
        /// 고스트 칸이 실제로 채워졌는가 — **보드가 쓰고 튜토리얼이 읽는다.**
        ///
        /// ⚠️ 이 한 칸 때문에 `MBI.Combat`이 `MBI.Logistics`를 참조하지 않아도 된다.
        /// 참조를 걸면 되돌릴 때 asmdef까지 되돌려야 하는데, 되돌림 지점이 걸린 작업에서
        /// 그건 비싼 대가다(9월 4일 게이트 2).
        /// </summary>
        public static bool GhostCellFilled;

        /// <summary>
        /// 팔레트의 **병합기**를 빛나게 한다. 비워 둔 칸이 병합기 자리이므로(2026-09-01),
        /// 자리를 알아도 무엇을 놓을지 모르면 여전히 막힌다 — 고스트는 자리만 말한다.
        /// </summary>
        public static bool HighlightMerger;

        /// <summary>
        /// **조립 모드 버튼**을 빛나게 한다 — 강제 버튼(튜토리얼 기획서 T-7 확정, 2026-08-24).
        ///
        /// ⚠️ 이것이 없으면 첫 조작에서 막힌다. 기본 모드가 **이동**이라 팔레트를 눌러도
        /// 배치가 되지 않는데, 화면에는 그 이유가 없다 — 2026-09-02 브라우저 실측에서
        /// 실제로 병합기를 못 놓았다. 「첫 조작에서 막히는 것은 이탈률 최우선 원칙에
        /// 정면으로 위배된다」(T-7 근거).
        ///
        /// 보드가 **이동 모드일 때만** 그린다. 조립 모드로 바꾸면 할 일이 끝난 표시다.
        /// </summary>
        public static bool HighlightBuildMode;

        /// <summary>
        /// 비워 둔 칸을 **다시 비워 달라** — 촬영 복귀 전용(260902_W09 §1-2).
        ///
        /// 병합기가 놓인 채로 튜토리얼에 돌아가면 조립 화면에 들어가는 순간 통과해 버린다
        /// (2026-09-02 실측: 경과 0.7초). A구간을 다시 찍으려면 그 칸이 비어 있어야 한다.
        ///
        /// 보드가 가져가며 내린다 — 안 내리면 플레이어가 놓는 족족 지워진다.
        /// </summary>
        public static bool ClearEmptySlotRequested;

        /// <summary>도메인 리로드 비활성 시 이전 Play의 값이 남는 것을 막는다.</summary>
        public static void Reset()
        {
            GhostCell = null;
            HighlightBoardButton = false;
            GhostCellFilled = false;
            HighlightMerger = false;
            HighlightBuildMode = false;
            ClearEmptySlotRequested = false;
        }
    }
}
