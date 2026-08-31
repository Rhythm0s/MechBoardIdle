using System.Collections.Generic;

namespace MBI.Core
{
    /// <summary>
    /// 병목 힌트 — **왜 막혔는지가 아니라 무엇을 하면 되는지**를 한 줄로 준다(260831_V02 §3 확정).
    ///
    /// 원칙 둘(설계 트랙 확정):
    ///   - 원인 설명이 아니라 **행동**을 쓴다
    ///   - **시스템은 정답을 말하지 않는다 — 방향만 준다**(지침 §3 물류 무개입)
    ///
    /// 한 번에 **하나만** 보여 준다. 넷을 한꺼번에 늘어놓으면 「무엇부터」가 사라져
    /// 힌트가 아니라 목록이 된다.
    /// </summary>
    public static class BottleneckHint
    {
        /// <summary>원인별 문구. 설계 트랙이 확정해 넘긴 원문 그대로다 — 여기서 손보지 않는다.</summary>
        public static string TextOf(ConstraintCause cause)
        {
            switch (cause)
            {
                case ConstraintCause.Blocked:
                    return "나가는 곳이 없다. 벨트를 잇거나 쓰는 노드를 늘려라";
                case ConstraintCause.NoInput:
                    return "들어오는 것이 없다. 앞 단계 노드를 확인해라";
                case ConstraintCause.Power:
                    return "전력이 모자라 느려졌다. 발전소를 늘리거나 노드를 줄여라";
                case ConstraintCause.Heat:
                    // ⚠️ 원래 문구는 「냉각 모듈을 붙여라」였다. 모듈이 영상 이후로 연기되면서
                    // **없는 물건을 가리키게 되어** 교체했다(260831_V07). 제거 도구는 이미 팔레트에
                    // 있으니 실행 가능한 안내다. 모듈이 들어오면 원래 문구로 되돌린다.
                    return "열이 올라 느려졌다. 열이 몰린 곳의 노드를 덜어내라";
                default:
                    return "";
            }
        }

        /// <summary>
        /// 지금 가장 시급한 원인 하나.
        ///
        /// 순서는 **전역 → 개별**이다. 전력·발열은 보드 전체가 함께 느려지는 것이고,
        /// 막힘·입력없음은 그 노드 하나가 선 것이라 파급이 다르다.
        /// 전역 안의 Power → Heat 순서는 기존 전역 원인 배지 규칙을 그대로 잇는다 —
        /// 두 곳이 다른 것을 가리키면 배지와 힌트가 서로 다른 말을 하게 된다.
        ///
        /// 이 우선순위는 260831_V07에서 **승인**됐다 — 전역 원인은 여러 노드를 한꺼번에
        /// 멈추므로 먼저 고쳐야 하고, 개별 원인은 한 자리만 고치면 된다.
        /// </summary>
        public static ConstraintCause MostUrgent(ConstraintCause global,
            IReadOnlyList<NodeDiagnostic> diagnostics)
        {
            if (global == ConstraintCause.Power || global == ConstraintCause.Heat) return global;

            if (diagnostics == null) return ConstraintCause.None;

            // 개별 정지는 「막힘」이 먼저다 — 앞은 만들고 있는데 갈 곳이 없는 쪽이
            // 뒤가 안 와서 노는 쪽보다 손해가 크다(만든 것이 버려진다).
            bool blocked = false, noInput = false;
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].cause == ConstraintCause.Blocked) blocked = true;
                else if (diagnostics[i].cause == ConstraintCause.NoInput) noInput = true;
            }

            if (blocked) return ConstraintCause.Blocked;
            if (noInput) return ConstraintCause.NoInput;
            return ConstraintCause.None;
        }

        /// <summary>지금 보여 줄 힌트 한 줄. 막힌 곳이 없으면 빈 문자열.</summary>
        public static string For(ConstraintCause global, IReadOnlyList<NodeDiagnostic> diagnostics) =>
            TextOf(MostUrgent(global, diagnostics));

    }
}
