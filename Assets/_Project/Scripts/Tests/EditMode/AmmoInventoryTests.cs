using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 탄약 재고 단일 층(§5-7, 2026-08-21 컷 철회분).
    /// 원천 = 밸런스 문서「밸런스 확정 원칙」의 **탄약 소진 = 공격 정지**(대체 수단 없음)와
    /// 「태그 시스템 수치」의 재고 층위 단일 판정. 용량 40발은 확정치.
    /// </summary>
    public sealed class AmmoInventoryTests
    {
        private const float D = 0.001f;

        // ---- 재고 자체 ----

        [Test]
        public void Produce_AccumulatesUpToCapacity()
        {
            var inv = new AmmoInventory(40f, 0f);
            inv.Produce(1f, 6f);
            Assert.AreEqual(6f, inv.Stock, D);

            inv.Produce(100f, 6f); // 한참 생산
            Assert.AreEqual(40f, inv.Stock, D, "용량을 넘는 분은 버려진다");
            Assert.IsTrue(inv.IsFull, "만재 — 태그·과부하 트리거의 기준");
        }

        [Test]
        public void TryConsume_FailsWhenInsufficient_AndDoesNotGoNegative()
        {
            var inv = new AmmoInventory(40f, 1f);
            Assert.IsTrue(inv.TryConsume(1f));
            Assert.IsTrue(inv.IsEmpty);

            Assert.IsFalse(inv.TryConsume(1f), "모자라면 실패");
            Assert.AreEqual(0f, inv.Stock, D, "실패해도 음수로 내려가지 않는다");
        }

        [Test]
        public void FillRatio_IsStockOverCapacity()
        {
            var inv = new AmmoInventory(40f, 10f);
            Assert.AreEqual(0.25f, inv.FillRatio, D);
            inv.Fill();
            Assert.AreEqual(1f, inv.FillRatio, D);
            inv.Drain();
            Assert.AreEqual(0f, inv.FillRatio, D);
        }

        [Test]
        public void InitialStock_IsClampedToCapacity()
        {
            Assert.AreEqual(40f, new AmmoInventory(40f, 999f).Stock, D);
            Assert.AreEqual(0f, new AmmoInventory(40f, -5f).Stock, D);
        }

        // ---- 전투 연동: 재고가 마르면 발사가 멈춘다 ----

        private static List<AmmoLine> PierceLine(float rate) =>
            new List<AmmoLine> { new AmmoLine(AmmoKind.Pierce, 20f, rate) };

        private static RobotSetup Robot(float capacity, float initial, float rate) => new RobotSetup
        {
            hp = 1000f, mountCoef = 1f, moduleMult = 1f, attackRange = 100f,
            multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
            lines = PierceLine(rate),
            ammoCapacity = capacity, ammoInitialStock = initial,
        };

        private static List<EnemySpawn> Sandbag() => new List<EnemySpawn>
        {
            new EnemySpawn { label = "샌드백", hp = 1000000f, def = 0f, atk = 0f,
                moveSpeed = 0f, attackRange = 0.5f, attackInterval = 1f },
        };

        [Test]
        public void EmptyStock_StopsFiring()
        {
            // 재고 2발, 생산 0 → 2발만 나가고 멈춘다. 대체 수단이 없다는 확정 원칙.
            var sim = new CombatSimulation(Robot(40f, 2f, 5f), Sandbag(), 6f, 120f, 0f)
            { AmmoSupplyRate = 0f };

            for (int i = 0; i < 100; i++) sim.Tick(0.02f); // 2초

            float dealt = 1000000f - sim.Enemies[0].hp;
            Assert.AreEqual(40f, dealt, D, "2발 × 20 = 40. 재고가 마르면 더 안 나간다");
            Assert.IsTrue(sim.AmmoStock <= 0f);
        }

        [Test]
        public void SupplyRefills_AndFiringResumes()
        {
            // 재고 0에서 시작해도 생산이 들어오면 다시 쏜다(창고가 버퍼 역할).
            var sim = new CombatSimulation(Robot(40f, 0f, 5f), Sandbag(), 6f, 120f, 0f)
            { AmmoSupplyRate = 5f };

            for (int i = 0; i < 100; i++) sim.Tick(0.02f); // 2초

            float dealt = 1000000f - sim.Enemies[0].hp;
            Assert.Greater(dealt, 0f, "생산이 들어오면 발사가 재개된다");
        }

        [Test]
        public void SupplyMatchingFireRate_KeepsFiringSteady()
        {
            // 생산 5 = 발사 5 → 재고가 유지되고 2초에 10발(200 피해)이 나간다.
            var sim = new CombatSimulation(Robot(40f, 40f, 5f), Sandbag(), 6f, 120f, 0f)
            { AmmoSupplyRate = 5f };

            for (int i = 0; i < 100; i++) sim.Tick(0.02f);

            float dealt = 1000000f - sim.Enemies[0].hp;
            Assert.AreEqual(200f, dealt, 20f, "생산이 소비를 따라가면 발사가 끊기지 않는다");
        }

        // ---- 저장 노드 흐름: 군수 → 저장 벨트 연결이 성립해야 한다 ----

        [Test]
        public void StorageNode_AcceptsAmmoFromMunitions()
        {
            // 2026-08-21 정정 전에는 저장 노드 포트가 Material이라 군수(Ammo) 출력과 kind가 안 맞아
            // 연결 자체가 성립하지 않았다.
            var grid = new BoardGrid(4, 4, 1f, UnityEngine.Vector2.zero);
            NodeDefinition muni = Node(NodeType.Munitions, new NodePort(PortFace.East, PortIO.Output, FlowKind.Ammo));
            NodeDefinition stor = Node(NodeType.Storage, new NodePort(PortFace.West, PortIO.Input, FlowKind.Ammo));

            grid.TryPlace(new UnityEngine.Vector2Int(0, 0), muni, out _);
            grid.TryPlace(new UnityEngine.Vector2Int(1, 0), stor, out _);

            List<BeltLink> links = BeltRouting.BuildLinks(grid);
            Assert.AreEqual(1, links.Count, "군수 → 저장 연결이 성립해야 한다");
            Assert.AreEqual(FlowKind.Ammo, links[0].kind);

            UnityEngine.Object.DestroyImmediate(muni);
            UnityEngine.Object.DestroyImmediate(stor);
        }

        private static NodeDefinition Node(NodeType type, params NodePort[] ports)
        {
            var n = UnityEngine.ScriptableObject.CreateInstance<NodeDefinition>();
            n.type = type;
            n.implemented = true;
            n.ports = new List<NodePort>(ports);
            return n;
        }
    }
}
