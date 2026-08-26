namespace MBI.Data
{
    /// <summary>
    /// 그리기 순서 7층(260826_V01 §B 확정). **한 곳에서만 정의한다.**
    ///
    ///   배경 → 타일 → 하단 이펙트 → 액터 → 상단 이펙트 → HUD → 컷인
    ///
    /// **하단 이펙트가 따로 필요한 이유:** 바닥 그림자가 액터보다 아래여야 한다.
    /// 이펙트를 한 층으로 두면 그림자가 로봇 위에 올라가 높이 위조가 뒤집힌다.
    ///
    /// 층 간격을 10으로 벌려 둔 것은 같은 층 안에서 미세 조정(HP바를 본체 위로 등)을 할
    /// 자리를 남기기 위해서다. 층 경계를 넘지 않는 한 자유롭게 ±1~9를 쓴다.
    /// 층별 자산 배정은 자산 레지스트리의 `깊이` 열이 원천이다.
    /// </summary>
    public static class SortingLayers
    {
        /// <summary>배경 — 아레나 바닥, 보드 배경 패널.</summary>
        public const int Background = -30;

        /// <summary>타일 — 노드·벨트 등 격자 위에 놓인 것.</summary>
        public const int Tile = -20;

        /// <summary>하단 이펙트 — **바닥 그림자.** 액터보다 아래여야 한다.</summary>
        public const int EffectUnder = -10;

        /// <summary>액터 — 로봇·몬스터·드론 본체.</summary>
        public const int Actor = 0;

        /// <summary>상단 이펙트 — 탄선·피격 플래시·폭발.</summary>
        public const int EffectOver = 10;

        /// <summary>HUD — 체력바·게이지·경고 아이콘·모드 표시.</summary>
        public const int Hud = 20;

        /// <summary>컷인 — 합체 연출 등 화면을 덮는 것.</summary>
        public const int Cutin = 30;

        /// <summary>층 간 간격. 같은 층 안의 미세 조정은 이 값을 넘지 않아야 한다.</summary>
        public const int Step = 10;
    }
}
