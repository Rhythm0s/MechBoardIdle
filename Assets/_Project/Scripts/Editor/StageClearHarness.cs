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
        private static float Dt = 0.05f;   // #205 ⑤ — 틱 폭 탐침 손잡이(하네스 전용 · 기본 0.05 그대로 · 🗑️ 구 const)

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

            /// <summary>
            /// 아직 안 나온 적 (2026-10-16 · #151 하네스 정정). ⚠️ <see cref="remaining"/> 는 **살아 있는 적**뿐이라
            /// 종전 표의 「처치 = 전체 − remaining」은 **안 나온 적까지 처치로 셌다** — 처치는 전체 − 살아 있음 − 안 나옴이다.
            /// </summary>
            public int unspawned;

            /// <summary>마지막 적의 **예정** 스폰 시각(WaveSpawnRule · 묶음 간격). 제한 시간보다 크면 스폰이 막는 판이다.</summary>
            public float plannedLastSpawn;

            /// <summary>실제로 다 나온 시각. 다 안 나왔으면 -1.</summary>
            public float allSpawnedAt;

            /// <summary>한 순간에 깨어 있던(로봇을 쫓던) 적의 최대 수 — 한 번에 끌려오는 수(사용자 확정 #152 확인 열).</summary>
            public int peakAggro;
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

        /// <summary>
        /// 【S6 보스 판】 **로봇이 실제로 밀려나는가** (2026-10-01 · ⑫ 확인).
        ///
        /// ⚠️ **단위 시험만으로는 못 본다** — `BossPushesRobotTests` 는 규칙이 맞는지 보고,
        /// 이것은 **판에서 실제로 걸리는지**를 본다. 보스가 로봇에 닿지도 않으면 규칙이
        /// 맞아도 아무 일이 안 난다.
        ///
        /// 📌 **세기 0 판과 나란히 돈다** — 밀린 거리에는 **제 걸음도 섞이므로**
        ///    한 판만 봐서는 밀기의 몫을 못 가른다. 끄고 켠 둘을 견주는 것이 측정법이다.
        ///
        /// ⚠️ **값을 안 고친다** — 세기만 잠시 0 으로 두고 **원래 값으로 되돌린다.**
        ///
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunBossPushBatch</c>
        /// </summary>
        [MenuItem("MBI/Harness S6 보스 밀기 (1001 ⑫)")]
        public static void RunBossPushMenu() => Debug.Log(RunBossPush());

        /// <summary>
        /// ⚠️ **끝났으면 나가야 한다**(2026-10-01) — 이 줄이 빠져 있어서 일을 다 끝낸
        /// 배치 프로세스가 **26 분간 프로젝트 잠금을 쥐고 안 나갔다.** 뒤따르던 시험 실행이
        /// 컴파일도 못 하고 반환 1 로 죽었는데, 로그에는 오류가 없어 **컴파일 실패로 오진**했다.
        ///
        /// 📌 다른 열두 배치 함수는 다 이 줄을 들고 있었다 — 빠진 것은 이 하나였다.
        /// ⚠️ <c>-runTests</c> 에 <c>-quit</c> 를 붙이는 것과 다른 일이다(그쪽은 금지).
        ///    여기는 **내 코드가 끝낸 뒤** 스스로 나가는 자리다.
        /// </summary>
        public static void RunBossPushBatch()
        {
            Debug.Log(RunBossPush());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **S6 을 구 추격 규칙(`hold = 1`)으로 한 판** — 겹침이 밀기 때문이 아니라
        /// **로봇이 보스 쪽으로 걸어 들어간 것**이었는지 가른다
        /// (2026-10-01 사용자 지시 · 계획 세션 가설).
        ///
        /// ⚠️ **게임 코드는 0 줄이다** — 자산 값 `autoPilotHoldWhenMoreThanTbd` 만
        /// 재는 동안 1 로 두고 **되돌린다.** 09-30 에 사용자가 0 으로 확정한 값이므로
        /// 되돌리기가 빠지면 확정이 조용히 뒤집힌다.
        ///
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunHoldProbeBatch</c>
        /// </summary>
        [MenuItem("MBI/Harness S6 구 추격 hold=1 (1001)")]
        public static void RunHoldProbeMenu() => Debug.Log(RunHoldProbe());

        public static void RunHoldProbeBatch()
        {
            Debug.Log(RunHoldProbe());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string RunHoldProbe()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>($"{SoRoot}/CombatTuning.asset");
            if (tuning == null) return "자산을 못 읽었다 — CombatTuning.asset";
            int keep = tuning.autoPilotHoldWhenMoreThanTbd;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("############ S6 구 추격 규칙 비교 (2026-10-01 · ⑫ 가설 가르기) ############");
            sb.AppendLine();
            sb.AppendLine($"⚠️ **`autoPilotHoldWhenMoreThanTbd` 자산 값은 {keep} 다** — 재는 동안만 바꾸고 되돌린다.");
            sb.AppendLine("📌 묻는 것: 09-30 에 본 「계속 겹침」이 밀기 탓이 아니라");
            sb.AppendLine("   **로봇이 보스 쪽으로 걸어 들어간 것**이었는가. 겹친 틱과 최소 거리를 본다.");
            sb.AppendLine();

            sb.AppendLine($"======== 지금 규칙 (hold = {keep}) ========");
            sb.AppendLine(Run("S6"));

            tuning.autoPilotHoldWhenMoreThanTbd = 1;
            sb.AppendLine();
            sb.AppendLine("======== 구 규칙 (hold = 1) ========");
            sb.AppendLine(Run("S6"));
            tuning.autoPilotHoldWhenMoreThanTbd = keep;   // ⚠️ 반드시 되돌린다
            sb.AppendLine();
            sb.AppendLine($"✅ `autoPilotHoldWhenMoreThanTbd` 를 {keep} 로 되돌렸다.");

            sb.AppendLine("############ 끝 ############");
            return sb.ToString();
        }

        public static string RunBossPush()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>($"{SoRoot}/CombatTuning.asset");
            float keep = tuning != null ? tuning.enemyPushStrengthTbd : 0f;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("############ S6 보스 밀기 (2026-10-01 · ⑫) ############");
            sb.AppendLine();
            sb.AppendLine($"⚠️ **세기 {keep} 는 자산 값 그대로다** — 끈 판은 재는 동안만 0 이고 되돌린다.");
            sb.AppendLine();

            sb.AppendLine("======== 켠 판 (세기 " + keep.ToString("F0") + ") ========");
            sb.AppendLine(Run("S6"));

            if (tuning != null)
            {
                tuning.enemyPushStrengthTbd = 0f;
                sb.AppendLine();
                sb.AppendLine("======== 끈 판 (세기 0 · 구 거동) ========");
                sb.AppendLine(Run("S6"));
                tuning.enemyPushStrengthTbd = keep;   // ⚠️ 반드시 되돌린다
                sb.AppendLine();
                sb.AppendLine($"✅ 세기를 {keep} 로 되돌렸다.");
            }

            sb.AppendLine("############ 끝 ############");
            return sb.ToString();
        }

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
            var robot = Knob(AssetDatabase.LoadAssetAtPath<RobotDefinition>($"{SoRoot}/Robots/Robot_A.asset"));
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

            // 마운트계수 — 러너 `Begin` 과 같은 규칙이다.
            // 🗑️ 구 「스테이지의 축(powerModel)이 정한다」 폐기(2026-10-16 #150) — 강화 표식 하나가 정한다.
            float mountCoef = MountEnhanceRule.MountCoef(Enhanced, robot.mountCoef, robot.enhancedMountCoef);

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
            if (SpawnHook != null) spawns = SpawnHook(spawns);   // 2026-10-16 문턱 훑기(하네스 전용)
            List<StageComposition> farmRoster = null;            // 2026-10-19 파밍 생존 훑기(하네스 전용)
            if (FarmMode) { farmRoster = FarmRoster(stage); spawns = new List<EnemySpawn>(); _farmSpawned = 0; _armorSpawned = 0; LastArmorFirstKill = -1f; }

            var sim = new CombatSimulation(setup, spawns,
                tuning.arenaRadiusTbd, stage.challengeTime, tuning.spawnCadenceTbd);

            // ⚠️ **띠를 러너와 같게 세운다.** 안 세우면 적이 기본 거리에 한 줄로 서고,
            //    그러면 「가까운 것은 제자리에서 쏘고 먼 것에는 걸어간다」가 안 일어난다 —
            //    게임과 다른 판을 재게 된다(2026-09-15 사용자 확정 · §72-40).
            // 게임과 같은 값으로 곁눈질을 붙든다 — 안 넣으면 떨림 잣대가 다른 판을 잰다.
            sim.SetSideStepHold(tuning.enemySideStepHoldTbd);
            // ⚠️ **러너와 같은 자산에서 읽는다** — 안 읽으면 하네스가 종전 거동(예산 0)으로
            //    재서 화면과 다른 판을 본다(오늘 아침 `RespawnUnreachable` 과 같은 자리).
            sim.SetSideDetourCells(tuning.enemySideDetourCellsTbd);
            sim.SetSideDetourRecoverSeconds(tuning.enemySideDetourRecoverSecondsTbd);
            sim.SetEnemyPushStrength(tuning.enemyPushStrengthTbd);
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
            sim.SpawnAllAtOnce = true;                         // 사용자 확정 #152 — 러너와 같은 문(도전 층 전원 스폰)
            sim.SetAggroRange(tuning.enemyAggroRangeTbd);      // 사용자 확정 #152 — 탐색 범위
            if (AggroOverride > 0f) sim.SetAggroRange(AggroOverride);   // 훑기 전용(하네스 · 자산 0)

            if (tuning.spawnRingMinTbd > 0f && tuning.spawnRingMaxTbd > 0f)
                sim.SetSpawnBand(tuning.spawnRingMinTbd, tuning.spawnRingMaxTbd);
            else if (tuning.spawnRingRadiusTbd > 0f)
                sim.SetSpawnRing(tuning.spawnRingRadiusTbd);
            else
                sb.AppendLine("  ⚠️ 스폰 띠·링이 튜닝에 없다 — 시뮬 기본값으로 돈다(게임과 다를 수 있다)");

            // 2026-10-19 파밍 생존 훑기 — 러너 `OpenFarmLayer` 와 같은 문(무한 · 줄 선 적 멈춤 · 즉시 접근 · 첫 바퀴 바로 채움).
            if (FarmMode)
            {
                sim.Endless = true;
                sim.SuspendQueuedSpawns = true;
                sim.SpawnAllAtOnce = false;
                sim.SetAggroRange(FarmAggro ? tuning.enemyAggroRangeTbd : 0f);
                FarmRefill(sim, farmRoster, catalog, tuning);
            }

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
            // 되돌린 횟수 — 0 이면 **문턱에 아무도 안 닿았다**는 뜻이지 규칙이 꺼진 게 아니다.
            int respawned = 0;
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
            float allSpawnedAt = -1f;   // #151 — 다 나온 시각
            int peakAggro = 0;          // #152 — 한 번에 깨어 있던 최대 수
            CombatResult result = CombatResult.InProgress;

            // ⚠️ 보스 밀기 계측(2026-10-01) — 보스가 없는 판에서는 전부 0 으로 남는다.
            float bossGapMin = float.MaxValue;
            int bossOverlapTicks = 0;
            float bossPushTotal = 0f;
            bool bossSeen = false;

            float farmClock = 0f, farmMinHp = float.MaxValue;
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
                // ⚠️ **보스–로봇 거리를 틱 앞뒤로 잰다**(2026-10-01 ⑫ 확인 · 하네스 열만).
                //    밀기는 `Tick` 안에서 일어나므로 **앞뒤를 둘 다 봐야** 밀린 양이 나온다.
                Vector2 beforePush = sim.Robot != null ? sim.Robot.position : Vector2.zero;

                sim.Tick(Dt);

                CombatEntity bossNow = null;
                for (int bi = 0; bi < sim.Enemies.Count; bi++)
                    if (sim.Enemies[bi] != null && sim.Enemies[bi].isBoss && sim.Enemies[bi].IsAlive)
                    { bossNow = sim.Enemies[bi]; break; }
                if (bossNow != null && sim.Robot != null)
                {
                    float d = Vector2.Distance(bossNow.position, sim.Robot.position);
                    if (d < bossGapMin) bossGapMin = d;
                    float want = bossNow.radius + sim.Robot.radius;
                    if (d < want)
                    {
                        bossOverlapTicks++;
                        bossPushTotal += Vector2.Distance(beforePush, sim.Robot.position);
                    }
                    bossSeen = true;
                }

                // ⚠️⚠️ **게임이 지나는 문을 그대로 지난다**(2026-09-19 · 리허설 1 ② 실측 중 발견).
                //
                // 러너는 매 틱 `Tick` **다음에** 이것을 부르는데(§71-28 2), **하네스는 안 불렀다.**
                // 그래서 하네스 판에는 **게임에는 없는 「영영 못 닿는 적」이 남아 있었고**,
                // 「굳음 N」 수치가 그만큼 부풀어 있었다. 09-15 §7 등재 항목의 재발이다 —
                // **「재는 것과 보는 것은 다른 일 — 프로브는 사람이 지나는 문을 안 지난다」.**
                //
                // ⚠️ **앞선 측정과 견줄 때 주의한다** — 오늘 이전의 굳음·클리어 수치는
                //    이 문을 안 지난 판의 것이다(개체 수는 안 변하므로 클리어 여부 자체는
                //    같지만, 적이 서 있던 **자리**가 다르다).
                respawned += sim.RespawnUnreachable();

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
                    // ⚠️⚠️ **「굳음」 하나로는 까닭을 못 가른다**(2026-09-19 · 사용자 리허설 1 ②
                    //    「멀리 정지한 채 안 오는 몬스터」). 종전 표본은 수만 셌는데,
                    //    **서 있는 것이 규칙인 적**(포격은 사거리 7 에서 서서 쏜다)과
                    //    **막혀서 못 오는 적**이 같은 칸에 들어갔다. 둘은 다른 일이다 —
                    //    앞은 사양이고 뒤는 결함 후보다.
                    //
                    // 그래서 **종과 거리를 같이** 찍는다. 값을 짓지 않고 있는 것을 적는다.
                    int alive = 0, stuck = 0, inReach = 0;
                    var byKind = new Dictionary<string, int[]>();   // [살아있음, 사거리안, 굳음]
                    float farthestStuck = 0f;
                    string farthestStuckWho = null;

                    // ⚠️⚠️ **굳음을 갈래로 나눈다**(2026-09-19 · 리허설 1 ②).
                    //
                    // 「안 움직인다」에는 서로 다른 일이 섞여 있다 —
                    //   · **길막** : 주 축도 곁눈질도 막혔다(`StepOrSide` 가 null 을 낸다).
                    //                **규칙이다**(밀어내지 않음 · 막히면 멈춤 · 사용자 확정).
                    //   · **안 막혔는데 정지** : 갈 수 있는데 안 갔다. **결함 후보다.**
                    //
                    // 📌 `IsBlocked` 는 **순수 함수**라 여기서 불러도 시뮬이 안 흔들린다
                    //    (`StepOrSide` 는 붙들기 상태를 고쳐 쓰므로 프로브가 부르면 안 된다).
                    int stuckBlocked = 0, stuckFree = 0;

                    foreach (CombatEntity e in sim.Enemies)
                    {
                        if (e == null || !e.IsAlive) continue;
                        alive++;

                        string kind = string.IsNullOrEmpty(e.label) ? "?" : e.label;
                        if (!byKind.TryGetValue(kind, out int[] row)) byKind[kind] = row = new int[3];
                        row[0]++;

                        float d = (e.position - sim.RobotPosition).magnitude;
                        float reach = e.attackRange + 0.5f + e.radius;   // 로봇 반경은 setup 과 같다
                        if (d <= reach) { inReach++; row[1]++; lastPos[e] = e.position; continue; }

                        if (e.sideStepDir != Vector2.zero)
                        {
                            if (lastSide.TryGetValue(e, out Vector2 prevSide)
                                && prevSide != Vector2.zero && prevSide != e.sideStepDir) sideFlips++;
                            lastSide[e] = e.sideStepDir;
                        }

                        if (lastPos.TryGetValue(e, out Vector2 was)
                            && (e.position - was).magnitude < 0.05f)
                        {
                            stuck++;
                            row[2]++;
                            // **가장 먼 굳음**이 어디인지가 갈래를 가른다 — 스폰 띠 바깥이면
                            // 디스폰 되돌림 자리이고, 띠 안쪽이면 길막이다.
                            if (d > farthestStuck) { farthestStuck = d; farthestStuckWho = kind; }

                            // 길막인가 — `StepOrSide` 가 보는 것과 **같은 두 칸**을 본다.
                            Vector2 main = GridMovement.Step(e.position, sim.RobotPosition, e.moveSpeed * Dt);
                            bool blocked = main == e.position
                                || GridMovement.IsBlocked(main, e.radius, e, sim.Enemies, null);
                            if (blocked)
                            {
                                Vector2 delta = sim.RobotPosition - e.position;
                                bool horiz = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
                                float other = horiz ? delta.y : delta.x;
                                if (Mathf.Abs(other) <= 1e-6f) { stuckBlocked++; }
                                else
                                {
                                    Vector2 dir = horiz
                                        ? new Vector2(0f, Mathf.Sign(other))
                                        : new Vector2(Mathf.Sign(other), 0f);
                                    Vector2 side = e.position
                                        + dir * Mathf.Min(e.moveSpeed * Dt, Mathf.Abs(other));
                                    if (GridMovement.IsBlocked(side, e.radius, e, sim.Enemies, null))
                                        stuckBlocked++;
                                    else stuckFree++;   // 곁눈질이 뚫려 있는데 안 갔다
                                }
                            }
                            else stuckFree++;
                        }
                        lastPos[e] = e.position;
                    }

                    var kinds = new StringBuilder();
                    foreach (KeyValuePair<string, int[]> kv in byKind)
                        kinds.Append($" · {kv.Key} {kv.Value[0]}(안 {kv.Value[1]}·굳음 {kv.Value[2]})");

                    stuckLog.Add($"    {elapsed:F0}초 · 살아있음 {alive} · 사거리 안 {inReach}"
                                 + $" · **굳음 {stuck}**{kinds}"
                                 + (farthestStuckWho != null
                                     ? $" · 가장 먼 굳음 {farthestStuckWho} {farthestStuck:F1}칸"
                                     : string.Empty)
                                 + (stuck > 0
                                     ? $" · [길막 {stuckBlocked} · **안 막혔는데 정지 {stuckFree}**]"
                                     : string.Empty));
                    nextStuckAt += 10f;
                }

                if (FarmMode)
                {
                    farmClock += Dt;
                    if (farmClock >= stage.spawnInterval) { farmClock = 0f; FarmRefill(sim, farmRoster, catalog, tuning); }
                    if (sim.Robot != null && sim.Robot.hp < farmMinHp) farmMinHp = sim.Robot.hp;
                    if (LastArmorFirstKill < 0f && _armorSpawned > 0)
                    {
                        int armorAlive = 0;
                        foreach (CombatEntity e in sim.Enemies) if (e.IsAlive && e.label == "장갑") armorAlive++;
                        if (armorAlive < _armorSpawned) LastArmorFirstKill = elapsed;
                    }
                    if (elapsed >= FarmSeconds) { result = sim.Result; break; }
                }
                if (allSpawnedAt < 0f && sim.Unspawned == 0) allSpawnedAt = elapsed;   // #151 열
                { int awake = 0; foreach (CombatEntity e in sim.Enemies) if (e.IsAlive && e.aggro) awake++; if (awake > peakAggro) peakAggro = awake; }   // #152 열
                result = sim.Result;
                if (result != CombatResult.InProgress) break;
            }

            // ── 보고 ──────────────────────────────────────────────────────
            if (FarmMode)
            {
                LastFarmMinHp = farmMinHp == float.MaxValue ? sim.Robot.hp : Mathf.Max(0f, farmMinHp);
                int aliveNow = 0; foreach (CombatEntity e in sim.Enemies) if (e.IsAlive) aliveNow++;
                LastFarmKills = _farmSpawned - aliveNow;
                int armorAliveEnd = 0; foreach (CombatEntity e in sim.Enemies) if (e.IsAlive && e.label == "장갑") armorAliveEnd++;
                LastArmorKills = _armorSpawned - armorAliveEnd;
            }
            LastVerdict = new StageVerdict
            {
                stageId = stageId, how = "한 로봇",
                ok = result == CombatResult.Win, result = result, seconds = elapsed,
                remaining = sim.Remaining, total = sim.TotalEnemies,
                hp = sim.Robot.hp, maxHp = sim.Robot.maxHp,
                unspawned = sim.Unspawned,
                plannedLastSpawn = WaveSpawnRule.SpawnTime(sim.TotalEnemies - 1, sim.WaveSize, sim.WaveInterval),
                allSpawnedAt = allSpawnedAt,
                peakAggro = peakAggro,
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

            // ── 보스 밀기 (2026-10-01 ⑫ 확인) ──────────────────────────────
            //
            // ⚠️ **보스가 없는 판에서는 안 적는다** — 0 을 적으면 「밀기가 안 걸렸다」로
            //    읽히는데, 사실은 **밀 보스가 없는 판**이다. 둘은 다른 말이다.
            if (bossSeen)
            {
                sb.AppendLine();
                sb.AppendLine("[보스 밀기 — 로봇이 실제로 밀려나는가]"
                              + "  ← 측정법: 틱마다 보스–로봇 중심 거리와 로봇이 움직인 양");
                sb.AppendLine($"    보스–로봇 **최소 거리 {bossGapMin:F3} 유닛**"
                              + "  ← 겹침 판정선(반지름 합)보다 작게 내려갔는가를 본다");
                sb.AppendLine($"    **겹친 틱 {bossOverlapTicks}**"
                              + "  ← 겹친 동안에만 민다. 0 이면 애초에 안 붙었다는 뜻이다");
                sb.AppendLine($"    **밀린 총 거리 {bossPushTotal:F2} 유닛**"
                              + "  ← 겹친 틱에 로봇이 움직인 양의 합(제 걸음도 섞인다)");
                sb.AppendLine("    ⚠️ 세기 0 판과 **나란히 놓고** 봐야 밀기의 몫이 갈린다 —"
                              + " 이 수만으로는 제 걸음과 밀린 몫을 못 가른다.");
            }

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
            sb.AppendLine($"    되돌림(못 닿는 적 → 링) **{respawned}회**"
                          + $" · 문턱 = 이동 속도 × {OffscreenRespawnRule.UnreachableSeconds:F0}초");
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
            BoardHook?.Invoke(g);   // 2026-10-16 · S2 모듈 갈래(하네스 전용 · 게임 코드 0)

            BeltAutoOrient.Resolve(g);
            BeltFlow.Resolve(g);
            return g;
        }

        /// <summary>적 묶음 갈고리 — 하네스 전용(2026-10-16 문턱 훑기). 자산은 안 건드린다.</summary>
        internal static System.Func<List<EnemySpawn>, List<EnemySpawn>> SpawnHook;

        /// <summary>두 로봇 판(RunTag) 로봇 HP 덮기 — 하네스 전용. 0 이면 자산 값.</summary>
        internal static float HpAll;

        /// <summary>적을 고르게 솎는다 — 차례는 지키고 k 비율만 남긴다(앞에서 자르면 쉬운 적만 남는다).</summary>
        private static List<EnemySpawn> Thin(List<EnemySpawn> src, float k)
        {
            var o = new List<EnemySpawn>();
            for (int i = 0; i < src.Count; i++)
                if (Mathf.FloorToInt((i + 1) * k) > Mathf.FloorToInt(i * k)) o.Add(src[i]);
            return o;
        }

        private static List<EnemySpawn> ScaleAtk(List<EnemySpawn> src, float f)
        {
            var o = new List<EnemySpawn>(src.Count);
            foreach (EnemySpawn e in src) { EnemySpawn c = e; c.atk *= f; o.Add(c); }
            return o;
        }

        private static string Cell(StageVerdict v) =>
            $"{(v.result == CombatResult.Win ? "**Win**" : v.result.ToString())} · 처치 {v.total - v.remaining - v.unspawned}/{v.total}"
            + (v.unspawned > 0 ? $" (미등장 {v.unspawned})" : "")
            + $" · {v.seconds:F1}초 · HP {v.hp:F0}/{v.maxHp:F0}";
        // 🗑️ 구 「처치 = total − remaining」 폐기(2026-10-16 · #151) — remaining 은 살아 있는 적뿐이라 안 나온 적을 처치로 셌다.
        //    10-16 문턱·피해량 훑기 표의 「처치」는 그 셈이다(미등장이 있던 칸은 처치가 부풀어 있다).

        private static StageVerdict RunOne(string id, float hp)
        {
            bool two = id == "S5" || id == "S6";
            HpAll = hp;
            if (two) RunTag(id); else Run(id, preloadMount: true, propellantNeed: BasePropellantNeed, hpOverride: hp);
            HpAll = 0f;
            return LastVerdict;
        }

        /// <summary>
        /// **문턱 훑기**(2026-10-16 · 사용자 결정 「지금 값 손봐 새 빌드」 앞 · 자산·값 0).
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunThresholdSweepBatch</c>
        /// </summary>
        public static void RunThresholdSweepBatch()
        {
            var sb = new StringBuilder();
            string[] ids = { "S1", "S2", "S3", "S4", "S5", "S6" };
            sb.AppendLine("############ 문턱 훑기 (2026-10-16 · 9d3a11e 자산 · 하네스 안에서만 바꿈) ############");
            sb.AppendLine("판 = 시작 보드 + 튜토리얼 칸 · 마운트 가득 · 자동 조종 · S5·S6 = 두 로봇(태그) 판");
            try
            {
                sb.AppendLine();
                sb.AppendLine("## ① 로봇 HP");
                sb.AppendLine("| HP | " + string.Join(" | ", ids) + " |");
                sb.AppendLine("|---|" + string.Concat(System.Array.ConvertAll(ids, _ => "---|")));
                foreach (float hp in new[] { 1000f, 1500f, 2000f, 3000f, 5000f })
                {
                    var cells = new List<string>();
                    foreach (string id in ids) cells.Add(Cell(RunOne(id, hp)));
                    sb.AppendLine($"| {hp:F0} | " + string.Join(" | ", cells) + " |");
                }

                sb.AppendLine();
                sb.AppendLine("## ② 몬스터 수 (HP 1000) — 차례를 지켜 고르게 솎음");
                sb.AppendLine("| 비율 | " + string.Join(" | ", ids) + " |");
                sb.AppendLine("|---|" + string.Concat(System.Array.ConvertAll(ids, _ => "---|")));
                foreach (float k in new[] { 1f, 0.8f, 0.6f, 0.45f })
                {
                    SpawnHook = l => Thin(l, k);
                    var cells = new List<string>();
                    foreach (string id in ids) cells.Add(Cell(RunOne(id, 0f)));
                    SpawnHook = null;
                    sb.AppendLine($"| ×{k:F2} | " + string.Join(" | ", cells) + " |");
                }

                sb.AppendLine();
                sb.AppendLine("## ③ 적 공격력 (수·HP 고정)");
                sb.AppendLine("| 배 | " + string.Join(" | ", ids) + " |");
                sb.AppendLine("|---|" + string.Concat(System.Array.ConvertAll(ids, _ => "---|")));
                foreach (float f in new[] { 1f, 0.75f, 0.5f })
                {
                    SpawnHook = l => ScaleAtk(l, f);
                    var cells = new List<string>();
                    foreach (string id in ids) cells.Add(Cell(RunOne(id, 0f)));
                    SpawnHook = null;
                    sb.AppendLine($"| ×{f:F2} | " + string.Join(" | ", cells) + " |");
                }

                sb.AppendLine();
                sb.AppendLine("## ④ 모듈 구멍 메움 어림 — 판 배수(PlantScale)로 대신 잼 · ⚠️ 어림(판 전체를 곱해 실제 모듈 하나보다 크다)");
                sb.AppendLine("| 판 배수 | " + string.Join(" | ", ids) + " |");
                sb.AppendLine("|---|" + string.Concat(System.Array.ConvertAll(ids, _ => "---|")));
                foreach (float k in new[] { 1.5f, 2.25f })
                {
                    PlantScale = k;
                    var cells = new List<string>();
                    foreach (string id in ids) cells.Add(Cell(RunOne(id, 0f)));
                    PlantScale = 1f;
                    sb.AppendLine($"| ×{k:F2} (어림) | " + string.Join(" | ", cells) + " |");
                }
            }
            finally { SpawnHook = null; HpAll = 0f; PlantScale = 1f; }

            System.IO.File.WriteAllText("Docs/261016_threshold_sweep.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // ── 파밍 층 생존 훑기(2026-10-19 · 플랜 ① · 하네스 전용 · 자산·게임 코드 0) ──────────
        /// <summary>참이면 Run 이 **파밍 층**으로 돈다 — 무한(승패 없음) · 정원까지 간격마다 리필 · 로봇 파괴만 끝.</summary>
        internal static bool FarmMode;
        /// <summary>정원 바꿈(0 = 표 그대로). 종류 비율을 지켜 줄인다.</summary>
        internal static int FarmCapTarget;
        /// <summary>참이면 파밍에도 탐색 범위(#153 도전 층 규칙)를 건다 — 즉시 접근 끔.</summary>
        internal static bool FarmAggro;
        /// <summary>참이면 적 HP 의 S1 대비 증가분을 반으로(#149 식 한 번 더).</summary>
        internal static bool FarmHpHalfGrowth;
        /// <summary>파밍 한 판 길이(초).</summary>
        internal static float FarmSeconds = 60f;
        /// <summary>마지막 파밍 판의 HP 바닥 · 처치 수.</summary>
        internal static float LastFarmMinHp; internal static int LastFarmKills;

        /// <summary>장갑 축 훑기(2026-10-19 · 하네스 전용) — 음수 = 표 그대로.</summary>
        internal static float FarmArmorHp = -1f, FarmArmorDef = -1f; internal static int FarmArmorCount = -1;
        /// <summary>마지막 파밍 판의 장갑 처치 수 · 첫 장갑 처치 시각(없으면 -1).</summary>
        internal static int LastArmorKills; internal static float LastArmorFirstKill;
        private static int _armorSpawned;

        private static List<StageComposition> FarmRoster(StageDefinition stage)
        {
            var roster = new List<StageComposition>(stage.farmComposition);
            for (int i = roster.Count - 1; i >= 0; i--)
            {
                if (roster[i].enemyKey != "armor") continue;
                StageComposition c = roster[i];
                if (FarmArmorHp >= 0f) c.hp = FarmArmorHp;
                if (FarmArmorDef >= 0f) c.def = FarmArmorDef;
                if (FarmArmorCount >= 0) c.count = FarmArmorCount;
                if (c.count <= 0) roster.RemoveAt(i); else roster[i] = c;
            }
            if (FarmHpHalfGrowth)
            {
                var s1 = AssetDatabase.LoadAssetAtPath<StageDefinition>($"{SoRoot}/Stages/Stage_S1.asset");
                for (int i = 0; i < roster.Count; i++)
                {
                    StageComposition c = roster[i];
                    float baseHp = 0f;
                    if (s1 != null) foreach (StageComposition b in s1.farmComposition) if (b.enemyKey == c.enemyKey) { baseHp = b.hp; break; }
                    if (baseHp <= 0f) baseHp = c.hp;   // S1 에 없는 종류 = 기준 없음 → 그대로(⚠️)
                    c.hp = baseHp + (c.hp - baseHp) * 0.5f;
                    roster[i] = c;
                }
            }
            int cap = FarmRefillRule.CapacityOf(roster);
            if (FarmCapTarget > 0 && cap > 0 && FarmCapTarget != cap)
            {
                // 비율을 지켜 줄인다 — 큰 나머지 순(종류가 0 으로 사라지지 않게 최소 1).
                var exact = new float[roster.Count]; int sum = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    exact[i] = roster[i].count * (float)FarmCapTarget / cap;
                    StageComposition c = roster[i]; c.count = Mathf.Max(1, Mathf.FloorToInt(exact[i])); roster[i] = c; sum += c.count;
                }
                while (sum < FarmCapTarget)
                {
                    int best = 0; float bestR = -1f;
                    for (int i = 0; i < roster.Count; i++) { float r = exact[i] - roster[i].count; if (r > bestR) { bestR = r; best = i; } }
                    StageComposition c = roster[best]; c.count++; roster[best] = c; sum++;
                }
                while (sum > FarmCapTarget)
                {
                    int best = -1; int most = 1;
                    for (int i = 0; i < roster.Count; i++) if (roster[i].count > most) { most = roster[i].count; best = i; }
                    if (best < 0) break;
                    StageComposition c = roster[best]; c.count--; roster[best] = c; sum--;
                }
            }
            return roster;
        }

        private static void FarmRefill(CombatSimulation sim, List<StageComposition> roster,
                                       List<EnemyDefinition> catalog, CombatTuning tuning)
        {
            var alive = new Dictionary<string, int>();
            foreach (CombatEntity e in sim.Enemies)
            {
                if (!e.IsAlive || string.IsNullOrEmpty(e.label)) continue;
                string key = null;
                foreach (EnemyDefinition d in catalog) if (d != null && d.displayName == e.label) { key = d.enemyKey; break; }
                if (key == null) continue;
                alive.TryGetValue(key, out int n); alive[key] = n + 1;
            }
            var plan = new List<FarmRefillRule.Refill>();
            FarmRefillRule.Plan(roster, alive, plan);
            var batch = new List<EnemySpawn>();
            foreach (FarmRefillRule.Refill r in plan)
                for (int i = 0; i < r.count; i++)
                {
                    batch.Add(StageSpawnFactory.FarmSpawn(r.entry, catalog, tuning));
                    if (r.entry.enemyKey == "armor") _armorSpawned++;
                }
            if (batch.Count > 0) sim.SpawnBatch(batch);
            _farmSpawned += batch.Count;
        }
        private static int _farmSpawned;

        /// <summary>
        /// **파밍 층 생존 훑기**(2026-10-19 · 플랜 ① · 표만). 판 = 시작 보드 + S2 모듈 하나(7,8 생산량) · 강화 ×1.45 ·
        /// 마운트 가득 · 로봇 A 한 대(⚠️ S5 · S6 도 A 단독 — 실제 파밍은 태그 Auto 로 B 가 섞인다) · 60초.
        /// 배치: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunFarmSurvivalBatch</c>
        /// </summary>
        public static void RunFarmSurvivalBatch()
        {
            var m = AssetDatabase.LoadAssetAtPath<ModuleDefinition>($"{SoRoot}/Modules/Module_M.asset");
            var sb = new StringBuilder();
            sb.AppendLine("############ 파밍 층 생존 훑기 (2026-10-19 · 하네스 전용 · 자산 0) ############");
            sb.AppendLine("판 = 시작 보드 + 모듈 M (7,8) · 강화 ×1.45 · 마운트 가득 · 로봇 A 단독 · 60초 · 간격마다 정원까지 리필 · 즉시 접근(탐색 0)");
            sb.AppendLine("칸 = 생존(60초) 또는 파괴 시각 · 처치 · HP 바닥");
            sb.AppendLine();
            string F(StageVerdict v) => v.result == CombatResult.LoseDead
                ? $"파괴 {v.seconds:F1}초 · 처치 {LastFarmKills}"
                : $"**생존** · 처치 {LastFarmKills} · HP 바닥 {LastFarmMinHp:F0}";
            StageVerdict One(string sid, float hp)
            {
                BoardHook = g => g.GetAt(new Vector2Int(7, 8))?.TryAttachModule(m);
                Enhanced = true; FarmMode = true;
                Run(sid, preloadMount: true, propellantNeed: BasePropellantNeed, hpOverride: hp);
                StageVerdict v = LastVerdict;
                BoardHook = null; Enhanced = false; FarmMode = false;
                return v;
            }
            try
            {
                foreach (string sid in new[] { "S3", "S4", "S5", "S6" })
                {
                    var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>($"{SoRoot}/Stages/Stage_{sid}.asset");
                    int cap = stage != null ? FarmRefillRule.CapacityOf(stage.farmComposition) : 0;
                    sb.AppendLine($"## {sid} · 정원 {cap} · 간격 {(stage != null ? stage.spawnInterval : 0f):F0}초");
                    sb.AppendLine("| 갈래 | 결과 |");
                    sb.AppendLine("|---|---|");
                    sb.AppendLine($"| 기준(정원 {cap} · HP 1000) | {F(One(sid, 0f))} |");
                    int[] caps = cap >= 15 ? new[] { 13, 10, 8 } : new[] { 7, 5 };
                    foreach (int c in caps)
                    {
                        FarmCapTarget = c;
                        sb.AppendLine($"| (a) 정원 {c} | {F(One(sid, 0f))} |");
                        FarmCapTarget = 0;
                    }
                    FarmAggro = true;
                    sb.AppendLine($"| (b) 탐색 6 + 맞으면 깸 | {F(One(sid, 0f))} |");
                    FarmAggro = false;
                    foreach (float hp in new[] { 1500f, 2000f })
                        sb.AppendLine($"| (c) 로봇 HP {hp:F0} | {F(One(sid, hp))} |");
                    FarmHpHalfGrowth = true;
                    sb.AppendLine($"| (d) 적 HP 증가분 ×0.5 | {F(One(sid, 0f))} |");
                    FarmHpHalfGrowth = false;
                    sb.AppendLine();
                }
            }
            finally { FarmMode = false; FarmCapTarget = 0; FarmAggro = false; FarmHpHalfGrowth = false; BoardHook = null; KnobReset(); }
            System.IO.File.WriteAllText("Docs/261019_farm_survival.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **장갑 축 훑기**(2026-10-19 · 사용자 「방패몹 체력·방어 너무 강함」 · 표만).
        /// 배치: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunFarmArmorBatch</c>
        /// </summary>
        public static void RunFarmArmorBatch()
        {
            var m = AssetDatabase.LoadAssetAtPath<ModuleDefinition>($"{SoRoot}/Modules/Module_M.asset");
            var sb = new StringBuilder();
            sb.AppendLine("############ 파밍 장갑 축 훑기 (2026-10-19 · 하네스 전용 · 자산 0) ############");
            sb.AppendLine("판 = 시작 보드 + 모듈 M (7,8) · 강화 ×1.45 · 마운트 가득 · 로봇 A 단독 · 60초 · 리필 · 즉시 접근");
            sb.AppendLine("칸 = 생존(HP 바닥) / 파괴 시각 · 처치 · 장갑 처치 · 첫 장갑 처치 시각");
            sb.AppendLine();
            string F(StageVerdict v) => (v.result == CombatResult.LoseDead ? $"파괴 {v.seconds:F1}초" : $"**생존** · HP 바닥 {LastFarmMinHp:F0}")
                + $" · 처치 {LastFarmKills} · 장갑 처치 {LastArmorKills}" + (LastArmorFirstKill >= 0f ? $" · 첫 장갑 {LastArmorFirstKill:F1}초" : " · 장갑 못 잡음");
            string One(string sid)
            {
                BoardHook = g => g.GetAt(new Vector2Int(7, 8))?.TryAttachModule(m);
                Enhanced = true; FarmMode = true;
                Run(sid, preloadMount: true, propellantNeed: BasePropellantNeed, hpOverride: 0f);
                string r = F(LastVerdict);
                BoardHook = null; Enhanced = false; FarmMode = false;
                return r;
            }
            void Row(string label, string sid, float hp, float def, int cnt, int cap = 0)
            {
                FarmArmorHp = hp; FarmArmorDef = def; FarmArmorCount = cnt; FarmCapTarget = cap;
                sb.AppendLine($"| {label} | {One(sid)} |");
                FarmArmorHp = -1f; FarmArmorDef = -1f; FarmArmorCount = -1; FarmCapTarget = 0;
            }
            try
            {
                sb.AppendLine("## S3 (보병 14 · 포격 2 · 장갑 1 · 장갑 hp 600 · def 3)");
                sb.AppendLine("| 갈래 | 결과 |"); sb.AppendLine("|---|---|");
                Row("(ㄹ) 지금 값", "S3", -1f, -1f, -1);
                Row("(ㄹ) 정원 10(비율)", "S3", -1f, -1f, -1, 10);
                foreach (float hp in new[] { 400f, 300f, 150f }) Row($"(ㄱ) 장갑 hp {hp:F0}", "S3", hp, -1f, -1);
                foreach (float d in new[] { 1f, 0f }) Row($"(ㄴ) 장갑 def {d:F0}", "S3", -1f, d, -1);
                Row("(ㄷ) 장갑 0기(정원 16)", "S3", -1f, -1f, 0);
                sb.AppendLine();
                sb.AppendLine("## 참고 — S4 (장갑 1 · 550/5) · S5 (장갑 2 · 850/45)");
                sb.AppendLine("| 갈래 | 결과 |"); sb.AppendLine("|---|---|");
                Row("S4 지금 값", "S4", -1f, -1f, -1);
                Row("S4 장갑 0기", "S4", -1f, -1f, 0);
                Row("S4 장갑 hp 275(반)", "S4", 275f, -1f, -1);
                Row("S5 지금 값", "S5", -1f, -1f, -1);
                Row("S5 장갑 0기", "S5", -1f, -1f, 0);
                Row("S5 장갑 def 5(보병과 같게)", "S5", -1f, 5f, -1);
                Row("S5 장갑 hp 425(반)", "S5", 425f, -1f, -1);
            }
            finally { FarmMode = false; FarmArmorHp = -1f; FarmArmorDef = -1f; FarmArmorCount = -1; FarmCapTarget = 0; BoardHook = null; KnobReset(); }
            System.IO.File.WriteAllText("Docs/261019_farm_armor.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>두 로봇 파밍 — 장갑에 든 피해(어림 · 틱 단위 귀속: 태그 스킬 틱 / 합체 중 / A 나섬 / B 나섬).</summary>
        internal static float ArmorDmgA, ArmorDmgB, ArmorDmgSkill, ArmorDmgMerge;

        /// <summary>
        /// **두 로봇 파밍 생존 훑기**(2026-10-19 · 사용자 「방어 유지하고 로봇 B 등장 이후」 · 표만).
        /// 배치: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunFarmTagBatch</c>
        /// </summary>
        public static void RunFarmTagBatch()
        {
            var m = AssetDatabase.LoadAssetAtPath<ModuleDefinition>($"{SoRoot}/Modules/Module_M.asset");
            var sb = new StringBuilder();
            sb.AppendLine("############ 두 로봇 파밍 생존 훑기 (2026-10-19 · 하네스 전용 · 자산 0) ############");
            sb.AppendLine("판 = A·B 둘 · Auto 태그 · 강화 A·B ×1.45 · 마운트 A·B 가득 · 합체 만충 즉시 · 60초 · 리필 · 즉시 접근");
            sb.AppendLine("칸 = 생존(HP 바닥) / 파괴 · 처치 · 장갑 처치 · 장갑 피해 몫(어림 · 틱 귀속 = 태그 스킬 / 합체 중 / A / B)");
            sb.AppendLine();
            string Row(string sid, string label, System.Action<BoardGrid> hook)
            {
                BoardHook = hook; Enhanced = true; FarmMode = true; AutoMerge = true;
                AStartRatio = 1f; BStartRatio = 1f; MergeStartRatio = 0f;
                RunTag(sid);
                StageVerdict v = LastVerdict;
                float tot = ArmorDmgA + ArmorDmgB + ArmorDmgSkill + ArmorDmgMerge;
                string share = tot <= 0f ? "장갑 피해 0"
                    : $"A {ArmorDmgA / tot * 100f:F0}% · B {ArmorDmgB / tot * 100f:F0}% · 합체 {ArmorDmgMerge / tot * 100f:F0}% · 태그 스킬 {ArmorDmgSkill / tot * 100f:F0}% (합 {tot:F0})";
                string r = $"| {sid} | {label} | " + (v.result == CombatResult.LoseDead ? $"파괴 {v.seconds:F1}초" : $"**생존** · HP 바닥 {LastFarmMinHp:F0}")
                    + $" · 처치 {LastFarmKills} · 장갑 처치 {LastArmorKills} | {share} |";
                BoardHook = null; FarmMode = false; KnobReset();
                return r;
            }
            try
            {
                sb.AppendLine("| 판 | 보드 | 결과 | 장갑 피해 몫 |");
                sb.AppendLine("|---|---|---|---|");
                foreach (string sid in new[] { "S5", "S6" })
                {
                    sb.AppendLine(Row(sid, "(a) 시작 보드 + M", g => g.GetAt(new Vector2Int(7, 8))?.TryAttachModule(m)));
                    sb.AppendLine(Row(sid, "(b) 폭발탄 프리셋(#169)", g => PresetBoards.ApplyExplosive(g, NodeResolver(BasePropellantNeed))));
                }
                sb.AppendLine();
                sb.AppendLine("## 참고 — #204 반영 판(A 단독 · 앞 탐침 그대로)");
                sb.AppendLine("| 판 | 결과 |"); sb.AppendLine("|---|---|");
                foreach (string sid in new[] { "S3", "S4" })
                {
                    BoardHook = g => g.GetAt(new Vector2Int(7, 8))?.TryAttachModule(m); Enhanced = true; FarmMode = true;
                    Run(sid, preloadMount: true, propellantNeed: BasePropellantNeed, hpOverride: 0f);
                    StageVerdict v = LastVerdict;
                    sb.AppendLine($"| {sid} | " + (v.result == CombatResult.LoseDead ? $"파괴 {v.seconds:F1}초" : $"**생존** · HP 바닥 {LastFarmMinHp:F0}")
                        + $" · 처치 {LastFarmKills} · 장갑 처치 {LastArmorKills} |");
                    BoardHook = null; FarmMode = false; Enhanced = false;
                }
            }
            finally { FarmMode = false; BoardHook = null; KnobReset(); }
            System.IO.File.WriteAllText("Docs/261019_farm_tag.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **#205 ④ — S5 파밍 생존 탐침**(2026-10-19 · CID_206 사용자 판정 자료 · 값 0 · 표만).
        /// 배치: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunFarmS5Batch</c>
        /// </summary>
        public static void RunFarmS5Batch()
        {
            var m = AssetDatabase.LoadAssetAtPath<ModuleDefinition>($"{SoRoot}/Modules/Module_M.asset");
            var sb = new StringBuilder();
            sb.AppendLine("############ S5 파밍 생존 탐침 (2026-10-19 · #205 ④ · 하네스 전용 · 자산 0) ############");
            sb.AppendLine("판 = A·B 둘 · Auto 태그 · 합체 만충 즉시 · 60초 · 리필 · 즉시 접근 · 정원 = FarmCapTarget(0 = 표 그대로 · 비율 유지)");
            sb.AppendLine();
            string Row(string sid, string label, System.Action<BoardGrid> hook, int bEnh, int cap, float aStart, float bStart)
            {
                BoardHook = hook; Enhanced = true; EnhancedBOverride = bEnh; FarmMode = true; AutoMerge = true; FarmCapTarget = cap;
                AStartRatio = aStart; BStartRatio = bStart; MergeStartRatio = 0f;
                RunTag(sid);
                StageVerdict v = LastVerdict;
                string r = $"| {sid} | {label} | " + (v.result == CombatResult.LoseDead ? $"파괴 {v.seconds:F1}초" : $"**생존** · HP 바닥 {LastFarmMinHp:F0}")
                    + $" · 처치 {LastFarmKills} · 장갑 처치 {LastArmorKills} |";
                BoardHook = null; FarmMode = false; FarmCapTarget = 0; KnobReset();
                return r;
            }
            System.Action<BoardGrid> startM = g => g.GetAt(new Vector2Int(7, 8))?.TryAttachModule(m);
            System.Action<BoardGrid> preset = g => PresetBoards.ApplyExplosive(g, NodeResolver(BasePropellantNeed));
            try
            {
                sb.AppendLine("## ⓐ 정원 갈래 — 화면 조건(A 강화 · **B 미강화** · 시작 보드 + M)");
                sb.AppendLine("| 판 | 갈래 | 결과 |"); sb.AppendLine("|---|---|---|");
                sb.AppendLine(Row("S5", "정원 표 그대로 · 마운트 가득 시작", startM, 0, 0, 1f, 1f));
                sb.AppendLine(Row("S5", "정원 표 그대로 · 마운트 빈 채 시작", startM, 0, 0, 0f, 0f));
                foreach (int cap in new[] { 7, 5 })
                {
                    sb.AppendLine(Row("S5", $"정원 {cap} · 마운트 가득 시작", startM, 0, cap, 1f, 1f));
                    sb.AppendLine(Row("S5", $"정원 {cap} · 마운트 빈 채 시작", startM, 0, cap, 0f, 0f));
                }
                sb.AppendLine(Row("S5", "참고 — B 강화(종전 표 조건)", startM, 1, 0, 1f, 1f));
                sb.AppendLine();
                sb.AppendLine("## ⓑ S5 막 동안 전투 정지 갈래(#203 에 S5 추가)");
                sb.AppendLine("막 동안 = 적 · 발사 정지 → 피해 0(정의상). 막이 걷힌 뒤 A 판 = 다섯 칸 완성 = 폭발탄 프리셋과 같은 판:");
                sb.AppendLine("| 판 | 갈래 | 결과 |"); sb.AppendLine("|---|---|---|");
                sb.AppendLine(Row("S5", "막 뒤 판(폭발탄 프리셋) · B 미강화 · 정원 표 그대로", preset, 0, 0, 1f, 1f));
                sb.AppendLine(Row("S5", "막 뒤 판(폭발탄 프리셋) · B 미강화 · 마운트 빈 채", preset, 0, 0, 0f, 0f));
                sb.AppendLine();
                sb.AppendLine("## ⓒ S3 화면 ↔ 하네스 갈림 탐침(A 단독 · 강화 · 시작 보드 + M)");
                sb.AppendLine("| 판 | 갈래 | 결과 |"); sb.AppendLine("|---|---|---|");
                foreach (bool pre in new[] { true, false })
                {
                    BoardHook = startM; Enhanced = true; FarmMode = true;
                    Run("S3", preloadMount: pre, propellantNeed: BasePropellantNeed, hpOverride: 0f);
                    StageVerdict v = LastVerdict;
                    sb.AppendLine($"| S3 | 마운트 {(pre ? "가득" : "빈 채")} 시작 | " + (v.result == CombatResult.LoseDead ? $"파괴 {v.seconds:F1}초" : $"**생존** · HP 바닥 {LastFarmMinHp:F0}")
                        + $" · 처치 {LastFarmKills} · 장갑 처치 {LastArmorKills} |");
                    BoardHook = null; FarmMode = false; Enhanced = false;
                }
            }
            finally { FarmMode = false; FarmCapTarget = 0; BoardHook = null; KnobReset(); }
            System.IO.File.WriteAllText("Docs/261019_farm_s5.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>피해량 배수 — 하네스 전용(2026-10-16 피해량 축). `moduleMult` 한 칸에 곱한다(PerHit 의 곱 항 · 드론도 같은 항).</summary>
        internal static float DmgMult = 1f;

        /// <summary>발사율 배수 — 하네스 전용. 무기 `shotsPerSec` 와 `consumptionCap` 에 함께 곱한다.</summary>
        internal static float RateMult = 1f;

        /// <summary>자산을 메모리에서만 복제해 배수를 건다 — **자산 파일은 안 건드린다**(저장 안 함).</summary>
        private static RobotDefinition Knob(RobotDefinition r)
        {
            if (r == null || (Mathf.Approximately(DmgMult, 1f) && Mathf.Approximately(RateMult, 1f))) return r;
            RobotDefinition c = Object.Instantiate(r);
            c.hideFlags = HideFlags.DontSave;
            c.moduleMult = r.moduleMult * DmgMult;
            c.weapons = new List<WeaponSpec>(r.weapons);
            for (int i = 0; i < c.weapons.Count; i++)
            {
                WeaponSpec w = c.weapons[i];
                w.shotsPerSec *= RateMult;
                c.weapons[i] = w;
            }
            c.consumptionCap = r.consumptionCap * RateMult;
            return c;
        }

        private static string Row(string head, string[] ids)
        {
            var cells = new List<string>();
            foreach (string id in ids) cells.Add(Cell(RunOne(id, 0f)));
            return $"| {head} | " + string.Join(" | ", cells) + " |";
        }

        /// <summary>
        /// **피해량 축 훑기**(2026-10-16 · 사용자 「클리어 못 하는 조건은 피해량이 부족해서다」 · 자산·값 0).
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunDamageSweepBatch</c>
        /// </summary>
        public static void RunDamageSweepBatch()
        {
            var sb = new StringBuilder();
            string[] ids = { "S1", "S2", "S3", "S4", "S5", "S6" };
            string head = "| 배수 | " + string.Join(" | ", ids) + " |";
            string bar = "|---|" + string.Concat(System.Array.ConvertAll(ids, _ => "---|"));
            sb.AppendLine("############ 피해량 축 훑기 (2026-10-16 · 9d3a11e 자산 · 하네스 안에서만 바꿈) ############");
            try
            {
                // ── 바탕 수치 — 자산에서 읽는다 ──
                var ra = AssetDatabase.LoadAssetAtPath<RobotDefinition>($"{SoRoot}/Robots/Robot_A.asset");
                var tu = AssetDatabase.LoadAssetAtPath<CombatTuning>($"{SoRoot}/CombatTuning.asset");
                sb.AppendLine($"Robot_A moduleMult {ra.moduleMult} · mountCoef {ra.mountCoef} · enhanced {ra.enhancedMountCoef} · consumptionCap {ra.consumptionCap} · multiShot {tu.multiShotCountTbd} · aoeSplash {tu.aoeSplashFactorTbd}");
                foreach (WeaponSpec w in ra.weapons) sb.AppendLine($"  무기 {w.kind} d{w.damagePerShot} · {w.shotsPerSec}발/초");
                foreach (string id in new[] { "S1", "S2", "S3", "S4" })
                {
                    List<EnemySpawn> got = null;
                    SpawnHook = l => { got = l; return l; };
                    RunOne(id, 0f);
                    SpawnHook = null;
                    var kinds = new Dictionary<string, int>();
                    var desc = new Dictionary<string, string>();
                    if (got != null)
                        foreach (EnemySpawn e in got)
                        {
                            kinds[e.label] = kinds.TryGetValue(e.label, out int n) ? n + 1 : 1;
                            desc[e.label] = $"hp{e.hp} def{e.def} atk{e.atk}";
                        }
                    var parts = new List<string>();
                    foreach (var kv in kinds) parts.Add($"{kv.Key} ×{kv.Value} ({desc[kv.Key]})");
                    sb.AppendLine($"  {id} 적: " + string.Join(" · ", parts));
                }

                sb.AppendLine();
                sb.AppendLine("## ② 피해량 (moduleMult × k · 발사율 그대로)");
                sb.AppendLine(head); sb.AppendLine(bar);
                foreach (float k in new[] { 1f, 1.25f, 1.5f, 2f, 3f })
                { DmgMult = k; sb.AppendLine(Row($"피해 ×{k:F2}", ids)); DmgMult = 1f; }

                sb.AppendLine();
                sb.AppendLine("## ②' 발사율 (shotsPerSec·consumptionCap × k · 피해 그대로)");
                sb.AppendLine(head); sb.AppendLine(bar);
                foreach (float k in new[] { 1.5f, 2f })
                { RateMult = k; sb.AppendLine(Row($"발사율 ×{k:F2}", ids)); RateMult = 1f; }

                sb.AppendLine();
                sb.AppendLine("## ③ 모듈 구멍 메움 + 피해량 — 모듈 흐름은 판 배수로 대신 잼 ⚠️ 어림(판 전체 곱 · 전력도 같이 곱해짐)");
                sb.AppendLine("  모듈 1개(생산량 또는 생산속도) = 탄약 2.00→2.50 = 판 ×1.25 · 둘(같은 노드 ×1.5×1.5) = 2.00→3.25 = 판 ×1.625");
                sb.AppendLine(head); sb.AppendLine(bar);
                foreach (float m in new[] { 1.25f, 1.625f })
                    foreach (float k in new[] { 1f, 1.25f, 1.5f, 2f, 3f })
                    {
                        PlantScale = m; DmgMult = k;
                        sb.AppendLine(Row($"판 ×{m:F3}(어림) · 피해 ×{k:F2}", ids));
                        PlantScale = 1f; DmgMult = 1f;
                    }
            }
            finally { SpawnHook = null; HpAll = 0f; PlantScale = 1f; DmgMult = 1f; RateMult = 1f; }

            System.IO.File.WriteAllText("Docs/261016_damage_sweep.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// 마운트 강화 표식 — 하네스 판의 「저장」 자리(2026-10-16 #150). 기본 거짓 = 강화 안 한 판.
        /// ⚠️ 종전 하네스는 S4+ 를 저절로 ×1.45 로 쟀다 — 이제 이 칸을 켜야 그 판이다.
        /// </summary>
        public static bool Enhanced;
        /// <summary>#205 ④ — 로봇 B 강화만 따로(-1 = Enhanced 따라감 · 0 = 미강화 · 1 = 강화) · 하네스 전용.</summary>
        internal static int EnhancedBOverride = -1;

        /// <summary>
        /// **#149·#150 투입 뒤 S1~S6 표**(2026-10-16) — 강화 전·후 둘.
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunPostChangeBatch</c>
        /// </summary>
        public static void RunPostChangeBatch()
        {
            var sb = new StringBuilder();
            string[] ids = { "S1", "S2", "S3", "S4", "S5", "S6" };
            sb.AppendLine("############ #149~#153 투입 뒤 (2026-10-16 · moduleMult 1.5 · hp 성장 절반 · 계수 = 강화 표식 · 도전 60초·수 절반 · 전원 스폰 · 탐색 범위) ############");
            sb.AppendLine("판 = 시작 보드 + 튜토리얼 칸 · 마운트 가득 · 자동 조종 · S5·S6 = 두 로봇(태그) 판 · 제한 시간 = 자산(#151 뒤 60초)");
            try
            {
                var ra = AssetDatabase.LoadAssetAtPath<RobotDefinition>($"{SoRoot}/Robots/Robot_A.asset");
                sb.AppendLine($"Robot_A moduleMult {ra.moduleMult} · mountCoef {ra.mountCoef} · enhanced {ra.enhancedMountCoef}");
                foreach (string id in new[] { "S1", "S2", "S3", "S4", "S5", "S6" })
                {
                    List<EnemySpawn> got = null;
                    SpawnHook = l => { got = l; return l; };
                    RunOne(id, 0f);
                    SpawnHook = null;
                    var kinds = new Dictionary<string, int>();
                    var desc = new Dictionary<string, string>();
                    if (got != null)
                        foreach (EnemySpawn e in got)
                        {
                            kinds[e.label] = kinds.TryGetValue(e.label, out int n) ? n + 1 : 1;
                            desc[e.label] = $"hp{e.hp} def{e.def} atk{e.atk}";
                        }
                    var parts = new List<string>();
                    foreach (var kv in kinds) parts.Add($"{kv.Key} ×{kv.Value} ({desc[kv.Key]})");
                    sb.AppendLine($"  {id} 적: " + string.Join(" · ", parts));
                }
                sb.AppendLine();
                sb.AppendLine("| 강화 | " + string.Join(" | ", ids) + " |");
                sb.AppendLine("|---|" + string.Concat(System.Array.ConvertAll(ids, _ => "---|")));
                var spawnRows = new List<string>();
                foreach (bool en in new[] { false, true })
                {
                    Enhanced = en;
                    var cells = new List<string>();
                    foreach (string id in ids)
                    {
                        StageVerdict v = RunOne(id, 0f);
                        cells.Add(Cell(v));
                        string after = v.allSpawnedAt < 0f ? $"— (다 안 나옴 · 미등장 {v.unspawned})"
                            : v.result == CombatResult.Win ? $"{v.seconds - v.allSpawnedAt:F1}초"
                            : $"— (전멸 못 함 · 살아 있음 {v.remaining})";
                        spawnRows.Add($"| {(en ? "뒤" : "전")} | {id} | {v.plannedLastSpawn:F1}초 | "
                                      + (v.allSpawnedAt < 0f ? "—" : $"{v.allSpawnedAt:F1}초") + $" | {after} | {v.peakAggro} |");
                    }
                    sb.AppendLine($"| {(en ? "투입 뒤 (×1.45)" : "투입 전 (×1.0)")} | " + string.Join(" | ", cells) + " |");
                    Enhanced = false;
                }
                sb.AppendLine();
                sb.AppendLine("## 스폰 열 — 마지막 적 예정 시각(WaveSpawnRule) · 실제 다 나온 시각 · 다 나온 뒤 전멸까지(= 순수 처치 시간)");
                sb.AppendLine("⚠️ #152 뒤 「마지막 스폰 예정」은 옛 묶음 규칙의 셈이다 — 도전 층은 이제 첫 틱에 전원이 선다(다 나온 시각 참고).");
                sb.AppendLine("| 강화 | 판 | 마지막 스폰 예정(옛 셈) | 다 나온 시각 | 다 나온 뒤 전멸까지 | 한 번에 깨어 있던 최대 수 |");
                sb.AppendLine("|---|---|---|---|---|---|");
                foreach (string r in spawnRows) sb.AppendLine(r);
            }
            finally { SpawnHook = null; HpAll = 0f; PlantScale = 1f; DmgMult = 1f; RateMult = 1f; Enhanced = false; }

            System.IO.File.WriteAllText("Docs/261016_post155_sweep.md", sb.ToString());   // 🗑️ 구 post149·151·152·153·153d 파일명 — 그 표들은 앞 커밋에 남는다
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **S5 LoseDead 당겨짐 원인 프로브**(2026-10-16 · 플랜 요청 · 값 0). S5 두 로봇 판을 갈래별로 돌려
        /// 상세 보고를 통째로 남긴다 — 무엇이 47.4초 → 25.7초를 만들었나.
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunS5ProbeBatch</c>
        /// </summary>
        public static void RunS5ProbeBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ S5 원인 프로브 (2026-10-16 · 하네스 안에서만 바꿈) ############");
            try
            {
                var ra = AssetDatabase.LoadAssetAtPath<RobotDefinition>($"{SoRoot}/Robots/Robot_A.asset");
                var rb = AssetDatabase.LoadAssetAtPath<RobotDefinition>($"{SoRoot}/Robots/Robot_B.asset");
                var rf = AssetDatabase.LoadAssetAtPath<RobotDefinition>($"{SoRoot}/Robots/Robot_Fusion.asset");
                sb.AppendLine($"moduleMult A {ra.moduleMult} · B {rb.moduleMult} · Fusion {(rf != null ? rf.moduleMult.ToString() : "없음")}");
                var cases = new (string name, float dmg, bool en, System.Func<List<EnemySpawn>, List<EnemySpawn>> hook)[]
                {
                    ("지금 값(moduleMult 1.5 · 장갑 7 × hp850 · 강화 안 함)", 1f, false, null),
                    ("지금 값 + 강화(×1.45)", 1f, true, null),
                    ("moduleMult 1.0 으로 되돌림(피해 ÷1.5)", 1f / 1.5f, false, null),
                    ("moduleMult 1.0 + 강화(= 구 9d3a11e 의 계수 조합)", 1f / 1.5f, true, null),
                    ("장갑 hp 1100 으로 되돌림", 1f, false, l => { var o = new List<EnemySpawn>(); foreach (var e in l) { var c = e; c.hp = 1100f; o.Add(c); } return o; }),
                    ("moduleMult 1.0 + 강화 + hp 1100 + 수 15(같은 줄 두 번 · 구 판에 가깝게)", 1f / 1.5f, true,
                        l => { var o = new List<EnemySpawn>(); foreach (var e in l) { var c = e; c.hp = 1100f; o.Add(c); o.Add(c); } if (o.Count > 15) o.RemoveRange(15, o.Count - 15); return o; }),
                };
                foreach (var c in cases)
                {
                    DmgMult = c.dmg; Enhanced = c.en; SpawnHook = c.hook;
                    string detail = RunTag("S5");
                    StageVerdict v = LastVerdict;
                    DmgMult = 1f; Enhanced = false; SpawnHook = null;
                    sb.AppendLine();
                    sb.AppendLine($"## {c.name} → {Cell(v)}");
                    sb.AppendLine(detail);
                }
            }
            finally { SpawnHook = null; HpAll = 0f; PlantScale = 1f; DmgMult = 1f; RateMult = 1f; Enhanced = false; }

            System.IO.File.WriteAllText("Docs/261016_s5_probe.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>탐색 범위 덮기 — 하네스 전용(2026-10-16 훑기). 0 이하면 자산 값.</summary>
        internal static float AggroOverride;

        /// <summary>
        /// **탐색 범위 훑기**(2026-10-16 · #152 뒤 · 값 0 · 하네스만).
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunAggroSweepBatch</c>
        /// </summary>
        public static void RunAggroSweepBatch()
        {
            var sb = new StringBuilder();
            string[] ids = { "S1", "S2", "S3", "S4", "S5", "S6" };
            sb.AppendLine("############ 탐색 범위 훑기 (2026-10-16 · 08ec1c7 자산 · 하네스 안에서만 바꿈 · 60초) ############");
            sb.AppendLine("칸 = 결과 · 처치 · 시간 · HP · [한 번에 깨어 있던 최대 수]");
            var bothAt = new List<string>();
            try
            {
                foreach (bool en in new[] { false, true })
                {
                    sb.AppendLine();
                    sb.AppendLine(en ? "## 강화 뒤 (×1.45)" : "## 강화 전 (×1.0)");
                    sb.AppendLine("| 탐색 범위 | " + string.Join(" | ", ids) + " |");
                    sb.AppendLine("|---|" + string.Concat(System.Array.ConvertAll(ids, _ => "---|")));
                    foreach (float r in new[] { 4f, 5f, 6f, 7f, 8f, 9.2f })
                    {
                        AggroOverride = r; Enhanced = en;
                        var cells = new List<string>();
                        bool s3 = false, s4 = false;
                        foreach (string id in ids)
                        {
                            StageVerdict v = RunOne(id, 0f);
                            cells.Add(Cell(v) + $" · [{v.peakAggro}]");
                            if (id == "S3") s3 = v.result == CombatResult.Win;
                            if (id == "S4") s4 = v.result == CombatResult.Win;
                        }
                        AggroOverride = 0f; Enhanced = false;
                        sb.AppendLine($"| {r:0.0}{(Mathf.Approximately(r, 9.2f) ? " (지금 표)" : "")} | " + string.Join(" | ", cells) + " |");
                        if (s3 && s4) bothAt.Add($"{(en ? "강화 뒤" : "강화 전")} {r:0.0}");
                    }
                }
            }
            finally { SpawnHook = null; HpAll = 0f; PlantScale = 1f; DmgMult = 1f; RateMult = 1f; Enhanced = false; AggroOverride = 0f; }

            sb.AppendLine();
            sb.AppendLine("S3·S4 둘 다 서는 범위: " + (bothAt.Count > 0 ? string.Join(" · ", bothAt) : "없음(4~9.2 안에서)"));
            System.IO.File.WriteAllText("Docs/261016_aggro_sweep.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **S3·S4 처치 가능 수 재기**(2026-10-16 · 사용자 확정 #153 ④ · 값 0 · 하네스만).
        /// S3 = 강화 전 · S4 = 강화 뒤(벽). 지금 수로 60초에 잡은 수 → 그 90% 를 종류 비율대로 내림 →
        /// 그 수로 솎아(Thin · 차례 유지) 다시 돌려 서는지 본다.
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunCountProbeBatch</c>
        /// </summary>
        public static void RunCountProbeBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ S3·S4 처치 가능 수 (2026-10-16 · #153 ①②③ 뒤 · 60초 · 하네스 안에서만 바꿈) ############");
            try
            {
                foreach (var (id, en) in new[] { ("S3", false), ("S4", true) })
                {
                    Enhanced = en;
                    List<EnemySpawn> got = null;
                    SpawnHook = l => { got = l; return l; };
                    StageVerdict full = RunOne(id, 0f);
                    SpawnHook = null;
                    int kills = full.total - full.remaining - full.unspawned;
                    int target = Mathf.FloorToInt(kills * 0.9f);
                    sb.AppendLine();
                    sb.AppendLine($"## {id} ({(en ? "강화 뒤" : "강화 전")})");
                    sb.AppendLine($"지금 수 그대로: {Cell(full)} · [깬 최대 {full.peakAggro}] → 60초 처치 {kills} → 90% = {target}");

                    var kinds = new Dictionary<string, int>();
                    foreach (EnemySpawn e in got) kinds[e.label] = kinds.TryGetValue(e.label, out int n) ? n + 1 : 1;
                    sb.AppendLine("| 종류 | 지금 | 비율대로 90% 처치 몫(내림) |");
                    sb.AppendLine("|---|---|---|");
                    int sum = 0;
                    foreach (var kv in kinds)
                    {
                        int share = Mathf.Max(kv.Value > 0 && target > 0 ? 1 : 0,
                            Mathf.FloorToInt((float)target * kv.Value / full.total));
                        sum += share;
                        sb.AppendLine($"| {kv.Key} | {kv.Value} | {share} |");
                    }
                    sb.AppendLine($"| 합 | {full.total} | {sum} |");

                    float k = full.total > 0 ? (float)sum / full.total : 1f;
                    foreach (bool en2 in new[] { false, true })
                    {
                        Enhanced = en2;
                        SpawnHook = l => Thin(l, k);
                        StageVerdict v = RunOne(id, 0f);
                        SpawnHook = null;
                        sb.AppendLine($"  솎아 다시({(en2 ? "강화 뒤" : "강화 전")} · 약 {sum}기 · 차례 유지 솎음 ⚠️ 종류 수는 솎음 결과라 표와 ±1): {Cell(v)} · [깬 최대 {v.peakAggro}]");
                    }
                    Enhanced = false;
                }
            }
            finally { SpawnHook = null; HpAll = 0f; PlantScale = 1f; DmgMult = 1f; RateMult = 1f; Enhanced = false; AggroOverride = 0f; }

            System.IO.File.WriteAllText("Docs/261016_count_probe.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **S3·S4 서는 최대 수 찾기**(2026-10-16 · #153 ④ · 값 0). 90% 솎음이 안 서서(밀도가 낮아지면 처치 속도도 준다)
        /// 수를 내려 가며 강화 전·후 둘 다 잰다. 종류 비율은 Thin(차례 유지 솎음)이 지킨다.
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunCountSearchBatch</c>
        /// </summary>
        public static void RunCountSearchBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ S3·S4 서는 수 찾기 (2026-10-16 · #153 ①②③ 뒤 · 60초 · 하네스 안에서만 바꿈) ############");
            sb.AppendLine("칸 = 결과 · 처치 · 시간 · HP · [깬 최대] · 종류(솎음 결과)");
            try
            {
                foreach (string id in new[] { "S3", "S4" })
                {
                    sb.AppendLine();
                    sb.AppendLine($"## {id}");
                    sb.AppendLine("| 목표 수 | 강화 전 | 강화 뒤 |");
                    sb.AppendLine("|---|---|---|");
                    int total = id == "S3" ? 87 : 98;
                    foreach (int want in new[] { 39, 36, 33, 30, 27, 24, 21, 18, 15 })
                    {
                        float k = (float)want / total;
                        var cells = new List<string>();
                        string mix = "";
                        foreach (bool en in new[] { false, true })
                        {
                            Enhanced = en;
                            List<EnemySpawn> got = null;
                            SpawnHook = l => { got = Thin(l, k); return got; };
                            StageVerdict v = RunOne(id, 0f);
                            SpawnHook = null;
                            cells.Add(Cell(v) + $" · [{v.peakAggro}]");
                            var kinds = new Dictionary<string, int>();
                            foreach (EnemySpawn e in got) kinds[e.label] = kinds.TryGetValue(e.label, out int n) ? n + 1 : 1;
                            var parts = new List<string>();
                            foreach (var kv in kinds) parts.Add($"{kv.Key} {kv.Value}");
                            mix = string.Join(" · ", parts);
                        }
                        Enhanced = false;
                        sb.AppendLine($"| {want} ({mix}) | " + string.Join(" | ", cells) + " |");
                    }
                }
            }
            finally { SpawnHook = null; HpAll = 0f; PlantScale = 1f; DmgMult = 1f; RateMult = 1f; Enhanced = false; AggroOverride = 0f; }

            System.IO.File.WriteAllText("Docs/261016_count_search.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>합체가 차는 즉시 누른다 — 하네스 전용(2026-10-16 S6 확인).</summary>
        internal static bool AutoMerge;

        /// <summary>S6 자취 — 2.5초마다 로봇·보스 자리(하네스 진단 · 2026-10-16).</summary>
        internal static StringBuilder TraceS6;
        private static float TraceNext;

        /// <summary>#155 — 이 비율(0~1)의 게이지로 들어간다(하네스 · 음수면 0 에서 시작).</summary>
        internal static float MergeStartRatio = -1f;
        internal static float LastHpBeforeMerge = -1f, LastMergeEndHp = -1f;

        /// <summary>합체 게이지를 1초 앞까지 채운 채 시작 — 하네스 가정 판(2026-10-16 · 「합체를 쓸 수 있었다면」).</summary>
        internal static bool MergePrefill;
        internal static float LastMergeAt = -1f, LastMergeReadyAt = -1f, LastBurstDamage, LastEnemyHpLeft;
        /// <summary>#223 가름 — 합체 순간 나선 로봇(0 = A · 1 = B · -1 = 합체 없음) · 판 전체 교대 수.</summary>
        internal static int LastActiveAtMerge = -1, LastTagSwaps;
        /// <summary>
        /// **버스트 문서식 가정 손잡이**(2026-10-20 사용자 물음 · 하네스 전용 · 값 0 · 런타임 불변).
        /// <c>MergeSkillOff</c> = 합체 중 태그 스킬 금지(#163 되돌림 가정) · <c>DocBurstPercent</c> &gt; 0 이면 합체 순간
        /// 「롤링 전투력 합(A 명목 · B 드론 도착률 × 충전량) × 계수」를 사거리 안 적 **전부**에(방어 무시 · 가정) 더 준다.
        /// </summary>
        internal static bool MergeSkillOff;
        /// <summary>#229 과부하 손잡이(하네스 전용) — 0 이면 자산 `overloadSec`.</summary>
        internal static float OverloadSecOverride;
        /// <summary>
        /// #229 원인 분리 손잡이(2026-10-20 플랜 요청 · **하네스 전용 · 리플렉션 덮개 · 런타임 코드 0**).
        /// <c>OverloadSlotMult</c> = 과부하 동안 드론 슬롯 상한 ×N(끝나면 원래대로) · <c>OverloadFireOff</c> = 소비 ×2 끔(발사 ×1)
        /// · <c>OverloadSubstitute</c> = 과부하 동안 줄 탄종이 바닥이면 같은 마운트의 다른 탄 1발을 그 탄종으로 옮겨 쏘게 함(가정 · 피해는 그 줄 값).
        /// </summary>
        internal static float OverloadSlotMult = 1f;
        internal static bool OverloadFireOff, OverloadSubstitute;
        /// <summary>(ㅂ) 과부하 발사 배율 덮개(0 = 자산값) · (ㅁ) 과부하 중 발당 소비 비(1 = 그대로 · 0.5 = 두 발에 한 번 꼴 · 하네스 환급 덮개).</summary>
        internal static float OverloadFireRateOverride, OverloadConsumePerShot = 1f;
        /// <summary>#235 산출 배율 훑기(0 = 자산 overloadProduce) · 과부하 중 A 마운트 도착 수 · 최대 필드 드론 수.</summary>
        internal static float OverloadProduceOverride;
        /// <summary>#236 과부하 중 벨트 속도 ×N(하네스 · `BeltItemFlow.SpeedMultiplier`) · 상한 분리 진단값.</summary>
        internal static float OverloadBeltMult;   // 0 = 자산 overloadBeltMult(#239 게임과 같게)
        /// <summary>
        /// #233 퇴행 원인 분리 · 드론 쌓임 변형(2026-10-20 플랜 요청 · **하네스 덮개만 · 런타임 코드 0**).
        /// ㄱ <c>DroneFractionOverride</c> 타격 몫(0 = 자산) · ㄴ <c>DroneAllowanceUncap</c> 이월 1기 캡을 그림자 허용량으로 무력화(리플렉션)
        /// · ㄷ <c>DroneBCoefRestore</c> B 강화 = 마운트계수 1.45 복귀 · ㄹ <c>DroneSlotCapRestore</c> 동시 유지 상한(Active &lt; Slots) 흉내(구 코드 = 이월 무제한 + 상한)
        /// · ⓒ <c>DroneLaunchOnlyWithTarget</c> 사거리 안 살아 있는 적이 없으면 그 틱 처리량 0.
        /// </summary>
        internal static float DroneFractionOverride;
        /// <summary>
        /// #240 사용자 제안 덮개(2026-10-20 · 하네스만 · 런타임 코드 0) — 드론 **종류마다** 동시 유지 상한.
        /// <c>DroneKindCap</c> 0 = 끔 · 3(강화 전) · <c>DroneKindCapEnh</c> 5(B 강화) · <c>DroneCapOverloadDouble</c> = 과부하 동안 상한 ×2(갈래 b)
        /// · 덮개가 켜지면 처리량은 droneRelease(3) 고정(강화 +2 처리량 끔 → 강화 = 상한 +2 로만).
        /// 방법: 틱 전에 종류별 「남은 자리」를 넘는 재고를 B 마운트에서 잠시 빼 두고 틱 뒤 되돌린다(DroneBay 밖).
        /// </summary>
        internal static int DroneKindCap, DroneKindCapEnh = 5;
        /// <summary>태그 링 물음(2026-10-20 · 사용자 육안) — 대기 마운트 Total · Capacity 시계열(1초 · 하네스 전용).</summary>
        internal static StringBuilder StandbyTrace;
        private static float _standbyTraceNext;
        internal static bool DroneCapOverloadDouble;
        /// <summary>(c) 과부하 동안 드론 타격 간격 ×0.5(0.5 → 0.25 · 수명 절반 · 하네스 덮개 · sim.DroneHitInterval 프로퍼티).</summary>
        internal static bool DroneOverloadHitHalf;
        internal static int LastMaxStack, LastMaxAoe, LastDronesAtLastEnemy;
        internal static bool DroneAllowanceUncap, DroneBCoefRestore, DroneSlotCapRestore, DroneLaunchOnlyWithTarget;
        internal static float LastOverloadMountFullSec, LastFireDemandA;
        internal static float LastOverloadArrivalsA, LastOverloadSecondsA;
        internal static int LastMaxDronesOnField;
        internal static int LastMergeLaunches;
        private static readonly System.Reflection.BindingFlags NonPub = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        private static System.Collections.IList SidesOf(CombatSimulation sim) =>
            (System.Collections.IList)typeof(CombatSimulation).GetField("_sides", NonPub).GetValue(sim);

        private static object SideField(object side, string name) => side.GetType().GetField(name).GetValue(side);

        private static int DroneSpawnedOf(CombatSimulation sim) =>
            (int)typeof(CombatSimulation).GetField("_droneSpawned", NonPub).GetValue(sim);

        /// <summary>사출대 슬롯 상한을 덮는다(자동 속성 뒷칸 · 하네스 전용).</summary>
        private static void SetBaySlots(CombatSimulation sim, int[] baseSlots, float mult)
        {
            System.Collections.IList sides = SidesOf(sim);
            for (int i = 0; i < sides.Count; i++)
            {
                var bay = SideField(sides[i], "bay") as DroneBay;
                if (bay == null) continue;
                typeof(DroneBay).GetField("<Slots>k__BackingField", NonPub).SetValue(bay, Mathf.RoundToInt(baseSlots[i] * mult));
            }
        }

        private static int[] BaySlotsOf(CombatSimulation sim)
        {
            System.Collections.IList sides = SidesOf(sim);
            var r = new int[sides.Count];
            for (int i = 0; i < sides.Count; i++) { var bay = SideField(sides[i], "bay") as DroneBay; r[i] = bay != null ? bay.Slots : 0; }
            return r;
        }

        private static readonly MountItem[] AmmoItems = { MountItem.Pierce, MountItem.Standard, MountItem.Explosive };

        /// <summary>(ㄷ) 가정 — 줄 탄종이 바닥이면 같은 마운트의 다른 탄 1발을 그 탄종으로 옮긴다(틱마다 줄당 1발).</summary>
        private static void SubstituteDryLines(CombatSimulation sim)
        {
            System.Collections.IList sides = SidesOf(sim);
            for (int i = 0; i < sides.Count; i++)
            {
                var mount = SideField(sides[i], "mount") as MountLoad;
                var setup = (RobotSetup)SideField(sides[i], "setup");
                if (mount == null || setup.lines == null) continue;
                foreach (AmmoLine l in setup.lines)
                {
                    if (l.shotsPerSec <= 0f) continue;
                    MountItem need = MountItemMap.From(l.kind);
                    if (mount.AmountOf(need) >= 1f) continue;
                    foreach (MountItem other in AmmoItems)
                    {
                        if (other == need || mount.AmountOf(other) < 1f) continue;
                        if (!mount.TryConsume(other, 1f)) continue;
                        if (mount.Load(need, 1f) < 1f) mount.Load(other, 1f);   // 칸이 없으면 되돌린다
                        break;
                    }
                }
            }
        }
        internal static float LastMountADryAt, LastMountBDryAt, LastMergeEndAt, LastRefullA, LastRefullB, LastMountAAtMergeEnd, LastMountBAtMergeEnd;
        internal static float DocBurstPercent;
        internal static float LastDocBurst;
        /// <summary>#223 결함 후보 가름 — 합체 20초 동안 2초마다 A 줄 · A 마운트 · 실제 발사 · B 드론(하네스 전용 · 비우면 안 씀).</summary>
        internal static StringBuilder MergeDiag;
        private static float _mergeDiagNext;
        private static int _mergeDiagFired0;

        /// <summary>
        /// **S6 태그·합체 확인**(2026-10-16 사용자 직접 지시 「스테이지 6 태그 및 합체도 사용해도 시간 초과 발생하는지」 · 값 0).
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunS6MergeBatch</c>
        /// </summary>
        public static void RunS6MergeBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ S6 태그·합체 확인 (2026-10-16 · 983f3f6 자산 · 60초 · 두 로봇 판 · 자동 태그 켬) ############");
            sb.AppendLine("| 강화 | 합체 | 결과 | 합체 찬 시각 | 합체 누른 시각 | 버스트 피해 | 보스 남은 HP |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            try
            {
                foreach (bool en in new[] { false, true })
                    foreach (bool merge in new[] { false, true })
                    {
                        Enhanced = en; AutoMerge = merge;
                        StageVerdict v = RunOne("S6", 0f);
                        Enhanced = false; AutoMerge = false;
                        sb.AppendLine($"| {(en ? "뒤" : "전")} | {(merge ? "씀(차면 바로)" : "안 씀")} | {Cell(v)} | "
                                      + (LastMergeReadyAt < 0f ? "안 참" : $"{LastMergeReadyAt:F1}초") + " | "
                                      + (LastMergeAt < 0f ? "—" : $"{LastMergeAt:F1}초") + $" | {LastBurstDamage:F0} | {LastEnemyHpLeft:F0} |");
                    }
                sb.AppendLine();
                sb.AppendLine("## ⚠️ 가정 판 — 합체 게이지를 미리 채워 두었다면(게임에는 없는 판 · 게이지 90초 > 도전 60초라 지금은 못 찬다)");
                sb.AppendLine("| 강화 | 결과 | 합체 누른 시각 | 버스트 피해 | 보스 남은 HP |");
                sb.AppendLine("|---|---|---|---|---|");
                foreach (bool en in new[] { false, true })
                {
                    Enhanced = en; AutoMerge = true; MergePrefill = true;
                    StageVerdict v = RunOne("S6", 0f);
                    Enhanced = false; AutoMerge = false; MergePrefill = false;
                    sb.AppendLine($"| {(en ? "뒤" : "전")} | {Cell(v)} | " + (LastMergeAt < 0f ? "—" : $"{LastMergeAt:F1}초") + $" | {LastBurstDamage:F0} | {LastEnemyHpLeft:F0} |");
                }
                sb.AppendLine();
                sb.AppendLine("## #155 — 파밍에서 이어 온 게이지로 들어감 · 적이 로봇 사거리에 든 순간 합체(하네스 자동)");
                sb.AppendLine("| 강화 | 들고 온 게이지 | 결과 | 합체 누른 시각 | 버스트 피해 | 합체 20초 동안 깎은 HP | 60초에 깎은 HP | 보스 남은 HP |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|");
                foreach (bool en in new[] { false, true })
                    foreach (float r in new[] { 1f, 0.5f, 0f })
                    {
                        Enhanced = en; AutoMerge = true; MergeStartRatio = r;
                        StageVerdict v = RunOne("S6", 0f);
                        Enhanced = false; AutoMerge = false; MergeStartRatio = -1f;
                        float total = 18000f;
                        string during = LastHpBeforeMerge < 0f ? "—"
                            : $"{LastHpBeforeMerge - LastBurstDamage - (LastMergeEndHp >= 0f ? LastMergeEndHp : LastEnemyHpLeft):F0}"
                              + (LastMergeEndHp >= 0f ? "" : "(판 끝까지)");
                        sb.AppendLine($"| {(en ? "뒤" : "전")} | {r * 100f:F0}% | {Cell(v)} | "
                                      + (LastMergeAt < 0f ? "못 씀" : $"{LastMergeAt:F1}초") + $" | {LastBurstDamage:F0} | {during} | {total - LastEnemyHpLeft:F0} | {LastEnemyHpLeft:F0} |");
                    }
                sb.AppendLine();
                sb.AppendLine("## 자취 — 강화 뒤 · 게이지 100% · 합체 자동");
                TraceS6 = new StringBuilder(); TraceNext = 0f;
                Enhanced = true; AutoMerge = true; MergeStartRatio = 1f;
                RunOne("S6", 0f);
                Enhanced = false; AutoMerge = false; MergeStartRatio = -1f;
                sb.Append(TraceS6.ToString()); TraceS6 = null;
                sb.AppendLine();
                sb.AppendLine("상세(강화 뒤 · 합체 씀):");
                Enhanced = true; AutoMerge = true;
                sb.AppendLine(RunTag("S6"));
            }
            finally { SpawnHook = null; HpAll = 0f; PlantScale = 1f; DmgMult = 1f; RateMult = 1f; Enhanced = false; AggroOverride = 0f; AutoMerge = false; MergePrefill = false; MergeStartRatio = -1f; TraceS6 = null; }

            System.IO.File.WriteAllText("Docs/261016_s6_merge.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>#156 — 두 로봇 판에서 B 마운트도 드론 가득으로 시작(하네스 · 기본 거짓).</summary>
        internal static bool FillBAtStart;

        /// <summary>
        /// **#157 뒤 S5·S6 합체 재측**(2026-10-16 · 합체 중 A 탄 + B 드론 둘 다 · 강화 뒤 · 60초 · 값 0).
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunMerge157Batch</c>
        /// </summary>
        public static void RunMerge157Batch()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ #157 뒤 S5·S6 합체 재측 (2026-10-16 · 강화 뒤 · 60초 · 자동 태그 · 적이 사거리에 든 순간 합체) ############");
            sb.AppendLine("| 판 | 시작 마운트 | 게이지 | 결과 | 합체 누른 시각 | 버스트 | 합체 20초 동안 깎은 HP | 적 남은 HP / 전체 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            try
            {
                foreach (string id in new[] { "S5", "S6" })
                    foreach (bool fillB in new[] { false, true })
                        foreach (float g in new[] { 0f, 1f })
                        {
                            float pool = 0f;
                            SpawnHook = l => { pool = 0f; foreach (var e in l) pool += e.hp; return l; };
                            Enhanced = true; AutoMerge = g > 0f; MergeStartRatio = g; FillBAtStart = fillB;
                            StageVerdict v = RunOne(id, 0f);
                            Enhanced = false; AutoMerge = false; MergeStartRatio = -1f; FillBAtStart = false; SpawnHook = null;
                            string during = LastHpBeforeMerge < 0f ? "—"
                                : $"{LastHpBeforeMerge - LastBurstDamage - (LastMergeEndHp >= 0f ? LastMergeEndHp : LastEnemyHpLeft):F0}"
                                  + (LastMergeEndHp >= 0f ? "" : "(판 끝까지)");
                            sb.AppendLine($"| {id} | {(fillB ? "A 40 · B 40(#156 바로가기)" : "A 40 · B 0")} | {g * 100f:F0}% | {Cell(v)} | "
                                          + (LastMergeAt < 0f ? (g > 0f ? "못 씀(적이 사거리에 안 듦)" : "안 씀") : $"{LastMergeAt:F1}초")
                                          + $" | {LastBurstDamage:F0} | {during} | {LastEnemyHpLeft:F0} / {pool:F0} |");
                        }
            }
            finally { SpawnHook = null; HpAll = 0f; PlantScale = 1f; DmgMult = 1f; RateMult = 1f; Enhanced = false; AggroOverride = 0f; AutoMerge = false; MergeStartRatio = -1f; FillBAtStart = false; }

            System.IO.File.WriteAllText("Docs/261016_merge157.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>두 로봇 판 시작 마운트 비율(하네스 · 음수 = 종전: A 가득 · B 는 FillBAtStart).</summary>
        internal static float AStartRatio = -1f, BStartRatio = -1f;
        internal static int LastTagStrikes;
        internal static string LastStrikeLog = "";
        /// <summary>#160 — 차오름을 건다 · 합체를 이 시각 전에는 안 누른다(늦게 누른 판).</summary>
        internal static bool RushMerge;

        // ── 합체 손잡이 훑기(2026-10-17 · 하네스 덮개만) ──
        internal static float KnobRefillMult = 1f, KnobMergeSeconds;
        internal static bool KnobFreeDrones, KnobHalfSkill;

        /// <summary>④ 합체 중 대기 마운트가 절반 이상이면 있는 만큼 스킬(쿨다운 5초 그대로) — 리플렉션으로 기다리는 스킬을 놓는다.</summary>
        private static void KnobTryHalfSkill(CombatSimulation sim)
        {
            TagBattle tb = sim.Tag;
            if (tb.HasPendingSkill || tb.StandbyMount == null) return;
            float cap = tb.StandbyMount.FullCapacity;
            if (cap <= 0f || tb.StandbyMount.Total < cap * 0.5f || tb.StandbyMount.IsFull) return;   // 만충은 게임 규칙이 낸다
            if (!tb.Tag.TrySkillOnly()) return;
            var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(TagBattle).GetField("_pendingRounds", bf).SetValue(tb, tb.StandbyMount.Total);
            typeof(TagBattle).GetField("_pendingIndex", bf).SetValue(tb, tb.StandbyIndex);
            typeof(CombatSimulation).GetField("_tagSkillWait", bf).SetValue(sim, 0f);
        }

        /// <summary>
        /// **합체 손잡이 훑기** — 강화 뒤 · A 40 · B 0 진입 · 3초 차오름 · Auto 켬 · 60초. 손잡이별 승리 시각·여유·큰 스킬 시각.
        /// 배치: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunMergeKnobBatch</c>
        /// </summary>
        public static void RunMergeKnobBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ 합체 손잡이 훑기 (2026-10-17 · aa2be1b 자산 · S6 12000 · 강화 뒤 · A 40 · B 0 · 3초 차오름 · Auto 켬) ############");
            sb.AppendLine("⚠️ 「하네스 종전」 행만 합체 중 노드 산출 ×2 를 안 태운 판(10-16 까지의 하네스 표가 모두 이것). 나머지 행은 게임처럼 `BoardItemTick.Step` 에 `mergeOutputMult`(2) 를 태운다. ③ ×K 는 그 위에 더 곱한다.");
            sb.AppendLine("③ = 합체 중 B 마운트 **도착률**(보드 → 마운트 적재 속도)에 ×K. 산출(노드)·운송(벨트)은 안 건드린다.");
            sb.AppendLine();
            var knobs = new List<(string name, System.Action set)>
            {
                ("하네스 종전(합체 ×2 안 태움)", () => HarnessMergeOutput = false),
                ("기준 = 게임처럼 합체 중 노드 산출 ×2", () => { }),
                ("③ B 적재 ×2", () => KnobRefillMult = 2f),
                ("③ B 적재 ×3", () => KnobRefillMult = 3f),
                ("③ B 적재 ×4", () => KnobRefillMult = 4f),
                ("③ B 적재 ×6", () => KnobRefillMult = 6f),
                ("① 합체 중 B 드론 재고 안 줄음", () => KnobFreeDrones = true),
                ("② 합체 지속 10초", () => KnobMergeSeconds = 10f),
                ("④ 합체 중 절반 이상이면 있는 만큼", () => KnobHalfSkill = true),
                ("⑤ 보스 HP 10000", () => KnobBossHp = 10000f),
                ("⑤ 보스 HP 8000", () => KnobBossHp = 8000f),
            };
            var rateLines = new List<string>();
            try
            {
                foreach (string sid in new[] { "S6", "S5" })
                {
                    sb.AppendLine($"#### {sid}");
                    sb.AppendLine("| 손잡이 | 즉시 합체(3초) | 여유 | 큰 스킬 시각(피해) | 45초 합체 | 안 누름 |");
                    sb.AppendLine("|---|---|---|---|---|---|");
                    foreach (var k in knobs)
                    {
                        if (sid == "S5" && k.name.StartsWith("⑤")) continue;
                        string[] cells = new string[3]; string big = ""; string margin = "";
                        float[] ats = { 0f, 45f, 999f };
                        for (int i = 0; i < 3; i++)
                        {
                            HarnessMergeOutput = true; RateReset();
                            k.set();
                            if (KnobBossHp > 0f) { float h = KnobBossHp; SpawnHook = l => { var o = new List<EnemySpawn>(); foreach (var e in l) { var c = e; c.hp = h; o.Add(c); } return o; }; }
                            Enhanced = true; AutoMerge = true; RushMerge = true; MergeNotBefore = ats[i]; MergeStartRatio = 0f; AStartRatio = 1f; BStartRatio = 0f;
                            StageVerdict v = RunOne(sid, 0f);
                            cells[i] = v.result == CombatResult.Win ? $"**Win {v.seconds:F1}초**" : $"{v.result} · 남은 {LastEnemyHpLeft:F0}";
                            if (i == 0)
                            {
                                margin = v.result == CombatResult.Win ? $"{60f - v.seconds:F1}초" : "—";
                                var bigs = new List<string>();
                                foreach (string part in LastStrikeLog.Split(new[] { " / " }, System.StringSplitOptions.RemoveEmptyEntries))
                                {
                                    var m = System.Text.RegularExpressions.Regex.Match(part, @"^([\d.]+)초 · 표적 \d+ · 총 피해 (\d+)");
                                    if (m.Success) bigs.Add($"{m.Groups[1].Value}({m.Groups[2].Value})");
                                }
                                big = string.Join(" · ", bigs);
                                if (k.name.StartsWith("하네스 종전") || k.name.StartsWith("기준")) rateLines.Add($"{sid} {k.name}: {RateLine()}");
                            }
                            KnobReset();
                        }
                        sb.AppendLine($"| {k.name} | {cells[0]} | {margin} | {big} | {cells[1]} | {cells[2]} |");
                    }
                    sb.AppendLine();
                }
            }
            finally { KnobReset(); }
            sb.AppendLine("#### 마운트 도착률 — 합체 전(3초~합체) / 합체 중 (즉시 합체 판)");
            foreach (string l in rateLines) sb.AppendLine("- " + l);
            System.IO.File.WriteAllText("Docs/261017_merge_knobs.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **#164 S6 보스 HP 값 찾기** — ×2 태운 판 · 강화 뒤 · A 40 · B 0 · 3초 차오름 · Auto 켬 · 60초.
        /// 12000 부터 500 단위로 즉시 합체 / 45초 합체 / 안 누름 / 강화 전(즉시 합체).
        /// 배치: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunS6HpSearchBatch</c>
        /// </summary>
        public static void RunS6HpSearchBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ #164 S6 보스 HP 찾기 (2026-10-17 · c74c4b8 하네스 · 합체 중 노드 산출 ×2 태움 · 강화 뒤 · A 40 · B 0 · 3초 차오름 · Auto 켬 · 60초) ############");
            sb.AppendLine("| 보스 HP | 즉시 합체 | 남은 시간 | 큰 스킬(시각·피해) | 45초 합체 | 안 누름 | 강화 전 · 즉시 합체 |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            try
            {
                for (float hp = 12000f; hp <= 40000f; hp += 500f)
                {
                    string[] c = new string[4]; string big = "", left = ""; bool anyWin = false;
                    for (int i = 0; i < 4; i++)
                    {
                        float h = hp;
                        SpawnHook = l => { var o = new List<EnemySpawn>(); foreach (var e in l) { var x = e; x.hp = h; o.Add(x); } return o; };
                        HarnessMergeOutput = true; Enhanced = i != 3; AutoMerge = true; RushMerge = true;
                        MergeNotBefore = i == 1 ? 45f : i == 2 ? 999f : 0f; MergeStartRatio = 0f; AStartRatio = 1f; BStartRatio = 0f;
                        StageVerdict v = RunOne("S6", 0f);
                        bool win = v.result == CombatResult.Win;
                        c[i] = win ? $"**Win {v.seconds:F1}초**" : $"{v.result} · 남은 {LastEnemyHpLeft:F0}";
                        if (i == 0)
                        {
                            anyWin = win;
                            left = win ? $"{60f - v.seconds:F1}초" : "—";
                            big = LastStrikeLog.Replace(" · 표적 1 · 총 피해 ", "(");
                            big = System.Text.RegularExpressions.Regex.Replace(big, @" · 한 체 몫 \d+ · 소진 직전 적재\([^)]*\)", ")");
                        }
                        KnobReset();
                    }
                    sb.AppendLine($"| {hp:F0} | {c[0]} | {left} | {big} | {c[1]} | {c[2]} | {c[3]} |");
                    if (!anyWin) break;   // 즉시 합체로 못 잡으면 더 올릴 까닭이 없다
                }
            }
            finally { KnobReset(); }
            System.IO.File.WriteAllText("Docs/261017_s6_hp164.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        internal static float LastDroneDamage;

        /// <summary>
        /// **누적형 드론이 방어를 몇 번 빼나 — 한 번 재기**(2026-10-18 · 조립 기획서 점검 · 코드 0).
        /// S5(장갑 def 45) · 강화 뒤/전 · A 40 · B 40 · 합체 안 누름 · Auto 켬. 드론 총 피해와 태그 스킬 수를 적는다.
        /// 배치: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunDroneDefenseBatch</c>
        /// </summary>
        public static void RunDroneDefenseBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ 누적형 드론 × 방어 — S5(def 45) · A 40 · B 40 · 합체 안 누름 · Auto 켬 · 60초 (2026-10-18 · 1ea6b01) ############");
            sb.AppendLine("| 강화 | 결과 | 드론이 준 피해 합 | 태그 스킬 | 남은 적 HP |");
            sb.AppendLine("|---|---|---|---|---|");
            try
            {
                foreach (bool en in new[] { true, false })
                {
                    Enhanced = en; HarnessMergeOutput = true; AutoMerge = false; AStartRatio = 1f; BStartRatio = 1f; MergeStartRatio = 0f;
                    StageVerdict v = RunOne("S5", 0f);
                    sb.AppendLine($"| {(en ? "뒤" : "전")} | {Cell(v)} | {LastDroneDamage:F0} | {LastTagStrikes}회 | {LastEnemyHpLeft:F0} |");
                    KnobReset();
                }
            }
            finally { KnobReset(); }
            System.IO.File.WriteAllText("Docs/261018_drone_defense.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        internal static float KnobBossHp;
        internal static bool HarnessMergeOutput;
        internal static float RateMergeB, RateMergeA, RateMergeT, RatePreB, RatePreA, RatePreT;
        private static void RateReset() { RateMergeB = RateMergeA = RateMergeT = RatePreB = RatePreA = RatePreT = 0f; }
        private static string RateLine() =>
            $"B 드론 도착 {(RatePreT > 0f ? RatePreB / RatePreT : 0f):F2} → 합체 중 {(RateMergeT > 0f ? RateMergeB / RateMergeT : 0f):F2}/초 · "
            + $"A 탄 도착 {(RatePreT > 0f ? RatePreA / RatePreT : 0f):F2} → {(RateMergeT > 0f ? RateMergeA / RateMergeT : 0f):F2}/초";
        private static void KnobReset()
        {
            HarnessMergeOutput = false;
            KnobRefillMult = 1f; KnobMergeSeconds = 0f; KnobFreeDrones = false; KnobHalfSkill = false; KnobBossHp = 0f; SpawnHook = null;
            Enhanced = false; AutoMerge = false; RushMerge = false; MergeNotBefore = 0f; MergeStartRatio = -1f; AStartRatio = -1f; BStartRatio = -1f;
            EnhancedBOverride = -1; OverloadSecOverride = 0f; OverloadSlotMult = 1f; OverloadFireOff = false; OverloadSubstitute = false;
            OverloadFireRateOverride = 0f; OverloadConsumePerShot = 1f; OverloadProduceOverride = 0f;
            OverloadBeltMult = 0f; BeltItemFlow.SpeedMultiplier = 1f;
            DroneFractionOverride = 0f; DroneAllowanceUncap = DroneBCoefRestore = DroneSlotCapRestore = DroneLaunchOnlyWithTarget = false;
            DroneKindCap = 0; DroneKindCapEnh = 5; DroneCapOverloadDouble = false; DroneOverloadHitHalf = false;
        }
        internal static float MergeNotBefore;

        /// <summary>
        /// **#160 확인** — S6 강화 뒤/전 · A 40 · B 0(파밍 중 Auto 태그로 소진) · 게이지 갈래 · 차오름 3초 ·
        /// 합체는 적과 붙는 즉시 / 45초에. 배치: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunMergeRushBatch</c>
        /// </summary>
        public static void RunMergeRushBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ #160·#161(S6 12000)·#163(합체 중 태그 스킬) — 차오름 3초 · A 40 · B 0 · Auto 켬 · 60초 (범위 = 합체 있는 도전 층) ############");
            sb.AppendLine("| 판 | 강화 | 들고 온 게이지 | 합체 누름 | 결과 | 합체 찬/누른 시각 | 태그 스킬(시각 피해) | 남은 적 HP |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            try
            {
                foreach (string sid in new[] { "S5", "S6" })
                foreach (bool en in new[] { true, false })
                    foreach (float g in new[] { 0f, 0.5f })
                        foreach (float at in new[] { 0f, 45f, 999f })
                        {
                            Enhanced = en; AutoMerge = true; RushMerge = true; MergeNotBefore = at; MergeStartRatio = g; AStartRatio = 1f; BStartRatio = 0f;
                            StageVerdict v = RunOne(sid, 0f);
                            Enhanced = false; AutoMerge = false; RushMerge = false; MergeNotBefore = 0f; MergeStartRatio = -1f; AStartRatio = -1f; BStartRatio = -1f;
                            string log = LastStrikeLog.Replace(" · 표적 1 · 총 피해 ", " ");
                            log = System.Text.RegularExpressions.Regex.Replace(log, @" · 한 체 몫 \d+ · 소진 직전 적재\([^)]*\)", "");
                            sb.AppendLine($"| {sid} | {(en ? "뒤" : "전")} | {g * 100f:F0}% | {(at <= 0f ? "붙는 즉시" : at > 900f ? "안 누름" : $"{at:F0}초에")} | {Cell(v)} | "
                                          + (LastMergeReadyAt < 0f ? "안 참" : $"{LastMergeReadyAt:F1}초") + " / " + (LastMergeAt < 0f ? "—" : $"{LastMergeAt:F1}초")
                                          + $" | {LastTagStrikes}회: {log} | {LastEnemyHpLeft:F0} |");
                        }
            }
            finally { Enhanced = false; AutoMerge = false; RushMerge = false; MergeNotBefore = 0f; MergeStartRatio = -1f; AStartRatio = -1f; BStartRatio = -1f; }
            sb.AppendLine();
            sb.AppendLine("############ S1~S4 영향 확인 — 차오름 끔/켬 (강화 뒤 · 기본 시작) ############");
            sb.AppendLine("| 판 | 차오름 끔 | 차오름 켬 | 같은가 |");
            sb.AppendLine("|---|---|---|---|");
            try
            {
                foreach (string sid in new[] { "S1", "S2", "S3", "S4" })
                {
                    Enhanced = true; RushMerge = false; StageVerdict off = RunOne(sid, 0f); string offCell = Cell(off);
                    Enhanced = true; RushMerge = true; AutoMerge = true; StageVerdict on = RunOne(sid, 0f); string onCell = Cell(on);
                    Enhanced = false; RushMerge = false; AutoMerge = false;
                    sb.AppendLine($"| {sid} | {offCell} | {onCell} | {(offCell == onCell ? "같음" : "⚠️ 다름")} |");
                }
            }
            finally { Enhanced = false; RushMerge = false; AutoMerge = false; }

            System.IO.File.WriteAllText("Docs/261016_merge163.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **S6 자연 진행 갈림 재기**(2026-10-16 · 사용자 육안 「합체가 거의 마지막에 차서 못 잡음」 · 값 0).
        /// 시작 마운트(A·B)와 들고 온 게이지를 갈라 S6 을 돌린다 — 강화 뒤 · Auto 켬 · 합체는 차고 적이 사거리에 들면 바로.
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunS6NaturalBatch</c>
        /// </summary>
        public static void RunS6NaturalBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ S6 갈림 — 시작 마운트 × 들고 온 게이지 (2026-10-16 · 0749886 자산 · 강화 뒤 · Auto 켬 · 60초) ############");
            sb.AppendLine("| A 시작 | B 시작 | 게이지 | 결과 | 태그 스킬 수 | 합체 찬/누른 시각 | 보스 남은 HP / 13500 |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            try
            {
                foreach (float a in new[] { 1f, 0f })
                    foreach (float b in new[] { 1f, 0.5f, 0f })
                        foreach (float g in new[] { 0f, 0.5f, 1f })
                        {
                            Enhanced = true; AutoMerge = true; MergeStartRatio = g; AStartRatio = a; BStartRatio = b;
                            StageVerdict v = RunOne("S6", 0f);
                            Enhanced = false; AutoMerge = false; MergeStartRatio = -1f; AStartRatio = -1f; BStartRatio = -1f;
                            sb.AppendLine($"| {a * 40f:F0} | {b * 40f:F0} | {g * 100f:F0}% | {Cell(v)} | {LastTagStrikes} | "
                                          + (LastMergeReadyAt < 0f ? "안 참" : $"{LastMergeReadyAt:F1}초") + " / "
                                          + (LastMergeAt < 0f ? "—" : $"{LastMergeAt:F1}초") + $" | {LastEnemyHpLeft:F0} |");
                        }
            }
            finally { SpawnHook = null; HpAll = 0f; PlantScale = 1f; DmgMult = 1f; RateMult = 1f; Enhanced = false; AggroOverride = 0f; AutoMerge = false; MergeStartRatio = -1f; FillBAtStart = false; AStartRatio = -1f; BStartRatio = -1f; }

            sb.AppendLine();
            sb.AppendLine("############ 늦게 찬 합체 — A 40 · B 40 · 게이지를 갈라 합체가 차는 시각을 늦춘다 ############");
            sb.AppendLine("| 게이지 | 결과 | 합체 찬/누른 시각 | 태그 스킬(시각·피해) | 보스 남은 HP |");
            sb.AppendLine("|---|---|---|---|---|");
            try
            {
                foreach (float g in new[] { 0f, 0.9f, 0.7f, 0.5f, 0.45f, 0.4f, 0.36f })
                {
                    Enhanced = true; AutoMerge = true; MergeStartRatio = g; AStartRatio = 1f; BStartRatio = 1f;
                    StageVerdict v = RunOne("S6", 0f);
                    Enhanced = false; AutoMerge = false; MergeStartRatio = -1f; AStartRatio = -1f; BStartRatio = -1f;
                    sb.AppendLine($"| {g * 100f:F0}% | {Cell(v)} | " + (LastMergeReadyAt < 0f ? "안 참" : $"{LastMergeReadyAt:F1}초") + " / "
                                  + (LastMergeAt < 0f ? "—" : $"{LastMergeAt:F1}초") + $" | {LastTagStrikes}회: {LastStrikeLog} | {LastEnemyHpLeft:F0} |");
                }
                // 강화 안 한 판(사용자 저장이 강화 전일 수 있다)
                foreach (float g in new[] { 0f, 0.5f })
                {
                    Enhanced = false; AutoMerge = true; MergeStartRatio = g; AStartRatio = 1f; BStartRatio = 1f;
                    StageVerdict v = RunOne("S6", 0f);
                    AutoMerge = false; MergeStartRatio = -1f; AStartRatio = -1f; BStartRatio = -1f;
                    sb.AppendLine($"| 강화 전 · {g * 100f:F0}% | {Cell(v)} | " + (LastMergeReadyAt < 0f ? "안 참" : $"{LastMergeReadyAt:F1}초") + " / "
                                  + (LastMergeAt < 0f ? "—" : $"{LastMergeAt:F1}초") + $" | {LastTagStrikes}회: {LastStrikeLog} | {LastEnemyHpLeft:F0} |");
                }
            }
            finally { Enhanced = false; AutoMerge = false; MergeStartRatio = -1f; AStartRatio = -1f; BStartRatio = -1f; }

            sb.AppendLine();
            sb.AppendLine("############ 화면 재현 조건 — A 40 · B 0(파밍 중 Auto 태그가 B 를 씀) · 합체 누름 켬/끔 ############");
            sb.AppendLine("| 강화 | 게이지 | 합체 | 결과 | 합체 찬/누른 시각 | 태그 스킬(시각·피해) | 보스 남은 HP |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            try
            {
                foreach (bool en in new[] { true, false })
                    foreach (float g in new[] { 0f, 0.3f, 0.4f, 0.5f, 0.6f })
                        foreach (bool mg in new[] { true, false })
                        {
                            if (g == 0f && !mg) continue;
                            Enhanced = en; AutoMerge = mg; MergeStartRatio = g; AStartRatio = 1f; BStartRatio = 0f;
                            StageVerdict v = RunOne("S6", 0f);
                            Enhanced = false; AutoMerge = false; MergeStartRatio = -1f; AStartRatio = -1f; BStartRatio = -1f;
                            sb.AppendLine($"| {(en ? "뒤" : "전")} | {g * 100f:F0}% | {(mg ? "누름" : "안 누름")} | {Cell(v)} | " + (LastMergeReadyAt < 0f ? "안 참" : $"{LastMergeReadyAt:F1}초") + " / "
                                          + (LastMergeAt < 0f ? "—" : $"{LastMergeAt:F1}초") + $" | {LastTagStrikes}회: {LastStrikeLog} | {LastEnemyHpLeft:F0} |");
                        }
            }
            finally { Enhanced = false; AutoMerge = false; MergeStartRatio = -1f; AStartRatio = -1f; BStartRatio = -1f; }

            System.IO.File.WriteAllText("Docs/261016_s6_natural.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **#166 모듈이 실제 흐름에 든 뒤 — S1~S6 · 강화 전/뒤 · 모듈 0/1/2**(2026-10-18 · T4 대상 칸 (7,8) · 하네스 전용).
        /// 모듈 1 = 생산량(M) · 모듈 2 = 생산량 + 생산속도(M·R). 두 로봇 판(S5·S6)은 기본 시작(A 가득 · B 0) · 합체 중 ×2 태움 · 합체 안 누름.
        /// 배치: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunModule166Batch</c>
        /// </summary>
        public static void RunModule166Batch()
        {
            var m = AssetDatabase.LoadAssetAtPath<ModuleDefinition>($"{SoRoot}/Modules/Module_M.asset");
            var r = AssetDatabase.LoadAssetAtPath<ModuleDefinition>($"{SoRoot}/Modules/Module_R.asset");
            var hooks = new System.Action<BoardGrid>[]
            {
                null,
                g => g.GetAt(new Vector2Int(7, 8))?.TryAttachModule(m),
                g => { var n = g.GetAt(new Vector2Int(7, 8)); n?.TryAttachModule(m); n?.TryAttachModule(r); },
            };
            var sb = new StringBuilder();
            sb.AppendLine("############ #166 모듈 → 실제 흐름 · S1~S6 · 60초 (2026-10-18 · 하네스 · T4 대상 칸 (7,8)) ############");
            sb.AppendLine("모듈 1 = 생산량(M · 산출 ×1.5 · 재료 ×1) · 모듈 2 = M + 생산속도(R · 산출·재료 ×1.5) · 두 로봇 판 = A 가득 · B 0 · 합체 안 누름 · 합체 ×2 태움");
            sb.AppendLine("✅ #237(2026-10-20 사용자 확정) — #234 뒤(B 강화 = 출격 +2 · 🗑️ 드론 피해 ×1.45) S6 「강화 뒤 · 합체 안 누름」 Timeout = **수용**. 원인 = B ×1.45 빠짐(하네스 ㄷ 복귀만으로 Win 57.2 · Docs/261020_overload.md).");
            sb.AppendLine();
            sb.AppendLine("| 판 | 강화 | 모듈 0 | 모듈 1 (M) | 모듈 2 (M+R) |");
            sb.AppendLine("|---|---|---|---|---|");
            try
            {
                foreach (string sid in new[] { "S1", "S2", "S3", "S4", "S5", "S6" })
                    foreach (bool en in new[] { false, true })
                    {
                        var cells = new string[3];
                        for (int k = 0; k < 3; k++)
                        {
                            BoardHook = hooks[k];
                            Enhanced = en; HarnessMergeOutput = true;
                            if (sid == "S5" || sid == "S6") { AStartRatio = 1f; BStartRatio = 0f; MergeStartRatio = 0f; }
                            StageVerdict v = RunOne(sid, 0f);
                            cells[k] = Cell(v) + (v.result == CombatResult.Win ? "" : $" · 남은 적 HP {LastEnemyHpLeft:F0}");
                            BoardHook = null; KnobReset();
                        }
                        sb.AppendLine($"| {sid} | {(en ? "뒤" : "전")} | {cells[0]} | {cells[1]} | {cells[2]} |");
                    }
            }
            finally { BoardHook = null; KnobReset(); }
            System.IO.File.WriteAllText("Docs/261018_module166.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **#167·#168 폭발탄 프리셋 확인**(2026-10-18 · 하네스 · S5·S6 바로가기 판).
        /// ① 물류만 60초 — 마운트 도착 수(종류별) · 전력 공급/수요 · 프리셋이 다 섰는가.
        /// ② 전투 — S5·S6 · 프리셋 없음/있음 · 강화 뒤(즉시 합체 · 45초 합체 · 안 누름) + 강화 전(즉시 합체) · A 40 · B 0 · 3초 차오름 · 합체 ×2 태움.
        /// 배치: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunPreset167Batch</c>
        /// </summary>
        public static void RunPreset167Batch()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ #167·#168 폭발탄 프리셋 (2026-10-18 · 하네스) ############");
            bool presetOk = false;
            System.Action<BoardGrid> hook = g => presetOk = PresetBoards.ApplyExplosive(g, NodeResolver(BasePropellantNeed));

            // ① 물류만
            foreach (bool on in new[] { false, true })
            {
                BoardHook = on ? hook : null;
                BoardGrid g = Board();
                BoardHook = null;
                var flow = new BeltItemFlow();
                flow.Rebuild(g);
                for (int i = 0; i < 1200; i++) BoardItemTick.Step(g, flow, 0.05f, 1f);
                var connected = LogisticsReach.ConnectedNodes(g);
                var bal = AssetDatabase.LoadAssetAtPath<BalanceConfig>($"{SoRoot}/BalanceConfig.asset");
                WorkloadRate.Result work = WorkloadRate.Compute(g, connected, bal);
                NetworkAggregate agg = LogisticsNetwork.Aggregate(g, connected, work, 1f);
                if (on)
                {
                    foreach (var cc in new[] { new Vector2Int(6, 8), new Vector2Int(7, 8), new Vector2Int(6, 7), new Vector2Int(8, 7) })
                    {
                        NodeInstance n = g.GetAt(cc);
                        if (n == null) { sb.AppendLine($"  · {cc} 노드 없음"); continue; }
                        var ib = new StringBuilder(); foreach (var kv in n.InputBuffer) ib.Append($"{kv.Key}={kv.Value:F1} ");
                        sb.AppendLine($"  · {cc} {n.Definition.nodeId} 조합표 {n.CurrentRecipe.kind} 연결 {connected.Contains(cc)} 입력[{ib}] 출력 {n.OutputBuffer:F1}({n.BufferKind}) 정지 {n.StallReason}"
                                      + $" 포트[{string.Join(",", System.Linq.Enumerable.Select(n.Ports(), pp => pp.face + ":" + pp.io + ":" + pp.kind))}]");
                    }
                    foreach (var bc in new[] { new Vector2Int(7, 7), new Vector2Int(8, 8), new Vector2Int(8, 6), new Vector2Int(8, 5), new Vector2Int(9, 8), new Vector2Int(9, 4), new Vector2Int(2, 4), new Vector2Int(2, 5), new Vector2Int(2, 6), new Vector2Int(1, 4) })
                    {
                        BeltInstance b = g.GetBeltAt(bc);
                        sb.AppendLine($"  · 벨트 {bc} " + (b == null ? "없음" : $"{b.Element} in[{string.Join(",", b.InFaces)}] out[{string.Join(",", b.OutFaces)}]"));
                    }
                }
                sb.AppendLine($"- 물류 60초 · {(on ? "폭발탄 프리셋" : "시작 보드")}{(on ? (presetOk ? " (다 섬)" : " ⚠️ 못 선 칸 있음") : "")}: "
                              + $"마운트 도착 표준탄 {flow.MountArrivedOf(FlowKind.StandardAmmo)} · 폭발탄 {flow.MountArrivedOf(FlowKind.ExplosiveAmmo)} · 관통탄 {flow.MountArrivedOf(FlowKind.PierceAmmo)}"
                              + $" · 전력 공급 {agg.powerSupply:F1} / 수요 {agg.powerDraw:F1}");
            }
            sb.AppendLine();

            // ② 전투
            sb.AppendLine("| 판 | 보드 | 강화 뒤 · 즉시 합체 | 남은 시간 | 강화 뒤 · 45초 합체 | 강화 뒤 · 안 누름 | 강화 전 · 즉시 합체 |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            try
            {
                foreach (string sid in new[] { "S5", "S6" })
                    foreach (bool on in new[] { false, true })
                    {
                        var c = new string[4]; string left = "";
                        (bool en, float at)[] runs = { (true, 0f), (true, 45f), (true, 999f), (false, 0f) };
                        for (int k = 0; k < 4; k++)
                        {
                            BoardHook = on ? hook : null;
                            Enhanced = runs[k].en; HarnessMergeOutput = true; AutoMerge = true; RushMerge = true;
                            MergeNotBefore = runs[k].at; MergeStartRatio = 0f; AStartRatio = 1f; BStartRatio = 0f;
                            StageVerdict v = RunOne(sid, 0f);
                            c[k] = v.result == CombatResult.Win ? $"**Win {v.seconds:F1}초**" : $"{v.result} · 남은 적 HP {LastEnemyHpLeft:F0}";
                            if (k == 0) left = v.result == CombatResult.Win ? $"{60f - v.seconds:F1}초" : "—";
                            BoardHook = null; KnobReset();
                        }
                        sb.AppendLine($"| {sid} | {(on ? "폭발탄 프리셋" : "시작 보드")} | {c[0]} | {left} | {c[1]} | {c[2]} | {c[3]} |");
                    }
            }
            finally { BoardHook = null; KnobReset(); }
            System.IO.File.WriteAllText("Docs/261018_preset167.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **#205 ⑤ — S6 프리셋 즉시 합체 · 틱 폭 탐침**(2026-10-19 · 8133 화면 S6 시간 초과 가름 · 값 0).
        /// 화면 재현은 인앱 창이 숨겨진 채(배경 탭 · 프레임 조임)였다 — 프레임이 굵으면 보드 틱이 지는지 잰다.
        /// 배치: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunDtProbeBatch</c>
        /// </summary>
        public static void RunDtProbeBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ #205 ⑤ S6 프리셋 · 틱 폭 탐침 (2026-10-19 · 하네스 전용 · 자산 0) ############");
            sb.AppendLine("판 = S6 도전 · 폭발탄 프리셋 · A·B 강화 · 즉시 합체 · A 40 · B 0 · 게이지 0(RunPreset167 ① 칸과 같은 문)");
            sb.AppendLine();
            sb.AppendLine("| 틱 폭(초) | ≈ 프레임 | S6 프리셋 | S6 시작 보드 | 물류 60초 도착(프리셋 · 표준/폭발) |");
            sb.AppendLine("|---|---|---|---|---|");
            System.Action<BoardGrid> hook = g => PresetBoards.ApplyExplosive(g, NodeResolver(BasePropellantNeed));
            try
            {
                foreach (float dt in new[] { 0.05f, 0.1f, 0.2f, 0.333f })
                {
                    Dt = dt;
                    var c = new string[2];
                    for (int k = 0; k < 2; k++)
                    {
                        BoardHook = k == 0 ? hook : null;
                        Enhanced = true; HarnessMergeOutput = true; AutoMerge = true; RushMerge = true;
                        MergeNotBefore = 0f; MergeStartRatio = 0f; AStartRatio = 1f; BStartRatio = 0f;
                        StageVerdict v = RunOne("S6", 0f);
                        c[k] = v.result == CombatResult.Win ? $"**Win {v.seconds:F1}초**" : $"{v.result} · 남은 적 HP {LastEnemyHpLeft:F0}";
                        BoardHook = null; KnobReset();
                    }
                    BoardHook = hook; BoardGrid g = Board(); BoardHook = null;
                    var flow = new BeltItemFlow(); flow.Rebuild(g);
                    int n = Mathf.RoundToInt(60f / dt);
                    for (int i = 0; i < n; i++) BoardItemTick.Step(g, flow, dt, 1f);
                    sb.AppendLine($"| {dt:F3} | {1f / dt:F0} fps | {c[0]} | {c[1]} | {flow.MountArrivedOf(FlowKind.StandardAmmo)} / {flow.MountArrivedOf(FlowKind.ExplosiveAmmo)} |");
                }
            }
            finally { Dt = 0.05f; BoardHook = null; KnobReset(); }
            System.IO.File.WriteAllText("Docs/261019_dt_probe.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **#223 — 도전 진입 만재(A · B 40) 대조**(2026-10-20 · 하네스 전용 · 값 0).
        /// S4 · S5 · S6 × 시작 보드 / 폭발탄 프리셋 × 강화 뒤 × 즉시 합체 / 45초 합체 · B 0 시작(종전 표) ↔ B 가득(#223).
        /// 배치: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunFill223Batch</c>
        /// </summary>
        /// <summary>
        /// 🗑️ #231(2026-10-20) — (다) 300% 광역 꼴 폐기 · 과부하로 재측(<see cref="RunOverloadBatch"/>). #228 뒤로 (가)(나) 칸은 같아진다.
        /// **버스트 문서식 가정 표**(2026-10-20 사용자 물음 「합체 때는 태그 스킬이 아니라 버스트가 나가야」 · 하네스 전용 · 값 0).
        /// S5 · S6 × 시작 보드 / 프리셋 × B 0 / 95% / 가득 × (가) 지금 (나) 합체 중 태그 스킬 금지 (다) (나)+문서식 버스트 300%
        /// (라) (나)+문서식 버스트 500% · 1000%. 강화 둘 · A 40 · 즉시 합체(3초). 배치: RunBurstDocBatch
        /// </summary>
        public static void RunBurstDocBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ 버스트 문서식 가정 표 (2026-10-20 · 하네스 전용 · 런타임 불변) ############");
            sb.AppendLine("강화 A·B · A 40 · 게이지 0 → #160 3초 만충 · 즉시 합체 · 칸 = 결과(승 · 시각 / 남은 보스 HP) · 합체 때 나선 로봇 · 문서식 버스트 값");
            sb.AppendLine();
            sb.AppendLine("| 판 | 보드 | B 시작 | (가) 지금 | (나) 합체 중 스킬 금지 | (다) (나)+버스트 300% | (라) (나)+버스트 500% | (라) (나)+버스트 1000% |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            System.Action<BoardGrid> hook = g => PresetBoards.ApplyExplosive(g, NodeResolver(BasePropellantNeed));
            try
            {
                foreach (string sid in new[] { "S5", "S6" })
                    foreach (bool on in new[] { false, true })
                        foreach (float b in new[] { 0f, 0.95f, 1f })
                        {
                            (bool off, float pct)[] vs = { (false, 0f), (true, 0f), (true, 300f), (true, 500f), (true, 1000f) };
                            var c = new string[vs.Length];
                            for (int k = 0; k < vs.Length; k++)
                            {
                                BoardHook = on ? hook : null;
                                Enhanced = true; HarnessMergeOutput = true; AutoMerge = true; RushMerge = true;
                                MergeNotBefore = 0f; MergeStartRatio = 0f; AStartRatio = 1f; BStartRatio = b;
                                MergeSkillOff = vs[k].off; DocBurstPercent = vs[k].pct;
                                StageVerdict v = RunOne(sid, 0f);
                                c[k] = (v.result == CombatResult.Win ? $"**Win {v.seconds:F1}**" : $"Timeout · {LastEnemyHpLeft:F0}")
                                       + $" · {(LastActiveAtMerge == 0 ? "A" : LastActiveAtMerge == 1 ? "B" : "-")}"
                                       + (vs[k].pct > 0f ? $" · 버스트 {LastDocBurst:F0}" : "");
                                BoardHook = null; MergeSkillOff = false; DocBurstPercent = 0f; KnobReset();
                            }
                            sb.AppendLine($"| {sid} | {(on ? "프리셋" : "시작")} | {b * 100f:F0}% | {c[0]} | {c[1]} | {c[2]} | {c[3]} | {c[4]} |");
                        }
            }
            finally { BoardHook = null; MergeSkillOff = false; DocBurstPercent = 0f; KnobReset(); }
            System.IO.File.WriteAllText("Docs/261020_burst_doc.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **#229 과부하 표**(2026-10-20 사용자 확정 · #231 재측). S5 · S6 × 시작 보드 / 프리셋 × B 0 / 95% / 가득 ×
        /// 과부하 지속 10 / 15 / 20초. 강화 둘 · A 40 · 즉시 합체(3초) · 노드 산출 ×2.5(과부하 동안).
        /// 칸 = 결과 · 끝 시각 · 합체 중 A/B 마운트 바닥(합체 뒤 초) · 합체 끝 뒤 A/B 만재 복귀(초). 배치: RunOverloadBatch
        /// </summary>
        public static void RunOverloadBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ #229 과부하 표 (2026-10-20 · 하네스 · 발사 ×2 · 피해 ×1.8 · 산출 ×2.5 · 합체 중 태그 스킬 없음) ############");
            sb.AppendLine("강화 A·B · A 40 · 게이지 0 → #160 3초 만충 · 즉시 합체 · 칸 = 결과(승 시각 / 남은 적 HP) · 나선 로봇 · 바닥 A/B(합체 뒤 초 · — = 안 바닥) · 합체 끝 재고 A/B · 만재 복귀 A/B(합체 끝 뒤 초 · — = 판 끝까지 안 참)");
            sb.AppendLine();
            sb.AppendLine("| 판 | 보드 | B 시작 | 과부하 10초 | 과부하 15초 | 과부하 20초 |");
            sb.AppendLine("|---|---|---|---|---|---|");
            System.Action<BoardGrid> hook = g => PresetBoards.ApplyExplosive(g, NodeResolver(BasePropellantNeed));
            string T(float v) => v < 0f ? "—" : $"{v:F1}";
            try
            {
                foreach (string sid in new[] { "S5", "S6" })
                    foreach (bool on in new[] { false, true })
                        foreach (float b in new[] { 0f, 0.95f, 1f })
                        {
                            float[] secs = { 10f, 15f, 20f };
                            var c = new string[secs.Length];
                            for (int k = 0; k < secs.Length; k++)
                            {
                                BoardHook = on ? hook : null;
                                Enhanced = true; HarnessMergeOutput = true; AutoMerge = true; RushMerge = true;
                                MergeNotBefore = 0f; MergeStartRatio = 0f; AStartRatio = 1f; BStartRatio = b;
                                OverloadSecOverride = secs[k];
                                StageVerdict v = RunOne(sid, 0f);
                                c[k] = (v.result == CombatResult.Win ? $"**Win {v.seconds:F1}**" : $"Timeout · {LastEnemyHpLeft:F0}")
                                       + $" · {(LastActiveAtMerge == 0 ? "A" : LastActiveAtMerge == 1 ? "B" : "-")}"
                                       + $" · 바닥 {T(LastMountADryAt)}/{T(LastMountBDryAt)}"
                                       + $" · 끝재고 {(LastMountAAtMergeEnd < 0f ? "—" : LastMountAAtMergeEnd.ToString("F0"))}/{(LastMountBAtMergeEnd < 0f ? "—" : LastMountBAtMergeEnd.ToString("F0"))}"
                                       + $" · 복귀 {T(LastRefullA)}/{T(LastRefullB)}";
                                BoardHook = null; KnobReset();
                            }
                            sb.AppendLine($"| {sid} | {(on ? "프리셋" : "시작")} | {b * 100f:F0}% | {c[0]} | {c[1]} | {c[2]} |");
                        }
            }
            finally { BoardHook = null; KnobReset(); }
            System.IO.File.WriteAllText("Docs/261020_overload.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **#229 원인 분리 표**(2026-10-20 플랜 요청 · 하네스 손잡이만). 과부하 20초 · 조건은 <see cref="RunOverloadBatch"/> 그대로.
        /// 지금 · (ㄱ) 슬롯 ×2 · (ㄴ) 소비 ×2 끔 + 슬롯 ×2 · (ㄷ) (ㄱ) + 바닥 탄종 대체. Docs/261020_overload.md 뒤에 잇는다.
        /// </summary>
        public static void RunOverloadVariantsBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("############ #229 원인 분리 (2026-10-20 · 하네스 손잡이만 · 과부하 20초) ############");
            sb.AppendLine("칸 = 결과(승 시각 / 남은 적 HP) · 합체 중 A 바닥(합체 뒤 초 · — = 안 바닥) · 합체 중 실제 드론 발진 수(합체 끝까지 · 판이 먼저 끝나면 「판 끝」)");
            sb.AppendLine("(ㄱ) 지금 + 과부하 중 드론 슬롯 ×2(3→6) · (ㄴ) 발사 ×1(소비 ×2 끔) + 슬롯 ×2 · (ㄷ) (ㄱ) + 과부하 중 바닥 탄종을 같은 마운트 다른 탄으로 대체(가정 · 피해는 그 줄 값)");
            sb.AppendLine();
            sb.AppendLine("| 판 | 보드 | B 시작 | 지금(80b5392) | (ㄱ) 슬롯 ×2 | (ㄴ) 소비 ×1 + 슬롯 ×2 | (ㄷ) (ㄱ) + 대체 |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            System.Action<BoardGrid> hook = g => PresetBoards.ApplyExplosive(g, NodeResolver(BasePropellantNeed));
            string T(float v) => v < 0f ? "—" : $"{v:F1}";
            var wins = new int[4];
            try
            {
                foreach (string sid in new[] { "S5", "S6" })
                    foreach (bool on in new[] { false, true })
                        foreach (float b in new[] { 0f, 0.95f, 1f })
                        {
                            (float slot, bool fireOff, bool sub)[] vs = { (1f, false, false), (2f, false, false), (2f, true, false), (2f, false, true) };
                            var c = new string[vs.Length];
                            for (int k = 0; k < vs.Length; k++)
                            {
                                BoardHook = on ? hook : null;
                                Enhanced = true; HarnessMergeOutput = true; AutoMerge = true; RushMerge = true;
                                MergeNotBefore = 0f; MergeStartRatio = 0f; AStartRatio = 1f; BStartRatio = b;
                                OverloadSecOverride = 20f; OverloadSlotMult = vs[k].slot; OverloadFireOff = vs[k].fireOff; OverloadSubstitute = vs[k].sub;
                                StageVerdict v = RunOne(sid, 0f);
                                if (v.result == CombatResult.Win) wins[k]++;
                                c[k] = (v.result == CombatResult.Win ? $"**Win {v.seconds:F1}**" : $"Timeout · {LastEnemyHpLeft:F0}")
                                       + $" · A 바닥 {T(LastMountADryAt)}"
                                       + $" · 발진 {(LastMergeLaunches >= 0 ? LastMergeLaunches.ToString() : "판 끝")}";
                                BoardHook = null; KnobReset();
                            }
                            sb.AppendLine($"| {sid} | {(on ? "프리셋" : "시작")} | {b * 100f:F0}% | {c[0]} | {c[1]} | {c[2]} | {c[3]} |");
                        }
            }
            finally { BoardHook = null; KnobReset(); }
            sb.AppendLine();
            sb.AppendLine($"승 수(12칸 중): 지금 {wins[0]} · (ㄱ) {wins[1]} · (ㄴ) {wins[2]} · (ㄷ) {wins[3]}");
            System.IO.File.AppendAllText("Docs/261020_overload.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **#229 원인 분리 2**(2026-10-20 플랜 요청 · 사용자 「소비 안 늘면 공속도 안 는다」). 과부하 20초 · 슬롯 ×2 둘 다.
        /// (ㅁ) 발사 ×2 + 발당 소비 0.5(하네스 환급) · (ㅂ) 발사 ×1.5(소비 ×1.5). Docs/261020_overload.md 뒤에 잇는다.
        /// </summary>
        public static void RunOverloadVariants2Batch()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("############ #229 원인 분리 2 (2026-10-20 · 하네스 손잡이만 · 과부하 20초 · 슬롯 ×2) ############");
            sb.AppendLine("(ㅁ) 발사 ×2 + 발당 소비 0.5(틱마다 나간 탄 수 × 0.5 를 A 마운트에 환급 · 정수 단위 이월) · (ㅂ) 발사 ×1.5(소비 ×1.5)");
            sb.AppendLine("칸 = 결과(승 시각 / 남은 적 HP) · 합체 중 A 바닥(합체 뒤 초) · 합체 중 드론 발진 수");
            sb.AppendLine();
            sb.AppendLine("| 판 | 보드 | B 시작 | (ㅁ) 발사 ×2 · 소비 0.5 · 슬롯 ×2 | (ㅂ) 발사 ×1.5 · 슬롯 ×2 |");
            sb.AppendLine("|---|---|---|---|---|");
            System.Action<BoardGrid> hook = g => PresetBoards.ApplyExplosive(g, NodeResolver(BasePropellantNeed));
            string T(float v) => v < 0f ? "—" : $"{v:F1}";
            var wins = new int[2];
            try
            {
                foreach (string sid in new[] { "S5", "S6" })
                    foreach (bool on in new[] { false, true })
                        foreach (float b in new[] { 0f, 0.95f, 1f })
                        {
                            (float rate, float per)[] vs = { (0f, 0.5f), (1.5f, 1f) };
                            var c = new string[vs.Length];
                            for (int k = 0; k < vs.Length; k++)
                            {
                                BoardHook = on ? hook : null;
                                Enhanced = true; HarnessMergeOutput = true; AutoMerge = true; RushMerge = true;
                                MergeNotBefore = 0f; MergeStartRatio = 0f; AStartRatio = 1f; BStartRatio = b;
                                OverloadSecOverride = 20f; OverloadSlotMult = 2f;
                                OverloadFireRateOverride = vs[k].rate; OverloadConsumePerShot = vs[k].per;
                                StageVerdict v = RunOne(sid, 0f);
                                if (v.result == CombatResult.Win) wins[k]++;
                                c[k] = (v.result == CombatResult.Win ? $"**Win {v.seconds:F1}**" : $"Timeout · {LastEnemyHpLeft:F0}")
                                       + $" · A 바닥 {T(LastMountADryAt)}"
                                       + $" · 발진 {(LastMergeLaunches >= 0 ? LastMergeLaunches.ToString() : "판 끝")}";
                                BoardHook = null; KnobReset();
                            }
                            sb.AppendLine($"| {sid} | {(on ? "프리셋" : "시작")} | {b * 100f:F0}% | {c[0]} | {c[1]} |");
                        }
            }
            finally { BoardHook = null; KnobReset(); }
            sb.AppendLine();
            sb.AppendLine($"승 수(12칸 중 · 앞 표 포함): 지금 9 · (ㄱ) 10 · (ㄴ) 12 · (ㄷ) 10 · (ㅁ) {wins[0]} · (ㅂ) {wins[1]}");
            System.IO.File.AppendAllText("Docs/261020_overload.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **#235 산출 배율 훑기**(2026-10-20 · #233 · #234 위 · 소비 ×2 · 과부하 20초 · 강화 둘 · A 40 · 즉시 합체).
        /// 배율 2.5 / 3 / 4 / 5 × 12칸 + 연출 부담(S4~S6 파밍 · 도전 최대 필드 드론). 배치: RunOverloadProduceBatch
        /// </summary>
        public static void RunOverloadProduceBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("############ #235 산출 배율 훑기 (2026-10-20 · #233 상한 폐지 · 타격 5×20 · #234 B 강화 = 출격 +2 · 소비 ×2 · 과부하 20초) ############");
            sb.AppendLine("칸 = 결과(승 시각 / 남은 적 HP) · 합체 중 A 바닥(합체 뒤 초) · 과부하 중 A 마운트 도착률(발/초 · 판이 먼저 끝나면 그때까지) · 합체 중 발진 · 최대 필드 드론");
            sb.AppendLine();
            sb.AppendLine("| 판 | 보드 | B 시작 | 산출 ×2.5 | ×3 | ×4 | ×5 |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            System.Action<BoardGrid> hook = g => PresetBoards.ApplyExplosive(g, NodeResolver(BasePropellantNeed));
            string T(float v) => v < 0f ? "—" : $"{v:F1}";
            float[] mults = { 2.5f, 3f, 4f, 5f };
            var wins = new int[mults.Length];
            var rateSum = new float[mults.Length]; var rateN = new int[mults.Length];
            try
            {
                foreach (string sid in new[] { "S5", "S6" })
                    foreach (bool on in new[] { false, true })
                        foreach (float b in new[] { 0f, 0.95f, 1f })
                        {
                            var c = new string[mults.Length];
                            for (int k = 0; k < mults.Length; k++)
                            {
                                BoardHook = on ? hook : null;
                                Enhanced = true; HarnessMergeOutput = true; AutoMerge = true; RushMerge = true;
                                MergeNotBefore = 0f; MergeStartRatio = 0f; AStartRatio = 1f; BStartRatio = b;
                                OverloadSecOverride = 20f; OverloadProduceOverride = mults[k];
                                StageVerdict v = RunOne(sid, 0f);
                                if (v.result == CombatResult.Win) wins[k]++;
                                float rate = LastOverloadSecondsA > 0f ? LastOverloadArrivalsA / LastOverloadSecondsA : -1f;
                                if (rate >= 0f) { rateSum[k] += rate; rateN[k]++; }
                                c[k] = (v.result == CombatResult.Win ? $"**Win {v.seconds:F1}**" : $"Timeout · {LastEnemyHpLeft:F0}")
                                       + $" · A 바닥 {T(LastMountADryAt)} · 도착 {(rate < 0f ? "—" : rate.ToString("F1"))}/초"
                                       + $" · 발진 {(LastMergeLaunches >= 0 ? LastMergeLaunches.ToString() : "판 끝")} · 필드 최대 {LastMaxDronesOnField}";
                                BoardHook = null; KnobReset();
                            }
                            sb.AppendLine($"| {sid} | {(on ? "프리셋" : "시작")} | {b * 100f:F0}% | {c[0]} | {c[1]} | {c[2]} | {c[3]} |");
                        }
                sb.AppendLine();
                sb.Append("승 수(12칸 중): ");
                for (int k = 0; k < mults.Length; k++) sb.Append($"×{mults[k]:0.#} {wins[k]}{(k < mults.Length - 1 ? " · " : "")}");
                sb.AppendLine();
                sb.Append("과부하 중 A 도착률 평균(발/초): ");
                for (int k = 0; k < mults.Length; k++) sb.Append($"×{mults[k]:0.#} {(rateN[k] > 0 ? rateSum[k] / rateN[k] : 0f):F2}{(k < mults.Length - 1 ? " · " : "")}");
                sb.AppendLine();

                // 연출 부담 — S4~S6 × 파밍 / 도전 × B 강화 전 / 뒤 · 최대 필드 드론(합체 있음 · 과부하 소비 ×2 · 산출 자산값)
                sb.AppendLine();
                sb.AppendLine("연출 부담(#233 상한 폐지) — 최대 필드 드론 수(양쪽 합) · 강화 A 뒤 · B 가득 시작 · 도전 = 즉시 합체 / 파밍 = 하네스 파밍 훑기 꼴");
                sb.AppendLine("| 판 | 층 | B 강화 전 | B 강화 뒤 |");
                sb.AppendLine("|---|---|---|---|");
                foreach (string sid in new[] { "S4", "S5", "S6" })
                    foreach (bool farm in new[] { true, false })
                    {
                        var d = new string[2];
                        for (int k = 0; k < 2; k++)
                        {
                            Enhanced = true; EnhancedBOverride = k; HarnessMergeOutput = true;
                            AutoMerge = !farm; RushMerge = !farm; MergeNotBefore = 0f; MergeStartRatio = 0f; AStartRatio = 1f; BStartRatio = 1f;
                            FarmMode = farm;
                            StageVerdict v = RunOne(sid, 0f);
                            d[k] = $"{LastMaxDronesOnField} ({v.result} {v.seconds:F0}초)";
                            FarmMode = false; KnobReset();
                        }
                        sb.AppendLine($"| {sid} | {(farm ? "파밍" : "도전")} | {d[0]} | {d[1]} |");
                    }
            }
            finally { BoardHook = null; FarmMode = false; KnobReset(); }
            System.IO.File.AppendAllText("Docs/261020_overload.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **#236 상한 분리 · 지렛대 훑기**(2026-10-20 · 소비 ×2 · 과부하 20초 · 강화 둘 · A 40 · 즉시 합체 · #233 · #234 위).
        /// 생산 2.5 / 3 / 4 / 5(벨트 ×1) · 벨트 ×2(생산 2.5) · 생산 3 + 벨트 ×2. 배치: RunOverloadBeltBatch
        /// </summary>
        public static void RunOverloadBeltBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("############ #236 상한 분리 · 생산 / 벨트 지렛대 (2026-10-20 · 하네스 · 소비 ×2 · 과부하 20초 · 벨트 배율은 과부하 동안만) ############");
            sb.AppendLine("칸 = 결과 · 합체 중 A 바닥(합체 뒤 초) · 과부하 중 A 도착(발/초) · 과부하 중 A 마운트 가득 비율 · 발사 수요(A 줄 발/초 합 × 2)");
            sb.AppendLine("상한 낱말(생산 2.5 · 벨트 ×1 칸 기준): 가득 비율 ≥ 50% = 「적재 상한」 · 아니고 벨트 ×2 에서 도착 +15% 이상 = 「벨트」 · 아니면 「생산력」 · 운송 지연은 도착 **률**에는 안 걸림(늦게 시작할 뿐) → 낱말 후보에서 뺌");
            sb.AppendLine();
            (float p, float belt, string name)[] vs = { (2.5f, 1f, "생산 2.5"), (3f, 1f, "생산 3"), (4f, 1f, "생산 4"), (5f, 1f, "생산 5"), (2.5f, 2f, "벨트 ×2 · 생산 2.5"), (3f, 2f, "생산 3 · 벨트 ×2") };
            sb.Append("| 판 | 보드 | B | 상한 |");
            foreach (var v in vs) sb.Append($" {v.name} |");
            sb.AppendLine();
            sb.Append("|---|---|---|---|");
            foreach (var _ in vs) sb.Append("---|");
            sb.AppendLine();
            System.Action<BoardGrid> hook = g => PresetBoards.ApplyExplosive(g, NodeResolver(BasePropellantNeed));
            string T(float x) => x < 0f ? "—" : $"{x:F1}";
            var wins = new int[vs.Length];
            var rateSum = new float[vs.Length]; var rateN = new int[vs.Length];
            try
            {
                foreach (string sid in new[] { "S5", "S6" })
                    foreach (bool on in new[] { false, true })
                        foreach (float b in new[] { 0f, 0.95f, 1f })
                        {
                            var c = new string[vs.Length];
                            var rates = new float[vs.Length]; var fulls = new float[vs.Length];
                            for (int k = 0; k < vs.Length; k++)
                            {
                                BoardHook = on ? hook : null;
                                Enhanced = true; HarnessMergeOutput = true; AutoMerge = true; RushMerge = true;
                                MergeNotBefore = 0f; MergeStartRatio = 0f; AStartRatio = 1f; BStartRatio = b;
                                OverloadSecOverride = 20f; OverloadProduceOverride = vs[k].p; OverloadBeltMult = vs[k].belt;
                                StageVerdict v = RunOne(sid, 0f);
                                if (v.result == CombatResult.Win) wins[k]++;
                                float sec = LastOverloadSecondsA;
                                rates[k] = sec > 0f ? LastOverloadArrivalsA / sec : -1f;
                                fulls[k] = sec > 0f ? LastOverloadMountFullSec / sec : 0f;
                                if (rates[k] >= 0f) { rateSum[k] += rates[k]; rateN[k]++; }
                                c[k] = (v.result == CombatResult.Win ? $"**Win {v.seconds:F1}**" : $"Timeout · {LastEnemyHpLeft:F0}")
                                       + $" · 바닥 {T(LastMountADryAt)} · 도착 {(rates[k] < 0f ? "—" : rates[k].ToString("F1"))}"
                                       + $" · 가득 {fulls[k] * 100f:F0}% · 수요 {(LastFireDemandA < 0f ? "—" : LastFireDemandA.ToString("F1"))}";
                                BoardHook = null; KnobReset();
                            }
                            string limit = fulls[0] >= 0.5f ? "적재 상한"
                                : (rates[0] > 0f && rates[4] >= rates[0] * 1.15f) ? "벨트" : "생산력";
                            sb.Append($"| {sid} | {(on ? "프리셋" : "시작")} | {b * 100f:F0}% | {limit} |");
                            foreach (string x in c) sb.Append($" {x} |");
                            sb.AppendLine();
                        }
                sb.AppendLine();
                sb.Append("승 수(12칸 중): ");
                for (int k = 0; k < vs.Length; k++) sb.Append($"{vs[k].name} {wins[k]}{(k < vs.Length - 1 ? " · " : "")}");
                sb.AppendLine();
                sb.Append("과부하 중 A 도착 평균(발/초): ");
                for (int k = 0; k < vs.Length; k++) sb.Append($"{vs[k].name} {(rateN[k] > 0 ? rateSum[k] / rateN[k] : 0f):F2}{(k < vs.Length - 1 ? " · " : "")}");
                sb.AppendLine();
            }
            finally { BoardHook = null; KnobReset(); }
            System.IO.File.AppendAllText("Docs/261020_overload.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **#233 퇴행 원인 분리 + 드론 쌓임 ⓒ**(2026-10-20 플랜 요청 · 하네스 덮개만). 배치: RunDroneSplitBatch
        /// (1) 전투 12줄 두 로봇 판(S5 · S6 × 강화 전/뒤 · A 가득 · B 0 · 합체 안 누름 · 모듈 0) × 지금 · ㄱ · ㄴ · ㄷ · ㄹ
        /// (2) 24칸(소비 ×2 · 과부하 20초 · 강화 둘 · A 40 · 즉시 합체) × ⓐ 지금 · ⓒ 표적 있을 때만 출격(생산 2.5) · ⓒ 생산 3 + 화면 드론 최대
        /// </summary>
        public static void RunDroneSplitBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("############ #233 퇴행 원인 분리 · 드론 쌓임 ⓒ (2026-10-20 · 하네스 덮개만 · 런타임 코드 0) ############");
            sb.AppendLine("(1) 전투 12줄 두 로봇 판 — A 가득 · B 0 · 합체 안 누름 · 모듈 0 · 60초");
            sb.AppendLine("ㄱ 타격 몫 0.1(10×10) · ㄴ 허용량 이월 1기 캡 해제 · ㄷ B 강화 = 마운트계수 ×1.45 복귀 · ㄹ 동시 유지 상한(Active < Slots 3) 복귀(구 코드 꼴 = 이월 무제한 + 상한)");
            sb.AppendLine();
            sb.AppendLine("| 판 | 강화 | 91f1b8d 이전(종전 표) | 지금 | ㄱ 몫 0.1 | ㄴ 캡 해제 | ㄷ B ×1.45 | ㄹ 상한 복귀 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            var before = new System.Collections.Generic.Dictionary<string, string>
            {
                { "S5전", "Win 53.6" }, { "S5뒤", "Win 33.4" }, { "S6전", "Timeout 4883" }, { "S6뒤", "Win 54.9" },
            };
            (string n, System.Action set)[] split =
            {
                ("지금", () => { }),
                ("ㄱ", () => DroneFractionOverride = 0.1f),
                ("ㄴ", () => DroneAllowanceUncap = true),
                ("ㄷ", () => DroneBCoefRestore = true),
                ("ㄹ", () => DroneSlotCapRestore = true),
            };
            var splitWins = new int[split.Length];
            try
            {
                foreach (string sid in new[] { "S5", "S6" })
                    foreach (bool en in new[] { false, true })
                    {
                        var c = new string[split.Length];
                        for (int k = 0; k < split.Length; k++)
                        {
                            Enhanced = en; HarnessMergeOutput = true;
                            AStartRatio = 1f; BStartRatio = 0f; MergeStartRatio = 0f;
                            split[k].set();
                            StageVerdict v = RunOne(sid, 0f);
                            if (v.result == CombatResult.Win) splitWins[k]++;
                            c[k] = (v.result == CombatResult.Win ? $"**Win {v.seconds:F1}**" : $"{v.result} · 남은 적 HP {LastEnemyHpLeft:F0}") + $" · 필드 최대 {LastMaxDronesOnField}";
                            KnobReset();
                        }
                        sb.AppendLine($"| {sid} | {(en ? "뒤" : "전")} | {before[sid + (en ? "뒤" : "전")]} | {c[0]} | {c[1]} | {c[2]} | {c[3]} | {c[4]} |");
                    }
                sb.Append("승 수(4줄 중): 종전 3 · ");
                for (int k = 0; k < split.Length; k++) sb.Append($"{split[k].n} {splitWins[k]}{(k < split.Length - 1 ? " · " : "")}");
                sb.AppendLine();

                // (2) ⓒ
                sb.AppendLine();
                sb.AppendLine("(2) 드론 쌓임 변형 — ⓐ 지금 · ⓒ 사거리 안 살아 있는 적이 있을 때만 출격(없으면 그 틱 처리량 0) · 소비 ×2 · 과부하 20초 · 강화 둘 · A 40 · 즉시 합체");
                sb.AppendLine("칸 = 결과 · 합체 중 A 바닥 · 화면 드론 최대");
                sb.AppendLine();
                sb.AppendLine("| 판 | 보드 | B | ⓐ 생산 2.5 | ⓒ 생산 2.5 | ⓒ 생산 3 |");
                sb.AppendLine("|---|---|---|---|---|---|");
                System.Action<BoardGrid> hook = g => PresetBoards.ApplyExplosive(g, NodeResolver(BasePropellantNeed));
                string T(float x) => x < 0f ? "—" : $"{x:F1}";
                (bool c3, float p)[] vs = { (false, 2.5f), (true, 2.5f), (true, 3f) };
                var w = new int[vs.Length];
                var maxF = new int[vs.Length];
                foreach (string sid in new[] { "S5", "S6" })
                    foreach (bool on in new[] { false, true })
                        foreach (float b in new[] { 0f, 0.95f, 1f })
                        {
                            var c = new string[vs.Length];
                            for (int k = 0; k < vs.Length; k++)
                            {
                                BoardHook = on ? hook : null;
                                Enhanced = true; HarnessMergeOutput = true; AutoMerge = true; RushMerge = true;
                                MergeNotBefore = 0f; MergeStartRatio = 0f; AStartRatio = 1f; BStartRatio = b;
                                OverloadSecOverride = 20f; OverloadProduceOverride = vs[k].p; DroneLaunchOnlyWithTarget = vs[k].c3;
                                StageVerdict v = RunOne(sid, 0f);
                                if (v.result == CombatResult.Win) w[k]++;
                                if (LastMaxDronesOnField > maxF[k]) maxF[k] = LastMaxDronesOnField;
                                c[k] = (v.result == CombatResult.Win ? $"**Win {v.seconds:F1}**" : $"Timeout · {LastEnemyHpLeft:F0}")
                                       + $" · 바닥 {T(LastMountADryAt)} · 필드 최대 {LastMaxDronesOnField}";
                                BoardHook = null; KnobReset();
                            }
                            sb.AppendLine($"| {sid} | {(on ? "프리셋" : "시작")} | {b * 100f:F0}% | {c[0]} | {c[1]} | {c[2]} |");
                        }
                sb.AppendLine();
                sb.AppendLine($"승 수(12칸 중): ⓐ 생산 2.5 {w[0]} · ⓒ 생산 2.5 {w[1]} · ⓒ 생산 3 {w[2]} · 필드 최대(12칸 중 최고): {maxF[0]} · {maxF[1]} · {maxF[2]}");

                sb.AppendLine();
                sb.AppendLine("화면 드론 최대 — S4~S6 × 파밍 / 도전 × B 강화 전 / 뒤(표 2 꼴 · 강화 A 뒤 · B 가득 시작 · 도전 = 즉시 합체)");
                sb.AppendLine("| 판 | 층 | ⓐ B 강화 전 | ⓐ B 강화 뒤 | ⓒ B 강화 전 | ⓒ B 강화 뒤 |");
                sb.AppendLine("|---|---|---|---|---|---|");
                foreach (string sid in new[] { "S4", "S5", "S6" })
                    foreach (bool farm in new[] { true, false })
                    {
                        var d = new string[4];
                        for (int k = 0; k < 4; k++)
                        {
                            Enhanced = true; EnhancedBOverride = k % 2; HarnessMergeOutput = true;
                            AutoMerge = !farm; RushMerge = !farm; MergeNotBefore = 0f; MergeStartRatio = 0f; AStartRatio = 1f; BStartRatio = 1f;
                            FarmMode = farm; DroneLaunchOnlyWithTarget = k >= 2;
                            StageVerdict v = RunOne(sid, 0f);
                            d[k] = $"{LastMaxDronesOnField} ({v.result} {v.seconds:F0}초)";
                            FarmMode = false; KnobReset();
                        }
                        sb.AppendLine($"| {sid} | {(farm ? "파밍" : "도전")} | {d[0]} | {d[1]} | {d[2]} | {d[3]} |");
                    }
            }
            finally { BoardHook = null; FarmMode = false; KnobReset(); }
            System.IO.File.AppendAllText("Docs/261020_overload.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>**#238 · #239 최종 24칸**(자산 그대로 · 산출 3 · 벨트 ×2 · 소비 ×2 · 과부하 20초 · 강화 둘 · A 40 · 즉시 합체). 배치: RunOverloadFinalBatch</summary>
        public static void RunOverloadFinalBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("############ #238 · #239 최종 (2026-10-20 · 자산 그대로: 산출 ×3 · 벨트 ×2 · 소비 ×2 · 과부하 20초 · 드론 종류별 상한 3/강화 5 · 과부하 타격 간격 ×0.5 · 최대 유지 5초) ############");
            sb.AppendLine("| 판 | 보드 | B | 결과 |");
            sb.AppendLine("|---|---|---|---|");
            System.Action<BoardGrid> hook = g => PresetBoards.ApplyExplosive(g, NodeResolver(BasePropellantNeed));
            string T(float x) => x < 0f ? "—" : $"{x:F1}";
            int wins = 0, maxF = 0;
            try
            {
                foreach (string sid in new[] { "S5", "S6" })
                    foreach (bool on in new[] { false, true })
                        foreach (float b in new[] { 0f, 0.95f, 1f })
                        {
                            BoardHook = on ? hook : null;
                            Enhanced = true; HarnessMergeOutput = true; AutoMerge = true; RushMerge = true;
                            MergeNotBefore = 0f; MergeStartRatio = 0f; AStartRatio = 1f; BStartRatio = b;
                            StageVerdict v = RunOne(sid, 0f);
                            if (v.result == CombatResult.Win) wins++;
                            if (LastMaxDronesOnField > maxF) maxF = LastMaxDronesOnField;
                            sb.AppendLine($"| {sid} | {(on ? "프리셋" : "시작")} | {b * 100f:F0}% | "
                                + (v.result == CombatResult.Win ? $"**Win {v.seconds:F1}**" : $"Timeout · {LastEnemyHpLeft:F0}")
                                + $" · A 바닥 {T(LastMountADryAt)} · 필드 최대 {LastMaxDronesOnField} |");
                            BoardHook = null; KnobReset();
                        }
            }
            finally { BoardHook = null; KnobReset(); }
            sb.AppendLine();
            sb.AppendLine($"승 수 {wins}/12 · 화면 드론 최대 {maxF}");
            System.IO.File.AppendAllText("Docs/261020_overload.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **S5 폭발탄 줄 연결 확인**(2026-10-20 사용자 보고 「폭발탄 쪽 벨트 연결이 안 되어 있다」 · 하네스 · 값 0).
        /// 같은 시작 보드에 (가) 튜토리얼 다섯 칸만(걸음 ①~⑤ 끝 직후) (나) (가) + #216 꼬리 자동 완성(덮어쓰기 거짓)
        /// (다) 바로가기 프리셋(ApplyExplosive) → 물류 60초 · 마운트 도착 탄종별 · 꼬리 칸 상태. 배치: RunExplosiveLinkBatch
        /// ⚠️ 튜토리얼 칸은 화면 손이 아니라 같은 좌표 · 같은 면으로 직접 놓는다(가정).
        /// </summary>
        public static void RunExplosiveLinkBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ S5 폭발탄 줄 연결 (2026-10-20 · 하네스 · 시작 보드 + 칸 놓기 · 물류 60초) ############");
            void Clr(BoardGrid g, Vector2Int c) { if (g.GetAt(c) != null) g.TryRemove(c); if (g.GetBeltAt(c) != null) g.TryRemoveBelt(c); }
            System.Action<BoardGrid> steps = g =>
            {
                NodeInstance pm = g.GetAt(new Vector2Int(6, 7)); pm?.SelectRecipe(RecipeKind.PowerMaterial);
                Clr(g, new Vector2Int(7, 7)); g.TryPlaceBelt(new Vector2Int(7, 7), PortFace.West, PortFace.East, FlowKind.None, out _);
                Clr(g, new Vector2Int(8, 8)); g.TryPlaceBeltElement(new Vector2Int(8, 8), BeltElementKind.Sorter,
                    new[] { PortFace.West }, new[] { PortFace.South, PortFace.East }, FlowKind.None, out _);
                Clr(g, new Vector2Int(8, 7));
                NodeDefinition mx = NodeResolver(BasePropellantNeed)(StartingBoardB.ComplexId);
                if (mx != null && g.TryPlace(new Vector2Int(8, 7), mx, out NodeInstance x)) { x.Rotation = 1; x.SelectRecipe(RecipeKind.ExplosiveAmmo); }
            };
            int tailSkipped = 0;
            System.Action<BoardGrid> stepsTail = g =>
            {
                steps(g);
                tailSkipped = 0;
                foreach (PresetBoards.TailStep t in PresetBoards.ExplosiveTail) if (!PresetBoards.PlaceTail(g, t, overwrite: false)) tailSkipped++;
            };
            System.Action<BoardGrid> preset = g => PresetBoards.ApplyExplosive(g, NodeResolver(BasePropellantNeed));
            (string name, System.Action<BoardGrid> hook)[] cases = { ("시작 보드(대조)", null), ("(가) 튜토리얼 다섯 칸만", steps), ("(나) (가) + #216 꼬리 자동 완성", stepsTail), ("(다) 바로가기 프리셋", preset) };
            try
            {
                foreach (var cs in cases)
                {
                    BoardHook = cs.hook;
                    BoardGrid g = Board();
                    BoardHook = null;
                    var flow = new BeltItemFlow();
                    flow.Rebuild(g);
                    for (int i = 0; i < 1200; i++) BoardItemTick.Step(g, flow, 0.05f, 1f);
                    sb.AppendLine($"- {cs.name}: 마운트 도착 표준탄 {flow.MountArrivedOf(FlowKind.StandardAmmo)} · 폭발탄 {flow.MountArrivedOf(FlowKind.ExplosiveAmmo)}"
                                  + (cs.hook == stepsTail ? $" · 꼬리 건너뜀 {tailSkipped}/{PresetBoards.ExplosiveTail.Length}" : ""));
                    NodeInstance cx = g.GetAt(new Vector2Int(8, 7));
                    if (cx != null) sb.AppendLine($"    (8,7) {cx.Definition.nodeId} 돌림 {cx.Rotation} 조합표 {cx.CurrentRecipe.kind} 출력 {cx.OutputBuffer:F1} 정지 {cx.StallReason}");
                    var cells = new System.Collections.Generic.List<Vector2Int> { new Vector2Int(7, 7), new Vector2Int(8, 8), new Vector2Int(8, 6), new Vector2Int(8, 5) };
                    foreach (PresetBoards.TailStep t in PresetBoards.ExplosiveTail) cells.Add(t.cell);
                    cells.Add(new Vector2Int(2, 6)); cells.Add(new Vector2Int(1, 4));
                    var line = new StringBuilder("    ");
                    foreach (Vector2Int c in cells)
                    {
                        BeltInstance b = g.GetBeltAt(c);
                        NodeInstance nn = g.GetAt(c);
                        line.Append($"{c.x},{c.y}=" + (b != null ? $"{b.Element}[{string.Join("", System.Linq.Enumerable.Select(b.InFaces, f => f.ToString()[0]))}>{string.Join("", System.Linq.Enumerable.Select(b.OutFaces, f => f.ToString()[0]))}]" : nn != null ? nn.Definition.nodeId : "빈칸") + " ");
                    }
                    sb.AppendLine(line.ToString());
                }
            }
            finally { BoardHook = null; }
            System.IO.File.WriteAllText("Docs/261020_explosive_link.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **#240 종류별 드론 상한 표**(2026-10-20 사용자 제안 · 하네스 덮개만). 24칸(자산 그대로: 소비 ×2 · 산출 ×3 · 벨트 ×2 · 20초)
        /// × 지금(상한 없음) · (a) 상한 3/강화 5 · 과부하 출격만 ×2 · (b) (a) + 과부하 상한 ×2 · 강화 전(B 미강화 · 상한 3 · a 꼴)
        /// + 전투 12줄 두 로봇 판(S5 · S6 × 강화 전/뒤) (a) 덮개. 배치: RunDroneCapBatch
        /// </summary>
        public static void RunDroneCapBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("############ #240 종류별 드론 동시 유지 상한 (2026-10-20 · 하네스 덮개만 · 런타임 코드 0) ############");
            sb.AppendLine("덮개: 종류(누적형 · 광역형)마다 상한 3 · B 강화 5 · 처리량 3기/초 고정(강화 +2 처리량 끔) · (a) 과부하 = 출격 ×2만 · (b) 과부하 = 상한도 ×2(6/10)");
            sb.AppendLine("칸 = 결과 · A 바닥 · 화면 최대(합 / 누적 / 광역) · 적 1 남았을 때 필드 드론(판 끝 직전 값 · — = 그런 순간 없음)");
            sb.AppendLine();
            sb.AppendLine("(c) 3/5 · 출격 ×2 · 과부하 동안 드론 타격 간격 0.5 → 0.25초(누적형 · 광역형 · 수명 2.5 → 1.25초)");
            sb.AppendLine("| 판 | 보드 | B | 지금(상한 없음) | (a) 3/5 · 출격만 ×2 | (b) 3/5 · 과부하 상한 ×2 | 강화 전(B 상한 3 · a) | (c) 3/5 · 과부하 타격 ×2 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            System.Action<BoardGrid> hook = g => PresetBoards.ApplyExplosive(g, NodeResolver(BasePropellantNeed));
            string T(float x) => x < 0f ? "—" : $"{x:F1}";
            (int cap, bool dbl, int enhB, bool half)[] vs = { (0, false, -1, false), (3, false, -1, false), (3, true, -1, false), (3, false, 0, false), (3, false, -1, true) };
            var wins = new int[vs.Length]; var maxF = new int[vs.Length];
            try
            {
                foreach (string sid in new[] { "S5", "S6" })
                    foreach (bool on in new[] { false, true })
                        foreach (float b in new[] { 0f, 0.95f, 1f })
                        {
                            var c = new string[vs.Length];
                            for (int k = 0; k < vs.Length; k++)
                            {
                                BoardHook = on ? hook : null;
                                Enhanced = true; EnhancedBOverride = vs[k].enhB; HarnessMergeOutput = true; AutoMerge = true; RushMerge = true;
                                MergeNotBefore = 0f; MergeStartRatio = 0f; AStartRatio = 1f; BStartRatio = b;
                                DroneKindCap = vs[k].cap; DroneCapOverloadDouble = vs[k].dbl; DroneOverloadHitHalf = vs[k].half;
                                StageVerdict v = RunOne(sid, 0f);
                                if (v.result == CombatResult.Win) wins[k]++;
                                if (LastMaxDronesOnField > maxF[k]) maxF[k] = LastMaxDronesOnField;
                                c[k] = (v.result == CombatResult.Win ? $"**Win {v.seconds:F1}**" : $"Timeout · {LastEnemyHpLeft:F0}")
                                       + $" · 바닥 {T(LastMountADryAt)} · 최대 {LastMaxDronesOnField}/{LastMaxStack}/{LastMaxAoe}"
                                       + $" · 적1 {(LastDronesAtLastEnemy < 0 ? "—" : LastDronesAtLastEnemy.ToString())}";
                                BoardHook = null; KnobReset();
                            }
                            sb.AppendLine($"| {sid} | {(on ? "프리셋" : "시작")} | {b * 100f:F0}% | {c[0]} | {c[1]} | {c[2]} | {c[3]} | {c[4]} |");
                        }
                sb.AppendLine();
                sb.AppendLine($"승 수(12칸 중): 지금 {wins[0]} · 강화 전(3) {wins[3]} · (a) {wins[1]} · (b) {wins[2]} · (c) {wins[4]} · 화면 최대(12칸 중 최고): 지금 {maxF[0]} · 강화 전 {maxF[3]} · (a) {maxF[1]} · (b) {maxF[2]} · (c) {maxF[4]}");

                sb.AppendLine();
                sb.AppendLine("전투 12줄 두 로봇 판(A 가득 · B 0 · 합체 안 누름 · 모듈 0 · 60초) — 지금 ↔ (a) 덮개(합체 없음 = 과부하 없음이라 a = b = c)");
                sb.AppendLine("| 판 | 강화 | 지금 | (a) 상한 3/5 |");
                sb.AppendLine("|---|---|---|---|");
                foreach (string sid in new[] { "S5", "S6" })
                    foreach (bool en in new[] { false, true })
                    {
                        var d = new string[2];
                        for (int k = 0; k < 2; k++)
                        {
                            Enhanced = en; HarnessMergeOutput = true; AStartRatio = 1f; BStartRatio = 0f; MergeStartRatio = 0f;
                            DroneKindCap = k == 1 ? 3 : 0;
                            StageVerdict v = RunOne(sid, 0f);
                            d[k] = (v.result == CombatResult.Win ? $"**Win {v.seconds:F1}**" : $"{v.result} · 남은 적 HP {LastEnemyHpLeft:F0}") + $" · 최대 {LastMaxDronesOnField}";
                            KnobReset();
                        }
                        sb.AppendLine($"| {sid} | {(en ? "뒤" : "전")} | {d[0]} | {d[1]} |");
                    }
            }
            finally { BoardHook = null; KnobReset(); }
            System.IO.File.AppendAllText("Docs/261020_overload.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>태그 링 물음 — S5 시작 보드 · 강화 둘 · A 40 · B 0 · 합체 30초에 · 대기 마운트 시계열. 배치: RunStandbyRingBatch</summary>
        public static void RunStandbyRingBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ 태그 링 = 대기 마운트 Total ÷ Capacity 시계열 (2026-10-20 · 하네스 · S5 시작 보드 · 강화 둘 · A 40 · B 0 · 합체 30초) ############");
            sb.AppendLine("| 초 | 나선 | 합체 | 대기 Total | 대기 Capacity | 링(비) |");
            sb.AppendLine("|---|---|---|---|---|---|");
            try
            {
                StandbyTrace = sb;
                Enhanced = true; HarnessMergeOutput = true; AutoMerge = true; RushMerge = true;
                MergeNotBefore = 30f; MergeStartRatio = 0f; AStartRatio = 1f; BStartRatio = 0f;
                StageVerdict v = RunOne("S5", 0f);
                sb.AppendLine($"결과 {v.result} {v.seconds:F1}초 · 합체 {LastMergeAt:F1}초");
            }
            finally { StandbyTrace = null; KnobReset(); }
            System.IO.File.WriteAllText("Docs/261020_standby_ring.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// **#250 S5 점프 꼴 도전 확인**(2026-10-20) — 시작 판 + 튜토리얼 다섯 칸(걸음 자동 밟기) + #216 꼬리(덮어쓰기 거짓)
        /// ↔ 🗑️ 종전 프리셋. 점프 상태 흉내: A 강화 · B 미강화 · A 40 · B 드론 가득 · 합체 3초 차오름 즉시. 배치: RunS5JumpTutorialBatch
        /// </summary>
        public static void RunS5JumpTutorialBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("############ #250 S5 점프 꼴 — 튜토리얼판(다섯 칸 + 꼬리) ↔ 종전 프리셋 (2026-10-20 · 하네스) ############");
            void Clr(BoardGrid g, Vector2Int c) { if (g.GetAt(c) != null) g.TryRemove(c); if (g.GetBeltAt(c) != null) g.TryRemoveBelt(c); }
            System.Action<BoardGrid> tutorial = g =>
            {
                g.GetAt(new Vector2Int(6, 7))?.SelectRecipe(RecipeKind.PowerMaterial);
                Clr(g, new Vector2Int(7, 7)); g.TryPlaceBelt(new Vector2Int(7, 7), PortFace.West, PortFace.East, FlowKind.None, out _);
                Clr(g, new Vector2Int(8, 8)); g.TryPlaceBeltElement(new Vector2Int(8, 8), BeltElementKind.Sorter,
                    new[] { PortFace.West }, new[] { PortFace.South, PortFace.East }, FlowKind.None, out _);
                Clr(g, new Vector2Int(8, 7));
                NodeDefinition mx = NodeResolver(BasePropellantNeed)(StartingBoardB.ComplexId);
                if (mx != null && g.TryPlace(new Vector2Int(8, 7), mx, out NodeInstance x)) { x.Rotation = 1; x.SelectRecipe(RecipeKind.ExplosiveAmmo); }
                foreach (PresetBoards.TailStep t in PresetBoards.ExplosiveTail) PresetBoards.PlaceTail(g, t, overwrite: false);
            };
            System.Action<BoardGrid> preset = g => PresetBoards.ApplyExplosive(g, NodeResolver(BasePropellantNeed));
            try
            {
                foreach (var cs in new (string n, System.Action<BoardGrid> h)[] { ("튜토리얼판(#250)", tutorial), ("🗑️ 종전 프리셋", preset) })
                {
                    BoardHook = cs.h;
                    Enhanced = true; EnhancedBOverride = 0; HarnessMergeOutput = true; AutoMerge = true; RushMerge = true;
                    MergeNotBefore = 0f; MergeStartRatio = 0f; AStartRatio = 1f; BStartRatio = 1f;
                    StageVerdict v = RunOne("S5", 0f);
                    sb.AppendLine($"- S5 도전 · {cs.n}: " + (v.result == CombatResult.Win ? $"**Win {v.seconds:F1}초**" : $"{v.result} · 남은 적 HP {LastEnemyHpLeft:F0}")
                                  + $" · 합체 중 A 바닥 {(LastMountADryAt < 0f ? "—" : LastMountADryAt.ToString("F1"))} · 화면 드론 최대 {LastMaxDronesOnField}");
                    BoardHook = null; KnobReset();
                }
            }
            finally { BoardHook = null; KnobReset(); }
            System.IO.File.AppendAllText("Docs/261020_explosive_link.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>#223 결함 후보 가름 — S6 시작 보드 · 즉시 합체 · B 95% ↔ B 가득 합체 20초 자취. 배치: RunMergeDiagBatch</summary>
        public static void RunMergeDiagBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ #223 결함 후보 가름 — 합체 20초 자취 (S6 시작 보드 · 강화 뒤 · A 40) ############");
            try
            {
                foreach (float b in new[] { 0.95f, 1f })
                {
                    sb.AppendLine($"## B {b * 100f:F0}% 시작");
                    MergeDiag = sb;
                    Enhanced = true; HarnessMergeOutput = true; AutoMerge = true; RushMerge = true;
                    MergeNotBefore = 0f; MergeStartRatio = 0f; AStartRatio = 1f; BStartRatio = b;
                    StageVerdict v = RunOne("S6", 0f);
                    sb.AppendLine($"  → {v.result} {v.seconds:F1}초 · 합체 때 {(LastActiveAtMerge == 0 ? "A" : "B")} · 교대 {LastTagSwaps}");
                    MergeDiag = null; KnobReset();
                }
            }
            finally { MergeDiag = null; KnobReset(); }
            System.IO.File.WriteAllText("Docs/261020_merge_diag.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static void RunFill223Batch()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ #223 도전 진입 만재 대조 (2026-10-20 · 하네스 전용 · 자산 0) ############");
            sb.AppendLine("A 40 시작 · 강화 뒤 · 합체 게이지 0(#160 3초 만충 그대로) · B 0(종전) ↔ B 가득(#223)");
            sb.AppendLine();
            sb.AppendLine("칸 = 결과 · 합체 때 나선 로봇 · 교대 수 · 태그 스킬 수 · 합체 시각(#223 원인 가름)");
            sb.AppendLine("| 판 | 보드 | 즉시 · B 0(ⓐ 빈 B) | 즉시 · B 95%(ⓐ 화면 38/40) | 즉시 · B 가득(🗑️ 첫 #223) | 45초 · B 0 | 45초 · B 가득 |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            System.Action<BoardGrid> hook = g => PresetBoards.ApplyExplosive(g, NodeResolver(BasePropellantNeed));
            try
            {
                foreach (string sid in new[] { "S4", "S5", "S6" })
                    foreach (bool on in new[] { false, true })
                    {
                        var c = new string[5];
                        (float at, float b)[] runs = { (0f, 0f), (0f, 0.95f), (0f, 1f), (45f, 0f), (45f, 1f) };
                        for (int k = 0; k < 5; k++)
                        {
                            BoardHook = on ? hook : null;
                            Enhanced = true; HarnessMergeOutput = true; AutoMerge = true; RushMerge = true;
                            MergeNotBefore = runs[k].at; MergeStartRatio = 0f; AStartRatio = 1f; BStartRatio = runs[k].b;
                            StageVerdict v = RunOne(sid, 0f);
                            c[k] = (v.result == CombatResult.Win ? $"**Win {v.seconds:F1}초**" : $"{v.result} · 남은 적 HP {LastEnemyHpLeft:F0}")
                                   + $" · 합체 때 {(LastActiveAtMerge == 0 ? "A" : LastActiveAtMerge == 1 ? "B" : "-")} · 교대 {LastTagSwaps} · 스킬 {LastTagStrikes} · 합체 {(LastMergeAt >= 0f ? LastMergeAt.ToString("F1") + "초" : "-")}";
                            BoardHook = null; KnobReset();
                        }
                        sb.AppendLine($"| {sid} | {(on ? "폭발탄 프리셋" : "시작 보드")} | {c[0]} | {c[1]} | {c[2]} | {c[3]} | {c[4]} |");
                    }
            }
            finally { BoardHook = null; KnobReset(); }
            System.IO.File.WriteAllText("Docs/261020_fill223.md", sb.ToString());
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>판을 세운 직후 손대는 갈고리 — 하네스 전용(2026-10-16 · 사용자 막힘 「S2 도전을 못 넘는다」 실측).</summary>
        internal static System.Action<BoardGrid> BoardHook;

        /// <summary>
        /// **S2 도전 — 시작 보드 / +생산량 모듈 / +생산량·생산속도 모듈**(T4 대상 (7,8)) 세 갈래를 같은 문으로 잰다.
        /// 배치 실행: <c>-executeMethod MBI.EditorTools.StageClearHarness.RunS2ModuleBatch</c>
        /// </summary>
        public static void RunS2ModuleBatch()
        {
            var sb = new StringBuilder();
            sb.AppendLine("############ S2 도전 · 모듈 갈래 (2026-10-16 · 9d3a11e 자산) ############");
            var m = AssetDatabase.LoadAssetAtPath<ModuleDefinition>($"{SoRoot}/Modules/Module_M.asset");
            var r = AssetDatabase.LoadAssetAtPath<ModuleDefinition>($"{SoRoot}/Modules/Module_R.asset");
            var cases = new (string name, System.Action<BoardGrid> hook)[]
            {
                ("ⓐ 시작 보드", null),
                ("ⓑ +생산량 모듈 @(7,8)", g => g.GetAt(new Vector2Int(7, 8))?.TryAttachModule(m)),
                ("ⓒ +생산량·생산속도 @(7,8)", g => { var n = g.GetAt(new Vector2Int(7, 8)); n?.TryAttachModule(m); n?.TryAttachModule(r); }),
            };
            try
            {
                foreach (var c in cases)
                {
                    BoardHook = c.hook;
                    string report = Run("S2", preloadMount: true);
                    StageVerdict v = LastVerdict;
                    sb.AppendLine($"{c.name}: {v.result} · {v.seconds:F1}초 · 남은 적 {v.remaining}/{v.total} · 처치 {v.total - v.remaining}"
                                  + $" · HP {v.hp:F0}/{v.maxHp:F0}");
                    sb.AppendLine("---- 보고 ----");
                    sb.AppendLine(report);
                }
            }
            finally { BoardHook = null; }
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
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
            var robotA = Knob(AssetDatabase.LoadAssetAtPath<RobotDefinition>($"{SoRoot}/Robots/Robot_A.asset"));
            var robotB = Knob(AssetDatabase.LoadAssetAtPath<RobotDefinition>($"{SoRoot}/Robots/Robot_B.asset"));
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

            // 🗑️ 구 powerModel 조건 폐기(#150) — 강화 표식이 정한다(러너와 같은 문)
            float mountCoef = MountEnhanceRule.MountCoef(Enhanced, robotA.mountCoef, robotA.enhancedMountCoef);

            var setupA = new RobotSetup
            {
                hp = HpAll > 0f ? HpAll : tuning.robotHp, mountCoef = mountCoef, moduleMult = robotA.moduleMult,
                attackRange = tuning.robotAttackRangeTbd, radius = 0.5f,
                multiShotCount = tuning.multiShotCountTbd,
                aoeRadius = tuning.aoeRadiusTbd, aoeSplashFactor = tuning.aoeSplashFactorTbd,
                lines = new List<AmmoLine>(lines),
                ammoCapacity = ammoCapacity, ammoStore = new AmmoInventory(ammoCapacity),
            };

            // 로봇 B — 본체 무기가 없다(화력은 전부 드론). 러너의 `BuildRobotBSetup` 과 같은 값이다.
            var setupB = new RobotSetup
            {
                hp = HpAll > 0f ? HpAll : tuning.robotHp,
                mountCoef = DroneBCoefRestore   // ㄷ 하네스 덮개 — 구 B 강화 ×1.45
                    ? MountEnhanceRule.MountCoef(EnhancedBOverride < 0 ? Enhanced : EnhancedBOverride == 1, robotB.mountCoef, robotB.enhancedMountCoef)
                    : robotB.mountCoef,   // ✅ #234 — B 강화는 출격 +2(아래) · 🗑️ MountCoef(표식, 기본, 1.45)
                moduleMult = robotB.moduleMult,
                attackRange = tuning.robotAttackRangeTbd, radius = 0.5f,
                multiShotCount = 1, aoeRadius = 0f, aoeSplashFactor = 1f,
                lines = new List<AmmoLine>(),
                ammoCapacity = ammoCapacity, ammoStore = new AmmoInventory(ammoCapacity),
                droneSlots = bal != null ? bal.droneSlots : 3,
                droneReleaseRate = bal != null ? bal.droneReleaseRate : 1f,
                droneThroughput = bal != null ? bal.droneRelease : 0f,   // #233 · #242 — 처리량 고정(🗑️ #234 강화 +2)
                droneKindCap = bal != null ? DroneReleaseRule.KindCap(bal.droneKindCap, bal.enhanceDroneCap,
                    EnhancedBOverride < 0 ? Enhanced : EnhancedBOverride == 1) : 0,   // #242
                droneCharge = bal != null ? bal.droneCharge : 100f,
                droneDamagePerHit = (bal != null ? bal.droneCharge : 100f)
                                    * (DroneFractionOverride > 0f ? DroneFractionOverride : tuning.droneDamageFractionTbd),   // ㄱ 덮개
                droneAttackRange = tuning.robotAttackRangeTbd,
                droneAoeJudgeRadius = bal != null ? bal.droneAoeJudgeRadius : 0f,
                droneAoeDamageFactor = bal != null ? bal.droneAoeDamageFactor : 0f,
                mountStackLimit = stack,
            };

            // 튜토리얼 종료 판 — A 마운트만 표준탄으로 채운다(B 는 제 보드가 채운다).
            mountA.Load(MountItem.Standard, mountA.SlotCount * stack * (AStartRatio >= 0f ? AStartRatio : 1f));
            // #156 — 바로가기 만충 판을 흉내 낼 때 B 드론도 가득(누적형 반 · 광역형 반) · BStartRatio 면 그 몫만
            float bFill = BStartRatio >= 0f ? BStartRatio : (FillBAtStart ? 1f : 0f);
            if (bFill > 0f)
            {
                float capB = MountLoad.SlotsRobotB * stack * droneFactor * bFill;
                mountB.Load(MountItem.Drone, capB * 0.5f);
                mountB.Load(MountItem.DroneAoe, capB * 0.5f);
            }

            List<EnemySpawn> spawns = StageSpawnFactory.Build(stage, EnemyCatalog(), tuning);
            if (SpawnHook != null) spawns = SpawnHook(spawns);   // 2026-10-16 문턱 훑기(하네스 전용)
            List<StageComposition> farmRosterT = null;           // 2026-10-19 두 로봇 파밍 훑기(하네스 전용)
            if (FarmMode) { farmRosterT = FarmRoster(stage); spawns = new List<EnemySpawn>(); _farmSpawned = 0; _armorSpawned = 0; LastArmorFirstKill = -1f; }
            var sim = new CombatSimulation(setupA, setupB, mountA, mountB, spawns,
                tuning.arenaRadiusTbd, stage.challengeTime, tuning.spawnCadenceTbd);
            sim.AutoTagEnabled = true;
            // #229 과부하 — 러너와 같은 자산에서(지속만 하네스 손잡이로 덮을 수 있다).
            if (robotA.balanceRef != null)
            {
                sim.OverloadFireRate = robotA.balanceRef.overloadFireRate;
                if (sim.Merge != null) sim.Merge.OverloadSeconds = OverloadSecOverride > 0f ? OverloadSecOverride : robotA.balanceRef.overloadSec;
            }
            if (bal != null) { sim.OverloadDroneHitMult = bal.overloadDroneHitMult; sim.DroneMaxLife = bal.droneMaxLife; }   // #243 · #244 러너와 같게
            if (OverloadFireOff) sim.OverloadFireRate = 1f;   // (ㄴ) 하네스 손잡이
            if (OverloadFireRateOverride > 0f) sim.OverloadFireRate = OverloadFireRateOverride;   // (ㅂ)
            var refundCarry = new float[3];
            float droneShadowAllowance = 0f;   // ㄴ · ㄹ 그림자 허용량(이월 캡 없는 구 셈)
            bool capEnhB = EnhancedBOverride < 0 ? Enhanced : EnhancedBOverride == 1;
            float droneHitBase = sim.DroneHitInterval;
            if (DroneKindCap > 0 && bal != null) sim.SetDroneThroughput(1, bal.droneRelease);   // #240 덮개 — 강화 +2 처리량 끔
            int[] baseBaySlots = BaySlotsOf(sim);
            bool slotsRaised = false;
            int launchesAtMerge = -1;
            LastMergeLaunches = -1;
            sim.SetSideStepHold(tuning.enemySideStepHoldTbd);
            // ⚠️ **러너와 같은 자산에서 읽는다** — 안 읽으면 하네스가 종전 거동(예산 0)으로
            //    재서 화면과 다른 판을 본다(오늘 아침 `RespawnUnreachable` 과 같은 자리).
            sim.SetSideDetourCells(tuning.enemySideDetourCellsTbd);
            sim.SetSideDetourRecoverSeconds(tuning.enemySideDetourRecoverSecondsTbd);
            sim.SetEnemyPushStrength(tuning.enemyPushStrengthTbd);

            // ⚠️⚠️ **웨이브를 러너와 같게 건다**(2026-09-18 · 시안 3 ③).
            //    안 걸면 하네스는 **한 마리씩** 오는 판을, 게임은 **묶음으로** 오는 판을 돌린다 —
            //    09-15 에 「프로브는 초록인데 화면은 낡았다」로 겪은 그 모양이다.
            sim.SetWave(tuning.waveSizeTbd,
                WaveSpawnRule.Interval(tuning.waveIntervalSecondsTbd,
                                       tuning.waveSizeTbd, tuning.spawnCadenceTbd));
            sim.SpawnAllAtOnce = true;                         // 사용자 확정 #152 — 러너와 같은 문(도전 층 전원 스폰)
            sim.SetAggroRange(tuning.enemyAggroRangeTbd);      // 사용자 확정 #152 — 탐색 범위
            if (AggroOverride > 0f) sim.SetAggroRange(AggroOverride);   // 훑기 전용(하네스 · 자산 0)
            var catalogT = EnemyCatalog();
            if (FarmMode)
            {
                sim.Endless = true; sim.SuspendQueuedSpawns = true; sim.SpawnAllAtOnce = false;
                sim.SetAggroRange(FarmAggro ? tuning.enemyAggroRangeTbd : 0f);
                FarmRefill(sim, farmRosterT, catalogT, tuning);
            }
            float farmClockT = 0f, farmMinHpT = float.MaxValue;
            ArmorDmgA = ArmorDmgB = ArmorDmgSkill = ArmorDmgMerge = 0f;
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
            var tagLines = new List<AmmoLine>();   // 매 틱 A 줄 재배분용(아래 · 2026-10-16 정정)
            float allSpawnedAt = -1f;   // #151 — 다 나온 시각
            LastMergeAt = -1f; LastMergeReadyAt = -1f; LastBurstDamage = 0f;   // 2026-10-16 S6 합체 확인
            LastActiveAtMerge = -1; LastTagSwaps = 0; _mergeDiagNext = 0f; LastDocBurst = 0f;
            LastMountADryAt = LastMountBDryAt = LastMergeEndAt = LastRefullA = LastRefullB = -1f; LastMountAAtMergeEnd = LastMountBAtMergeEnd = -1f;
            LastOverloadArrivalsA = LastOverloadSecondsA = 0f; LastMaxDronesOnField = 0;
            LastOverloadMountFullSec = 0f; LastFireDemandA = -1f;
            LastMaxStack = LastMaxAoe = 0; LastDronesAtLastEnemy = -1; _standbyTraceNext = 0f;
            LastHpBeforeMerge = -1f; LastMergeEndHp = -1f;
            int peakAggro = 0;          // #152 — 한 번에 깨어 있던 최대 수
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
                // 2026-10-17 — 게임처럼 합체 중 노드 산출 ×mergeOutputMult 를 태울 수 있다(러너 `MergeSignals.OutputMultiplier` → `BoardItemTick.Step`).
                // ✅ #229 — 과부하 동안만 ×overloadProduce(2.5) · 🗑️ 합체 전체 ×mergeOutputMult(2).
                bool mmOn = sim.IsOverloaded;
                BeltItemFlow.SpeedMultiplier = mmOn
                    ? (OverloadBeltMult > 0f ? OverloadBeltMult : robotA.balanceRef != null ? Mathf.Max(1f, robotA.balanceRef.overloadBeltMult) : 1f)
                    : 1f;   // #239 — 게임과 같게(손잡이가 있으면 덮개)
                if (mmOn && mountA.IsFull) LastOverloadMountFullSec += Dt;
                float mm = HarnessMergeOutput && mmOn && robotA.balanceRef != null
                    ? Mathf.Max(1f, OverloadProduceOverride > 0f ? OverloadProduceOverride : robotA.balanceRef.overloadProduce) : 1f;
                BoardItemTick.Step(gridA, flowA, Dt, throttleA.Scale, mm);
                if (mmOn) { LastOverloadArrivalsA += flowA.PendingMountArrivals.Count; LastOverloadSecondsA += Dt; }   // #235 과부하 중 A 도착
                deliveryA.Observe(flowA.PendingMountArrivals, DamageOf, Dt, MountOwner.RobotA);
                flowA.ClearPendingMountArrivals();
                deliveryA.TryDrain(DeliverySampleSeconds, out _);
                // 2026-10-18 — **A 탄종별 도착률을 사격 배분에 싣는다**(러너 `LogisticsOutputProvider` 와 같다 · 단일 판 `Run` 은 이미 함).
                //    종전 두 로봇 판은 안 실어 배분이 탄종 도착을 못 봤다 — #167 폭발탄 프리셋이 전투에 안 잡히던 자리.
                for (int k = 0; k < SupplySignals.MountArrivalRate.Length; k++)
                    SupplySignals.MountArrivalRate[k] = deliveryA.RateOf((AmmoKind)k);

                BoardItemTick.Step(gridB, flowB, Dt, throttleB.Scale, mm);
                deliveryB.Observe(flowB.PendingMountArrivals, DamageOf, Dt, MountOwner.RobotB);
                flowB.ClearPendingMountArrivals();
                deliveryB.TryDrain(DeliverySampleSeconds, out _);
                arrivedStack += deliveryB.StackDroneRate * Dt;
                arrivedAoe += deliveryB.AoeDroneRate * Dt;
                // 합체 전/중 마운트 도착률 대조(2026-10-17 · 플랜 물음 「×2 가 실제 흐름에 곱해지나」)
                if (mmOn) { RateMergeB += (deliveryB.StackDroneRate + deliveryB.AoeDroneRate) * Dt; RateMergeA += deliveryA.Rate * Dt; RateMergeT += Dt; }
                else if (LastMergeAt < 0f && elapsed >= 3f) { RatePreB += (deliveryB.StackDroneRate + deliveryB.AoeDroneRate) * Dt; RatePreA += deliveryA.Rate * Dt; RatePreT += Dt; }

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

                // ⚠️⚠️ **A 발사 줄을 매 틱 다시 낸다**(2026-10-16 · 하네스 결함 정정). 종전 RunTag 는 판을 세울 때
                //    **마운트가 실리기 전** 한 번만 배분해 A 의 줄이 비어 있었다 — 두 로봇 판(S5·S6)에서 A 가 **한 발도
                //    안 쐈다**(S6 자취: 보스가 사거리 8 에 28초 서 있는데 HP 그대로). 러너는 매 프레임 `RefreshFireRate` 로
                //    다시 낸다 — 같은 문을 지난다. B 가 나가 있을 때는 건드리지 않는다(B 는 본체 무기가 없다).
                if (sim.ActiveRobotIndex == 0)
                {
                    // ⚠️ 재고는 **나선 마운트에서 바로** 읽는다 — RunTag 는 러너처럼 슬롯 신호(`PublishMount`)를 안 실어
                    //    `SupplySignals.MountStockOf` 가 늘 0 이었다(이것이 A 무발사의 뿌리). 러너가 싣는 값과 같다.
                    MountLoad am = sim.ActiveMount;
                    ShotAllocator.AllocateRates(robotA.weapons, robotA.consumptionCap,
                        SupplySignals.ArrivalRateOf, k => am != null ? am.AmountOf(MountItemMap.From(k)) : 0f, tagLines);
                    sim.SetFireLines(tagLines);
                }
                // 2026-10-16 사용자 지시 「S6 태그·합체를 써도 시간 초과인가」 — 합체가 차는 즉시 누른다(하네스 전용).
                if (MergePrefill && elapsed <= 0f && sim.Merge != null) sim.Merge.FillGaugeAlmost();   // 가정 판 — 게이지가 1초 앞에서 시작
                // #155 — 파밍에서 이어 온 게이지로 들어간 판(러너 `Begin` 과 같은 문 · SetCharge)
                if (MergeStartRatio >= 0f && elapsed <= 0f && sim.Merge != null)
                    sim.Merge.SetCharge(MergeStartRatio * MergeSystem.GaugeFullSeconds);
                // #160 — S6 도전 층 차오름(러너 `TickMergeRush` 와 같은 식)
                if (RushMerge && sim.Merge != null && !sim.Merge.UsedThisStage && !sim.Merge.IsActive && elapsed <= MergeRushRule.RushSeconds + Dt)
                    sim.Merge.SetCharge(MergeRushRule.ChargeAt(Mathf.Max(0f, MergeStartRatio) * MergeSystem.GaugeFullSeconds, elapsed, sim.Merge.ChargeSeconds));
                if (sim.Merge != null && sim.Merge.IsReady && LastMergeReadyAt < 0f) LastMergeReadyAt = elapsed;
                bool engaged = false;   // 적이 로봇 사거리 안에 든 뒤에 누른다 — 밖에서 누르면 버스트가 허공이다
                foreach (CombatEntity en in sim.Enemies)
                    if (en.IsAlive && (en.position - sim.Robot.position).magnitude <= tuning.robotAttackRangeTbd + en.radius)
                    { engaged = true; break; }
                float enemyHp = 0f;
                foreach (CombatEntity en in sim.Enemies) if (en.IsAlive) enemyHp += en.hp;
                bool wasMerging = sim.Merge != null && sim.Merge.IsActive;
                if (AutoMerge && engaged && elapsed >= MergeNotBefore && LastMergeAt < 0f && sim.Merge != null && sim.Merge.IsReady && sim.TryMerge())
                {
                    LastMergeAt = elapsed;
                    LastHpBeforeMerge = enemyHp;
                    LastActiveAtMerge = sim.ActiveRobotIndex;   // #223 가름
                    {   // #236 — 발사 수요 = A 줄 발/초 합 × 과부하 배율(합체 순간 배분)
                        var sideA = SidesOf(sim)[0];
                        var setupNow = (RobotSetup)SideField(sideA, "setup");
                        float dem = 0f;
                        if (setupNow.lines != null) foreach (AmmoLine l in setupNow.lines) dem += Mathf.Max(0f, l.shotsPerSec);
                        LastFireDemandA = dem * Mathf.Max(1f, sim.OverloadFireRate);
                    }
                    // 가정 손잡이(하네스 전용) — 합체 중 태그 스킬 금지 · 문서식 버스트
                    if (MergeSkillOff && sim.Tag != null) { sim.Tag.SkillWhileLocked = false; sim.Tag.CancelPendingSkill(); }
                    if (DocBurstPercent > 0f)
                    {
                        float aPower = RobotOutput.Nominal(robotA.weapons, setupA.mountCoef, robotA.moduleMult);
                        float bPower = (deliveryB.StackDroneRate + deliveryB.AoeDroneRate) * setupB.droneCharge * setupB.mountCoef;
                        float burst = (aPower + bPower) * DocBurstPercent / 100f;
                        LastDocBurst = burst;
                        foreach (CombatEntity en in sim.Enemies)
                            if (en.IsAlive && (en.position - sim.Robot.position).magnitude <= tuning.robotAttackRangeTbd + en.radius)
                            { en.hp -= burst; en.aggro = true; }
                    }
                    _mergeDiagFired0 = sim.FiredOf(AmmoKind.Standard) + sim.FiredOf(AmmoKind.Explosive) + sim.FiredOf(AmmoKind.Pierce);
                    _mergeDiagNext = elapsed;
                }
                if (TraceS6 != null && elapsed >= TraceNext)
                {
                    TraceNext += 2.5f;
                    foreach (CombatEntity en in sim.Enemies)
                        if (en.IsAlive)
                            TraceS6.AppendLine($"    {elapsed,5:F1}초 · 활성 {(sim.ActiveRobotIndex == 0 ? "A" : "B")} · 로봇({sim.Robot.position.x:F1},{sim.Robot.position.y:F1}) · 보스({en.position.x:F1},{en.position.y:F1}) r{en.radius:F2} · 거리 {(en.position - sim.Robot.position).magnitude:F2} · 깸 {en.aggro} · 보스 HP {en.hp:F0} · 합체 {(sim.Merge != null && sim.Merge.IsActive ? "중" : "-")}");
                }
                int strikesBefore = sim.TagSkillStrikes;
                if (wasMerging && LastMergeEndHp < 0f && sim.Merge != null && !sim.Merge.IsActive) LastMergeEndHp = enemyHp;
                // #229 과부하 자취 — 합체 중 마운트 바닥 시각 · 합체 끝 재고 · 끝난 뒤 만재 복귀까지 초.
                if (LastMergeAt >= 0f && sim.Merge != null)
                {
                    if (sim.Merge.IsActive)
                    {
                        if (LastMountADryAt < 0f && mountA.Total < 1f) LastMountADryAt = elapsed - LastMergeAt;
                        if (LastMountBDryAt < 0f && mountB.Total < 1f) LastMountBDryAt = elapsed - LastMergeAt;
                    }
                    else
                    {
                        if (LastMergeEndAt < 0f) { LastMergeEndAt = elapsed; LastMountAAtMergeEnd = mountA.Total; LastMountBAtMergeEnd = mountB.Total; }
                        if (LastRefullA < 0f && mountA.IsFull) LastRefullA = elapsed - LastMergeEndAt;
                        if (LastRefullB < 0f && mountB.IsFull) LastRefullB = elapsed - LastMergeEndAt;
                    }
                }
                float bStack = mountB.AmountOf(MountItem.Drone);
                float bAoe = mountB.AmountOf(MountItem.DroneAoe);


                // ── 합체 손잡이 훑기(2026-10-17 · 하네스 덮개만 · 게임 코드 0) ──
                bool knobMerging = sim.Merge != null && sim.Merge.IsActive;
                float knobS0 = mountB.AmountOf(MountItem.Drone), knobA0 = mountB.AmountOf(MountItem.DroneAoe);
                if (knobMerging && KnobHalfSkill && sim.Tag != null) KnobTryHalfSkill(sim);

                float armorHpBefore = 0f;
                if (FarmMode) foreach (CombatEntity e in sim.Enemies) if (e.IsAlive && e.label == "장갑") armorHpBefore += e.hp;
                bool aActiveTick = sim.ActiveRobotIndex == 0;
                bool mergingTick = sim.Merge != null && sim.Merge.IsActive;
                // #229 원인 분리 손잡이(하네스 전용)
                if (OverloadSlotMult != 1f && sim.IsOverloaded != slotsRaised)
                {
                    slotsRaised = sim.IsOverloaded;
                    SetBaySlots(sim, baseBaySlots, slotsRaised ? OverloadSlotMult : 1f);
                }
                if (OverloadSubstitute && sim.IsOverloaded) SubstituteDryLines(sim);
                if (mergingTick && launchesAtMerge < 0) launchesAtMerge = DroneSpawnedOf(sim);
                if (!mergingTick && launchesAtMerge >= 0 && LastMergeLaunches < 0) LastMergeLaunches = DroneSpawnedOf(sim) - launchesAtMerge;
                bool refundTick = OverloadConsumePerShot < 1f && sim.IsOverloaded;
                // ── 드론 덮개(하네스) ──
                DroneBay bayB = SidesOf(sim).Count > 1 ? SideField(SidesOf(sim)[1], "bay") as DroneBay : null;
                float bayBaseT = bayB != null ? bayB.Throughput : 0f;
                bool bayTicked = bayB != null && ((sim.Merge != null && sim.Merge.IsActive) || sim.ActiveRobotIndex == 1);
                float bayDtEff = Dt * (sim.IsOverloaded ? Mathf.Max(1f, sim.OverloadFireRate) : 1f);
                int spawnedPre = DroneSpawnedOf(sim);
                bool shadowOn = bayB != null && (DroneAllowanceUncap || DroneSlotCapRestore);
                if (shadowOn && bayTicked)
                {
                    float set = droneShadowAllowance;
                    if (DroneSlotCapRestore) set = Mathf.Min(set, bayB.Slots - bayB.Active - bayBaseT * bayDtEff);
                    typeof(DroneBay).GetField("_allowance", NonPub).SetValue(bayB, set);
                }
                bool cutLaunch = false;
                float heldStack = 0f, heldAoe = 0f;
                if (DroneOverloadHitHalf && sim.IsOverloaded) sim.DroneHitInterval = droneHitBase * 0.5f;   // (c)
                // ⚠️ 사출대가 실제로 도는 틱(B 나감 · 합체)에만 — A 나간 동안 B 재고를 빼면 「대기 마운트 만충」이 안 서서 자동 태그가 막힌다(첫 판 실측 · 12줄 드론 0).
                if (DroneKindCap > 0 && SidesOf(sim).Count > 1 && bayTicked)
                {
                    var dl = SideField(SidesOf(sim)[1], "drones") as System.Collections.Generic.List<DroneUnit>;
                    int ns = 0, na = 0;
                    if (dl != null) foreach (DroneUnit du in dl) { if (du.Kind == DroneKind.Aoe) na++; else ns++; }
                    int cap = capEnhB ? DroneKindCapEnh : DroneKindCap;
                    if (DroneCapOverloadDouble && sim.IsOverloaded) cap *= 2;
                    float roomS = Mathf.Max(0, cap - ns), roomA = Mathf.Max(0, cap - na);
                    float hs = mountB.AmountOf(MountItem.Drone) - roomS, ha = mountB.AmountOf(MountItem.DroneAoe) - roomA;
                    if (hs > 0f && mountB.TryConsume(MountItem.Drone, hs)) heldStack = hs;
                    if (ha > 0f && mountB.TryConsume(MountItem.DroneAoe, ha)) heldAoe = ha;
                }
                if (DroneLaunchOnlyWithTarget && bayB != null)
                {
                    bool any = false;
                    foreach (CombatEntity en in sim.Enemies)
                        if (en.IsAlive && (en.position - sim.Robot.position).magnitude <= tuning.robotAttackRangeTbd + en.radius) { any = true; break; }
                    if (!any) { sim.SetDroneThroughput(1, 0f); cutLaunch = true; }
                }
                int fP0 = sim.FiredOf(AmmoKind.Pierce), fS0 = sim.FiredOf(AmmoKind.Standard), fE0 = sim.FiredOf(AmmoKind.Explosive);
                sim.Tick(Dt);
                if (cutLaunch) sim.SetDroneThroughput(1, bayBaseT);   // ⓒ 되돌림
                if (DroneOverloadHitHalf) sim.DroneHitInterval = droneHitBase;   // (c) 되돌림
                if (heldStack > 0f) mountB.Load(MountItem.Drone, heldStack);   // #240 되돌림
                if (heldAoe > 0f) mountB.Load(MountItem.DroneAoe, heldAoe);
                {
                    var dl = SidesOf(sim).Count > 1 ? SideField(SidesOf(sim)[1], "drones") as System.Collections.Generic.List<DroneUnit> : null;
                    int ns = 0, na = 0;
                    if (dl != null) foreach (DroneUnit du in dl) { if (du.Kind == DroneKind.Aoe) na++; else ns++; }
                    if (ns > LastMaxStack) LastMaxStack = ns;
                    if (na > LastMaxAoe) LastMaxAoe = na;
                    int alive = 0; foreach (CombatEntity en in sim.Enemies) if (en.IsAlive) alive++;
                    if (alive == 1) LastDronesAtLastEnemy = sim.DronesOnField;
                }
                if (shadowOn && bayTicked)
                    droneShadowAllowance += bayBaseT * bayDtEff - (DroneSpawnedOf(sim) - spawnedPre);
                if (sim.DronesOnField > LastMaxDronesOnField) LastMaxDronesOnField = sim.DronesOnField;   // #233 연출 부담
                if (StandbyTrace != null && sim.Tag != null && elapsed >= _standbyTraceNext)
                {
                    _standbyTraceNext = elapsed + 1f;
                    MountLoad sb0 = sim.Tag.StandbyMount;
                    if (sb0 != null)
                        StandbyTrace.AppendLine($"| {elapsed:F0} | {(sim.ActiveRobotIndex == 0 ? "A" : "B")} | {(sim.Merge != null && sim.Merge.IsActive ? "합체" : "-")} | {sb0.Total:F1} | {sb0.Capacity:F0} | {(sb0.Capacity > 0f ? sb0.Total / sb0.Capacity : 0f):F2} |");
                }
                // (ㅁ) 발당 소비 0.5 — 이번 틱에 나간 **탄 수**(FiredOf 는 쪼갠 발을 세므로 ShotsPerRound 로 나눔)의 (1 − 비)만큼 A 마운트에 되돌린다.
                if (refundTick)
                {
                    int split = ShotSplitRule.ShotsPerRound(sim.ShotsPerRound);
                    int[] d = { sim.FiredOf(AmmoKind.Pierce) - fP0, sim.FiredOf(AmmoKind.Standard) - fS0, sim.FiredOf(AmmoKind.Explosive) - fE0 };
                    for (int q = 0; q < 3; q++)
                    {
                        refundCarry[q] += d[q] / (float)split * (1f - OverloadConsumePerShot);
                        if (refundCarry[q] >= 1f) { float whole = Mathf.Floor(refundCarry[q]); mountA.Load(AmmoItems[q], whole); refundCarry[q] -= whole; }
                    }
                }
                elapsed += Dt;
                if (FarmMode)
                {
                    float armorHpAfter = 0f;
                    foreach (CombatEntity e in sim.Enemies) if (e.label == "장갑" && e.IsAlive) armorHpAfter += e.hp;
                    // 그 틱에 죽은 장갑은 남은 hp 를 모른다 — 죽기 직전 hp 를 앞 합에서 빼지 않으면 피해가 남는다(빼 둔다 · 어림).
                    float lost = Mathf.Max(0f, armorHpBefore - armorHpAfter);
                    if (sim.TagSkillResolvedThisTick) ArmorDmgSkill += lost;
                    else if (mergingTick) ArmorDmgMerge += lost;
                    else if (aActiveTick) ArmorDmgA += lost; else ArmorDmgB += lost;
                    farmClockT += Dt;
                    if (farmClockT >= stage.spawnInterval) { farmClockT = 0f; FarmRefill(sim, farmRosterT, catalogT, tuning); }
                    if (sim.Robot != null && sim.Robot.hp < farmMinHpT) farmMinHpT = sim.Robot.hp;
                    if (AutoMerge && sim.Merge != null && sim.Merge.IsReady) sim.TryMerge();
                    if (elapsed >= FarmSeconds) break;
                }

                if (knobMerging)
                {
                    if (KnobRefillMult > 1f)   // ③ 합체 중 B 마운트 적재 속도 ×K(보드가 마운트에 닿게 하는 도착률에 곱함)
                    {
                        mountB.Load(MountItem.Drone, (KnobRefillMult - 1f) * deliveryB.StackDroneRate * Dt);
                        mountB.Load(MountItem.DroneAoe, (KnobRefillMult - 1f) * deliveryB.AoeDroneRate * Dt);
                    }
                    if (KnobFreeDrones)        // ① 합체 중 B 드론이 나가도 재고는 안 준다
                    {
                        float dS = mountB.AmountOf(MountItem.Drone) - knobS0, dA = mountB.AmountOf(MountItem.DroneAoe) - knobA0;
                        if (dS < 0f) mountB.Load(MountItem.Drone, -dS);
                        if (dA < 0f) mountB.Load(MountItem.DroneAoe, -dA);
                    }
                    if (KnobMergeSeconds > 0f && LastMergeAt >= 0f && elapsed - LastMergeAt >= KnobMergeSeconds && sim.Merge.IsActive)
                        typeof(MergeSystem).GetField("_remaining", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                            .SetValue(sim.Merge, 0.0001f);   // ② 지속을 줄인다 — 다음 틱에 끝나고 잠금이 풀린다
                }

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
                if (MergeDiag != null && LastMergeAt >= 0f && elapsed - LastMergeAt <= 20f + Dt && elapsed >= _mergeDiagNext)
                {
                    _mergeDiagNext += 2f;
                    float aRate = 0f; int aLines = setupA.lines != null ? setupA.lines.Count : 0;
                    if (setupA.lines != null) foreach (AmmoLine l in setupA.lines) aRate += l.shotsPerSec;
                    int fired = sim.FiredOf(AmmoKind.Standard) + sim.FiredOf(AmmoKind.Explosive) + sim.FiredOf(AmmoKind.Pierce) - _mergeDiagFired0;
                    MergeDiag.AppendLine($"    {elapsed,5:F1}초 · 나선 {(sim.ActiveRobotIndex == 0 ? "A" : "B")} · 합체 {(sim.Merge != null && sim.Merge.IsActive ? "중" : "-")}"
                        + $" · A 줄 {aLines}개 Σ{aRate:F2}발/초 · A 마운트 {(mountA != null ? mountA.Total : 0f):F0}"
                        + $" · 합체 뒤 실제 발사 {fired} · B 드론 재고 {(mountB != null ? mountB.AmountOf(MountItem.Drone) + mountB.AmountOf(MountItem.DroneAoe) : 0f):F0}"
                        + $" · 보스 HP {enemyHp:F0}");
                }
                if (allSpawnedAt < 0f && sim.Unspawned == 0) allSpawnedAt = elapsed;   // #151 열
                { int awake = 0; foreach (CombatEntity e in sim.Enemies) if (e.IsAlive && e.aggro) awake++; if (awake > peakAggro) peakAggro = awake; }   // #152 열
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
            LastTagSwaps = tagSwaps;   // #223 가름
            LastBurstDamage = 0f;   // 🗑️ #229 — 순간 버스트 없음
            LastTagStrikes = sim.TagSkillStrikes;
            LastDroneDamage = sim.DroneDamageDealt;
            LastStrikeLog = string.Join(" / ", strikeLog).Replace("    ", "");
            LastEnemyHpLeft = 0f;
            foreach (CombatEntity e in sim.Enemies) if (e.IsAlive) LastEnemyHpLeft += e.hp;
            if (FarmMode)
            {
                LastFarmMinHp = farmMinHpT == float.MaxValue ? sim.Robot.hp : Mathf.Max(0f, farmMinHpT);
                int aliveT = 0, armorAliveT = 0;
                foreach (CombatEntity e in sim.Enemies) if (e.IsAlive) { aliveT++; if (e.label == "장갑") armorAliveT++; }
                LastFarmKills = _farmSpawned - aliveT; LastArmorKills = _armorSpawned - armorAliveT;
            }
            LastVerdict = new StageVerdict
            {
                stageId = stageId, how = "두 로봇",
                ok = sim.Result == CombatResult.Win, result = sim.Result, seconds = elapsed,
                remaining = sim.Remaining, total = sim.TotalEnemies,
                hp = sim.Robot.hp, maxHp = sim.Robot.maxHp,
                unspawned = sim.Unspawned,
                plannedLastSpawn = WaveSpawnRule.SpawnTime(sim.TotalEnemies - 1, sim.WaveSize, sim.WaveInterval),
                allSpawnedAt = allSpawnedAt,
                peakAggro = peakAggro,
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

            // ══ 회피 축 (2026-09-30 · 사용자 물음 「회피는 발동하였는가?」) ══════════
            //
            // ⚠️⚠️ **이 판에는 이 열이 없었다.** 한 로봇 판(`Run`)에만 있었고, 두 로봇 판은
            //    「회피 스택 최고」만 찍었다. 그런데 **스택 최고는 발동 여부가 아니다** —
            //    가득 찬 순간이 있었다는 뜻일 뿐이라, 한 번도 안 쓰고 8 이 찍힐 수 있다.
            //    `autoPilotHoldWhenMoreThanTbd` 를 0 으로 바꾼 뒤 S5 가 29 초 빨리 죽었는데,
            //    그 까닭을 가르려면 **발동 회수**가 있어야 한다.
            //
            // 📌 **값은 하나도 안 건드렸다** — 하네스가 세는 열만 늘렸다.
            sb.AppendLine();
            sb.AppendLine("[회피 축 — 두 로봇 판]  ← 측정법: 한 로봇 판(`Run`)과 같은 값을 읽는다");
            sb.AppendLine($"    **자동 회피 발동 {sim.Dodge.TotalDodges} 회**"
                          + "  ← 측정법: `DodgeSystem.TotalDodges`(발동할 때마다 1 증가) 를 판 끝에서 읽는다");
            sb.AppendLine($"    **피격 {sim.HitsTaken} 회**(회피로 무효가 된 것 포함)"
                          + "  ← 측정법: 근접·포탄이 **명중 판정에 들어온** 순간마다 1 증가");
            sb.AppendLine($"    **받은 총 피해 {sim.DamageTaken:F0}**"
                          + "  ← 측정법: 실제로 **HP 에서** 깎인 값의 합");
            sb.AppendLine($"    **회피로 무효화한 피해 {sim.DamageAvoided:F0}**"
                          + "  ← 측정법: 무적이라 **계산에 들어가지도 않은** 공격력의 합");
            sb.AppendLine($"    **쉴드가 막은 피해 {sim.ShieldAbsorbed:F0}**"
                          + "  ← 측정법: 피격마다 게이지에서 실제로 빠진 양의 합");
            sb.AppendLine("    ⚠️ **스택 최고 8 과 발동 0 회는 같이 설 수 있다** — 그릇은 찼는데"
                          + " 쓸 일이 없었거나, 쓸 수 없었다는 뜻이다. 둘은 다른 물음이다.");

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
