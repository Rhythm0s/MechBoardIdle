using System.Collections.Generic;
using System.IO;
using MBI.Data;
using UnityEditor;
using UnityEngine;

namespace MBI.Editor
{
    /// <summary>
    /// balance_v4.json(1차 계약)을 읽어 전투 데이터 SO를 생성/갱신한다:
    /// RobotDefinition(로봇A) 1종 · EnemyDefinition 4종 · StageDefinition 6종(S1~S6).
    /// CombatTuning(TBD)은 LoadOrCreate만 하고 값은 덮어쓰지 않는다(인스펙터 조정 유지, §3).
    /// 메뉴: MBI/Generate Combat Data (Robot+Enemy+Stage).
    ///
    /// - 수치 원천은 json(§9). 코드에 밸런스 리터럴을 두지 않는다(§3).
    /// - 무기 발사율 = 물류 생산율 pA(대표 상태, mock): 관통1/분열1/폭발2 → 출력 ΣpA×dA = 145 = s3Break.
    ///   무기 기계 최대치가 아니라 물류 산출(핵심 명제 = 물류가 제약). 실 물류 시뮬 완성 시 동적 산출로 교체.
    /// - 재실행 시 같은 경로 자산을 덮어써 GUID 보존(참조 안정).
    /// </summary>
    public static class CombatAssetGenerator
    {
        private const string SoRoot = "Assets/_Project/ScriptableObjects";
        private const string RobotsDir = SoRoot + "/Robots";
        private const string EnemiesDir = SoRoot + "/Enemies";
        private const string StagesDir = SoRoot + "/Stages";
        private const string ConfigPath = SoRoot + "/BalanceConfig.asset";
        private const string TuningPath = SoRoot + "/CombatTuning.asset";

        [MenuItem("MBI/Generate Combat Data (Robot+Enemy+Stage)")]
        public static void Generate()
        {
            BalanceJson json = BalanceJsonLoader.Load();

            EnsureDir(SoRoot);
            EnsureDir(RobotsDir);
            EnsureDir(EnemiesDir);
            EnsureDir(StagesDir);

            BalanceConfig config = AssetDatabase.LoadAssetAtPath<BalanceConfig>(ConfigPath);
            if (config == null)
                Debug.LogWarning($"[MBI] BalanceConfig 없음({ConfigPath}) — 먼저 'MBI/Generate Balance + Nodes' 권장. RobotDefinition.balanceRef=null로 진행.");

            BuildRobot(json, config);
            int enemies = BuildEnemies(json);
            int stages = BuildStages(json);
            LoadOrCreate<CombatTuning>(TuningPath); // TBD placeholder — 값 덮어쓰지 않음

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MBI] 전투 데이터 생성 완료 — 원천 {json.meta.schemaVersion} ({json.meta.exportedAt}). " +
                      $"Robot 1 · Enemy {enemies} · Stage {stages} · CombatTuning(TBD, LoadOrCreate).");
        }

        // ---- 로봇A: 탄약 스펙트럼(발당피해 dA + 물류 생산율 pA·mock) + 판정식 계수 ----
        private static void BuildRobot(BalanceJson json, BalanceConfig config)
        {
            float capA = json.Param("capA");             // 6 소비 상한
            float enh = json.Param("enh");               // 1.45 강화 마운트계수
            float moduleMult = json.Param("moduleMult"); // 1.0 모듈배율

            // shotsPerSec = 물류 생산 발사율(대표 상태 pA, mock). 무기 기계 최대치 아님(§물류 제약).
            // 출력 = Σ pA×dA = 1×20 + 1×25 + 2×50 = 145 = s3Break(§9). 벨트/시뮬 완성 시 동적 산출로 교체.
            var weapons = new List<WeaponSpec>
            {
                new WeaponSpec(AmmoKind.Pierce, json.Param("dA0"), json.Param("pA0")),    // 20 × 1
                new WeaponSpec(AmmoKind.Split, json.Param("dA1"), json.Param("pA1")),     // 25 × 1
                new WeaponSpec(AmmoKind.Explosive, json.Param("dA2"), json.Param("pA2")), // 50 × 2
            };

            RobotDefinition r = LoadOrCreate<RobotDefinition>($"{RobotsDir}/Robot_A.asset");
            r.robotId = "robotA";
            r.displayName = "로봇A";
            r.weapons = weapons;
            r.consumptionCap = capA;
            r.mountCoef = 1f;             // 물류 상태(강화 전) = 항등 1.0 (밸런스 수치 아님)
            r.enhancedMountCoef = enh;    // S4+ 강화 = 1.45
            r.moduleMult = moduleMult;    // 1.0
            r.balanceRef = config;
            r.sprite = LoadArt("robot_a");
            EditorUtility.SetDirty(r);

            // 로봇 B — 드론 운용기(밸런스 params pB/dB). 전투 등장은 MVP 이후지만
            // 아트가 들어왔으므로 SO 자리를 만들어 둔다. 무기 스펙은 A와 축이 달라 비워 둔다.
            RobotDefinition b = LoadOrCreate<RobotDefinition>($"{RobotsDir}/Robot_B.asset");
            b.robotId = "robotB";
            b.displayName = "로봇B";
            b.mountCoef = 1f;
            b.enhancedMountCoef = enh;
            b.moduleMult = moduleMult;
            b.balanceRef = config;
            b.sprite = LoadArt("robot_b");
            b.droneSprite = LoadArt("drone_n"); // 누적형 = 기본 프리셋(params pB 1.0 × dB 100)
            EditorUtility.SetDirty(b);
        }

        // Art/Units에서 스프라이트를 읽는다. 없으면 null — 뷰가 플레이스홀더로 폴백한다.
        // 경로가 여기 한 곳에만 있고 런타임 코드에는 SO 참조만 남는다(§8 명명 규칙).
        private static Sprite LoadArt(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Art/Units/{fileName}.png");
        }

        // ---- 적 4종: atk 카탈로그(hp/def는 스테이지) ----
        private static int BuildEnemies(BalanceJson json)
        {
            if (json.enemies == null) return 0;
            int n = 0;
            foreach (EnemyEntry e in json.enemies)
            {
                if (e == null || string.IsNullOrEmpty(e.key)) continue;
                EnemyDefinition d = LoadOrCreate<EnemyDefinition>($"{EnemiesDir}/Enemy_{e.key}.asset");
                d.enemyKey = e.key;
                d.displayName = string.IsNullOrEmpty(e.label) ? e.key : e.label;
                d.role = ToRole(e.key);
                d.atk = e.atk;
                d.atkConfirmed = e.confirmed;
                EditorUtility.SetDirty(d);
                n++;
            }
            return n;
        }

        // ---- 스테이지 6종: 요구치·모델·구성 ----
        private static int BuildStages(BalanceJson json)
        {
            if (json.stages == null) return 0;
            int n = 0;
            foreach (StageEntry s in json.stages)
            {
                if (s == null || string.IsNullOrEmpty(s.id)) continue;
                StageDefinition d = LoadOrCreate<StageDefinition>($"{StagesDir}/Stage_{s.id}.asset");
                d.stageId = s.id;
                d.topic = s.topic;
                d.reqType = ToReqType(s.reqType);
                d.req = s.req;
                d.reqBand = (s.reqBand != null && s.reqBand.Length >= 2)
                    ? new Vector2(s.reqBand[0], s.reqBand[1]) : Vector2.zero;
                d.powerModel = ToPowerModel(s.powerModel);
                d.challengeTime = s.challengeTime;
                d.enhMaterialReward = s.enhMaterialReward;
                d.bossHp = s.bossHp;

                // 상주 파밍 정원·간격(둘 다 TBD 0). 0이면 FarmSpawner가 돌지 않는다 —
                // 미확정 상태를 기본값으로 덮어 감추지 않는다.
                d.spawnCap = s.spawnCap;
                d.spawnInterval = s.spawnInterval;
                d.spawnConfirmed = s.spawnConfirmed;

                var comp = new List<StageComposition>();
                if (s.composition != null)
                {
                    foreach (CompEntry c in s.composition)
                    {
                        if (c == null) continue;
                        comp.Add(new StageComposition
                        {
                            enemyKey = c.enemy,
                            count = c.count,
                            hp = c.hp,
                            def = c.def,
                        });
                    }
                }
                d.composition = comp;
                EditorUtility.SetDirty(d);
                n++;
            }
            return n;
        }

        // ---- 매핑 ----
        private static EnemyRole ToRole(string key)
        {
            switch (key)
            {
                case "infantry": return EnemyRole.Infantry;
                case "artillery": return EnemyRole.Artillery;
                case "armor": return EnemyRole.Armor;
                case "boss": return EnemyRole.Boss;
                default:
                    Debug.LogWarning($"[MBI] 미지 enemy key '{key}' → Infantry로 폴백(§7 확인).");
                    return EnemyRole.Infantry;
            }
        }

        private static StageReqType ToReqType(string s)
        {
            switch (s)
            {
                case "fixed": return StageReqType.Fixed;
                case "band": return StageReqType.Band;
                case "formula": return StageReqType.Formula;
                case "budget": return StageReqType.Budget;
                default:
                    Debug.LogWarning($"[MBI] 미지 reqType '{s}' → Fixed 폴백(§7).");
                    return StageReqType.Fixed;
            }
        }

        private static StagePowerModel ToPowerModel(string s)
        {
            switch (s)
            {
                case "logistics": return StagePowerModel.Logistics;
                case "enhanced": return StagePowerModel.Enhanced;
                case "tag": return StagePowerModel.Tag;
                case "burst": return StagePowerModel.Burst;
                default:
                    Debug.LogWarning($"[MBI] 미지 powerModel '{s}' → Logistics 폴백(§7).");
                    return StagePowerModel.Logistics;
            }
        }

        // ---- 유틸 (BalanceAssetGenerator와 동일 패턴) ----
        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static void EnsureDir(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string leaf = Path.GetFileName(assetPath);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
