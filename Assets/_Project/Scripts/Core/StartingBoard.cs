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

        /// <summary>관통 라인의 다섯째 자리 — **비워 둔 칸.** 여기를 채우면 80 → 100이 된다.</summary>
        public static readonly Vector2Int EmptySlot = new Vector2Int(3, 8);

        /// <summary>빈 칸을 채웠을 때 놓이는 것. 팔레트 기본값과 같아야 100이 나온다.</summary>
        public static readonly Slot FillsEmptySlot = new Slot(3, 8, MuniId, AmmoKind.Pierce);

        /// <summary>시작 노드. 관통 4 + 코어 + 에너지 — 빈 칸은 여기 없다.</summary>
        public static readonly IReadOnlyList<Slot> Nodes = new[]
        {
            new Slot(5, 7, CoreId),
            new Slot(5, 8, EnergyId),
            new Slot(3, 4, MuniId, AmmoKind.Pierce),
            new Slot(3, 5, MuniId, AmmoKind.Pierce),
            new Slot(3, 6, MuniId, AmmoKind.Pierce),
            new Slot(3, 7, MuniId, AmmoKind.Pierce),
        };

        /// <summary>시작 배선. 노드만 있고 벨트가 없으면 연결성 게이트에 걸려 출력이 0이다.</summary>
        public static readonly IReadOnlyList<Run> Belts = new[]
        {
            Run.Merger(4, 7), // M1 — 코어 서쪽 입구로 낸다(노드가 벨트를 이긴다)
            Run.Merger(4, 6), // M2 — 북쪽 M1으로
            Run.Merger(4, 5), // M3 — 북쪽 M2로

            new Run(4, 8, PortFace.West, PortFace.South), // 빈 칸 → M1 북쪽. 직선이라 남향이 된다
            new Run(4, 4, PortFace.West, PortFace.North),  // 관통(3,4) → M3 남쪽

            // 전력 — 에너지도 동쪽으로만 내므로 코어를 오른쪽으로 돌아 남쪽 입구로 들어간다.
            new Run(6, 8, PortFace.West, PortFace.South),
            new Run(6, 7, PortFace.North, PortFace.South),
            new Run(6, 6, PortFace.North, PortFace.West),
            new Run(5, 6, PortFace.East, PortFace.North), // → 코어 남쪽(전력)
        };
    }
}
