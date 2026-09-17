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

        // ── 회피 스택 상한 × 4칸 (2026-09-17 사용자 확정 · `260917_W04` 2장) ──

        [Test]
        public void 부스터_둘인_보드는_상한이_여덟이다()
        {
            // ⚠️ 상한은 **대수의 파생값**이다 — 시작 보드가 부스터 둘이므로 2 × 4 = 8.
            NetworkAggregate a = Aggregate(Build(MountOwner.RobotA, fillEmptySlot: true));
            Assert.AreEqual(2, a.boosterCount, "시험 전제가 깨졌다");

            var dodge = new DodgeSystem { BoosterCount = a.boosterCount };
            Assert.AreEqual(8, dodge.Capacity,
                "부스터 둘인데 상한이 8 이 아니다 — 계수가 4 로 안 서 있다");
        }

        [Test]
        public void 계수는_자산에서_온다()
        {
            // ⚠️⚠️ **하드코딩 금지**(`260917_W04` 2장). 구 값 2 는 `DodgeSystem` 에
            //    `const` 로 박혀 있어서 밸런스가 이 칸을 못 움직였다.
            var bal = AssetDatabase.LoadAssetAtPath<BalanceConfig>(
                "Assets/_Project/ScriptableObjects/BalanceConfig.asset");
            Assert.IsNotNull(bal, "BalanceConfig 자산이 없다 — 생성기를 먼저 돌린다");

            Assert.AreEqual(4, bal.dodgeStacksPerBooster,
                "params dodgeStacksPerBooster = 4 (밸런스「회피와 기동 라인」)");
            Assert.AreEqual(bal.dodgeStacksPerBooster, DodgeSystem.DefaultStacksPerBooster,
                "코드 기본값이 자산과 갈렸다 — 자산을 못 읽는 판에서 다른 수가 나온다");
        }

        [Test]
        public void 상한_계수는_세대_표식을_안_바꾼다()
        {
            // 설계가 물은 것 — 「상한 개정으로 세대 표식은 바뀌지 않아야 한다」.
            // 📌 표식은 **배치**에서 뽑는다. 계수는 배치가 아니므로 닿지 않는다.
            //    이 시험은 그 사실을 **값으로** 못 박는다 — 나중에 누가 표식에 전투 값을
            //    섞으면 여기서 걸리고, 그때 사용자 저장이 조용히 버려지는 것을 막는다.
            int before = DodgeSystem.StacksPerBooster;
            try
            {
                string a1 = StartingBoard.Generation;
                string b1 = StartingBoardB.Generation;

                DodgeSystem.StacksPerBooster = 2;
                Assert.AreEqual(a1, StartingBoard.Generation, "A 표식이 계수를 따라 바뀌었다");
                Assert.AreEqual(b1, StartingBoardB.Generation, "B 표식이 계수를 따라 바뀌었다");

                DodgeSystem.StacksPerBooster = 9;
                Assert.AreEqual(a1, StartingBoard.Generation);
                Assert.AreEqual(b1, StartingBoardB.Generation);
            }
            finally { DodgeSystem.StacksPerBooster = before; }
        }

        [Test]
        public void 회피_게이지_눈금은_상한을_따른다()
        {
            // 설계가 물은 것 — 「눈금 수는 상한을 따른다 · 부스터 5대면 눈금 20개」.
            // 📌 **코드는 이미 열에서 접는다**(`HudMeters.MaxTicks`) — 20 개가 서는 일은 없다.
            //    상한 8 은 열 밑이라 **눈금 여덟**이 그대로 선다.
            Assert.AreEqual(8, HudMeters.TickCount(8), "상한 8 인데 눈금이 여덟이 아니다");
            Assert.AreEqual(4, HudMeters.TickCount(4), "부스터 한 대면 눈금 넷");
            Assert.LessOrEqual(HudMeters.TickCount(20), 10,
                "상한 20 에서 눈금이 열을 넘었다 — 고정 길이 바가 못 읽힌다");
            Assert.AreNotEqual(string.Empty, HudMeters.OverflowTag(20),
                "열을 넘겼는데 꼬리표가 없다 — 접힌 것을 화면이 안 말한다");
        }

        // ── 쉴드 줄 배포 (2026-09-17 사용자 확정 · `260917_W07` 4장) ──

        [Test]
        public void 두_판_다_쉴드_발생_하나와_방어_재료를_낸다()
        {
            // ⚠️⚠️ **부스터 줄과 같은 함정이 걸리는 자리다.** 줄이 놓이기만 하고 군수가
            //    표준탄으로 남으면 방어 재료가 **한 개도 안 나오고** 쉴드는 영영 빈 그릇이 된다 —
            //    에러도 경고도 없이 최대치만 200 으로 뜬다.
            //    그래서 보는 것은 배치가 아니라 **집계**다.
            foreach (MountOwner owner in new[] { MountOwner.RobotA, MountOwner.RobotB })
            {
                NetworkAggregate a = Aggregate(Build(owner, fillEmptySlot: true));

                Assert.AreEqual(1, a.shieldNodeCount,
                    $"{owner} 판의 쉴드 발생 노드가 하나가 아니다 — 안 이어졌으면 집계에서 빠진다");
                Assert.Greater(a.shieldMaterialProduce, 0f,
                    $"{owner} 방어 재료 산출이 0 이다 — 군수가 기본값(표준탄)으로 돌고 있다");
            }
        }

        [Test]
        public void 쉴드_그릇은_노드_수를_따른다()
        {
            // 최대치 = 노드 수 × 대당(200). 발생 하나면 **200** 이고, 충전률은 그 2.5%(5/초)다.
            var bal = AssetDatabase.LoadAssetAtPath<BalanceConfig>(
                "Assets/_Project/ScriptableObjects/BalanceConfig.asset");
            Assert.IsNotNull(bal, "BalanceConfig 자산이 없다");

            NetworkAggregate a = Aggregate(Build(MountOwner.RobotA, fillEmptySlot: true));
            float max = ShieldSystem.MaxFrom(a.shieldNodeCount, bal.shieldMaxPerNode);

            Assert.AreEqual(200f, max, 0.001f, "발생 하나인데 그릇이 200 이 아니다");
            Assert.AreEqual(5f, ShieldSystem.ChargeFrom(max, bal.shieldChargeRatioPerSec,
                a.shieldMaterialProduce, a.shieldNodeCount, bal.shieldMaterialPerSec), 0.001f,
                "충전률이 5/초가 아니다 — 재료가 모자라거나 비율이 갈렸다");
        }

        [Test]
        public void 쉴드_줄이_들어오며_두_판의_세대가_바뀌었고_까닭이_판마다_적힌다()
        {
            // ⚠️⚠️ **사용자가 감수한 자리다** — A · B 저장이 한 번씩 버려진다
            //    (`260917_W07` 4장 2번). 지키는 것은 「버려진다」가 아니라
            //    **「왜 버렸는지가 판마다 따로 적힌다」**이다.
            foreach (MountOwner owner in new[] { MountOwner.RobotA, MountOwner.RobotB })
            {
                var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero,
                    PartLayout.BuildMask(), owner);

                var stale = new BoardStateV1
                {
                    columns = g.Columns, rows = g.Rows,
                    owner = (int)owner,
                    generation = "구세대-쉴드줄-이전",
                };

                Assert.IsFalse(BoardStateCodec.Fits(stale, g), $"{owner} 옛 저장이 그대로 맞는다");

                string why = BoardStateCodec.WhyNotFit(stale, g);
                StringAssert.Contains("세대가 다르다", why, $"{owner} 까닭이 세대가 아니다 — {why}");
                StringAssert.Contains(owner.ToString(), why,
                    $"까닭에 어느 판인지가 없다 — A · B 가 한 줄로 뭉치면 무엇이 버려졌는지 못 읽는다");
            }
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
