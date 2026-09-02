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

        /// <summary>도메인 리로드 비활성 시 이전 Play의 값이 남는 것을 막는다.</summary>
        public static void Reset() => BoardViewActive = false;
    }
}
