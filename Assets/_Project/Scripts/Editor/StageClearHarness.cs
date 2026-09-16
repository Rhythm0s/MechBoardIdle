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
        public static void RunMenu() => Debug.Log(RunBoth("S1"));

        public static void RunBatch()
        {
            Debug.Log(RunBoth("S1"));
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **시작 조건 둘을 각각 돌린다** (2026-09-16 · 설계 요청 · `260916_W03` 2장).
        ///
        /// ⚠️⚠️ **여태 잰 것은 「빈손」 하나뿐이었다.** 그런데 플레이어가 S1 에 들어서는
        /// 정상 경로는 **튜토리얼을 끝낸 상태**이며, 그때 마운트는 이미 차 있다.
        /// 빈손만 재면 **첫 6초를 못 쏘는 판**을 정상 판으로 착각하게 된다.
        ///
        /// 📌 **둘을 다 남긴다** — 심사자 바로가기(S1 점프)는 실제로 빈손으로 들어가므로
        /// 그 조건도 여전히 재야 한다. 어느 쪽 수인지를 표에 적는 것이 이 함수의 일이다.
        /// </summary>
        public static string RunBoth(string stageId)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Run(stageId, preloadMount: true));
            sb.AppendLine();
            sb.AppendLine(Run(stageId, preloadMount: false));
            return sb.ToString();
        }

        /// <summary>⚠️ 인자 없는 옛 길 — **튜토리얼 종료 조건**으로 돈다(정상 경로).</summary>
        public static string Run(string stageId) => Run(stageId, preloadMount: true);

        public static string Run(string stageId, bool preloadMount)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== 스테이지 {stageId} 클리어 하네스 (2026-09-16) ===");
            sb.AppendLine(preloadMount
                ? "[시작 조건] **튜토리얼 종료** — 마운트 적재 참(정상 경로) · 창고 빈손"
                : "[시작 조건] **심사자 S1 점프** — 빈 칸 자동 채움 · 창고·마운트 **빈손**");

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

            // ⚠️⚠️ **시작 조건이 둘이다**(2026-09-16 · 설계 요청 · `260916_W03` 2장).
            //
            // · `preloadMount: true` — **튜토리얼 종료 상태**. 플레이어가 S1 에 들어서는
            //   정상 경로다. 튜토리얼이 마운트를 채우고 끝나므로 **빈손이 아니다.**
            // · `preloadMount: false` — **심사자 S1 점프**. 빈 칸이 자동으로 채워지고
            //   창고·마운트는 빈손이다(`ReviewerShortcuts`).
            //
            // 🗑️ 구 주석 「빈손으로 시작한다 — 보드가 대는가를 재려면」은 **반만 맞았다.**
            //    빈손 판은 「0 에서 대는가」를 재고, 적재 판은 「이미 찬 뒤 유지되는가」를
            //    잰다. 둘은 다른 물음이고 **정상 경로는 뒤쪽**이다.
            if (preloadMount)
            {
                // 마운트를 표준탄으로 가득 채운다 — 용량 그대로(A 4칸 × 스택 10 = 40).
                // ⚠️ 창고는 **안 채운다.** 튜토리얼은 마운트까지 나르고 끝나며,
                //    창고 재고는 그 뒤 보드가 대는 것이다 — 지어 넣으면 판이 물러진다.
                mount.Load(MountItem.Standard, mount.SlotCount * stack);
            }
            List<EnemySpawn> spawns = StageSpawnFactory.Build(stage, catalog, tuning);

            var sim = new CombatSimulation(setup, spawns,
                tuning.arenaRadiusTbd, stage.challengeTime, tuning.spawnCadenceTbd);

            // ⚠️ **띠를 러너와 같게 세운다.** 안 세우면 적이 기본 거리에 한 줄로 서고,
            //    그러면 「가까운 것은 제자리에서 쏘고 먼 것에는 걸어간다」가 안 일어난다 —
            //    게임과 다른 판을 재게 된다(2026-09-15 사용자 확정 · §72-40).
            // 게임과 같은 값으로 곁눈질을 붙든다 — 안 넣으면 떨림 잣대가 다른 판을 잰다.
            sim.SetSideStepHold(tuning.enemySideStepHoldTbd);

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
            // ⚠️ 시작 적재는 조건마다 다르다 — 머리의 [시작 조건] 줄과 **같은 말을 해야 한다**.
            //    한쪽만 고치면 표가 거짓말을 한다(2026-09-16).
            sb.AppendLine("  보드 = 시작 배치 + 튜토리얼 칸(운반로 이어진 판) · "
                          + (preloadMount ? "마운트 **가득**(40) · 창고 빈손" : "창고·마운트 **빈손**"));

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

            // ⚠️ **떨림 잣대**(2026-09-16 설계 완화 · §74-12 B) — 같은 적이 곁눈질 방향을
            //    몇 번 뒤집는가. 붙들기가 없으면 두 축이 비슷할 때 프레임마다 뒤집혀
            //    **제자리에서 떠는 것처럼** 보인다. 수가 크면 붙들기가 모자란 것이다.
            var lastSide = new Dictionary<CombatEntity, Vector2>();
            int sideFlips = 0;
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

            // ── 못 쏜 틱을 까닭별로 가른다 (2026-09-16 · 설계 요청 · `260916_W03` 2장 ②) ──
            //
            // **물음** — 쏜 발이 공급의 절반이다(48초에 43발 ≈ 0.9 발/초 vs 공급 2.0).
            // 후보 셋 중 어느 것인가: 사거리 밖 대기 / 창고→마운트 이송 / 발사 게이트.
            //
            // 📌 **틱마다 하나씩만 센다** — 겹쳐 세면 합이 뜻을 잃는다. 차례가 곧 인과다:
            //    표적이 없으면 재고를 봐도 소용없고, 재고가 없으면 게이트를 봐도 소용없다.
            int tickNoTarget = 0, tickNoAmmo = 0, tickGated = 0, tickFired = 0;
            float peakStore = 0f, peakMount = 0f, mountSum = 0f;
            int mountSamples = 0;

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

                // 못 쏜 까닭 — 차례가 인과다(표적 → 재고 → 게이트).
                if (fired > 0) tickFired++;
                else if (!sim.AimDirection.HasValue) tickNoTarget++;
                else if (mount.Total <= 0f) tickNoAmmo++;
                else tickGated++;

                if (sim.AmmoStock > peakStore) peakStore = sim.AmmoStock;
                if (mount.Total > peakMount) peakMount = mount.Total;
                mountSum += mount.Total;
                mountSamples++;

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

                        if (e.sideStepDir != Vector2.zero)
                        {
                            if (lastSide.TryGetValue(e, out Vector2 prevSide)
                                && prevSide != Vector2.zero && prevSide != e.sideStepDir) sideFlips++;
                            lastSide[e] = e.sideStepDir;
                        }

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
            sb.AppendLine("[못 쏜 까닭 — 틱마다 하나씩 · 차례가 인과다]");
            int totalTicks = tickFired + tickNoTarget + tickNoAmmo + tickGated;
            if (totalTicks <= 0) totalTicks = 1;
            sb.AppendLine($"  쏜 틱             {tickFired,6} ({tickFired * 100f / totalTicks:F1}%)");
            sb.AppendLine($"  표적 없음         {tickNoTarget,6} ({tickNoTarget * 100f / totalTicks:F1}%)"
                          + "  ← 사거리 밖 대기");
            sb.AppendLine($"  표적 있고 재고 0  {tickNoAmmo,6} ({tickNoAmmo * 100f / totalTicks:F1}%)"
                          + "  ← 창고→마운트 이송");
            sb.AppendLine($"  둘 다 있는데 안 쏨{tickGated,6} ({tickGated * 100f / totalTicks:F1}%)"
                          + "  ← 발사 간격·게이트");
            sb.AppendLine($"  창고 최고 {peakStore:F1} · 마운트 최고 {peakMount:F1}"
                          + $" · 마운트 평균 {mountSum / Mathf.Max(1, mountSamples):F1}");
            sb.AppendLine("  📌 **창고는 높은데 마운트가 낮으면 이송이 병목**이고,"
                          + " 둘 다 높은데 안 쏘면 게이트다.");

            sb.AppendLine();
            sb.AppendLine("[굳은 적 — 10초마다 · 사거리 밖인데 안 움직인 수]");
            foreach (string line in stuckLog) sb.AppendLine(line);
            sb.AppendLine($"  좌우 반전 {sideFlips} 회 — 떨림 잣대. 크면 붙들기(현재 "
                          + $"{tuning.enemySideStepHoldTbd:F2}초)가 모자란 것이다");
            sb.AppendLine("  구 규칙(우회 금지)은 2026-09-16 에 폐기됐다 — 주 축이 막히면");
            sb.AppendLine("  부 축으로 한 칸 본다. 둘 다 막히면 그대로 선다(길막은 살아 있다).");

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
