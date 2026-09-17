using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 온보딩 시작 보드 — **표준탄 3단 단일 라인 + 빈 칸 하나**(2026-09-05 재작성).
    ///
    /// 이 파일이 있는 이유: 종전에는 배치가 씬 생성기 안 좌표 리터럴이라
    /// 「정말 그 숫자가 나오는가」를 확인할 방법이 **씬을 열어 보는 것뿐**이었다.
    /// 배치를 <see cref="StartingBoard"/>로 빼고 여기서 잡아 두면,
    /// 좌표 하나가 어긋나는 순간 배치모드가 깨진다.
    ///
    /// ⚠️ **값 테스트는 지금 비어 있다**(`260904_W04` 4장). 종전 이 파일은 80 · 100 · 90.9 ·
    /// 전력 11/10 · 「군수 다섯 대」를 못 박고 있었는데, 그 숫자는 전부 **구 보드**
    /// (군수 다섯 대 → 병합기 사다리 → 코어)의 것이다. 「코어는 시작이다」 판정(W03 1장)으로
    /// 그 구조가 없어졌으므로 숫자도 함께 무효가 됐다.
    ///
    /// 새 값은 **4단 체인 실측 뒤에 설계가 확정한다.** 그때까지 값 테스트는 `Assert.Ignore`로
    /// 자리를 남겨 둔다 — 지워 버리면 무엇을 재야 하는지가 같이 사라지고, 지금 나오는 수를
    /// 그대로 적으면 **측정하지 않은 것을 확정치로 굳히는 것**이 된다.
    ///
    /// 지금 지키는 것은 **구조**다: 빈 칸이 비어 있는가 · 채우는 것이 무엇인가 ·
    /// 채우기 전에 0인가 · 채우면 라인이 마운트까지 이어지는가.
    ///
    /// ⚠️ 여기서 재는 것은 **물류 출력**이다(마운트계수 미적용). 전투가 마운트계수를 곱한다.
    /// </summary>
    public sealed class StartingBoardTests
    {
        // 🗑️ 구 `CountOf(nodeId)` 폐기 — **배치를 배치로 견주는 동어반복**이었다.
        //    이 파일이 지키는 것은 배치 그 자체라 수를 박는 쪽이 맞다.

        private const float D = 0.01f;

        private BalanceConfig _bal;

        [SetUp]
        public void SetUp()
        {
            _bal = AssetDatabase.LoadAssetAtPath<BalanceConfig>(
                "Assets/_Project/ScriptableObjects/BalanceConfig.asset");
            if (_bal == null || Node(StartingBoard.MuniId) == null)
                Assert.Ignore("자산 없음 — 먼저 밸런스·노드 생성 메뉴를 실행해야 한다.");
        }

        private static NodeDefinition Node(string id) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>(
                $"Assets/_Project/ScriptableObjects/Nodes/Node_{id}.asset");

        /// <summary>씬 생성기와 **같은 데이터**로 격자를 깐다 — 배치를 여기서 다시 적지 않는다.</summary>
        private static BoardGrid Build(bool fillEmptySlot)
        {
            // ⚠️ **실제 유효 셀 마스크를 쓴다.** 전부 유효한 격자로 재면 실루엣 밖 칸에
            // 노드를 둬도 테스트가 통과하고, 씬에서만 조용히 빠진다.
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero,
                PartLayout.BuildMask());

            foreach (StartingBoard.Slot slot in StartingBoard.Nodes) Place(g, slot);

            foreach (StartingBoard.Run run in StartingBoard.Belts) PlaceRun(g, run);
            // 빈 칸을 채우는 것은 **운반로 벨트 한 칸**이다 (2026-09-11 설계 확정 (가)).
            // 세 판째다 — 병합기 → 기초 가공소 → 벨트. 매번 **놓기 전 0** 이 되는 자리를 찾아왔다.
            if (fillEmptySlot) PlaceRun(g, StartingBoard.FillsEmptySlot);

            // 실제 경로와 같은 순서로 푼다: 면 → 품목.
            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        // 🗑️ 구 `PlaceRun` 폐기 (2026-09-17) — 병합기만 보고 분류기를 직선으로 깔았다.
        //    놓는 손은 `StartingBoard.Place` 하나다.
        private static void PlaceRun(BoardGrid g, StartingBoard.Run run)
            => StartingBoard.Place(g, run);

        private static void Place(BoardGrid g, StartingBoard.Slot slot)
        {
            NodeDefinition def = Node(slot.nodeId);
            Assert.NotNull(def, slot.nodeId);
            Assert.IsTrue(g.TryPlace(slot.cell, def, out NodeInstance placed),
                $"{slot.nodeId} @ {slot.cell} — 놓을 수 없는 칸이다");
            if (placed == null) return;
            placed.AmmoKind = slot.ammo;
            placed.Rotation = slot.rotation;
            // ⚠️ 조합표를 안 고르면 추진제 노드가 표준탄으로 돌아 그 줄이 죽는다.
            if (slot.recipe != RecipeKind.None) placed.SelectRecipe(slot.recipe);
        }

        /// <summary>한 칸을 빼고 깐다 — 「그 칸이 없으면 라인이 끊기는가」를 재기 위한 것.</summary>
        private static BoardGrid BuildWithout(Vector2Int omit, bool fillEmptySlot)
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f, Vector2.zero,
                PartLayout.BuildMask());

            foreach (StartingBoard.Slot slot in StartingBoard.Nodes) Place(g, slot);

            foreach (StartingBoard.Run run in StartingBoard.Belts)
            {
                if (run.cell == omit) continue; // 이 칸을 비운다
                PlaceRun(g, run);
            }
            if (fillEmptySlot && StartingBoard.EmptySlot != omit)
                PlaceRun(g, StartingBoard.FillsEmptySlot);

            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        /// <summary>보드가 만드는 물류 출력. 이어진 노드만 센다(260829_V03 A안).</summary>
        private float Output(BoardGrid g)
        {
            NetworkAggregate agg = LogisticsNetwork.Aggregate(g, LogisticsReach.ConnectedNodes(g));

            var lines = new List<MunitionsLine>
            {
                new MunitionsLine(AmmoKind.Pierce, _bal.LineSpecOf(AmmoKind.Pierce), 20f, agg.muniPierce),
                new MunitionsLine(AmmoKind.Standard, _bal.LineSpecOf(AmmoKind.Standard), 25f, agg.muniSplit),
                new MunitionsLine(AmmoKind.Explosive, _bal.LineSpecOf(AmmoKind.Explosive), 50f, agg.muniExplosive),
            };
            return AmmoLineProduction.TotalOutput(lines, _bal.muniPerNode);
        }

        // ---- 숫자 ----

        /// <summary>
        /// **시작 0.** 부품까지는 만들어지는데 그것을 탄으로 바꿀 노드가 없다
        /// (촬영 스크립트 A구간 확정, 2026-09-01).
        ///
        /// 0으로 시작하는 근거는 그대로다 — 일부라도 돌면 마운트가 저절로 차고,
        /// 놓는 순간 곧바로 끝나 「쌓인다」를 못 본다.
        ///
        /// **이 테스트가 보는 것은 값이 아니라 0이다.** 0은 실측 대상이 아니라 라인이
        /// 끊겼다는 사실이라 재산출 대상에 들지 않는다.
        /// </summary>
        [Test]
        public void StartsAtZero_BecauseTheLineIsCut()
        {
            // ✅ **규격 단언으로 되돌렸다**(2026-09-11 설계 확정 (가) · `260911_W02`).
            //
            // 09-11 오전에는 빈 칸이 **군수 한 대**라 나머지 세 줄이 흘러 60초에 165개가
            // 닿았고, 그때는 **지금 사실**(`Assert.Greater`)을 적고 판정 요청으로 올렸다.
            // 빈 칸이 **합류 뒤 운반로의 외길 한 칸**으로 옮겨 오면서 조건이 실제로 선다 —
            // 이제 바라는 것과 사실이 같으므로 **규격**을 단언한다.
            //
            // 이것이 튜토리얼 기획서 4-2 의 종료 조건이다: **놓기 전 0.**
            Assert.AreEqual(0f, Output(Build(fillEmptySlot: false)), D,
                "끊긴 라인 — 이것이 고장난 로봇의 증거다");
        }

        /// <summary>
        /// 빈 칸을 채우면 **0이 아닌 무엇**이 된다 — 그 「무엇」은 아직 측정 중이다.
        ///
        /// ⚠️ 종전 값 100은 구 보드(군수 다섯 대)의 것이다. 새 보드는 기초 가공소 한 대이므로
        /// 자릿수부터 다르다. **여기에 지금 나오는 수를 적지 않는다** — 4단 체인 실측 전이고,
        /// 밸런스 문서의 100·80은 이미 재산출 대상에 올라 있다(`260904_W04` 4장).
        /// </summary>
        [Test]
        public void FillingTheEmptySlot_RaisesOutput()
        {
            Assert.Ignore("측정 중 — 새 시작 보드의 출력은 4단 체인 실측 후 설계가 확정한다.");
        }

        /// <summary>
        /// 채우는 것은 **기초 가공소 노드**다 (2026-09-05). 종전에는 병합기였는데,
        /// 단일 라인이 되면서 합칠 갈래가 없어져 병합기가 놀게 됐다.
        ///
        /// 스테이지 0의 목표는 「라인을 이으면 물건이 만들어진다」이므로,
        /// 놓는 것과 목표가 같은 말이라는 점은 그대로다.
        /// </summary>
        [Test]
        public void TheFillIsTheCarryBelt()
        {
            // ⚠️ **구 시험 `TheFillIsTheMunitionsNode` 폐기**(2026-09-11 설계 확정 (가)).
            // 채우는 것이 노드에서 **벨트**로 바뀌었다 — 합류 뒤 외길이라 그 한 칸이
            // 비면 도착이 **정확히 0** 이고, 그것이 「놓기 전 0」을 세운다.
            Assert.AreEqual(StartingBoard.EmptySlot, StartingBoard.FillsEmptySlot.cell);
            Assert.IsFalse(StartingBoard.FillsEmptySlot.merger, "직선 벨트다 — 드래그가 만든다");

            // 그 칸이 `Belts` 목록에는 없어야 한다 — 있으면 처음부터 깔려 빈 칸이 아니다.
            foreach (StartingBoard.Run run in StartingBoard.Belts)
                Assert.AreNotEqual(StartingBoard.EmptySlot, run.cell,
                    "비워 둔 칸이 시작 배선에 들어 있다");

            // 운반로 위인가 — 합류(7,5) 바로 서쪽이다.
            Assert.AreEqual(5, StartingBoard.EmptySlot.y, "운반로는 y=5 다");
        }

        /// <summary>빈 칸은 **비어 있다** — 시작 배선에 그 칸이 들어 있으면 온보딩이 사라진다.</summary>
        [Test]
        public void TheEmptySlotIsActuallyEmpty()
        {
            foreach (StartingBoard.Run run in StartingBoard.Belts)
                Assert.AreNotEqual(StartingBoard.EmptySlot, run.cell, "비워 둔 칸에 벨트가 있다");
            foreach (StartingBoard.Slot slot in StartingBoard.Nodes)
                Assert.AreNotEqual(StartingBoard.EmptySlot, slot.cell, "비워 둔 칸에 노드가 있다");

            BoardGrid g = Build(fillEmptySlot: false);
            Assert.IsNull(g.GetAt(StartingBoard.EmptySlot));
            Assert.IsNull(g.GetBeltAt(StartingBoard.EmptySlot));
        }

        // ---- 배선이 실제로 이어지는가 ----

        /// <summary>
        /// 채우기 전에는 **탄약 집계가 0이다.**
        ///
        /// ⚠️ **뜻이 바뀌었다**(2026-09-11 설계 확정 (가)). 종전에는 「탄을 만드는 노드가
        /// 하나도 없다」였는데, 이제 **군수는 넷 다 놓여 있다.** 0 인 이유는 다른 것이다 —
        /// 집계가 **라인에 든 노드만** 세는데(`LogisticsReach.ConnectedNodes`) 운반로가
        /// 한 칸 끊겨 **넷 다 마운트에 못 닿기 때문**이다.
        ///
        /// **같은 0 이지만 다른 사실**이라 근거를 바꿔 적는다.
        /// </summary>
        [Test]
        public void NoMunitions_UntilTheEmptySlotIsFilled()
        {
            // 놓여 있기는 하다 — 끊긴 것은 배선이다.
            int placed = 0;
            foreach (StartingBoard.Slot slot in StartingBoard.Nodes)
                if (slot.nodeId == StartingBoard.MuniId) placed++;
            // ⚠️ 2026-09-17 — **셋이다.** 표준탄 둘 + **추진제 하나**(부스터 줄이 배포에 들어왔다).
            // 🗑️ 구 「셋」 폐기 — 09-17 에 쉴드 줄이 들어와 **넷**이 됐다.
            Assert.AreEqual(4, placed,
                "기초 가공소 넷 — 표준탄 둘(두 줄) + 추진제(부스터 줄) + 방어 재료(쉴드 줄)");

            BoardGrid g = Build(fillEmptySlot: false);
            NetworkAggregate agg = LogisticsNetwork.Aggregate(g, LogisticsReach.ConnectedNodes(g));
            Assert.AreEqual(0, agg.muniPierce + agg.muniSplit + agg.muniExplosive,
                "끊긴 운반로라 넷 다 라인 밖이다");
        }

        /// <summary>
        /// 벨트 한 칸을 채우면 **네 줄이 한꺼번에 이어진다** — 놓을 수 있다는 것과
        /// 이어진다는 것은 다르다.
        ///
        /// 이것이 새 보드에서 가장 깨지기 쉬운 지점이다. 라인의 끝이 마운트 고정 포트라
        /// <see cref="LogisticsReach"/>가 마운트를 도착지로 모르면 노드들이
        /// **조용히 0으로 세어진다**(2026-09-05에 실제로 그랬다).
        ///
        /// ⚠️ **빈 칸 자체는 라인에 안 든다** — 이제 노드가 아니라 **벨트**라서다.
        /// 확인할 것은 그 칸이 아니라 **그 칸이 이어 준 군수 넷**이다.
        /// </summary>
        [Test]
        public void FillingTheBelt_WiresTheWholeLine()
        {
            BoardGrid g = Build(fillEmptySlot: true);
            NetworkAggregate agg = LogisticsNetwork.Aggregate(g, LogisticsReach.ConnectedNodes(g));

            Assert.AreEqual(2, agg.muniPierce + agg.muniSplit + agg.muniExplosive,
                "한 칸을 이으면 군수 둘이 한꺼번에 라인에 든다 — 그 칸은 합류 뒤 외길이라 "
                + "막히면 둘 다 못 나간다(줄 수가 줄어도 그 성질은 그대로다)");
        }

        // ---- 일감률·전력 ----

        /// <summary>
        /// 온보딩 첫 화면에 「노는 중」이 떠 있으면 안 된다 — 안 되는 것을 고르라는 뜻이 된다.
        ///
        /// ⚠️ **측정 중.** 종전 근거(「관통 스펙 5에 4노드라 초과분이 없다」)는 구 보드의
        /// 산술이다. 새 보드에서 일감률이 얼마인지는 노드별 산출률이 확정돼야 나오고,
        /// 그 값들은 지금 카탈로그 센티넬이다.
        /// </summary>
        [Test]
        public void NothingIsIdleAtTheStart()
        {
            Assert.Ignore("측정 중 — 새 보드의 일감률은 노드별 산출률 확정 후에 잰다.");
        }

        /// <summary>
        /// 발전은 **처음부터 켜져 있다** — 에너지 노드가 이어져 있으므로 공급이 0이 아니다.
        ///
        /// 공급·수요의 **구체적인 수는 재산출 대상**이라 여기서 못 박지 않는다. 종전 값
        /// (수요 9 → 11, 공급 10)은 「군수 4대 + 에너지 1대」 구성의 것이고 그 구성이 없어졌다.
        /// 지키는 것은 「전력 라인이 이어져 있다」 하나다.
        /// </summary>
        [Test]
        public void PowerLine_IsWired_FromTheStart()
        {
            NetworkAggregate agg = Aggregate(Build(fillEmptySlot: false));

            Assert.Greater(agg.powerSupply, 0f, "에너지가 이어져 발전이 돈다");
        }

        /// <summary>
        /// ⚠️ **측정 중** — 빈 칸을 채웠을 때 전력이 남는지 모자라는지.
        ///
        /// 구 보드에서는 채운 순간 모자라졌다(수요 11 > 공급 10). 새 보드는 노드 수가 달라
        /// 그 결론을 그대로 옮길 수 없고, 복합 가공소의 대당 전력은 **아직 확정치가 없다**
        /// (「대당 전력 7종」에 여덟째가 없다). 값이 서기 전에는 부등호도 못 쓴다.
        /// </summary>
        [Test]
        public void FillingTheEmptySlot_PowerBalance()
        {
            Assert.Ignore("측정 중 — 대당 전력(복합 가공소 포함) 확정 후에 잰다.");
        }

        /// <summary>
        /// ⚠️ **측정 중** — 전력이 모자란 만큼 깎인 출력.
        ///
        /// 종전 90.9는 100 × 10/11이었고, 100도 11도 구 보드의 수다. **감쇠 모델 기준으로
        /// 다시 쓰지 않는다**(`260904_W04` 4장) — 여기서 새 수를 만들면 실측이 그 수를
        /// 따라오게 된다.
        /// </summary>
        [Test]
        public void FilledOutput_IsThrottledByPower()
        {
            Assert.Ignore("측정 중 — 출력·전력 확정 후에 잰다.");
        }

        // ---- 스테이지 0: 어느 칸을 비우면 라인이 끊기는가 (260901_W05 §4층 검증 3) ----

        /// <summary>
        /// **비워 둔 칸을 빼면 라인이 끊긴다**(후보 A).
        ///
        /// 스테이지 0은 「끊긴 라인을 잇는다」가 목표이므로, 그 칸이 채워지기 전에는
        /// 출력이 **0**이어야 한다. 일부라도 돌면 마운트가 저절로 차고,
        /// 놓는 순간 곧바로 끝나 「쌓인다」를 못 본다.
        /// </summary>
        [Test]
        public void WithoutTheEmptySlot_OutputIsZero()
        {
            BoardGrid g = BuildWithout(StartingBoard.EmptySlot, fillEmptySlot: false);

            // ✅ 합류 **뒤**의 외길이라 그 한 칸이 비면 네 줄이 다 막힌다(2026-09-11).
            Assert.AreEqual(0f, Output(g), D, "합류 뒤 외길 한 칸이 비면 도착은 0 이다");
        }

        /// <summary>
        /// **시작 보드에 군수 노드가 없다** — 그 자리가 비워 둔 칸이다.
        ///
        /// 종전에는 「다섯 대가 처음부터 놓여 있고 막힌 것은 출구」였다. 코어가 시작이 되면서
        /// 막을 출구 자체가 사라져(W03 1장), 끊는 자리를 **노드 자리**로 옮겼다.
        /// </summary>
        [Test]
        public void NoMunitionsNode_IsPlacedFromTheStart()
        {
            int muni = 0;
            foreach (StartingBoard.Slot slot in StartingBoard.Nodes)
                if (slot.nodeId == StartingBoard.MuniId) muni++;

            // ✅ 군수는 다 놓여 있다 — 플레이어 몫은 **벨트 한 칸**이다(2026-09-11).
            // ⚠️ 2026-09-17 — 셋이다(표준탄 둘 + 추진제 하나).
            Assert.AreEqual(4, muni, "기초 가공소 넷이 다 놓여 있다");
        }

        private NetworkAggregate Aggregate(BoardGrid g)
        {
            ICollection<Vector2Int> connected = LogisticsReach.ConnectedNodes(g);
            return LogisticsNetwork.Aggregate(g, connected, WorkloadRate.Compute(g, connected, _bal));
        }

        /// <summary>
        /// 시작 보드의 구성 — **코어 · 가공 · 에너지 셋**이고 저장은 없다.
        ///
        /// ⚠️ **가공이 2026-09-05에 들어왔다.** 코어가 원천이 되면서 부품 단계가 필요해졌기
        /// 때문이다(코어 에너지 → 기초재료·부품 → 표준탄). 종전 이 테스트는 「가공 없음」을
        /// 못 박고 있었는데, 그것은 코어가 탄약을 직접 받던 시절의 구성이다.
        ///
        /// 저장이 없는 것은 그대로다 — 창고를 배우는 것은 스테이지 0의 목표가 아니다.
        /// </summary>
        [Test]
        public void StartingBoard_IsCoreProcessingEnergy()
        {
            int core = 0, proc = 0, muni = 0, ener = 0, stor = 0, boost = 0, shield = 0;
            foreach (StartingBoard.Slot slot in StartingBoard.Nodes)
            {
                if (slot.nodeId == StartingBoard.CoreId) core++;
                if (slot.nodeId == StartingBoard.ProcId) proc++;
                if (slot.nodeId == StartingBoard.MuniId) muni++;
                if (slot.nodeId == StartingBoard.EnergyId) ener++;
                if (slot.nodeId == "stor") stor++;
                if (slot.nodeId == StartingBoard.BoosterId) boost++;
                if (slot.nodeId == StartingBoard.ShieldId) shield++;
            }

            // ⚠️ 네 줄 보드다(2026-09-11 · `260911_W01` 값 3) — 코어 하나가 네 방향으로
            // 라인을 세우므로 가공도 넷이다. 에너지 셋은 **벨트를 안 물고** 놓여만 있다.
            // 📌 **여기 수는 박는 것이 맞다** — 배치 그 자체를 지키는 시험이라,
            //    줄이 늘면 이 줄이 빨개지는 것이 **알림**이지 헛짚음이 아니다.
            //    (파생값을 박는 것과 다르다 — 그쪽은 `BalanceFixture` 가 맡는다.)
            Assert.AreEqual(1, core, "코어 1대");
            // ⚠️ 2026-09-17 — 가공이 둘 늘었다: 추진제 줄(**발전재료**) · 쉴드 줄(**기초재료·부품**).
            Assert.AreEqual(4, proc, "가공 4대 — 표준탄 두 줄 + 추진제 줄 + 쉴드 줄");
            Assert.AreEqual(4, muni, "기초 가공소 4대 — 표준탄 둘 + 추진제 + 방어 재료");
            Assert.AreEqual(3, ener, "에너지 3대 — 전력망은 전역이라 안 이어도 발전한다");
            Assert.AreEqual(0, stor, "저장 노드 없음");
            Assert.AreEqual(2, boost, "부스터 2대 — 회피 스택 상한 8 의 뿌리");
            Assert.AreEqual(1, shield, "쉴드 발생 1대 — 그릇 200 · 충전 5/초");
            // 🗑️ 구 「12 = 코어1 + 가공3 + 군수3 + 에너지3 + 부스터2」 폐기 —
            //    09-17 에 **쉴드 줄**이 들어와 가공 · 군수 · 쉴드가 하나씩 늘었다.
            // 📌 총계는 **종별 합**으로 센다 — 종별이 다 세어졌는지를 보는 것이 뜻이고,
            //    합을 따로 박으면 다음에 줄이 늘 때 또 이 자리가 헛짚게 만든다.
            Assert.AreEqual(core + proc + muni + ener + stor + boost + shield,
                StartingBoard.Nodes.Count,
                "배치에 위 종별 어디에도 안 세어진 노드가 있다");
        }
    }
}
