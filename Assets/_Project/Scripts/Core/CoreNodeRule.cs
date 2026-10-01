using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// **코어에 걸리는 규칙 한 곳** (2026-09-30 사용자 확정 ① ② · 조립 문서 11장).
    ///
    /// 코어는 **시작 보드에 박힌 한 대**다 — 플레이어가 놓지도, 돌리지도, 옮기지도,
    /// 지우지도 못한다. 그 넷이 서로 다른 화면 자리에 흩어져 있어서 **규칙을 여기 모은다.**
    ///
    /// 📌 **순수 정적이라 EditMode 에서 돈다** — 화면을 띄우지 않고도 지킬 수 있다.
    /// 종전에는 회전 차단이 `OnGUI` 안의 지역 변수라 **시험이 닿지 못했다.**
    ///
    /// ⚠️ **여기 없는 문 둘** — 「팔레트에 안 보인다」는
    /// <see cref="PaletteCategories.Shows"/>, 「판에 한 대만」은 <c>BoardGrid.TryPlace</c> 다.
    /// 그 둘은 각자의 자리에서 이미 묻고 있고, 묻는 자리를 옮기면 답이 둘이 된다(지침 §7).
    /// </summary>
    public static class CoreNodeRule
    {
        /// <summary>이 정의가 코어인가. 정의가 없으면 **코어가 아니다**(없는 것을 단정하지 않는다).</summary>
        public static bool IsCore(NodeDefinition def) =>
            def != null && def.type == NodeType.Core;

        /// <summary>이 노드가 코어인가.</summary>
        public static bool IsCore(NodeInstance node) =>
            node != null && IsCore(node.Definition);

        /// <summary>
        /// **돌릴 수 있는가.** 코어는 못 돌린다 — 자리도 방향도 플레이어의 것이 아니다.
        ///
        /// ⚠️ 화면은 이 값이 거짓이면 **버튼을 아예 안 그린다.** 그려 두고 눌러도 아무 일이
        /// 없게 하면 「고장」으로 읽힌다.
        ///
        /// ⚠️ **노드가 없으면 참이다** — 돌릴 것이 없는 자리에 「못 돌린다」고 답하면
        /// 부르는 쪽이 코어와 빈 칸을 못 가린다.
        /// </summary>
        public static bool CanRotate(NodeInstance node) => !IsCore(node);

        /// <summary>
        /// **지울 수 있는가.** 코어는 못 지운다 (2026-09-15 사용자 확정 · 육안 ⑧).
        ///
        /// 코어가 없으면 아무것도 못 만들고, 만들 것이 없으니 다시 놓지도 못한다 —
        /// **되돌릴 수 없는 상태**가 되는 유일한 칸이라 규칙이 따로 있다.
        /// </summary>
        public static bool CanRemove(NodeInstance node) => !IsCore(node);

        /// <summary>
        /// **저장에서 돌아온 판의 코어가 설 자리.** 저장이 뭐라 적었든 시작 보드 값이다.
        ///
        /// ⚠️ 복원은 시작 배치를 **아예 안 깔고** 저장된 판을 그대로 세운다. 그래서
        /// 코어를 **먼저** 이 자리에 세워 두고, 저장 쪽 코어는 「판에 한 대」 규칙이
        /// 거절하게 한다 — **막는 규칙을 새로 만들지 않고 있는 규칙을 쓴다.**
        /// </summary>
        public static Vector2Int FixedCell => StartingBoard.CoreCell;
    }
}
