namespace MBI.Core
{
    /// <summary>노드 칸에 얹는 상태 표식 다섯 (2026-09-11 · 플랜 §71-33 ③).</summary>
    public enum NodeIcon
    {
        None,
        Normal,        // icon_logi_normal
        Slow,          // icon_logi_slow
        Stopped,       // icon_logi_stop
        NotConnected,  // icon_not_connected
        PowerShort,    // icon_power_short
    }

    /// <summary>
    /// 어느 표식을 얹을지 고른다 (2026-09-11 신설 · 플랜 §71-33 ③).
    ///
    /// **왜 신설하는가.** `Art/UI/` 에 상태 아이콘 다섯이 09-04 부터 설치돼 있는데
    /// **코드가 한 곳에서도 안 읽고 있었다**(실측: 이름으로 찾은 참조 0건).
    /// 효과음 여덟과 같은 꼴이다 — **자산은 왔고 배선만 없었고, 그래서 에러가 없었다.**
    ///
    /// **왜 밝기 틴트로 부족한가.** 지금 상태는 `NodeStatusTint` 의 **밝기**로만 말한다.
    /// 어두운 칸이 「정지」인지 「그늘진 그림」인지는 옆 칸과 견줘야 알 수 있고,
    /// **전력 부족과 미연결은 밝기 축에 자리 자체가 없다.** 표식은 그 둘을 위해 있다.
    ///
    /// ⚠️ **하나만 얹는다.** 한 칸에 둘을 겹치면 무엇을 고쳐야 하는지가 도로 흐려진다 —
    /// 그래서 여기는 우선순위를 가진 **판정**이지 매핑이 아니다.
    /// </summary>
    public static class NodeStatusIcon
    {
        /// <summary>
        /// 정상 칸에도 표식을 얹는가. ⚠️ **가정 false** — 규격 문서에 절이 없다.
        ///
        /// 시작 보드만 117칸이라 정상에도 얹으면 **화면이 표식으로 덮이고**,
        /// 그러면 정작 봐야 할 정지·전력 표식이 그 안에 묻힌다.
        /// 「이상이 있는 칸에만 뜬다」가 표식을 표식이게 한다.
        /// `icon_logi_normal` 은 그래서 **자산은 물려 두고 안 쓴다**(설계 역기입 자리).
        /// </summary>
        public const bool ShowWhenNormal = false;

        /// <summary>
        /// 표식 하나를 고른다.
        ///
        /// **차례가 곧 진단 순서다** — 전력이 먼저다. 전력이 모자라면 여러 칸이 한꺼번에
        /// 느려지는데, 그때 칸마다 「느림」을 띄우면 **원인 하나가 결과 여럿으로 보인다.**
        /// 미연결이 그다음이다 — 끊긴 줄은 산출률이 0 이라 「정지」로도 읽히지만,
        /// 고칠 것은 노드가 아니라 **줄**이다.
        /// </summary>
        public static NodeIcon Of(bool powerShort, bool notConnected, float ratio)
        {
            if (powerShort) return NodeIcon.PowerShort;
            if (notConnected) return NodeIcon.NotConnected;

            // 밝기와 **같은 눈금**을 쓴다 — 표식과 밝기가 다른 말을 하면 둘 다 안 믿게 된다.
            float tint = NodeStatusTint.Of(ratio);
            if (tint <= NodeStatusTint.Stopped + 0.0001f) return NodeIcon.Stopped;
            if (tint < NodeStatusTint.Normal - 0.0001f) return NodeIcon.Slow;

            return ShowWhenNormal ? NodeIcon.Normal : NodeIcon.None;
        }
    }
}
