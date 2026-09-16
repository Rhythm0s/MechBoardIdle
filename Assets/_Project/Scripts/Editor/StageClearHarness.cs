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

        /// <summary>도착률을 뽑는 창. **제공자와 같아야 한다** — 다르면 다른 수가 나온다.</summary>
        private const float DeliverySampleSeconds = 0.1f;

        /// <summary>
        /// 제공자의 롤링 창 — **씬에서 읽은 값이다**(`Game.unity` · `rollingWindow: 60`).
        ///
        /// ⚠️⚠️ 처음에 0.5 초로 적었다가 고쳤다. 근거 없이 짧게 잡았던 것이고,
        /// 그 값으로는 **화면이 내는 것과 다른 수**가 나온다. 창은 값이지 취향이 아니다.
        ///
        /// ⚠️ 60 초 창은 120 초 판의 절반이라 **초반 구간은 아직 안 찬 평균**이다 —
        /// 그래서 「최고」로 본다(끝까지 간 판에서는 창이 찬 뒤의 값이 최고가 된다).
        /// </summary>
        private const float ProviderRollingSeconds = 60f;

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

            // 출력 축 — **게임과 같은 함수**로 낸다(2026-09-16 · §74-6 ②).
            // 종전에는 이 줄이 없어 「요구치 18 은 못 잰다」고 적어 두었다. 짓는 코드가
            // 제공자 안에 private 으로 있었고, 이제 `MunitionsLineFactory` 로 나왔다.
            var muniLines = new List<MunitionsLine>();
            float origin = robot.balanceRef != null ? robot.balanceRef.origin : 100f;
            float baseEff = MunitionsLineFactory.BaseOutput(robot, agg, muniLines);

            sb.AppendLine($"  이어진 노드 {connected.Count} · 탄약 생산 {agg.ammoProduce:F2} 발/초"
                          + $" · 생산 배율 {throttle.Scale:F2} · 코어 {(agg.hasCore ? "있음" : "**없음**")}");
            if (config == null) sb.AppendLine("  ⚠️ LogisticsConfig 가 없다 — 발열 문턱을 기본값으로 쓴다");

            // ── 끝까지 돌린다 ──────────────────────────────────────────────
            int steps = Mathf.RoundToInt(HardCapSeconds / Dt);
            float elapsed = 0f;
            float firstShotAt = -1f, firstArrivalAt = -1f;
            float peakArrival = 0f;
            // ① 육안 9차 — 「3.2초에 로봇 바로 옆에 스폰」을 가른다.
            //    **나타난 순간의 거리**를 찍는다 — 나중에 걸어온 거리와 섞으면 구분이 안 된다.
            var spawnLog = new List<string>();

            // ③ 육안 9차 — 「89.6초에 오른쪽 무리가 로봇 쪽으로 안 온다」.
            // 10 초마다 **살아 있는 적이 굳었는가**를 센다. 굳음 = 사거리 밖인데
            // 지난 표본 뒤로 **거의 안 움직였다**. 우회가 금지라(장갑형 길막) 막히면
            // 그대로 서는 것이 규칙이지만, **몇이 그렇게 되는지**는 아무도 안 셌다.
            var lastPos = new Dictionary<CombatEntity, Vector2>();
            var stuckLog = new List<string>();
            float nextStuckAt = 10f;
            int seenEnemies = 0;

            LogisticsResult lastResult = default;
            float peakActual = 0f;

            // ⚠️⚠️ **굴리지 않은 최고값으로 판정하면 안 된다**(2026-09-16 · 첫 판에서 잡았다).
            //    0.1 초 창에서는 한 틱에 몰려 도착한 것이 순간 50.0 으로 읽힌다 — 명목 20.0 의
            //    2.5 배다. 그것을 「요구치 18 을 넘겼다」의 근거로 쓰면 **없는 성능을 보고**하게 된다.
            //    제공자는 같은 값을 롤링 창으로 굴려 화면에 낸다 — 여기서도 같은 창을 쓴다.
            // ⚠️⚠️ **게임이 지나는 문을 그대로 지난다**(2026-09-16 · 사용자 보고 「도착은 하는데
            //    발사를 안 한다」). 종전 하네스는 매 틱 `SetFireLines` 를 **무조건** 불렀다 —
            //    그러면 `FireRateGate` 를 건너뛰므로 **게임과 다른 판을 재게 된다.**
            //    09-15 §7 등재: 「재는 것과 보는 것은 다른 일 — 프로브는 사람이 지나는 문을 안 지난다」.
            var fireGate = new FireRateGate();
            fireGate.Reset(1f);
            float nominalOutput = RobotOutput.Nominal(robot.weapons, 1f, robot.moduleMult);
            int reallocs = 0;

            var roll = new RollingWindow(1, ProviderRollingSeconds);
            var sample = new float[1];
            float peakRolled = 0f;
            int shots = 0;
            CombatResult result = CombatResult.InProgress;

            for (int i = 0; i < steps; i++)
            {
                // 1) 물류 — 실제 보드가 실제로 나른다
                BoardItemTick.Step(grid, flow, Dt, throttle.Scale);
                delivery.Observe(flow.PendingMountArrivals, DamageOf, Dt, MountOwner.RobotA);
                flow.ClearPendingMountArrivals();
                // ⚠️ **제공자와 같은 창(0.1초)으로 뽑는다** — 창이 다르면 다른 수가 나온다.
                if (delivery.TryDrain(DeliverySampleSeconds, out float deliveredRate))
                    lastResult = LogisticsSimulation.Compute(baseEff, throttle, deliveredRate, origin);
                for (int k = 0; k < SupplySignals.MountArrivalRate.Length; k++)
                    SupplySignals.MountArrivalRate[k] = delivery.RateOf((AmmoKind)k);

                SupplySignals.EnsureSlots(mount.SlotCount);
                for (int k = 0; k < mount.SlotCount; k++)
                {
                    SupplySignals.MountSlotItem[k] = mount.ItemAt(k);
                    SupplySignals.MountSlotAmount[k] = mount.AmountAt(k);
                }

                if (firstArrivalAt < 0f && mount.Total > 0f) firstArrivalAt = elapsed;

                // 2) 배분 — **러너의 `RefreshFireRate` 와 같은 순서·같은 문**이다.
                //    배율은 굴린 출력을 명목으로 나눈 값이고, 그것이 게이트의 첫 입력이다.
                if (nominalOutput > 0f)
                {
                    float fireScale = Mathf.Max(0f, lastResult.actual / nominalOutput);
                    if (fireGate.ShouldReallocate(fireScale, SupplySignals.ArrivalRateOf,
                            SupplySignals.MountStockOf, 0.01f))
                    {
                        ShotAllocator.AllocateRates(robot.weapons, robot.consumptionCap,
                            SupplySignals.ArrivalRateOf, SupplySignals.MountStockOf, lines);
                        sim.SetFireLines(lines);
                        reallocs++;
                    }
                }

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
                if (lastResult.actual > peakActual) peakActual = lastResult.actual;

                sample[0] = lastResult.actual;
                roll.TrySample(elapsed, sample);
                float rolled = roll.Average(0);
                if (rolled > peakRolled) peakRolled = rolled;

                if (sim.TotalEnemies > 0 && spawnLog.Count < 10)
                {
                    int live = 0;
                    foreach (CombatEntity e in sim.Enemies) live++;
                    if (live > seenEnemies)
                    {
                        int idx = 0;
                        foreach (CombatEntity e in sim.Enemies)
                        {
                            if (idx >= seenEnemies && spawnLog.Count < 10)
                                spawnLog.Add($"    #{idx} {elapsed:F2}초 · 거리 {(e.position - sim.RobotPosition).magnitude:F2}");
                            idx++;
                        }
                        seenEnemies = live;
                    }
                }

                if (elapsed >= nextStuckAt && stuckLog.Count < 12)
                {
                    int alive = 0, stuck = 0, inReach = 0;
                    foreach (CombatEntity e in sim.Enemies)
                    {
                        if (e == null || !e.IsAlive) continue;
                        alive++;

                        float d = (e.position - sim.RobotPosition).magnitude;
                        float reach = e.attackRange + 0.5f + e.radius;   // 로봇 반경은 setup 과 같다
                        if (d <= reach) { inReach++; lastPos[e] = e.position; continue; }

                        if (lastPos.TryGetValue(e, out Vector2 was)
                            && (e.position - was).magnitude < 0.05f) stuck++;
                        lastPos[e] = e.position;
                    }

                    stuckLog.Add($"    {elapsed:F0}초 · 살아있음 {alive} · 사거리 안 {inReach}"
                                 + $" · **굳음 {stuck}**");
                    nextStuckAt += 10f;
                }

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
            sb.AppendLine($"  재배분 {reallocs} 회 — 0 이면 라인이 시작값(빈 줄)으로 굳은 것이다");

            sb.AppendLine();
            sb.AppendLine("[굳은 적 — 10초마다 · 사거리 밖인데 안 움직인 수]");
            foreach (string line in stuckLog) sb.AppendLine(line);
            sb.AppendLine("  ⚠️ 우회는 금지다(장갑형 길막이 규칙) — 막히면 서는 것이 맞다.");
            sb.AppendLine("     물음은 「서는가」가 아니라 **「몇이 영영 서는가」**다.");

            sb.AppendLine();
            sb.AppendLine($"[스폰 거리 — 띠 {tuning.spawnRingMinTbd:F1}~{tuning.spawnRingMaxTbd:F1}]");
            foreach (string line in spawnLog) sb.AppendLine(line);
            sb.AppendLine("  ⚠️ 이것은 **나타난 순간**의 거리다 — 적은 그 뒤 로봇 쪽으로 걸어온다.");

            sb.AppendLine();
            sb.AppendLine("[출력 축 — 요구치 " + stage.req.ToString("F0") + "]");
            sb.AppendLine($"  명목 {baseEff:F1} · 배율 뒤 {(baseEff * throttle.Scale):F1}"
                          + $" · 실제(도착 기준) 최고 {peakActual:F1} · 마지막 {lastResult.actual:F1}");
            // ⚠️ 갭 분해는 **마지막 틱의 값**이다 — 판이 끝난 순간이라 도착이 0 이면
            //    갭이 통째로 명목만큼 잡힌다. 「어느 축이 막았나」의 방향만 읽고 크기는 안 읽는다.
            sb.AppendLine($"  갭 분해(마지막 틱) {lastResult.gap:F1} — 전력 {lastResult.gapPower:F1}"
                          + $" · 발열 {lastResult.gapHeat:F1} · 벨트 {lastResult.gapBelt:F1}");
            sb.AppendLine($"  굴린 값({ProviderRollingSeconds:F0}초 창 · 화면이 내는 것과 같은 방식) 최고 {peakRolled:F1}");
            sb.AppendLine(stage.req > 0f && peakRolled >= stage.req
                ? $"  ✅ 요구치 {stage.req:F0} 를 넘겼다 (굴린 최고 {peakRolled:F1})"
                : $"  ❌ **요구치 {stage.req:F0} 에 못 미쳤다** (굴린 최고 {peakRolled:F1})"
                  + " — 못 미치는 것 자체가 보고 내용이다");
            sb.AppendLine("  ⚠️ 판정은 **굴린 값**으로 한다 — 굴리지 않은 최고는 한 틱에 몰려 도착한");
            sb.AppendLine("     것이 스파이크로 읽힌 수라, 그것으로 판정하면 없는 성능을 보고하게 된다.");
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
