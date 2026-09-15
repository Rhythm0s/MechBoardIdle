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
    /// 【조사】 적이 로봇을 안 쫓는다 (2026-09-15 · 사용자 육안 ⑤).
    ///
    /// 증상 — 게임 시작 직후 Mob_infantry 가 로봇을 안 쫓고, 가까이 와도 공격하지 않는다.
    /// 화면에서는 적이 **한쪽에 대각선 일렬로 뭉쳐** 있고 로봇은 반대쪽에 있었다.
    ///
    /// ⚠️ **값만 낸다. 고치지 않는다.**
    ///
    /// 가설 셋을 갈라서 잰다 —
    /// ⓐ **서로 막는다** — `GridMovement.IsBlocked` 가 적끼리도 본다. 「막히면 멈춘다」라
    ///    같은 방향에서 나온 뒤엣것이 앞엣것에 막혀 줄이 선다.
    /// ⓑ **스폰이 한쪽에 몰린다** — 각도가 균등하지 않으면 처음부터 뭉쳐 나온다.
    /// ⓒ **좌표계가 어긋난다** — 카메라 추적(09-15)이 들어간 뒤라 화면과 월드가 갈렸을 수 있다.
    ///    시뮬은 화면을 모르므로, 시뮬 안에서 거리가 줄면 ⓒ 는 시뮬 밖 문제다.
    ///
    /// 배치 실행: <c>-executeMethod MBI.EditorTools.EnemyChaseProbe.RunBatch</c>
    /// </summary>
    public static class EnemyChaseProbe
    {
        private const float Dt = 0.05f;
        private const float Seconds = 30f;

        [MenuItem("MBI/Probe Enemy Chase")]
        public static void RunMenu() => Debug.Log(Run());

        public static void RunBatch()
        {
            Debug.Log(Run());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 적 추격 실측 (2026-09-15 · 육안 5) ===");

            CombatTuning tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>(
                "Assets/_Project/ScriptableObjects/CombatTuning.asset");
            EnemyDefinition inf = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                "Assets/_Project/ScriptableObjects/Enemies/Enemy_infantry.asset");
            if (tuning == null || inf == null)
            {
                sb.AppendLine("  (자산 없음)");
                return sb.ToString();
            }

            sb.AppendLine();
            sb.AppendLine("[0] 보병 스펙 (자산 그대로)");
            // ⚠️ hp·def·반경은 **정의가 아니라 스테이지 칸**에서 온다(StageRunner.BuildSpawns).
            // 여기서는 S1 의 첫 칸을 그대로 읽는다 — 값을 지어내지 않는다.
            StageDefinition s1 = AssetDatabase.LoadAssetAtPath<StageDefinition>(
                "Assets/_Project/ScriptableObjects/Stages/Stage_S1.asset");
            float cellHp = 30f, cellDef = 0f;
            if (s1 != null && s1.composition != null && s1.composition.Count > 0)
            {
                cellHp = s1.composition[0].hp;
                cellDef = s1.composition[0].def;
            }

            sb.AppendLine("  이동 " + inf.moveSpeed.ToString("F2") + " · 사거리 "
                + inf.attackRange.ToString("F2") + " · 간격 " + inf.attackInterval.ToString("F2")
                + " · 투사체 " + inf.projectileSpeed.ToString("F2"));
            sb.AppendLine("  S1 첫 칸 — hp " + cellHp.ToString("F0")
                + " · def " + cellDef.ToString("F0"));
            sb.AppendLine("  스폰 띠 " + tuning.spawnRingMinTbd.ToString("F1")
                + " ~ " + tuning.spawnRingMaxTbd.ToString("F1")
                + " · cadence " + tuning.spawnCadenceTbd.ToString("F2"));

            Trace(sb, tuning, inf, cellHp, cellDef, "[1] 로봇이 가만히 있을 때", false);
            Trace(sb, tuning, inf, cellHp, cellDef,
                "[2] 로봇이 걸을 때 (자동 조종 흉내 — 초당 한 칸 오른쪽)", true);
            return sb.ToString();
        }

        private static void Trace(StringBuilder sb, CombatTuning tuning, EnemyDefinition inf,
            float cellHp, float cellDef, string title, bool robotWalks)
        {
            sb.AppendLine();
            sb.AppendLine(title);

            var setup = new RobotSetup
            {
                hp = 99999f,                 // 조사 중에 죽으면 표가 끊긴다
                mountCoef = 1f,
                moduleMult = 1f,
                attackRange = tuning.robotAttackRangeTbd,
                radius = 0.5f,
                lines = new List<AmmoLine>(),   // 안 쏜다 — 이 프로브는 **적의 이동**만 본다
                ammoCapacity = 40f,
            };

            var spawns = new List<EnemySpawn>();
            for (int i = 0; i < 20; i++)
                spawns.Add(new EnemySpawn
                {
                    label = inf.displayName,
                    hp = cellHp, def = cellDef, atk = inf.atk,
                    moveSpeed = inf.moveSpeed > 0f ? inf.moveSpeed : tuning.enemyMoveSpeedTbd,
                    attackRange = inf.attackRange > 0f ? inf.attackRange : tuning.enemyAttackRangeTbd,
                    attackInterval = inf.attackInterval > 0f
                        ? inf.attackInterval : tuning.enemyAttackIntervalTbd,
                    radius = 0.5f,   // StageRunner 는 EnemySize(hp)*0.5 — 뷰 값이라 여기선 고정
                    projectileSpeed = inf.projectileSpeed,
                });

            var sim = new CombatSimulation(setup, spawns, 999f, 999f,
                tuning.spawnCadenceTbd > 0f ? tuning.spawnCadenceTbd : 0.8f);
            if (tuning.spawnRingMinTbd > 0f && tuning.spawnRingMaxTbd > 0f)
                sim.SetSpawnBand(tuning.spawnRingMinTbd, tuning.spawnRingMaxTbd);

            int steps = Mathf.RoundToInt(Seconds / Dt);
            float nextReport = 5f;
            var lastPos = new Dictionary<CombatEntity, Vector2>();
            int stuckTicks = 0, movedTicks = 0;

            for (int i = 0; i < steps; i++)
            {
                // 로봇을 걷게 한다 — 러너의 자동 조종이 하는 일을 흉내 낸다.
                if (robotWalks && sim.Robot != null)
                    sim.Robot.position += new Vector2(1f, 0f) * Dt;

                foreach (CombatEntity e in sim.Enemies)
                    if (e.hp > 0f) lastPos[e] = e.position;

                sim.Tick(Dt);

                foreach (CombatEntity e in sim.Enemies)
                {
                    if (e.hp <= 0f || !lastPos.TryGetValue(e, out Vector2 was)) continue;
                    if ((e.position - was).sqrMagnitude > 1e-8f) movedTicks++;
                    else stuckTicks++;
                }

                float t = (i + 1) * Dt;
                if (t + 0.0001f >= nextReport)
                {
                    Report(sb, sim, t);
                    nextReport += 5f;
                }
            }

            sb.AppendLine("  움직인 적-틱 " + movedTicks + " · 안 움직인 적-틱 " + stuckTicks
                + "  (안 움직인 것에는 **사거리에 닿아 때리는 중**도 든다)");
        }

        private static void Report(StringBuilder sb, CombatSimulation sim, float t)
        {
            Vector2 robot = sim.Robot != null ? sim.Robot.position : Vector2.zero;
            int alive = 0;
            float near = float.MaxValue, far = 0f, sum = 0f;
            int inRange = 0;

            foreach (CombatEntity e in sim.Enemies)
            {
                if (e.hp <= 0f) continue;
                alive++;
                float d = Vector2.Distance(robot, e.position);
                if (d < near) near = d;
                if (d > far) far = d;
                sum += d;
                // ⚠️ **시뮬과 같은 규칙으로 센다** — 사거리는 **표면 사이 거리**다
                // (2026-09-15 · 육안 ⑤). 중심 거리로 세면 표가 늘 0 을 찍는다.
                float reach = e.attackRange + (sim.Robot != null ? sim.Robot.radius : 0f) + e.radius;
                if (d <= reach) inRange++;
            }

            sb.AppendLine("  t=" + t.ToString("F0") + "s  살아있는 적 " + alive
                + " · 최근접 " + (alive == 0 ? "—" : near.ToString("F1"))
                + " · 평균 " + (alive == 0 ? "—" : (sum / alive).ToString("F1"))
                + " · 최원 " + (alive == 0 ? "—" : far.ToString("F1"))
                + " · 사거리 안 " + inRange
                + " · 로봇 HP " + (sim.Robot != null ? sim.Robot.hp.ToString("F0") : "—"));
        }
    }
}
