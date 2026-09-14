namespace MBI.Core
{
    /// <summary>
    /// 지금 어느 화면을 보고 있는가 — **레이어가 쓰고 전투 UI가 읽는 중립 채널.**
    ///
    /// <see cref="LogisticsOutputBridge"/>·<see cref="TutorialSignals"/>와 같은 패턴이다.
    /// `MBI.Combat`은 `MBI.Logistics`를 참조하지 않으므로(의도된 분리) `GameLayerController`를
    /// 직접 볼 수 없다. 그런데 **전투 쪽 UI도 화면이 바뀐 것을 알아야 한다** —
    /// 안 그러면 조립 화면 위에 전투용 버튼이 그대로 떠서 보드 팔레트를 덮는다.
    ///
    /// ⚠️ 실제로 그렇게 됐다. 심사자용 바로가기(우측 하단)가 조립 화면에서 노드 팔레트의
    /// 「병합기」를 통째로 가려 **보드에서 병합기를 고를 수 없었다**(2026-09-02 브라우저 실측).
    /// 두 OnGUI가 같은 자리를 그리는데 서로를 몰랐던 것이다.
    /// </summary>
    public static class GameViewSignals
    {
        /// <summary>조립(물류 보드) 레이어가 활성인가. 레이어 컨트롤러가 매 프레임 넣는다.</summary>
        public static bool BoardViewActive;

        /// <summary>
        /// **카메라가 비출 자리** — 싸우는 로봇의 위치 (2026-09-15 사용자 확정 · 육안 ⑥ · UI 9-5).
        ///
        /// ⚠️ **종전에는 카메라가 고정이었다.** 로봇만 절대 좌표로 걸어 다니고 바닥 타일만
        /// 밀어 「흐르는 것처럼」 보이게 했는데, 09-11 에 이동 클램프(아레나 원반)가 폐기되면서
        /// **로봇이 화면 밖까지 걸어 나갈 수 있게 됐다** — 09-15 육안에서 스테이지 시작 9 초에
        /// 로봇이 뷰포트 오른쪽 끝에 붙어 있었다.
        ///
        /// 전투가 쓰고 레이어 컨트롤러가 읽는다. <see cref="BoardViewActive"/> 와 같은 채널이라
        /// `MBI.Combat` 이 `MBI.Logistics` 를 참조하지 않아도 된다.
        ///
        /// ⚠️ **주 카메라와 인셋 카메라가 같이 따라간다** — 조립 화면 상단 30% 의 인셋도
        /// 같은 자리를 비춰야 「지금 어디서 싸우는가」가 두 화면에서 어긋나지 않는다.
        /// </summary>
        public static UnityEngine.Vector2 CombatFocus;

        /// <summary>
        /// <see cref="CombatFocus"/> 가 실제 전투에서 온 값인가. 전투가 없으면 <c>false</c> 이고
        /// 그때는 씬이 정한 중심을 쓴다 — **0,0 으로 튀지 않게** 하는 것이 이 깃발의 일이다.
        /// </summary>
        public static bool HasCombatFocus;

        /// <summary>도메인 리로드 비활성 시 이전 Play의 값이 남는 것을 막는다.</summary>
        public static void Reset()
        {
            BoardViewActive = false;
            CombatFocus = UnityEngine.Vector2.zero;
            HasCombatFocus = false;
        }
    }
}
