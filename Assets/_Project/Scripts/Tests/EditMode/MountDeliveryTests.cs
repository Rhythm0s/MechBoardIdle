using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **마운트에 실제로 도착한 것이 출력이다** (2026-09-05 · `260904_W04` 2-1 4번).
    ///
    /// 종전 `actual`은 「군수 노드 수 × 라인 스펙 × 배율들」이라 벨트를 어떻게 깔든 노드 수만
    /// 같으면 같은 수가 나왔다. 그 상태에서는 「물류 라인을 최적화하는 행위가 재미있는가」에서
    /// **최적화의 결과가 숫자에 안 보인다.** 이 파일이 그 인과를 붙든다.
    /// </summary>
    public sealed class MountDeliveryTests
    {
        private const float Delta = 0.001f;

        // 발당피해 — 무기 스펙의 대표값(관통 20 · 분열 25 · 폭발 50)을 그대로 쓴다.
        private static float Damage(AmmoKind kind)
        {
            switch (kind)
            {
                case AmmoKind.Pierce: return 20f;
                case AmmoKind.Split: return 25f;
                default: return 50f;
            }
        }

        private static IReadOnlyList<MountArrival> One(FlowKind kind) =>
            new[] { new MountArrival(MountOwner.RobotA, kind) };

        // ---- 환산 ----

        /// <summary>도착 하나가 발당피해만큼의 전투력이다.</summary>
        [Test]
        public void Arrival_IsWorthItsDamagePerShot()
        {
            var d = new MountDelivery();

            d.Observe(One(FlowKind.PierceAmmo), Damage, 0.5f);
            d.Observe(One(FlowKind.PierceAmmo), Damage, 0.5f);

            Assert.IsTrue(d.TryDrain(0.1f, out float rate));
            Assert.AreEqual(40f, rate, Delta, "관통 20 × 2발 ÷ 1초");
            Assert.AreEqual(40f, d.TotalPower, Delta);
        }

        /// <summary>
        /// **표준탄은 분열 자리다.** `FlowKind.StandardAmmo` 선언에 적힌 대응을 그대로 쓴다 —
        /// 여기서 새로 정하는 것이 아니다.
        /// </summary>
        [Test]
        public void StandardAmmo_MapsToSplit()
        {
            Assert.IsTrue(MountItemMap.TryAmmoKindOf(FlowKind.StandardAmmo, out AmmoKind kind));
            Assert.AreEqual(AmmoKind.Split, kind);
        }

        /// <summary>
        /// ⚠️ **탄약이 아닌 도착은 안 센다.** 부품이나 배터리가 마운트에 닿아도 전투력이 아니다 —
        /// 기본값으로 폭발탄을 돌려주면 부품 하나가 발당 50으로 둔갑한다.
        /// </summary>
        [Test]
        public void NonAmmo_IsNotCounted()
        {
            var d = new MountDelivery();

            d.Observe(new[]
            {
                new MountArrival(MountOwner.RobotA, FlowKind.BasicParts),
                new MountArrival(MountOwner.RobotA, FlowKind.Battery),
                new MountArrival(MountOwner.RobotA, FlowKind.StackDrone),
            }, Damage, 1f);

            Assert.IsTrue(d.TryDrain(0.1f, out float rate));
            Assert.AreEqual(0f, rate, Delta, "탄이 아니면 0이다");

            Assert.IsFalse(MountItemMap.TryAmmoKindOf(FlowKind.BasicParts, out _));
            Assert.IsFalse(MountItemMap.TryAmmoKindOf(FlowKind.StackDrone, out _));
        }

        /// <summary>탄종이 섞이면 각자의 발당피해로 더한다.</summary>
        [Test]
        public void MixedKinds_SumByTheirOwnDamage()
        {
            var d = new MountDelivery();

            d.Observe(new[]
            {
                new MountArrival(MountOwner.RobotA, FlowKind.PierceAmmo),     // 20
                new MountArrival(MountOwner.RobotB, FlowKind.ExplosiveAmmo),  // 50
                new MountArrival(MountOwner.RobotA, FlowKind.StandardAmmo),   // 25 (분열 자리)
            }, Damage, 2f);

            Assert.IsTrue(d.TryDrain(0.1f, out float rate));
            Assert.AreEqual(47.5f, rate, Delta, "(20+50+25) ÷ 2초");
        }

        // ---- 구간 ----

        /// <summary>
        /// **너무 짧은 구간으로는 안 나눈다.** 한 프레임(수 ms)으로 나누면 한 개만 닿아도
        /// 비율이 수백으로 튄다. 구간이 찰 때까지 모은다.
        /// </summary>
        [Test]
        public void ShortInterval_IsHeldNotDivided()
        {
            var d = new MountDelivery();

            d.Observe(One(FlowKind.PierceAmmo), Damage, 0.016f);

            Assert.IsFalse(d.TryDrain(0.1f, out float rate), "아직 구간이 안 찼다");
            Assert.AreEqual(0f, rate, Delta, "직전 비율이 그대로 — 튀지 않는다");

            // 구간을 채우면 모아 둔 것이 한꺼번에 비율이 된다.
            d.Observe(null, Damage, 0.084f);
            Assert.IsTrue(d.TryDrain(0.1f, out rate));
            Assert.AreEqual(200f, rate, Delta, "관통 20 ÷ 0.1초");
        }

        /// <summary>한 번 낸 구간은 비운다 — 안 비우면 같은 도착이 계속 세어진다.</summary>
        [Test]
        public void Draining_ClearsTheInterval()
        {
            var d = new MountDelivery();

            d.Observe(One(FlowKind.PierceAmmo), Damage, 1f);
            Assert.IsTrue(d.TryDrain(0.1f, out float first));
            Assert.AreEqual(20f, first, Delta);

            d.Observe(null, Damage, 1f); // 아무것도 안 왔다
            Assert.IsTrue(d.TryDrain(0.1f, out float second));
            Assert.AreEqual(0f, second, Delta, "안 오면 0이다 — 직전 값이 남지 않는다");
        }

        /// <summary>보드가 바뀌면 모으던 구간을 버린다.</summary>
        [Test]
        public void Reset_DropsEverything()
        {
            var d = new MountDelivery();

            d.Observe(One(FlowKind.PierceAmmo), Damage, 1f);
            d.Reset();

            Assert.AreEqual(0f, d.Rate, Delta);
            Assert.AreEqual(0f, d.TotalPower, Delta);
            Assert.AreEqual(0f, d.PendingSeconds, Delta);
        }

        // ---- 통합: 배치가 출력을 만든다 ----

        private static NodeDefinition Node(string id) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>(
                $"Assets/_Project/ScriptableObjects/Nodes/Node_{id}.asset");

        /// <summary>시작 보드를 그대로 깐다 — 배치를 여기서 다시 적지 않는다.</summary>
        private static BoardGrid BuildStartingBoard(bool fillEmptySlot, Vector2Int omitBelt)
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero,
                PartLayout.BuildMask());

            foreach (StartingBoard.Slot slot in StartingBoard.Nodes)
                g.TryPlace(slot.cell, Node(slot.nodeId), out _);

            foreach (StartingBoard.Run run in StartingBoard.Belts)
            {
                if (run.cell == omitBelt) continue;
                g.TryPlaceBelt(run.cell, run.inFace, run.outFace, FlowKind.None, out _);
            }

            if (fillEmptySlot)
                g.TryPlace(StartingBoard.FillsEmptySlot.cell,
                    Node(StartingBoard.FillsEmptySlot.nodeId), out _);

            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        /// <summary>보드를 <paramref name="seconds"/>초 돌리고 마운트에 닿은 전투력 총합을 낸다.</summary>
        private static float RunAndMeasure(BoardGrid grid, float seconds)
        {
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);

            var delivery = new MountDelivery();
            const float dt = 0.05f;

            for (int i = 0; i < Mathf.RoundToInt(seconds / dt); i++)
            {
                BoardItemTick.Step(grid, flow, dt, 1f);
                delivery.Observe(flow.PendingMountArrivals, Damage, dt);
                flow.ClearPendingMountArrivals();
            }
            return delivery.TotalPower;
        }

        /// <summary>
        /// **빈 칸을 채우면 마운트에 물건이 닿는다.** 이것이 스테이지 0의 인과 그 자체다 —
        /// 노드 하나를 놓는 것이 화면의 숫자를 움직인다.
        ///
        /// ⚠️ **값을 못 박지 않는다**(`260904_W04` 4장). 여기서 보는 것은 0과 0이 아님의 차이이지
        /// 「몇이 나오는가」가 아니다. 노드 산출률이 아직 전부 센티넬이라 그 수는 확정치가 아니다.
        /// </summary>
        [Test]
        public void FillingTheEmptySlot_MakesThingsArriveAtTheMount()
        {
            if (Node(StartingBoard.MuniId) == null)
                Assert.Ignore("자산 없음 — 먼저 밸런스·노드 생성 메뉴를 실행해야 한다.");

            float empty = RunAndMeasure(BuildStartingBoard(false, -Vector2Int.one), 60f);
            float filled = RunAndMeasure(BuildStartingBoard(true, -Vector2Int.one), 60f);

            Assert.AreEqual(0f, empty, Delta, "탄을 만들 노드가 없으면 아무것도 안 닿는다");
            Assert.Greater(filled, 0f, "채우면 닿는다 — 배치가 출력을 만든다");
        }

        /// <summary>
        /// **벨트 한 칸을 끊으면 출력이 0이 된다.** 노드는 그대로 다 놓여 있다.
        ///
        /// 이것이 이번 개정으로 처음 성립하는 인과다. 종전 `actual`은 노드 수에서 나왔으므로
        /// 벨트를 지워도 같은 수가 나왔다 — 배선이 출력에 안 걸려 있었다.
        /// </summary>
        [Test]
        public void CuttingOneBelt_DropsOutputToZero_ThoughNodesRemain()
        {
            if (Node(StartingBoard.MuniId) == null)
                Assert.Ignore("자산 없음 — 먼저 밸런스·노드 생성 메뉴를 실행해야 한다.");

            float whole = RunAndMeasure(BuildStartingBoard(true, -Vector2Int.one), 60f);
            Assert.Greater(whole, 0f, "온전한 라인은 물건을 나른다");

            // 마운트 바로 앞 칸을 뺀다. 노드는 하나도 안 건드렸다.
            Vector2Int lastLeg = StartingBoard.Belts[StartingBoard.Belts.Count - 2].cell;
            float cut = RunAndMeasure(BuildStartingBoard(true, lastLeg), 60f);

            Assert.AreEqual(0f, cut, Delta, $"{lastLeg}를 끊으면 마운트에 못 닿는다");
        }
    }
}
