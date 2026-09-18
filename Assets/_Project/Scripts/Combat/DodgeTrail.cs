using MBI.Core.Combat;
using MBI.Data;
using UnityEngine;

namespace MBI.Combat
{
    /// <summary>
    /// 회피 연출 — **잔상 + 줄기** (2026-09-18 사용자 확정 · 참고 이미지 = 대시 잔상과
    /// 이어지는 빛줄기 · ⚠️ 연출값 전부 가정 · 설계 사후 `vfx_booster` 행 개정).
    ///
    /// 🗑️ **구 연출 폐기가 아니다 — 더한 것이다.** 끝점의 `vfx_booster` 한 방은 그대로
    /// 남고(그림 자산이 있는 유일한 회피 연출이다), 그 앞에 **지나온 자리**가 생긴다.
    /// 종전에는 회피가 「한 자리에서 한 번 번쩍」이라 **어디서 어디로 갔는지**가 안 보였다.
    ///
    /// ⚠️⚠️ **판정에 손대지 않는다.** 무적 0.167초 · 거리 1.25칸은 그대로다 —
    /// 이 파일은 시뮬이 이미 끝낸 이동의 **자국만** 그린다(전투 시스템 문서 10-1 판정 무개입).
    ///
    /// ⚠️ **가산 합성은 자산으로 실어 보낸다.** `Resources/DodgeStreakAdditive.mat` 하나가
    /// 레거시 가산 셰이더를 물고 있어 웹빌드가 그것을 **스트립하지 않는다** —
    /// `Shader.Find` 로 이름만 부르면 **에디터에서는 빛나고 빌드에서는 안 빛난다**
    /// (2026-09-18 설계 지적 · 그 머티리얼은 `DodgeStreakMaterialGenerator` 가 굽는다).
    /// 자산이 없으면 **보통 알파**로 떨어진다 — 덜 화려할 뿐 안 깨진다.
    /// </summary>
    public sealed class DodgeTrail : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private float _seconds;
        private float _elapsed;
        private Color _from;

        /// <summary>
        /// 한 판 띄운다 — 잔상 n 장과 줄기 하나.
        ///
        /// <param name="body">지금 로봇이 쓰는 그림. <c>null</c> 이면 잔상을 안 그린다 —
        /// **없는 그림을 흰 사각으로 대신하지 않는다**(그러면 회피마다 흰 상자가 남는다).</param>
        /// </summary>
        public static void Play(Transform parent, Vector2 from, Vector2 to,
                                Sprite body, bool flipX, Vector3 bodyScale,
                                CombatTuning tuning)
        {
            if (parent == null) return;

            float length = DodgeTrailRule.StreakLength(from, to);
            if (length <= 0.001f) return;   // 제자리 회피 — 그릴 자국이 없다

            int count = tuning != null && tuning.dodgeAfterimageCountTbd > 0
                ? tuning.dodgeAfterimageCountTbd : DodgeTrailRule.DefaultAfterimages;
            float seconds = tuning != null && tuning.dodgeTrailSecondsTbd > 0f
                ? tuning.dodgeTrailSecondsTbd : 0.4f;
            float peak = tuning != null ? Mathf.Clamp01(tuning.dodgeAfterimageAlphaTbd) : 0.45f;

            Color tint = BoosterTint;

            // ── ① 잔상 ──
            if (body != null)
            {
                Vector2[] spots = DodgeTrailRule.AfterimagePositions(from, to, count);
                for (int i = 0; i < spots.Length; i++)
                {
                    float a = DodgeTrailRule.AfterimageAlpha(i, spots.Length, peak);
                    var one = Make(parent, spots[i], body,
                        new Color(tint.r, tint.g, tint.b, a),
                        // ⚠️ **로봇 바로 아래**다(사용자 지시) — 위에 두면 본체를 가린다.
                        SortingLayers.Actor - 1, seconds, additive: false);
                    one.transform.localScale = bodyScale;
                    var sr = one.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.flipX = flipX;
                }
            }

            // ── ② 줄기 — 시작점과 끝점을 잇는다 ──
            {
                Vector2 center = DodgeTrailRule.StreakCenter(from, to);
                var streak = Make(parent, center, PlaceholderSprite.Streak(),
                    new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(peak + 0.25f)),
                    SortingLayers.Actor - 1, seconds, additive: true);

                // 그림은 1유닛 길이라 **길이가 곧 x 배율**이다. 두께는 가정.
                float thick = tuning != null && tuning.dodgeStreakThicknessTbd > 0f
                    ? tuning.dodgeStreakThicknessTbd : 0.35f;
                streak.transform.localScale = new Vector3(length, thick, 1f);
                streak.transform.rotation =
                    Quaternion.Euler(0f, 0f, DodgeTrailRule.StreakDegrees(from, to));
            }
        }

        /// <summary>회피 주황 — 플레이어 색 축이다. ⚠️ 값은 가정(설계 역기입 자리).</summary>
        private static readonly Color BoosterTint = new Color(1f, 0.62f, 0.25f);

        private static GameObject Make(Transform parent, Vector2 pos, Sprite sprite,
                                       Color color, int sortingOrder, float seconds, bool additive)
        {
            var go = new GameObject("DodgeTrail");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            if (additive) ApplyAdditive(sr);

            var t = go.AddComponent<DodgeTrail>();
            t._sr = sr;
            t._seconds = Mathf.Max(0.01f, seconds);
            t._from = color;
            return go;
        }

        /// <summary>
        /// 가산 합성을 **있으면** 건다.
        ///
        /// ⚠️⚠️ **`Shader.Find` 로 찾지 않는다**(2026-09-18 · 설계 지적). 그 이름을 무는
        /// 머티리얼이 하나도 없으면 웹빌드가 셰이더를 **통째로 스트립**해서 `null` 이 된다 —
        /// **에디터에서는 빛나고 빌드에서는 안 빛나는** 갈림이 그렇게 난다.
        ///
        /// 📌 그래서 `Resources/` 에 **머티리얼 자산 하나**를 두고 그것을 읽는다. 자산이
        /// 셰이더를 물고 있으므로 빌드가 둘 다 싣는다(`DodgeStreakMaterialGenerator` 가 굽는다).
        ///
        /// ⚠️ **자산이 없어도 안 깨진다** — 못 찾으면 아무것도 안 하고 보통 알파로 그려진다.
        ///    없는 셰이더를 억지로 물리면 **분홍 사각**이 되는데, 그건 덜 화려한 것보다 나쁘다.
        ///
        /// ⚠️ 머티리얼을 **한 벌만 만들어 나눠 쓴다** — 회피마다 새로 지으면 잔상 하나에
        ///    머티리얼 하나가 새어 나간다.
        /// </summary>
        private static void ApplyAdditive(SpriteRenderer sr)
        {
            if (!_additiveLooked)
            {
                _additiveLooked = true;
                _additive = Resources.Load<Material>("DodgeStreakAdditive");
            }
            if (_additive != null) sr.sharedMaterial = _additive;
        }

        private static Material _additive;
        private static bool _additiveLooked;

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(_elapsed / _seconds);

            // ⚠️ **이동보다 길게 사라진다**(사용자 지시) — 회피는 0.167초인데 자국은 0.4초다.
            //    그래야 멈춘 뒤에도 「방금 저기서 왔다」가 읽힌다.
            if (_sr != null)
                _sr.color = new Color(_from.r, _from.g, _from.b, _from.a * (1f - u));

            if (u >= 1f) Destroy(gameObject);
        }
    }
}
