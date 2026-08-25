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

        // ---- 재고 자체 (탄종별 스택 · 총량 캡 공유 — 260824_V02 §2) ----

        [Test]
        public void Produce_AccumulatesUpToSharedCapacity()
        {
            var inv = new AmmoInventory(40f);
            inv.Produce(AmmoKind.Pierce, 1f, 6f);
            Assert.AreEqual(6f, inv.StockOf(AmmoKind.Pierce), D);
            Assert.AreEqual(6f, inv.Total, D);

            inv.Produce(AmmoKind.Pierce, 100f, 6f); // 한참 생산
            Assert.AreEqual(40f, inv.Total, D, "총량 캡을 넘는 분은 버려진다");
            Assert.IsTrue(inv.IsFull, "만재 — 태그·과부하 트리거의 기준");
        }

        [Test]
        public void TryConsume_FailsWhenInsufficient_AndDoesNotGoNegative()
        {
            var inv = new AmmoInventory(40f);
            inv.Add(AmmoKind.Pierce, 1f);

            Assert.IsTrue(inv.TryConsume(AmmoKind.Pierce, 1f));
            Assert.IsTrue(inv.IsEmpty);

            Assert.IsFalse(inv.TryConsume(AmmoKind.Pierce, 1f), "모자라면 실패");
            Assert.AreEqual(0f, inv.StockOf(AmmoKind.Pierce), D, "실패해도 음수로 내려가지 않는다");
        }

        /// <summary>
        /// 탄종별 스택의 핵심 계약: 폭발이 창고를 채워도 **관통은 나가지 않는다.**
        /// 단일 스칼라였다면 폭발 재고로 관통을 쏘게 되어 「관통 3발 남았는데 폭발을 쏜다」가 표현되지 않는다.
        /// </summary>
        [Test]
        public void TryConsume_DoesNotBorrowFromAnotherKind()
        {
            var inv = new AmmoInventory(40f);
            inv.Add(AmmoKind.Explosive, 40f);

            Assert.IsFalse(inv.TryConsume(AmmoKind.Pierce, 1f), "관통 재고가 0이면 폭발로 대신 쏘지 않는다");
            Assert.AreEqual(40f, inv.StockOf(AmmoKind.Explosive), D, "실패가 남의 스택을 깎지 않는다");
            Assert.IsTrue(inv.TryConsume(AmmoKind.Explosive, 1f), "제 탄종은 나간다");
        }

        /// <summary>
        /// **잠식 허용**(V02 §2): 용량 40은 셋이 나눠 쓴다. 한 탄종이 다 채우면 다른 탄종은 쌓을 자리가 없다.
        /// 무엇을 비축할지가 판단이 되는 지점이 여기다.
        /// </summary>
        [Test]
        public void Encroachment_OneKindFillsCapacity_OthersGetNoSpace()
        {
            var inv = new AmmoInventory(40f);
            inv.Add(AmmoKind.Explosive, 40f);
            Assert.AreEqual(0f, inv.FreeSpace, D);

            inv.Produce(AmmoKind.Pierce, 10f, 5f); // 50발어치 생산 시도
            Assert.AreEqual(0f, inv.StockOf(AmmoKind.Pierce), D, "자리가 없으면 한 발도 안 쌓인다");
            Assert.AreEqual(40f, inv.Total, D, "총량은 캡을 넘지 않는다");
        }

        /// <summary>
        /// 만충 분모는 **총합**이다. 탄종마다 따로 세면 태그 주기가 탄종 수만큼 늘어나
        /// 「주기 10초 = 저장 40 ÷ 대표 생산 4발/초」(밸런스 문서「태그 시스템 수치」)가 깨진다.
        /// </summary>
        [Test]
        public void IsFull_CountsTotalAcrossKinds_NotPerKind()
        {
            var inv = new AmmoInventory(40f);
            inv.Add(AmmoKind.Pierce, 10f);
            inv.Add(AmmoKind.Split, 10f);
            inv.Add(AmmoKind.Explosive, 20f);

            Assert.AreEqual(40f, inv.Total, D);
            Assert.IsTrue(inv.IsFull, "어느 탄종도 단독 40이 아니지만 총합이 40이면 만충이다");
            Assert.AreEqual(1f, inv.FillRatio, D);
        }

        [Test]
        public void FillRatio_IsTotalOverCapacity()
        {
            var inv = new AmmoInventory(40f);
            inv.Add(AmmoKind.Split, 10f);
            Assert.AreEqual(0.25f, inv.FillRatio, D);

            inv.Fill(AmmoKind.Split);
            Assert.AreEqual(1f, inv.FillRatio, D);

            inv.Drain();
            Assert.AreEqual(0f, inv.FillRatio, D);
            Assert.AreEqual(0f, inv.StockOf(AmmoKind.Split), D, "비우면 전 탄종이 비워진다");
        }

        [Test]
        public void Add_IsClampedToCapacity_AndIgnoresNegative()
        {
            var inv = new AmmoInventory(40f);
            inv.Add(AmmoKind.Pierce, 999f);
            Assert.AreEqual(40f, inv.Total, D);

            var other = new AmmoInventory(40f);
            other.Add(AmmoKind.Pierce, -5f);
            Assert.AreEqual(0f, other.Total, D);
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
