namespace MBI.Core
{
    /// <summary>
    /// **설정 패널이 열려 있는가** (2026-09-18 사용자 확정 · 설정 칩 되살리기).
    ///
    /// ⚠️⚠️ **<see cref="MainMenuGate"/> 와 다른 빗장이다.** 메뉴 빗장은 **게임의 시작을 막고**
    /// `OnGUI` 일곱을 억제한다 — 그것을 설정에 쓰면 **누를 때마다 HUD 가 통째로 사라진다**
    /// (2026-09-18 사용자 육안에서 실제로 났다. 그 결함이 이 파일이 생긴 까닭이다).
    ///
    /// 📌 **설정은 얹기만 한다** — 억제하지 않는다. 열려 있어도 칩 줄·배지·원형·상태 띠는
    /// 그대로 살아 있고, 패널이 그 위에 그려질 뿐이다.
    ///
    /// ⚠️ **소리는 여기 안 든다.** 소리 패널은 제 버튼(`AudioOptionsPanel`)이 이미 있다 —
    /// 설정 안에 하나 더 두면 **같은 일을 하는 자리가 둘**이 된다(사용자 확정 · 중복 금지).
    /// </summary>
    public static class SettingsGate
    {
        /// <summary>설정 패널이 떠 있는가. 기본은 닫힘이다.</summary>
        public static bool IsOpen { get; private set; }

        public static void Open() => IsOpen = true;
        public static void Close() => IsOpen = false;
        public static void Toggle() => IsOpen = !IsOpen;

        /// <summary>
        /// **「메인 메뉴로」** — 설정을 닫고 메뉴 빗장을 건다(2026-09-18 사용자 확정).
        ///
        /// ⚠️ **시작 깃발은 안 내린다**(<see cref="MainMenuGate.HasStarted"/>). 되돌아온 메뉴는
        /// 「다시 시작」이 아니라 **「보고 있는 중」**이다 — `MainMenuGate.TryStart` 주석이
        /// 이미 그렇게 정해 두었고, 내리면 전투가 **처음부터 다시 잡힌다.**
        ///
        /// 📌 전투를 **세우는 일**은 러너가 이 빗장을 보고 한다 — 여기서는 상태만 바꾼다
        /// (판정도 시간도 이 파일에 없다).
        /// </summary>
        public static void ReturnToMainMenu()
        {
            IsOpen = false;
            MainMenuGate.Open();
        }

        /// <summary>씬을 다시 열 때 처음 상태로. 도메인 리로드를 꺼도 값이 안 남게 한다.</summary>
        public static void Reset() => IsOpen = false;
    }
}
