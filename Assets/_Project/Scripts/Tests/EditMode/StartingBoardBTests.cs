using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **로봇 B 의 시작 보드가 실제로 흐르는가** (2026-09-16 신설 · 플랜 §74-16 ②).
    ///
    /// ⚠️⚠️ **이 시험이 없으면 B 판은 「놓여 있는데 아무것도 안 흐르는」 판이 될 수 있다.**
    /// 조합표 하나가 안 걸리거나 면 하나가 어긋나도 **에러가 안 난다** — 도착만 0 이 된다.
    /// 이 리포에서 여러 번 나온 「실패하지 않는 결함」의 꼴 그대로다(지침 §7).
    ///
    /// 📌 통과 조건은 사용자가 준 그대로다 — **B 마운트 도착 > 0**.
    /// 몇 개가 적정인지는 값이고, 값은 설계가 정한다.
    /// </summary>
    public sealed class StartingBoardBTests
    {
        private const string NodeRoot = "Assets/_Project/ScriptableObjects/Nodes";
        private const float Dt = 1f / 60f;

        /// <summary>얼마나 지켜보는가. 줄이 길어 첫 산출까지 시간이 걸린다.</summary>
        private const float ObserveSeconds = 120f;

        private static NodeDefinition Node(string id)
            => AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodeRoot}/Node_{id}.asset");

        private static BoardGrid BuildB()
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero,
                PartLayout.BuildMask(), MountOwner.RobotB);

            foreach (StartingBoardB.Slot slot in StartingBoardB.Nodes)
            {
                NodeDefinition def = Node(slot.nodeId);
                Assert.IsNotNull(def, $"노드 자산이 없다 — Node_{slot.nodeId}.asset");
                Assert.IsTrue(g.TryPlace(slot.cell, def, out NodeInstance placed),
                    $"{slot.nodeId} 를 {slot.cell} 에 못 놓았다 — 실루엣 밖이거나 이미 찼다");
                if (slot.recipe != RecipeKind.None)
                    Assert.IsTrue(placed.SelectRecipe(slot.recipe),
                        $"{slot.nodeId} 가 조합표 {slot.recipe} 를 못 받는다");
            }

            foreach (StartingBoard.Run run in StartingBoardB.Belts)
                Assert.IsTrue(g.TryPlaceBelt(run.cell, run.inFace, run.outFace, FlowKind.None, out _),
                    $"벨트를 {run.cell} 에 못 깔았다");

            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        [Test]
        public void 모든_칸이_실루엣_안이고_안_겹친다()
        {
            // 판을 세우는 것 자체가 시험이다 — 위 `Assert` 들이 자리를 지킨다.
            BoardGrid g = BuildB();
            Assert.AreEqual(MountOwner.RobotB, g.Owner);
        }

        [Test]
        public void 마지막_벨트는_B_마운트_포트로_나간다()
        {
            BoardGrid g = BuildB();
            var flow = new BeltItemFlow();
            flow.Rebuild(g);

            StartingBoard.Run last = default;
            foreach (StartingBoard.Run r in StartingBoardB.Belts) last = r;

            Assert.IsTrue(flow.TryGetMountExitOwner(last.cell, out MountOwner owner),
                $"마지막 벨트 {last.cell} 가 마운트 출구가 아니다");
            Assert.AreEqual(MountOwner.RobotB, owner, "B 판인데 출구 주인이 B 가 아니다");
        }

        [Test]
        public void B_마운트에_실제로_도착한다()
        {
            BoardGrid g = BuildB();
            var flow = new BeltItemFlow();
            flow.Rebuild(g);

            int steps = Mathf.RoundToInt(ObserveSeconds / Dt);
            float firstAt = -1f;
            int arrived = 0;

            for (int i = 1; i <= steps; i++)
            {
                BoardItemTick.Step(g, flow, Dt, 1f);
                arrived += flow.PendingMountArrivals.Count;
                if (firstAt < 0f && arrived > 0) firstAt = i * Dt;
                flow.ClearPendingMountArrivals();
            }

            Assert.Greater(arrived, 0,
                $"{ObserveSeconds:F0}초 동안 B 마운트 도착이 0 이다 — "
                + "줄이 이어져 있지 않거나 조합표가 안 걸렸다");
            Debug.Log($"[B 시작 보드] {ObserveSeconds:F0}초 도착 {arrived} 개 · 첫 도착 {firstAt:F1}초");
        }
    }
}
