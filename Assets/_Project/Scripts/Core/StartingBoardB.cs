using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// **로봇 B 의 시작 보드** (2026-09-16 신설 · 사용자 확정 · 플랜 §74-16 ②).
    ///
    /// ⚠️⚠️ **A 와 달리 「완성본」으로 시작한다.** A 의 판은 한 칸을 비워 두고
    /// 튜토리얼이 그것을 가르치는 판이다(<see cref="StartingBoard.EmptySlot"/>).
    /// B 는 **튜토리얼 대상이 아니므로**(사용자 확정 — 「튜토리얼은 A만」) 비워 둘 칸이
    /// 없고, 처음부터 흐르는 줄이어야 한다.
    ///
    /// ⚠️ **배치는 가정이다**(설계 사후 역기입 자리). 확정으로 받은 것은 **줄의 모양**이다 —
    ///   · 코어 → 가공(발전재료) → 가공(배터리)
    ///   · 코어 → 가공(부품) → 기초 가공소(드론 몸체) → 복합 가공소(누적 드론) → **B 포트**
    ///   · 전력 셋
    /// 어느 칸에 놓을지와 어느 면으로 물릴지는 여기서 정한 것이며, **값(산출률·소비)은
    /// 노드 자산이 든다** — 이 파일은 자리만 적는다.
    ///
    /// 📌 **왜 씬이 아니라 코드인가.** 씬의 `initialLayout` 은 목록이 **하나**뿐이라
    /// 판 둘을 못 담는다. A 를 코드로 옮기면 인스펙터에서 보던 것이 사라지므로,
    /// 늘어난 쪽인 B 만 코드가 든다. ⚠️ 그래서 **A 와 B 의 시작 배치가 사는 곳이 다르다** —
    /// 둘을 같이 고쳐야 할 때 한쪽을 빠뜨리기 쉬운 자리다.
    /// </summary>
    public static class StartingBoardB
    {
        /// <summary>시작 배치 노드 하나 — 조합표까지 같이 적는다.</summary>
        public readonly struct Slot
        {
            public readonly Vector2Int cell;
            public readonly string nodeId;
            public readonly RecipeKind recipe;

            /// <summary>
            /// 놓인 방향(90도 단위 0~3) — 2026-09-18 신설. A 쪽 `StartingBoard.Slot` 에는
            /// 09-15 부터 있던 칸인데 **B 에는 없었다.**
            ///
            /// 📌 **왜 이제 쓰나.** 노드는 서면으로 받아 동면으로 낸다 — 산물이 늘 동쪽으로
            /// 흐른다. 그런데 둘째 드론 줄의 도착지(B 포트 (2,10))는 **판 서쪽 끝**이라,
            /// 안 돌리면 판을 가로질러 되돌아와야 한다. **180도 돌리면 동으로 먹고 서로 낸다.**
            /// </summary>
            public readonly int rotation;

            public Slot(int x, int y, string nodeId, RecipeKind recipe = RecipeKind.None,
                        int rotation = 0)
            {
                cell = new Vector2Int(x, y);
                this.nodeId = nodeId;
                this.recipe = recipe;
                this.rotation = rotation;
            }
        }

        /// <summary>복합 가공소 노드 id — `Node_munix.asset`.</summary>
        public const string ComplexId = "munix";

        /// <summary>
        /// **마운트 고정 포트로 나가는 칸** (2026-09-17 신설 · 구 「목록의 마지막」 폐기).
        /// 시험이 차례로 짚고 있었는데, 추진제 줄을 뒤에 붙이자 그 차례가 어긋났다.
        /// </summary>
        public static readonly Vector2Int MountExit = new Vector2Int(9, 10);

        /// <summary>
        /// 노드 여덟.
        ///
        /// **자리 잡는 규칙 하나** — 노드끼리 맞닿으면 벨트가 필요 없다(A 의 동 줄과 같다).
        /// 그래서 두 가공 줄은 코어의 **북면·남면**에 바로 붙이고, 벨트는 어깨까지
        /// 나가는 구간에만 쓴다.
        /// </summary>
        /// <summary>
        /// 노드 여덟.
        ///
        /// ⚠️⚠️ **줄은 서 → 동으로만 흐른다.** 노드 자산이 그렇게 생겼다 —
        /// 가공·기초 가공소는 **서면이 입력 · 동면이 출력**이고, 복합 가공소는
        /// **서면(드론 몸체) · 남면(배터리)이 입력 · 동면이 출력**이다(회전 0 기준).
        /// 그래서 두 줄을 **위아래로 깔고 동쪽 끝에서 합류**시킨다 —
        /// 배터리 줄이 아래(y7)라야 복합 가공소의 **남면**으로 들어간다.
        ///
        /// 📌 첫 판을 서쪽(어깨R)으로 끌고 갔다가 **아무것도 안 흘렀다** — 줄을 거꾸로
        ///    깐 것이다. 시험(`StartingBoardBTests`)이 그것을 잡았다.
        /// </summary>
        public static readonly IReadOnlyList<Slot> Nodes = new[]
        {
            new Slot(5, 8, StartingBoard.CoreId, RecipeKind.CoreEnergy),

            // ── 윗줄: 부품 → 드론 몸체. 코어 **동면**에서 바로 받는다(벨트 0칸).
            new Slot(6, 8, StartingBoard.ProcId, RecipeKind.BasicParts),
            new Slot(7, 8, StartingBoard.MuniId, RecipeKind.DroneBody),

            // ── 아랫줄: 발전재료 → 배터리. 코어 **남면** → 벨트 한 칸으로 꺾어 받는다.
            new Slot(6, 7, StartingBoard.ProcId, RecipeKind.PowerMaterial),
            new Slot(7, 7, StartingBoard.ProcId, RecipeKind.Battery),

            // ── 합류: 복합 가공소가 **서면으로 드론 몸체 · 남면으로 배터리**를 받아
            //    누적형 드론을 낸다. 팔L(x9~11 · y4~8) 안이다.
            new Slot(9, 8, ComplexId, RecipeKind.StackDrone),

            // ── 전력 셋 — A 와 같은 규칙(벨트를 안 문다 · 전력망은 전역). 다리 안쪽.
            new Slot(3, 2, StartingBoard.EnergyId),
            new Slot(4, 2, StartingBoard.EnergyId),
            new Slot(5, 2, StartingBoard.EnergyId),

            // ── 추진제 줄 + 부스터 둘 (2026-09-17 사용자 확정 · `260917_W03` 3장) ──
            //
            // ⚠️ **A 와 자리가 다르다.** B 는 어깨L(x≥9)이 드론 운반로라 A 의 자리를 못 쓴다 —
            //    코어의 **서면**으로 내려가 몸통 아래를 쓴다. 노드 수·조합표·부스터 대수는 같다.
            //
            //      y=8   벨(4,8)←코어(5,8) 西面
            //      y=7   벨(4,7)↓
            //      y=6   벨(4,6)→ 가공(5,6)→ 군수(6,6)→ 분류기(7,6)→ 부스터(8,6)
            //      y=5                                   벨(7,5)→ 부스터(8,5)
            new Slot(5, 6, StartingBoard.ProcId, RecipeKind.PowerMaterial),
            new Slot(6, 6, StartingBoard.MuniId, RecipeKind.Propellant),
            new Slot(8, 6, StartingBoard.BoosterId),
            new Slot(8, 5, StartingBoard.BoosterId),

            // ── 쉴드 줄 (2026-09-17 사용자 확정 · `260917_W07` 4장) ──
            //
            // ⚠️ **A 와 자리가 다르다.** A 는 코어 **서면**을 쓰는데 B 는 그 자리에 추진제 줄이
            //    이미 있다(서 줄 (4,8)~(4,6)). B 는 **북면**이 비어 있어 그쪽으로 낸다 —
            //    쓰이는 것은 같은 노드 셋(가공 1 · 기초 가공소 1 · 발생 1)이다.
            //
            //      y=9   벨(5,9) 남→동 → 가공(6,9) → 군수(7,9) 방어 재료 → 쉴드(8,9)
            //
            // 📌 **로봇 B 의 정체성이 에너지와 쉴드**라는 사용자 말이 여기 걸린다 —
            //    B 는 이제 배터리 줄(에너지)과 쉴드 줄을 다 갖는다.
            // ⚠️ 가공은 표준탄·드론 줄과 **안 나눠 쓴다**(사용자 확정 · A 와 같은 규칙).
            new Slot(6, 9, StartingBoard.ProcId, RecipeKind.BasicParts),
            new Slot(7, 9, StartingBoard.MuniId, RecipeKind.DefenseMaterial),
            new Slot(8, 9, StartingBoard.ShieldId),

            // ── 둘째 드론 줄 — **광역형** (2026-09-18 사용자 확정 · `260918_W01` 2장) ──
            //
            // 🗑️ **구 배치 폐기**(같은 날 오후) — 몸통 아래·다리에 깔고 팔L 에서 **병합기로
            //    기존 줄에 합류**시켰던 판이다. 사용자가 **비워 둔 포트 (2,10) 을 쓰는 쪽**으로
            //    닫았다. 그 포트는 판 **서쪽 끝**이라 안 돌린 노드로는 못 간다.
            //
            // ✅ **노드를 180도 돌린다** — 돌리면 **동으로 먹고 서로 낸다**(09-15 회전 코드).
            //    그러면 줄이 코어(동)에서 포트(서)로 **한 방향으로** 흐른다. 그 덕에
            //    분류기도 **하나면 된다**(코어 서면 하나가 셋으로 갈린다 — 기존 추진제 줄 ·
            //    부품 줄 · 발전재료 줄). 🗑️ 구 「분류기 둘」 폐기.
            //
            //      y=9   벨(4,9)← 분류기 북 갈래 → 변환기R(3,9) 발전재료 → 변환기R(2,9) 배터리
            //                                                              → 벨(1,9)↑
            //      y=8   분류기(4,8) → 변환기R(3,8) 부품 → 기초변환기R(2,8) 몸체 → 벨(1,8)→(0,8)↑
            //      y=10  벨(0,10)→ **복합(1,10) 광역형** → 벨(2,10)→ **포트 (2,10) 동면**
            //
            // 📌 복합만 **안 돌린다** — 서면(몸체)·남면(배터리)으로 받아 **동면**으로 내야
            //    포트가 있는 동쪽으로 나간다.
            new Slot(3, 8, StartingBoard.ProcId, RecipeKind.BasicParts, rotation: 2),
            new Slot(2, 8, StartingBoard.MuniId, RecipeKind.DroneBody, rotation: 2),
            new Slot(3, 9, StartingBoard.ProcId, RecipeKind.PowerMaterial, rotation: 2),
            new Slot(2, 9, StartingBoard.ProcId, RecipeKind.Battery, rotation: 2),
            new Slot(1, 10, ComplexId, RecipeKind.AoeDrone),
        };

        /// <summary>
        /// 벨트 여덟.
        ///
        /// ⚠️ **마지막 칸은 (9,10) 서면이다** — `PartLayout` 의 로봇 B 포트가 거기다.
        /// 어깨L 안쪽 면이라 실루엣 **안**에서 적재 칸이 선다(§72-6).
        ///
        /// 📌 **B 포트는 둘인데 하나만 쓴다**((2,10) 동면은 비워 둔다). 시작 판이
        /// 두 줄을 다 채우면 플레이어가 늘릴 자리가 없다 — A 가 코어 출력면 넷 중
        /// 둘만 쓰는 것과 같은 뜻이다.
        /// ⚠️ 어깨**L** 을 쓰는 것은 줄이 **동쪽으로** 흐르기 때문이다(위 주석).
        /// </summary>
        public static readonly IReadOnlyList<StartingBoard.Run> Belts = new[]
        {
            // 코어 남면 → 동으로 꺾어 가공(발전재료)에 넣는다
            new StartingBoard.Run(5, 7, PortFace.North, PortFace.East),

            // 윗줄(드론 몸체) → 복합 가공소 **서면**
            new StartingBoard.Run(8, 8, PortFace.West, PortFace.East),

            // 아랫줄(배터리) → 동으로 간 뒤 북으로 꺾어 복합 가공소 **남면**
            new StartingBoard.Run(8, 7, PortFace.West, PortFace.East),
            new StartingBoard.Run(9, 7, PortFace.West, PortFace.North),

            // 복합 가공소 → 어깨L 을 타고 올라가 마운트 고정 포트 (9,10) **서면**으로
            // ⚠️⚠️ **이 한 줄이 빠지자 기존 드론 줄이 통째로 끊겼다**(2026-09-18 · 오후에
            //    병합기로 바꿨다가 되돌리면서 복구를 빠뜨렸다). 하네스가 「광역 몫 1.00 ·
            //    누적형 0」으로 잡았다 — **이어진 노드 수가 하나 줄어드는 것**이 그 신호였다.
            new StartingBoard.Run(10, 8, PortFace.West, PortFace.North),
            new StartingBoard.Run(10, 9, PortFace.South, PortFace.North),
            new StartingBoard.Run(10, 10, PortFace.South, PortFace.West),
            new StartingBoard.Run(9, 10, PortFace.East, PortFace.West),

            // ── 추진제 줄 (2026-09-17) — 코어 서면에서 몸통 아래로 ──
            // 🗑️ 구 벨트 (4,8) 폐기(2026-09-18) — **분류기로 바뀌었다**(아래 둘째 드론 줄).
            //    남면 출력은 그대로라 추진제 줄은 안 흔들린다.
            new StartingBoard.Run(4, 7, PortFace.North, PortFace.South),
            new StartingBoard.Run(4, 6, PortFace.North, PortFace.East),
            StartingBoard.Run.Sorter(7, 6),
            new StartingBoard.Run(7, 5, PortFace.North, PortFace.East),

            // ── 쉴드 줄 (2026-09-17) — 코어 북면에서 동으로 ──
            new StartingBoard.Run(5, 9, PortFace.South, PortFace.East),

            // ── 둘째 드론 줄 (2026-09-18 사용자 확정 · 포트 (2,10) 로 나간다) ──
            //
            // ⚠️⚠️ **분류기는 코어 에너지를 가르는 자리다 — 드론 비율을 가르는 자리가 아니다.**
            //    누적형과 광역형의 비율은 **두 복합 노드의 생산 속도**가 정한다(사용자 확정).
            //
            // 🗑️ 구 「분류기 둘 + 병합기 하나」 폐기(같은 날 오후) — 노드를 돌리자
            //    **분류기 하나로 셋이 갈리고** 병합기가 필요 없어졌다.
            //
            //  분류기 (4,8) — 남(기존 추진제 줄 그대로) · 서(부품 줄) · 북(발전재료 줄)
            StartingBoard.Run.Sorter(4, 8),
            new StartingBoard.Run(4, 9, PortFace.South, PortFace.West),

            // 드론 몸체 — 돌린 노드 둘을 지나 서쪽으로 나와 어깨R 로 올라간다
            new StartingBoard.Run(1, 8, PortFace.East, PortFace.West),
            new StartingBoard.Run(0, 8, PortFace.East, PortFace.North),
            new StartingBoard.Run(0, 9, PortFace.South, PortFace.North),
            new StartingBoard.Run(0, 10, PortFace.South, PortFace.East),

            // 배터리 — 돌린 노드 둘을 지나 한 칸 올라가 복합 **남면**으로
            new StartingBoard.Run(1, 9, PortFace.East, PortFace.North),

            // 광역형 드론 → **B 포트 (2,10) 동면**. 어깨R 안이라 실루엣 안에서 적재 칸이 선다
            new StartingBoard.Run(2, 10, PortFace.West, PortFace.East),
        };

        /// <summary>
        /// **놓는 문 하나** — A 쪽 <see cref="StartingBoard.Apply"/> 와 같은 까닭이다
        /// (2026-09-17 신설). 조합표를 한 곳만 빠뜨려도 그 판은 조용히 반쪽이 된다.
        /// </summary>
        public static int Apply(BoardGrid grid, System.Func<string, NodeDefinition> nodeById)
        {
            if (grid == null || nodeById == null) return 0;

            int placed = 0;
            foreach (Slot slot in Nodes)
            {
                NodeDefinition def = nodeById(slot.nodeId);
                if (def == null) continue;
                if (!grid.TryPlace(slot.cell, def, out NodeInstance node)) continue;
                node.Rotation = slot.rotation;   // 2026-09-18 — 안 돌리면 둘째 드론 줄이 못 선다
                if (slot.recipe != RecipeKind.None) node.SelectRecipe(slot.recipe);
                placed++;
            }

            foreach (StartingBoard.Run run in Belts) StartingBoard.Place(grid, run);
            return placed;
        }

        /// <summary>
        /// 이 판의 **세대 표식** (2026-09-17 신설 · 사용자 결정 · `260917_V01` 6-1).
        ///
        /// ⚠️⚠️ **없어서 규칙 5 가 B 에서 안 섰다.** 저장을 뜰 때도 견줄 때도
        /// `StartingBoard.Generation`(= A 의 표식) 하나를 보고 있었으므로,
        /// **이 판을 고쳐도 표식이 안 바뀌었다.** 크기도 주인도 같으니 옛 B 저장이
        /// 새 B 시작 보드 위에 조용히 올라온다 — 09-15 에 A 에서 났던 사고 그대로다.
        ///
        /// ⚠️ **조합표를 함께 섞는다.** 이 판은 가공 노드 셋이 **같은 자산**이고
        /// 조합표만 다르다(부품 · 발전재료 · 배터리). 조합표를 안 섞으면 둘을 맞바꿔도
        /// 표식이 그대로여서, 바뀐 판 위에 옛 저장이 올라온다.
        /// </summary>
        public static string Generation
        {
            get
            {
                if (_generation != null) return _generation;

                var keys = new List<BoardGeneration.NodeKey>(Nodes.Count);
                foreach (Slot n in Nodes)
                    keys.Add(new BoardGeneration.NodeKey(n.cell, n.nodeId, n.recipe));

                _generation = BoardGeneration.Of(
                    PartLayout.Columns, PartLayout.Rows, keys, Belts);
                return _generation;
            }
        }

        private static string _generation;
    }
}
