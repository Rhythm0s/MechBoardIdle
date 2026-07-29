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

        /// <summary>물류 단독 천장 = origin * ceil. S3req &lt; 천장 &lt; S4밴드 (S4가 강화-only 벽).</summary>
        public float LogisticsCeiling => origin * ceil;
    }
}
