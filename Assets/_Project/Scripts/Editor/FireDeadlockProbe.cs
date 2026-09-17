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
    /// 【핵심】 발사 0 교착 실측 (2026-09-15 · 사용자 육안 ①).
    ///
    /// 증상 — 창고 40/40 · 마운트 40 인데 HUD 가 「관통 0.0 · 표준 0.0 · 폭발 0.0 (실제)」이고
    /// 적 96 기에 둘러싸여 78 초 동안 한 발도 안 나갔다.
    ///
    /// ⚠️ **값만 낸다. 고치지 않는다.** 규칙에 닿는 수정은 사용자 갈래 뒤다.
    ///
    /// ⚠️ **게임이 도는 경로를 그대로 흉내 낸다** — 어제 하네스가 게임과 다른 판을 재서
    /// 다섯 번 틀렸다(HANDOFF 함정). 그래서 배분 함수만 부르지 않고
    /// StageRunner.RefreshFireRate 의 **재배분 게이트까지** 같이 옮겨 온다.
    ///
    /// 배치 실행: -executeMethod MBI.EditorTools.FireDeadlockProbe.RunBatch
    /// </summary>
    public static class FireDeadlockProbe
    {
        private const string NodeRoot = "Assets/_Project/ScriptableObjects/Nodes";
        private const float Dt = 0.05f;
        private const float Seconds = 60f;

        // StageRunner 가 쓰는 값 그대로 (StageRunner.cs:53).
        private const float ScaleEpsilon = 0.001f;

        [MenuItem("MBI/Probe Fire Deadlock")]
        public static void RunMenu() => Debug.Log(Run());

        public static void RunBatch()
        {
            Debug.Log(Run());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 발사 0 교착 실측 (2026-09-15 · 육안 1) ===");

            RobotDefinition robot = AssetDatabase.LoadAssetAtPath<RobotDefinition>(
                "Assets/_Project/ScriptableObjects/Robots/Robot_A.asset");
            CombatTuning tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>(
                "Assets/_Project/ScriptableObjects/CombatTuning.asset");
            if (robot == null || tuning == null)
            {
                sb.AppendLine("  (자산 없음 — Robot_A / CombatTuning)");
                return sb.ToString();
            }

            ArrivalTrace(sb, false);
            ArrivalTrace(sb, true);
            GateTrace(sb, robot);
            FireRun(sb, robot, tuning, true, true);
            FireRun(sb, robot, tuning, false, true);
            return sb.ToString();
        }

        // ────────────────────────────────────────────────────────────────
        //  1. 도착률 자체는 서는가 — 실제 시작 보드로 잰다
        // ────────────────────────────────────────────────────────────────

        private static void ArrivalTrace(StringBuilder sb, bool fill)
        {
            sb.AppendLine();
            sb.AppendLine(fill
                ? "[1-2] 빈 칸 (6,5) 를 **채운** 보드"
                : "[1-1] 시작 보드 그대로 (빈 칸 (6,5) 비어 있음)");

            BoardGrid grid = Board(fill);
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);
            var delivery = new MountDelivery();

            // 도착률이 0 일 때 **어디서 0 이 되는지**를 가른다 —
            // 도착 자체가 없는 것과, 왔는데 소유자 필터에 걸려 안 세어지는 것은 다른 병이다.
            int rawArrivals = 0;
            var rawByOwner = new Dictionary<MountOwner, int>();
            var rawByKind = new Dictionary<FlowKind, int>();

            int steps = Mathf.RoundToInt(Seconds / Dt);
            float nextReport = 10f;
            for (int i = 0; i < steps; i++)
            {
                BoardItemTick.Step(grid, flow, Dt, 1f);

                foreach (MountArrival a in flow.PendingMountArrivals)
                {
                    rawArrivals++;
                    rawByOwner.TryGetValue(a.owner, out int no);
                    rawByOwner[a.owner] = no + 1;
                    rawByKind.TryGetValue(a.kind, out int nk);
                    rawByKind[a.kind] = nk + 1;
                }

                delivery.Observe(flow.PendingMountArrivals, DamageOf, Dt, MountOwner.RobotA);
                flow.ClearPendingMountArrivals();
                delivery.TryDrain(1f, out _);

                float t = (i + 1) * Dt;
                if (t + 0.0001f >= nextReport)
                {
                    sb.AppendLine("  t=" + t.ToString("F0") + "s  관통 "
                        + delivery.RateOf(AmmoKind.Pierce).ToString("F2") + " · 표준 "
                        + delivery.RateOf(AmmoKind.Standard).ToString("F2") + " · 폭발 "
                        + delivery.RateOf(AmmoKind.Explosive).ToString("F2") + " 발/초");
                    nextReport += 10f;
                }
            }

            sb.AppendLine("  필터 전 총 도착 = " + rawArrivals + " 개 / 60초");
            foreach (var kv in rawByOwner)
                sb.AppendLine("    소유자 " + kv.Key + " : " + kv.Value + " 개");
            foreach (var kv in rawByKind)
                sb.AppendLine("    품목 " + kv.Key + " : " + kv.Value + " 개");
            sb.AppendLine("  노드 도착(소비처) 총계 = " + flow.DeliveredCount + " 개");

            // ⚠️ **꼬리 네 칸의 실제 면을 읽는다.** 코드가 지정한 면과 Resolve 뒤의 면이
            // 다를 수 있다 — 다르면 마운트 출구가 아예 안 선다.
            sb.AppendLine();
            sb.AppendLine("  꼬리 칸 실제 면 (BeltAutoOrient·BeltFlow.Resolve 뒤)");
            foreach (Vector2Int c in new[]
                     {
                         new Vector2Int(2, 6), new Vector2Int(1, 6),
                         new Vector2Int(1, 5), new Vector2Int(1, 4),
                     })
            {
                BeltInstance b = grid.GetBeltAt(c);
                if (b == null) { sb.AppendLine("    (" + c.x + "," + c.y + ") 벨트 없음"); continue; }
                sb.AppendLine("    (" + c.x + "," + c.y + ") 입 [" + string.Join(",", b.InFaces)
                    + "] 출 [" + string.Join(",", b.OutFaces) + "] 품목 " + b.Kind
                    + " · 마운트출구 " + PartLayout.TryGetMountPort(c, PortFace.South, out _)
                    + " · 지금 실린 것 " + flow.ItemsAt(c).Count + " 개");
            }

            // 꼬리로 물건이 오기는 하는가 — 상류부터 훑는다.
            sb.AppendLine();
            sb.AppendLine("  벨트 칸별 적재 (60초 뒤 · 0 이 아닌 칸만)");
            int loaded = 0;
            foreach (StartingBoard.Run r in StartingBoard.Belts)
            {
                var c = new Vector2Int(r.cell.x, r.cell.y);
                int n = flow.ItemsAt(c).Count;
                if (n <= 0) continue;
                loaded++;
                sb.AppendLine("    (" + c.x + "," + c.y + ") " + n + " 개");
            }
            if (loaded == 0) sb.AppendLine("    (벨트 전 구간이 비어 있다)");
        }

        // ────────────────────────────────────────────────────────────────
        //  2. 재배분 게이트 — 도착률이 올라도 다시 배분되는가
        // ────────────────────────────────────────────────────────────────

        private static void GateTrace(StringBuilder sb, RobotDefinition robot)
        {
            sb.AppendLine();
            sb.AppendLine("[2] 재배분 게이트 (StageRunner.RefreshFireRate 그대로)");
            sb.AppendLine("    게이트식: |출력/명목 − 직전| < 0.001 이면 재배분하지 않는다");
            float nominal = RobotOutput.Nominal(robot.weapons, 1f, robot.moduleMult);
            sb.AppendLine("    명목 출력 = " + nominal.ToString("F2"));
            sb.AppendLine("    ⚠️ 이 식에는 도착률이 안 들어간다 — 배분 함수만 탄종별로 바꿨고");
            sb.AppendLine("       그 함수를 부를지 말지는 여전히 구 전역 배율이 정한다.");
        }

        // ────────────────────────────────────────────────────────────────
        //  3·4. 60초 전투 — 게이트 있을 때 / 없을 때
        // ────────────────────────────────────────────────────────────────

        private static void FireRun(StringBuilder sb, RobotDefinition robot, CombatTuning tuning,
            bool gated, bool fill)
        {
            sb.AppendLine();
            sb.AppendLine(gated
                ? "[3] 60초 전투 — **구 게이트** (배율 하나만 본다 · 09-15 이전)"
                : "[4] 60초 전투 — **지금 코드** (FireRateGate + 발사 규칙 (가))");

            BoardGrid grid = Board(fill);
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);
            var delivery = new MountDelivery();

            SupplySignals.Reset();
            SupplySignals.HasCombat = true;
            SupplySignals.ActiveOwner = MountOwner.RobotA;

            var lineBuffer = new List<AmmoLine>();
            ShotAllocator.AllocateRates(robot.weapons, robot.consumptionCap,
                SupplySignals.ArrivalRateOf, lineBuffer);
            sb.AppendLine("    시작 라인 수 = " + lineBuffer.Count + "  (도착률이 아직 0인 시점)");

            float ammoCapacity = robot.balanceRef != null ? robot.balanceRef.storeCapacity : 40f;
            var store = new AmmoInventory(ammoCapacity);
            var mount = new MountLoad(MountLoad.SlotsRobotA, MountLoad.StandardStacks(10f));

            var setup = new RobotSetup
            {
                hp = tuning.robotHpTbd,
                mountCoef = robot.mountCoef,
                moduleMult = robot.moduleMult,
                attackRange = tuning.robotAttackRangeTbd,
                radius = 0.5f,
                multiShotCount = tuning.multiShotCountTbd,
                aoeRadius = tuning.aoeRadiusTbd,
                aoeSplashFactor = tuning.aoeSplashFactorTbd,
                lines = new List<AmmoLine>(lineBuffer),
                ammoCapacity = ammoCapacity,
                ammoStore = store,
                mount = mount,
            };

            // ⚠️ **사용자가 본 상태로 세운다** — 창고 40/40 · 마운트 40.
            // 값을 지어내는 것이 아니라 **관측된 화면을 재현**하는 것이다. 이 상태에서
            // 한 발이라도 나가는가가 이 프로브의 물음이다.
            store.Fill(AmmoKind.Standard);
            mount.Load(MountItem.Standard, 40f);

            var spawns = new List<EnemySpawn>();
            for (int i = 0; i < 40; i++)
                spawns.Add(new EnemySpawn
                {
                    hp = 30f, def = 0f, atk = 1f, moveSpeed = 0.5f,
                    attackRange = 1f, attackInterval = 1f, radius = 0.4f,
                });

            var sim = new CombatSimulation(setup, spawns, 999f, 999f, 0.8f);

            // ⚠️ **적을 사거리 안에 세운다.** 사용자가 본 화면은 「적 96 기에 둘러싸였는데
            // 안 쏜다」였다 — 적이 멀어서 안 쏘는 것과 섞이면 이 프로브가 다른 판을 재게 된다.
            // 게임은 카메라 시야에서 링을 뽑지만(SpawnRingRule), 여기서는 **둘러싸인 상태**를
            // 직접 세워 「쏠 수 있는데 안 쏘는가」만 남긴다.
            sim.SetSpawnBand(4f, 8f);

            float nominal = RobotOutput.Nominal(robot.weapons, 1f, robot.moduleMult);
            float lastScale = 1f;
            var fireGate = new FireRateGate();
            fireGate.Reset(1f);

            int steps = Mathf.RoundToInt(Seconds / Dt);
            int realloc = 0;
            float nextTrace = 15f;
            for (int i = 0; i < steps; i++)
            {
                // 1) 물류 — 실제 보드가 실제로 나른다
                BoardItemTick.Step(grid, flow, Dt, 1f);
                delivery.Observe(flow.PendingMountArrivals, DamageOf, Dt, MountOwner.RobotA);
                flow.ClearPendingMountArrivals();
                delivery.TryDrain(1f, out _);
                for (int k = 0; k < SupplySignals.MountArrivalRate.Length; k++)
                    SupplySignals.MountArrivalRate[k] = delivery.RateOf((AmmoKind)k);

                // 2) 재배분 — 게이트 유무가 이 프로브의 대조점이다
                // 마운트 재고를 신호에 올린다 — 러너의 `PublishSupplySignals` 가 하는 일이다.
                SupplySignals.EnsureSlots(mount.SlotCount);
                for (int k = 0; k < mount.SlotCount; k++)
                {
                    SupplySignals.MountSlotItem[k] = mount.ItemAt(k);
                    SupplySignals.MountSlotAmount[k] = mount.AmountAt(k);
                }

                if (gated)
                {
                    // ⚠️ **구 게이트** — 배율 하나만 본다. 만공급이면 평평해서 안 움직인다.
                    float scale = 1f;
                    if (Mathf.Abs(scale - lastScale) >= ScaleEpsilon)
                    {
                        lastScale = scale;
                        ShotAllocator.AllocateRates(robot.weapons, robot.consumptionCap,
                            SupplySignals.ArrivalRateOf, lineBuffer);
                        sim.SetFireLines(lineBuffer);
                        realloc++;
                    }
                }
                else
                {
                    // **지금 코드** — `FireRateGate` + 발사 규칙 (가).
                    if (fireGate.ShouldReallocate(1f, SupplySignals.ArrivalRateOf,
                            SupplySignals.MountStockOf, ScaleEpsilon))
                    {
                        ShotAllocator.AllocateRates(robot.weapons, robot.consumptionCap,
                            SupplySignals.ArrivalRateOf, SupplySignals.MountStockOf, lineBuffer);
                        sim.SetFireLines(lineBuffer);
                        realloc++;
                    }
                }

                sim.Tick(Dt);

                float t = (i + 1) * Dt;
                if (t + 0.0001f >= nextTrace)
                {
                    float near = float.MaxValue;
                    foreach (CombatEntity e in sim.Enemies)
                        if (e.hp > 0f)
                        {
                            float d = Vector2.Distance(sim.Robot != null ? sim.Robot.position : Vector2.zero,
                                e.position);
                            if (d < near) near = d;
                        }
                    sb.AppendLine("    t=" + t.ToString("F0") + "s  라인 " + lineBuffer.Count
                        + "줄 · 표준도착 " + delivery.RateOf(AmmoKind.Standard).ToString("F2")
                        + " · 살아있는 적 " + sim.Remaining
                        + " · 최근접 " + (near == float.MaxValue ? "없음" : near.ToString("F1"))
                        + " · 사거리 " + setup.attackRange.ToString("F1")
                        + " · 쏜 표준 " + sim.FiredOf(AmmoKind.Standard));
                    nextTrace += 15f;
                }
            }

            sb.AppendLine("    재배분 횟수 = " + realloc + " / " + steps + " 틱");
            sb.AppendLine("    쏜 발수 — 관통 " + sim.FiredOf(AmmoKind.Pierce)
                + " · 표준 " + sim.FiredOf(AmmoKind.Standard)
                + " · 폭발 " + sim.FiredOf(AmmoKind.Explosive));
            sb.AppendLine("    창고 " + store.Total.ToString("F1") + "/" + ammoCapacity.ToString("F0")
                + " · 마운트 " + mount.Total.ToString("F1")
                + " · 남은 적 " + sim.Remaining + " · 결과 " + sim.Result);
        }

        // ── 배선 ──────────────────────────────────────────────────────────

        private static float DamageOf(AmmoKind kind) =>
            kind == AmmoKind.Pierce ? 20f : kind == AmmoKind.Explosive ? 50f : 10f;

        private static BoardGrid Board(bool fill)
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f,
                Vector2.zero, PartLayout.BuildMask());
            // ⚠️ **게임이 지나는 문으로 세운다** — 조합표를 안 고르면 추진제 노드가
            //    표준탄으로 돌아 그 줄이 죽고, 프로브가 없는 판을 재게 된다(2026-09-17).
            StartingBoard.Apply(g, Node);
            foreach (StartingBoard.Run r in StartingBoard.Belts) StartingBoard.Place(g, r);
            if (fill) StartingBoard.Place(g, StartingBoard.FillsEmptySlot);
            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }


        private static NodeDefinition Node(string id) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>(NodeRoot + "/Node_" + id + ".asset");
    }
}
