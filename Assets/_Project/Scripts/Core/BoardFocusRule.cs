using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// **튜토리얼 시야 이동은 카메라가 선 뒤에**(CID_214 · 2026-10-19 · #210 플랜 판단 ⚠️).
    ///
    /// 조립에 들어가면 주 카메라가 전투 자리 → 보드 자리로 몇 프레임에 걸쳐 미끄러진다
    /// (`GameLayerController` · 위치 · 배율 Lerp). 종전 `FocusCell` 은 **들어간 첫 프레임에 한 번** 불려
    /// 미끄러지는 중의 카메라로 화면 좌표를 재 — 판을 엉뚱한 만큼 밀었다. S3 A 마운트는 기본 시야 안이라
    /// 티가 안 났고, S6 B 마운트(2,10)는 보드 위쪽이라 화면 밖에 남았다(8133 7635b57 실측).
    /// 규칙 하나: **카메라가 목표 위치 · 배율에 닿은 뒤** 시야를 옮긴다(A · B 강화 · S5 폭발탄 같은 길).
    /// </summary>
    public static class BoardFocusRule
    {
        /// <summary>닿았다고 보는 위치 차(월드 단위 · ⚠️ 구현 판단).</summary>
        public const float PositionEpsilon = 0.05f;

        /// <summary>닿았다고 보는 배율 차(orthographicSize · ⚠️ 구현 판단).</summary>
        public const float SizeEpsilon = 0.02f;

        public static bool CameraSettled(Vector2 position, Vector2 target, float size, float targetSize) =>
            (position - target).sqrMagnitude <= PositionEpsilon * PositionEpsilon
            && Mathf.Abs(size - targetSize) <= SizeEpsilon;

        /// <summary>이번 프레임에 시야를 옮기는가 — 아직 안 옮겼고 · 보드 화면이고 · 카메라가 섰다.</summary>
        public static bool ShouldFocus(bool alreadyFocused, bool boardView, bool cameraSettled) =>
            !alreadyFocused && boardView && cameraSettled;
    }
}
