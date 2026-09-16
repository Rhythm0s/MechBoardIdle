namespace MBI.Core
{
    /// <summary>
    /// 튜토리얼 동안 **무엇을 누를 수 있는가** (2026-09-11 사용자 확정 · 튜토리얼 기획서 2장 · 플랜 §71-16 ④).
    ///
    /// **왜 신설하는가.** 종전의 강제는 **빛나게 하는 것**뿐이었다 — 눌러야 할 버튼이 반짝이지만
    /// 나머지도 전부 눌린다. 그래서 다른 것을 눌러 **국면 밖으로 나가 버리면 돌아올 길이 안 보였다**
    /// (리허설 1차 「첫 조작에서 막혔다」의 다른 얼굴이다). 기획서 2장의 「강제」는
    /// **하나만 켜는 것**이지 「하나를 밝게 하는 것」이 아니다.
    ///
    /// ⚠️ **판정하지 않고 그리지 않는다.** <see cref="TutorialSignals"/> 를 읽어 **지금 국면**과
    /// **그 국면이 허용하는 조작**만 낸다 — 어둡게 덮는 것과 입력을 막는 것은 화면 쪽 일이다.
    ///
    /// ⚠️ **국면은 신호에서 따라 나온다** — 새 상태를 만들지 않는다. 상태를 하나 더 두면
    /// 신호와 어긋나는 날이 오고, 그때 화면과 튜토리얼이 서로 다른 국면을 믿게 된다.
    /// </summary>
    public static class TutorialGate
    {
        /// <summary>튜토리얼이 지금 붙잡고 있는 국면.</summary>
        public enum Phase
        {
            /// <summary>튜토리얼이 안 잡고 있다 — 전부 눌린다.</summary>
            None,
            /// <summary>조립 화면으로 들어가야 한다.</summary>
            EnterBoard,
            /// <summary>조립 모드로 바꿔야 한다(기본이 이동 모드다).</summary>
            BuildMode,
            /// <summary>
            /// 고스트 칸을 채워야 한다. **이름은 `PlaceNode` 지만 지금 놓는 것은 벨트다**
            /// (2026-09-11 설계 확정 (가)) — 국면의 뜻은 「고스트 칸을 채운다」이고
            /// 무엇을 놓는가는 시작 보드가 정한다. 이름을 바꾸면 부르는 자리가 전부 흔들려
            /// **폐기 표기로 남긴다.**
            /// </summary>
            PlaceNode,
        }

        /// <summary>막을 수 있는 조작들.</summary>
        public enum Control
        {
            EnterBoard, ExitBoard, ModeToggle,
            PaletteMunitions, PaletteOther, BeltElement, Module, Remove,
            Zoom, MiniMap,
        }

        /// <summary>
        /// 지금 국면. **차례가 있다** — 앞선 것을 못 했으면 뒤엣것을 물을 이유가 없다.
        ///
        /// ⚠️⚠️ **국면은 신호만으로 안 정해진다 — 보드의 현재 모드가 같이 들어온다**
        /// (2026-09-11 결함 수정 · 플랜 §71-19 ①).
        ///
        /// 종전에는 「고스트가 떠 있으면 놓기 국면」이었고, 놓기 국면은 **이미 조립 모드라는
        /// 뜻**으로 읽어 모드 버튼을 잠갔다. 그런데 **이동 모드인 채로 고스트가 뜨는 자리**가
        /// 있었고, 거기서 **모드 버튼이 잠긴 채 조립 모드로 들어갈 수 없었다** — 강제가
        /// 아니라 **정지**였다. 막다른 국면을 시험이 못 잡은 이유도 같다: 시험이 신호만
        /// 넣고 모드를 안 넣었으니 **없는 상태 조합**을 검사한 셈이다.
        ///
        /// 이제 고스트가 떠 있어도 **이동 모드면 모드 국면**이다 — 먼저 조립 모드로 바꾸게 한다.
        /// </summary>
        public static Phase Current =>
            // ⚠️ **보드가 이미 열려 있으면 「들어가라」 국면이 아니다**
            // (2026-09-15 · 「이동 모드 버튼이 안 먹는다」).
            // 신호를 켜는 곳만 있고 끄는 곳이 없어 **들어간 뒤에도 그 국면에 머물렀고**,
            // 그 국면은 조립 버튼 하나만 허락하므로 **보드가 통째로 잠겼다.**
            Resolve(TutorialSignals.HighlightBoardButton && !TutorialSignals.BoardViewOpen,
                    TutorialSignals.HighlightBuildMode,
                    TutorialSignals.GhostCell.HasValue && !TutorialSignals.GhostCellFilled,
                    TutorialSignals.BoardInBuildMode);

        /// <summary>
        /// 국면을 입력에서 직접 따라 낸다(시험·진단용). **모드가 네 번째 입력이다.**
        /// </summary>
        public static Phase Resolve(bool urgeEnterBoard, bool urgeBuildMode,
            bool ghostWaiting, bool inBuildMode)
        {
            if (urgeEnterBoard) return Phase.EnterBoard;

            // ⚠️ **모드가 먼저다.** 이동 모드면 보드를 눌러도 안 놓이므로, 고스트가 떠 있어도
            // 할 수 있는 일은 「조립 모드로 바꾸기」 하나뿐이다.
            if (ghostWaiting) return inBuildMode ? Phase.PlaceNode : Phase.BuildMode;

            // ⚠️⚠️ **이미 조립 모드면 「바꿔라」 국면이 아니다**
            // (2026-09-15 · 사용자 육안 7차 — 「전투로 버튼이 안 눌린다」).
            //
            // `HighlightBuildMode` 도 **켜는 곳만 있고 끄는 곳이 없다** — 앞의
            // `HighlightBoardButton` 과 **똑같은 병이고 신호 하나 옆이다.**
            // 빈 칸을 채우면 `ghostWaiting` 이 거짓이 되어 여기로 내려오는데, 그때
            // 이미 조립 모드인데도 `BuildMode` 국면이 서고 그 국면은
            // **모드 버튼 하나만** 내준다 — **전투로 나갈 수가 없다.**
            //
            // 📌 **막다른 국면이다.** 남은 목표는 「마운트가 가득 찬다」인데 그것은
            // 전투가 돌아야 차오른다. 나갈 수 없으면 **기다리는 것 말고 할 일이 없고**,
            // 화면에는 왜 안 눌리는지가 안 적힌다.
            //
            // 📌 **그리는 쪽은 이미 알고 있었다** — `BoardController` 가 강조를
            // `HighlightBuildMode && !build` 로 끄고 있다. **두 쪽이 다른 것을 믿고 있었고**,
            // 그래서 버튼은 안 빛나는데 잠겨 있었다.
            if (urgeBuildMode && !inBuildMode) return Phase.BuildMode;
            return Phase.None;
        }

        /// <summary>튜토리얼이 조작을 붙잡고 있는가.</summary>
        public static bool Locked => Current != Phase.None;

        /// <summary>지금 국면이 이 조작을 허용하는가.</summary>
        public static bool Allows(Control control) => Allows(Current, control);

        /// <summary>국면을 직접 주고 묻는다(시험·진단용).</summary>
        public static bool Allows(Phase phase, Control control)
        {
            switch (phase)
            {
                case Phase.EnterBoard:
                    // ⚠️ **전투로는 여기서도 연다** — 예외를 두면 규칙이 「언제나」가 아니게 되고,
                    //    그 예외가 어느 화면에 걸리는지를 다시 따져야 한다.
                    //    이 국면은 전투 화면이라 그 버튼이 아예 안 그려진다 — 열어도 안 는다.
                    return control == Control.EnterBoard || control == Control.ExitBoard;

                case Phase.BuildMode:
                    // ⚠️⚠️ **전투로는 언제나 열어 둔다**(2026-09-16 사용자 지시 ·
                    //    「이동 모드·조립 모드와 관계 없이 활성화되어야 함」).
                    //
                    // 🗑️ 구 주석은 「전투로 돌아가는 것도 막는다 — 나가면 아무것도 빛나지
                    //    않는 화면이 된다」였다. **그 걱정이 만든 것이 막다른 국면이다** —
                    //    바로 아래 📌 가 이미 그렇게 적어 두었는데도 잠금은 남아 있었다.
                    //    「빛날 것이 없다」는 안내가 약한 것이고, **나갈 수 없는 것은 갇힌 것**이다.
                    //    둘 중 나중이 훨씬 나쁘다.
                    return control == Control.ModeToggle || control == Control.ExitBoard;

                case Phase.PlaceNode:
                    // ⚠️ **팔레트를 전부 막는다**(2026-09-11 설계 확정 (가)).
                    //
                    // 놓을 것이 **노드가 아니라 벨트**가 됐다. 직선 벨트는 팔레트에 버튼이
                    // 없고 **드래그가 만든다** — 그래서 강제가 「팔레트 하나만 켜기」에서
                    // **「보드만 열기」**로 바뀐다. 기초 군수를 켜 두면 **엉뚱한 것을 놓으라고
                    // 가리키는 셈**이다.
                    //
                    // ⚠️ 모드 버튼은 여전히 막는다 — 이동 모드로 돌아가면 드래그가 화면
                    // 이동이 되어 벨트가 안 깔린다.
                    //
                    // ⚠️⚠️ **전투로는 연다**(2026-09-16 사용자 지시). 튜토리얼 중에도
                    //    나갈 길은 있어야 한다 — 막으면 「기다리는 것 말고 할 일이 없는」
                    //    화면이 되고, 그것이 09-15·09-16 에 두 번 보고된 모양이다.
                    return control == Control.MiniMap || control == Control.Zoom
                           || control == Control.ExitBoard;

                default:
                    return true;
            }
        }

        /// <summary>
        /// 보드 칸을 눌러 놓을 수 있는가. 버튼이 아니라 **격자**라 따로 묻는다.
        ///
        /// 국면 <see cref="Phase.PlaceNode"/> 에서만 참이다 — 그 앞 두 국면에서는
        /// 보드를 눌러도 아무 일이 없어야 「지금 할 일은 저것 하나」가 선다.
        /// </summary>
        public static bool AllowsBoardTap => Current == Phase.None || Current == Phase.PlaceNode;
    }
}
