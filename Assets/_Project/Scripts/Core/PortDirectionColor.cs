using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// **면이 들어오는 쪽인가 나가는 쪽인가 — 색 한 벌** (2026-10-13 신설 · 사용자 확정 #96).
    ///
    /// 규칙은 두 줄이다 — **입력은 파랑, 출력은 주황.**
    ///
    /// ⚠️⚠️ **왜 한곳에 두는가.** 이 색을 쓰는 곳이 여럿이다 — 아직 안 이어진 노드의
    /// 면 화살표, 미연결 벨트의 방향 화살표, 그리고 앞으로 올 방향 묻기. 각자 제 색을
    /// 들면 **같은 뜻이 화면에서 다른 색으로** 나오고, 한쪽만 고쳐지는 날이 온다(지침 §7).
    ///
    /// 🗑️ 이 한 벌이 <c>BoardController.BeltArrowColor</c>(미연결 노랑)와
    /// <c>WiringArrowColor</c>(미색)를 대신한다. 둘은 **폐기 표기**로 남는다 —
    /// 지우지 않는 까닭은 그 색이 무엇이었는지를 읽는 사람이 알 수 있게 하려는 것이다.
    ///
    /// ⚠️ **RGB 는 가정이다**(설계 역기입 자리). UI 문서에 이 절이 없다 —
    /// 사용자가 정한 것은 「파랑 · 주황」이라는 **말**이고, 정확한 값이 서면 여기 두 줄만 바뀐다.
    /// </summary>
    public static class PortDirectionColor
    {
        /// <summary>입력 — 파랑. ⚠️ 값은 가정.</summary>
        public static readonly Color Input = new Color(0.35f, 0.70f, 1.00f, 1f);

        /// <summary>출력 — 주황. ⚠️ 값은 가정.</summary>
        public static readonly Color Output = new Color(1.00f, 0.60f, 0.20f, 1f);

        /// <summary>면이 바깥을 보는가(출력)로 고른다.</summary>
        public static Color Of(bool outward) => outward ? Output : Input;
    }
}
