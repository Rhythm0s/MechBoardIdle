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
    /// 지형 — 노드는 서쪽에서 받아 동쪽으로 내므로 나란히 붙이면 벨트 없이 직결된다.
    /// 마운트는 왼팔 바깥면이라 라인이 한 번 돌아 나간다.
    /// <code>
    ///   y=7   벨트(1,7)←(2,7)←(3,7)←(4,7)←(5,7)←(6,7)  ← 서향 운반로
    ///   y=6   마운트◀(0,6) 벨트(1,6)  코어(3,6)→가공(4,6)→[빈 칸](5,6)  벨트(6,6)
    ///   y=5   에너지(2,5) → 벨트(3,5)↑ → 코어 남쪽 전력
    /// </code>
    ///
    /// ⚠️ **코어는 남쪽으로만 전력을 받는다.** 에너지는 동쪽으로만 내므로 둘을 나란히
    /// 붙일 수 없고, 코어 **바로 아래 칸**에 벨트를 두어 북으로 꺾어 넣어야 한다.
    /// 그 칸에 에너지를 놓으면 출력면이 동쪽이라 코어를 스쳐 지나간다 —
    /// 2026-09-05 첫 배선이 그래서 발전이 통째로 0이었다.
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

        public const string CoreId = "core";
        public const string ProcId = "proc";
        public const string MuniId = "muni";
        public const string EnergyId = "ener";

        /// <summary>
        /// **비워 둔 칸 — 기초 군수 자리다** (2026-09-05).
        ///
        /// 코어와 가공은 놓여 있어 부품까지는 만들어지는데, **그것을 탄으로 바꿀 노드가 없다.**
        /// 그래서 마운트 도착이 0이고 출력도 0이다. 놓는 순간 라인이 이어져 물건이 흐른다.
        ///
        /// 빈 칸을 하나만 두는 원칙은 그대로다 — 빈 보드로 시작하면 「게임이 고장난 것」처럼
        /// 보이고, 완성된 라인을 주면 물류 보드를 열 이유가 사라진다.
        /// </summary>
        public static readonly Vector2Int EmptySlot = new Vector2Int(5, 6);

        /// <summary>빈 칸을 채우는 것 — **기초 군수 노드**다.</summary>
        public static readonly Slot FillsEmptySlot = new Slot(5, 6, MuniId);

        /// <summary>
        /// 시작 노드. **기초 군수가 빠져 있다** — 그 자리가 비워 둔 칸이다.
        /// </summary>
        public static readonly IReadOnlyList<Slot> Nodes = new[]
        {
            new Slot(3, 6, CoreId),
            new Slot(4, 6, ProcId),
            new Slot(2, 5, EnergyId),
        };

        /// <summary>
        /// 시작 배선. 기초 군수(5,6)의 동쪽 출구에서 받아 **북으로 돌아 서쪽으로** 흘려
        /// 왼팔 바깥면의 마운트 고정 포트로 보낸다.
        /// </summary>
        public static readonly IReadOnlyList<Run> Belts = new[]
        {
            // 운반로 — 군수 동쪽 출구 → 북 → 서향 → 남 → 마운트
            new Run(6, 6, PortFace.West, PortFace.North),
            new Run(6, 7, PortFace.South, PortFace.West),
            new Run(5, 7, PortFace.East, PortFace.West),
            new Run(4, 7, PortFace.East, PortFace.West),
            new Run(3, 7, PortFace.East, PortFace.West),
            new Run(2, 7, PortFace.East, PortFace.West),
            new Run(1, 7, PortFace.East, PortFace.South),
            new Run(1, 6, PortFace.North, PortFace.West),
            new Run(0, 6, PortFace.East, PortFace.West), // 서쪽 면이 마운트 고정 포트다

            // 전력 — 에너지(2,5) 동쪽 출구를 받아 코어(3,6) 남쪽 면으로 꺾어 올린다.
            new Run(3, 5, PortFace.West, PortFace.North),
        };
    }
}
