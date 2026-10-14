using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// **카메라 시야를 정하는 셈 한 곳** (2026-10-14 사용자 육안 「보드가 계속 줌인·줌아웃된다」).
    ///
    /// ⚠️⚠️ **까닭은 임자가 둘이었던 것이다**(지침 §7 「한 값이 두 곳에 살면 답이 둘이 된다」).
    /// 매 프레임
    ///   · `GameLayerController` 가 `orthographicSize` 를 **`boardSize` 쪽으로 Lerp** 하고,
    ///   · `BoardController.ApplyZoom` 이 같은 값을 **`_baseOrtho / _zoom`** 으로 덮었다.
    /// 둘의 `Update` 순서는 정해져 있지 않아 프레임마다 이긴 쪽이 바뀌었고,
    /// 그것이 화면에서 **줌인·줌아웃이 번갈아 뛰는** 그림이 됐다.
    /// 그 위에 `_baseOrtho` 는 **그 싸움 중간값**을 집어 와서 기준조차 틀어져 있었다.
    ///
    /// 📌 이제 **쓰는 곳은 `GameLayerController` 하나**다. 보드의 배율은 값을 덮지 않고
    /// <see cref="BoardZoomSignals"/> 에 **곱할 수**로 올려 두고, 목표 시야를 여기서 낸다.
    /// </summary>
    public static class BoardZoomRule
    {
        /// <summary>
        /// 지금 목표 시야(세로 절반).
        ///
        /// 배율이 클수록 **가까이** 보이므로 시야는 그만큼 **작아진다**.
        /// 전투 화면에는 배율이 안 걸린다 — 조립을 확대해 둔 채 나갔다고 전투까지
        /// 확대되면 안 된다(종전 `ApplyZoom` 주석이 지키던 것이고, 그대로 지킨다).
        /// </summary>
        public static float TargetOrtho(bool boardView, float boardSize, float combatSize, float zoom)
        {
            if (!boardView) return combatSize;
            return boardSize / Mathf.Max(0.01f, zoom);
        }
    }

    /// <summary>
    /// 보드 배율 — **보는 쪽이 하나**라 신호로 올린다(2026-10-14).
    ///
    /// ⚠️ 1 이 기본이고 1 보다 크면 확대다. 보드를 안 보는 동안에도 값은 남는다 —
    /// 다시 들어갔을 때 아까 보던 배율이 그대로여야 한다.
    /// </summary>
    public static class BoardZoomSignals
    {
        public static float Factor = 1f;

        public static void Reset() => Factor = 1f;
    }
}
