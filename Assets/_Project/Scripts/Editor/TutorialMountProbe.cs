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
    /// 【조사】 튜토리얼 둘째 목표(마운트 만충)가 어느 단에서 끊기는가
    /// (2026-09-15 · 사용자 육안 4차 ⑤ · <see cref="TutorialGoalProbe"/> 의 다음 단).
    ///
    /// 앞 프로브가 **보드는 결백하다**를 냈다 — 채운 판에서 마운트 도착 339개/90초.
    /// 그런데 만충을 만드는 것은 그 도착이 아니다. 사슬은 넷이다 —
    ///
    /// <code>
    /// 벨트 도착 → (도착률) → 발사 라인 → 창고 생산 → 마운트 적재 → 만충
    /// </code>
    ///
    /// ⚠️ **가운데 두 단이 서로를 먹는다.** 창고 생산은 `lines` 를 돌며 넣는데
    /// (`ProduceAmmoInto` 는 `lines` 가 비면 **그냥 돌아간다**), 그 `lines` 는
    /// 도착률·재고로 배분된다. 그래서 **0 에서 시작하면 스스로 못 일어날 수 있다.**
    /// 그 자리가 맞는지 틱 단위로 찍는다.
    ///
    /// ⚠️ **값만 낸다. 고치지 않는다.**
    ///
    /// 배치 실행: <c>-executeMethod MBI.EditorTools.TutorialMountProbe.RunBatch</c>
    /// </summary>
    public static class TutorialMountProbe
    {
        private const string NodeRoot = "Assets/_Project/ScriptableObjects/Nodes";
        private const float Dt = 0.05f;
        private const float Seconds = 60f;
        private const string ROBOT_PATH = "Assets/_Project/ScriptableObjects/Robots/Robot_A.asset";

        [MenuItem("MBI/Probe Tutorial Mount")]
        public static void RunMenu() => Debug.Log(Run());

        public static void RunBatch() => Debug.Log(Run());

        public static string Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 만충 사슬 실측 (2026-09-15 · 밸런스 (가) 확인) ===");
            sb.AppendLine();
            sb.AppendLine("[ㄱ] 안 쏠 때 (적 0)");
            sb.AppendLine(Measure(false));
            sb.AppendLine("[ㄴ] 쏠 때 (적 120 · 사거리 안)");
            sb.AppendLine(Measure(true));
            return sb.ToString();
        }

        private static string Measure(bool withEnemies)
        {
            var sb = new StringBuilder();

            RobotDefinition robot = AssetDatabase.LoadAssetAtPath<RobotDefinition>(
                ROBOT_PATH);
            var tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>(
                "Assets/_Project/ScriptableObjects/CombatTuning.asset");
            if (robot == null || tuning == null)
                return sb.AppendLine("자산이 없다 — RobotConfig / CombatTuning").ToString();

            BoardGrid grid = Board();
            var flow = new BeltItemFlow();
            flow.Rebuild(grid);
            var delivery = new MountDelivery();

            SupplySignals.Reset();
            SupplySignals.HasCombat = true;
            SupplySignals.ActiveOwner = MountOwner.RobotA;
            LogisticsOutputBridge.Reset();

            float cap = robot.balanceRef != null ? robot.balanceRef.storeCapacity : 40f;
            var store = new AmmoInventory(cap);
            var mount = new MountLoad(MountLoad.SlotsRobotA, MountLoad.StandardStacks(10f));

            // ⚠️ **빈 채로 시작한다** — 튜토리얼은 처음 켠 판이다. 채워 두고 재면
            // 「스스로 일어나는가」라는 물음 자체가 사라진다.
            var setup = new RobotSetup
            {
                hp = tuning.robotHp,
                mountCoef = robot.mountCoef,
                moduleMult = robot.moduleMult,
                attackRange = tuning.robotAttackRangeTbd,
                radius = 0.5f,
                multiShotCount = tuning.multiShotCountTbd,
                aoeRadius = tuning.aoeRadiusTbd,
                aoeSplashFactor = tuning.aoeSplashFactorTbd,
                lines = new List<AmmoLine>(),
                ammoCapacity = cap,
                ammoStore = store,
                mount = mount,
            };

            // ⚠️ **적 유무로 두 판을 잰다**(2026-09-15 · 밸런스 판정 (가) 확인).
            // (ㄱ) 안 쏠 때 = 적 0 → 마운트가 얼마 만에 차는가
            // (ㄴ) 쏠 때  = 적 있음 → 소비가 공급을 넘으면 마운트가 비는가
            var spawns = new List<EnemySpawn>();
            if (withEnemies)
                for (int e = 0; e < 120; e++)
                    spawns.Add(new EnemySpawn
                    {
                        hp = 30f, def = 0f, atk = 1f, moveSpeed = 0.5f,
                        attackRange = 1f, attackInterval = 1f, radius = 0.4f,
                    });

            var sim = new CombatSimulation(setup, spawns, 999f, 999f, 0.8f);
            sim.Endless = true;
            if (withEnemies) sim.SetSpawnBand(2f, 5f);

            float nominal = RobotOutput.Nominal(robot.weapons, 1f, robot.moduleMult);
            sb.AppendLine("  만공급 출력(분모) = " + nominal.ToString("F2"));

            var gate = new FireRateGate();
            var lineBuffer = new List<AmmoLine>();
            int steps = Mathf.RoundToInt(Seconds / Dt);
            float fullAt = -1f;
            int rawAmmo = 0, rawNonAmmo = 0, rawNonStandard = 0;
            float firstAt = -1f;

            sb.AppendLine();
            sb.AppendLine("  초 | 도착률(표준) | 라인 | 창고유입 | 창고재고 | 마운트 | 만충");

            for (int i = 0; i < steps; i++)
            {
                // ── 물류 쪽 (LogisticsOutputProvider 가 하는 일) ──
                BoardItemTick.Step(grid, flow, Dt, 1f);

                // ⚠️ **창 값과 누적을 같이 센다**(2026-09-15 · 「공칭 4인데 6으로 재어진다」).
                // `MountDelivery.RateOf` 는 **0.5초 창**의 값이라 도착이 뭉치면 튄다 —
                // 긴 구간의 진짜 값은 누적으로만 나온다.
                IReadOnlyList<MountArrival> got = flow.PendingMountArrivals;
                for (int a = 0; a < got.Count; a++)
                {
                    if (MountItemMap.TryAmmoKindOf(got[a].kind, out AmmoKind ak))
                    {
                        rawAmmo++;
                        if (firstAt < 0f) firstAt = i * Dt;
                        if (ak != AmmoKind.Standard) rawNonStandard++;
                    }
                    else rawNonAmmo++;
                }

                delivery.Observe(flow.PendingMountArrivals, DamageOf, Dt, SupplySignals.ActiveOwner);
                flow.ClearPendingMountArrivals();
                delivery.TryDrain(0.5f, out float _);
                for (int k = 0; k < SupplySignals.MountArrivalRate.Length; k++)
                    SupplySignals.MountArrivalRate[k] = delivery.RateOf((AmmoKind)k);

                // 창고 유입은 보드 산출에서 온다 — 여기서는 도착률을 그대로 쓴다.
                LogisticsOutputBridge.AmmoProduce = delivery.RateOf(AmmoKind.Standard);
                LogisticsOutputBridge.Result = new LogisticsResult
                {
                    actual = nominal * Mathf.Clamp01(
                        SupplySignals.ArrivalRateOf(AmmoKind.Standard)),
                    expected = nominal,
                };

                // ── 전투 쪽 (StageRunner 가 하는 일) ──
                float scale = nominal > 0f ? LogisticsOutputBridge.Output / nominal : 0f;
                if (gate.ShouldReallocate(scale, SupplySignals.ArrivalRateOf,
                        SupplySignals.MountStockOf, 0.001f))
                {
                    ShotAllocator.AllocateRates(robot.weapons, robot.consumptionCap,
                        SupplySignals.ArrivalRateOf, SupplySignals.MountStockOf, lineBuffer);
                    sim.SetFireLines(lineBuffer);
                }
                sim.AmmoSupplyRate = LogisticsOutputBridge.AmmoProduce;
                sim.Tick(Dt);

                // 마운트 재고를 신호에 실어 둔다 — 배분식이 그것을 읽는다.
                SupplySignals.EnsureSlots(mount.SlotCount);
                for (int sIdx = 0; sIdx < mount.SlotCount; sIdx++)
                {
                    SupplySignals.MountSlotItem[sIdx] = mount.ItemAt(sIdx);
                    SupplySignals.MountSlotAmount[sIdx] = mount.AmountAt(sIdx);
                }

                if (fullAt < 0f && mount.IsFull) fullAt = i * Dt;

                if (i % 100 == 0)
                    sb.AppendLine("  " + (i * Dt).ToString("F1")
                        + " | " + SupplySignals.ArrivalRateOf(AmmoKind.Standard).ToString("F2")
                        + " | " + lineBuffer.Count
                        + " | " + sim.AmmoSupplyRate.ToString("F2")
                        + " | " + store.Total.ToString("F1")
                        + " | " + mount.Total.ToString("F1")
                        + " | " + mount.IsFull);
            }

            sb.AppendLine();
            sb.AppendLine(fullAt >= 0f
                ? "  ✅ 마운트 만충 = **" + fullAt.ToString("F1") + "초**"
                : "  ⛔ **" + Seconds.ToString("F0") + "초 동안 만충이 안 섰다** — 둘째 목표가 영영 안 닫힌다");
            sb.AppendLine("  끝 상태 — 라인 " + lineBuffer.Count + "줄 · 창고 "
                          + store.Total.ToString("F1") + "/" + cap.ToString("F0")
                          + " · 마운트 " + mount.Total.ToString("F1"));

            // ⚠️ **소비와 공급을 나란히 적는다** — 판정 (가) 의 핵심이 그 대소다.
            float consume = 0f;
            for (int i2 = 0; i2 < lineBuffer.Count; i2++) consume += lineBuffer[i2].shotsPerSec;
            // 누적 — 첫 도착부터 끝까지로 나눈다(앞의 빈 구간이 평균을 낮춘다).
            float active = firstAt >= 0f ? Seconds - firstAt : 0f;
            sb.AppendLine("  도착 누적 — 탄약 " + rawAmmo + "개 · 탄약 아닌 것 " + rawNonAmmo + "개"
                          + " · 표준 아닌 탄약 " + rawNonStandard + "개");
            sb.AppendLine("  첫 도착 " + (firstAt < 0f ? "없음" : firstAt.ToString("F1") + "초")
                          + " · 그 뒤 " + active.ToString("F1") + "초 동안 초당 "
                          + (active > 0f ? (rawAmmo / active).ToString("F2") : "0")
                          + " 발  <- 이것이 긴 구간의 진짜 공급이다");
            sb.AppendLine("  소비(라인 합) " + consume.ToString("F2")
                          + " 발/초  vs  공급(도착률) "
                          + SupplySignals.ArrivalRateOf(AmmoKind.Standard).ToString("F2") + " 발/초"
                          + (consume > SupplySignals.ArrivalRateOf(AmmoKind.Standard)
                              ? "  <- 소비가 크다: 전투 중 마운트가 안 찬다" : ""));
            return sb.ToString();
        }

        private static float DamageOf(AmmoKind kind) => 1f;

        private static BoardGrid Board()
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f,
                Vector2.zero, PartLayout.BuildMask());
            // ⚠️ **게임이 지나는 문으로 세운다** — 조합표를 안 고르면 추진제 노드가
            //    표준탄으로 돌아 그 줄이 죽고, 프로브가 없는 판을 재게 된다(2026-09-17).
            StartingBoard.Apply(g, Node);
            foreach (StartingBoard.Run r in StartingBoard.Belts) StartingBoard.Place(g, r);
            StartingBoard.Place(g, StartingBoard.FillsEmptySlot);   // 사용자가 빈 칸을 채운 뒤
            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }


        private static NodeDefinition Node(string id) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>(NodeRoot + "/Node_" + id + ".asset");
    }
}
