using UnityEngine;

namespace MBI.Data
{
    /// <summary>
    /// 실시간 탑뷰 전투의 튜닝 상수(§5-6·7). ⚠️ 전부 TBD placeholder — 확정 밸런스 아님.
    ///
    /// CLAUDE.md §0·§3 역할 경계: 로봇 HP·전투 타이밍·이동/사거리는 §9에서 미확정(TBD).
    /// Claude Code가 수치를 임의 확정하지 않는다 → 여기에 명시 placeholder로 두고 사용자에게 보고
    /// → chat+Notion 확정 후 balance_v4.json/CLAUDE.md 원천에 반영. (§3 "미확정치는 TBD 밴드 상수")
    ///
    /// BalanceConfig(밸런스 계약)와 분리한다: 여기 값을 계약에 섞으면 생성기 재실행이 덮어쓰고
    /// 오염된다(§3 한 파일=한 책임, BoardConfig 선례). 이 SO는 CombatAssetGenerator가 LoadOrCreate만
    /// 하고 값은 덮어쓰지 않는다(인스펙터 조정 유지).
    /// </summary>
    [CreateAssetMenu(fileName = "CombatTuning", menuName = "MBI/Combat Tuning (TBD)", order = 13)]
    public sealed class CombatTuning : ScriptableObject
    {
        [Header("로봇 (⚠️ TBD — 생존축 후순위, §9 미확정)")]
        [Tooltip("TBD — 로봇 최대 HP. chat+Notion 확정 필요. §9 생존축 예산 밖·후순위.")]
        public float robotHpTbd = 3000f;
        // ⚠️ **100 → 9.2**(2026-09-15 사용자 판정 · 가정값 · 설계 역기입 대기).
        //
        // 구 100 은 사실상 **무한**이라 `AutoPilotPolicy` 의 규칙 둘 중 하나가 죽어 있었다 —
        // 「사거리 밖 → 최근접 적을 향해 이동」이 **한 번도 안 걸렸다.** 화면에서는
        // 로봇이 제자리에 서서 쏘기만 했고, 그것이 자동 조종이 안 걷는 원인이다.
        //
        // 가정값은 **화면 대각선의 절반**이다 — 전투 카메라 `orthographicSize = 8`(세로 절반)에
        // 촬영 창 1440×2560(비 0.5625)이면 세로 16 · 가로 9 유닛이고,
        // 가운데에서 모서리까지가 √(4.5² + 8²) ≈ **9.2**.
        // 근거는 값이 아니라 **보이는 것**이다: 화면에 든 적은 쏠 수 있고 밖은 걸어가야 한다.
        //
        // ⚠️ **Tbd 는 그대로 둔다** — 확정값이 아니다. 창 비가 바뀌면 이 수도 바뀐다.
        [Tooltip("TBD — 로봇 사거리(유닛). 가정 9.2 = 화면 대각선의 절반(ortho 8 · 비 0.5625).")]
        public float robotAttackRangeTbd = 9.2f;
        // ⚠️ **사거리 안에 이만큼 넘게 있으면 제자리**에서 쏜다 (2026-09-15 사용자 확정 · 가정 3).
        // 구 규칙(하나라도 있으면 제자리)에서는 적이 많을수록 로봇이 **한 걸음도 안 걸었다**.
        [Tooltip("TBD — 사거리 안 적이 이 수를 넘으면 제자리 사격. 이하면 가장 가까운 무리로 이동.")]
        public int autoPilotHoldWhenMoreThanTbd = 3;

        [Tooltip("TBD — 로봇 이동 속도(유닛/초). WASD/화살표로 조작(카이팅). chat+Notion 확정 필요.")]
        public float robotMoveSpeedTbd = 4.5f;

        [Header("적 (⚠️ TBD)")]
        [Tooltip("TBD — 적 이동 속도(유닛/초). chat+Notion 확정 필요.")]
        public float enemyMoveSpeedTbd = 1.2f;
        [Tooltip("TBD — 적 공격 사거리(유닛). 이 거리 내에서 로봇을 타격.")]
        public float enemyAttackRangeTbd = 1.0f;
        [Tooltip("TBD — 적 공격 간격(초). atk를 이 주기로 로봇에 가함.")]
        public float enemyAttackIntervalTbd = 1.5f;

        [Header("스폰 / 아레나 (⚠️ TBD)")]
        // ⚠️ **→ 0.8** (2026-09-14 · §72-14 사용자 확정). 구 값은 **둘이었다** —
        // **C# 기본값 0.35** · **SO 자산에 저장된 런타임 값 0.15**. 둘 다 적어 둔다 —
        // 자산이 이기므로 화면에 서던 것은 0.15 였고, 새 값은 둘 다 대신한다.
        // 개체가 많아진 제안표(S1 보병 120)에서 0.35 면 40초 만에 전원이 나온다 —
        // 로봇이 **에워싸여** 이동으로 못 벗어난다. 0.8 이면 96초에 걸쳐 들어온다.
        [Tooltip("TBD — 스폰 간격(초). 0이면 시작 시 전원 스폰(웨이브 없음). 구 0.35(§72-14 전).")]
        public float spawnCadenceTbd = 0.8f;
        [Tooltip("⚠️ 폐기 — 이동 경계가 없어졌다(2026-09-11). 스폰 링은 spawnRingRadiusTbd 가 든다.")]
        public float arenaRadiusTbd = 6f;

        // ⚠️ **스폰 띠**(2026-09-15 사용자 확정 · §72-40 · 값 대기).
        //
        // 적은 로봇 중심 **min~max 사이 아무 거리**에나 난다 — 가까이도 멀리도.
        // 반경 하나였을 때는 모든 적이 같은 거리에서 나서, 그 거리가 사거리보다 짧으면
        // 로봇이 **한 걸음도 안 걸었다**(§72-38 실측: 사거리 9.2 와 100 이 둘 다 95.9초).
        //
        // ⚠️ **값이 아직 없다.** 0 이면 러너가 안 넣고 시뮬은 `arenaRadius` 한 점을 쓴다 —
        // **지금 거동이 안 바뀐다.** 값이 오면 여기 둘만 채우면 된다.
        // ⚠️ **값이 왔다 — 4 ~ 14**(2026-09-15 사용자 확정 · 가정 · 설계 사후 역기입).
        // 사거리 가정 9.2 를 **띠가 가운데서 가른다**: 4~9.2 는 제자리 사격, 9.2~14 는 걸어간다.
        [Tooltip("TBD — 스폰 띠 안쪽(유닛). 0이면 안 쓴다(arenaRadius 한 점).")]
        public float spawnRingMinTbd = 4f;
        [Tooltip("TBD — 스폰 띠 바깥(유닛). 디스폰 되돌림도 이 거리를 쓴다. 0이면 안 쓴다.")]
        public float spawnRingMaxTbd = 14f;

        [Tooltip("TBD — 스폰 링 반경(유닛). 0 이면 카메라에서 잰다(화면 대각선 반 + 한 칸 가정). 값은 260911_W03.")]
        public float spawnRingRadiusTbd = 0f;

        [Header("자동 전투 (⚠️ TBD — 밸런스 아님, 연출·조작감)")]
        // autoPilotDesiredGapTbd 삭제(260829_V03 승인) — 2026-08-26 카이팅 폐기로
        // 「후퇴 개시 거리」라는 개념 자체가 사라졌다. 읽는 곳도 0건이었다.
        [Tooltip("TBD — 수동 입력 후 자동 조종이 다시 잡기까지의 유예(초). 조작 중 자동이 끼어들지 않게.")]
        public float manualOverrideGraceTbd = 2f;
        [Tooltip("TBD — 전투 종료 후 자동 재시작까지 대기(초). 승리 연출을 볼 시간 vs 방치 효율.")]
        public float autoRestartDelayTbd = 1.5f;

        [Header("애니메이션 길이 (260907_W01 4-5 · 사용자 확정)")]
        // 초당 프레임은 폐기됐다(W01 2-2). 한 칸은 1/16초로 고정이고 벌마다 목표 초를 사람이 정한다 —
        // 그래야 그림 수가 부드러움과 길이를 겸하지 않는다. 칸 배분은 AnimSchedule 이 계산한다(4-6).
        // 값 자체는 확정이라 TBD 가 아니다. 하드코딩 금지(지침 §3)라 여기에 둔다.
        [Tooltip("한 칸이 화면에 머무는 시간(초). 1/16 고정 — 벌마다 바꾸지 않는다.")]
        public float animCellSeconds = 1f / 16f;
        [Tooltip("대기 목표 초. 왕복이라 기본 칸(8)의 배수에서만 고른다.")]
        public float animIdleSeconds = 1.00f;
        [Tooltip("이동 목표 초. 나머지 넷이 착지 머무름으로 열린다.")]
        public float animMoveSeconds = 1.00f;
        [Tooltip("사망 목표 초.")]
        public float animDeathSeconds = 2.00f;
        [Tooltip("태그 전환 목표 초. 잠정 — 전투 시스템 문서「태그 규칙」 확인 뒤 확정한다(W01 9장 2).")]
        public float animTagInSeconds = 0.75f;
        // 코드 이동은 발사 프레임보다 뒤에 끝나고 클립보다는 먼저 끝난다(`260907_W02` 2-3 사용자 확정).
        // 설계가 확정한 것은 값이 아니라 순서다 — 태그 지속 시간이 바뀌어도 이 꼬리는 그대로 두면
        // 이동이 따라 맞는다. 꼬리 칸이 남아야 반동과 수렴이 제자리에서 보인다(15-1 5-1 구간 3·4).
        [Tooltip("태그 진입에서 도착 뒤에 남기는 꼬리 칸 수. 이동은 이만큼 일찍 끝난다.")]
        public int animTagEntryTrailCells = 3;

        // ── 태그 스킬 연출 아홉 (2026-09-08 사용자 목업 확정 · 260908_W04 2-3) ──
        // 아트 자산은 탄환 한 발(vfx_tagbullet)뿐이고 나머지는 코드가 그린다.
        // 값이 확정이라 TBD 가 아니지만, 하드코딩 금지(지침 §3)라 전부 여기에 둔다.
        // 아트 픽셀 → 유닛 환산은 PPU 192 를 쓴다(연출 아트 요청 문서 6장).
        [Header("태그 스킬 연출 (260908_W04 2-3 · 사용자 확정)")]
        [Tooltip("레이저(B 태그 인)가 맵을 한 바퀴 도는 시간(초).")]
        public float tagLaserSweepSeconds = 0.40f;
        [Tooltip("레이저 빔 굵기(아트 픽셀). 목업 눈금 7 × 7.5 = 53.")]
        public float tagLaserWidthArtPixels = 53f;
        [Tooltip("레이저 잔광이 남는 각도(도).")]
        public float tagLaserAfterglowDegrees = 90f;
        [Tooltip("탄환비(A 태그 인)가 한 바퀴 도는 시간(초).")]
        public float tagBulletSweepSeconds = 0.40f;
        [Tooltip("탄환비가 덮는 부채꼴 각도(도).")]
        public float tagBulletFanDegrees = 120f;
        [Tooltip("한 바퀴에 떨어지는 탄환 수.")]
        public int tagBulletCount = 10;
        [Tooltip("탄환 한 발의 크기(아트 픽셀). 목업 눈금 5 × 7.5 = 38.")]
        public float tagBulletSizeArtPixels = 38f;
        // 목업이 (1-k) × 26 눈금으로 그리고 있었다 — 눈금 하나가 7.5 아트 픽셀이라 195,
        // 반올림하면 192 = 격자 한 칸이다(260908_W05 2-3). 앞서 넣었던 96은 그 절반이었다.
        [Tooltip("탄환이 위에서 떨어져 보이도록 주는 낙하 거리(아트 픽셀). 192 = 격자 한 칸.")]
        public float tagBulletFallArtPixels = 192f;
        [Tooltip("한 바퀴만 돈다. 반복 없음 — 켜 두면 규칙 위반이라 값이 아니라 표시다.")]
        public bool tagSweepOnceOnly = true;

        // 탄환 한 발(vfx_tagbullet)은 **아트 자산이 있는 유일한 태그 스킬 연출**이다 —
        // 레이저는 전부 코드 드로잉이라 자리 자체가 없다(연출 아트 요청 문서 3-1).
        // 경로는 생성기(CombatAssetGenerator)에만 두고 런타임은 이 참조만 본다(§8 명명 규칙).
        [Tooltip("탄환비가 뿌리는 탄환 한 발. 비면 자리표시(흰 사각)로 그린다.")]
        public Sprite tagBulletSprite;

        [Header("설치된 VFX 자산 (2026-09-08 배선 · 260908_W06 6장)")]
        // 경로는 생성기에만 있고 런타임은 이 참조만 본다(§8 명명 규칙).
        // 비어 있으면 **연출을 그리지 않는다** — 자리표시로 대신하지 않는다.
        // 태그 스킬 탄환과 다른 점이 그것이다: 저쪽은 흰 사각이라도 형태가 읽히지만,
        // 이 넷은 「무엇인지」가 그림에만 있어 흰 사각으로는 뜻이 안 선다.
        [Tooltip("드론이 사출구에서 나가는 순간의 짧은 분사.")]
        public Sprite droneLaunchSprite;
        [Tooltip("회피 발동 순간 등판에서 뒤로 뿜는 분사. 회피 시작에만 1회.")]
        public Sprite boosterSprite;
        [Tooltip("드론이 충전량을 다 쓰고 흩어지는 것.")]
        public Sprite droneExpireSprite;
        [Tooltip("공급이 끊겨 공격이 멈췄다는 표시.")]
        public Sprite ammoOutSprite;

        [Tooltip("탄약 소진 아이콘을 몇 배로 줄여 그리는가. ⚠️ 가정 — 연출 문서에 크기 절이 없다. " +
                 "0.5 는 아이콘 실루엣(212px)이 로봇 실루엣(220px)의 절반 이하가 되는 값이며 " +
                 "상한은 EffectTiming.AmmoOutScaleMax(0.52). 설계가 연출 문서에 역기입할 자리다.")]
        public float ammoOutScaleAssumed = 0.5f;

        // ── 전투 배경 (2026-09-09 배선) ────────────────────────────────────────────
        //
        // 캔버스 256이라 한 장이 1.333칸이고, **코드가 격자로 복제해 깐다.**
        // 임포트 설정(Wrap)은 건드리지 않는다 — 스프라이트를 Repeat으로 늘리려면
        // 임포터를 손대야 하는데 그 설정은 `SpriteImportRules`가 한 곳에서 강제하고 있고,
        // 배경 하나 때문에 그 규격을 흔들면 **다른 스프라이트가 조용히 따라 바뀐다**.
        [Header("전투 배경 (2026-09-09 배선)")]
        [Tooltip("전투 바닥. 비어 있으면 안 깐다 — 아레나 원반만 남는다.")]
        public Sprite combatBackgroundSprite;
        [Tooltip("보스전 바닥. **S6에서만** 위 것을 대신한다(reqType = Budget).")]
        public Sprite bossBackgroundSprite;

        [Tooltip("위 셋이 한 번 그려지고 사라지는 데 걸리는 초. 이펙트는 로봇보다 짧게 끝난다(연출 2장).")]
        public float vfxOneShotSeconds = 0.2f;

        [Header("히트 패턴 (로봇A 탄종 = 단일 표적)")]
        // 플레이어블 로봇 기획서「무기 스펙트럼」(스테이징): 등가선은 단일 표적 기준, 표적 수/광역은 스펙트럼 밖 역할 축(드론 2종 한정).
        // → 로봇A 관통/표준/폭발은 전부 단일 표적. 멀티샷/AoE 메커니즘(HitResolver)은 드론용으로 보존.
        [Tooltip("멀티샷 표적 수. 로봇A 표준탄 = 1(단일). 다중은 드론 역할.")]
        public int multiShotCountTbd = 1;
        [Tooltip("AoE 스플래시 반경(유닛). 로봇A 폭발탄은 splashFactor 0이라 무효(드론용 보존).")]
        public float aoeRadiusTbd = 1.5f;
        [Tooltip("AoE 스플래시 배율. 로봇A 폭발탄 = 0(스플래시 없음, 단일 표적). 드론 광역형이 >0 사용.")]
        public float aoeSplashFactorTbd = 0f;

        [Header("수동 회피 입력 — 화면 플릭")]
        // 회피 자체의 확정치(무적 0.167초 · 스택 상한 3)는 DodgeSystem이 든다. 여기는 **입력 인식** 축이라
        // 별개다 — 플릭으로 볼 문턱은 원천에 없어 TBD다(문턱을 무적 값에서 끌어오면 안 된다).
        [Tooltip("TBD — 플릭으로 인정할 최소 이동(픽셀). chat+Notion 확정 필요.")]
        public float flickMinPixelsTbd = 40f;
        [Tooltip("TBD — 플릭으로 인정할 최대 시간(초). 이보다 오래 끌면 드래그로 본다.")]
        public float flickMaxSecondsTbd = 0.3f;

        // 마운트 탄약 용량(capA=6)은 RobotDefinition.consumptionCap 단일 소스로 통합(§3 한 파일=한 책임, L4-R).
        //   — 중복 미러(mountAmmoCapTbd) 제거. HUD/발사 배분은 robot.consumptionCap을 읽는다.
        // 물류 출력(전투력 입력)은 별도 상수를 두지 않는다 — RobotDefinition.weapons의 mock 생산율(pA)에서
        // MockLogisticsOutput이 집계(ΣpA×dA=145). 실 물류 시뮬(§5-4·5-5) 완성 시 그쪽이 동적 산출.
    }
}
