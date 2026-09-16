using MBI.Core.Combat;
using UnityEngine;

namespace MBI.Combat
{
    /// <summary>
    /// 피격 VFX 한 벌을 **한 번** 재생하고 스스로 사라진다 (2026-09-16 · 사용자 승인 §74-7 ②).
    ///
    /// ⚠️⚠️ **끝을 코드가 만든다.** 아트가 낸 넷 중 `vfx_hit_explosive` 는 **마지막 칸이
    /// 첫 칸보다 커서 스스로 안 꺼진다**(아트 실측 · 09-16). 마지막 칸에서 그냥 멈추면
    /// **불덩이가 화면에 남는다.** 그래서 뒤쪽 구간을 알파로 뺀다 — 사용자 판정이다.
    ///
    /// 📌 `vfx_hit_standard` 는 마지막 칸 화소가 0 이라 안 빼도 되지만, **셋을 다르게
    /// 다루지 않는다** — 같은 사건을 셋으로 나누면 하나만 고쳐지는 자리가 또 생긴다.
    ///
    /// ⚠️ **길이는 `EffectTiming.HitFlashDuration`(0.35초)을 쓴다** — 새 값을 만들지 않는다.
    /// 그 값은 09-15 에 사용자가 0.12 → 0.35 로 확정한 **피격 표시 길이**이고,
    /// 코드 플래시와 이 그림이 **같은 사건**이므로 같은 길이여야 한다.
    ///
    /// ⚠️ <see cref="SpriteFrameAnimator"/> 를 안 쓴다 — 그쪽은 `UnitAnimClip`(방향·상태가
    /// 있는 캐릭터 벌)을 위한 것이라, 칸 넷짜리 한 번 재생에 끌어다 쓰면 둘 다 흐려진다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitVfxPlayer : MonoBehaviour
    {
        /// <summary>뒤쪽 몇 할을 알파로 빼는가. ⚠️ **가정**(문서에 페이드 절이 없다).</summary>
        public const float FadeTail = 0.4f;

        private SpriteRenderer _renderer;
        private Sprite[] _frames;
        private float _seconds;
        private float _elapsed;
        private Color _tint = Color.white;

        /// <summary>지금 재생 중인가 — 시험이 본다.</summary>
        public bool Playing { get; private set; }

        /// <summary>
        /// 칸 번호. <paramref name="t"/> 는 0~1. **마지막 칸을 넘지 않는다** —
        /// 넘으면 배열 밖을 짚고, 그것은 에러 없이 마지막 프레임에서 예외가 된다.
        /// </summary>
        public static int FrameAt(int count, float t)
        {
            if (count <= 0) return -1;
            int i = Mathf.FloorToInt(Mathf.Clamp01(t) * count);
            return Mathf.Min(i, count - 1);
        }

        /// <summary>
        /// 남은 밝기. 앞 구간은 1 이고 뒤 <see cref="FadeTail"/> 구간에서 0 으로 내려간다.
        /// </summary>
        public static float AlphaAt(float t)
        {
            t = Mathf.Clamp01(t);
            float start = 1f - FadeTail;
            if (t <= start) return 1f;
            return Mathf.Clamp01(1f - (t - start) / FadeTail);
        }

        public void Play(Sprite[] frames, Color tint, float seconds, int sortingOrder)
        {
            if (frames == null || frames.Length == 0) { Destroy(gameObject); return; }

            _frames = frames;
            _tint = tint;
            _seconds = Mathf.Max(0.01f, seconds);
            _elapsed = 0f;
            Playing = true;

            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = sortingOrder;
            Apply();
        }

        private void Update()
        {
            if (!Playing) return;

            _elapsed += Time.deltaTime;
            if (_elapsed >= _seconds)
            {
                Playing = false;
                Destroy(gameObject);
                return;
            }

            Apply();
        }

        private void Apply()
        {
            float t = _elapsed / _seconds;
            int i = FrameAt(_frames.Length, t);
            if (i >= 0) _renderer.sprite = _frames[i];

            Color c = _tint;
            c.a = _tint.a * AlphaAt(t);
            _renderer.color = c;
        }
    }
}
