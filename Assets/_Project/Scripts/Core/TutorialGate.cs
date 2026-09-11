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
        /// ⚠️ <see cref="Phase.BuildMode"/> 가 <see cref="Phase.PlaceNode"/> 보다 앞이다.
        /// 고스트가 떠 있어도 **이동 모드면 보드를 눌러도 안 놓인다** — 그 자리에서 막힌 것이
        /// T-7 의 근거였다.
        /// </summary>
        public static Phase Current
        {
            get
            {
                if (TutorialSignals.HighlightBoardButton) return Phase.EnterBoard;
                if (TutorialSignals.HighlightBuildMode) return Phase.BuildMode;
                if (TutorialSignals.GhostCell.HasValue && !TutorialSignals.GhostCellFilled)
                    return Phase.PlaceNode;
                return Phase.None;
            }
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
