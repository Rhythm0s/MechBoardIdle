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

        // ---- 탄약 소진 아이콘 (2026-09-10 사용자 확정 · 촬영 전 임시) ----

        /// <summary>
        /// 아이콘을 몇 배로 줄여 그리는가의 **상한**.
        ///
        /// **왜 줄여야 하는가.** `vfx_ammoout` 은 **256 캔버스에 실루엣 212px** 로,
        /// 로봇 실루엣 **220px** 과 거의 같다. 그대로 두면 아이콘이 로봇만 해져
        /// 「무엇이 멈췄는지」보다 **「무언가 가려졌다」가 먼저 읽힌다.**
        ///
        /// 상한을 0.52 로 잡은 근거 — 아이콘 실루엣이 로봇 실루엣의 **절반 이하**가 되어야 하고,
        /// 아이콘은 캔버스의 212/256, 로봇은 220/256 을 쓰므로
        /// <c>배율 × 212 ≤ 220 ÷ 2</c> → 배율 ≤ **0.519** 다.
        ///
        /// ⚠️ **이 숫자는 실측이지만 규정이 아니다.** 연출 문서에 아이콘 크기 절이 없어
        /// 구현이 가정으로 넣었다 — 설계가 역기입한다.
        /// </summary>
        public const float AmmoOutScaleMax = 0.52f;

        /// <summary>고른 배율을 쓸 수 있는 값으로 자른다. 0 이하면 아이콘이 사라진다.</summary>
        public static float AmmoOutScale(float requested) =>
            UnityEngine.Mathf.Clamp(requested, 0.05f, AmmoOutScaleMax);

        /// <summary>
        /// 아이콘이 놓이는 자리(본체 중심 기준) — **발 아래**다.
        ///
        /// 그림자가 쓰는 발밑(<see cref="ShadowFootOffset"/>)보다 **더 내려간다.**
        /// 그 자리는 아직 실루엣 안이라, 거기에 두면 아이콘 위쪽이 다리를 덮는다.
        /// 여기서는 **본체 캔버스 아래쪽 끝**에서 아이콘 높이의 절반만큼 더 내려
        /// 아이콘이 통째로 몸통 밖에 놓이게 한다.
        ///
        /// ⚠️ 그림자와 겹치는 것은 괜찮다 — 층이 갈려 있어(EffectOver 대 EffectUnder)
        /// 아이콘이 위에 그려진다.
        /// </summary>
        public static float AmmoOutFootOffset(float bodySize, float iconSize) =>
            -bodySize * 0.5f - iconSize * 0.5f;
    }
}
