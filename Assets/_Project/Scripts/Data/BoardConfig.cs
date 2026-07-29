using UnityEngine;

namespace MBI.Data
{
    /// <summary>
    /// 물류 보드 레이아웃 설정(§5-3) — 격자 치수·셀 크기.
    ///
    /// BalanceConfig(밸런스 앵커, balance.json 미러)와 분리한다:
    /// 레이아웃은 밸런스 원천과 무관한 Unity측 배치 설정이며, BalanceConfig에 섞으면
    /// BalanceAssetGenerator 재실행이 덮어쓰고 계약이 오염된다(§3 한 파일=한 책임, §7 드리프트).
    ///
    /// §3: 치수/셀 크기 하드코딩 금지 → 이 직렬화 필드가 유일 원천. 인스펙터에서 조정.
    /// 값은 레이아웃 placeholder다(밸런스 아님) — 카메라 orthographicSize 5(가시 높이 10)에
    /// 8×8·cellSize 1 격자가 들어맞는 기본값.
    /// </summary>
    [CreateAssetMenu(fileName = "BoardConfig", menuName = "MBI/Board Config", order = 2)]
    public sealed class BoardConfig : ScriptableObject
    {
        [Header("격자 치수 (셀 개수)")]
        [Tooltip("가로 셀 개수(열). 유효 셀 x ∈ [0, columns).")]
        [Min(1)] public int columns = 8;
        [Tooltip("세로 셀 개수(행). 유효 셀 y ∈ [0, rows).")]
        [Min(1)] public int rows = 8;

        [Header("셀 크기")]
        [Tooltip("셀 한 변의 월드 길이(유닛). 월드↔셀 변환 배율.")]
        [Min(0.01f)] public float cellSize = 1f;
    }
}
