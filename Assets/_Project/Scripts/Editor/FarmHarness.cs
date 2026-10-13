using System.Collections.Generic;
using System.Text;
using MBI.Core;
using MBI.Data;
using UnityEditor;
using UnityEngine;

namespace MBI.EditorTools
{
    /// <summary>
    /// 【하네스】 **상주 파밍 층**을 잰다 — 정원 보충 주기 · 고철 누계 · 포화
    /// (2026-10-13 사용자 확정 #111·#112 · 순서 7).
    ///
    /// ⚠️⚠️ **무엇을 재고 무엇을 안 재는지 먼저 밝힌다.**
    ///   · **잰다** — 표(`FARM_COMP_DATA`·`STAGE_DATA`·`ENEMY_DATA`)와 파밍 규칙이
    ///     만드는 수: 정원, 보충 주기, 바퀴마다 보충되는 마릿수, 고철 누계, 상한 시급,
    ///     그리고 정원을 한 바퀴 안에 다 잡는 데 필요한 **포화 출력**.
    ///   · **안 잰다** — 로봇이 실제로 그만큼 잡는가. 그것은 화력·물류의 일이고
    ///     <see cref="StageClearHarness"/> 가 잰다. 여기서 로봇을 흉내 내면
    ///     **두 하네스가 다른 답을 내는** 자리가 생긴다(지침 §7).
    ///
    /// 📌 **그래서 이 하네스가 답하는 물음은 하나다** —
    ///    「표가 정한 파밍의 **천장**이 얼마이고, 거기 닿으려면 출력이 얼마여야 하나.」
    ///    오프라인 정산이 쓰는 시급(순서 6)의 상한이 바로 이 수다.
    ///
    /// ⚠️ **값을 하나도 지어내지 않는다** — 정원·간격·단가·체력 전부 자산에서 읽는다.
    ///    자산이 없거나 표가 비면 **그 사실을 적고** 그 판은 건너뛴다.
    ///
    /// ⚠️ 처치 비율 셋(0 · 절반 · 전부)은 **가정**이다 — 「로봇이 한 바퀴에 정원의 몇을
    ///    잡는가」는 문서에 없다. 그래서 세 지점을 찍어 **시급이 어떻게 자라는지**를 보인다.
    ///
    /// 배치 실행: <c>-executeMethod MBI.EditorTools.FarmHarness.RunFarmProbeBatch</c>
    /// </summary>
    public static class FarmHarness
    {
        private const string SoRoot = "Assets/_Project/ScriptableObjects";

        /// <summary>한 틱 — 재는 쪽은 고정 간격이어야 되풀이가 된다(도전 하네스와 같은 규약).</summary>
        private const float Dt = 0.1f;

        /// <summary>몇 바퀴를 돌려 보나. ⚠️ 가정 — 주기가 고르다는 것만 보이면 된다.</summary>
        private const int Laps = 3;

        /// <summary>처치 비율 세 지점(정원 대비). ⚠️ 가정 — 문서에 없는 수다.</summary>
        private static readonly float[] KillFractions = { 0f, 0.5f, 1f };

        [MenuItem("MBI/Harness 파밍 S1~S6")]
        public static void RunFarmProbeMenu() => Debug.Log(RunFarmProbe());

        public static void RunFarmProbeBatch()
        {
            Debug.Log(RunFarmProbe());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string RunFarmProbe()
        {
            var head = new StringBuilder();
            var body = new StringBuilder();
            var rows = new List<string>();

            head.AppendLine("############ 파밍 층 자체 시험 S1~S6 (2026-10-13 · 순서 7) ############");
            head.AppendLine();
            head.AppendLine("| 판 | 정원 | 간격 | 바퀴당 고철(전부 잡을 때) | 상한 시급 | 포화 출력 | 표 |");
            head.AppendLine("|---|---|---|---|---|---|---|");

            Dictionary<string, EnemyDefinition> byKey = EnemyCatalog();
            if (byKey.Count == 0) return "적 자산이 없다 — 'MBI/Generate Combat Data' 먼저.";

            for (int n = 1; n <= StageMapName.TotalStages; n++)
            {
                string id = "S" + n;
                var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(
                    SoRoot + "/Stages/Stage_" + id + ".asset");

                body.AppendLine();
                body.AppendLine("======== " + id + " — 상주 파밍 ========");

                if (stage == null)
                {
                    body.AppendLine("  자산이 없다 — 건너뛴다.");
                    rows.Add("| **" + id + "** | — | — | — | — | — | ❌ **자산 없음** |");
                    continue;
                }

                body.Append(ProbeOne(stage, byKey, rows));
            }

            foreach (string r in rows) head.AppendLine(r);

            head.AppendLine();
            head.AppendLine("⚠️ **로봇 화력은 안 재는 하네스다** — 여기 수는 「표가 정한 천장」이고,");
            head.AppendLine("   실제로 그만큼 잡는가는 도전 하네스(`StageClearHarness`)가 잰다.");
            head.AppendLine("⚠️ **처치 비율 셋은 가정**이다 — 문서에 「한 바퀴에 정원의 몇을 잡나」가 없다.");
            head.AppendLine("⚠️ **값을 하나도 안 고쳤다** — 정원·간격·단가·체력은 전부 자산에서 읽은 수다.");
            head.AppendLine();
            head.AppendLine(body.ToString());
            head.AppendLine("############ 끝 ############");
            return head.ToString();
        }

        private static string ProbeOne(StageDefinition stage,
            Dictionary<string, EnemyDefinition> byKey, List<string> rows)
        {
            var sb = new StringBuilder();
            string id = stage.stageId;

            List<StageComposition> roster = stage.farmComposition;
            int cap = FarmRefillRule.CapacityOf(roster);
            float interval = stage.spawnInterval;

            sb.AppendLine("  표 확정 여부 " + (stage.spawnConfirmed ? "확정" : "**미확정**")
                          + " · 정원 " + cap + " · 간격 " + interval.ToString("F1") + "초");

            if (cap <= 0 || interval <= 0f)
            {
                sb.AppendLine("  ⚠️ 파밍 표가 비었다 — 파밍 층이 안 선다. 재지 않는다.");
                rows.Add("| **" + id + "** | " + cap + " | " + interval.ToString("F1")
                         + " | — | — | — | ❌ **표 없음** |");
                return sb.ToString();
            }

            // ── 표가 말하는 한 바퀴 ────────────────────────────────────────
            //
            // 「정원을 전부 잡으면 한 바퀴에 얼마인가」 — 종류마다 단가가 다르므로
            // 마릿수로 못 낸다(#112 ⑤). 줄마다 수 × 그 종류 단가를 더한다.
            double lapScrapFull = 0d;
            float hpSum = 0f;
            int counted = 0;
            var missing = new List<string>();

            for (int i = 0; i < roster.Count; i++)
            {
                StageComposition e = roster[i];
                if (e.count <= 0 || string.IsNullOrEmpty(e.enemyKey)) continue;

                float perKill = 0f;
                if (byKey.TryGetValue(e.enemyKey, out EnemyDefinition def) && def != null)
                    perKill = def.scrapPerKill;
                else
                    missing.Add(e.enemyKey);

                lapScrapFull += e.count * (double)perKill;
                hpSum += e.count * e.hp;
                counted += e.count;

                sb.AppendLine("    · " + e.enemyKey + " ×" + e.count
                              + " · 체력 " + e.hp.ToString("F0")
                              + " · 방어 " + e.def.ToString("F0")
                              + " · 단가 " + perKill.ToString("F0")
                              + (perKill <= 0f ? "  ⚠️ **단가 0**(표 미기입)" : string.Empty));
            }

            if (missing.Count > 0)
                sb.AppendLine("  ⚠️ 적 자산에 없는 종류: " + string.Join(", ", missing)
                              + " — 그 줄은 단가 0 으로 셌다(지어내지 않는다).");

            // 평균 체력 — 포화 출력 식이 체력 **하나**를 받으므로 정원 가중 평균을 쓴다.
            // ⚠️ 종류마다 체력이 다르면 평균은 근사다. 그 사실을 적는다.
            float avgHp = counted > 0 ? hpSum / counted : 0f;
            float saturation = FarmSpawnRule.SaturationPower(cap, avgHp, interval);
            double capRate = FarmSpawnRule.HourlyRateOf(lapScrapFull, interval);

            sb.AppendLine("  한 바퀴 고철(정원 전부) **" + lapScrapFull.ToString("F0") + "**"
                          + " · 상한 시급 **" + capRate.ToString("F0") + "**/시간");
            sb.AppendLine("  포화 출력 **" + saturation.ToString("F1") + "** (= 정원 " + cap
                          + " × 평균체력 " + avgHp.ToString("F1")
                          + " ÷ 간격 " + interval.ToString("F1")
                          + ") — 이 출력에 닿으면 수입이 천장이다");
            if (roster.Count > 1)
                sb.AppendLine("  ⚠️ 종류마다 체력이 달라 **평균 체력은 근사**다 — 포화 출력도 근사다.");

            // ── 바퀴를 실제로 돌린다 ───────────────────────────────────────
            //
            // 게임과 **같은 세션**(`FarmSession`)을 돌린다. 적을 흉내 내는 대신
            // 「살아 있는 수」와 「이번 틱에 죽은 것들이 들고 있던 고철」만 먹인다 —
            // 그 둘이 러너가 세션에 주는 것 전부다.
            foreach (float frac in KillFractions)
            {
                int killsPerLap = Mathf.Clamp(Mathf.RoundToInt(cap * frac), 0, cap);

                // 그 마릿수가 들고 오는 고철 — 정원 전부일 때의 액수를 비율로 나눈다.
                // ⚠️ 어느 종류를 먼저 잡는지는 모른다 — **고르게 섞인 것**으로 본다(근사).
                double scrapPerLap = lapScrapFull * killsPerLap / cap;

                var session = new FarmSession(cap, interval);
                var lapRates = new List<double>();
                var lapRefills = new List<int>();

                // 첫 보충을 러너와 같게 **즉시** 준다(`OpenFarmLayer` 가 그렇게 한다).
                int alive = cap;
                int firstRefill = cap;
                float t = 0f;
                int overCap = 0;

                int steps = Mathf.CeilToInt(interval * Laps / Dt) + 2;
                for (int i = 0; i < steps; i++)
                {
                    // 한 바퀴 안에 **고르게** 잡는다 — 바퀴 끝에 몰아 잡으면 주기만 보이고
                    // 「잡는 동안 비는가」가 안 보인다.
                    int killNow = 0;
                    if (killsPerLap > 0 && alive > 0)
                    {
                        float per = interval / killsPerLap;      // 한 마리당 몇 초
                        if (per > 0f
                            && Mathf.FloorToInt((t + Dt) / per) > Mathf.FloorToInt(t / per))
                            killNow = 1;
                    }
                    if (killNow > alive) killNow = alive;
                    alive -= killNow;

                    double scrapNow = killNow > 0 && killsPerLap > 0
                        ? scrapPerLap * killNow / killsPerLap : 0d;

                    FarmTickResult r = session.Tick(Dt, alive, killNow, scrapNow);
                    t += Dt;

                    if (!r.lapClosed) continue;

                    lapRefills.Add(r.refill);
                    lapRates.Add(r.lapHourlyRate);
                    alive += r.refill;
                    t = 0f;
                    if (alive > cap) overCap++;
                }

                sb.AppendLine("  [처치 " + (frac * 100f).ToString("F0") + "%] 첫 보충 "
                              + firstRefill + "기 · 바퀴마다 보충 " + Join(lapRefills)
                              + " · 바퀴 시급 " + JoinRate(lapRates)
                              + (overCap > 0
                                  ? "  ❌ **정원 초과 " + overCap + "회**" : "  ✅ 정원 안"));
            }

            rows.Add("| **" + id + "** | " + cap + " | " + interval.ToString("F1") + "초 | "
                     + lapScrapFull.ToString("F0") + " | " + capRate.ToString("F0") + "/시간 | "
                     + saturation.ToString("F1") + " | "
                     + (stage.spawnConfirmed ? "✅ 확정" : "⚠️ 미확정") + " |");

            return sb.ToString();
        }

        private static string Join(List<int> v)
        {
            if (v.Count == 0) return "**없음**";
            var sb = new StringBuilder();
            for (int i = 0; i < v.Count; i++)
            {
                if (i > 0) sb.Append("/");
                sb.Append(v[i]);
            }
            return sb.ToString();
        }

        private static string JoinRate(List<double> v)
        {
            if (v.Count == 0) return "**없음**";
            var sb = new StringBuilder();
            for (int i = 0; i < v.Count; i++)
            {
                if (i > 0) sb.Append("/");
                sb.Append(v[i].ToString("F0"));
            }
            return sb.ToString();
        }

        /// <summary>적 자산 전부 — 종류 이름으로 찾게.</summary>
        private static Dictionary<string, EnemyDefinition> EnemyCatalog()
        {
            var map = new Dictionary<string, EnemyDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyDefinition"))
            {
                var d = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (d != null && !string.IsNullOrEmpty(d.enemyKey)) map[d.enemyKey] = d;
            }
            return map;
        }
    }
}
