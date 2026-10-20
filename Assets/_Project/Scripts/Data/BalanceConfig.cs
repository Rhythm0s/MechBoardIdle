using System;
using UnityEngine;

namespace MBI.Data
{
    /// <summary>
    /// 밸런스 단일 원천(SO) — balance_v4.json(1차 계약·schemaVersion 4.0) 확정 앵커의 미러.
    /// 데이터 흐름(§9): Unity SO = 단일 원천 → balance.json export → 시뮬 읽기 전용.
    /// 이 자산은 BalanceAssetGenerator가 balance.json에서 생성/갱신한다(§7 드리프트 방지).
    ///
    /// confirmed 앵커만 담는다. 병목/태그 등 미확정치(confirmed:false)는 여기 두지 않고
    /// 노드 SO에 Tbd placeholder로 흩어 둔다(§3).
    /// </summary>
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "MBI/Balance Config", order = 0)]
    public sealed class BalanceConfig : ScriptableObject
    {
        [Header("출처 (balance.json meta)")]
        [Tooltip("balance.json meta.schemaVersion — 드리프트 감시용(§7).")]
        public string schemaVersion = "4.0";
        [Tooltip("balance.json meta.exportedAt.")]
        public string exportedAt = "TBD_STAMP_ON_EXPORT";

        [Header("원점·곡선 (확정 앵커)")]
        [Tooltip("원점 출력 = 요구치 분모. balance.json params.origin.")]
        public float origin = 100f;
        [Tooltip("마운트계수(S4 강화, 곱셈). S4부터만 적용. params.enh / enhance.enhPoint.")]
        public float enh = 1.45f;

        [Header("강화 밴드 (enhance 블록)")]

        // ⚠️⚠️ **한 칸(Vector2) 에서 두 칸으로 갈랐다** (2026-10-13 사용자 확정 ①).
        //
        // 원천이 표(CSV)로 옮겨 가는데, 그 표는 **한 줄에 값 하나**를 담는 꼴이다
        // (`FieldName` · `Value`). 벡터를 한 칸에 담으려면 「쉼표로 둘」 같은 **표에 안 적힌
        // 약속**이 생기고, 그 약속을 모르는 사람은 표를 못 읽는다.
        //
        // 📌 **합쳐 내는 쪽은 그대로 둔다**(<see cref="enhBand"/>) — 읽는 코드와 시험은
        //    종전처럼 `enhBand.x` · `enhBand.y` 를 쓴다. 바뀐 것은 **담는 꼴**뿐이다.
        [Tooltip("강화 배율 밴드 아래값. ENHANCE 표 enhBandX.")]
        public float enhBandX = 1.3f;

        [Tooltip("강화 배율 밴드 위값. ENHANCE 표 enhBandY.")]
        public float enhBandY = 1.5f;

        /// <summary>
        /// 강화 배율 밴드 [lo, hi] — **두 칸을 합쳐 낸다. 읽기 전용이다.**
        ///
        /// ⚠️ 여기에 값을 **쓰지 않는다**. 쓰는 자리는 <see cref="enhBandX"/>·<see cref="enhBandY"/>
        /// 둘뿐이며, 그 둘의 원천은 `ENHANCE_DATA` 표다 — 한 값이 두 곳에 살면 답이 둘이 된다(§7).
        /// </summary>
        public Vector2 enhBand => new Vector2(enhBandX, enhBandY);
        [Tooltip("요구치 스냅 허용 오차(±). enhance.snapBand.")]
        public float snapBand = 0.08f;

        // ⚠️ **`s3Break`(돌파 타깃)와 `s4Band` 는 폐기됐다** (2026-09-10 · `260910_W02` 2-1·2-2).
        //
        // **요구치를 정하는 규칙 자체가 바뀌었다** — 이제 요구치 = 도달치 × **0.9**(역산)이고,
        // 턱걸이를 막는 장치는 **그 상수 0.9 하나뿐**이다. 거기에 돌파 타깃 +10% 를 또 얹으면
        // 타깃이 도달치의 99% 가 되어 **남는 여유가 반올림 잔여와 구분되지 않았다.**
        // 돌파 체감이 부족하면 **0.9 를 올린다** — 타깃을 되살리지 않는다.
        //
        // S4 도 밴드가 아니라 **고정치 183**이며, 요구치는 여기가 아니라
        // <see cref="StageDefinition"/> 의 `reqType`·`req` 에 있다.
        // 구 값은 `balance_v4.json` 의 `enhance.s3BreakRetired`·`s4BandRetired` 에 폐기 표기로 남겼다.

        [Header("강화 비용")]
        [Tooltip("S4 강화 비용(⑦ A안 닫힌 곡선). enhance.s4Cost.")]
        public float s4Cost = 100f;

        [Header("도전")]
        [Tooltip("도전 제한시간(초). stages[].challengeTime.")]
        public float challengeTime = 120f;

        // 🗑️🗑️ **`offlineCapHours` 는 `EconomyConfig` 로 옮겼다** (2026-10-13 사용자 확정 ③).
        //
        // **왜 옮겼나.** 오프라인 값이 셋인데(상한 · 계수 · 기본 시급) **상한만 여기 있고
        // 둘은 `EconomyConfig` 에 있었다.** 한 묶음이 두 자산에 갈려 있으면 표도 둘로
        // 갈라야 하고, 그러면 「어느 표가 오프라인을 든 표인가」에 답이 둘이 된다.
        // 이제 셋이 `EconomyConfig` 한 자산 · `ECONOMY_DATA` 한 표에 산다.
        //
        // ⚠️ **36 은 원천이 있다.** `balance_v4.json` 의 `economy.offline.capHours = 36` 이고,
        // 구 주석의 「경제 항목 중 유일한 확정치」는 **맞는 말이었다.** 옮기는 것은 자산일 뿐
        // 값도 확정 여부도 안 바뀐다 — 표에는 그 36 을 그대로 옮겨 적는다.

        [Header("재고")]
        [Tooltip("탄약 재고 용량(발). params.store = 40 확정치. 재고는 단일 층 — 마운트 적재와 창고 비축이 별개가 아니다. 저장 노드 배치에 따른 증가 규칙은 TBD(LogisticsConfig).")]
        public float storeCapacity = 40f;

        [Tooltip("마운트 품목 최대 스택. 탄약 3종·드론 2종 공통 10 확정(260901_V03). " +
                 "적재량 = 슬롯 × 이 값 → 로봇 A 4×10 = 40 · 로봇 B 8×10 = 80. " +
                 "⚠️ 저장 노드 용량 40과 숫자가 같은 것은 우연이다 — 층이 다르다.")]
        public float mountStackLimit = 10f;

        [Header("탄종별 생산 — 260824_V02 §1 확정")]
        [Tooltip("🗑️ 폐기(2026-09-17) — 로봇 고정 그릇. 최대치는 이제 노드 수 × shieldMaxPerNode 다. 0 으로 둔다.")]
        public float shieldMax;

        [Tooltip("쉴드 발생 노드 1대당 최대치. params.shieldMaxPerNode = 200 확정. 최대치 = 노드 수 × 이 값.")]
        public float shieldMaxPerNode = 200f;

        [Tooltip("🗑️ 폐기(2026-09-17) — 개당 충전량은 이제 파생값이다(최대치 × 비율 ÷ 대당 소비). 0 으로 둔다.")]
        public float shieldChargePerMaterial;

        [Tooltip("쉴드 초당 충전 비율. params.shieldChargeRatioPerSec = 0.025 확정. 충전률 = 최대치 × 이 값. 빈 게이지가 차는 데 40초.")]
        public float shieldChargeRatioPerSec = 0.025f;

        [Tooltip("쉴드 발생 노드 1대가 1초에 먹는 쉴드 재료(개). params.shieldMaterialPerSec = 1 측정용 고정값.")]
        public float shieldMaterialPerSec = 1f;

        [Tooltip("추진제 필요 생산치. params.propellantNeed = 30 확정치(구 150). 산출률 = 노드 생산력 ÷ 이 값이라 기초 가공소(구 기초 군수) 1대가 3초에 1개를 낸다.")]
        public float propellantNeed = 30f;

        [Tooltip("회피할 때 밀려나는 거리(칸). params.dodgeMoveDistance = 0.5 확정치(2026-09-17 사용자).")]
        public float dodgeMoveDistance = 0.5f;

        [Tooltip("회피 이동에 걸리는 시간(초). params.dodgeMoveSeconds = 0.167 확정치 — 무적과 같이 끝난다.")]
        public float dodgeMoveSeconds = 0.167f;

        [Tooltip("광역형 드론의 광역 판정 반경(칸). params.droneAoeJudgeRadius = 2 ⚠️ 잠정 점값 — 설계 확정은 「본전 4마리」이고 이 수는 그것을 만들 추정이다. 0 이면 구 거동(드론 사거리)으로 떨어진다.")]
        public float droneAoeJudgeRadius = 2f;

        [Tooltip("광역형 드론의 표적당 피해 비(누적형 대비). params.droneAoeDamageFactor = 0.5 ⚠️ 가정 표기 — 타격당 피해만 절반이고 충전량·수명은 그대로다. 0 이면 구 거동(둘이 같은 피해).")]
        public float droneAoeDamageFactor = 0.5f;

        [Tooltip("드론 스택 비(탄약 대비). params.mountDroneStackFactor = 0.5 ⚠️ 가정 표기 — 값은 사용자 확정이고 칸만 코드에서 옮겨 왔다. 로봇 B 적재량 = 슬롯 8 × 스택 × 이 비.")]
        public float mountDroneStackFactor = 0.5f;

        [Tooltip("부스터 1대당 회피 스택 칸. params.dodgeStacksPerBooster = 4 확정치(구 2). 회피 스택 상한 = 부스터 노드 대수 × 이 값. 시작 보드는 부스터 둘이라 상한 8.")]
        public int dodgeStacksPerBooster = 4;

        [Tooltip("합체 지속 중 두 보드 모든 노드의 산출량 배율. params.mergeOutputMult = 2 확정치. 재료·전력·생산 주기는 그대로이고 벨트는 제외다(12/초 고정). 합체가 끝나면 즉시 1.0.")]
        public float mergeOutputMult = 2f;   // 🗑️ #229 — 산출 배율은 이제 과부하 동안 `overloadProduce`

        [Tooltip("과부하 발사 배율(사용자 확정 #229 · 2026-10-20). params.overloadFireRate = 2 — 과부하 동안 마운트 소비(= 발사 · 드론 발진) ×2. 피해 ×1.8(mergeMult)은 그대로 곱한다.")]
        public float overloadFireRate = 2f;

        [Tooltip("과부하 산출 배율(사용자 확정 #229 · #238 2.5 → 3). params.overloadProduce = 3 — 과부하 동안 A·B 보드 노드(코어·가공소) 산출 ×3. 합체 뒤 만재 복귀용.")]
        public float overloadProduce = 3f;

        [Tooltip("과부하 벨트 속도 배율(사용자 확정 #239). params.overloadBeltMult = 2 — 과부하 동안 벨트 아이템 이동 ×2. ⚠️ 연출 축(승패 지렛대 아님 · #236 하네스).")]
        public float overloadBeltMult = 2f;

        [Tooltip("과부하 지속(초). params.overloadSec = 20 ⚠️ 미확정(사용자 「10~20초」) — 기본 = 합체 20초 전체.")]
        public float overloadSec = 20f;

        [Tooltip("군수 노드 1개당 생산(발/초). params.muniPerNode = 1 확정치. ⚠️ 소비 상한(capA 6)과 혼동 금지 — 여기에 6을 넣으면 노드 하나가 상한을 다 채워 보드가 출력을 못 바꾼다.")]
        public float muniPerNode = 1f;

        [Tooltip("라인 100% 가동 발사율(발/초). params.specA0/1/2 = 관통 5 / 표준 6 / 폭발 2. " +
                 "⚠️ 등가선 「스펙 × 발당피해 = 100」은 폐기됐다 — 축이 노드당 출력으로 바뀌었다(260909_W01 2-2). " +
                 "표준탄만 초당 60인 것은 결함이 아니라 체인이 짧기 때문이다.")]
        public Vector3 lineSpecShots = new Vector3(5f, 6f, 2f);

        [Tooltip("노드 1대가 초당 내는 생산치. 산출 속도 = 이 값 ÷ 그 레시피의 필요 생산치(260909_W01 2-3). " +
                 "⚠️ 발/초와 다른 축이다 — 이것은 노드의 일량이고, 몇 개가 나오는지는 레시피가 정한다.")]
        public float nodeProductionPower = 10f;

        /// <summary>탄종별 라인 스펙(발/초). 성분 순서 = AmmoKind(Pierce 0 · Standard 1 · Explosive 2).</summary>
        public float LineSpecOf(AmmoKind kind)
        {
            switch (kind)
            {
                case AmmoKind.Pierce: return lineSpecShots.x;
                case AmmoKind.Standard: return lineSpecShots.y;
                default: return lineSpecShots.z;
            }
        }

        [Header("드론(로봇 B) — 확정치")]
        [Tooltip("드론 슬롯 수. params.slot = 3. 🗑️ #233(2026-10-20) 동시 유지 상한 폐지 — 이 값은 이제 「B 에 드론이 있다」 표식 · 광역형 궤도 각 나눔에만 쓴다.")]
        public int droneSlots = 3;

        [Tooltip("드론 방출 처리량(기/초). params.droneRelease = 3 (#233 — 슬롯 3 × r 1.0 과 같은 값 · 유지 상한 없음).")]
        public float droneRelease = 3f;

        [Tooltip("B 마운트 강화 출격 보너스(기/초). params.enhanceDroneRelease = 2 (사용자 확정 #234 · 🗑️ B 강화 = 드론 피해 ×1.45).")]
        public float enhanceDroneRelease = 2f;

        [Tooltip("슬롯당 방출률(기/초). params.r = 1.0 확정치. 실효 방출량 = min(유입, 슬롯 x 방출률).")]
        public float droneReleaseRate = 1f;

        [Tooltip("드론 1기의 충전량(= 피해 총량). params.dB = 100 확정치. 1기 = 1회 타격 = 충전량 전량 — 나눠 쏘면 등가선을 벗어난다.")]
        public float droneCharge = 100f;

        [Tooltip("드론 몸체 유입(기/초). params.pB = 1.0 확정치. 등가선: pB x dB = 100 = 관통 20x5와 같은 DPS.")]
        public float droneInflow = 1f;

    }
}
