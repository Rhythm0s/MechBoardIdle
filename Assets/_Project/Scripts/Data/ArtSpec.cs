namespace MBI.Data
{
    /// <summary>
    /// 도트 아트 임포트 규격(260824_V02 §4 승인). **한 곳에서만 정의한다.**
    ///
    /// PPU 192의 근거: 노드·벨트 타일 캔버스가 192×192이고 격자 한 칸이 192 화면 픽셀이다
    /// (2304 ÷ 12 = 192, 2496 ÷ 13 = 192). 따라서 **타일 스프라이트 1장 = 1 월드 유닛 = 정확히 한 칸**이 되어
    /// 격자 좌표와 월드 좌표가 1:1로 붙는다 — 배치·스냅에 배율 보정이 끼지 않는다.
    ///
    /// 여기 있는 것은 밸런스가 아니라 아트 규격이라 SO가 아니라 상수다.
    /// AssetPostprocessor가 임포트 시점에 읽어야 하는데, 그 시점에는 SO 로드를 보장할 수 없다.
    ///
    /// 캔버스 → 월드 크기(= 캔버스 ÷ PPU):
    ///   노드·벨트 192 → 1.000칸 · 로봇 A/B 256 → 1.333칸
    ///   합체·보스 512 → 2.667칸 · 몬스터 128 → 0.667칸 · 드론 64 → 0.333칸
    ///
    /// 카메라: orthographicSize = (뷰포트 세로 픽셀 ÷ 2) ÷ PPU.
    /// </summary>
    public static class ArtSpec
    {
        /// <summary>픽셀 퍼 유닛. 격자 한 칸 = 192px = 1 월드 유닛.</summary>
        public const float PixelsPerUnit = 192f;

        // ---- 캔버스 규격(px) ----
        public const int TileCanvas = 192;     // 노드 · 벨트
        public const int RobotCanvas = 256;    // 로봇 A · B
        public const int LargeCanvas = 512;    // 합체 로봇 · 보스
        public const int MonsterCanvas = 128;  // 몬스터 3종
        public const int DroneCanvas = 64;     // 드론 2종

        /// <summary>캔버스(px) → 월드 유닛. localScale은 1로 두고 크기는 캔버스가 결정한다.</summary>
        public static float WorldSize(int canvasPixels) => canvasPixels / PixelsPerUnit;

        // ---- 미리 계산된 월드 크기(플레이스홀더 localScale에 그대로 쓴다) ----
        // 아트 교체 전에도 실물과 같은 자리를 차지하게 해서, 교체 시 레이아웃이 흔들리지 않게 한다.
        public static float TileSize => WorldSize(TileCanvas);        // 1.000
        public static float RobotSize => WorldSize(RobotCanvas);      // 1.333
        public static float LargeSize => WorldSize(LargeCanvas);      // 2.667
        public static float MonsterSize => WorldSize(MonsterCanvas);  // 0.667
        public static float DroneSize => WorldSize(DroneCanvas);      // 0.333

        /// <summary>보드용 스프라이트인가(캔버스가 타일 규격의 배수여야 하는 폴더).</summary>
        public static bool IsBoardArtPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            return assetPath.Contains("/Nodes/") || assetPath.Contains("/Belts/");
        }
    }
}
