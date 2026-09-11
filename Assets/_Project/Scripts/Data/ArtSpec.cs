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

        /// <summary>
        /// 보스 뷰 배율 — **정수 2배** (2026-09-10 사용자 확정).
        ///
        /// **벌은 256 이고 코드가 2배로 키운다.** 512 벌을 새로 뽑는 대신 이 길을 고른 것은,
        /// 승인본이 256 이고 아트가 회전·사망을 **그 위에** 뽑기 때문이다 — 앵커를 512 로
        /// 바꾸면 승인받은 그림이 앵커가 아니게 된다.
        ///
        /// **정수배만 쓴다.** 1.5 배 같은 것을 곱하면 도트가 이웃 픽셀에 반씩 걸쳐
        /// 실루엣 가장자리가 뭉개진다. 2 배는 한 픽셀이 정확히 네 픽셀이 된다.
        ///
        /// 실루엣 실측(아트) — **256 승인본 142×132** · 구 512 스틸 414×384.
        /// 256 승인본을 2 배로 키우면 284×264 이고, 보스 문서의 **「화면 폭 35.6%」**
        /// (1440 × 0.356 = 513px) 에는 아직 못 미친다. **금요일 2차 육안 뒤 3 으로 올릴 수 있다** —
        /// 3 배면 426×396 으로 구 512 스틸의 실루엣과 거의 같아진다.
        /// **올릴 곳은 이 상수 하나**이며, 고친 뒤 생성기를 다시 돌리면 SO 로 퍼진다.
        ///
        /// ⚠️ **크기의 원천은 여전히 캔버스다.** 이 배율은 「캔버스가 모자란 것을 메우는 값」이지
        /// 크기를 정하는 두 번째 축이 아니다. 512 벌이 오는 날 이 값은 1 로 돌아간다.
        /// </summary>
        public const int BossViewScale = 2;

        /// <summary>보스가 화면에서 차지하는 크기 = 벌 캔버스(256) × 배율. 지금은 2.667칸.</summary>
        public static float BossViewSize => WorldSize(RobotCanvas) * BossViewScale;

        // ---- 이펙트 규격(260826_V01 §C 확정) ----

        /// <summary>
        /// 이펙트 최대 프레임 수. **1회 재생 후 소멸, 반복 없음.**
        /// 상한을 두는 이유는 이펙트가 애니메이션이 아니라 사건의 표시이기 때문이다 —
        /// 길어지면 다음 사건과 겹쳐 무엇이 일어났는지 읽히지 않는다.
        /// </summary>
        public const int EffectMaxFrames = 6;

        /// <summary>
        /// 이펙트는 **1방향만 생성하고 회전은 코드가 준다.**
        /// 방향마다 그리면 자산이 방향 수만큼 늘고, 각도가 어긋나면 그 방향만 틀어진다.
        /// 회전 적용 지점 = 이 함수 하나 — 탄선·피격·폭발이 전부 여기를 통과한다.
        /// </summary>
        public static float EffectRotationDegrees(UnityEngine.Vector2 direction)
        {
            if (direction.sqrMagnitude < 1e-8f) return 0f;
            return UnityEngine.Mathf.Atan2(direction.y, direction.x) * UnityEngine.Mathf.Rad2Deg;
        }

        /// <summary>
        /// UI 그릇 9-슬라이스 여백(px) — **원본 64 에 16** (`260911_W02` 9장).
        ///
        /// ⚠️ **그림이 바뀌면 이 값도 같이 바뀐다.** 여백은 그림에 그려진 것이라
        /// 코드가 정하는 값이 아니라 **그림을 받아 적는 값**이다.
        /// </summary>
        public const int UiPlateBorder = 16;

        /// <summary>
        /// UI 그릇 스프라이트인가 — **9-슬라이스 여백을 강제하는 자리**.
        ///
        /// 아이콘(`icon_*`)은 제외한다. 아이콘은 늘려 쓰지 않으므로 여백을 주면
        /// **가운데만 늘어나 그림이 찌그러진다.**
        /// </summary>
        public static bool IsUiPlatePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            return assetPath.Contains("/UI/") && assetPath.Contains("ui_plate_");
        }

        /// <summary>보드용 스프라이트인가(캔버스가 타일 규격의 배수여야 하는 폴더).</summary>
        public static bool IsBoardArtPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            return assetPath.Contains("/Nodes/") || assetPath.Contains("/Belts/");
        }
    }
}
