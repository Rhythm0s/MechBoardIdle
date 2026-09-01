using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 온보딩 시작 보드 — **관통 4노드 + 빈 칸 하나**(260831_V11 정정).
    ///
    /// 시작 80, 빈 칸을 채우면 100. 노드 1개 = 1발/초이고 관통 발당 20이므로
    /// 4노드 = 80, 5노드 = 100이다(관통 라인 스펙 5라 다섯째까지 전부 일한다).
    ///
    /// **왜 데이터로 빼는가**: 종전에는 이 배치가 `GameSceneCreator` 안에 좌표 리터럴로만
    /// 있어서 「정말 80이 나오는가」를 확인할 방법이 씬을 열어 보는 것뿐이었다.
    /// 여기 두면 씬 생성기와 테스트가 **같은 것**을 읽어, 숫자가 어긋나면 배치모드에서 깨진다.
    ///
    /// 배치 원칙(종전과 같다): 빈 보드로 시작하면 출력 0 = 전투 정지라 「게임이 고장난 것」처럼
    /// 보이고, 완성된 라인을 주면 물류 보드를 열 이유가 사라진다. **한 칸만 비운다.**
    ///
    /// 지형(전부 몸통 x3~8 · y4~9 안이다 — 몸통이 생산 허브라는 11-3 결론):
    /// <code>
    ///   y=8   [빈 칸](3,8) → 벨트(4,8) W→S ↓          에너지(5,8) → 벨트(6,8) W→S
    ///   y=7   관통(3,7)    → 병합기 M1(4,7) → 코어(5,7)          벨트(6,7) N→S
    ///   y=6   관통(3,6)    → 병합기 M2(4,6) ↑           벨트(5,6) E→N ← 벨트(6,6) N→W
    ///   y=5   관통(3,5)    → 병합기 M3(4,5) ↑
    ///   y=4   관통(3,4)    → 벨트(4,4) W→N ↑
    /// </code>
    ///
    /// ⚠️ **병합기 사다리가 북향인 이유.** <c>BeltAutoOrient</c>는 병합기 출력면을
    /// 노드 → 벨트 순으로 찾고, 벨트끼리는 **N→E→S→W 순서로 처음 만난 면**을 쓴다.
    /// 남향으로 흘리려 하면 북쪽 이웃이 출력면을 가로채 라인이 통째로 끊긴다 —
    /// 실제로 그렇게 짰다가 4줄 중 1줄만 새는 것이 아니라 3줄만 이어졌다.
    /// 직선 벨트는 면이 고정이라(자동 배향 대상이 아니다) (4,8)만 남향으로 쓴다.
    ///
    /// ⚠️ **병합기가 셋인 이유.** 코어의 탄약 입구는 서쪽 한 면뿐이고 병합기 하나가 여는
    /// 입구는 셋이다. 다섯 줄을 모으려면 병합기를 물려야 한다 — 그것이 「대역을 늘리려면
    /// 병렬 경로」의 실제 모습이고, 플레이어가 여섯째 줄을 놓을 때 똑같은 문제를 만난다.
    ///
    /// ⚠️ 군수 노드는 **동쪽으로만** 낸다(포트가 West 입력 / East 출력 고정). 벨트와 달리
    /// 면을 못 돌리므로 노드는 전부 병합기·벨트의 서쪽에 선다.
    /// 에너지도 동쪽으로만 내므로 전력선은 코어를 오른쪽으로 돌아 남쪽 입구로 들어간다.
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
        public const string MuniId = "muni";
        public const string EnergyId = "ener";

        /// <summary>
        /// **비워 둔 칸 — 코어 직전 병합기 자리다**(촬영 스크립트 A구간 확정, 2026-09-01).
        ///
        /// 종전에는 다섯째 노드 자리(3,8)였다. 그러면 나머지 네 대가 계속 돌아 마운트가
        /// 저절로 차고, 플레이어가 놓는 순간 곧바로 끝나 **「쌓인다」를 한 번도 못 본다.**
        /// 여기를 비우면 코어로 가는 유일한 입구가 막혀 **출력이 0**이 되고,
        /// 놓는 순간부터 5발/초로 8초를 채운다 — 순서가 물리로 강제된다.
        ///
        /// 그리고 목표 문구와 같은 말이 된다. 스테이지 0의 목표는 「벨트를 이으면 물건이
        /// 만들어진다」인데, 병합기는 벨트 요소이므로 **놓는 것이 곧 잇는 것**이다.
        /// </summary>
        public static readonly Vector2Int EmptySlot = new Vector2Int(4, 7);

        /// <summary>빈 칸을 채우는 것 — **노드가 아니라 병합기**다.</summary>
        public static readonly Run FillsEmptySlot = Run.Merger(4, 7);

        /// <summary>
        /// 시작 노드. **관통 5대가 전부 놓여 있다** — 빈 칸은 이제 노드 자리가 아니다.
        /// 다섯 대가 다 돌아도 병합기가 없으면 코어에 닿지 못해 출력은 0이다.
        /// </summary>
        public static readonly IReadOnlyList<Slot> Nodes = new[]
        {
            new Slot(5, 7, CoreId),
            new Slot(5, 8, EnergyId),
            new Slot(3, 4, MuniId, AmmoKind.Pierce),
            new Slot(3, 5, MuniId, AmmoKind.Pierce),
            new Slot(3, 6, MuniId, AmmoKind.Pierce),
            new Slot(3, 7, MuniId, AmmoKind.Pierce),
            new Slot(3, 8, MuniId, AmmoKind.Pierce),
        };

        /// <summary>
        /// 시작 배선. **M1(4,7)이 빠져 있다** — 그 자리가 비워 둔 칸이다.
        /// </summary>
        public static readonly IReadOnlyList<Run> Belts = new[]
        {
            Run.Merger(4, 6), // M2 — 북쪽 M1 자리로
            Run.Merger(4, 5), // M3 — 북쪽 M2로

            new Run(4, 8, PortFace.West, PortFace.South), // 관통(3,8) → M1 자리
            new Run(4, 4, PortFace.West, PortFace.North),  // 관통(3,4) → M3 남쪽

            // 전력 — 에너지도 동쪽으로만 내므로 코어를 오른쪽으로 돌아 남쪽 입구로 들어간다.
            new Run(6, 8, PortFace.West, PortFace.South),
            new Run(6, 7, PortFace.North, PortFace.South),
            new Run(6, 6, PortFace.North, PortFace.West),
            new Run(5, 6, PortFace.East, PortFace.North), // → 코어 남쪽(전력)
        };
    }
}
