using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **부스터 줄이 배포 시작 보드에서 실제로 도는가** (2026-09-17 · `260917_W03` 3장).
    ///
    /// ⚠️⚠️ **지키는 것은 「회피가 난다」이다.** 줄이 놓이기만 하고 조합표가 표준탄으로
    /// 남거나 분류기가 직선으로 깔리면, 추진제가 **한 개도 안 나오고 부스터는 영영 빈
    /// 그릇**이 된다 — 에러도 경고도 없이 회피만 0 이 된다.
    /// 09-17 에 실제로 그 자리에서 한 번 걸렸다(분류기를 노드에서 한 칸 띄웠다).
    ///
    /// 📌 그래서 보는 것은 배치가 아니라 **집계**다 — 부스터 대수와 추진제 산출.
    /// 🗑️ 구 `HarnessBoosterLineTests` 를 이 파일이 잇는다 — 줄이 하네스에서 배포로 갔다.
    /// </summary>
    public sealed class StartingBoardBoosterTests
    {
        private const string NodeRoot = "Assets/_Project/ScriptableObjects/Nodes";

        private static NodeDefinition Node(string id)
            => AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodeRoot}/Node_{id}.asset");

        private static BoardGrid Build(MountOwner owner, bool fillEmptySlot)
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero,
                PartLayout.BuildMask(), owner);

            int placed = owner == MountOwner.RobotB
                ? StartingBoardB.Apply(g, Node)
                : StartingBoard.Apply(g, Node);

            int want = owner == MountOwner.RobotB
                ? StartingBoardB.Nodes.Count : StartingBoard.Nodes.Count;
            Assert.AreEqual(want, placed, $"{owner} 판에 노드가 다 안 섰다");

            if (owner == MountOwner.RobotA && fillEmptySlot)
                StartingBoard.Place(g, StartingBoard.FillsEmptySlot);

            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        private static NetworkAggregate Aggregate(BoardGrid g)
        {
            ICollection<Vector2Int> connected = LogisticsReach.ConnectedNodes(g);
            WorkloadRate.Result work = WorkloadRate.Compute(g, connected, null);
            return LogisticsNetwork.Aggregate(g, connected, work);
        }

        [Test]
        public void A_판이_부스터_둘과_추진제를_집계에_낸다()
        {
            NetworkAggregate a = Aggregate(Build(MountOwner.RobotA, fillEmptySlot: true));

            Assert.AreEqual(2, a.boosterCount,
                "부스터가 집계에 안 잡혔다 — 안 이어졌으면 `connectedOnly` 에서 빠진다");
            Assert.Greater(a.propellantProduce, 0f,
                "추진제 산출이 0 이다 — 기초 군수가 기본값(표준탄)으로 돌고 있다");
        }

        [Test]
        public void B_판이_부스터_둘과_추진제를_집계에_낸다()
        {
            NetworkAggregate a = Aggregate(Build(MountOwner.RobotB, fillEmptySlot: false));

            Assert.AreEqual(2, a.boosterCount);
            Assert.Greater(a.propellantProduce, 0f);
        }

        [Test]
        public void 두_판_다_전력이_모자라지_않는다()
        {
            foreach (MountOwner owner in new[] { MountOwner.RobotA, MountOwner.RobotB })
            {
                NetworkAggregate a = Aggregate(Build(owner, fillEmptySlot: true));
                Assert.GreaterOrEqual(a.powerSupply, a.powerDraw,
                    $"{owner} 전력이 모자란다 — 공급 {a.powerSupply} · 수요 {a.powerDraw}. "
                    + "에너지 노드를 더 놓아야 하고, 그 사실을 보고에 적어야 한다");
            }
        }

        [Test]
        public void 부스터_줄은_마운트로_안_간다()
        {
            // ⚠️⚠️ **튜토리얼의 수업이 여기 걸려 있다**(튜토리얼 기획서 「검증 순서」 3번).
            //    합류 뒤 운반로 한 칸을 비우면 마운트 도착이 **정확히 0** 이어야 한다.
            //    부스터 줄이 마운트 포트로 흘러들면 그 0 이 깨지고, 「이어야 흐른다」는
            //    수업이 화면에서 성립하지 않는다.
            BoardGrid g = Build(MountOwner.RobotA, fillEmptySlot: false);

            var flow = new BeltItemFlow();
            flow.Rebuild(g);

            int arrived = 0;
            for (int i = 0; i < 1200; i++)   // 60초
            {
                BoardItemTick.Step(g, flow, 0.05f, 1f);
                arrived += flow.PendingMountArrivals.Count;
                flow.ClearPendingMountArrivals();
            }

            Assert.AreEqual(0, arrived,
                "빈 칸을 비워 두었는데 마운트에 무언가 닿았다 — 부스터 줄이 마운트로 새고 있다");
        }

        [Test]
        public void 빈_칸을_채우면_다시_흐른다()
        {
            // 위 시험의 짝 — 0 이 「줄이 죽어서」가 아니라 「비워 둔 칸 때문에」임을 못 박는다.
            BoardGrid g = Build(MountOwner.RobotA, fillEmptySlot: true);

            var flow = new BeltItemFlow();
            flow.Rebuild(g);

            int arrived = 0;
            for (int i = 0; i < 1200; i++)
            {
                BoardItemTick.Step(g, flow, 0.05f, 1f);
                arrived += flow.PendingMountArrivals.Count;
                flow.ClearPendingMountArrivals();
            }

            Assert.Greater(arrived, 0, "빈 칸을 채웠는데도 마운트에 하나도 안 닿았다");
        }

        [Test]
        public void 분류기가_직선으로_깔리지_않았다()
        {
            // 🗑️ 구 `Run.merger`(참/거짓)로는 분류기를 못 적었다 — 값으로 바꾼 까닭이다.
            //    참/거짓만 보던 옛 자리가 남아 있으면 분류기가 **직선 벨트**로 조용히 깔린다.
            foreach (MountOwner owner in new[] { MountOwner.RobotA, MountOwner.RobotB })
            {
                BoardGrid g = Build(owner, fillEmptySlot: true);
                var sorters = new List<Vector2Int>();
                for (int x = 0; x < g.Columns; x++)
                for (int y = 0; y < g.Rows; y++)
                {
                    BeltInstance b = g.GetBeltAt(new Vector2Int(x, y));
                    if (b != null && b.Element == BeltElementKind.Sorter)
                        sorters.Add(new Vector2Int(x, y));
                }

                Assert.AreEqual(1, sorters.Count,
                    $"{owner} 판의 분류기 수가 하나가 아니다 — 실제 {sorters.Count}");
            }
        }
    }
}
