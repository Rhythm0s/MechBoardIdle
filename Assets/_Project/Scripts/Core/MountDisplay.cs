using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 마운트 표시의 규칙 — **자리 · 슬롯 · 채움 · 점멸** (2026-09-14 신설 · 플랜 §71-41).
    ///
    /// **왜 신설하는가.** 화면에 있던 것은 결합부 그림(`mount_port`)과 「마운트」 글자뿐이고,
    /// UI 문서 12-4 의 **적재 그리드는 코드에 없었다**. 그래서 마운트가 **무엇을 얼마나 들고
    /// 있는지**가 조립 화면에서 안 보였고, 12-1 의 빨간 점멸은 그 대신 **포트 마커**에
    /// 걸려 있었다(「자산이 오면 옮길 자리」라고 코드에 적혀 있던 자리다).
    ///
    /// ⚠️ **마운트는 실루엣 바깥에 선다** — 보드 없는 소비 파츠다(조립 기획서 6장).
    /// 그래서 격자 칸이 아니라 **포트 면 바깥의 가상 칸**을 쓴다. 격자에 넣으면
    /// 노드를 놓을 수 있는 자리가 되어 버린다.
    ///
    /// ⚠️ **값은 전부 가정이다**(칸 크기·간격·채움 분모). 설계가 UI 문서 12-4 에 역기입한다.
    /// </summary>
    public static class MountDisplay
    {
        /// <summary>
        /// 슬롯 수 — A 4 · B 8 (전투 문서 · <see cref="MountLoad.SlotsRobotA"/> 와 같은 값).
        /// ⚠️ 여기서 새로 정하지 않는다 — 코어가 이미 든 값을 가리킨다.
        /// </summary>
        public static int SlotsOf(MountOwner owner) =>
            owner == MountOwner.RobotB ? MountLoad.SlotsRobotB : MountLoad.SlotsRobotA;

        /// <summary>
        /// 줄 수 — A 한 줄 · B **두 줄**(§71-41). 여덟을 한 줄로 늘어놓으면 마운트 그림보다
        /// 그리드가 길어져 **무엇에 딸린 표시인지가 안 읽힌다.**
        /// </summary>
        public static int RowsOf(MountOwner owner) => owner == MountOwner.RobotB ? 2 : 1;

        /// <summary>한 줄에 몇 칸인가.</summary>
        public static int ColumnsOf(MountOwner owner)
        {
            int rows = RowsOf(owner);
            return rows <= 0 ? SlotsOf(owner) : Mathf.CeilToInt(SlotsOf(owner) / (float)rows);
        }

        /// <summary>
        /// 마운트 그림이 서는 **가상 칸** — 포트 칸에서 포트 면 쪽으로 한 칸 바깥이다.
        ///
        /// ⚠️ **격자 밖의 좌표를 일부러 돌려준다.** A 는 `(0,6)` 서면이라 `(-1,6)` 이 나오고,
        /// 이것은 오류가 아니라 **실루엣 바깥**이라는 뜻이다 — 부르는 쪽은 이 좌표를
        /// 격자 판정에 쓰지 않고 **그리는 자리로만** 쓴다.
        /// </summary>
        public static Vector2Int VirtualCell(Vector2Int portCell, PortFace face)
        {
            switch (face)
            {
                case PortFace.West: return portCell + new Vector2Int(-1, 0);
                case PortFace.East: return portCell + new Vector2Int(1, 0);
                case PortFace.North: return portCell + new Vector2Int(0, 1);
                default: return portCell + new Vector2Int(0, -1);
            }
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
        /// **비어서 점멸하는가** (UI 문서 12-1).
        ///
        /// ⚠️ 판정을 새로 짓지 않고 <see cref="SupplyStopRules.MountIsEmpty"/> 를 부른다 —
        /// 이름표·그림·그리드가 **같은 박자로** 깜빡여야 하고, 두 번 재면 갈린다.
        /// </summary>
        public static bool Blinks(bool hasCombat, float mountTotal) =>
            SupplyStopRules.MountIsEmpty(hasCombat, mountTotal);
    }
}
