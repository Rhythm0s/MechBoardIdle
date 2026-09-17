using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **벨트는 아이템 단위다** (2026-09-14 사용자 확정 · §72-29).
    ///
    /// 벨트 위에 품목이 각자 놓여 **섞여 흐르고**, 병합기·분류기는 품목이 달라도
    /// 합류·분배한다. **품목을 가리는 곳은 노드 입력 하나뿐**이다.
    ///
    /// ⚠️ **왜 바꿨는가.** 종전에는 벨트 한 장이 `Kind` 하나만 날랐고 링크도 인계도
    /// 그것을 관문으로 썼다. 그래서 표준탄 줄과 폭발탄 줄을 병합기로 모으면
    /// **한 종류로 굳고 나머지가 끊겨** 60초에 **0개**가 닿았다(하네스 `203897e`).
    /// 「표준 4 + 폭발 2 = 140」이 격자 위에서 설 수 없던 자리다.
    /// </summary>
    public sealed class MixedBeltTests
    {
        private const string NodesDir = "Assets/_Project/ScriptableObjects/Nodes";

        private NodeDefinition _core, _proc, _muni, _munix;

        [SetUp]
        public void SetUp()
        {
            _core = Load("core");
            _proc = Load("proc");
            _muni = Load("muni");
            _munix = Load("munix");
            if (_core == null || _proc == null || _muni == null || _munix == null)
                Assert.Ignore("노드 자산 없음 — 먼저 메뉴 'MBI/Generate Balance + Nodes' 실행.");
        }

        private static NodeDefinition Load(string id) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodesDir}/Node_{id}.asset");

        // ────────────────────────────────────────────────────────────────
        //  1. 병합기 — 표준탄 + 폭발탄이 **한 포트**로 들어간다
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 마운트 고정 포트는 **포트 칸의 벨트 한 장**이 넣는다. 그 한 장이 한 품목만
        /// 날랐기 때문에 두 탄종이 영영 못 닿았다 — 이제 섞여 들어간다.
        /// </summary>
        [Test]
        public void MergedLines_DeliverBothAmmoKindsToOneMountPort()
        {
            var g = new BoardGrid(12, 14, 1f, Vector2.zero, PartLayout.BuildMask());

            // 표준 줄 — 코어(5,8) 동면 → 가공 → 기초 가공소 → 표준탄
            g.TryPlace(new Vector2Int(5, 8), _core, out _);
            g.TryPlace(new Vector2Int(6, 8), _proc, out _);
            g.TryPlace(new Vector2Int(7, 8), _muni, out _);
            g.TryPlaceBelt(new Vector2Int(8, 8), PortFace.West, PortFace.South, FlowKind.None, out _);

            // 코어 서면 → 분류기가 폭발 줄 둘(재료·발전재료)에 나눠 먹인다
            g.TryPlaceBelt(new Vector2Int(4, 8), PortFace.East, PortFace.South, FlowKind.None, out _);
            g.TryPlaceBeltElement(new Vector2Int(4, 7), BeltElementKind.Sorter,
                new[] { PortFace.North }, new[] { PortFace.East, PortFace.South },
                FlowKind.None, out _);
            g.TryPlaceBelt(new Vector2Int(4, 6), PortFace.North, PortFace.East, FlowKind.None, out _);

            // 폭발 본줄 (y7)
            g.TryPlace(new Vector2Int(5, 7), _proc, out _);
            g.TryPlace(new Vector2Int(6, 7), _muni, out _);
            g.TryPlace(new Vector2Int(7, 7), _munix, out NodeInstance mx);
            Assert.IsTrue(mx.SelectRecipe(RecipeKind.ExplosiveAmmo));

            // ⚠️ 둘째 재료는 **남면**으로 든다 — 아래 줄(y6)이 꺾어 올려 준다.
            g.TryPlace(new Vector2Int(5, 6), _proc, out NodeInstance pm);
            Assert.IsTrue(pm.SelectRecipe(RecipeKind.PowerMaterial));
            g.TryPlaceBelt(new Vector2Int(6, 6), PortFace.West, PortFace.East, FlowKind.None, out _);
            g.TryPlaceBelt(new Vector2Int(7, 6), PortFace.West, PortFace.North, FlowKind.None, out _);

            // x8 기둥에서 **병합기**로 모아 운반로 y5 → 마운트 A(1,4 남면)
            g.TryPlaceBeltElement(new Vector2Int(8, 7), BeltElementKind.Merger,
                StartingBoard.MergerInFaces(PortFace.South), new[] { PortFace.South },
                FlowKind.None, out _);
            g.TryPlaceBelt(new Vector2Int(8, 6), PortFace.North, PortFace.South, FlowKind.None, out _);
            g.TryPlaceBelt(new Vector2Int(8, 5), PortFace.North, PortFace.West, FlowKind.None, out _);
            for (int x = 7; x >= 2; x--)
                g.TryPlaceBelt(new Vector2Int(x, 5), PortFace.East, PortFace.West, FlowKind.None, out _);
            g.TryPlaceBelt(new Vector2Int(1, 5), PortFace.East, PortFace.South, FlowKind.None, out _);
            g.TryPlaceBelt(new Vector2Int(1, 4), PortFace.North, PortFace.South, FlowKind.None, out _);

            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);

            var flow = new BeltItemFlow();
            flow.Rebuild(g);

            var got = new Dictionary<FlowKind, int>();
            const float dt = 0.05f;
            for (int i = 0; i < 1200; i++) // 60초
            {
                BoardItemTick.Step(g, flow, dt, 1f);
                foreach (MountArrival a in flow.PendingMountArrivals)
                {
                    got.TryGetValue(a.kind, out int n);
                    got[a.kind] = n + 1;
                }
                flow.ClearPendingMountArrivals();
            }

            Assert.Greater(got.Count, 1, "**두 탄종이 다 닿아야 한다** — 구 규칙에서는 0개였다");
            Assert.Greater(got[FlowKind.StandardAmmo], 0, "표준탄이 닿는다");
            Assert.Greater(got[FlowKind.ExplosiveAmmo], 0, "폭발탄이 닿는다");
        }

        // ────────────────────────────────────────────────────────────────
        //  2. 분류기 — 품목을 안 보고 **도착 순서대로** 셋에 고르게 나눈다
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 분류기 커서는 **차례**로 돈다. 품목을 보면 한 갈래가 한 품목을 독차지해
        /// 「갈래를 늘리려면 분류기를 놓는다」가 성립하지 않는다.
        /// </summary>
        [Test]
        public void Sorter_SplitsRoundRobin_RegardlessOfKind()
        {
            var g = new BoardGrid(10, 10, 1f, Vector2.zero);

            // (1,2) 분류기 — 동·북·남 셋으로 나눈다. 갈래마다 두 칸이라 넉넉히 받는다
            // (한 칸 최대가 3이라 한 칸이면 4를 못 담는다).
            g.TryPlaceBeltElement(new Vector2Int(1, 2), BeltElementKind.Sorter,
                new[] { PortFace.West },
                new[] { PortFace.East, PortFace.North, PortFace.South },
                FlowKind.None, out _);

            g.TryPlaceBelt(new Vector2Int(2, 2), PortFace.West, PortFace.East, FlowKind.None, out _);
            g.TryPlaceBelt(new Vector2Int(3, 2), PortFace.West, PortFace.East, FlowKind.None, out _);
            g.TryPlaceBelt(new Vector2Int(1, 3), PortFace.South, PortFace.North, FlowKind.None, out _);
            g.TryPlaceBelt(new Vector2Int(1, 4), PortFace.South, PortFace.North, FlowKind.None, out _);
            g.TryPlaceBelt(new Vector2Int(1, 1), PortFace.North, PortFace.South, FlowKind.None, out _);
            g.TryPlaceBelt(new Vector2Int(1, 0), PortFace.North, PortFace.South, FlowKind.None, out _);

            var flow = new BeltItemFlow();
            flow.Rebuild(g);

            // 품목 셋을 **섞어** 열둘 밀어 넣는다.
            var kinds = new[] { FlowKind.StandardAmmo, FlowKind.PierceAmmo, FlowKind.ExplosiveAmmo };
            var sorter = new Vector2Int(1, 2);
            int sent = 0;
            for (int i = 0; i < 12; i++)
            {
                if (flow.TryPushFrom(sorter, kinds[i % 3])) sent++;
                flow.Tick(0.25f);   // 한 칸씩 밀어 다음 것이 들어갈 자리를 낸다
            }

            Assert.AreEqual(12, sent, "열둘이 다 나갔다");

            // ⚠️ **갈래 셋이 고르게 받는다** — 품목이 섞여 있어도 차례는 흔들리지 않는다.
            int east = Count(flow, new Vector2Int(2, 2)) + Count(flow, new Vector2Int(3, 2));
            int north = Count(flow, new Vector2Int(1, 3)) + Count(flow, new Vector2Int(1, 4));
            int south = Count(flow, new Vector2Int(1, 1)) + Count(flow, new Vector2Int(1, 0));

            Assert.AreEqual(4, east, "동 4");
            Assert.AreEqual(4, north, "북 4");
            Assert.AreEqual(4, south, "남 4");
        }

        private static int Count(BeltItemFlow flow, Vector2Int cell) => flow.ItemsAt(cell).Count;
    }
}
