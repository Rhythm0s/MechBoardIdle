using System.Collections.Generic;
using System.IO;
using MBI.Core.Audio;
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
    /// - 무기 발사율 = 물류 생산율 pA(대표 상태, mock): 관통1/표준1/폭발2 → 출력 ΣpA×dA = 145.
    ///   ⚠️ 구 주석의 「= s3Break」는 폐기 — 그 앵커가 없어졌고(260910_W02 2-2), 이 145 도
    ///   표준탄 좌표 개정(09-09) 전의 mock 값이라 지금 대표 조합 140 과 다르다.
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
            // 애니메이션 목표 초의 소스. 값을 덮어쓰지 않고 읽기만 한다.
            CombatTuning tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>(TuningPath);

            // ⚠️ 값은 안 덮어쓰되 **파일에는 적히게 한다.** 새 필드는 클래스 초기값으로만 살아 있고
            // 에셋 파일에는 안 들어가는데, 그러면 「값이 어디에 사는가」에 답이 코드가 된다 —
            // 지침 §3 「수치 하드코딩 금지」가 막으려던 바로 그 자리다. 더티만 찍어 직렬화를 부른다.
            // 탄환 한 발을 SO 자리에 걸어 둔다 — 런타임은 경로를 모른다(§8).
            // 아직 없으면 null 이고 뷰가 자리표시로 폴백한다.
            if (tuning != null) tuning.tagBulletSprite = LoadVfx("vfx_tagbullet");

            // 설치된 VFX 넷 (2026-09-08 · 260908_W06 6장). 셋은 배선됐고
            // `vfx_ammoout`은 **사건 자리가 코드에 없어** 자리만 걸어 둔다 — 지어 넣지 않는다.
            if (tuning != null)
            {
                tuning.droneLaunchSprite = LoadVfx("vfx_dronelaunch");
                tuning.boosterSprite     = LoadVfx("vfx_booster");
                tuning.droneExpireSprite = LoadVfx("vfx_droneexpire");
                tuning.ammoOutSprite     = LoadVfx("vfx_ammoout");
            }

            // 전투 배경 둘 (2026-09-09 배선). 보스 배경은 **S6에서만** 쓰인다 —
            // 고르는 일은 러너가 하고 여기서는 둘 다 걸어 두기만 한다.
            if (tuning != null)
            {
                tuning.combatBackgroundSprite = LoadBackground("bg_combat");
                tuning.bossBackgroundSprite   = LoadBackground("bg_combat_boss");
            }

            if (tuning != null) EditorUtility.SetDirty(tuning);

            // 소리 값 묶음 — 곡 둘은 이미 리포에 있고 효과음 여덟은 아직 없다.
            BuildAudioConfig();
            BuildPortfolioLinks(); // 메인 메뉴 주소 — 자리만 만들고 값은 사용자가 넣는다

            float capA = json.Param("capA");             // 6 소비 상한
            float enh = json.Param("enh");               // 1.45 강화 마운트계수
            float moduleMult = json.Param("moduleMult"); // 1.0 모듈배율

            // shotsPerSec = 물류 생산 발사율(대표 상태 pA, mock). 무기 기계 최대치 아님(§물류 제약).
            // 출력 = Σ pA×dA = 1×20 + 1×25 + 2×50 = 145 (mock). 벨트/시뮬 완성 시 동적 산출로 교체.
            // ⚠️ 「= s3Break」 표기는 폐기 — 앵커가 없어졌다(260910_W02 2-2).
            var weapons = new List<WeaponSpec>
            {
                new WeaponSpec(AmmoKind.Pierce, json.Param("dA0"), json.Param("pA0")),    // 20 × 1
                new WeaponSpec(AmmoKind.Standard, json.Param("dA1"), json.Param("pA1")),     // 25 × 1
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
            r.animClips = LoadAnimClips("robot_a", tuning);
            // 꼬리 칸 3 — **문서가 확정한 값**이다(로봇 A 아트 요청 문서 5-1 · 규칙은 캐릭터 15 7-7).
            r.tagEntryTrailCells = 3;
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
            b.animClips = LoadAnimClips("robot_b", tuning);
            // ⚠️ **꼬리 칸 3은 가정이다**(2026-09-09 · `260908_W09` 2-3). 로봇 B 문서(15-2) 5장은
            // 이 값을 **미정**으로 신설했고, 「화면을 보고 고른다」가 정해진 방식이다.
            // A의 3을 그대로 둔 것은 **되돌릴 수 있는 출발점**이며 재서 나온 값이 아니다 —
            // B는 아래로 눌리는 반동이라 A보다 길어야 할 수도 있다. 화면을 본 뒤 여기에 역기입한다.
            b.tagEntryTrailCells = 3;
            EditorUtility.SetDirty(b);

            // 합체체 — 260907_W01 3-1 이 HUD 묶음보다 앞으로 올렸다. 촬영 C구간 25초가 전부
            // 합체이고, 00:40~00:55 는 합체체가 화면에 서 있어야 하는 구간이다. 자산은 이미
            // 있었고 배선만 없었다. 무기 스펙은 A·B 의 것을 쓰므로 여기서는 그림만 갖는다.
            RobotDefinition f = LoadOrCreate<RobotDefinition>($"{RobotsDir}/Robot_Fusion.asset");
            f.robotId = "fusion";
            f.displayName = "합체체";
            f.mountCoef = 1f;
            f.enhancedMountCoef = enh;
            f.moduleMult = moduleMult;
            f.balanceRef = config;
            f.sprite = LoadArt("robot_fusion_256");
            f.animClips = LoadAnimClips("fusion", tuning);
            EditorUtility.SetDirty(f);
        }

        // Art/Units에서 스프라이트를 읽는다. 없으면 null — 뷰가 플레이스홀더로 폴백한다.
        // 경로가 여기 한 곳에만 있고 런타임 코드에는 SO 참조만 남는다(§8 명명 규칙).
        private static Sprite LoadArt(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Art/Units/{fileName}.png");
        }

        // ── 소리 (2026-09-09 배선) ────────────────────────────────────────────────
        //
        // ⚠️ **경로가 사는 곳은 여기 하나다**(§8). `Assets/_Project/Audio/`는 아트 세션
        // 소유라 그 아래 파일을 만들거나 고치지 않고 **읽기만 한다**(소유 표 §20-1).

        public const string AudioConfigPath = SoRoot + "/AudioConfig.asset";
        public const string PortfolioLinksPath = SoRoot + "/PortfolioLinks.asset";

        /// <summary>
        /// 메인 메뉴가 여는 주소 묶음의 **자리만** 만든다 (플랜 §66-10).
        ///
        /// ⚠️ **값을 하나도 안 넣는다.** 주소는 사용자가 주는 값이고 아직 안 왔다.
        /// 자리표시 주소를 지어 넣으면 심사자가 그것을 눌러 엉뚱한 곳으로 간다 —
        /// 빈 주소의 버튼은 화면에서 **비활성**이라 눌리지 않는다.
        ///
        /// ⚠️ <c>LoadOrCreate</c> 라 **이미 있으면 그대로 둔다.** 사용자가 인스펙터에
        /// 넣은 주소가 다음 생성에서 날아가면 안 된다.
        /// </summary>
        public static PortfolioLinks BuildPortfolioLinks()
        {
            PortfolioLinks links = LoadOrCreate<PortfolioLinks>(PortfolioLinksPath);
            EditorUtility.SetDirty(links);
            return links;
        }

        /// <summary>
        /// 소리 값 묶음을 만들고 **자산 자리만** 다시 채운다.
        ///
        /// ⚠️ **값은 안 덮는다.** 겹침 상한과 볼륨은 화면에서 듣고 고치는 값이라
        /// (사운드 문서 9장 S-1·S-2) 생성기가 재실행될 때마다 되돌리면 **귀로 고른 값이
        /// 조용히 사라진다.** 여기서 채우는 것은 클립 참조뿐이다.
        /// </summary>
        public static AudioConfig BuildAudioConfig()
        {
            AudioConfig audio = LoadOrCreate<AudioConfig>(AudioConfigPath);

            audio.musicBattle = LoadBgm("bgm_battle");
            audio.musicBoss = LoadBgm("bgm_boss");

            // 효과음 여덟 — **순서는 `SoundIds.All`과 같다.** 없는 것은 null로 남고
            // 재생기가 조용히 건너뛴다(자리표시 소리 금지).
            var clips = new AudioClip[SoundIds.All.Length];
            for (int i = 0; i < SoundIds.All.Length; i++) clips[i] = LoadSfx(SoundIds.All[i]);
            audio.sfxClips = clips;

            EditorUtility.SetDirty(audio);
            return audio;
        }

        private static AudioClip LoadBgm(string fileName)
            => AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Project/Audio/bgm/{fileName}.ogg");

        /// <summary>
        /// 효과음. ⚠️ **아직 하나도 없다**(소리 자산 대장 2장 — 사용자가 무료 자산으로 조달 중).
        /// 형식은 대장이 `.wav`로 적어 두었다.
        /// </summary>
        private static AudioClip LoadSfx(string fileName)
            => AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Project/Audio/sfx/{fileName}.wav");

        /// <summary>
        /// 배경은 `Art/Backgrounds` 아래에 산다 (2026-09-09 배선).
        ///
        /// ⚠️ **`internal`인 이유** — 배경 셋이 SO 둘에 나뉘어 걸린다. 전투 배경 둘은
        /// <see cref="CombatTuning"/>이 들고 보드 배경 하나는 `BoardArtSet`이 드는데,
        /// 그 둘의 생성기가 다르다. **경로 문자열을 양쪽에 하나씩 두면 폴더를 옮길 때
        /// 한쪽만 고치게 된다** — 지침 §7 ［09-07］「한 값이 두 곳에 살면 답이 둘이 된다」.
        /// 그래서 읽는 함수는 하나이고 부르는 곳이 둘이다.
        /// </summary>
        internal static Sprite LoadBackground(string fileName)
            => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Art/Backgrounds/{fileName}.png");

        // 이펙트는 Art/VFX 아래에 산다. 없으면 null — 뷰가 자리표시로 폴백한다.
        private static Sprite LoadVfx(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Art/VFX/{fileName}.png");
        }

        // ---- 애니메이션 프레임 ----

        /// <summary>
        /// 상태별 목표 초. **값의 소스는 <see cref="CombatTuning"/>이다**(지침 §3 하드코딩 금지) —
        /// `260907_W01` 4-5가 사용자 확정으로 넷을 줬고 그것이 SO에 들어 있다.
        /// SO를 못 찾으면 W01 표의 값으로 내린다.
        /// </summary>
        private static float TargetSeconds(CombatTuning tuning, UnitAnimState state)
        {
            switch (state)
            {
                case UnitAnimState.Idle:  return tuning != null ? tuning.animIdleSeconds  : 1.00f;
                case UnitAnimState.Move:  return tuning != null ? tuning.animMoveSeconds  : 1.00f;
                case UnitAnimState.Death: return tuning != null ? tuning.animDeathSeconds : 2.00f;
                default:                  return tuning != null ? tuning.animTagInSeconds : 0.75f;
            }
        }

        /// <summary>
        /// 왕복은 대기만이다(W01 4-5 · 사용자 확정). 이동을 왕복시키면 여섯 칸을 걷고
        /// 그것을 거꾸로 걷는다 — 뒷걸음질이 된다.
        /// </summary>
        private static bool IsPingPong(UnitAnimState state) => state == UnitAnimState.Idle;

        /// <summary>
        /// 그림 순서와 다른 칸 목록이 필요한 벌에 그 목록을 준다. 없으면 null — 그림 순서대로 돈다.
        ///
        /// <b>로봇 A 이동 남·북과 합체 이동 남·북이 그 자리다</b>(`260907_W03` 2-3 · `260907_W04` 4-3).
        /// 걷기의 한 바퀴는
        /// 「왼발 앞 → 모임 → 오른발 앞 → 모임 → 왼발 앞」이라 <b>모이는 자세를 두 번 지난다</b> —
        /// 그림은 다섯 장인데 칸은 여섯이다. 그 한 칸을 사본 파일로 채우고 있었는데,
        /// 「9프레임 상한은 그림 장수다」(15 7-1)에 어긋나 목록으로 옮겼다.
        ///
        /// ⚠️ <b>다섯 장만 돌리면 안 된다</b> — 모이는 자세를 건너뛰고 오른발 앞에서 왼발 앞으로
        /// 바로 넘어가 다리가 튄다. 가운데를 한 번 더 가리키는 그 칸이 그것을 막는다.
        ///
        /// <b>장수로 조건을 건다.</b> 사본이 아직 안 지워져 여섯 장이면 지금까지대로 돌아
        /// 화면이 안 바뀐다 — 파일 삭제와 이 코드의 순서를 서로 기다리지 않아도 된다.
        ///
        /// <b>벌마다 적는다 — 기체 이름을 빼고 일반화하지 않는다</b>(`260907_W04` 4-3).
        /// 「걷기 벌이 다섯 장이면」으로 묶으면 아직 안 온 기체까지 규칙이 미리 걸린다.
        /// 조건식을 나란히 두면 어느 벌이 왜 이 목록을 갖는지가 코드에서 읽힌다.
        /// </summary>
        /// <remarks><c>public</c>인 것은 <c>MBI.Tests.EditMode</c>가 다른 어셈블리라서다 —
        /// 규칙을 테스트가 직접 부르지 못하면 「벌마다 적는다」가 코드로 안 지켜진다.</remarks>
        public static int[] CellOrder(string robot, UnitAnimState state, UnitAnimDirection dir, int frameCount)
        {
            bool walkSouthNorthFive = state == UnitAnimState.Move
                                      && (dir == UnitAnimDirection.South || dir == UnitAnimDirection.North)
                                      && frameCount == 5;

            bool robotAWalk = robot == "robot_a" && walkSouthNorthFive;
            bool fusionWalk = robot == "fusion" && walkSouthNorthFive;

            return robotAWalk || fusionWalk ? new[] { 0, 1, 2, 3, 4, 2 } : null;
        }

        /// <summary>
        /// <c>Art/Anim/{robot}_{State}/{dir}/frame_*.png</c>를 이름 순으로 읽어 벌을 만든다.
        /// 폴더가 없으면 그 벌을 건너뛴다 — 아직 안 만든 방향이 있어도 있는 것만 걸린다.
        ///
        /// 경로가 여기 한 곳에만 있고 런타임에는 SO 참조만 남는다(§8 명명 규칙).
        /// </summary>
        private static List<UnitAnimClip> LoadAnimClips(string robot, CombatTuning tuning)
        {
            var clips = new List<UnitAnimClip>();
            foreach (UnitAnimState state in System.Enum.GetValues(typeof(UnitAnimState)))
            {
                foreach (UnitAnimDirection dir in System.Enum.GetValues(typeof(UnitAnimDirection)))
                {
                    string folder = $"Assets/_Project/Art/Anim/{robot}_{state}/{dir.ToString().ToLowerInvariant()}";
                    if (!Directory.Exists(folder)) continue;

                    var frames = new List<Sprite>();
                    foreach (string file in Directory.GetFiles(folder, "frame_*.png"))
                    {
                        string path = file.Replace('\\', '/');
                        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                        if (sp != null) frames.Add(sp);
                    }
                    if (frames.Count == 0) continue;

                    clips.Add(new UnitAnimClip
                    {
                        state = state,
                        direction = dir,
                        frames = frames.ToArray(),
                        targetSeconds = TargetSeconds(tuning, state),
                        pingPong = IsPingPong(state),
                        // 머무름 칸은 화면을 보고 고르는 값이라 아직 비어 있다 — W01 확인 4.
                        dwellCells = null,
                        cellOrder = CellOrder(robot, state, dir, frames.Count),
                    });
                }
            }
            return clips;
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
                // 화면 배율 — 보스만 2다(2026-09-10 사용자 확정). 값의 원천은 상수 하나이며
                // 여기서 SO 로 옮긴다. 코드가 배율을 직접 들고 있지 않게 하려는 것이다(§3).
                d.viewScale = d.role == EnemyRole.Boss ? ArtSpec.BossViewScale : 1;
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
