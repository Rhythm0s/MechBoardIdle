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
        /// ⚠️ **폐기 — 아무도 안 켠다**(2026-09-11 설계 확정 (가)).
        ///
        /// 팔레트의 병합기를 빛나게 하던 신호다. 비워 둔 칸이 병합기 자리였을 때(2026-09-01)
        /// 만들었고, 그 뒤 기초 가공소로, 다시 **운반로 벨트 한 칸**으로 옮겨 갔다.
        /// 지금 채우는 것은 드래그로 까는 **직선 벨트**라 팔레트에 고를 버튼이 없고,
        /// 놓기 국면은 팔레트를 **전부 잠근다** — 켜 두면 **잠긴 채 깜빡이는 버튼**이 된다.
        ///
        /// **자리는 남긴다** — 뒤 스테이지에서 병합기를 가르칠 때 같은 장치가 필요하다
        /// (분류기는 S3 수업 · `260911_W01` 2-4).
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

        /// <summary>
        /// 비워 둔 칸을 **채워 달라** — 심사자 바로가기 전용 (2026-09-15 사용자 확정 · 육안 ⑦).
        ///
        /// ⚠️ **이것이 없어서 심사자가 끊긴 보드로 S1 에 떨어졌다.** 보드는 종전에
        /// <c>IdleSignals.TutorialCleared</c> 일 때만 그 칸을 채웠는데, 바로가기는
        /// 튜토리얼을 **건너뛰므로** 그 깃발이 안 선다. 그러면 운반로가 (6,5) 에서 끊긴 채라
        /// **탄이 마운트에 하나도 안 닿는다** — 09-15 육안 ① 의 뿌리가 그것이었다
        /// (실측: 안 채운 판 60 초 0 개 / 채운 판 219 개).
        ///
        /// ⚠️ **튜토리얼 스테이지 자체는 안 건드린다.** 튜토리얼로 **돌아갈** 때는 여전히
        /// 비어 있어야 배울 것이 남는다 — 그쪽은 <see cref="ClearEmptySlotRequested"/> 다.
        ///
        /// 보드가 가져가며 내린다 — 안 내리면 플레이어가 지우는 족족 다시 깔린다.
        /// </summary>
        public static bool FillEmptySlotRequested;

        /// <summary>
        /// 보드가 지금 **조립 모드**인가 — <b>보드가 쓰고 튜토리얼 게이트가 읽는다.</b>
        /// (2026-09-11 결함 수정 · 플랜 §71-19 ①)
        ///
        /// ⚠️ <b>이것이 없어서 막다른 국면이 났다.</b> 게이트가 「놓기」 국면을 <b>고스트가
        /// 떠 있는가</b>로만 잡았는데, 그 국면은 <b>이미 조립 모드라는 뜻</b>으로 읽고
        /// 모드 버튼을 잠갔다 — 실제로는 이동 모드인 채로 고스트가 떠서
        /// <b>모드 버튼이 잠긴 채 조립 모드로 들어갈 수 없었다.</b>
        ///
        /// 「지금 무엇을 할 수 있는가」는 신호만으로 못 정한다. <b>보드가 어떤 상태인지</b>가
        /// 같이 들어와야 한다.
        /// </summary>
        public static bool BoardInBuildMode;

        /// <summary>
        /// 조립 보드가 **지금 열려 있는가** — <b>레이어가 쓰고 튜토리얼 게이트가 읽는다.</b>
        /// (2026-09-15 · 「이동 모드 (바꾸기) 버튼이 안 먹는다」 · 09-11 결함과 같은 모양)
        ///
        /// ⚠️⚠️ **이것이 없어서 보드가 통째로 잠겼다.** 게이트의 첫 국면은
        /// 「조립 버튼을 눌러라」(<see cref="HighlightBoardButton"/>)인데, 그 신호를
        /// <b>켜는 곳만 있고 끄는 곳이 없었다</b> — 눌러서 보드에 들어가도 국면이
        /// `EnterBoard` 에 머물렀고, 그 국면은 **조립 버튼 하나만** 허락한다.
        /// 그래서 보드 안에서 <b>모드 버튼·팔레트·탭·칸 탭이 전부 잠겼다.</b>
        ///
        /// 📌 **튜토리얼이 끝날 수 없었다** — 벨트를 깔려면 조립 모드로 바꿔야 하는데
        /// 그 버튼이 잠겨 있으니 첫 줄(「끊긴 자리를 잇는다」)이 영영 안 선다.
        /// 09-15 「목표 달성 불가」의 뿌리가 이것이다. 보드·물류·마운트를 다 재고도
        /// 못 찾았던 까닭은 **그 셋이 다 멀쩡했기** 때문이다 — 막힌 것은 <b>손</b>이었다.
        ///
        /// ⚠️ **끄는 것이 아니라 상태를 읽는다.** 신호를 눌릴 때 끄면 전투로 돌아갔을 때
        /// 다시 켜 줄 곳이 필요해지고, 그 자리를 또 빠뜨린다(09-11 에 같은 이유로
        /// <see cref="BoardInBuildMode"/> 를 들여놓았다).
        /// </summary>
        public static bool BoardViewOpen;

        /// <summary>
        /// 튜토리얼 목표 둘 — **스테이지 0 세션이 쓰고 전투 화면의 마일스톤 카드가 읽는다**
        /// (2026-09-18 설계 지시 ④).
        ///
        /// ⚠️⚠️ **판정은 여기 없다.** 걸쇠는 <see cref="Stage0Goal"/> 하나가 쥐고,
        /// 이 칸들은 그 값을 **옮기는 관**이다 — 화면이 제 걸쇠를 따로 두면
        /// 카드와 스테이지가 서로 다른 달성 상태를 믿게 된다(지침 §7).
        ///
        /// ⚠️ <see cref="GoalActive"/> 가 거짓이면 카드를 **안 그린다** — 튜토리얼이 아닌
        /// 판에서 「끊긴 자리를 잇는다」가 떠 있으면 그것은 없는 목표다.
        /// </summary>
        public static bool GoalActive;

        /// <summary>목표 ① — 끊긴 자리를 잇는다.</summary>
        public static bool GoalSlotFilled;

        /// <summary>목표 ② — 마운트가 가득 찬다.</summary>
        public static bool GoalMountFilled;

        /// <summary>도메인 리로드 비활성 시 이전 Play의 값이 남는 것을 막는다.</summary>
        public static void Reset()
        {
            GhostCell = null;
            HighlightBoardButton = false;
            GhostCellFilled = false;
            HighlightMerger = false;
            HighlightBuildMode = false;
            ClearEmptySlotRequested = false;
            FillEmptySlotRequested = false;
            BoardInBuildMode = false;
            BoardViewOpen = false;
            GoalActive = false;
            GoalSlotFilled = false;
            GoalMountFilled = false;
        }
    }
}
