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
            /// <summary>고스트 칸에 기초 군수를 놓아야 한다.</summary>
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
            Resolve(TutorialSignals.HighlightBoardButton,
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

            if (urgeBuildMode) return Phase.BuildMode;
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
                    return control == Control.EnterBoard;

                case Phase.BuildMode:
                    // ⚠️ **전투로 돌아가는 것도 막는다.** 여기서 나가면 국면이 EnterBoard 로
                    // 되돌아가는 것이 아니라 **아무것도 빛나지 않는 화면**이 된다.
                    return control == Control.ModeToggle;

                case Phase.PlaceNode:
                    // ⚠️ **모드 버튼을 막는다.** 이 국면은 이미 조립 모드라는 뜻이고,
                    // 여기서 이동 모드로 돌아가면 보드를 눌러도 안 놓여 다시 막힌다.
                    return control == Control.PaletteMunitions || control == Control.MiniMap
                           || control == Control.Zoom;

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
