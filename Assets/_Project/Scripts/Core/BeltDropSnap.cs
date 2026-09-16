using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 드래그로 놓는 **그 순간에만** 양 끝 면을 이웃 노드에 맞춘다
    /// (2026-09-16 사용자 확정 ⓑ+ⓒ · 플랜 §74-12 A).
    ///
    /// ⚠️⚠️ **왜 필요했나.** 코어 위 칸에 **가로로** 벨트를 끌면 받는 면이 동/서라
    /// 코어(사면 출력)에서 영영 안 받는다. 화면에는 붙어 있는데 안 흐르고,
    /// **왜 안 되는지가 어디에도 안 적힌다**(사용자 육안 09-16).
    ///
    /// 📌 **놓는 순간에만 한다**(ⓑ). 계속 따라가게 하면 **플레이어가 일부러 돌려 둔 방향을
    /// 코드가 덮는다** — 놓은 뒤의 회전은 건드리지 않는다. 저장이 싣는 면도 그대로다.
    ///
    /// ⚠️ **노드만 본다.** 벨트끼리의 이음은 드래그 경로가 이미 정한다(<see cref="BeltPath"/>) —
    /// 여기서 또 보면 경로가 정한 방향을 뒤집는다.
    ///
    /// ⚠️ **양 끝만이다.** 가운데 칸은 경로가 정한 대로 둔다 — 중간을 건드리면 줄이 끊긴다.
    ///
    /// ⚠️ <see cref="BeltAutoOrient"/> 는 **그대로 둔다.** 그쪽은 병합기·분류기를 **배치가
    /// 바뀔 때마다** 다시 잡는 것이고, 이쪽은 **한 번**이다. 둘은 다른 일이다.
    /// </summary>
    public static class BeltDropSnap
    {
        /// <summary>
        /// 면을 고르는 차례 — 북 → 동 → 남 → 서.
        ///
        /// ⚠️ **이웃 노드가 둘 이상이면 이 차례의 첫 번째를 쓴다 — 가정이다.**
        /// 문서에 「어느 쪽을 먼저 무는가」 절이 없다(설계 역기입 자리).
        /// 근거는 값이 아니라 **결정론**이다 — 같은 보드가 언제나 같은 결과여야 한다.
        /// <see cref="BeltAutoOrient"/> 가 쓰는 차례와 **같은 것**을 쓴다(두 곳이 달라지면 안 된다).
        /// </summary>
        private static readonly PortFace[] FaceOrder =
        {
            PortFace.North, PortFace.East, PortFace.South, PortFace.West,
        };

        /// <summary>이웃 노드가 이 칸으로 **내보내는** 면. 없으면 <c>null</c>.</summary>
        public static PortFace? ProducerFaceAt(BoardGrid grid, Vector2Int cell, PortFace? prefer = null)
            => FindNodeFace(grid, cell, PortIO.Output, prefer);

        /// <summary>이웃 노드가 이 칸에서 **받는** 면. 없으면 <c>null</c>.</summary>
        public static PortFace? ConsumerFaceAt(BoardGrid grid, Vector2Int cell, PortFace? prefer = null)
            => FindNodeFace(grid, cell, PortIO.Input, prefer);

        /// <summary>
        /// 이 면의 이웃 노드가 <paramref name="io"/> 방향 포트를 이쪽으로 대고 있는가.
        /// </summary>
        private static bool NodeFacesUs(BoardGrid grid, Vector2Int cell, PortFace f, PortIO io)
        {
            Vector2Int nb = cell + BeltRouting.Delta(f);
            if (!grid.IsInside(nb)) return false;

            NodeInstance node = grid.GetAt(nb);
            if (node == null) return false;

            PortFace need = NodeConnectionRules.Opposite(f);
            foreach (NodePort p in node.Ports())
                if (p.io == io && p.face == need) return true;
            return false;
        }

        /// <summary>
        /// <paramref name="prefer"/> 쪽에 맞는 노드가 있으면 **그쪽을 먼저** 고른다.
        ///
        /// ⚠️ **후보가 둘일 때의 잣대다**(2026-09-16 설계 완화 · §74-12 끝).
        /// 끝 칸이 노드 둘에 닿으면 **드래그가 마지막으로 민 방향의 앞 면**을 우선한다 —
        /// 손이 가리킨 쪽이 플레이어의 뜻에 가깝다. 같으면 북 → 동 → 남 → 서.
        ///
        /// 🗑️ 구 규칙(무조건 FaceOrder 첫 순위)은 폐기. 그것은 「가정」 표기였고
        /// 설계가 더 나은 잣대를 주었다.
        /// </summary>
        private static PortFace? FindNodeFace(BoardGrid grid, Vector2Int cell, PortIO io,
            PortFace? prefer = null)
        {
            if (grid == null) return null;

            if (prefer.HasValue && NodeFacesUs(grid, cell, prefer.Value, io)) return prefer.Value;

            foreach (PortFace f in FaceOrder)
                if (NodeFacesUs(grid, cell, f, io)) return f;

            return null;
        }

        /// <summary>
        /// 경로가 낸 세그먼트들의 **첫 입구**와 **마지막 출구**만 이웃 노드에 맞춘다.
        /// 목록을 그 자리에서 고친다(새로 만들지 않는다 — 부르는 쪽이 그대로 쓴다).
        /// </summary>
        /// <returns>맞춘 끝의 수(0~2). 진단·시험이 읽는다.</returns>
        public static int Snap(BoardGrid grid, List<BeltSegmentSpec> segs)
        {
            if (grid == null || segs == null || segs.Count == 0) return 0;

            int snapped = 0;

            // 시작 — 이웃 노드가 **내보내는** 쪽이 있으면 그쪽을 입구로 삼는다.
            BeltSegmentSpec head = segs[0];
            // 시작 칸의 「앞」은 드래그가 **온 쪽**이다 — 첫 이동의 반대.
            PortFace? producer = ProducerFaceAt(grid, head.cell,
                NodeConnectionRules.Opposite(head.outFace));
            if (producer.HasValue && producer.Value != head.inFace
                && producer.Value != head.outFace)   // 입구와 출구가 같아지면 줄이 죽는다
            {
                head.inFace = producer.Value;
                segs[0] = head;
                snapped++;
            }

            // 끝 — 이웃 노드가 **받는** 쪽이 있으면 그쪽을 출구로 삼는다.
            BeltSegmentSpec tail = segs[segs.Count - 1];
            // 끝 칸의 「앞」은 드래그가 **마지막으로 민 방향**이다.
            PortFace? consumer = ConsumerFaceAt(grid, tail.cell, tail.outFace);
            if (consumer.HasValue && consumer.Value != tail.outFace
                && consumer.Value != tail.inFace)
            {
                tail.outFace = consumer.Value;
                segs[segs.Count - 1] = tail;
                snapped++;
            }

            return snapped;
        }
    }
}
