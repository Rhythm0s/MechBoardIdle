using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **아이템은 들어온 면에서 나타나야 한다** (2026-10-14 사용자 육안 · 확정 #116).
    ///
    /// 사용자가 8125 에서 본 그림 — **「병합기 안의 탄이 12시 → 6시로만 흐른다」.**
    /// 남 줄에서 **서면**으로 들어오는 것도 12시에서 나타났다.
    ///
    /// 까닭 — `BeltItem` 이 `kind`·`progress` 만 들고 있어서, 그리는 쪽이 **칸의 `InFace`
    /// 하나**로 그렸다. 받는 면이 셋인 병합기에서는 어느 면에서 왔든 같은 면에서 나타난다.
    /// **흐름은 정상이었다**(1.92/초 · `261014_merge_probe.log`) — 그리기만 틀렸다.
    /// </summary>
    public sealed class BeltItemEntryFaceTests
    {
        [Test]
        public void 어긋남으로_면을_읽는다()
        {
            // 이웃이 북쪽에 있으면(델타 +y) 그 면은 북이다.
            foreach (PortFace f in System.Enum.GetValues(typeof(PortFace)))
            {
                Assert.IsTrue(BeltItemPose.TryFaceOfDelta(BeltRouting.Delta(f), out PortFace got),
                    f + " 의 어긋남을 못 읽었다");
                Assert.AreEqual(f, got);
            }
        }

        [Test]
        public void 맞닿지_않은_칸이면_모른다()
        {
            // 대각선·멀리 떨어진 칸은 면이 없다 — 지어내면 엉뚱한 자리에서 나타난다.
            Assert.IsFalse(BeltItemPose.TryFaceOfDelta(new Vector2Int(1, 1), out _));
            Assert.IsFalse(BeltItemPose.TryFaceOfDelta(new Vector2Int(2, 0), out _));
            Assert.IsFalse(BeltItemPose.TryFaceOfDelta(Vector2Int.zero, out _));
        }

        [Test]
        public void 서면으로_들어오면_서쪽_가장자리에서_나타난다()
        {
            // ⚠️ **이것이 사용자가 본 결함의 시험이다.**
            //    들어온 면이 서쪽이면 progress 0 의 자리는 **서쪽 가장자리**여야 한다.
            Vector2 west = BeltItemPose.LocalOffset(PortFace.West, PortFace.South, 0f);
            Assert.Less(west.x, -0.4f, "서쪽에서 들어왔는데 서쪽 가장자리가 아니다");
            Assert.AreEqual(0f, west.y, 0.01f);

            // 같은 칸이라도 북면으로 들어오면 북쪽 가장자리다 — 둘이 달라야 한다.
            Vector2 north = BeltItemPose.LocalOffset(PortFace.North, PortFace.South, 0f);
            Assert.Greater(north.y, 0.4f, "북쪽에서 들어왔는데 북쪽 가장자리가 아니다");
            Assert.AreNotEqual(west, north,
                "받는 면이 달라도 같은 자리에 그리면 「12시에서만 나온다」가 된다");
        }

        [Test]
        public void 나가는_자리는_받는_면과_무관하다()
        {
            // 출구는 하나다 — 어느 면으로 들어왔든 끝에서는 같은 곳에 선다.
            Vector2 a = BeltItemPose.LocalOffset(PortFace.West, PortFace.South, 1f);
            Vector2 b = BeltItemPose.LocalOffset(PortFace.North, PortFace.South, 1f);
            Assert.AreEqual(a, b);
            Assert.Less(a.y, -0.4f, "남쪽으로 나가는데 남쪽 가장자리가 아니다");
        }

        [Test]
        public void 직선_벨트는_종전과_같다()
        {
            // ⚠️ 고침이 직선·코너를 건드리면 안 된다 — 거기선 받는 면이 하나뿐이라
            //    들고 온 면과 칸의 입력면이 같은 값이다.
            Vector2 mid = BeltItemPose.LocalOffset(PortFace.East, PortFace.West, 0.5f);
            Assert.AreEqual(Vector2.zero, mid, "직선 벨트의 가운데는 칸 중심이다");

            Vector2 start = BeltItemPose.LocalOffset(PortFace.East, PortFace.West, 0f);
            Assert.Greater(start.x, 0.4f);
        }

        [Test]
        public void 모르는_면은_거짓으로_남는다()
        {
            // 기본값은 「모른다」여야 한다 — 참으로 두면 노드가 밀어 넣은 것이
            // 엉뚱한 면에서 나타난다(그때는 칸의 입력면으로 떨어지는 것이 맞다).
            var item = new BeltItem { kind = FlowKind.StandardAmmo, progress = 0f };
            Assert.IsFalse(item.hasEntryFace);
        }

        /// <summary>
        /// ⚠️⚠️ **두 번째 신고의 시험**(2026-10-14 사용자 육안 「병합기 서면에서 진입 품목이
        /// 등장하지 않음」). 첫 고침은 벨트→벨트(<c>TryHandOff</c>)만 면을 달았고,
        /// **노드가 미는 길**(<c>TryPushFrom</c> → <c>TryInsert</c>)은 빠져 있었다.
        /// 병합기에 넣는 쪽이 바로 노드라, 서면 품목이 그대로 12시에서 나왔다.
        /// </summary>
        [Test]
        public void 미는_쪽도_들어온_면을_들고_간다()
        {
            var grid = new BoardGrid(5, 5, 1f, Vector2.zero);

            // 병합기 (2,2) — 서·남에서 받아 동으로 낸다.
            grid.TryPlaceBeltElement(new Vector2Int(2, 2), BeltElementKind.Merger,
                new[] { PortFace.West, PortFace.South }, new[] { PortFace.East },
                FlowKind.Ammo, out _);

            // 그 서쪽 칸에서 동으로 민다.
            var from = new Vector2Int(1, 2);
            grid.TryPlaceBelt(from, PortFace.West, PortFace.East, FlowKind.Ammo, out _);

            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            Assert.IsTrue(flow.TryPushFrom(from, FlowKind.Ammo), "서쪽 칸에서 병합기로 밀린다");

            var merger = new Vector2Int(2, 2);
            Assert.AreEqual(1, flow.ItemsAt(merger).Count);

            BeltItem item = flow.ItemsAt(merger)[0];
            Assert.IsTrue(item.hasEntryFace, "민 쪽이 면을 안 달면 칸의 입력면으로 떨어진다");
            Assert.AreEqual(PortFace.West, item.entryFace,
                "서쪽 칸에서 왔으면 서면이다 — 아니면 12시에서 나타난다");
        }

        [Test]
        public void 어디서_왔는지_모르면_면을_안_지어낸다()
        {
            var grid = new BoardGrid(3, 3, 1f, Vector2.zero);
            var cell = new Vector2Int(1, 1);
            grid.TryPlaceBelt(cell, PortFace.West, PortFace.East, FlowKind.Ammo, out _);

            var flow = new BeltItemFlow();
            flow.Rebuild(grid);
            Assert.IsTrue(flow.TryInsert(cell, FlowKind.Ammo));

            Assert.IsFalse(flow.ItemsAt(cell)[0].hasEntryFace,
                "모르는 것을 안다고 하면 엉뚱한 면에서 나타난다");
        }
    }
}
