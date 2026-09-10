using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 온보딩 시작 보드 — **표준탄 3단 단일 라인 + 빈 칸 하나** (2026-09-05 재작성 · `260904_W03` 1-1).
    ///
    /// **왜 다시 짰는가.** 코어의 탄약 입력이 폐기되면서(W03 1장) 종전 구조가 통째로 무너졌다.
    /// 그 구조는 「군수 다섯 대 → 병합기 사다리 → 코어」였고, **코어가 라인의 끝**이라는
    /// 전제 위에 서 있었다. 이제 코어는 시작이고 도착지는 마운트 고정 포트다.
    ///
    /// **왜 표준탄인가.** 관통탄은 복합 군수 소관이고 표준탄을 먹는다. 그걸로 시작 보드를
    /// 짜면 튜토리얼에서 복합 군수까지 가르쳐야 하는데, 스테이지의 학습 목표는
    /// 「벨트를 이으면 물건이 만들어진다」 하나다. **표준탄은 기초 군수만으로 완성되는
    /// 유일한 탄이라** 3단으로 끝난다.
    ///
    /// 지형 — 가공과 군수는 서쪽에서 받아 동쪽으로 내므로 나란히 붙이면 벨트 없이 직결된다.
    /// **코어만 다르다**(남으로 받아 북으로 낸다). 마운트는 팔R 바깥면(화면 왼쪽)이라 라인이 돌아 나간다.
    /// <code>
    ///   y=6   마운트◀(0,6)←(1,6)←(2,6)←(3,6)←(4,6)←(5,6)←(6,6)←(7,6)   ← 서향 운반로
    ///   y=5   벨트(4,5)  가공(5,5) → [빈 칸](6,5) → 벨트(7,5)↑
    ///   y=4   코어(4,4)↑
    ///   y=3   에너지(3,3) → 벨트(4,3)↑ → 코어 남쪽 전력
    /// </code>
    ///
    /// ⚠️ **코어를 무엇과도 나란히 붙일 수 없다.** 남으로 받아 북으로 내므로, 동쪽 이웃에게
    /// 아무것도 안 주고 서쪽 이웃에게서 아무것도 안 받는다. 위아래로 벨트를 물려야 한다.
    /// 2026-09-05 첫 배선이 이것을 놓쳐 두 번 끊겨 있었다 —
    /// 처음에는 전력이 코어를 스쳐 지나가 **발전이 0**이었고, 고친 뒤에도 가공을 동쪽에
    /// 붙여 두어 **코어 에너지가 갈 곳이 없었다.** 둘 다 「노드는 서→동」이라는
    /// 습관에서 나온 같은 실수다.
    ///
    /// 그 상태에서도 <see cref="LogisticsReach"/>의 도달 판정은 통과한다 — 군수에서 마운트까지는
    /// 이어져 있기 때문이다. 도달은 「무엇이 오는가」를 안 보므로, 라인이 실제로 도는지는
    /// **마운트에 물건이 닿는지**로만 알 수 있다(`MountDeliveryTests`).
    ///
    /// ⚠️ **병합기가 사라졌다.** 단일 라인이면 합칠 갈래가 없어 병합기가 논다.
    /// 종전에 셋이었던 근거(「코어의 탄약 입구가 서쪽 한 면뿐」)도 그 입구와 함께 없어졌다.
    ///
    /// ⚠️ **노드 수와 전투력 값은 여기서 정하지 않는다** (`260904_W04` 4장).
    /// 4단 체인 실측 전이며, 밸런스 문서의 100과 80은 이미 재산출 대상에 올라 있다.
    /// </summary>
    public static class StartingBoard
    {
        /// <summary>시작 배치 노드 하나. <c>nodeId</c>는 `Node_{id}.asset`의 id다.</summary>
        public struct Slot
        {
            public Vector2Int cell;
            public string nodeId;
            public AmmoKind ammo;

            public Slot(int x, int y, string nodeId, AmmoKind ammo = AmmoKind.Pierce)
            {
                cell = new Vector2Int(x, y);
                this.nodeId = nodeId;
                this.ammo = ammo;
            }
        }

        /// <summary>시작 배선 하나. 병합기면 면은 이웃에서 다시 잡히므로 무시된다.</summary>
        public struct Run
        {
            public Vector2Int cell;
            public PortFace inFace;
            public PortFace outFace;
            public bool merger;

            public Run(int x, int y, PortFace inFace, PortFace outFace, bool merger = false)
            {
                cell = new Vector2Int(x, y);
                this.inFace = inFace;
                this.outFace = outFace;
                this.merger = merger;
            }

            public static Run Merger(int x, int y) =>
                new Run(x, y, PortFace.West, PortFace.East, merger: true);
        }

        /// <summary>
        /// 병합기가 받는 면 — **출력면을 뺀 나머지 셋** (2026-09-11).
        ///
        /// **병합기의 뜻이 그것이다** — 어느 쪽에서 와도 받아 한 쪽으로 낸다.
        /// 종전에는 놓는 쪽마다 면을 따로 정해서, 입력면 하나만 열린 병합기가
        /// **옆에서 오는 줄을 거절했다**(2026-09-11 실측 — 네 줄 중 셋이 안 이어졌다).
        ///
        /// ⚠️ **자기 출력면으로는 안 받는다.** 받으면 자기가 낸 것을 자기가 도로 먹는다.
        /// </summary>
        public static PortFace[] MergerInFaces(PortFace outFace)
        {
            var all = new[] { PortFace.North, PortFace.East, PortFace.South, PortFace.West };
            var list = new List<PortFace>(3);
            foreach (PortFace f in all)
                if (f != outFace) list.Add(f);
            return list.ToArray();
        }

        public const string CoreId = "core";
        public const string ProcId = "proc";
        public const string MuniId = "muni";
        public const string EnergyId = "ener";

        // ────────────────────────────────────────────────────────────────────
        //  네 줄 배치 (2026-09-11 · `260911_W01` 2장 값 3)
        //
        //  코어 출력면이 넷이 되면서 **한 대가 네 방향으로 라인을 세운다.**
        //  라인 하나 = 가공 1 + 군수 1 = 표준탄 1발/초 → 네 줄 = 4발/초 = 도달 40.
        //
        //  ⚠️ **합류는 병합기로 한다.** 군수 산출이 넷 다 표준탄이라 섞여도 같은 것이다 —
        //  분류기가 필요한 자리는 여기 없다(W01 2-4: 분류기는 S3 수업).
        //
        //  ⚠️ **코어 에너지 줄과 표준탄 줄은 절대 안 섞는다.** 가공은 코어 에너지만 먹으므로
        //  표준탄이 흘러든 벨트가 가공에 닿으면 그 라인이 통째로 선다.
        //
        //          x  0   1   2   3   4   5   6   7   8
        //      y=9              ·   ·  벨→ 가공 군수 벨↓      ← 북 줄
        //      y=8              ·  벨↓ 코어 가공 군수 병합↓   ← 동 줄 · 코어
        //      y=7              ·  벨↓ 벨→ 가공 군수 병합↓   ← 남 줄
        //      y=6  마운트◀벨 ← 벨↑  벨→ 가공 군수 벨↓  벨↓   ← 서 줄
        //      y=5      ·  벨↑ 벨← 벨← 벨← 벨← 병합← 벨←     ← 운반로(서향)
        //
        //  운반로는 **y=5** 다. y=6 을 쓰면 서 줄의 가공·군수가 설 자리가 없어진다.
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// **비워 둔 칸 — 기초 군수 자리다** (2026-09-05 · 네 줄에서도 그대로).
        ///
        /// 코어와 가공은 놓여 있어 부품까지는 만들어지는데 **그것을 탄으로 바꿀 노드가 없다.**
        /// 놓는 순간 그 줄이 이어져 물건이 흐른다 — **수업은 연결성 하나**다(`260911_W01` 2-3).
        ///
        /// ⚠️ **네 줄 중 하나만 비운다.** 넷을 다 비우면 「아무것도 안 돈다」가 되어
        /// 무엇을 놓아야 하는지가 안 보인다.
        /// </summary>
        public static readonly Vector2Int EmptySlot = new Vector2Int(7, 8);

        /// <summary>빈 칸을 채우는 것 — **기초 군수 노드**다.</summary>
        public static readonly Slot FillsEmptySlot = new Slot(7, 8, MuniId);

        /// <summary>
        /// 시작 노드. **동 줄의 기초 군수가 빠져 있다** — 그 자리가 비워 둔 칸이다.
        ///
        /// ⚠️ **에너지·저장 대수는 `260911_W01` 에 없다.** 값 넷은 코어 산출·출력면·네 줄·요구치
        /// 뿐이라, 에너지는 **현행 한 대**를 그대로 두고 저장은 안 놓는다 — 없는 값을 지어내지 않는다.
        /// </summary>
        public static readonly IReadOnlyList<Slot> Nodes = new[]
        {
            new Slot(5, 8, CoreId),

            // 동 줄 — 코어 동면에서 바로 받는다(벨트 0칸)
            new Slot(6, 8, ProcId),
            // (7,8) 기초 군수 = 비워 둔 칸

            // 북 줄
            new Slot(6, 9, ProcId),
            new Slot(7, 9, MuniId),

            // 남 줄
            new Slot(6, 7, ProcId),
            new Slot(7, 7, MuniId),

            // 서 줄 — 코어 서면에서 내려와 동쪽으로 되돌아 들어간다
            new Slot(5, 6, ProcId),
            new Slot(6, 6, MuniId),

            // 전력 셋 — **벨트를 안 문다**(2026-09-11 확정). 전력망은 전역이라 놓기만 하면
            // 발전한다. 종전에 코어 남면으로 물리던 배선은 **문서에 없던 것**이었다.
            // 다리 안쪽에 둔다 — 라인이 지나지 않는 자리라 서로 안 막는다.
            new Slot(3, 2, EnergyId),
            new Slot(4, 2, EnergyId),
            new Slot(5, 2, EnergyId),
        };

        /// <summary>
        /// 시작 배선. 네 줄이 각자 달려 **x=8 기둥**에서 합류하고, 운반로 y=5 를 타고 서쪽
        /// 마운트 고정 포트(0,6)로 나간다.
        ///
        /// ⚠️ **마지막 칸은 (0,6)이다** — `PartLayout.MountPorts` 의 로봇 A 포트가 거기 서면이다.
        /// </summary>
        public static readonly IReadOnlyList<Run> Belts = new[]
        {
            // ── 북 줄: 코어 북면 → 동쪽으로 꺾어 가공에 넣는다
            new Run(5, 9, PortFace.South, PortFace.East),
            new Run(8, 9, PortFace.West, PortFace.South),

            // ── 남 줄: 코어 남면 → 동쪽으로 꺾어 가공에 넣는다
            new Run(5, 7, PortFace.North, PortFace.East),

            // ── 서 줄: 코어 서면 → 내려가서 동쪽으로 되돌아 가공에 넣는다
            new Run(4, 8, PortFace.East, PortFace.South),
            new Run(4, 7, PortFace.North, PortFace.South),
            new Run(4, 6, PortFace.North, PortFace.East),
            new Run(7, 6, PortFace.West, PortFace.South),

            // ── x=8 합류 기둥.
            // ⚠️ **병합기도 출력면을 적어야 한다.** `Run.Merger` 는 서→동 고정이고
            // 「이웃에서 다시 잡는다」는 주석과 달리 **자동 배향이 그 면을 안 고쳤다** —
            // 첫 배치가 여기서 통째로 끊겼다(2026-09-11 실측 · 출력면이 East 로 남아
            // 팔L 쪽 빈 칸으로 흘렀다). 입력면은 여럿을 받으므로 하나만 적으면 된다.
            new Run(8, 8, PortFace.North, PortFace.South, merger: true),   // 동 줄(서) + 북 줄(북)
            new Run(8, 7, PortFace.North, PortFace.South, merger: true),   // 남 줄(서) + 위(북)
            new Run(8, 6, PortFace.North, PortFace.South),
            new Run(8, 5, PortFace.North, PortFace.West),

            // ── 운반로 y=5 — 서향
            new Run(7, 5, PortFace.East, PortFace.West, merger: true),   // 서 줄(북) + 기둥(동)
            new Run(6, 5, PortFace.East, PortFace.West),
            new Run(5, 5, PortFace.East, PortFace.West),
            new Run(4, 5, PortFace.East, PortFace.West),
            new Run(3, 5, PortFace.East, PortFace.West),
            new Run(2, 5, PortFace.East, PortFace.North),

            // ── 팔R 바깥면으로
            new Run(2, 6, PortFace.South, PortFace.West),
            new Run(1, 6, PortFace.East, PortFace.West),
            new Run(0, 6, PortFace.East, PortFace.West), // 서쪽 면이 마운트 고정 포트다
        };
    }
}
