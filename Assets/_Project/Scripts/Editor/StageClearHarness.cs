using System.Collections.Generic;
using System.Text;
using MBI.Core;
using MBI.Core.Combat;
using MBI.Data;
using UnityEditor;
using UnityEngine;

namespace MBI.EditorTools
{
    /// <summary>
    /// 【하네스】 스테이지를 **끝까지** 돌린다 — S1 요구치 18 재측정 (2026-09-16 · `260916_W01` 6장 #1).
    ///
    /// ⚠️⚠️ **왜 다시 재나.** 문서에 실린 「S1 Win 95.9초」 계열은 **접촉 사거리 수정 전** 판에서
    /// 잰 값이고, 그 뒤 **시작 보드가 4줄 → 2줄**로 바뀌었다(09-15 사용자 확정). 즉 지금 판을
    /// 잰 값이 하나도 없다. 요구치도 36 → 18 로 바뀌었다.
    ///
    /// ⚠️⚠️ **하네스와 산수를 가른다**(`260916_W01` §8 등재). 여기서 나온 수만 「실측」이라 적는다.
    /// 종전에 「100/100/107/103초」를 실측처럼 적었다가 정정한 적이 있다 — 그것은 산수였다.
    ///
    /// 📌 **게임이 세우는 판과 같은 판을 세운다.** 적은 <see cref="StageSpawnFactory"/> 가 짓고
    /// (러너와 **같은 함수**다), 보드는 `StartingBoard` + 튜토리얼 칸을 채운 것이다.
    /// 프로브가 제 판을 따로 지으면 **사람이 지나는 문을 안 지나게 된다**(09-15 에 여러 번 겪었다).
    ///
    /// ⚠️ **값을 하나도 안 지어낸다** — 로봇·튜닝·스테이지·밸런스 전부 자산에서 읽는다.
    /// 자산이 없으면 그 사실을 적고 멈춘다.
    ///
    /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunBatch</c>
    /// </summary>
    public static class StageClearHarness
    {
        private const string SoRoot = "Assets/_Project/ScriptableObjects";
        private const string NodeRoot = SoRoot + "/Nodes";

        /// <summary>한 틱. 게임은 프레임마다 돌지만 재는 쪽은 고정 간격이어야 되풀이가 된다.</summary>
        private const float Dt = 0.05f;

        /// <summary>안 끝나면 여기서 끊는다. ⚠️ **끊긴 것은 「졌다」가 아니다** — 따로 적는다.</summary>
        private const float HardCapSeconds = 600f;

        [MenuItem("MBI/Harness S1 Clear")]
        public static void RunMenu() => Debug.Log(Run("S1"));

        public static void RunBatch()
        {
            Debug.Log(Run("S1"));
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string Run(string stageId)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== 스테이지 {stageId} 클리어 하네스 (2026-09-16) ===");

            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>($"{SoRoot}/Stages/Stage_{stageId}.asset");
            var robot = AssetDatabase.LoadAssetAtPath<RobotDefinition>($"{SoRoot}/Robots/Robot_A.asset");
            var tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>($"{SoRoot}/CombatTuning.asset");

            if (stage == null || robot == null || tuning == null)
            {
                sb.AppendLine("자산이 없다 — 'MBI/Generate Combat Data' 먼저.");
                sb.AppendLine($"  stage={stage != null} robot={robot != null} tuning={tuning != null}");
                return sb.ToString();
            }

            var catalog = new List<EnemyDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyDefinition"))
                catalog.Add(AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(guid)));

            // ── 판 세우기 ──────────────────────────────────────────────────
            BoardGrid grid = Board();
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);
            var delivery = new MountDelivery();

            SupplySignals.Reset();
            SupplySignals.HasCombat = true;
            SupplySignals.ActiveOwner = MountOwner.RobotA;

            float ammoCapacity = robot.balanceRef != null ? robot.balanceRef.storeCapacity : 40f;
            float stack = robot.balanceRef != null ? robot.balanceRef.mountStackLimit : 10f;
            var store = new AmmoInventory(ammoCapacity);
            var mount = new MountLoad(MountLoad.SlotsRobotA, MountLoad.StandardStacks(stack));

            var lines = new List<AmmoLine>();
            ShotAllocator.AllocateRates(robot.weapons, robot.consumptionCap,
                SupplySignals.ArrivalRateOf, SupplySignals.MountStockOf, lines);

            // 마운트계수는 스테이지의 축이 정한다 — 러너 `Begin` 과 같은 규칙이다.
            float mountCoef = stage.powerModel == StagePowerModel.Logistics
                ? robot.mountCoef : robot.enhancedMountCoef;

            var setup = new RobotSetup
            {
                hp = tuning.robotHpTbd,
                mountCoef = mountCoef,
                moduleMult = robot.moduleMult,
                attackRange = tuning.robotAttackRangeTbd,
                radius = 0.5f,
                multiShotCount = tuning.multiShotCountTbd,
                aoeRadius = tuning.aoeRadiusTbd,
                aoeSplashFactor = tuning.aoeSplashFactorTbd,
                lines = new List<AmmoLine>(lines),
                ammoCapacity = ammoCapacity,
                ammoStore = store,
                mount = mount,
            };

            // ⚠️ **빈손으로 시작한다.** 창고와 마운트를 미리 채우면 「보드가 대는가」가 안 재진다 —
            //    이 하네스의 물음이 바로 그것이다(2줄 보드로 18 을 넘기는가).
            List<EnemySpawn> spawns = StageSpawnFactory.Build(stage, catalog, tuning);

            var sim = new CombatSimulation(setup, spawns,
                tuning.arenaRadiusTbd, stage.challengeTime, tuning.spawnCadenceTbd);

            // ⚠️ **띠를 러너와 같게 세운다.** 안 세우면 적이 기본 거리에 한 줄로 서고,
            //    그러면 「가까운 것은 제자리에서 쏘고 먼 것에는 걸어간다」가 안 일어난다 —
            //    게임과 다른 판을 재게 된다(2026-09-15 사용자 확정 · §72-40).
            if (tuning.spawnRingMinTbd > 0f && tuning.spawnRingMaxTbd > 0f)
                sim.SetSpawnBand(tuning.spawnRingMinTbd, tuning.spawnRingMaxTbd);
            else if (tuning.spawnRingRadiusTbd > 0f)
                sim.SetSpawnRing(tuning.spawnRingRadiusTbd);
            else
                sb.AppendLine("  ⚠️ 스폰 띠·링이 튜닝에 없다 — 시뮬 기본값으로 돈다(게임과 다를 수 있다)");

            sb.AppendLine();
            sb.AppendLine("[판] " + stage.stageId + " · " + stage.topic);
            sb.AppendLine($"  요구치 {stage.req:F0} · 제한 {stage.challengeTime:F0}s · 적 {spawns.Count} 기");
            sb.AppendLine($"  로봇 HP {tuning.robotHpTbd:F0} · 사거리 {tuning.robotAttackRangeTbd:F1}"
                          + $" · 마운트계수 {mountCoef:F2} · 소비 상한 {robot.consumptionCap:F1}");
            sb.AppendLine($"  보드 = 시작 배치 + 튜토리얼 칸(운반로 이어진 판) · 창고·마운트 **빈손 시작**");

            // ── 보드가 내는 값 — **게임과 같은 순수 함수로 낸다** ────────────
            //
            // ⚠️⚠️ **여기가 빠져서 첫 판이 0 발로 끝났다**(2026-09-16 · 내 하네스 결함).
            //    도착률은 2.00 발/초로 찍히는데 마운트는 0 이었다 — 도착이 **창고를 거쳐**
            //    마운트로 실리는데(`RefillMount`), 창고를 채우는 `AmmoSupplyRate` 를 안 먹였다.
            //    「재는 장치가 게임의 한 단을 건너뛰면 없는 판을 재게 된다」의 실례다.
            //
            // 📌 값을 짓지 않는다 — `LogisticsOutputProvider` 가 부르는 것과 **같은 함수**다.
            //    보드가 이 하네스 안에서는 안 바뀌므로 한 번만 낸다.
            var config = AssetDatabase.LoadAssetAtPath<LogisticsConfig>($"{SoRoot}/LogisticsConfig.asset");
            ICollection<Vector2Int> connected = LogisticsReach.ConnectedNodes(grid);
            WorkloadRate.Result work = WorkloadRate.Compute(grid, connected, robot.balanceRef);
            NetworkAggregate agg = LogisticsNetwork.Aggregate(grid, connected, work);

            float heatThreshold = config != null ? config.heatThreshold : 12f;
            ProductionThrottle throttle = LogisticsSimulation.Throttles(
                agg.powerSupply, agg.powerDraw,
                agg.heatGenerate, config != null ? config.moduleCoolingTbd : 0f, heatThreshold);

            sb.AppendLine($"  이어진 노드 {connected.Count} · 탄약 생산 {agg.ammoProduce:F2} 발/초"
                          + $" · 생산 배율 {throttle.Scale:F2} · 코어 {(agg.hasCore ? "있음" : "**없음**")}");
            if (config == null) sb.AppendLine("  ⚠️ LogisticsConfig 가 없다 — 발열 문턱을 기본값으로 쓴다");

            // ── 끝까지 돌린다 ──────────────────────────────────────────────
            int steps = Mathf.RoundToInt(HardCapSeconds / Dt);
            float elapsed = 0f;
            float firstShotAt = -1f, firstArrivalAt = -1f;
            float peakArrival = 0f;
            int shots = 0;
            CombatResult result = CombatResult.InProgress;

            for (int i = 0; i < steps; i++)
            {
                // 1) 물류 — 실제 보드가 실제로 나른다
                BoardItemTick.Step(grid, flow, Dt, throttle.Scale);
                delivery.Observe(flow.PendingMountArrivals, DamageOf, Dt, MountOwner.RobotA);
                flow.ClearPendingMountArrivals();
                delivery.TryDrain(1f, out _);
                for (int k = 0; k < SupplySignals.MountArrivalRate.Length; k++)
                    SupplySignals.MountArrivalRate[k] = delivery.RateOf((AmmoKind)k);

                SupplySignals.EnsureSlots(mount.SlotCount);
                for (int k = 0; k < mount.SlotCount; k++)
                {
                    SupplySignals.MountSlotItem[k] = mount.ItemAt(k);
                    SupplySignals.MountSlotAmount[k] = mount.AmountAt(k);
                }

                if (firstArrivalAt < 0f && mount.Total > 0f) firstArrivalAt = elapsed;

                // 2) 배분 — 러너가 매 틱 하는 일과 같다
                ShotAllocator.AllocateRates(robot.weapons, robot.consumptionCap,
                    SupplySignals.ArrivalRateOf, SupplySignals.MountStockOf, lines);
                sim.SetFireLines(lines);

                // 3) 창고 유입 — 러너가 매 틱 하는 일(`_sim.AmmoSupplyRate = AmmoProduce`).
                //    이것이 있어야 도착분이 창고를 거쳐 마운트로 실린다.
                sim.AmmoSupplyRate = agg.ammoProduce;
                sim.DroneInflowRate = agg.droneProduce;
                sim.PropellantSupplyRate = agg.propellantProduce;
                sim.BoosterCount = agg.boosterCount;

                // 4) 전투
                sim.Tick(Dt);
                elapsed += Dt;

                int fired = sim.ShotsThisTick != null ? sim.ShotsThisTick.Count : 0;
                if (fired > 0)
                {
                    shots += fired;
                    if (firstShotAt < 0f) firstShotAt = elapsed;
                }

                float arrival = 0f;
                for (int k = 0; k < SupplySignals.MountArrivalRate.Length; k++)
                    arrival += SupplySignals.MountArrivalRate[k];
                if (arrival > peakArrival) peakArrival = arrival;

                result = sim.Result;
                if (result != CombatResult.InProgress) break;
            }

            // ── 보고 ──────────────────────────────────────────────────────
            sb.AppendLine();
            sb.AppendLine("[결과]");
            bool capped = result == CombatResult.InProgress;
            sb.AppendLine(capped
                ? $"  ⚠️ **안 끝났다** — {HardCapSeconds:F0}초에서 끊었다. 「졌다」가 아니다."
                : $"  {result} · {elapsed:F1}초");
            sb.AppendLine($"  남은 적 {sim.Remaining}/{sim.TotalEnemies} · 로봇 HP {sim.Robot.hp:F0}/{sim.Robot.maxHp:F0}");
            sb.AppendLine($"  쏜 발 {shots}");
            sb.AppendLine(firstArrivalAt >= 0f
                ? $"  첫 도착 {firstArrivalAt:F1}초"
                : "  첫 도착 **없음** — 마운트에 한 알도 안 닿았다(운반로가 끊겼다)");
            sb.AppendLine(firstShotAt >= 0f
                ? $"  첫 발사 {firstShotAt:F1}초"
                : "  첫 발사 **없음**");

            sb.AppendLine($"  마운트 최고 도착률 {peakArrival:F2} 발/초");

            sb.AppendLine();
            sb.AppendLine("[요구치 18 — 이 하네스는 못 잰다]");
            sb.AppendLine($"  이 판의 요구치는 {stage.req:F0}(출력 축)이다.");
            sb.AppendLine("  ⚠️ **출력은 여기서 안 잰다.** 그 값은 `LogisticsOutputProvider`(MonoBehaviour ·");
            sb.AppendLine("     롤링 평균)가 내는 것이라 EditMode 에서 안 돈다. 0 을 적어 「못 미쳤다」고");
            sb.AppendLine("     보고하면 **없는 측정을 한 것**이 된다 — 그래서 안 적는다.");
            sb.AppendLine("  📌 여기서 나온 수는 **클리어 여부 · 소요 초 · 도착 · 발사**뿐이다.");

            return sb.ToString();
        }

        private static float DamageOf(AmmoKind kind)
        {
            var robot = AssetDatabase.LoadAssetAtPath<RobotDefinition>($"{SoRoot}/Robots/Robot_A.asset");
            if (robot == null) return 0f;
            foreach (WeaponSpec w in robot.weapons) if (w.kind == kind) return w.damagePerShot;
            return 0f;
        }

        /// <summary>시작 보드 + 튜토리얼 칸 — **운반로가 이어진 판**이다.</summary>
        private static BoardGrid Board()
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f,
                Vector2.zero, PartLayout.BuildMask());
            foreach (StartingBoard.Slot s in StartingBoard.Nodes)
                g.TryPlace(s.cell, Node(s.nodeId), out _);
            foreach (StartingBoard.Run r in StartingBoard.Belts) Place(g, r);
            Place(g, StartingBoard.FillsEmptySlot);
            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        private static void Place(BoardGrid g, StartingBoard.Run run)
        {
            if (run.merger)
                g.TryPlaceBeltElement(run.cell, BeltElementKind.Merger,
                    StartingBoard.MergerInFaces(run.outFace), new[] { run.outFace },
                    FlowKind.None, out _);
            else
                g.TryPlaceBelt(run.cell, run.inFace, run.outFace, FlowKind.None, out _);
        }

        private static NodeDefinition Node(string id)
            => AssetDatabase.LoadAssetAtPath<NodeDefinition>(NodeRoot + "/Node_" + id + ".asset");
    }
}
