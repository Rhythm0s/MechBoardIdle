using System.Collections.Generic;
using System.IO;
using MBI.Core;
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
            // ⚠️ **값은 덮어쓰지 않되 새 필드는 자산에 박는다**(2026-09-15 · §72-42).
            //
            // `LoadOrCreate` 만 하면 **새로 만든 필드가 YAML 에 안 들어간다.** 그러면 런타임은
            // C# 기본값을 쓰고, 나중에 누가 인스펙터에서 한 번 저장하는 순간 **자산이 이기는
            // 값으로 바뀐다** — 그 갈림이 오늘 `spawnCadence` 에서 **SO 0.15 / C# 0.35** 로
            // 터졌던 자리다(§72-14).
            //
            // `SetDirty` + `SaveAssets` 는 **지금 값 그대로** 전 필드를 다시 적는다 —
            // 기존 값은 안 건드리고 **없던 키만 생긴다.** 「덮어쓰지 않는다」와 안 부딪힌다.
            CombatTuning tuningAsset = LoadOrCreate<CombatTuning>(TuningPath);
            if (tuningAsset != null) EditorUtility.SetDirty(tuningAsset);

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

            // 로봇 HP — **json 이 원천이다**(2026-09-17 사용자 확정 1000 · `260917_W05` 3장).
            // 🗑️ 구 이름 `robotHpTbd` 와 구 기본값 3000 폐기. 자산이 값을 따로 들면 진실이 둘이 된다.
            if (tuning != null) tuning.robotHp = json.Param("robotHp");

            // 설치된 VFX 넷 (2026-09-08 · 260908_W06 6장). 셋은 배선됐고
            // `vfx_ammoout`은 **사건 자리가 코드에 없어** 자리만 걸어 둔다 — 지어 넣지 않는다.
            if (tuning != null)
            {
                tuning.droneLaunchSprite = LoadVfx("vfx_dronelaunch");
                tuning.boosterSprite     = LoadVfx("vfx_booster");
                tuning.droneExpireSprite = LoadVfx("vfx_droneexpire");
                tuning.ammoOutSprite     = LoadVfx("vfx_ammoout");

                // 피격 VFX 셋 (2026-09-16 아트 설치 · 사용자 승인 §74-7 ②).
                // ⚠️ 한 장이 아니라 **폴더 안 칸 넷**이다(`frame_000`~`frame_003` · Anim 관례).
                //    없으면 빈 배열이고, 그때 러너가 코드 플래시로 떨어진다 — 지어 넣지 않는다.
                tuning.hitStandardFrames  = LoadVfxFrames("vfx_hit_standard");
                tuning.hitPierceFrames    = LoadVfxFrames("vfx_hit_pierce");
                tuning.hitExplosiveFrames = LoadVfxFrames("vfx_hit_explosive");

                // 광역형 드론 범위 고리 (2026-09-18 아트 설치 · 사용자 승인).
                // ⚠️ **그림만 걸린다 — 켜는 것은 따로다.** 고리를 그리는 크기가 곧 판정 반경인데
                //    그 값이 아직 없어(설계 `0918_W01` 대기) 지금은 드론 사거리를 쓰고 있다.
                //    그래서 `droneAoeRingOn` 은 **꺼 둔 채**다 — 09-17 판단 그대로.
                tuning.droneAoeSprite = LoadVfx("vfx_drone_aoe");
            }

            // 전투 배경 둘 (2026-09-09 배선). 보스 배경은 **S6에서만** 쓰인다 —
            // 고르는 일은 러너가 하고 여기서는 둘 다 걸어 두기만 한다.
            if (tuning != null)
            {
                tuning.combatBackgroundSprite = LoadBackground("bg_combat");
                tuning.bossBackgroundSprite   = LoadBackground("bg_combat_boss");
            }

            if (tuning != null) EditorUtility.SetDirty(tuning);

            // ⚠️ **소리는 여기서 안 건드린다**(2026-09-15 · 함정 세 번째 재발을 끊는다).
            // `AudioConfig` 의 주인은 `AudioAssetGenerator` 하나다 — 그 주석에 까닭이 있다.
            BuildPortfolioLinks(); // 메인 메뉴 주소 — 자리만 만들고 값은 사용자가 넣는다

            float capA = json.Param("capA");             // 6 소비 상한
            float enh = json.Param("enh");               // 1.45 강화 마운트계수
            float moduleMult = json.Param("moduleMult"); // 1.0 모듈배율

            // shotsPerSec = 물류 생산 발사율(대표 상태 pA, mock). 무기 기계 최대치 아님(§물류 제약).
            // 출력 = Σ pA×dA = 1×20 + 1×25 + 2×50 = 145 (mock). 벨트/시뮬 완성 시 동적 산출로 교체.
            // ⚠️ 「= s3Break」 표기는 폐기 — 앵커가 없어졌다(260910_W02 2-2).
            // ⚠️⚠️ **무기 값의 원천도 표다**(2026-09-18 · `WEAPON_DATA`).
            //    🗑️ json `params` 의 `dA*`·`pA*` 는 폐기 표기 대상 — 값은 남기되 안 읽는다.
            //    ⚠️ 드론 줄(`AmmoKind` 4·5)은 **탄종이 아니라** 건너뛴다.
            CsvTable weaponTable = GameDataTables.Load("WEAPON_DATA");
            weaponTable.Require("RobotID", "AmmoKind", "Damage", "ShotsPerSec",
                                "ShotsPerRound", "ShotDamageFactor",
                                "HitInterval", "DamageFraction");

            var weapons = new List<WeaponSpec>();
            foreach (CsvTable.Row w in weaponTable.Rows)
            {
                if (w.Int("RobotID") != 1) continue;              // 로봇A 만
                AmmoKind? kind = GameDataTables.AmmoKindOf(w.Int("AmmoKind"));
                if (kind == null) continue;                        // 드론 줄
                weapons.Add(new WeaponSpec(kind.Value, w.Num("Damage"), w.Num("ShotsPerSec")));
            }
            if (weapons.Count == 0)
                throw new System.FormatException("[WEAPON_DATA] 로봇A 의 탄종 줄이 하나도 없다");

            // ⚠️⚠️ **`CombatTuning` 의 네 칸도 표가 준다** — 여기서 `LoadOrCreate` 계약이 갈린다.
            //    그 자산은 「생성기가 만들기만 하고 값은 안 덮는다」였는데(미확정치를 인스펙터에서
            //    만져 보려고), **이 넷은 이제 표가 원천**이라 덮는 것이 맞다.
            //    📌 나머지 TBD 칸은 그대로 안 덮는다 — 갈린 것은 이 넷뿐이다.
            if (tuning != null)
            {
                foreach (CsvTable.Row w in weaponTable.Rows)
                {
                    if (w.Int("RobotID") == 1 && GameDataTables.AmmoKindOf(w.Int("AmmoKind")) != null)
                    {
                        tuning.shotsPerRound = w.Int("ShotsPerRound", 1);
                        tuning.shotDamageFactor = w.Num("ShotDamageFactor", 1f);
                    }
                    if (w.Int("RobotID") == 2)
                    {
                        tuning.droneHitIntervalTbd = w.Num("HitInterval");
                        tuning.droneDamageFractionTbd = w.Num("DamageFraction");
                    }
                }
                EditorUtility.SetDirty(tuning);
            }

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
            // ⚠️ **광역형 자산은 08-25 승인본인데 여태 안 걸려 있었다**(2026-09-16).
            //    종 구분이 코드에 없어 걸 자리가 없었다 — 이제 생겼다.
            b.droneAoeSprite = LoadArt("drone_w");
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
        /// ⚠️ **폐기 — 이 생성기는 `AudioConfig` 를 안 건드린다**
        /// (2026-09-15 · 등재된 함정의 **세 번째 재발**을 끊는다).
        ///
        /// **무슨 일이 있었나.** 여기서 효과음을 `.wav` 로 찾았는데
        /// <see cref="AudioAssetGenerator"/> 는 `.ogg` 로 꽂는다. 그래서 이 생성기를 돌릴
        /// 때마다 **못 찾은 클립이 null 로 덮여** 배선 여덟이 통째로 날아갔다 —
        /// 09-14 · 09-15 두 번 겪고 HANDOFF 에 함정으로 적어 두었는데, **적어 둔 것이
        /// 세 번째를 막지 못했다.** 적는 것과 고치는 것은 다른 일이다.
        ///
        /// **왜 확장자를 맞추는 것으로 안 끝내나.** 맞춰도 **주인이 둘인 것**은 그대로다.
        /// 한쪽이 경로를 바꾸면 다른 쪽이 조용히 어긋난다(지침 §7 「한 값이 두 곳에 살면
        /// 답이 둘이 된다」). 그래서 **`AudioConfig` 의 주인을 `AudioAssetGenerator` 하나로**
        /// 옮겼다 — BGM 둘까지 그쪽이 꽂는다.
        ///
        /// 메서드는 **폐기 표기로 남긴다.** 부르는 곳은 없다.
        /// </summary>
        public static AudioConfig BuildAudioConfig()
        {
            // 소유자는 `AudioAssetGenerator` 다. 여기서는 읽지도 만들지도 않는다.
            return AssetDatabase.LoadAssetAtPath<AudioConfig>(AudioConfigPath);
        }

        /// <summary>
        /// 배경은 `Art/Backgrounds` 아래에 산다 (2026-09-09 배선).
        ///
        /// ⚠️ **`internal`인 이유** — 배경 셋이 SO 둘에 나뉘어 걸린다. 전투 배경 둘은
        /// <see cref="CombatTuning"/>이 들고 보드 배경 하나는 `BoardArtSet`이 드는데,
        /// 그 둘의 생성기가 다르다. **경로 문자열을 양쪽에 하나씩 두면 폴더를 옮길 때
        /// 한쪽만 고치게 된다** — 지침 §7 ［09-07］「한 값이 두 곳에 살면 답이 둘이 된다」.
        /// 그래서 읽는 함수는 하나이고 부르는 곳이 둘이다.
        /// </summary>
        /// <summary>VFX 폴더의 **낱장** 그림 하나. 없으면 null — 지어내지 않는다.</summary>
        private static Sprite LoadVfxStill(string fileName)
            => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Art/VFX/{fileName}.png");

        internal static Sprite LoadBackground(string fileName)
            => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Art/Backgrounds/{fileName}.png");

        // 이펙트는 Art/VFX 아래에 산다. 없으면 null — 뷰가 자리표시로 폴백한다.
        /// <summary>
        /// VFX 폴더 하나를 **칸 순서대로** 읽는다 (`frame_000` · `frame_001` …).
        ///
        /// ⚠️ 이름순으로 정렬한다 — `AssetDatabase.FindAssets` 는 **순서를 지어 주지 않는다.**
        /// 정렬을 빼면 칸이 섞인 채로 돌고, 그것은 에러 없이 이상하게 보일 뿐이다.
        /// </summary>
        private static Sprite[] LoadVfxFrames(string folder)
        {
            string dir = "Assets/_Project/Art/VFX/" + folder;
            if (!System.IO.Directory.Exists(dir)) return new Sprite[0];

            var paths = new List<string>(System.IO.Directory.GetFiles(dir, "frame_*.png"));
            paths.Sort(System.StringComparer.Ordinal);

            var frames = new List<Sprite>();
            foreach (string path in paths)
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path.Replace("\\", "/"));
                if (sp != null) frames.Add(sp);
            }
            return frames.ToArray();
        }

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
        /// <summary>
        /// 적 키 → 아트 이름. **키와 파일 이름이 다르다** — 아트는 `mob_*` 로 뽑았고
        /// 밸런스는 병종 이름으로 부른다. 그 간극이 여기 한 곳에만 있다(§8).
        ///
        /// ⚠️ **보스는 `boss` 가 아니라 `boss_256` 이다.** `Units/boss.png` 는 **512 스틸·컷인용**이고,
        /// 전투 승인본은 **256**(`boss_256`)이다. 512 를 걸면 캔버스만으로 이미 2.667칸인데
        /// 거기에 배율 2 가 또 곱해져 **5.33칸**이 된다 — 화면의 절반을 덮는다.
        /// </summary>
        private static string ArtNameFor(string enemyKey)
        {
            switch (enemyKey)
            {
                case "infantry": return "mob_infantry";
                case "artillery": return "mob_cannon";
                case "armor": return "mob_armor";
                case "boss": return "boss";
                default: return null;
            }
        }

        /// <summary>
        /// 스틸 이름 — **벌 폴더 이름과 갈리는 것은 보스뿐이다.**
        ///
        /// 벌은 `Anim/boss_*` 에 있고 전투 스틸은 `Units/boss_256.png` 다.
        /// ⚠️ `Units/boss.png` 는 **512 스틸·컷인용**이다. 그것을 걸면 캔버스만으로 이미
        /// 2.667칸인데 배율 2 가 또 곱해져 **5.33칸** — 화면 절반을 덮는다.
        /// </summary>
        private static string StillNameFor(string enemyKey)
            => enemyKey == "boss" ? "boss_256" : ArtNameFor(enemyKey);

        /// <summary>
        /// 스틸 한 장. <c>Units/</c> 에 없으면 **대기 벌의 남면 첫 칸**을 스틸로 쓴다 —
        /// 몬스터 셋은 스틸을 따로 안 뽑았고 벌만 있다. 둘 다 없으면 null 이라
        /// 뷰가 색 사각으로 떨어진다(자리표시 그림을 만들지 않는다).
        /// </summary>
        private static Sprite LoadUnitStill(string artName)
        {
            if (string.IsNullOrEmpty(artName)) return null;

            Sprite unit = LoadArt(artName);
            if (unit != null) return unit;

            return AssetDatabase.LoadAssetAtPath<Sprite>(
                $"Assets/_Project/Art/Anim/{artName}_Idle/south/frame_000.png");
        }

        private static int BuildEnemies(BalanceJson json)
        {
            // 벌의 재생 시간은 조율 SO 에서 온다(로봇과 같은 값). 없으면 LoadAnimClips 가 기본을 쓴다.
            CombatTuning tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>(TuningPath);
            if (json.enemies == null) return 0;

            // **표를 연다** — 없으면 여기서 죽는다(json 으로 되돌아가면 누가 값을 냈는지 모른다).
            CsvTable enemyTable = GameDataTables.Load("ENEMY_DATA");
            enemyTable.Require("EnemyKey", "Atk", "MoveSpeed", "AttackRange",
                               "AttackInterval", "ProjectileSpeed", "ViewScale", "Confirmed");

            var enemyRows = new System.Collections.Generic.Dictionary<string, CsvTable.Row>();
            foreach (CsvTable.Row r in enemyTable.Rows)
                enemyRows[GameDataTables.EnemyKeyOf(r.Int("EnemyKey"))] = r;

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

                // ⚠️⚠️ **여기서부터 값의 원천은 표다**(2026-09-18 사용자 확정 · 플랜 §85-12).
                //
                // `ENEMY_DATA` 가 종류별 값을 준다 — 공격력과 **자산에 처음 채워지는 넷**
                // (이동 속도 · 사거리 · 공격 주기 · 투사체 속도)과 그림 배율.
                //
                // 🗑️ 구 거동 — 넷이 **전부 0** 이라 `StageSpawnFactory.Pick` 이 2단(병종 규칙)
                //    이나 3단(`CombatTuning` 폴백)으로 떨어졌다. 표가 1단을 채우므로 **자산이 이긴다.**
                //    ⚠️ **0 = 안 정함 규약은 그대로다** — 표의 칸이 비면 0 이 들어가고 폴백이 산다.
                //
                // ⚠️ json 의 `enemies[].atk` 는 표가 덮는다(폐기 표기 대상 · 값은 json 에 남는다).
                if (enemyRows.TryGetValue(e.key, out CsvTable.Row er))
                {
                    d.atk = er.Num("Atk");
                    d.moveSpeed = er.Num("MoveSpeed");
                    d.attackRange = er.Num("AttackRange");
                    d.attackInterval = er.Num("AttackInterval");
                    d.projectileSpeed = er.Num("ProjectileSpeed");
                    d.atkConfirmed = er.Bool("Confirmed");
                }
                else
                {
                    // **없는 줄을 지어내지 않는다** — 표에 없으면 그 사실이 보여야 한다.
                    Debug.LogWarning($"[MBI] ENEMY_DATA 에 '{e.key}' 줄이 없다 — json 값으로 굽는다");
                }

                // 그림 — 로봇과 같은 길로 주입한다. 경로는 생성기에만 있고 런타임은 참조만 본다.
                string artName = ArtNameFor(e.key);
                d.sprite = LoadUnitStill(StillNameFor(e.key));
                d.animClips = artName != null
                    ? LoadAnimClips(artName, tuning)
                    : new System.Collections.Generic.List<UnitAnimClip>();
                // 화면 배율 — 보스만 2다(2026-09-10 사용자 확정). 값의 원천은 상수 하나이며
                // 여기서 SO 로 옮긴다. 코드가 배율을 직접 들고 있지 않게 하려는 것이다(§3).
                // 화면 배율도 표가 든다(구 「보스만 2」 상수 갈래는 표의 한 열이 됐다).
                d.viewScale = enemyRows.TryGetValue(e.key, out CsvTable.Row vr)
                    ? Mathf.Max(1, vr.Int("ViewScale", 1))
                    : (d.role == EnemyRole.Boss ? ArtSpec.BossViewScale : 1);

                // ⚠️ **적 포탄 그림 — 자산이 오면 여기서 들어간다**(2026-09-16 · 육안 2차 ②).
                //    지금은 없어 `null` 이고 러너가 흰 사각으로 떨어진다. 파일만 놓으면
                //    다음 생성에서 저절로 걸린다 — 배선을 나중에 또 찾지 않게 미리 판다.
                //    ⚠️ 이름 `vfx_enemy_shell` 은 **아트 요청 가정**이다.
                d.projectileSprite = LoadVfxStill("vfx_enemy_shell");
                EditorUtility.SetDirty(d);
                n++;
            }
            return n;
        }

        // ---- 스테이지 6종: 요구치·모델·구성 ----
        private static int BuildStages(BalanceJson json)
        {
            if (json.stages == null) return 0;

            // **구성 표를 연다** — 스테이지마다 여러 줄이라 `StageID` 로 묶어 받는다.
            var compByStage = GameDataTables.CompositionByStage(
                GameDataTables.Load("STAGE_COMP_DATA"));

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

                // ⚠️⚠️ **구성의 원천도 표다**(2026-09-18 · `STAGE_COMP_DATA`).
                //    🗑️ json `stages[].composition` 은 폐기 표기 대상이다 — **값은 남기되**
                //    굽는 데는 안 쓴다. 표에 그 스테이지 줄이 없으면 **빈 구성**이며,
                //    그것은 「적이 없는 판」(S0)이라는 뜻이라 지어내지 않는다.
                var comp = new List<StageComposition>();
                if (compByStage.TryGetValue(s.id ?? string.Empty,
                        out System.Collections.Generic.List<CsvTable.Row> rows))
                {
                    foreach (CsvTable.Row c in rows)
                        comp.Add(new StageComposition
                        {
                            enemyKey = GameDataTables.EnemyKeyOf(c.Int("EnemyKey")),
                            count = c.Int("Count"),
                            hp = c.Num("Hp"),
                            def = c.Num("Def"),
                        });
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
