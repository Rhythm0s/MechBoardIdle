namespace MBI.Data
{
    /// <summary>
    /// 벨트 요소 유형(§5-4 L3). 아이템 흐름의 in/out 면 수로 구분한다.
    ///   - Straight : 1-in / 1-out(반대 면).
    ///   - Corner   : 1-in / 1-out(인접 면, 꺾임).
    ///   - Merger   : 다중-in / 1-out(여러 줄기 합류).
    ///   - Sorter   : 1-in / 다중-out(분배 — 라운드로빈 or kind).
    /// 직선/코너는 BeltPath가 자동 배향, 병합기/분류기는 명시 배치(팔레트/에디터).
    /// </summary>
    public enum BeltElementKind
    {
        Straight,
        Corner,
        Merger,
        Sorter
    }
}
