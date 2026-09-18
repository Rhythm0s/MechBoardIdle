using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **복합 가공소의 조합표를 바꾸면 전투의 드론 종이 따라 바뀌는가**
    /// (2026-09-16 사용자 육안 4차 ④ — 「광역형 → 누적형으로 바꿨는데 여전히 광역형」).
    ///
    /// 📌 **값이 어디서 끊기는지를 가르는 시험이다.** 길은 넷이다 —
    /// ① 노드가 조합표를 받는가 → ② 집계가 그것을 세는가 → ③ 몫이 갈리는가 →
    /// ④ 다리를 타고 전투로 가는가. 화면 없이 ①~③ 을 여기서 잡는다.
    /// </summary>
    public sealed class DroneRecipeSwitchTests
    {
        private const string NodeRoot = "Assets/_Project/ScriptableObjects/Nodes";

        private static NodeDefinition Node(string id)
            => AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodeRoot}/Node_{id}.asset");

        /// <summary>B 의 시작 보드 — 복합 가공소가 (9,8) 에 선다.</summary>
        private static BoardGrid BuildB()
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero,
                PartLayout.BuildMask(), MountOwner.RobotB);

            foreach (StartingBoardB.Slot slot in StartingBoardB.Nodes)
            {
                NodeDefinition def = Node(slot.nodeId);
                Assert.IsNotNull(def, $"노드 자산이 없다 — Node_{slot.nodeId}.asset");
                Assert.IsTrue(g.TryPlace(slot.cell, def, out NodeInstance placed));
                if (slot.recipe != RecipeKind.None)
                    Assert.IsTrue(placed.SelectRecipe(slot.recipe));
            }
            // ⚠️⚠️ **놓는 문을 그대로 지난다**(2026-09-18 정정). 종전에는 `TryPlaceBelt` 로
            //    직접 깔아서 **분류기·병합기가 전부 직선 벨트**로 깔렸다 — 09-18 에 B 판에
            //    분류기 둘과 병합기 하나가 생기자 줄이 끊겨 이 시험이 먼저 빨갛게 됐다.
            //    「프로브가 사람이 지나는 문을 안 지난다」(지침 §7 ［09-15］)의 실례다.
            foreach (StartingBoard.Run run in StartingBoardB.Belts)
                StartingBoard.Place(g, run);

            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        /// <summary>
        /// 판에 선 복합 변환기 **전부** (2026-09-18 정정).
        ///
        /// 🗑️ 구 「하나만 돌려준다」 폐기 — 둘째 드론 줄이 서면서 **복합이 둘**이 됐고,
        /// 옛 함수는 마지막 것만 집어 「하나를 바꿨는데 몫이 1 이 아니다」로 빨갛게 됐다.
        /// 그것은 결함이 아니라 **판이 바뀐 것**이다.
        /// </summary>
        private static List<NodeInstance> Complexes(BoardGrid g)
        {
            var list = new List<NodeInstance>();
            foreach (StartingBoardB.Slot slot in StartingBoardB.Nodes)
                if (slot.nodeId == StartingBoardB.ComplexId)
                {
                    NodeInstance n = g.GetAt(slot.cell);
                    Assert.IsNotNull(n, $"복합 변환기가 {slot.cell} 에 없다");
                    list.Add(n);
                }
            Assert.AreEqual(2, list.Count, "복합 변환기가 둘이 아니다 — 시작 보드가 바뀌었다");
            return list;
        }

        private static NetworkAggregate Aggregate(BoardGrid g)
        {
            var connected = LogisticsReach.ConnectedNodes(g);
            WorkloadRate.Result work = WorkloadRate.Compute(g, connected, null);
            return LogisticsNetwork.Aggregate(g, connected, work);
        }

        [Test]
        public void 노드가_두_조합표를_다_받는다()
        {
            // ① 조합표 자체를 못 받으면 나머지는 볼 것도 없다.
            BoardGrid g = BuildB();
            foreach (NodeInstance n in Complexes(g))
            {
                Assert.IsTrue(n.SelectRecipe(RecipeKind.AoeDrone), "광역형을 못 받는다");
                Assert.AreEqual(RecipeKind.AoeDrone, n.SelectedRecipe);

                Assert.IsTrue(n.SelectRecipe(RecipeKind.StackDrone), "누적형을 못 받는다");
                Assert.AreEqual(RecipeKind.StackDrone, n.SelectedRecipe);
            }
        }

        [Test]
        public void 조합표를_바꾸면_몫이_따라_바뀐다()
        {
            // ②③ — 집계가 세고, 몫이 갈린다. **여기가 끊기면 화면이 안 바뀐다.**
            BoardGrid g = BuildB();
            List<NodeInstance> all = Complexes(g);

            // ✅ **시작 배치는 반반이다**(2026-09-18) — 누적형 한 대 · 광역형 한 대.
            //    비율을 분류기가 아니라 **노드 수(생산 속도)**가 정한다는 것이 여기 있다.
            NetworkAggregate preset = Aggregate(g);
            Assert.AreEqual(0.5f, preset.AoeShare, 0.0001f,
                "시작 배치의 광역 몫이 반이 아니다 — 둘째 드론 줄이 안 돈다");

            foreach (NodeInstance n in all) Assert.IsTrue(n.SelectRecipe(RecipeKind.AoeDrone));
            BeltFlow.Resolve(g);   // 산출 품목이 바뀌면 벨트 품목도 다시 푼다
            NetworkAggregate aoe = Aggregate(g);
            Assert.AreEqual(1f, aoe.AoeShare, 0.0001f,
                "광역형만 도는데 몫이 1 이 아니다 — 집계가 조합표를 못 본다");

            foreach (NodeInstance n in all) Assert.IsTrue(n.SelectRecipe(RecipeKind.StackDrone));
            BeltFlow.Resolve(g);
            NetworkAggregate stack = Aggregate(g);
            Assert.AreEqual(0f, stack.AoeShare, 0.0001f,
                "누적형으로 바꿨는데 몫이 0 이 아니다 — 광역형이 그대로 남는다");
        }

        [Test]
        public void 조합표를_바꿔도_노드가_안_끊긴다()
        {
            // ⚠️ **끊기면 집계에서 통째로 빠진다** — 그러면 몫이 0/0 이 되어
            //    「누적형」으로 읽히고, 무엇이 잘못됐는지가 안 보인다.
            BoardGrid g = BuildB();
            List<NodeInstance> all = Complexes(g);

            foreach (RecipeKind k in new[] { RecipeKind.AoeDrone, RecipeKind.StackDrone })
                foreach (NodeInstance n in all)
                {
                    Assert.IsTrue(n.SelectRecipe(k));
                    BeltFlow.Resolve(g);
                    var connected = LogisticsReach.ConnectedNodes(g);
                    Assert.IsTrue(connected.Contains(n.Cell),
                        $"{k} 로 바꾸니 복합 변환기 {n.Cell} 가 이어진 노드에서 빠졌다");
                }
        }

        [Test]
        public void 몫은_두_산출의_비율이다()
        {
            // 집계 자체의 셈 — 노드 하나뿐이면 0 또는 1 이고, 섞이면 그 사이다.
            var both = new NetworkAggregate { droneStackProduce = 1f, droneAoeProduce = 3f };
            Assert.AreEqual(0.75f, both.AoeShare, 0.0001f);

            var neither = new NetworkAggregate();
            Assert.AreEqual(0f, neither.AoeShare, 0.0001f,
                "아무것도 안 만들면 누적형 쪽이 기본이다");
        }
    }
}
