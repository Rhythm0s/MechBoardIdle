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
    /// ⚠️ **마운트는 실루엣 바깥에 선다** — 보드 없는 소비 파츠다(조립 기획서 6장).
    /// 그래서 격자 칸이 아니라 **바깥 열**을 쓴다. 격자에 넣으면 노드를 놓을 수 있는 자리가 된다.
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
        /// 그 로봇의 슬롯 묶음이 시작하는 행 — **A 는 y5 · B 는 y9**(§72-5 확정).
        ///
        /// A 는 팔R(y 4~8) 옆, B 는 어깨(y 9~11) 옆이다. 둘이 **세로로 맞닿아**
        /// 바깥 열 하나를 y5~12 로 채운다 — 그래서 그림 칸은 그 열에 못 들어간다.
        /// </summary>
        public static int SlotRowStart(MountOwner owner) => owner == MountOwner.RobotB ? 9 : 5;

        /// <summary>
        /// 이 포트가 맡아 **보여 주는** 슬롯의 첫 번호.
        ///
        /// ⚠️ **표시 순서일 뿐 분배가 아니다**(§72-5). B 는 어깨가 둘인데 적재는 한 벌이라,
        /// 왼쪽이 0~3 · 오른쪽이 4~7 을 비추는 것이지 **두 몫으로 나뉜 것이 아니다.**
        /// 나뉜 것처럼 그리면 **없는 분배를 지어내는 것**이 된다.
        /// </summary>
        public static int FirstSlotOf(PortFace face) => face == PortFace.East ? SlotsPerPort : 0;

        /// <summary>
        /// 슬롯 묶음이 서는 **바깥 열** — 포트 면 쪽으로 격자 한 칸 밖이다.
        ///
        /// ⚠️ **격자 밖의 x 를 일부러 돌려준다.** 서면은 −1, 동면은 12 가 나오며
        /// 이것은 오류가 아니라 **실루엣 바깥**이라는 뜻이다.
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
        /// 마운트 **그림**이 서는 칸 — 묶음에 붙는 **바깥 한 칸**이다(자리는 구현 재량 · §72-5).
        ///
        /// ⚠️ **묶음 위아래로는 못 둔다.** A(y5~8)와 B(y9~12)가 같은 열에서 맞닿아 있어
        /// 위든 아래든 **다른 묶음의 슬롯과 겹친다.** 그래서 한 칸 더 바깥 열로 뺀다 —
        /// 스크롤 여유가 **두 칸**이어야 하는 이유가 이것이다.
        /// </summary>
        public static Vector2Int BodyCell(Vector2Int portCell, PortFace face, MountOwner owner)
        {
            int column = face == PortFace.East ? portCell.x + 2 : portCell.x - 2;
            // 묶음 넷의 세로 가운데에 붙인다(5~8 → 6 · 9~12 → 10).
            return new Vector2Int(column, SlotRowStart(owner) + 1);
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
