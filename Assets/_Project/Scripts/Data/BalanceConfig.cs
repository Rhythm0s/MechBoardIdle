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
        [Tooltip("물류 최적화 상한 배율. 물류만으로 이 배율 초과 불가. params.ceil.")]
        public float ceil = 1.6f;
        [Tooltip("마운트계수(S4 강화, 곱셈). S4부터만 적용. params.enh / enhance.enhPoint.")]
        public float enh = 1.45f;

        [Header("강화 밴드 (enhance 블록)")]
        [Tooltip("강화 배율 밴드 [lo, hi]. enhance.enhBand.")]
        public Vector2 enhBand = new Vector2(1.3f, 1.5f);
        [Tooltip("요구치 스냅 허용 오차(±). enhance.snapBand.")]
        public float snapBand = 0.08f;

        [Header("요구치 앵커 (S3 돌파 / S4 벽)")]
        [Tooltip("S3 돌파 요구치(v4 2차 실측 개정 = 145; v3.1은 143). enhance.s3Break.")]
        public float s3Break = 145f;
        [Tooltip("S4 요구치 밴드 [lo, hi]. enhance.s4Band.")]
        public Vector2 s4Band = new Vector2(186f, 215f);
        [Tooltip("S4 강화 비용(⑦ A안 닫힌 곡선). enhance.s4Cost.")]
        public float s4Cost = 100f;

        [Header("도전")]
        [Tooltip("도전 제한시간(초). stages[].challengeTime.")]
        public float challengeTime = 120f;

        [Header("경제")]
        [Tooltip("오프라인 보상 인정 상한(시간). economy.offline.capHours = 36 — 경제 항목 중 유일한 확정치. 계수·기본 시급은 TBD라 EconomyConfig에 있다.")]
        public float offlineCapHours = 36f;

        [Header("재고")]
        [Tooltip("탄약 재고 용량(발). params.store = 40 확정치. 재고는 단일 층 — 마운트 적재와 창고 비축이 별개가 아니다. 저장 노드 배치에 따른 증가 규칙은 TBD(LogisticsConfig).")]
        public float storeCapacity = 40f;

        [Tooltip("마운트 품목 최대 스택. 탄약 3종·드론 2종 공통 10 확정(260901_V03). " +
                 "적재량 = 슬롯 × 이 값 → 로봇 A 4×10 = 40 · 로봇 B 8×10 = 80. " +
                 "⚠️ 저장 노드 용량 40과 숫자가 같은 것은 우연이다 — 층이 다르다.")]
        public float mountStackLimit = 10f;

        [Header("탄종별 생산 — 260824_V02 §1 확정")]
        [Tooltip("군수 노드 1개당 생산(발/초). params.muniPerNode = 1 확정치. ⚠️ 소비 상한(capA 6)과 혼동 금지 — 여기에 6을 넣으면 노드 하나가 상한을 다 채워 보드가 출력을 못 바꾼다.")]
        public float muniPerNode = 1f;

        [Tooltip("라인 100% 가동 발사율(발/초). params.specA0/1/2 = 관통 5 / 분열 4 / 폭발 2. 등가선: 스펙 × 발당피해 = 100.")]
        public Vector3 lineSpecShots = new Vector3(5f, 4f, 2f);

        /// <summary>탄종별 라인 스펙(발/초). 성분 순서 = AmmoKind(Pierce · Split · Explosive).</summary>
        public float LineSpecOf(AmmoKind kind)
        {
            switch (kind)
            {
                case AmmoKind.Pierce: return lineSpecShots.x;
                case AmmoKind.Split: return lineSpecShots.y;
                default: return lineSpecShots.z;
            }
        }

        [Header("드론(로봇 B) — 확정치")]
        [Tooltip("드론 슬롯 수. params.slot = 3 확정치(강화 비대상 상수).")]
        public int droneSlots = 3;

        [Tooltip("슬롯당 방출률(기/초). params.r = 1.0 확정치. 실효 방출량 = min(유입, 슬롯 x 방출률).")]
        public float droneReleaseRate = 1f;

        [Tooltip("드론 1기의 충전량(= 피해 총량). params.dB = 100 확정치. 1기 = 1회 타격 = 충전량 전량 — 나눠 쏘면 등가선을 벗어난다.")]
        public float droneCharge = 100f;

        [Tooltip("드론 몸체 유입(기/초). params.pB = 1.0 확정치. 등가선: pB x dB = 100 = 관통 20x5와 같은 DPS.")]
        public float droneInflow = 1f;

        /// <summary>물류 단독 천장 = origin * ceil. S3req &lt; 천장 &lt; S4밴드 (S4가 강화-only 벽).</summary>
        public float LogisticsCeiling => origin * ceil;
    }
}
