using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 연출 타이밍 계산(UI 문서「연출 표현 규칙」, 260825_V01 §3). 순수 함수 — 씬 비의존이라 검증된다.
    ///
    /// 두 항목 모두 **스프라이트를 늘리거나 갈아끼우지 않는다.** 아트가 한 덩어리로 확정됐고
    /// (V01 §4-1 파츠 분리 폐기) 프레임 상한이 9라, 연출은 변형이 아니라 **위치와 색**으로 낸다.
    /// </summary>
    public static class EffectTiming
    {
        /// <summary>반동 거리(월드 유닛). 2픽셀 — PPU에서 파생되므로 규격이 바뀌면 함께 따라온다.</summary>
        public const float RecoilPixels = 2f;
        public static float RecoilDistance => RecoilPixels / ArtSpec.PixelsPerUnit;

        /// <summary>반동 왕복 시간(초). 밀렸다 돌아오는 한 사이클.</summary>
        public const float RecoilDuration = 0.09f;

        /// <summary>피격 점멸 지속(초). **세기는 일정하다** — 로봇에 방어력이 없어 받는 피해가
        /// 몬스터 공격력 그대로이므로, 세기로 정도를 표현하면 없는 정보를 지어내는 것이 된다.</summary>
        public const float HitFlashDuration = 0.12f;

        /// <summary>
        /// 발사 반동 오프셋. 표적 **반대 방향**으로 밀렸다가 복귀한다.
        /// 0 → 최대 → 0의 삼각 곡선: 튕겨 나갔다 돌아오는 것이 한 동작으로 읽힌다.
        /// </summary>
        public static Vector2 RecoilOffset(Vector2 fireDirection, float elapsed)
        {
            if (elapsed < 0f || elapsed >= RecoilDuration) return Vector2.zero;
            if (fireDirection.sqrMagnitude < 1e-8f) return Vector2.zero;

            float t = elapsed / RecoilDuration;
            float amount = t < 0.5f ? t * 2f : (1f - t) * 2f; // 앞 절반 밀림, 뒤 절반 복귀
            return -fireDirection.normalized * (RecoilDistance * amount);
        }

        /// <summary>
        /// 피격 점멸 색. 빨강 ↔ 하양을 오간다. 세기가 아니라 **있고 없고**만 말한다.
        /// 점멸이 끝나면 기본색을 그대로 돌려준다.
        /// </summary>
        public static Color HitFlashColor(Color baseColor, float elapsed)
        {
            if (elapsed < 0f || elapsed >= HitFlashDuration) return baseColor;

            // 지속을 4등분해 빨강·하양이 두 번 교차한다 — 한 번만 깜빡이면 눈에 안 걸린다.
            int phase = Mathf.FloorToInt(elapsed / (HitFlashDuration * 0.25f));
            return (phase % 2 == 0) ? Color.red : Color.white;
        }

        /// <summary>
        /// 바닥 그림자 크기(월드 유닛). 탑뷰에는 높이가 없어 **크기와 그림자로 위조**한다.
        /// 가로는 본체 폭의 70%, 세로는 그 40% — 납작해야 바닥에 누운 것으로 읽힌다.
        /// </summary>
        public static Vector2 ShadowSize(float bodySize) =>
            new Vector2(bodySize * 0.7f, bodySize * 0.28f);

        /// <summary>그림자가 놓이는 발밑 오프셋(본체 중심 기준). 캔버스 하단 근처.</summary>
        public static float ShadowFootOffset(float bodySize) => -bodySize * 0.42f;
    }
}
