namespace MBI.Core
{
    /// <summary>
    /// 물류 보드가 사는 렌더 층 (2026-09-16 · 육안 ② 「전투 화면에 보드가 비친다」).
    ///
    /// ⚠️⚠️ **왜 층으로 가르는가.** 종전에는 보드를 **멀리 두는 것**으로 숨겼다 —
    /// 보드 중심이 월드 `y = -20` 이고 전투 중심이 `y = 0` 이라 20 칸이 떨어져 있었다.
    ///
    /// 그런데 09-11 에 **이동 클램프가 폐기**되어 전장이 무한해졌고, 09-15 에
    /// **카메라가 로봇을 따라가게** 됐다. 전투 카메라의 반높이는 8 이고 보드가 차지하는
    /// 자리의 윗변은 대략 `y = -15` 이므로 — **로봇이 `y = -7` 아래로 내려가면
    /// 보드 윗변이 화면 아래에서 올라온다.** 사용자가 본 것이 그것이다.
    ///
    /// 📌 **거리를 늘리는 것은 고치는 것이 아니다.** 전장이 무한인 한 어떤 거리도
    /// 언젠가 닿는다. 규칙은 거리가 아니라 **「전투 화면에서는 보드를 안 그린다」**이고,
    /// 그것을 그대로 적을 수 있는 자리가 층과 컬링 마스크다.
    /// </summary>
    public static class BoardLayer
    {
        /// <summary>
        /// 보드 층의 번호. `ProjectSettings/TagManager.asset` 의 8번 칸에 「Board」로 적혀 있다.
        ///
        /// ⚠️ 이름이 아니라 **번호로 쓴다** — `LayerMask.NameToLayer` 는 이름이 안 맞으면
        /// 조용히 `-1` 을 주고, `-1` 로 만든 마스크는 **전부 끄거나 전부 켠다.**
        /// 값이 두 곳에 살지 않게, 여기 한 곳에서만 적는다(지침 §7).
        /// </summary>
        public const int Index = 8;

        /// <summary>보드 층 하나만 켠 마스크.</summary>
        public const int Mask = 1 << Index;

        /// <summary>
        /// 이 카메라가 써야 할 컬링 마스크 — <paramref name="boardVisible"/> 면 보드 층을 켜고
        /// 아니면 끈다. 나머지 층은 <paramref name="baseMask"/> 그대로 둔다.
        ///
        /// ⚠️ **나머지를 건드리지 않는 것이 요점이다.** 마스크를 통째로 써 버리면
        /// 씬이 정해 둔 다른 층까지 이 코드가 정하게 된다.
        /// </summary>
        public static int MaskFor(int baseMask, bool boardVisible)
            => boardVisible ? (baseMask | Mask) : (baseMask & ~Mask);
    }
}
