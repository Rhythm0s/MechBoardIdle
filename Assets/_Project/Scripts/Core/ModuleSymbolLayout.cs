using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 노드 타일 안에서 모듈 기호 둘이 앉는 자리 (2026-09-09 신설 · `260909_W01` 5장).
    /// **규칙만 낸다** — 그리는 일은 보드가 한다.
    ///
    /// W01 5장이 정한 것: **노드 타일 안쪽 아래에 둘을 나란히.** 포트 마커 막대가 네 변
    /// 24픽셀을 쓰므로 안쪽 <b>144</b>만 비어 있고, 64짜리 둘은 128이라 들어간다.
    /// **왼쪽부터 채우고 빈 칸은 그리지 않는다.**
    ///
    /// ⚠️ **밝기 축을 여기에 곱하지 않는다**(W01 5장). 산출률은 노드의 속성이고
    /// **모듈이 붙어 있는가는 그것과 무관하다** — 노드가 멈춰도 모듈은 그대로 붙어 있다.
    ///
    /// ⚠️ **글자를 대신하는 것이다.** 종전에는 「M」·「R」을 라벨로 얹었는데, 정지 상태에
    /// 0.4가 곱해지면 **글자는 색이 아니라 대비로 읽히므로 가장 먼저 사라진다**(W01 5장).
    /// </summary>
    public static class ModuleSymbolLayout
    {
        /// <summary>기호 캔버스(px). 품목과 같은 값이라 규격 문서에 새 행이 없다.</summary>
        public const int SymbolCanvas = 64;

        /// <summary>포트 마커 막대가 먹는 변 두께(px). 네 변에 같은 값이 붙는다.</summary>
        public const int PortBarPixels = 24;

        /// <summary>기호가 들어갈 수 있는 안쪽 폭(px) = 192 − 24 × 2 = 144.</summary>
        public const int InnerPixels = ArtSpec.TileCanvas - PortBarPixels * 2;

        /// <summary>기호 둘이 나란히 차지하는 폭(px) = 128. 안쪽 144에 들어간다.</summary>
        public const int RowPixels = SymbolCanvas * NodeInstance.ModuleSlots;

        /// <summary>기호 한 장의 크기(월드). 한 칸이 1이므로 64/192 = 1/3이다.</summary>
        public static float SymbolSize => ArtSpec.WorldSize(SymbolCanvas);

        /// <summary>
        /// 칸 한가운데를 원점으로 한 <paramref name="slot"/>번째 기호의 자리(월드).
        ///
        /// 가로는 **둘이 이루는 128 블록을 안쪽 144 가운데에 놓고 왼쪽부터** 채운다.
        /// 세로는 **안쪽 아래에 바닥을 맞춘다** — 아래 변 24를 넘지 않는다.
        /// </summary>
        public static Vector2 SlotOffset(int slot)
        {
            float px = ArtSpec.PixelsPerUnit;

            // 가로 — 블록 왼쪽 끝에서 슬롯 중심까지.
            float rowLeft = -RowPixels * 0.5f;
            float x = rowLeft + SymbolCanvas * (slot + 0.5f);

            // 세로 — 안쪽 아래 변에 바닥을 붙인다.
            float innerBottom = -InnerPixels * 0.5f;
            float y = innerBottom + SymbolCanvas * 0.5f;

            return new Vector2(x / px, y / px);
        }

        /// <summary>
        /// 그 칸에 기호를 그리는가. **빈 칸은 그리지 않는다**(W01 5장) —
        /// 자리표시를 남기면 「붙일 수 있는 칸이 둘」이 아니라 「모듈이 둘」로 읽힌다.
        /// </summary>
        public static bool SlotIsVisible(ModuleDefinition module) => module != null;

        /// <summary>
        /// 기호에 곱할 색. **강조색을 쓰지 않는다**(W01 5장) — 모듈은 계열 셋 어느 쪽에도
        /// 안 속한다. 명도로만 가르므로 코드는 흰색을 그대로 준다.
        /// </summary>
        public static Color Tint => Color.white;
    }
}
