namespace MBI.Core
{
    /// <summary>
    /// **보드가 열릴 때 어느 모드로 서는가** (2026-10-15 사용자 확정 #135).
    ///
    /// 사용자 문장: 「튜토리얼 시작 시 물류 보드의 기본 상태를 조립 모드로」 →
    /// 확인 답 「상태는 전투 화면, 조립 보드 등장 시, 이동 모드와 설치 모드에서 이동 모드가 아닌
    /// **설치 모드로 등장**할 것」. 첫 화면은 그대로 전투 화면이고, **보드가 열리는 순간** 모드가 갈린다.
    ///
    /// 📌 **튜토리얼 판에서만이다.** 보드 기본 모드 「이동」은 2026-09-28 사용자 확정
    ///    (「처음 보는 사람이 실수로 벨트를 깔지 않게」)이고 그대로다. 튜토리얼은 **벨트 한 칸을
    ///    까는 수업**이라, 이동 모드로 열리면 「조립 모드로 바꿔라」 한 계단을 더 밟아야 했다 —
    ///    끌어도 화면만 밀려 **수업이 안 먹는 것처럼** 보였다(코드 세션 실플레이에서도 그랬다).
    /// 📌 국면 「조립 모드로 바꿔라」(`TutorialGate.Phase.BuildMode`)는 **안 지운다** — 열릴 때
    ///    이미 조립 모드라 그 국면이 저절로 안 선다(게이트가 모드를 입력으로 받는다).
    ///    누가 이동 모드로 되돌리면 그 국면이 다시 서서 안내한다.
    /// </summary>
    public static class BoardOpenModeRule
    {
        /// <summary>보드가 **막 열렸을 때** 조립(설치) 모드로 세울 것인가.</summary>
        public static bool OpensInBuildMode(bool isTutorialStage) => isTutorialStage;

        /// <summary>
        /// T4(모듈 배치 튜토리얼)도 조립 모드로 연다 (2026-10-15 · #138 · ⚠️ 구현 판단).
        ///
        /// 모듈은 **조립 모드에서 노드를 탭해야** 붙는다 — 이동 모드에서는 누름이 화면 이동이 된다.
        /// #135 와 같은 까닭이다: 이동 모드로 열면 「바꿔라」 한 계단이 더 생기고 안내한 대로 해도
        /// 안 먹는 것처럼 보인다. 사용자 확정 #135 의 뜻(튜토리얼 보드 = 설치 모드)을 T4 에도 건다.
        /// </summary>
        public static bool OpensInBuildMode(bool isTutorialStage, bool moduleTutorialActive) =>
            isTutorialStage || moduleTutorialActive;

        /// <summary>
        /// ✅ **#252(2026-10-20 사용자 확정) — 설치 걸음 전부 설치 모드로.** 「카메라가 거기까지 포커스가 진행되면 이동이 필요 없으니
        /// 설치 모드를 기본으로」. 범위 = 튜토리얼 판(T1) · T4 모듈 · 고스트 칸이 떠 있는 걸음(T3 등) · S5 폭발탄 걸음 ①~⑤
        /// (#241 · #246 포커스가 걸리는 자리와 같은 범위 · ⚠️ 범위 가정). 보드가 **열릴 때**와 걸음이 **새로 설 때** 한 번 켠다 —
        /// 사용자가 이동 모드로 바꾸면 그대로 둔다(모드 단추 그대로) · 걸음이 끝나도 되돌리지 않는다(⚠️ 가정).
        /// 국면 「조립 모드로 바꿔라」(`TutorialGate.Phase.BuildMode`)는 이미 설치 모드면 저절로 안 선다(자동 통과).
        /// </summary>
        public static bool PlaceStepActive(bool isTutorialStage, bool moduleTutorialActive, bool ghostWaiting, bool explosiveActive) =>
            isTutorialStage || moduleTutorialActive || ghostWaiting || explosiveActive;
    }
}
