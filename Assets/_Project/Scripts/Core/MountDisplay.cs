using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 마운트 표시의 규칙 — **자리 · 슬롯 · 채움 · 점멸**
    /// (2026-09-14 신설 · 플랜 §71-41 → **§72-5 사용자 확정으로 개정**).
    ///
    /// **왜 있는가.** 화면에 있던 것은 결합부 그림(`mount_port`)과 「마운트」 글자뿐이고,
    /// UI 문서 12-4 의 **적재 그리드는 코드에 없었다**. 그래서 마운트가 **무엇을 얼마나 들고
    /// 있는지**가 조립 화면에서 안 보였고, 12-1 의 빨간 점멸은 그 대신 **포트 마커**에
    /// 걸려 있었다(「자산이 오면 옮길 자리」라고 코드에 적혀 있던 자리다).
    ///
    /// ⚠️ **1슬롯 = 보드 한 칸이다**(§72-5 확정). 첫 판은 마운트 그림 위에 **작은 그리드**를
    /// 얹었고 칸 크기·틈·띄움을 가정 넷으로 두었는데, 그 값들은 **폐기됐다** —
    /// 슬롯이 보드 칸과 같은 크기면 **재는 자가 화면에 이미 있다**(옆 칸이 눈금이다).
    ///
    /// ⚠️ **§72-6 에서 B 가 실루엣 안으로 들어왔다.** 구판은 A·B 모두 바깥 열이었는데,
    /// B 는 양쪽 어깨 바깥이라 **한 화면에 둘 다 못 넣었다.** 이제 B 는 머리 옆 **빈 열**
    /// (x3 · x8 · y10~13)을 쓴다 — 격자 안이지만 **파츠가 아니라 노드를 못 놓는 자리**이며,
    /// 맨 윗줄 y13 은 §72-6 이 그 목적으로 신설한 줄이다.
    ///
    /// ⚠️ **A 는 여전히 바깥이다**(x−1 · y5~8). 팔R 안쪽에는 빈 열이 없다.
    /// </summary>
    public static class MountDisplay
    {
        /// <summary>한 포트가 드러내는 슬롯 수 — **넷**(§72-5). 세로로 넷이 쌓인다.</summary>
        public const int SlotsPerPort = 4;

        /// <summary>
        /// 슬롯 수 — A 4 · B 8 (전투 문서 · <see cref="MountLoad.SlotsRobotA"/> 와 같은 값).
        /// ⚠️ 여기서 새로 정하지 않는다 — 코어가 이미 든 값을 가리킨다.
        /// </summary>
        public static int SlotsOf(MountOwner owner) =>
            owner == MountOwner.RobotB ? MountLoad.SlotsRobotB : MountLoad.SlotsRobotA;

        /// <summary>
        /// 그 로봇의 슬롯 묶음이 시작하는 행 — **A 는 y5 · B 는 y10**(§72-6 확정).
        ///
        /// A 는 팔R(y 4~8) **바깥**, B 는 머리 옆 **빈 열**(x3 · x8)이다.
        /// B 는 y10~13 이며 맨 위 y13 은 §72-6 이 신설한 **마운트 전용 줄**이다.
        /// </summary>
        public static int SlotRowStart(MountOwner owner) => owner == MountOwner.RobotB ? 10 : 5;

        /// <summary>
        /// 이 묶음이 맡아 **보여 주는** 슬롯의 첫 번호 — **왼쪽이 0~3 · 오른쪽이 4~7**.
        ///
        /// ⚠️ **표시 순서일 뿐 분배가 아니다**(§72-5·§72-6). B 는 어깨가 둘인데 적재는 한 벌이라,
        /// 두 자리가 **같은 여덟을 나눠 비추는 것**이지 두 몫으로 나뉜 것이 아니다.
        /// 나뉜 것처럼 그리면 **없는 분배를 지어내는 것**이 된다.
        ///
        /// ⚠️ **면이 아니라 자리로 가른다**(§72-6 에서 바뀐 점). 구판은 「동면이면 4」였는데
        /// B 포트가 어깨 **안쪽**으로 옮겨지며 왼쪽 묶음이 동면이 됐다 — 면으로 가르면
        /// **왼쪽이 4~7 을 비춘다.** 화면에서 읽는 것은 면이 아니라 **왼쪽·오른쪽**이다.
        /// </summary>
        public static int FirstSlotOf(int slotColumn) =>
            slotColumn * 2 < PartLayout.Columns ? 0 : SlotsPerPort;

        /// <summary>
        /// 슬롯 묶음이 서는 열 — **포트가 바라보는 쪽으로 한 칸**이다.
        ///
        /// A 는 팔R 서쪽 바깥이라 **−1**(격자 밖 · 오류가 아니다), B 는 어깨 안쪽 면이라
        /// **3 · 8**(격자 안의 빈 열)이 나온다.
        /// </summary>
        public static int SlotColumn(Vector2Int portCell, PortFace face) =>
            face == PortFace.East ? portCell.x + 1 : portCell.x - 1;

        /// <summary>
        /// 묶음 안 <paramref name="i"/>번째(0~3) 칸. **아래에서 위로 쌓는다** —
        /// 슬롯 번호가 커질수록 위다(채움이 아래에서 위로 차는 것과 같은 방향).
        /// </summary>
        public static Vector2Int SlotCell(Vector2Int portCell, PortFace face, MountOwner owner, int i) =>
            new Vector2Int(SlotColumn(portCell, face), SlotRowStart(owner) + i);

        /// <summary>
        /// 마운트 **그림**이 서는 칸 — 묶음에 붙는 한 칸이다(자리는 구현 재량 · §72-6).
        ///
        /// **A** 는 묶음(x−1 · y5~8)보다 **한 칸 더 바깥**(x−2)이다. 팔R 이 안쪽을 막고 있어
        /// 옆으로는 그쪽밖에 없고, 위아래는 다른 것과 부딪힌다.
        ///
        /// **B** 는 묶음(x3 · x8 · y10~13)의 **맨 윗 칸 옆**이다 — 즉 마운트 전용 줄 y13 의
        /// x2 · x9 다. 어깨(x0~2 · x9~11)가 y10~11 을 차지해 그 아래로는 빈 칸이 없고,
        /// **y13 은 파츠가 없어** 비어 있다.
        ///
        /// ⚠️ **그래서 세로 여유는 필요 없다**(§72-6) — B 의 그림까지 격자 안에 든다.
        /// 가로 여유 두 칸은 **A 때문에** 남는다.
        /// </summary>
        public static Vector2Int BodyCell(Vector2Int portCell, PortFace face, MountOwner owner)
        {
            int slotColumn = SlotColumn(portCell, face);
            bool left = slotColumn * 2 < PartLayout.Columns;
            int column = left ? slotColumn - 1 : slotColumn + 1;

            // A 는 묶음 세로 가운데(5~8 → 6) · B 는 맨 위(10~13 → 13).
            int row = owner == MountOwner.RobotB
                ? SlotRowStart(owner) + SlotsPerPort - 1
                : SlotRowStart(owner) + 1;

            return new Vector2Int(column, row);
        }

        /// <summary>
        /// 한 슬롯의 채움 비율. 분모는 **스택 상한**이다(확정 10 · `260901_V03`).
        ///
        /// ⚠️ **상한이 0이면 0을 돌려준다** — 나누지 않는다. 상한이 안 들어온 판에서
        /// 무한대가 나오면 칸이 통째로 차 보여 **재고가 없는데 가득 찬 그리드**가 된다.
        /// </summary>
        public static float FillRatio(float amount, float stackLimit)
        {
            if (stackLimit <= 0f) return 0f;
            return Mathf.Clamp01(amount / stackLimit);
        }

        /// <summary>
        /// 이 품목의 색이 쓸 흐름 갈래. 그리드 색을 **벨트와 같은 표**에서 가져오려는 것이다 —
        /// 마운트만 따로 색을 정하면 같은 탄이 벨트 위와 마운트에서 다른 색이 된다.
        /// </summary>
        public static FlowKind FlowOf(MountItem item)
        {
            switch (item)
            {
                case MountItem.Pierce: return FlowKind.PierceAmmo;
                case MountItem.Standard: return FlowKind.StandardAmmo;
                case MountItem.Explosive: return FlowKind.ExplosiveAmmo;
                case MountItem.Drone: return FlowKind.DroneBodyParts;
                default: return FlowKind.None;
            }
        }

        /// <summary>
        /// **비어서 점멸하는가** (UI 문서 12-1). 점멸 단위는 **묶음 전체**다(§72-5) —
        /// 그림 · 슬롯 칸 · 이름표가 한 덩어리로 깜빡인다.
        ///
        /// ⚠️ 판정을 새로 짓지 않고 <see cref="SupplyStopRules.MountIsEmpty"/> 를 부른다 —
        /// 세 곳이 같은 박자여야 하고, 두 번 재면 갈린다.
        /// </summary>
        public static bool Blinks(bool hasCombat, float mountTotal) =>
            SupplyStopRules.MountIsEmpty(hasCombat, mountTotal);
    }
}
