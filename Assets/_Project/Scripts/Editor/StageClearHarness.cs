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

        /// <summary>
        /// 한 판의 **판정 한 줄** — S0~S5 자체 시험이 읽는다 (2026-09-18 사용자 지시).
        ///
        /// ⚠️⚠️ **글을 다시 파싱하지 않는다.** 보고문에서 「Win」을 찾아 읽게 두면
        /// 문구를 고칠 때마다 시험이 조용히 깨진다 — **수는 수로 넘긴다.**
        /// </summary>
        public struct StageVerdict
        {
            public string stageId;
            public string how;          // 어떤 판으로 쟀나(한 로봇 · 두 로봇)
            public bool ok;             // 이겼는가
            public CombatResult result;
            public float seconds;
            public int remaining;
            public int total;
            public float hp;
            public float maxHp;
        }

        /// <summary>마지막으로 돌린 판의 판정. 판마다 덮어쓴다.</summary>
        public static StageVerdict LastVerdict;

        // ───────────────────── S0~S5 자체 시험 (2026-09-18 사용자 지시) ─────────────────────

        /// <summary>
        /// 【자체 시험】 **S0(튜토리얼)부터 S5까지 한 번에 돌린다** (2026-09-18 사용자 지시).
        ///
        /// ⚠️⚠️ **새 판을 짓지 않는다.** 판마다 **이미 있는 문**을 지난다 —
        /// S0 은 <see cref="TutorialGoalProbe"/>, S1~S4 는 이 파일의 <c>Run</c>,
        /// S5 는 <c>RunTag</c> 다. 시험이 제 판을 따로 지으면 **게임과 다른 것을 재게 된다**
        /// (09-15 에 여러 번 겪었다).
        ///
        /// 📌 **판마다 재는 것이 다르다** — 그것을 숨기지 않고 표에 적는다.
        /// · **S0** 은 전투가 없다(적 구성이 비어 있다). 그래서 「이겼나」가 아니라
        ///   **「빈 칸을 채우면 마운트에 물건이 닿는가」**를 본다 — 그것이 튜토리얼의 목표다.
        /// · **S5** 는 **태그 학습** 판이라(<c>reqType 2</c>) **두 로봇**으로 돌린다.
        ///   한 로봇으로 재면 게임이 세우지 않는 판을 재게 된다.
        /// · S1~S4 는 **튜토리얼을 마친 상태**(마운트 적재)로 돈다 — 정상 경로다.
        ///
        /// ⚠️ **안 끝난 판은 「졌다」가 아니다** — 상한에서 끊긴 것이며 그렇게 적는다.
        /// ⚠️ **값을 하나도 안 고친다.** 자산도 저장도 안 건드린다.
        ///
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunStageSweepBatch</c>
        /// </summary>
        [MenuItem("MBI/Harness S0~S5 자체 시험")]
        public static void RunStageSweepMenu() => Debug.Log(RunStageSweep());

        public static void RunStageSweepBatch()
        {
            Debug.Log(RunStageSweep());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string RunStageSweep()
        {
            var head = new StringBuilder();
            var body = new StringBuilder();
            var rows = new List<string>();

            head.AppendLine("############ S0~S5 자체 시험 (2026-09-18) ############");
            head.AppendLine();
            head.AppendLine("| 판 | 어떻게 쟀나 | 판정 | 수 |");
            head.AppendLine("|---|---|---|---|");

            // ── S0 — 전투가 없다. 「마운트에 닿는가」를 본다 ──
            body.AppendLine("======== S0 — 튜토리얼 (전투 없음 · 목표는 「이으면 만들어진다」) ========");
            body.AppendLine(TutorialGoalProbe.Run());

            bool s0Ok = TutorialGoalProbe.LastArrivals > 0 && TutorialGoalProbe.LastDangling == 0;
            rows.Add("| **S0** | 채운 판에서 마운트 도착을 잰다 | "
                     + (s0Ok ? "✅ 닿는다" : "❌ **안 닿는다**")
                     + " | 도착 " + TutorialGoalProbe.LastArrivals + "개 · 첫 도착 "
                     + (TutorialGoalProbe.LastFirstAt < 0f
                         ? "**없음**" : TutorialGoalProbe.LastFirstAt.ToString("F1") + "초")
                     + " · 끊긴 칸 " + TutorialGoalProbe.LastDangling + " |");

            // ── S1~S4 — 한 로봇 · 튜토리얼 종료 조건 ──
            foreach (string id in new[] { "S1", "S2", "S3", "S4" })
            {
                body.AppendLine();
                body.AppendLine("======== " + id + " — 한 로봇 · 튜토리얼 종료 적재 ========");
                body.AppendLine(Run(id, preloadMount: true));
                rows.Add(VerdictRow(LastVerdict));
            }

            // ── S5 — 태그 학습 판이라 두 로봇으로 ──
            body.AppendLine();
            body.AppendLine("======== S5 — **두 로봇**(태그 학습 판이라 한 로봇으로 안 잰다) ========");
            body.AppendLine(RunTag("S5"));
            rows.Add(VerdictRow(LastVerdict));

            foreach (string r in rows) head.AppendLine(r);

            head.AppendLine();
            head.AppendLine("⚠️ **「안 끝났다」는 「졌다」가 아니다** — " + HardCapSeconds.ToString("F0")
                            + "초 상한에서 끊은 것이다.");
            head.AppendLine("⚠️ **값을 하나도 안 고쳤다** — 자산·저장 그대로이고 여기 수는 **센 것**이다.");
            head.AppendLine();
            head.AppendLine(body.ToString());
            head.AppendLine("############ 끝 ############");
            return head.ToString();
        }

        /// <summary>
        /// 【자체 시험 · 키운 판】 **스테이지마다 판을 키워 가며 이기는 배수를 찾는다**
        /// (2026-09-18 사용자 지시).
        ///
        /// ⚠️⚠️ **배치를 지어내지 않았다.** 「S2 에서는 보드가 어디까지 커져 있다」가 문서에
        /// 없으므로, 판 전체를 **k 배**로 보는 모형을 쓴다(<see cref="PlantScale"/> 주석).
        /// 그래서 이 판이 답하는 것은 「어떤 배치여야 하나」가 **아니라**
        /// **「지금의 몇 배가 있어야 이기나」**다 — 그 둘은 다른 물음이다.
        ///
        /// ⚠️ **배수는 오름차순으로 훑고 처음 이긴 데서 멈춘다** — 더 키워도 이기는 것은
        /// 당연해서 잴 것이 없다.
        ///
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunGrownSweepBatch</c>
        /// </summary>
        [MenuItem("MBI/Harness 키운 판 S1~S5")]
        public static void RunGrownSweepMenu() => Debug.Log(RunGrownSweep());

        public static void RunGrownSweepBatch()
        {
            Debug.Log(RunGrownSweep());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>훑을 배수 — ⚠️ **가정**(촘촘히 훑으면 판이 수십 번 돈다).</summary>
        private static readonly float[] PlantSteps = { 1f, 1.5f, 2f, 3f, 4f, 6f, 8f };

        public static string RunGrownSweep()
        {
            var head = new StringBuilder();
            var body = new StringBuilder();

            head.AppendLine("############ 키운 판 — 스테이지마다 몇 배가 필요한가 (2026-09-18) ############");
            head.AppendLine();
            head.AppendLine("⚠️⚠️ **배치가 아니라 배수다.** 「그 스테이지의 보드가 어떻게 생겼나」는 문서에 없어");
            head.AppendLine("**판 전체를 k 배**(산출 · 전력 수요 · 전력 공급 함께)로 보는 모형으로 쟀다.");
            head.AppendLine("답하는 물음은 **「지금의 몇 배가 있어야 이기나」** 하나다. ⚠️ 자산은 안 건드렸다.");
            head.AppendLine();
            head.AppendLine("| 판 | 이긴 배수 | 그때 | 배수 1 일 때 |");
            head.AppendLine("|---|---|---|---|");

            try
            {
                foreach (string id in new[] { "S1", "S2", "S3", "S4", "S5" })
                {
                    bool two = id == "S5";   // 태그 학습 판은 두 로봇이다
                    string baseLine = null;
                    string wonAt = null, wonLine = null;

                    foreach (float k in PlantSteps)
                    {
                        PlantScale = k;
                        string report = two ? RunTag(id) : Run(id, preloadMount: true);
                        StageVerdict v = LastVerdict;

                        string line = v.result + " · " + v.seconds.ToString("F1") + "초 · 남은 적 "
                                      + v.remaining + "/" + v.total
                                      + " · HP " + v.hp.ToString("F0") + "/" + v.maxHp.ToString("F0");
                        if (baseLine == null) baseLine = line;

                        body.AppendLine();
                        body.AppendLine("======== " + id + " · 판 배수 " + k.ToString("F1")
                                        + " (" + (two ? "두 로봇" : "한 로봇") + ") ========");
                        body.AppendLine(report);

                        if (v.ok) { wonAt = k.ToString("F1"); wonLine = line; break; }
                    }

                    head.AppendLine("| **" + id + "** | "
                        + (wonAt != null ? "**" + wonAt + "배**" : "❌ **8배까지 못 이김**")
                        + " | " + (wonLine ?? "—") + " | " + baseLine + " |");
                }
            }
            finally
            {
                // ⚠️ **반드시 되돌린다** — 안 되돌리면 다음 측정이 키운 판을 재고,
                //    그 수가 실측처럼 남는다.
                PlantScale = 1f;
            }

            head.AppendLine();
            head.AppendLine("⚠️ 훑은 배수 — 1 · 1.5 · 2 · 3 · 4 · 6 · 8 (⚠️ 가정 · 처음 이긴 데서 멈춘다).");
            head.AppendLine();
            head.AppendLine(body.ToString());
            head.AppendLine("############ 끝 ############");
            return head.ToString();
        }

        /// <summary>
        /// 【확인】 **탄종이 여럿이면 화력 천장이 열리는가** (2026-09-18 사용자 물음 ③).
        ///
        /// ⚠️⚠️ **왜 재나.** 키운 판 스위프에서 **탄약을 여덟 배로 만들어도 쏜 발이 240 에서
        /// 안 움직였다.** 발사는 **줄마다 제 주기**로 나가고(`FireSide`) 소비는 **그 탄종의**
        /// 재고를 보는데(`ConsumeRound`), 마운트에 한 탄종만 있으면 **나머지 두 줄은 영영
        /// 못 쏜다** — 그 짐작이 맞는지 **같은 판을 두 번 돌려** 가른다.
        ///
        /// ⚠️ **게임의 거동을 재는 것이 아니다.** 게임은 창고에 있는 것을 싣고, 창고에는
        /// 보드가 만든 것만 있다. 여기서 여는 것은 **기계의 상한**이고, 그것을 실제로 쓰려면
        /// **보드가 다른 탄종을 만들어야** 한다 — 그 배치는 설계 몫이다.
        ///
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunAmmoMixProbeBatch</c>
        /// </summary>
        [MenuItem("MBI/Harness 탄종 섞기 확인")]
        public static void RunAmmoMixProbeMenu() => Debug.Log(RunAmmoMixProbe());

        public static void RunAmmoMixProbeBatch()
        {
            Debug.Log(RunAmmoMixProbe());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string RunAmmoMixProbe()
        {
            var head = new StringBuilder();
            var body = new StringBuilder();

            head.AppendLine("############ 탄종 섞기 — 천장이 열리는가 (2026-09-18) ############");
            head.AppendLine();
            head.AppendLine("| 판 | 마운트 | 판정 | 쏜 발 |");
            head.AppendLine("|---|---|---|---|");

            try
            {
                foreach (string id in new[] { "S1", "S2", "S3" })
                    foreach (bool mixed in new[] { false, true })
                    {
                        PreloadMixed = mixed;
                        string report = Run(id, preloadMount: true);
                        StageVerdict v = LastVerdict;

                        // 쏜 발은 보고문에만 있다 — **수로 넘기는 칸이 없어** 여기서만 읽는다.
                        string shots = "?";
                        foreach (string line in report.Split('\n'))
                            if (line.TrimStart().StartsWith("쏜 발"))
                            { shots = line.Trim(); break; }

                        head.AppendLine("| " + id + " | " + (mixed ? "**세 탄종**" : "표준탄만")
                            + " | " + (v.ok ? "✅ " : "") + v.result + " · "
                            + v.seconds.ToString("F1") + "초 · 남은 적 " + v.remaining + "/" + v.total
                            + " | " + shots + " |");

                        body.AppendLine();
                        body.AppendLine("======== " + id + " · "
                            + (mixed ? "세 탄종" : "표준탄만") + " ========");
                        body.AppendLine(report);
                    }
            }
            finally { PreloadMixed = false; }

            head.AppendLine();
            head.AppendLine("⚠️ **게임의 거동이 아니다** — 게임은 창고에 있는 것을 싣고,");
            head.AppendLine("창고에는 **보드가 만든 것**만 있다. 여기서 여는 것은 **기계의 상한**이다.");
            head.AppendLine();
            head.AppendLine(body.ToString());
            head.AppendLine("############ 끝 ############");
            return head.ToString();
        }

        private static string VerdictRow(StageVerdict v)
        {
            string mark = v.result == CombatResult.InProgress
                ? "⚠️ **안 끝났다**"
                : (v.ok ? "✅ " + v.result : "❌ **" + v.result + "**");

            return "| **" + v.stageId + "** | " + v.how + " | " + mark + " | "
                   + v.seconds.ToString("F1") + "초 · 남은 적 " + v.remaining + "/" + v.total
                   + " · HP " + v.hp.ToString("F0") + "/" + v.maxHp.ToString("F0") + " |";
        }

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
            => Run(stageId, preloadMount, propellantNeed: BasePropellantNeed);

        /// <summary>
        /// 【스위프】 **추진제 필요 생산치**만 바꿔 돌린다 (2026-09-17 · `260917_W03` 2-1).
        ///
        /// ⚠️⚠️ **`balance_v4.json` 과 노드 자산은 안 건드린다.** 자산을 고치면 그 값이
        /// 디스크에 남아 다음 빌드와 다음 측정이 **다른 판**을 보게 된다.
        /// 여기서는 노드 자산의 **복제본**을 만들어 추진제 조합표의 산출률만 바꾼다.
        /// </summary>
        public static string Run(string stageId, bool preloadMount, float propellantNeed)
            => Run(stageId, preloadMount, propellantNeed, hpOverride: 0f);

        /// <summary>
        /// 【스위프】 추진제 필요 생산치와 **로봇 HP** 를 바꿔 돌린다
        /// (2026-09-17 · `260917_W04` 4장).
        ///
        /// ⚠️⚠️ **배포 값은 안 건드린다.** HP 는 `CombatTuning.asset` 의 `robotHp` 에 사는데,
        /// 그 자산을 고치면 디스크에 남아 **빌드와 다음 측정이 바뀐 값을 보게 된다.**
        /// 여기서는 세우는 `RobotSetup` 의 `hp` 만 갈아 끼운다 — 자산은 그대로다.
        /// <paramref name="hpOverride"/> 가 0 이면 자산 값을 쓴다.
        /// </summary>
        public static string Run(string stageId, bool preloadMount, float propellantNeed,
            float hpOverride)
            => Run(stageId, preloadMount, propellantNeed, hpOverride, shieldOff: false);

        /// <summary>
        /// 【스위프】 위에 **쉴드 최대치·재료 1개당 충전량**을 얹은 판
        /// (2026-09-17 · `260917_W05` 5-1 · `260917_W06` 5장).
        ///
        /// ⚠️⚠️ **배포 값도 배포 보드도 안 건드린다.** 쉴드 값은 `balance_v4.json` 에서 0 이고
        /// (설계가 값을 안 줬다) 시작 보드에는 쉴드 줄이 없다. 여기서는 **하네스 안에서만**
        /// 줄을 한 벌 더 놓고 값을 갈아 끼운다 — 그래야 「값이 얼마면 살아남나」를
        /// 자산을 더럽히지 않고 잴 수 있다.
        ///
        /// ⚠️ **대당 최대치를 받는다**(2026-09-17 사용자 확정). 🗑️ 구 인자 `shieldMax`(판 전체의
        /// 고정 그릇) 폐기 — 최대치는 이제 **놓인 발생 노드 수 × 이 값**이다. 하네스가 제 수를
        /// 들면 러너와 다른 판을 재게 된다(`ShieldSystem.MaxFrom` 이 둘의 하나뿐인 문이다).
        ///
        /// <paramref name="shieldOff"/> 가 참이면 **배포 보드에서 쉴드 발생 노드 하나만 뽑아**
        /// 돌린다 — 견주기 위한 기준선이고 배포 거동이 아니다.
        /// ⚠️ **줄 전체를 걷는 것이 아니다**(벨트·가공·군수는 그대로) — 축을 하나만 움직이려는 것이다.
        /// </summary>
        public static string Run(string stageId, bool preloadMount, float propellantNeed,
            float hpOverride, bool shieldOff)
            => Run(stageId, preloadMount, propellantNeed, hpOverride, shieldOff, autoPilot: true);

        /// <summary>
        /// 위와 같되 **자동 조종을 끄고** 돌릴 수 있다(2026-09-17).
        /// ⚠️ 끈 판은 **게임과 다른 판**이다 — 09-17 이전 측정이 전부 그 상태였다는 것을
        /// 드러내려고 남긴다. 견주기 위한 것이지 기준이 아니다.
        /// </summary>
        public static string Run(string stageId, bool preloadMount, float propellantNeed,
            float hpOverride, bool shieldOff, bool autoPilot)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== 스테이지 {stageId} 클리어 하네스 (2026-09-17) ===");
            // ⚠️ 배수는 **자산 값 대비**다(구 상수 150 대비 폐기 — 위 `BasePropellantNeed` 머리말).
            float needBase = BasePropellantNeed;
            sb.AppendLine($"[판] 추진제 필요 생산치 **{propellantNeed:F0}**"
                          + $" (자산 {needBase:F0} 대비 ×{needBase / propellantNeed:F2}"
                          + (Mathf.Approximately(propellantNeed, needBase) ? " · 기준)" : ")"));
            if (hpOverride > 0f) sb.AppendLine($"[판] 로봇 HP **{hpOverride:F0}**(하네스 전용 · 자산은 그대로)");
            sb.AppendLine(shieldOff
                ? "[판] 쉴드 **발생 노드를 뽑았다**(벨트·가공·군수는 그대로) — 견주기 위한 기준선"
                : "[판] 쉴드 줄 **배포 보드에 포함** — 값은 자산이 든다(대당 최대치 · 충전 비율)");
            sb.AppendLine(autoPilot
                ? "[판] 자동 조종 **켬** — 게임과 같은 문(2026-09-17 신설)"
                : "[판] 자동 조종 **끔** — 로봇이 제자리에 선다(09-17 이전 하네스가 이 상태였다)");
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

            // 회피 스택 계수 — **게임과 같은 자산에서** 읽는다(2026-09-17 · `260917_W04` 2장).
            //    안 읽으면 하네스가 코드 기본값으로 재서 화면과 다른 판을 본다.
            if (robot.balanceRef != null && robot.balanceRef.dodgeStacksPerBooster > 0)
                DodgeSystem.StacksPerBooster = robot.balanceRef.dodgeStacksPerBooster;

            var catalog = new List<EnemyDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyDefinition"))
                catalog.Add(AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(guid)));

            // ── 판 세우기 ──────────────────────────────────────────────────
            BoardGrid grid = Board(propellantNeed);
            // 🗑️ **구 `PlaceShieldLine(grid)` 폐기 — 2026-09-17.** 쉴드 줄이 **배포 보드에
            //    들어왔으므로**(`260917_W07` 4장 1번) 하네스가 제 줄을 따로 놓으면
            //    **같은 일을 하는 자리 둘**이 된다. 지금은 시작 보드가 가진 것을 그대로 잰다.
            //
            // ⚠️ `shieldOff` 는 **견주기 위한 기준선**이다 — 배포 보드에서 **쉴드 발생 노드
            //    하나만** 뽑아 「쉴드가 얼마를 바꿨나」를 같은 판에서 본다. 배포 거동이 아니다.
            if (shieldOff)
            {
                RemoveShieldNode(grid);
                BeltAutoOrient.Resolve(grid);
                BeltFlow.Resolve(grid);
            }
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
                hp = hpOverride > 0f ? hpOverride : tuning.robotHp,
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
                if (PreloadMixed)
                {
                    // ⚠️⚠️ **세 탄종을 고르게 싣는다**(2026-09-18 사용자 물음 ③ 확인용).
                    //    발사는 **줄마다 제 주기**로 나가고 `ConsumeRound` 는 **그 탄종의**
                    //    재고를 본다 — 한 탄종만 실으면 나머지 두 줄은 영영 못 쏜다.
                    //    그 짐작이 맞는지 재려고 둔 갈림이다.
                    float each = mount.SlotCount * stack / 3f;
                    mount.Load(MountItem.Pierce, each);
                    mount.Load(MountItem.Standard, each);
                    mount.Load(MountItem.Explosive, each);
                }
                else
                {
                    // 마운트를 표준탄으로 가득 채운다 — 용량 그대로(A 4칸 × 스택 10 = 40).
                    // ⚠️ 창고는 **안 채운다.** 튜토리얼은 마운트까지 나르고 끝나며,
                    //    창고 재고는 그 뒤 보드가 대는 것이다 — 지어 넣으면 판이 물러진다.
                    mount.Load(MountItem.Standard, mount.SlotCount * stack);
                }
            }
            List<EnemySpawn> spawns = StageSpawnFactory.Build(stage, catalog, tuning);

            var sim = new CombatSimulation(setup, spawns,
                tuning.arenaRadiusTbd, stage.challengeTime, tuning.spawnCadenceTbd);

            // ⚠️ **띠를 러너와 같게 세운다.** 안 세우면 적이 기본 거리에 한 줄로 서고,
            //    그러면 「가까운 것은 제자리에서 쏘고 먼 것에는 걸어간다」가 안 일어난다 —
            //    게임과 다른 판을 재게 된다(2026-09-15 사용자 확정 · §72-40).
            // 게임과 같은 값으로 곁눈질을 붙든다 — 안 넣으면 떨림 잣대가 다른 판을 잰다.
            sim.SetSideStepHold(tuning.enemySideStepHoldTbd);
            // ⚠️ 누적형 활동 범위도 러너와 같게 건다(2026-09-18) — 안 걸면 다른 판을 잰다.
            sim.DroneLeashRadius = tuning.droneLeashRadiusTbd;
            // 한 발 쪼개 쏘기도 러너와 같게 건다 — 안 걸면 재는 판이 게임과 달라진다.
            sim.ShotsPerRound = tuning.shotsPerRound;
            sim.ShotDamageFactor = tuning.shotDamageFactor;

            // ⚠️⚠️ **웨이브를 러너와 같게 건다**(2026-09-18 · 시안 3 ③).
            //    안 걸면 하네스는 **한 마리씩** 오는 판을, 게임은 **묶음으로** 오는 판을 돌린다 —
            //    09-15 에 「프로브는 초록인데 화면은 낡았다」로 겪은 그 모양이다.
            sim.SetWave(tuning.waveSizeTbd,
                WaveSpawnRule.Interval(tuning.waveIntervalSecondsTbd,
                                       tuning.waveSizeTbd, tuning.spawnCadenceTbd));

            if (tuning.spawnRingMinTbd > 0f && tuning.spawnRingMaxTbd > 0f)
                sim.SetSpawnBand(tuning.spawnRingMinTbd, tuning.spawnRingMaxTbd);
            else if (tuning.spawnRingRadiusTbd > 0f)
                sim.SetSpawnRing(tuning.spawnRingRadiusTbd);
            else
                sb.AppendLine("  ⚠️ 스폰 띠·링이 튜닝에 없다 — 시뮬 기본값으로 돈다(게임과 다를 수 있다)");

            sb.AppendLine();
            sb.AppendLine("[판] " + stage.stageId + " · " + stage.topic);
            sb.AppendLine($"  요구치 {stage.req:F0} · 제한 {stage.challengeTime:F0}s · 적 {spawns.Count} 기");
            sb.AppendLine($"  로봇 HP {tuning.robotHp:F0} · 사거리 {tuning.robotAttackRangeTbd:F1}"
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

            // 쉴드 그릇 — **보드가 정한다**(`ShieldSystem.MaxFrom` · 러너와 같은 문).
            float shieldMaxPerNode = robot.balanceRef != null ? robot.balanceRef.shieldMaxPerNode : 0f;
            float shieldMax = ShieldSystem.MaxFrom(agg.shieldNodeCount, shieldMaxPerNode);

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

            // 쉴드 표본 — 「0 인 틱」과 「만충 틱」은 **다른 병**을 가리킨다.
            //   0 이 많다 = 재료가 모자라 못 채운다(공급 부족).
            //   만충이 많다 = 그릇이 남아돈다(맞는 것보다 채워지는 것이 빠르다).
            int tickShieldCountable = 0, tickShieldEmpty = 0, tickShieldFull = 0;

            // 대당 재료 소비 — 측정용 고정값 1/초(`balance_v4.json` · `shieldMaterialPerSec`).
            float shieldMaterialPerSec = robot.balanceRef != null
                ? robot.balanceRef.shieldMaterialPerSec : 1f;

            // 초당 충전 비율 — **자산이 원천**이다(사용자 확정 0.025/초 · 빈 게이지가 40초).
            float shieldChargeRatio = robot.balanceRef != null
                ? robot.balanceRef.shieldChargeRatioPerSec : 0.025f;

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

            // ── 회피 축 (2026-09-17 · `260917_W02` 2-2) ────────────────────
            //
            // **회피 스택 최고** = 이 판에서 그릇에 가장 많이 담겼던 수. 상한은 대수 × 2 다.
            // **추진제 소진 틱 비율** = 부스터가 있는데 **스택이 0 인** 틱 ÷ 전체 틱.
            //   ⚠️ 부스터가 없는 판(가)에서는 뜻이 없어 **안 센다** — 0/0 을 0% 로 적으면
            //   「소진이 없었다」로 읽혀 거짓말이 된다.
            int peakDodgeStacks = 0;
            int tickDodgeEmpty = 0;
            int tickDodgeCountable = 0;

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

                // 쉴드 — 러너와 **같은 문**(`ShieldSystem.ChargeFrom`)으로 낸다(§7).
                //    ⚠️ 매 틱 다시 쓴다: 한 번만 쓰면 러너와 다른 판을 재게 된다.
                sim.ShieldMax = shieldMax;   // 위에서 노드 수 × 대당으로 냈다(러너와 같은 문)
                sim.ShieldChargeRate = ShieldSystem.ChargeFrom(
                    shieldMax, shieldChargeRatio,
                    agg.shieldMaterialProduce, agg.shieldNodeCount, shieldMaterialPerSec);

                // 4-0) **자동 조종** — 게임이 지나는 문을 여기서도 지난다
                //      (2026-09-17 · `260917_W07` 3장 재측정).
                //
                // ⚠️⚠️ **여태 하네스에는 이 단이 없었다.** 로봇이 **한 발짝도 안 움직이는** 판을
                //    재고 있었고, 그래도 수가 맞아 보였던 까닭은 회피가 자리를 안 바꿨기 때문이다.
                //    09-17 에 회피 이동이 붙으면서 **밀린 자리에서 영영 안 돌아오는** 판이 됐고,
                //    S1 이 타임아웃으로 졌다. 게임에서는 자동 조종이 곧바로 걸어서 돌아온다.
                //    09-15 §7 등재: 「재는 것과 보는 것은 다른 일 — 프로브는 사람이 지나는 문을 안 지난다」.
                //
                // ⚠️ **회피로 밀리는 동안은 양보한다** — 러너와 같은 규칙이다.
                if (autoPilot && sim.Robot != null && !sim.DodgeMotionActive)
                {
                    var ctx = new AutoPilotContext
                    {
                        robotPos = sim.Robot.position,
                        enemies = sim.Enemies,
                        arenaRadius = tuning.arenaRadiusTbd,
                        attackRange = tuning.robotAttackRangeTbd,
                        moveSpeed = tuning.robotMoveSpeedTbd,
                        holdWhenMoreThan = tuning.autoPilotHoldWhenMoreThanTbd,
                        dt = Dt,
                    };
                    sim.Robot.position = AutoPilotPolicy.NextPosition(ctx);
                }

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

                // 회피 — **틱마다 본다.** 끝에서 한 번 보면 마지막 값만 남는다.
                DodgeSystem dodge = sim.Dodge;
                if (dodge.Stacks > peakDodgeStacks) peakDodgeStacks = dodge.Stacks;
                if (dodge.Capacity > 0)
                {
                    tickDodgeCountable++;
                    if (dodge.Stacks <= 0) tickDodgeEmpty++;
                }

                // 쉴드 — **틱마다 본다**(회피와 같은 결). 끝에서 한 번 보면 마지막 값만 남는다.
                if (shieldMax > 0f)
                {
                    tickShieldCountable++;
                    if (sim.Shield.Value <= 0f) tickShieldEmpty++;
                    else if (sim.Shield.IsFull) tickShieldFull++;
                }

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
            LastVerdict = new StageVerdict
            {
                stageId = stageId, how = "한 로봇",
                ok = result == CombatResult.Win, result = result, seconds = elapsed,
                remaining = sim.Remaining, total = sim.TotalEnemies,
                hp = sim.Robot.hp, maxHp = sim.Robot.maxHp,
            };

            sb.AppendLine();
            sb.AppendLine("[결과]");
            bool capped = result == CombatResult.InProgress;
            sb.AppendLine(capped
                ? $"  ⚠️ **안 끝났다** — {HardCapSeconds:F0}초에서 끊었다. 「졌다」가 아니다."
                : $"  {result} · {elapsed:F1}초");
            sb.AppendLine($"  남은 적 {sim.Remaining}/{sim.TotalEnemies} · 로봇 HP {sim.Robot.hp:F0}/{sim.Robot.maxHp:F0}");
            if (result == CombatResult.Win)
                sb.AppendLine($"  ✅ **이겼다 — 끝날 때 남은 HP {sim.Robot.hp:F0}"
                              + $" ({sim.Robot.hp / Mathf.Max(1f, sim.Robot.maxHp) * 100f:F0}%)**"
                              + "  ← 여유가 얼마인지가 값을 고를 때의 입력이다(`260917_W04` 4-2)");
            sb.AppendLine($"  쏜 발 {shots}");
            sb.AppendLine(firstArrivalAt >= 0f
                ? $"  첫 도착 {firstArrivalAt:F1}초"
                : "  첫 도착 **없음** — 마운트에 한 알도 안 닿았다(운반로가 끊겼다)");
            sb.AppendLine(firstShotAt >= 0f
                ? $"  첫 발사 {firstShotAt:F1}초"
                : "  첫 발사 **없음**");

            sb.AppendLine();
            sb.AppendLine("[회피 축 — `260917_W02` 2-2]");
            sb.AppendLine($"  부스터 {agg.boosterCount} 대 · 회피 스택 상한 {sim.Dodge.Capacity}"
                          + $" · 추진제 유입 {agg.propellantProduce:F4} 개/초");
            sb.AppendLine($"  **자동 회피 발동 {sim.Dodge.TotalDodges} 회**"
                          + "  ← 측정법: `DodgeSystem.TotalDodges`(발동할 때마다 1 증가) 를 판 끝에서 읽는다");
            sb.AppendLine($"  회피 스택 최고 {peakDodgeStacks}"
                          + "  ← 측정법: 매 틱 `Dodge.Stacks` 를 보고 그중 최대");
            sb.AppendLine(tickDodgeCountable > 0
                ? $"  추진제 소진 틱 {tickDodgeEmpty}/{tickDodgeCountable}"
                  + $" ({tickDodgeEmpty * 100f / tickDodgeCountable:F1}%)"
                  + "  ← 측정법: 부스터가 있는 틱 중 `Stacks == 0` 인 틱의 비율"
                : "  추진제 소진 틱 **못 잰다** — 부스터가 0 대라 그릇 자체가 없다");
            sb.AppendLine($"  **전력 효율 {throttle.power:F3}** (공급 {agg.powerSupply:F0}"
                          + $" · 수요 {agg.powerDraw:F1})"
                          + "  ← 측정법: `LogisticsSimulation.Throttles` 의 `power` = min(1, 공급/수요)");

            // ── 새 열 셋 (2026-09-17 · `260917_W03` 2-2) ──
            sb.AppendLine($"  **피격 {sim.HitsTaken} 회**(회피로 무효가 된 것 포함)"
                          + "  ← 측정법: 근접·포탄이 **명중 판정에 들어온** 순간마다 1 증가");
            sb.AppendLine($"  **받은 총 피해 {sim.DamageTaken:F0}**"
                          + "  ← 측정법: 실제로 **HP 에서** 깎인 값의 합."
                          + " 🗑️ 구 「쉴드는 시뮬에 없다」 폐기 — 09-17 에 쉴드가 붙었고 그 몫은 아래 줄이 센다");
            sb.AppendLine($"  **회피로 무효화한 피해 {sim.DamageAvoided:F0}**"
                          + "  ← 측정법: 무적이라 **계산에 들어가지도 않은** 공격력의 합");

            sb.AppendLine($"  마운트 최고 도착률 {peakArrival:F2} 발/초");
            sb.AppendLine($"  재배분 {reallocs} 회 — 0 이면 라인이 시작값(빈 줄)으로 굳은 것이다");

            // ── 쉴드 축 (2026-09-17 · `260917_W05` 5-1 · `260917_W06` 5장) ──
            sb.AppendLine();
            sb.AppendLine("[쉴드 축]");
            if (shieldMax <= 0f)
            {
                sb.AppendLine(shieldOff
                    ? "  쉴드 **발생 노드를 뽑았다** — 이 판이 기준선이다(벨트·가공·군수는 그대로 돈다)"
                    : "  ⚠️ 쉴드 줄이 배포 보드에 있는데 **최대치가 0** 이다 — 발생 노드가 안 이어졌다");
            }
            else
            {
                sb.AppendLine($"  최대치 {shieldMax:F0}"
                              + $" (발생 노드 {agg.shieldNodeCount} 대 × 대당 {shieldMaxPerNode:F0})"
                              + $" · 충전률 {sim.ShieldChargeRate:F2}/초"
                              + $" (비율 {shieldChargeRatio * 100f:F1}%/초"
                              + $" · 재료 {agg.shieldMaterialProduce:F3} 개/초 · 대당 소비 {shieldMaterialPerSec:F0})"
                              + "  ← 측정법: `ShieldSystem.ChargeFrom` = 최대치 × 비율 × (먹은 재료 ÷ 먹고 싶은 재료)");
                float fillSeconds = sim.ShieldChargeRate > 0f ? shieldMax / sim.ShieldChargeRate : 0f;
                sb.AppendLine($"  빈 게이지가 차는 데 {fillSeconds:F1}초"
                              + "  ← 측정법: 최대치 ÷ 충전률. **비율이 고정이라 그릇을 키워도 이 수는 안 변한다**");
                sb.AppendLine($"  **쉴드가 막은 피해 {sim.ShieldAbsorbed:F0}**"
                              + "  ← 측정법: 피격마다 게이지에서 실제로 빠진 양의 합(`ShieldSystem.Absorbed`)."
                              + " ⚠️ 넘친 몫은 여기 안 들고 위의 「받은 총 피해」로 간다");
                sb.AppendLine($"  쉴드 0 인 틱 {tickShieldEmpty}/{tickShieldCountable}"
                              + $" ({tickShieldEmpty * 100f / Mathf.Max(1, tickShieldCountable):F1}%)"
                              + "  ← 측정법: 매 틱 `Shield.Value <= 0` 인 틱의 비율 · **재료가 모자란다는 뜻**");
                sb.AppendLine($"  쉴드 만충 틱 {tickShieldFull}/{tickShieldCountable}"
                              + $" ({tickShieldFull * 100f / Mathf.Max(1, tickShieldCountable):F1}%)"
                              + "  ← 측정법: 매 틱 `Shield.IsFull` 인 틱의 비율 · **그릇이 남아돈다는 뜻**");
                sb.AppendLine("  쉴드 줄이 쓴 칸 6 칸(벨트 3 + 가공 1 + 군수 1 + 발생 1)"
                              + "  ← 측정법: **시작 보드 배치**에서 센다(하네스가 따로 안 놓는다)."
                              + " ⚠️ **가공을 표준탄 줄과 공유하지 않는 판정**이라 한 칸이 더 든다");
            }

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

        /// <summary>
        /// 【판 다】 **B 보드만 120초 가동** — 전투 없음 (`260917_W02` 2-2).
        ///
        /// ⚠️ S1 은 로봇 A 만 싸우므로 이 판은 S1 결과에 **안 들어간다.** 재는 것은
        /// 「**부스터 줄이 B 드론 라인을 얼마나 깎는가**」 하나다.
        ///
        /// 📌 **현행 116 과 같은 방법으로 잰다** — `BoardItemTick.Step` 을 0.05 초씩 120 초,
        /// `PendingMountArrivals` 의 수를 센다(`StartingBoardBTests` 와 같은 문).
        /// 배율은 **둘 다 낸다**: 116 과 견주려면 그쪽과 같은 **1.0** 이어야 하고,
        /// 전력이 실제로 어떤지는 **실제 배율**이 말한다. 하나만 적으면 둘 중 하나가 거짓이 된다.
        /// </summary>
        public static string RunBoardB(float propellantNeed)
        {
            const float Seconds = 120f;
            var sb = new StringBuilder();
            sb.AppendLine($"=== B 보드 120초 가동 (전투 없음) · 추진제 필요 생산치 "
                          + $"**{propellantNeed:F0}** (자산 {BasePropellantNeed:F0} 대비 ×{BasePropellantNeed / propellantNeed:F2}) ===");

            BoardGrid grid = BoardB(propellantNeed);

            var config = AssetDatabase.LoadAssetAtPath<LogisticsConfig>($"{SoRoot}/LogisticsConfig.asset");
            ICollection<Vector2Int> connected = LogisticsReach.ConnectedNodes(grid);
            WorkloadRate.Result work = WorkloadRate.Compute(grid, connected, null);
            NetworkAggregate agg = LogisticsNetwork.Aggregate(grid, connected, work);
            ProductionThrottle throttle = LogisticsSimulation.Throttles(
                agg.powerSupply, agg.powerDraw,
                agg.heatGenerate, config != null ? config.moduleCoolingTbd : 0f,
                config != null ? config.heatThreshold : 12f);

            sb.AppendLine($"  이어진 노드 {connected.Count} · 부스터 {agg.boosterCount} 대"
                          + $" · 추진제 {agg.propellantProduce:F4} 개/초"
                          + $" · 누적형 드론 {agg.droneStackProduce:F3} 기/초");
            sb.AppendLine($"  **코어 에너지 사용량 {CoreEnergyDraw(grid, connected):F2} 개/초**"
                          + "  ← 측정법: 이어진 노드 중 **코어 에너지를 먹는 조합표**의"
                          + " 입력 소요를 더한다(코어 산출은 10 개/초)");
            sb.AppendLine($"  **전력 효율 {throttle.power:F3}** (공급 {agg.powerSupply:F0}"
                          + $" · 수요 {agg.powerDraw:F1})");

            // 도착 — 두 배율로 각각 센다. 판을 새로 세워야 한다(앞 판의 버퍼가 남는다).
            int arrivedUnit = CountArrivals(propellantNeed, 1f, Seconds);
            int arrivedReal = Mathf.Approximately(throttle.Scale, 1f)
                ? arrivedUnit : CountArrivals(propellantNeed, throttle.Scale, Seconds);

            sb.AppendLine($"  **드론 도착 {arrivedUnit} 기**(배율 1.0 · 현행 116 과 같은 잣대)");
            sb.AppendLine($"  드론 도착 {arrivedReal} 기(실제 배율 {throttle.Scale:F3})");
            sb.AppendLine("  ← 측정법: `BoardItemTick.Step` 0.05초 × 2400 틱,"
                          + " 매 틱 `PendingMountArrivals.Count` 를 더한다");

            // 회피 스택 — **전투가 없어 쓰는 쪽이 없다.** 그래서 「최고」는 곧 상한이고,
            // 뜻을 갖는 것은 **언제 상한에 닿는가**다.
            if (agg.boosterCount > 0)
            {
                var dodge = new DodgeSystem { BoosterCount = agg.boosterCount };
                float carry = 0f, t = 0f, fullAt = -1f;
                int steps = Mathf.RoundToInt(Seconds / Dt);
                for (int i = 0; i < steps; i++)
                {
                    carry += agg.propellantProduce * throttle.Scale * Dt;
                    while (carry >= 1f) { if (dodge.AddStacks(1) == 0) { carry = 0f; break; } carry -= 1f; }
                    t += Dt;
                    if (fullAt < 0f && dodge.Stacks >= dodge.Capacity) fullAt = t;
                }
                sb.AppendLine($"  회피 스택 최고 {dodge.Stacks}/{dodge.Capacity}"
                              + (fullAt >= 0f ? $" · 상한 도달 {fullAt:F1}초" : " · **120초 안에 상한에 못 닿았다**"));
                sb.AppendLine("  ⚠️ 전투가 없어 **쓰는 쪽이 없다** — 이 수는 「채우는 속도」이지"
                              + " 실전의 스택이 아니다.");
            }
            else
            {
                sb.AppendLine("  회피 스택 — 부스터가 0 대라 그릇이 없다");
            }

            return sb.ToString();
        }

        /// <summary>판을 새로 세워 도착 수만 센다 — 버퍼가 남지 않게 매번 다시 짓는다.</summary>
        private static int CountArrivals(float propellantNeed, float scale, float seconds)
        {
            BoardGrid g = BoardB(propellantNeed);
            var flow = new BeltItemFlow();
            flow.Rebuild(g);

            int arrived = 0;
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                BoardItemTick.Step(g, flow, Dt, scale);
                arrived += flow.PendingMountArrivals.Count;
                flow.ClearPendingMountArrivals();
            }
            return arrived;
        }

        /// <summary>
        /// 【`260917_W02` 2-2】 **측정 세 판을 한 번에** — 가 · 나 · 다.
        ///
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunW02Batch</c>
        /// </summary>
        /// <summary>
        /// 【`260917_W04` 4장】 **HP 여섯 판** — HP 1000·1250·1500 × 추진제 150·30.
        ///
        /// ⚠️ 조건부다 — 「게임에도 쉴드 판정이 없다」일 때만 돌리라고 했고, 코드를 봤더니
        /// 쉴드는 **노드 스텁 하나**뿐이라 전투에 게이지도 판정도 없다(회신문 3장).
        /// ⚠️ **스택 상한은 새 값(부스터당 4칸 = 8)** 으로 돈다 — 자산에서 읽는다.
        /// </summary>
        [MenuItem("MBI/Harness W04 HP 여섯 판")]
        public static void RunW04Menu() => Debug.Log(RunW04());

        public static void RunW04Batch()
        {
            Debug.Log(RunW04());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string RunW04()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ 260917_W04 4장 HP 여섯 판 ############");
            sb.AppendLine();
            foreach (float need in new[] { 150f, 30f })
            foreach (float hp in new[] { 1000f, 1250f, 1500f })
                sb.AppendLine(Run("S1", preloadMount: true, propellantNeed: need, hpOverride: hp));
            return sb.ToString();
        }

        /// <summary>
        /// 【`260917_W05` 5-1 · `260917_W06` 5장】 **쉴드 측정 네 판**
        /// — 최대치 100·250 × 재료 1개당 충전 5·10.
        ///
        /// 고정: 재료 소비 **1/초**(자산) · 추진제 필요 생산치 **30**(W06 수정) ·
        /// A 시작 보드 + 부스터 줄 + **쉴드 줄** · 적재 40 · HP **1000** · 회피 상한 8.
        ///
        /// ⚠️ **기준선을 같이 낸다** — 쉴드 없는 판(최대치 0)이 맨 앞이다.
        ///    없으면 「쉴드가 얼마를 바꿨나」를 이 문서 안에서 못 읽는다.
        ///
        /// ⚠️⚠️ **넷 다 지면 넓히지 않고 그대로 보고한다**(설계 지시).
        ///    못 미치는 것 자체가 보고 내용이다.
        ///
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunW05Batch</c>
        /// </summary>
        [MenuItem("MBI/Harness W05 쉴드 네 판")]
        public static void RunW05Menu() => Debug.Log(RunW05());

        public static void RunW05Batch()
        {
            Debug.Log(RunW05());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// 🗑️ **폐기 — 2026-09-17.** 네 판은 최대치를 **판에 직접 걸어** 쟀다.
        /// 사용자 결정으로 최대치가 **노드 수 × 대당**이 되면서 그 조건이 성립하지 않는다.
        /// 값은 `260917_V04` 6장에 남아 있고, 다시 재는 것은 <see cref="RunW07"/> 다.
        public static string RunW05()
            => "🗑️ 폐기 — 최대치가 노드 수 × 대당으로 바뀌었다(2026-09-17). RunW07 을 쓴다.";

        /// <summary>
        /// 【사용자 결정 2026-09-17 · 플랜 §79-3】 **쉴드 재측정 두 판** —
        /// 노드 대당 최대치 **200**(노드 1대 = 그릇 200) × 재료 1개당 충전 **5 · 10**.
        ///
        /// 조건은 `260917_V04` 6장과 **같다** — S1 · A 보드 + 부스터 줄 + 쉴드 줄(발생 1) ·
        /// 추진제 30 · HP 1000 · 상한 8 · 적재 40. 기준선도 같이 낸다.
        ///
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunW07Batch</c>
        /// </summary>
        [MenuItem("MBI/Harness W07 쉴드 재측정 두 판")]
        public static void RunW07Menu() => Debug.Log(RunW07());

        public static void RunW07Batch()
        {
            Debug.Log(RunW07());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string RunW07()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ 쉴드 재측정 한 판 (대당 최대치 200 · 비율 2.5%/초) ############");
            sb.AppendLine();
            // 🗑️ 구 「두 판(개당 충전 5 · 10)」 폐기 — 충전률이 **최대치의 고정 비율**이 되면서
            //    개당 충전량이 파생값이 됐다. 이제 움직일 손잡이가 하나뿐이라 한 판이다.
            // 기준선(쉴드 줄을 뽑은 판) · 배포 보드 — 둘 다 **자동 조종 켠** 판이다.
            sb.AppendLine(Run("S1", preloadMount: true, propellantNeed: 30f, hpOverride: 1000f,
                              shieldOff: true));
            sb.AppendLine(Run("S1", preloadMount: true, propellantNeed: 30f, hpOverride: 1000f,
                              shieldOff: false));

            // ⚠️ **자동 조종을 끈 판** — 09-17 이전 하네스와 같은 조건이다.
            //    V04 6장의 수와 이 판을 견줘야 「무엇이 달라졌나」가 갈린다.
            sb.AppendLine(Run("S1", preloadMount: true, propellantNeed: 30f, hpOverride: 1000f,
                              shieldOff: false, autoPilot: false));
            return sb.ToString();
        }

        /// <summary>추진제 스위프의 네 값 — `260917_W03` 2-1. **밸런스 자산은 안 건드린다.**</summary>
        private static readonly float[] SweepNeeds = { 150f, 75f, 50f, 30f };

        [MenuItem("MBI/Harness W03 추진제 스위프")]
        public static void RunW03Menu() => Debug.Log(RunW03());

        public static void RunW03Batch()
        {
            Debug.Log(RunW03());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>【`260917_W03` 2장·4장】 스위프 A(S1 네 판) + 스위프 B(B 보드 네 판).</summary>
        public static string RunW03()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ 260917_W03 추진제 스위프 ############");
            sb.AppendLine();
            sb.AppendLine("======== 스위프 A — S1 (튜토리얼 종료 · 적재 40) ========");
            foreach (float need in SweepNeeds)
                sb.AppendLine(Run("S1", preloadMount: true, propellantNeed: need));

            sb.AppendLine("======== 스위프 B — B 보드 120초 가동 ========");
            foreach (float need in SweepNeeds)
                sb.AppendLine(RunBoardB(need));
            return sb.ToString();
        }

        private static float DamageOf(AmmoKind kind)
        {
            var robot = AssetDatabase.LoadAssetAtPath<RobotDefinition>($"{SoRoot}/Robots/Robot_A.asset");
            if (robot == null) return 0f;
            foreach (WeaponSpec w in robot.weapons) if (w.kind == kind) return w.damagePerShot;
            return 0f;
        }

        /// <summary>
        /// 🗑️ **구 상수 `BasePropellantNeed = 150f` 폐기 — 2026-09-17.**
        ///
        /// ⚠️⚠️ **이것이 오늘의 내 결함이다.** 09-17 에 사용자 확정으로 필요 생산치가
        /// `balance_v4.json` 으로 옮겨 가 **30** 이 됐는데, 하네스는 150 을 기준으로
        /// 배수를 셈하고 있었다 — 「추진제 30 판」이 실제로는 **자산 30 을 다시 5배 한 판**
        /// (사실상 필요치 6)이었다. `260917_V03` 의 「30/1000 도 진다」가 그래서 틀렸다.
        /// 지침 §7 「한 값이 두 곳에 살면 답이 둘이 된다」 그대로다.
        ///
        /// 지금은 **자산이 기준**이다 — 요청한 값이 자산 값과 같으면 배수가 1 이다.
        /// </summary>
        public static float BasePropellantNeed
        {
            get
            {
                var bal = AssetDatabase.LoadAssetAtPath<BalanceConfig>($"{SoRoot}/BalanceConfig.asset");
                return bal != null && bal.propellantNeed > 0f ? bal.propellantNeed : 30f;
            }
        }

        /// <summary>
        /// 추진제 산출률만 바꾼 **노드 자산 복제본**을 대는 손.
        ///
        /// ⚠️⚠️ **원본을 안 고친다.** `AssetDatabase` 가 주는 것은 디스크의 그 자산이라,
        /// 여기서 값을 바꾸면 **빌드와 다음 측정이 바뀐 값을 보게 된다.**
        /// 복제본은 메모리에만 살고 저장되지 않는다.
        /// </summary>
        private static System.Func<string, NodeDefinition> NodeResolver(float propellantNeed)
        {
            if (Mathf.Approximately(propellantNeed, BasePropellantNeed)) return Node;

            float scale = BasePropellantNeed / Mathf.Max(1f, propellantNeed);
            NodeDefinition src = Node(StartingBoard.MuniId);
            if (src == null) return Node;

            var clone = Object.Instantiate(src);
            clone.name = src.name;
            if (clone.recipes != null)
                for (int i = 0; i < clone.recipes.Count; i++)
                {
                    NodeRecipe r = clone.recipes[i];
                    if (r.kind != RecipeKind.Propellant) continue;
                    r.outputPerSec *= scale;
                    clone.recipes[i] = r;
                }

            return id => id == StartingBoard.MuniId ? clone : Node(id);
        }

        /// <summary>시작 보드 + 튜토리얼 칸 — **운반로가 이어진 판**이다.</summary>
        // 🗑️ **폐기 — 2026-09-17.** `PlaceShieldLine(BoardGrid)` 와 `MuniWith(RecipeKind)` 를
        //    걷었다. 쉴드 줄이 **배포 보드**에 들어가면서(`260917_W07` 4장 1번) 하네스가
        //    제 줄을 따로 놓을 까닭이 사라졌다 — 두면 **같은 배치가 두 곳에 사는** 자리가 된다.
        //    자리 · 조합표 · 「가공을 안 나눠 쓴다」는 이제 `StartingBoard` 주석이 든다.

        /// <summary>
        /// **쉴드 발생 노드를 뽑는다** — 견주기 위한 기준선을 만드는 자리다(배포 거동이 아니다).
        /// 발생 노드 하나만 걷는다: 그릇이 0 이 되고 벨트·군수는 그대로라 **한 축만 움직인다**.
        /// </summary>
        private static void RemoveShieldNode(BoardGrid g)
        {
            for (int x = 0; x < g.Columns; x++)
            for (int y = 0; y < g.Rows; y++)
            {
                var cell = new Vector2Int(x, y);
                NodeInstance n = g.GetAt(cell);
                if (n != null && n.Definition != null && n.Definition.type == NodeType.Shield)
                    g.TryRemove(cell);
            }
        }

        private static BoardGrid Board() => Board(BasePropellantNeed);

        /// <summary>
        /// A 시작 보드 + 튜토리얼 칸 — **게임이 세우는 것과 같은 문**(`StartingBoard.Apply`)으로 세운다.
        /// 2026-09-17 부터 배포 배치에 **부스터 둘 + 추진제 줄**이 들어 있다.
        /// </summary>
        private static BoardGrid Board(float propellantNeed)
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f,
                Vector2.zero, PartLayout.BuildMask());

            StartingBoard.Apply(g, NodeResolver(propellantNeed));
            StartingBoard.Place(g, StartingBoard.FillsEmptySlot);

            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        /// <summary>B 시작 보드 — 같은 문(`StartingBoardB.Apply`)으로 세운다.</summary>
        private static BoardGrid BoardB(float propellantNeed)
        {
            var g = new BoardGrid(PartLayout.Columns, PartLayout.Rows, 1f,
                Vector2.zero, PartLayout.BuildMask(), MountOwner.RobotB);

            StartingBoardB.Apply(g, NodeResolver(propellantNeed));

            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }


        /// <summary>
        /// **판 배수** — 「보드를 스테이지마다 키웠다」를 재기 위한 한 손잡이
        /// (2026-09-18 사용자 지시 · ⚠️⚠️ **모형이다 · 설계 판정 자리**).
        ///
        /// ⚠️⚠️ **왜 배치를 안 짓고 배수를 쓰나.** 「S2 에서는 보드가 어디까지 커져 있다」는
        /// **문서에 없다.** 배치를 지어내면 **게임이 세우지 않는 판**을 재게 되고, 그 수는
        /// 실측처럼 보이지만 아무것도 안 말한다 — 09-15 에 「프로브는 초록인데 화면은 낡았다」로
        /// 겪은 자리다.
        ///
        /// 📌 그래서 **판 전체를 k 배**로 본다 — 산출·전력 수요·전력 공급을 함께 곱한다.
        /// 「노드를 k 배로 늘리고 발전도 그만큼 늘린 판」과 **같은 수**가 나오는 모형이고,
        /// 답하는 물음은 하나다 — **「그 판을 이기려면 지금의 몇 배가 필요한가」**.
        ///
        /// ⚠️ **자산을 안 건드린다** — 복제본의 수만 바꾼다(추진제 스위프와 같은 수법).
        /// ⚠️ **1 이면 아무 일도 안 일어난다** — 배포 자산 그대로다.
        /// </summary>
        public static float PlantScale = 1f;

        /// <summary>
        /// 마운트를 **세 탄종으로** 채우는가 (2026-09-18 사용자 물음 ③ 확인용 · 기본 거짓).
        ///
        /// ⚠️ **게임의 거동이 아니다** — 게임은 창고에 있는 것을 싣고, 창고에는 보드가
        /// 만든 것만 있다. 이 갈림은 **「탄종이 여럿이면 천장이 열리는가」**만 잰다.
        /// </summary>
        public static bool PreloadMixed;

        private static NodeDefinition Node(string id)
        {
            var src = AssetDatabase.LoadAssetAtPath<NodeDefinition>(NodeRoot + "/Node_" + id + ".asset");
            if (src == null || Mathf.Approximately(PlantScale, 1f)) return src;

            // ⚠️ 판마다 새로 복제하면 한 판에 수십 개가 생긴다 — id 마다 한 벌만 든다.
            string key = id + "@" + PlantScale.ToString("F2");
            if (_scaled.TryGetValue(key, out NodeDefinition got) && got != null) return got;

            var clone = Object.Instantiate(src);
            clone.name = src.name;

            NodeResourceProfile res = clone.resources;
            res.ammoProduce *= PlantScale;
            res.powerDraw *= PlantScale;
            res.powerSupply *= PlantScale;
            clone.resources = res;

            if (clone.recipes != null)
                for (int i = 0; i < clone.recipes.Count; i++)
                {
                    NodeRecipe r = clone.recipes[i];
                    r.outputPerSec *= PlantScale;
                    clone.recipes[i] = r;
                }

            _scaled[key] = clone;
            return clone;
        }

        private static readonly Dictionary<string, NodeDefinition> _scaled =
            new Dictionary<string, NodeDefinition>();

        /// <summary>
        /// 이 판이 **코어 에너지를 초당 몇 개 먹는가** (2026-09-17 · `260917_W03` 4장).
        ///
        /// 코어 산출은 10 개/초이고, 그 안에서 줄들이 나눠 쓴다. 추진제 줄을 빠르게 돌리면
        /// **가공 한 대가 더 먹으므로** 이 수가 올라간다 — 「회피의 대가」가 있다면 여기 보인다.
        ///
        /// 📌 **집계가 이 값을 안 든다** — `NetworkAggregate` 는 전력·탄약·드론만 센다.
        /// 그래서 여기서 조합표를 직접 훑는다. ⚠️ 값을 짓지 않는다 — 전부 자산의 수다.
        /// </summary>
        private static float CoreEnergyDraw(BoardGrid grid, ICollection<Vector2Int> connected)
        {
            float sum = 0f;
            foreach (Vector2Int cell in connected)
            {
                NodeInstance node = grid.GetAt(cell);
                if (node?.Definition == null) continue;

                NodeRecipe r = node.CurrentRecipe;
                if (r.inputs == null) continue;
                foreach (RecipeInput i in r.inputs)
                    if (i.kind == FlowKind.CoreEnergy) sum += i.perOutput * r.outputPerSec;
            }
            return sum;
        }

        // ══ 태그 두 로봇 판 (2026-09-18 · `260918_W01` 4장) ══════════════════
        //
        // ⚠️⚠️ **왜 판을 하나 더 만드나.** 지금 하네스는 **로봇 하나짜리 생성자**를 써서
        //    `Tag` 가 null 이다 — 태그 스킬이 한 번도 안 나간다. 설계가 재라고 한 셋
        //    (피해 나눔의 대가 · 드론 스택 40 · 두 드론 혼재)은 **태그가 나가는 판에서만**
        //    드러나므로 그 판이 없으면 셋 다 못 잰다.
        //
        // ⚠️ **안 쓸 판은 안 만든다**(구현이 먼저 적고 설계가 받은 경고). 그래서 이 판에는
        //    **재는 목적 넷**이 붙어 있고, 보고 줄도 그 넷에 맞춰 있다.

        /// <summary>
        /// 태그 두 로봇 판 — 보드 둘을 다 돌리고 태그를 자동으로 시킨다.
        ///
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunTagBatch</c>
        /// </summary>
        [MenuItem("MBI/Harness 태그 두 로봇 판 (0918_W01 4장)")]
        public static void RunTagMenu() => Debug.Log(RunTag());

        public static void RunTagBatch()
        {
            Debug.Log(RunTag());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// 스테이지 **점프 판** — S1 · S3 · S5 를 차례로 돈다 (2026-09-18 사용자 결정).
        ///
        /// ⚠️⚠️ **재는 것은 표적 수 하나뿐이다 — 승패는 안 본다.**
        ///    S3·S5 는 로봇을 강화하지 않고 뛰어든 판이라 **지는 것이 정상**이고,
        ///    「졌다」를 밸런스로 읽으면 없는 결론이 선다. 물음은 하나다 —
        ///    **적이 촘촘해지면 광역형 한 대가 몇 마리를 치는가.**
        ///
        /// 📌 **값은 하나도 안 바꾼다** — 반경도 피해 비도 그대로 두고 세기만 한다.
        ///
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunTagJumpBatch</c>
        /// </summary>
        [MenuItem("MBI/Harness 태그 두 로봇 판 — 스테이지 점프 (S1 S3 S5)")]
        public static void RunTagJumpMenu() => Debug.Log(RunTagJump());

        public static void RunTagJumpBatch()
        {
            Debug.Log(RunTagJump());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string RunTagJump()
        {
            var sb = new StringBuilder();
            foreach (string id in new[] { "S1", "S3", "S5" })
            {
                sb.AppendLine(RunTag(id));
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public static string RunTag() => RunTag("S1");

        public static string RunTag(string stageId)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"############ 태그 두 로봇 판 · {stageId} (2026-09-18) ############");
            sb.AppendLine();

            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>($"{SoRoot}/Stages/Stage_{stageId}.asset");
            var robotA = AssetDatabase.LoadAssetAtPath<RobotDefinition>($"{SoRoot}/Robots/Robot_A.asset");
            var robotB = AssetDatabase.LoadAssetAtPath<RobotDefinition>($"{SoRoot}/Robots/Robot_B.asset");
            var tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>($"{SoRoot}/CombatTuning.asset");
            if (stage == null || robotA == null || robotB == null || tuning == null)
            {
                sb.AppendLine("자산이 없다 — 'MBI/Generate Combat Data' 먼저.");
                return sb.ToString();
            }

            BalanceConfig bal = robotA.balanceRef;
            float propellantNeed = bal != null ? bal.propellantNeed : 30f;
            float stack = bal != null ? bal.mountStackLimit : 10f;
            float droneFactor = bal != null && bal.mountDroneStackFactor > 0f
                ? bal.mountDroneStackFactor : MountLoad.DroneStackFactorFallback;
            if (bal != null && bal.dodgeStacksPerBooster > 0)
                DodgeSystem.StacksPerBooster = bal.dodgeStacksPerBooster;

            sb.AppendLine($"[판] {stageId} · 튜토리얼 종료 적재 · 자동 조종 **켬** · 태그 오토 **켬**");
            if (stageId != "S1")
            {
                sb.AppendLine("  ⚠️ **스테이지 점프 판이다 — 승패는 안 본다.** 로봇을 강화하지 않고 뛰어들었다.");
                sb.AppendLine("     여기서 보는 것은 **광역형이 한 대에 몇 마리를 치는가** 하나다.");
            }
            sb.AppendLine("  ⚠️ 태그 오토를 켠 것은 **가정**이다 — 게임 기본값은 꺼짐이고,");
            sb.AppendLine("     사람이 안 누르는 판에서 태그를 재려면 켜는 수밖에 없다.");
            sb.AppendLine($"  A 마운트 {MountLoad.SlotsRobotA} × {stack:F0} = {MountLoad.SlotsRobotA * stack:F0}"
                          + $" · B 마운트 {MountLoad.SlotsRobotB} × {stack * droneFactor:F0}"
                          + $" = {MountLoad.SlotsRobotB * stack * droneFactor:F0} (드론 스택 비 {droneFactor:F2})");
            sb.AppendLine($"  광역 판정 반경 {(bal != null ? bal.droneAoeJudgeRadius : 0f):F1} 칸"
                          + " (⚠️ 잠정 점값 · 설계 확정은 「본전 4마리」)");

            // ── 보드 둘 ────────────────────────────────────────────────────
            BoardGrid gridA = Board(propellantNeed);
            BoardGrid gridB = BoardB(propellantNeed);

            var flowA = new BeltItemFlow(); flowA.Rebuild(gridA);
            var flowB = new BeltItemFlow(); flowB.Rebuild(gridB);
            var deliveryA = new MountDelivery();
            var deliveryB = new MountDelivery();

            var config = AssetDatabase.LoadAssetAtPath<LogisticsConfig>($"{SoRoot}/LogisticsConfig.asset");
            float heatThreshold = config != null ? config.heatThreshold : 12f;
            float cooling = config != null ? config.moduleCoolingTbd : 0f;

            ICollection<Vector2Int> connA = LogisticsReach.ConnectedNodes(gridA);
            ICollection<Vector2Int> connB = LogisticsReach.ConnectedNodes(gridB);
            NetworkAggregate aggA = LogisticsNetwork.Aggregate(
                gridA, connA, WorkloadRate.Compute(gridA, connA, bal));
            NetworkAggregate aggB = LogisticsNetwork.Aggregate(
                gridB, connB, WorkloadRate.Compute(gridB, connB, bal));
            ProductionThrottle throttleA = LogisticsSimulation.Throttles(
                aggA.powerSupply, aggA.powerDraw, aggA.heatGenerate, cooling, heatThreshold);
            ProductionThrottle throttleB = LogisticsSimulation.Throttles(
                aggB.powerSupply, aggB.powerDraw, aggB.heatGenerate, cooling, heatThreshold);

            // 세대 표식 — 시작 보드를 고치면 이 수가 바뀌고, 옛 저장은 그때 버려진다.
            sb.AppendLine($"  B 세대 표식 {StartingBoardB.Generation}");
            sb.AppendLine($"  A 보드 — 이어진 노드 {connA.Count} · 탄약 {aggA.ammoProduce:F2} 발/초"
                          + $" · 전력 {aggA.powerSupply:F0}/{aggA.powerDraw:F0}");
            sb.AppendLine($"  B 보드 — 이어진 노드 {connB.Count} · 드론 {aggB.droneProduce:F2} 기/초"
                          + $" · 광역 몫 {aggB.AoeShare:F2} · 전력 {aggB.powerSupply:F0}/{aggB.powerDraw:F0}");

            // ── 로봇 둘 ────────────────────────────────────────────────────
            SupplySignals.Reset();
            SupplySignals.HasCombat = true;
            SupplySignals.ActiveOwner = MountOwner.RobotA;

            float ammoCapacity = bal != null ? bal.storeCapacity : 40f;
            var mountA = new MountLoad(MountLoad.SlotsRobotA, MountLoad.StandardStacks(stack));
            var mountB = new MountLoad(MountLoad.SlotsRobotB,
                MountLoad.StandardStacks(stack, stack * droneFactor));

            var lines = new List<AmmoLine>();
            ShotAllocator.AllocateRates(robotA.weapons, robotA.consumptionCap,
                SupplySignals.ArrivalRateOf, SupplySignals.MountStockOf, lines);

            float mountCoef = stage.powerModel == StagePowerModel.Logistics
                ? robotA.mountCoef : robotA.enhancedMountCoef;

            var setupA = new RobotSetup
            {
                hp = tuning.robotHp, mountCoef = mountCoef, moduleMult = robotA.moduleMult,
                attackRange = tuning.robotAttackRangeTbd, radius = 0.5f,
                multiShotCount = tuning.multiShotCountTbd,
                aoeRadius = tuning.aoeRadiusTbd, aoeSplashFactor = tuning.aoeSplashFactorTbd,
                lines = new List<AmmoLine>(lines),
                ammoCapacity = ammoCapacity, ammoStore = new AmmoInventory(ammoCapacity),
            };

            // 로봇 B — 본체 무기가 없다(화력은 전부 드론). 러너의 `BuildRobotBSetup` 과 같은 값이다.
            var setupB = new RobotSetup
            {
                hp = tuning.robotHp,
                mountCoef = stage.powerModel == StagePowerModel.Logistics
                    ? robotB.mountCoef : robotB.enhancedMountCoef,
                moduleMult = robotB.moduleMult,
                attackRange = tuning.robotAttackRangeTbd, radius = 0.5f,
                multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
                lines = new List<AmmoLine>(),
                ammoCapacity = ammoCapacity, ammoStore = new AmmoInventory(ammoCapacity),
                droneSlots = bal != null ? bal.droneSlots : 3,
                droneReleaseRate = bal != null ? bal.droneReleaseRate : 1f,
                droneCharge = bal != null ? bal.droneCharge : 100f,
                droneDamagePerHit = (bal != null ? bal.droneCharge : 100f)
                                    * tuning.droneDamageFractionTbd,
                droneAttackRange = tuning.robotAttackRangeTbd,
                droneAoeJudgeRadius = bal != null ? bal.droneAoeJudgeRadius : 0f,
                droneAoeDamageFactor = bal != null ? bal.droneAoeDamageFactor : 0f,
                mountStackLimit = stack,
            };

            // 튜토리얼 종료 판 — A 마운트만 표준탄으로 채운다(B 는 제 보드가 채운다).
            mountA.Load(MountItem.Standard, mountA.SlotCount * stack);

            List<EnemySpawn> spawns = StageSpawnFactory.Build(stage, EnemyCatalog(), tuning);
            var sim = new CombatSimulation(setupA, setupB, mountA, mountB, spawns,
                tuning.arenaRadiusTbd, stage.challengeTime, tuning.spawnCadenceTbd);
            sim.AutoTagEnabled = true;
            sim.SetSideStepHold(tuning.enemySideStepHoldTbd);

            // ⚠️⚠️ **웨이브를 러너와 같게 건다**(2026-09-18 · 시안 3 ③).
            //    안 걸면 하네스는 **한 마리씩** 오는 판을, 게임은 **묶음으로** 오는 판을 돌린다 —
            //    09-15 에 「프로브는 초록인데 화면은 낡았다」로 겪은 그 모양이다.
            sim.SetWave(tuning.waveSizeTbd,
                WaveSpawnRule.Interval(tuning.waveIntervalSecondsTbd,
                                       tuning.waveSizeTbd, tuning.spawnCadenceTbd));
            if (tuning.spawnRingMinTbd > 0f && tuning.spawnRingMaxTbd > 0f)
                sim.SetSpawnBand(tuning.spawnRingMinTbd, tuning.spawnRingMaxTbd);

            // 쉴드 — 두 보드가 각자 낸다(러너와 같은 문).
            float shieldPerNode = bal != null ? bal.shieldMaxPerNode : 0f;
            float shieldRatio = bal != null ? bal.shieldChargeRatioPerSec : 0.025f;
            float shieldMatPerSec = bal != null ? bal.shieldMaterialPerSec : 1f;

            // ── 잰다 ───────────────────────────────────────────────────────
            int steps = Mathf.RoundToInt(HardCapSeconds / Dt);
            float elapsed = 0f;
            int tagSwaps = 0, lastActive = 0;
            float bFullAt = -1f;
            var strikeLog = new List<string>();
            float stackSum = 0f, aoeSum = 0f;
            int mountSamples = 0;
            // 기존 줄이 **여전히 도는가** — 코어 서면에 분류기가 생겨 갈래가 하나 늘었으므로
            // 추진제 줄과 보호막 줄이 굶지 않는지 본다(2026-09-18 · 설계 요청 「기존 줄 산출 불변」).
            int peakDodgeB = 0;
            float peakShieldB = 0f;
            // 포트 둘로 **얼마나 들어왔나** — 도착률을 판 내내 적분한다.
            // ⚠️ 마지막 순간의 도착률만 보면 0 으로 읽힌다(0.1초 창이라 그 틱에 없으면 0 이다) —
            //    첫 판에서 「누적형 0.00」으로 잘못 읽힌 자리다.
            double arrivedStack = 0d, arrivedAoe = 0d;

            // ── 적 간격 (2026-09-18 · 설계 요청) ───────────────────────────
            //
            // **반경이 정할 수 없는 것을 반경으로 정하고 있었다** — 「몇 마리가 들어오는가」는
            // 적이 로봇에 붙었을 때 **서로 얼마나 떨어져 서는가**가 정한다. 그 값을 가진
            // 문서가 없어 2칸이 추정으로 들어왔다(`260918_W01` 3-2). 여기서 그것을 잰다.
            //
            // 📌 **붙은 적**만 센다 — 걸어오는 중인 적까지 넣으면 「띠에서 나는 간격」이
            //    섞여 **로봇 둘레의 밀도**가 아니게 된다.
            double gapSum = 0d;
            int gapSamples = 0;
            float gapMin = float.MaxValue;

            for (int i = 0; i < steps && sim.Result == CombatResult.InProgress; i++)
            {
                bool aActive = sim.ActiveRobotIndex == 0;
                SupplySignals.ActiveOwner = aActive ? MountOwner.RobotA : MountOwner.RobotB;

                // 1) 보드 둘 다 돈다 — 대기 보드도 계속 만든다(그것이 태그의 전제다).
                BoardItemTick.Step(gridA, flowA, Dt, throttleA.Scale);
                deliveryA.Observe(flowA.PendingMountArrivals, DamageOf, Dt, MountOwner.RobotA);
                flowA.ClearPendingMountArrivals();
                deliveryA.TryDrain(DeliverySampleSeconds, out _);

                BoardItemTick.Step(gridB, flowB, Dt, throttleB.Scale);
                deliveryB.Observe(flowB.PendingMountArrivals, DamageOf, Dt, MountOwner.RobotB);
                flowB.ClearPendingMountArrivals();
                deliveryB.TryDrain(DeliverySampleSeconds, out _);
                arrivedStack += deliveryB.StackDroneRate * Dt;
                arrivedAoe += deliveryB.AoeDroneRate * Dt;

                // 2) 각 보드의 산출을 **제 로봇에** 넣는다 — 활성이 누구냐로 자리가 갈린다.
                if (aActive)
                {
                    sim.AmmoSupplyRate = aggA.ammoProduce;
                    sim.PropellantSupplyRate = aggA.propellantProduce;
                    sim.BoosterCount = aggA.boosterCount;
                    sim.ShieldMax = ShieldSystem.MaxFrom(aggA.shieldNodeCount, shieldPerNode);
                    sim.ShieldChargeRate = ShieldSystem.ChargeFrom(sim.ShieldMax, shieldRatio,
                        aggA.shieldMaterialProduce, aggA.shieldNodeCount, shieldMatPerSec);

                    sim.StandbyStackDroneArrivalRate = deliveryB.StackDroneRate;
                    sim.StandbyAoeDroneArrivalRate = deliveryB.AoeDroneRate;
                    sim.StandbyPropellantSupplyRate = aggB.propellantProduce;
                    sim.StandbyBoosterCount = aggB.boosterCount;
                    sim.StandbyShieldMax = ShieldSystem.MaxFrom(aggB.shieldNodeCount, shieldPerNode);
                    sim.StandbyShieldChargeRate = ShieldSystem.ChargeFrom(sim.StandbyShieldMax,
                        shieldRatio, aggB.shieldMaterialProduce, aggB.shieldNodeCount, shieldMatPerSec);
                }
                else
                {
                    sim.StackDroneArrivalRate = deliveryB.StackDroneRate;
                    sim.AoeDroneArrivalRate = deliveryB.AoeDroneRate;
                    sim.PropellantSupplyRate = aggB.propellantProduce;
                    sim.BoosterCount = aggB.boosterCount;
                    sim.ShieldMax = ShieldSystem.MaxFrom(aggB.shieldNodeCount, shieldPerNode);
                    sim.ShieldChargeRate = ShieldSystem.ChargeFrom(sim.ShieldMax, shieldRatio,
                        aggB.shieldMaterialProduce, aggB.shieldNodeCount, shieldMatPerSec);

                    sim.StandbyAmmoSupplyRate = aggA.ammoProduce;
                    sim.StandbyPropellantSupplyRate = aggA.propellantProduce;
                    sim.StandbyBoosterCount = aggA.boosterCount;
                    sim.StandbyShieldMax = ShieldSystem.MaxFrom(aggA.shieldNodeCount, shieldPerNode);
                    sim.StandbyShieldChargeRate = ShieldSystem.ChargeFrom(sim.StandbyShieldMax,
                        shieldRatio, aggA.shieldMaterialProduce, aggA.shieldNodeCount, shieldMatPerSec);
                }

                // 3) 자동 조종 — 게임과 같은 문(회피로 밀리는 동안은 양보).
                if (sim.Robot != null && !sim.DodgeMotionActive)
                {
                    var ctx = new AutoPilotContext
                    {
                        robotPos = sim.Robot.position, enemies = sim.Enemies,
                        arenaRadius = tuning.arenaRadiusTbd,
                        attackRange = tuning.robotAttackRangeTbd,
                        moveSpeed = tuning.robotMoveSpeedTbd,
                        holdWhenMoreThan = tuning.autoPilotHoldWhenMoreThanTbd,
                        dt = Dt,
                    };
                    sim.Robot.position = AutoPilotPolicy.NextPosition(ctx);
                }

                int strikesBefore = sim.TagSkillStrikes;
                float bStack = mountB.AmountOf(MountItem.Drone);
                float bAoe = mountB.AmountOf(MountItem.DroneAoe);


                sim.Tick(Dt);
                elapsed += Dt;

                // 4) 표본
                stackSum += bStack; aoeSum += bAoe; mountSamples++;
                // 적 간격 — 0.5초마다 한 번만 센다(매 틱은 같은 자리를 되풀이해 센다).
                if (i % 10 == 0 && sim.Robot != null)
                {
                    var near = new List<CombatEntity>();
                    foreach (CombatEntity e in sim.Enemies)
                    {
                        if (!e.IsAlive) continue;
                        float reach = e.attackRange + e.radius + sim.Robot.radius;
                        if ((e.position - sim.Robot.position).sqrMagnitude <= reach * reach)
                            near.Add(e);
                    }

                    // 붙은 적 하나마다 **가장 가까운 이웃**까지의 거리를 잰다.
                    for (int a = 0; a < near.Count; a++)
                    {
                        float best = float.MaxValue;
                        for (int b = 0; b < near.Count; b++)
                        {
                            if (a == b) continue;
                            best = Mathf.Min(best, Vector2.Distance(near[a].position, near[b].position));
                        }
                        if (best == float.MaxValue) continue;   // 혼자 붙어 있으면 간격이 없다
                        gapSum += best;
                        gapSamples++;
                        gapMin = Mathf.Min(gapMin, best);
                    }
                }

                if (!aActive)   // B 가 나가 있는 동안의 값이라야 B 보드의 줄을 잰다
                {
                    peakDodgeB = Mathf.Max(peakDodgeB, sim.Dodge.Stacks);
                    peakShieldB = Mathf.Max(peakShieldB, sim.Shield != null ? sim.Shield.Value : 0f);
                }
                if (sim.ActiveRobotIndex != lastActive) { tagSwaps++; lastActive = sim.ActiveRobotIndex; }
                if (sim.TagSkillStrikes > strikesBefore)
                {
                    // ⚠️ **만충 시각은 이 자리에서 잰다.** 대기 보드가 채우는 것도, 만충을 보고
                    //    교대가 나는 것도 **틱 안**이라, 틱 경계에서 `IsFull` 을 보면 만충이던
                    //    순간이 통째로 안 보인다 — 태그 스킬은 만충일 때만 나가므로 그 자리가 곧 만충이다.
                    if (bFullAt < 0f) bFullAt = elapsed;
                    float total = sim.LastTagSkillDamage;
                    int n = Mathf.Max(1, sim.LastTagSkillTargetCount);
                    strikeLog.Add($"    {elapsed:F1}초 · 표적 {n} · 총 피해 {total:F0}"
                                  + $" · 한 체 몫 {total / n:F0}"
                                  + $" · 소진 직전 적재(누적 {bStack:F0} · 광역 {bAoe:F0})");
                }
            }

            // ── 보고 ───────────────────────────────────────────────────────
            LastVerdict = new StageVerdict
            {
                stageId = stageId, how = "두 로봇",
                ok = sim.Result == CombatResult.Win, result = sim.Result, seconds = elapsed,
                remaining = sim.Remaining, total = sim.TotalEnemies,
                hp = sim.Robot.hp, maxHp = sim.Robot.maxHp,
            };

            sb.AppendLine();
            sb.AppendLine("[결과]");
            sb.AppendLine($"  {sim.Result} · {elapsed:F1}초 · 남은 적 {sim.Remaining}/{sim.TotalEnemies}"
                          + $" · 로봇 HP {sim.Robot.hp:F0}/{sim.Robot.maxHp:F0}");
            sb.AppendLine($"  교대 {tagSwaps} 회 · 태그 스킬 {sim.TagSkillStrikes} 회");

            sb.AppendLine();
            sb.AppendLine("[목적 1 — 피해 나눔의 대가]  ← 측정법: 태그 스킬이 터질 때마다 표적 수와 총 피해를 읽는다");
            if (strikeLog.Count == 0)
                sb.AppendLine("    ⚠️ **한 번도 안 터졌다** — 만충이 안 섰거나 표적이 없었다. 아래 목적 2 를 먼저 본다.");
            else foreach (string line in strikeLog) sb.AppendLine(line);

            sb.AppendLine();
            sb.AppendLine("[목적 2 — 드론 스택 40]  ← 측정법: B 마운트가 처음 만충이 된 시각");
            sb.AppendLine(bFullAt >= 0f
                ? $"    **{bFullAt:F1}초**에 만충(적재량 {MountLoad.SlotsRobotB * stack * droneFactor:F0})"
                  + " ← 태그 스킬이 나간 자리로 잰다(만충일 때만 나간다)"
                : $"    ⚠️ **한 판 안에 만충이 안 섰다**(적재량 {MountLoad.SlotsRobotB * stack * droneFactor:F0})");

            sb.AppendLine();
            sb.AppendLine("[목적 3 — 두 드론 혼재]  ← 측정법: 매 틱 B 마운트의 두 종 적재를 평균 낸다");
            float avgStack = mountSamples > 0 ? stackSum / mountSamples : 0f;
            float avgAoe = mountSamples > 0 ? aoeSum / mountSamples : 0f;
            float both = avgStack + avgAoe;
            sb.AppendLine($"    평균 적재 — 누적형 {avgStack:F1} · 광역형 {avgAoe:F1}"
                          + (both > 0f ? $" · 광역 비율 {avgAoe / both:F2}" : " · 비율 없음(둘 다 0)"));
            sb.AppendLine($"    보드 산출 몫 — 광역 {aggB.AoeShare:F2} (비율이 이 수와 갈리면 마운트가 유입대로 안 찬 것이다)");

            sb.AppendLine();
            sb.AppendLine("[B 포트 둘 — 도착이 있는가]  ← 측정법: 종별 도착률을 판 내내 적분한 것. 종마다 다른 포트로 들어온다");
            sb.AppendLine($"    누적형 **{arrivedStack:F0} 기** (포트 (9,10) 서면) · "
                          + $"광역형 **{arrivedAoe:F0} 기** (포트 (2,10) 동면)");
            sb.AppendLine("    ⚠️ 한쪽이 0 이면 그 포트로 가는 줄이 끊긴 것이다.");

            sb.AppendLine();
            sb.AppendLine("[기존 줄 산출 불변]  ← 측정법: B 가 나가 있는 동안의 회피 스택 최고 · 보호막 최고");
            sb.AppendLine($"    회피 스택 최고 {peakDodgeB} (추진제 줄) · 보호막 최고 {peakShieldB:F0} (보호막 줄)");
            sb.AppendLine("    ⚠️ 둘 다 0 이면 코어 서면 분류기가 기존 줄을 굶긴 것이다.");

            sb.AppendLine();
            sb.AppendLine("[목적 4 — 광역형 한 기의 평균 표적 수]  ← 측정법: 광역 타격마다 (주 표적 1 + 곁에 닿은 수)");
            sb.AppendLine(sim.AoeHitEvents > 0
                ? $"    타격 {sim.AoeHitEvents} 회 · 닿은 표적 합 {sim.AoeHitTargetsTotal}"
                  + $" · **평균 {(float)sim.AoeHitTargetsTotal / sim.AoeHitEvents:F2} 마리**"
                  + " (설계 본전 4마리와 견준다)"
                : "    ⚠️ 광역형이 한 번도 안 때렸다 — 보드가 광역형을 안 만들었거나 사출이 없었다");

            sb.AppendLine();
            sb.AppendLine("[적 간격 — 반경이 무엇을 정하는가]  ← 측정법: 붙은 적마다 가장 가까운 이웃까지의 거리(0.5초마다)");
            sb.AppendLine(gapSamples > 0
                ? $"    표본 {gapSamples} · **평균 {gapSum / gapSamples:F2} 유닛** · 최소 {gapMin:F2} 유닛"
                : "    ⚠️ 붙은 적이 둘 이상인 적이 없었다 — 간격을 못 쟀다");

            sb.AppendLine();
            sb.AppendLine("[반경 스위프 — 반경 r 이면 몇 마리가 들어오나]  ← 측정법: 광역 타격 순간 r 안의 살아 있는 적 수");
            if (sim.AoeHitEvents > 0)
                for (int r = 0; r < CombatSimulation.AoeSweepRadii.Length; r++)
                    sb.AppendLine($"    r = {CombatSimulation.AoeSweepRadii[r]:F0} 칸 → 평균 "
                                  + $"**{(float)sim.AoeSweepTargets[r] / sim.AoeHitEvents:F2} 마리**");
            else sb.AppendLine("    ⚠️ 광역 타격이 없어 못 쟀다");
            sb.AppendLine("    ⚠️ **값은 안 옮겼다** — 판정은 자산의 반경 하나를 그대로 쓴다. 여기 수는 세기만 한 것이다.");

            sb.AppendLine();
            sb.AppendLine("############ 끝 ############");
            return sb.ToString();
        }

        /// <summary>
        /// B 판의 **「면이 다름」 경고 자리**를 찍는다 (2026-09-18 사용자 리허설 ② 진단).
        ///
        /// 📌 화면에서 본 경고가 **판이 진짜 안 이어진 것**인지 **경고가 거짓말**인지를
        ///    가르는 자리다 — 둘은 화면에서 같은 글자로 보인다.
        ///
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunBoardBProbeBatch</c>
        /// </summary>
        [MenuItem("MBI/Probe B 판 면 경고")]
        public static void RunBoardBProbeMenu() => Debug.Log(RunBoardBProbe());

        public static void RunBoardBProbeBatch()
        {
            Debug.Log(RunBoardBProbe());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string RunBoardBProbe()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ B 판 면 경고 진단 (2026-09-18) ############");

            var robotA = AssetDatabase.LoadAssetAtPath<RobotDefinition>($"{SoRoot}/Robots/Robot_A.asset");
            float need = robotA != null && robotA.balanceRef != null ? robotA.balanceRef.propellantNeed : 30f;
            BoardGrid g = BoardB(need);

            List<Vector2Int> bad = BeltRouting.FaceMismatchCells(g);
            sb.AppendLine($"[면이 다름] {bad.Count} 칸");
            foreach (Vector2Int c in bad)
            {
                BeltInstance b = g.GetBeltAt(c);
                sb.AppendLine($"    {c} · {(b != null ? b.Element.ToString() : "?")}"
                              + $" · In {(b != null && b.InFaces != null ? b.InFaces.Length : 0)} 면"
                              + $" · Out {(b != null && b.OutFaces != null ? b.OutFaces.Length : 0)} 면");
            }

            ICollection<Vector2Int> conn = LogisticsReach.ConnectedNodes(g);
            sb.AppendLine($"[이어진 노드] {conn.Count}");
            foreach (StartingBoardB.Slot slot in StartingBoardB.Nodes)
            {
                NodeInstance n = g.GetAt(slot.cell);
                sb.AppendLine($"    {slot.cell} {slot.nodeId} 회전 {(n != null ? n.Rotation : -1)}"
                              + $" · 이어짐 {(conn.Contains(slot.cell) ? "예" : "**아니오**")}");
            }
            sb.AppendLine("############ 끝 ############");
            return sb.ToString();
        }

        private static List<EnemyDefinition> EnemyCatalog()
        {
            var catalog = new List<EnemyDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyDefinition"))
                catalog.Add(AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid)));
            return catalog;
        }
    }
}
