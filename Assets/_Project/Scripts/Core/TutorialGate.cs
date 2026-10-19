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

            /// <summary>
            /// **T3 — 도전에 들어가야 한다**(튜토리얼 기획서 4장 · 2026-10-14 확정 #115).
            ///
            /// 앞 국면 셋은 **조립 화면**의 일이고 이것만 **전투 화면**의 일이다 —
            /// 가리키는 버튼이 「도전」이고, 잠그는 것은 「조립」·「스테이지」다.
            /// </summary>
            Challenge,

            /// <summary>
            /// **T4 ① — 조립 화면으로 들어가야 한다**(2026-10-15 사용자 확정 #138 · 전투 화면).
            /// 가리키는 버튼은 「▼ 조립」이고, 잠그는 것은 T3 와 같다(「도전」·「스테이지」).
            /// </summary>
            ModuleEnterBoard,

            /// <summary>
            /// **T4 ② — 대상 노드에 모듈을 붙여야 한다**(조립 화면). 팔레트 「모듈」 탭·M/R 단추와
            /// 대상 노드만 열린다.
            /// </summary>
            PlaceModule,

            /// <summary>
            /// **S4 파밍 — 「로봇 B 해금」 하나만 밝다**(2026-10-19 사용자 확정 #173). T3 와 같은 꼴 —
            /// 해금 단추 말고는 안 눌린다(조립 진입 · 도전 · 목록). 누르면(해금) 저절로 풀린다.
            /// </summary>
            UnlockRobotB,

            /// <summary>
            /// **S4 파밍 — 강화 안내 ① 전투 화면: 「▼ 조립」 하나만**(2026-10-19 사용자 확정 #182).
            /// T4 ① 과 같은 꼴 — 「도전」·「스테이지」를 닫는다(「▲ 전투로」 예외).
            /// </summary>
            EnhanceMount,

            /// <summary>
            /// **S4 파밍 — 강화 안내 ② 보드 화면: A 마운트 칸 (1,4)**(#182 · T1 막 재사용).
            /// 팔레트 · 지우기는 닫고 모드 단추 · 줌 · 미니맵 · 「▲ 전투로」는 연다(마운트 탭은 이동 모드 팝오버로도 된다 #148).
            /// 보드 탭은 마운트 칸 팝오버만 연다(`BoardController.OnPressEnd`). 강화하면(표식) 저절로 풀린다.
            /// </summary>
            EnhanceMountBoard,

            /// <summary>
            /// **S4 도전 층 — 합체 원형 하나만**(사용자 확정 #189 · 2026-10-19). T3 와 같은 꼴 —
            /// 합체 말고는 안 눌린다(태그 · 도전 · 목록 · 조립 진입) · 「▲ 전투로」 예외. 합체하면 저절로 풀린다.
            /// </summary>
            Merge,

            /// <summary>**S5 폭발탄 ① 전투 화면: 「▼ 조립」 하나만**(사용자 확정 #190 · T4 ① 과 같은 꼴).</summary>
            Explosive,

            /// <summary>
            /// **S5 폭발탄 ② 보드 화면: 가리키는 칸 하나**(#190). 팔레트는 그 걸음의 단추만(보드가 거른다) ·
            /// 지우기는 닫는다(대상 칸은 탭 한 번에 덮어쓴다) · 모드 · 줌 · 미니맵 · 「▲ 전투로」는 연다.
            /// </summary>
            ExplosiveBoard,
        }

        /// <summary>막을 수 있는 조작들.</summary>
        public enum Control
        {
            EnterBoard, ExitBoard, ModeToggle,
            // 🗑️ `PaletteMunitions` — 고스트가 노드였던 때의 「기초 가공소 하나만」 잔재. 팔레트는 더 이상 이것을
            //    묻지 않는다(사용자 확정 #183 · 노드 단추는 모두 `PaletteOther`). 지우지 않고 남긴다(폐기 표기).
            PaletteMunitions, PaletteOther, BeltElement, Module, Remove,
            Zoom, MiniMap,

            // ⚠️ **전투 화면의 조작 둘**(2026-10-14 · T3). 위의 것들은 조립 화면 것이다.
            EnterChallenge, StageList,

            // ⚠️ **팔레트 「모듈」 탭 하나**(2026-10-15 · T4). 다른 탭은 `PaletteOther` 가 든다 —
            //    T4 는 모듈 탭만 열어야 해서 따로 묻는다. 다른 국면에서는 `PaletteOther` 와 같은 답이다.
            ModuleTab,

            /// <summary>로봇 B 해금 단추(#173).</summary>
            UnlockRobotB,

            /// <summary>합체 원형(#189). 다른 국면에서는 늘 열린다.</summary>
            Merge,

            /// <summary>태그 원형(#189 — 합체 강제 동안만 닫힌다).</summary>
            TagButton,
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
            // #189 — 합체 강제는 도전 층의 일이라 다른 국면과 안 겹친다. 맨 앞에서 묻는다.
            TutorialSignals.HighlightMergeButton ? Phase.Merge :
            // #190 — S5 폭발탄. A 판의 일이라 보드에서는 A 판을 볼 때만 잡는다.
            TutorialSignals.ExplosiveActive && !TutorialSignals.BoardViewOpen ? Phase.Explosive :
            TutorialSignals.ExplosiveActive && TutorialSignals.BoardViewIsTutorialBoard ? Phase.ExplosiveBoard :
            // ⚠️ **보드가 이미 열려 있으면 「들어가라」 국면이 아니다**
            // (2026-09-15 · 「이동 모드 버튼이 안 먹는다」).
            // 신호를 켜는 곳만 있고 끄는 곳이 없어 **들어간 뒤에도 그 국면에 머물렀고**,
            // 그 국면은 조립 버튼 하나만 허락하므로 **보드가 통째로 잠겼다.**
            // #195 — B 안내도 같은 국면 둘(전투 「▼ 조립」 / 보드 마운트 칸)을 쓴다. 보드에서는 **그 판을 보고 있을 때만**.
            Resolve((TutorialSignals.HighlightEnhanceMount
                        && (!TutorialSignals.BoardViewOpen || TutorialSignals.BoardViewIsTutorialBoard))
                    || (TutorialSignals.HighlightEnhanceMountB
                        && (!TutorialSignals.BoardViewOpen || !TutorialSignals.BoardViewIsTutorialBoard)),
                    TutorialSignals.HighlightUnlockRobotB,
                    TutorialSignals.HighlightChallengeButton,
                    TutorialSignals.ModuleTutorialActive,
                    TutorialSignals.BoardViewOpen,
                    TutorialSignals.HighlightBoardButton && !TutorialSignals.BoardViewOpen,
                    TutorialSignals.HighlightBuildMode,
                    // ⚠️ **제 판에서만 기다린다**(2026-10-06 육안 결함) — 고스트 좌표에는
                    //    주인이 없어서, B 탭을 열면 A 의 고스트가 B 판을 덮고 잠갔다.
                    TutorialSignals.GhostCell.HasValue && !TutorialSignals.GhostCellFilled
                        && TutorialSignals.BoardViewIsTutorialBoard,
                    TutorialSignals.BoardInBuildMode);

        /// <summary>
        /// 국면을 입력에서 직접 따라 낸다(시험·진단용). **모드가 네 번째 입력이다.**
        /// </summary>
        /// <summary>
        /// **조립 화면 국면 셋만** 묻는다(시험·진단). T3 는 안 묻는다 —
        /// 그쪽은 전투 화면의 일이라 이 넷과 겹치지 않는다.
        ///
        /// ⚠️ **화면은 이쪽을 쓰지 않는다** — <see cref="Current"/> 가 다섯 인자 쪽을 쓴다.
        /// </summary>
        public static Phase Resolve(bool urgeEnterBoard, bool urgeBuildMode,
            bool ghostWaiting, bool inBuildMode)
            => Resolve(false, urgeEnterBoard, urgeBuildMode, ghostWaiting, inBuildMode);

        public static Phase Resolve(bool urgeChallenge, bool urgeEnterBoard, bool urgeBuildMode,
            bool ghostWaiting, bool inBuildMode)
            => Resolve(urgeChallenge, false, false, urgeEnterBoard, urgeBuildMode, ghostWaiting, inBuildMode);

        /// <summary>
        /// T4 까지 묻는 갈래 (2026-10-15 · #138). <paramref name="urgeModule"/> 이 서면
        /// 조립 화면이 닫혔으면 「들어가라」, 열렸으면 「붙여라」다.
        /// </summary>
        public static Phase Resolve(bool urgeChallenge, bool urgeModule, bool boardViewOpen,
            bool urgeEnterBoard, bool urgeBuildMode, bool ghostWaiting, bool inBuildMode)
            => Resolve(false, urgeChallenge, urgeModule, boardViewOpen,
                       urgeEnterBoard, urgeBuildMode, ghostWaiting, inBuildMode);

        /// <summary>
        /// #173 — 맨 앞에 「로봇 B 해금」 국면을 얹은 판. 다른 국면과 겹치지 않는다(S4 파밍 · 튜토리얼 마침 뒤에만 선다).
        /// </summary>
        public static Phase Resolve(bool urgeUnlockRobotB, bool urgeChallenge, bool urgeModule, bool boardViewOpen,
            bool urgeEnterBoard, bool urgeBuildMode, bool ghostWaiting, bool inBuildMode)
            => Resolve(false, urgeUnlockRobotB, urgeChallenge, urgeModule, boardViewOpen,
                       urgeEnterBoard, urgeBuildMode, ghostWaiting, inBuildMode);

        /// <summary>
        /// #182 — 맨 앞에 「마운트 강화」 국면을 얹은 판. 해금 막과 겹치지 않는다(해금 **뒤**에만 선다).
        /// 조립 화면이 닫혔으면 「들어가라」(전투 화면), 열렸으면 「마운트를 탭하라」(보드 화면).
        /// ⚠️ 부르는 쪽이 「A 판을 보고 있다」를 같이 넣는다 — B 판에서는 안 잡는다(구멍이 A 판에 있다).
        /// </summary>
        public static Phase Resolve(bool urgeEnhanceMount, bool urgeUnlockRobotB, bool urgeChallenge, bool urgeModule,
            bool boardViewOpen, bool urgeEnterBoard, bool urgeBuildMode, bool ghostWaiting, bool inBuildMode)
        {
            if (urgeEnhanceMount) return boardViewOpen ? Phase.EnhanceMountBoard : Phase.EnhanceMount;
            if (urgeUnlockRobotB) return Phase.UnlockRobotB;

            // ⚠️ **T3 를 먼저 묻는다.** 그 국면은 앞 셋이 전부 끝난 **뒤**에만 서고
            //    (튜토리얼 전용 스테이지를 마쳐야 켜진다), 전투 화면의 일이라 겹치지 않는다.
            if (urgeChallenge) return Phase.Challenge;

            // T4 는 S1 을 깬 **뒤**(T3 가 끝난 뒤)에만 선다 — T3 와 겹치지 않는다.
            if (urgeModule) return boardViewOpen ? Phase.PlaceModule : Phase.ModuleEnterBoard;

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
                    // **「보드만 열기」**로 바뀐다. 기초 가공소를 켜 두면 **엉뚱한 것을 놓으라고
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

                case Phase.Explosive:
                    // #190 ① — T4 ① 과 같게(「▼ 조립」 하나 · 「▲ 전투로」 예외).
                    return control == Control.EnterBoard || control == Control.ExitBoard;

                case Phase.ExplosiveBoard:
                    // #190 ② — 노드 · 벨트 요소 단추는 열되 보드가 그 걸음 것만 남긴다(`ExplosiveTutorialRule.AllowsNode`).
                    return control == Control.PaletteOther || control == Control.BeltElement
                           || control == Control.ModeToggle || control == Control.ExitBoard
                           || control == Control.Zoom || control == Control.MiniMap;

                case Phase.Merge:
                    // #189 — 합체 하나만(T3 「도전 하나만」과 같은 결 · 「▲ 전투로」 예외).
                    return control == Control.Merge || control == Control.ExitBoard;

                case Phase.EnhanceMount:
                    // #182 ① — T4 ① 과 같게 잠근다(「▼ 조립」 하나 · 「▲ 전투로」 예외).
                    return control == Control.EnterBoard || control == Control.ExitBoard;

                case Phase.EnhanceMountBoard:
                    // #182 ② — 팔레트(노드 · 모듈 · 벨트 요소) · 지우기는 닫는다(「지금 할 일」은 마운트 탭 하나).
                    //    모드 단추는 연다 — 이동 모드 탭으로도 마운트 팝오버가 열리고(#148), 바꿔 버렸으면 돌아올 길이 있어야 한다.
                    return control == Control.ModeToggle || control == Control.ExitBoard
                           || control == Control.Zoom || control == Control.MiniMap;

                case Phase.UnlockRobotB:
                    // #173 — 해금 단추 하나만(T3 「도전 하나만」과 같은 결 · 「▲ 전투로」는 예외).
                    return control == Control.UnlockRobotB || control == Control.ExitBoard;
                case Phase.Challenge:
                    // ⚠️⚠️ **기획서 2장 그대로 잠근다**(2026-10-14 사용자 확정 #115 · 후보 ⓒ).
                    //
                    // 「지금 눌러야 할 버튼만 빛나게 하고 다른 입력은 잠근다」 —
                    // 그래서 **「도전」 하나만** 열고 「조립」·「스테이지」는 닫는다.
                    //
                    // ⚠️ **「▲ 전투로」는 예외다**(2026-09-16 사용자 지시 · 2장에 명시).
                    //    T3 중에 조립 화면에 **들어갈** 길은 막히지만, 이미 들어가 있다면
                    //    **나올** 길은 있어야 한다 — 09-15·09-16 에 두 번 보고된 「갇힘」이
                    //    그 자리다. 들어갈 수도 나올 수도 없으면 멈춘 화면이 된다.
                    return control == Control.EnterChallenge || control == Control.ExitBoard;

                case Phase.ModuleEnterBoard:
                    // ✅ **T3 와 같게 잠근다**(2026-10-15 사용자 확정 #138) — 「도전」·「스테이지」를
                    //    닫고 가리키는 단추(「▼ 조립」) 하나만 연다. 「▲ 전투로」는 예외(2장).
                    return control == Control.EnterBoard || control == Control.ExitBoard;

                case Phase.PlaceModule:
                    // 「모듈」 탭 · M/R 단추 · 대상 노드(보드 탭)만 연다. 노드·벨트 팔레트와 지우기는 닫는다 —
                    // 붙이라고 해 놓고 다른 것을 놓을 수 있으면 「지금 할 일」이 둘이 된다.
                    // ⚠️ 모드 단추는 연다 — 이동 모드로 바꿔 버렸으면 돌아올 길이 있어야 한다(09-11 막다른 국면).
                    // ⚠️ 「▲ 전투로」는 연다(09-16 사용자 지시 · 나갈 길).
                    return control == Control.ModuleTab || control == Control.Module
                           || control == Control.ModeToggle || control == Control.ExitBoard
                           || control == Control.Zoom || control == Control.MiniMap;

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
        public static bool AllowsBoardTap =>
            Current == Phase.None || Current == Phase.PlaceNode || Current == Phase.PlaceModule
            || Current == Phase.EnhanceMountBoard || Current == Phase.ExplosiveBoard;

        /// <summary>
        /// **팔레트 노드 단추를 잠그는가**(사용자 확정 #183 · 2026-10-19) — 튜토리얼 전용 판(T1) 동안 **전부.**
        /// 고스트 칸은 벨트 드래그로만 채운다. 🗑️ 구 「기초 가공소 하나만 열기」(`PaletteMunitions`) 폐기.
        /// 전용 판 밖에서는 국면 규칙(`PaletteOther`)이 정한다. 모듈 · 벨트 요소 단추는 이 규칙 밖(종전대로).
        /// </summary>
        public static bool LocksPaletteNodes(bool isTutorialStage) =>
            isTutorialStage || !Allows(Control.PaletteOther);

        /// <summary>국면을 직접 주고 묻는다(시험용).</summary>
        public static bool LocksPaletteNodes(Phase phase, bool isTutorialStage) =>
            isTutorialStage || !Allows(phase, Control.PaletteOther);
    }
}
