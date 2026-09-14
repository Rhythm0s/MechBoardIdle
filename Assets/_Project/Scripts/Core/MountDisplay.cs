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
        public static int SlotRowStart(MountOwner owner) => owner == MountOwner.RobotB ? 10 : 0;

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
        /// **로봇 A 의 묶음 열** — `x1` 고정 (2026-09-14 · 「A 도 B 처럼 노드에 붙여라」).
        ///
        /// **왜 포트에서 안 끌어오는가.** A 포트는 `(0,6)` **서면**이라 면을 따라가면
        /// `x−1` — **격자 밖**이다. 그런데 A 포트는 못 옮긴다: `StartingBoard` 의 운반로가
        /// 그 칸에서 끝나고(「마지막 칸은 (0,6)」), 물류 도달 판정이 거기 걸려 있다.
        /// **옮기면 시작 보드가 깨진다.**
        ///
        /// 그래서 **포트는 그대로 두고 표시만** 격자 안으로 넣는다 —
        /// 팔R(x0~2 · y4~8) **바로 아래**의 빈 칸(x0~2 · y0~3)이다. 다리는 x3~8 이라
        /// 그 열두 칸은 **어느 파츠도 아니고 노드도 못 놓는다.**
        /// </summary>
        public const int RobotAColumn = 1;

        /// <summary>그 로봇의 묶음이 서는 열.</summary>
        public static int GroupColumn(Vector2Int portCell, PortFace face, MountOwner owner) =>
            owner == MountOwner.RobotB ? SlotColumn(portCell, face) : RobotAColumn;

        /// <summary>
        /// 묶음 안 <paramref name="i"/>번째(0~3) 칸. **아래에서 위로 쌓는다** —
        /// 슬롯 번호가 커질수록 위다(채움이 아래에서 위로 차는 것과 같은 방향).
        /// </summary>
        public static Vector2Int SlotCell(Vector2Int portCell, PortFace face, MountOwner owner, int i) =>
            new Vector2Int(GroupColumn(portCell, face, owner), SlotRowStart(owner) + i);

        /// <summary>
        /// 묶음 네 칸의 **세로 칸 수** — 그림을 이만큼 늘린다 (2026-09-14 · §72-13).
        ///
        /// ⚠️ **별도 그림 칸은 폐기됐다.** 그림을 슬롯 묶음 **네 칸 위에 세로로 늘려**
        /// 그리고 칸마다 품목색을 틴트로 곱한다 — 그림과 적재가 **한 덩어리**가 되어
        /// 「무엇에 딸린 표시인가」가 저절로 선다.
        ///
        /// 폐기 전에는 A `(−2,6)` · B `(2,13)`·`(9,13)` 을 따로 썼고 그 때문에 가로
        /// 여유가 **두 칸** 필요했다 — 이제 **한 칸**이면 된다(문서 13칸과 맞는다).
        /// </summary>
        public static int GroupHeightCells => SlotsPerPort;

        /// <summary>
        /// 마운트 그림을 **좌우로 뒤집는가** (2026-09-14 실측 · 플랜 「B 오른쪽 flipX 확인」).
        ///
        /// **잣대는 결합부다.** 그림에는 몸에 붙는 변이 있고, 그 변이 **로봇 쪽**을 봐야 한다.
        /// 규격이 「실루엣에 붙는 변 여백 0」이라 **여백을 재면 어느 변인지 갈린다** —
        /// `mount_dronebay_b` 는 **왼쪽 0 · 오른쪽 18** 이므로 **왼쪽이 붙는 변**이다.
        ///
        /// 그래서 판단은 「**몸이 그림의 어느 쪽에 있는가**」 하나다.
        ///   · B 왼쪽(묶음 x3 · 어깨R x0~2) — 몸이 왼쪽이라 **그대로**
        ///   · B 오른쪽(묶음 x8 · 어깨L x9~11) — 몸이 오른쪽이라 **뒤집는다**
        ///   · A(묶음 x−1) — **실루엣 바깥**이라 몸이 늘 안쪽(오른쪽)이다. 항상 뒤집는다
        ///
        /// ⚠️ **A 는 규격으로 안 갈린다** — `mount_gun_a` 는 좌우 여백이 **둘 다 0**이라
        /// 붙는 변을 못 고른다. 그림을 읽어 정했다(손잡이가 왼쪽 · 총열이 오른쪽이므로,
        /// 뒤집어야 **총열이 바깥을 겨누고 결합부가 몸 쪽**이 된다). 육안에서 반대면 여기 한 줄이다.
        /// </summary>
        public static bool FlipX(Vector2Int portCell, PortFace face, MountOwner owner)
        {
            // ⚠️ **A 는 뒤집지 않는다**(2026-09-14 · 자리가 격자 안으로 옮겨졌다).
            // 종전에는 실루엣 **바깥**이라 몸이 늘 오른쪽이었는데, 이제 묶음이 팔R **바로
            // 아래**라 몸이 **위**에 있다 — 좌우로는 가를 것이 없다. 육안에서 반대면 여기다.
            if (owner != MountOwner.RobotB) return false;

            // B 는 그림이 보드의 어느 쪽에 섰는가로 갈린다.
            return SlotColumn(portCell, face) * 2 >= PartLayout.Columns;
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
