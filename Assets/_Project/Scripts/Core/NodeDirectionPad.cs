using MBI.Data;

namespace MBI.Core
{
    /// <summary>누름 자리가 뜻하는 것 셋.</summary>
    public enum DirectionPadZone
    {
        /// <summary>3 × 3 밖 — **설치 취소**. 노드는 보드에 남지 않는다.</summary>
        Outside,

        /// <summary>노드 칸 — **지금 방향 그대로 확정**.</summary>
        Confirm,

        /// <summary>둘레 넷 중 하나 — **출력이 갈 쪽**.</summary>
        Face,
    }

    /// <summary>
    /// **노드를 놓을 때 방향을 묻는 누름 자리** (2026-10-01 사용자 확정 · WBS 「노드를 놓을 때
    /// 방향을 묻는다」 원문).
    ///
    /// 규칙은 원문 그대로다 —
    /// · 둘레에 누를 자리가 뜬다. **넓게 잡는다** — 노드를 둘러싼 3 × 3 에서 노드 칸을 뺀
    ///   자리 **전부**를 **대각선 X 자로 넷으로** 나눈다. 대각선 칸도 가까운 쪽으로 잡힌다.
    /// · **누른 쪽이 출력이 갈 쪽이다.** 노드가 통째로 돌고 입력도 따라 돈다.
    /// · **노드 칸 자체**를 누르면 지금 방향 그대로 확정된다.
    /// · **3 × 3 밖**을 누르면 설치가 취소된다.
    ///
    /// 📌 **여기는 자리만 읽는다.** 무엇을 그릴지도, 언제 뜰지도 부르는 쪽이 정한다 —
    /// 순수 함수라야 시험이 네 쪽과 경계를 다 밟아 볼 수 있다.
    /// </summary>
    public static class NodeDirectionPad
    {
        /// <summary>노드 칸 중심에서 3 × 3 바깥 변까지 (칸 단위). 3 × 3 이므로 1.5 다.</summary>
        public const float HalfSpan = 1.5f;

        /// <summary>노드 칸의 반 변 (칸 단위).</summary>
        public const float HalfCell = 0.5f;

        /// <summary>
        /// 노드 칸 **중심을 원점으로 한 칸 단위 오프셋**이 어느 자리인가.
        ///
        /// ⚠️ **정확히 대각선 위에 찍힌 점**은 가로세로 어느 쪽에도 더 가깝지 않다.
        /// 그 자리는 **세로(북·남)로 잡는다** — 어느 쪽으로 정하든 임의지만 **한 쪽으로
        /// 정해 두어야** 같은 자리를 두 번 눌렀을 때 답이 달라지지 않는다.
        /// </summary>
        public static DirectionPadZone Resolve(float dx, float dy, out PortFace face)
        {
            face = PortFace.North;

            float ax = dx < 0f ? -dx : dx;
            float ay = dy < 0f ? -dy : dy;

            // 노드 칸 — 확정.
            if (ax <= HalfCell && ay <= HalfCell) return DirectionPadZone.Confirm;

            // 3 × 3 밖 — 취소.
            if (ax > HalfSpan || ay > HalfSpan) return DirectionPadZone.Outside;

            // 둘레 — 대각선 X 자로 넷. 가로가 더 멀면 동·서, 아니면 북·남.
            if (ax > ay) face = dx > 0f ? PortFace.East : PortFace.West;
            else face = dy > 0f ? PortFace.North : PortFace.South;

            return DirectionPadZone.Face;
        }

        /// <summary>
        /// 출력을 <paramref name="desired"/> 쪽으로 보내려면 몇 번 돌려야 하는가(시계 90도).
        ///
        /// ⚠️ 돌리는 것은 **노드 전체**다 — 입력도 같이 돈다(원문). 그래서 출력 면 하나만
        /// 보고 각을 내면 나머지 면은 저절로 따라온다.
        /// </summary>
        public static int QuarterTurns(PortFace currentOutputFace, PortFace desired) =>
            (((int)desired - (int)currentOutputFace) % 4 + 4) % 4;
    }
}
