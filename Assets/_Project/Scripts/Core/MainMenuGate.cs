namespace MBI.Core
{
    /// <summary>
    /// 메인 메뉴가 열려 있는 동안 게임이 시작되지 않게 잡아 두는 빗장
    /// (2026-09-10 사용자 확정 · 플랜 §66-10).
    ///
    /// **왜 <c>Time.timeScale</c> 이 아닌가.** 시간을 0 으로 멈추면 **이미 시작한 것을 세우는**
    /// 것이지 **아직 시작하지 않은 것**이 되지 않는다. 전투는 시작하는 순간 창고·마운트·
    /// 발사 배분을 한 번 잡는데, 메뉴를 보는 동안 그것이 잡혀 있으면 「게임 시작」이
    /// 실제로는 **이어하기**가 된다. 여기서 잡는 것은 **시작 자체**다.
    ///
    /// ⚠️ **기본은 「열려 있지 않다」이다.** 메뉴가 없는 씬(격리 전투 씬·시험)에서
    /// 기본이 「열림」이면 **아무도 닫아 주지 않아 영원히 시작하지 않는다.**
    /// 메뉴가 스스로 <see cref="Open"/> 을 부르고, 메뉴가 없으면 빗장도 없다.
    ///
    /// ⚠️ **시작은 한 번뿐이다.** <see cref="TryStart"/> 는 처음 한 번만 참을 내며,
    /// 그래서 부르는 쪽이 매 프레임 물어도 <c>Begin</c> 이 두 번 돌지 않는다.
    /// </summary>
    public static class MainMenuGate
    {
        /// <summary>메뉴가 화면을 덮고 있는가. 메뉴가 없는 씬에서는 늘 거짓이다.</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>이미 시작했는가.</summary>
        public static bool HasStarted { get; private set; }

        /// <summary>지금 시작해도 되는가 — 묻기만 하고 아무것도 바꾸지 않는다.</summary>
        public static bool CanStart => !IsOpen && !HasStarted;

        /// <summary>메뉴가 뜬다. 시작을 막는다.</summary>
        public static void Open() => IsOpen = true;

        /// <summary>「게임 시작」을 눌렀다. 다음 <see cref="TryStart"/> 가 참을 낸다.</summary>
        public static void Close() => IsOpen = false;

        /// <summary>
        /// 시작을 집는다. **처음 한 번만 참**이며, 그 뒤로는 메뉴를 다시 열어도 거짓이다 —
        /// 되돌아온 메뉴는 「다시 시작」이 아니라 「보고 있는 중」이다.
        /// </summary>
        public static bool TryStart()
        {
            if (!CanStart) return false;
            HasStarted = true;
            return true;
        }

        /// <summary>씬을 다시 열 때 처음 상태로 — 열려 있지 않고, 시작한 적도 없다.</summary>
        public static void Reset()
        {
            IsOpen = false;
            HasStarted = false;
        }
    }
}
