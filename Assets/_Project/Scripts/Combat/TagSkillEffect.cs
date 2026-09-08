using MBI.Data;
using UnityEngine;

namespace MBI.Combat
{
    /// <summary>
    /// 태그 스킬 연출 둘 — **레이저 한 줄기**(B 태그 인)와 **부채꼴 탄환비**(A 태그 인).
    /// 값 아홉은 <see cref="CombatTuning"/>에 있다(2026-09-08 사용자 목업 확정 · <c>260908_W04</c> 2-3).
    ///
    /// **왜 코드가 그리는가.** 둘 다 맵을 한 바퀴 도는 연출이라 스프라이트 한 장으로는
    /// 화면을 못 덮는다 — 캔버스가 화면 대각선보다 작다(<c>260908_W04</c> 2-2).
    /// 그래서 **레이저는 자산이 없고**, 탄환비는 **탄환 한 발(<c>vfx_tagbullet</c>)을 코드가 뿌린다.**
    ///
    /// **회전은 새로 만들지 않는다** — <see cref="ArtSpec.EffectRotationDegrees"/>를 그대로 쓴다.
    /// 연출 아트 요청 문서 2장의 「1방향만 만들고 회전은 코드가 한다」가 그 자리다.
    ///
    /// ⚠️ **한 바퀴만 돈다.** 연출 문서「공통 생성 규칙」이 「한 번 재생 후 사라진다」이고,
    /// 태그 스킬은 태그 인 순간 1회뿐이다(전투 시스템 문서「태그 시스템」).
    ///
    /// ⚠️ **이 연출은 판정에 손대지 않는다.** 피해는 <c>CombatSimulation.TagSkillStrike</c>가
    /// 이미 넣었고, 여기는 그리기만 한다 — 전투 시스템 문서 10-1 「판정 무개입」.
    /// </summary>
    public sealed class TagSkillEffect : MonoBehaviour
    {
        /// <summary>아트 픽셀 → 유닛. PPU 192는 로봇·이펙트가 함께 쓴다(연출 문서 6장).</summary>
        public const float PixelsPerUnit = 192f;

        private const float FullTurn = 360f;

        private CombatTuning _tuning;
        private Sprite _bulletSprite;
        private Color _color;
        private float _radius;

        private bool _isLaser;
        private float _elapsed;
        private float _sweepSeconds;

        private Transform _beam;
        private Transform[] _bullets;
        private float[] _bulletAngles;

        /// <summary>레이저(B 태그 인) — 빔 한 줄기가 한 바퀴 돈다.</summary>
        public static TagSkillEffect PlayLaser(Transform parent, Vector2 origin, float radius,
                                               CombatTuning tuning, Sprite white, Color color)
        {
            TagSkillEffect fx = Create(parent, origin, radius, tuning, color);
            fx._isLaser = true;
            fx._sweepSeconds = tuning != null ? tuning.tagLaserSweepSeconds : 0.40f;

            float widthPx = tuning != null ? tuning.tagLaserWidthArtPixels : 53f;
            float width = widthPx / PixelsPerUnit;

            var beam = new GameObject("TagLaserBeam");
            beam.transform.SetParent(fx.transform, false);
            // 피벗이 가운데라 길이의 절반만큼 밀어야 원점에서 뻗는다.
            beam.transform.localPosition = new Vector3(radius * 0.5f, 0f, 0f);
            beam.transform.localScale = new Vector3(radius, width, 1f);
            var sr = beam.AddComponent<SpriteRenderer>();
            sr.sprite = white;
            sr.color = color;
            sr.sortingOrder = SortingLayers.EffectOver;
            fx._beam = fx.transform;
            return fx;
        }

        /// <summary>탄환비(A 태그 인) — 탄환 여럿이 부채꼴로 깔리며 한 바퀴 돈다.</summary>
        /// <param name="nativeSize">
        /// 참이면 스프라이트를 **제 크기 그대로** 둔다(PPU가 크기를 정한다).
        /// ⚠️ 자리표시(흰 사각)는 1유닛짜리라 크기를 줘야 하고, 실제 자산은 이미 크기를 갖는다.
        /// **규격 「탄환 38」이 몸통인지 꼬리까지인지가 아직 안 정해져**(`260908_V05` 판정 요청)
        /// 자산이 들어온 뒤에는 값을 강제하지 않는다 — 지어낸 배율을 넣지 않기 위해서다.
        /// </param>
        public static TagSkillEffect PlayBulletRain(Transform parent, Vector2 origin, float radius,
                                                   CombatTuning tuning, Sprite bullet, Color color,
                                                   bool nativeSize = false)
        {
            TagSkillEffect fx = Create(parent, origin, radius, tuning, color);
            fx._isLaser = false;
            fx._sweepSeconds = tuning != null ? tuning.tagBulletSweepSeconds : 0.40f;
            fx._bulletSprite = bullet;

            int count = tuning != null ? Mathf.Max(1, tuning.tagBulletCount) : 10;
            float fan = tuning != null ? tuning.tagBulletFanDegrees : 120f;
            float sizePx = tuning != null ? tuning.tagBulletSizeArtPixels : 38f;
            float size = sizePx / PixelsPerUnit;

            fx._bullets = new Transform[count];
            fx._bulletAngles = new float[count];
            for (int i = 0; i < count; i++)
            {
                // 부채꼴을 고르게 나눈다 — 손으로 고른 배분을 쓰지 않는다.
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                fx._bulletAngles[i] = (t - 0.5f) * fan;

                var one = new GameObject("TagBullet");
                one.transform.SetParent(fx.transform, false);
                if (!nativeSize) one.transform.localScale = new Vector3(size, size, 1f);
                var sr = one.AddComponent<SpriteRenderer>();
                sr.sprite = bullet;
                sr.color = color;
                sr.sortingOrder = SortingLayers.EffectOver;
                fx._bullets[i] = one.transform;
            }
            return fx;
        }

        private static TagSkillEffect Create(Transform parent, Vector2 origin, float radius,
                                             CombatTuning tuning, Color color)
        {
            var go = new GameObject("TagSkillEffect");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(origin.x, origin.y, 0f);
            TagSkillEffect fx = go.AddComponent<TagSkillEffect>();
            fx._tuning = tuning;
            fx._radius = Mathf.Max(radius, 0.01f);
            fx._color = color;
            return fx;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float turn = _sweepSeconds <= 0f ? 1f : Mathf.Clamp01(_elapsed / _sweepSeconds);
            float angle = turn * FullTurn;

            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            if (!_isLaser && _bullets != null)
            {
                float fallPx = _tuning != null ? _tuning.tagBulletFallArtPixels : 96f;
                float fall = fallPx / PixelsPerUnit;
                for (int i = 0; i < _bullets.Length; i++)
                {
                    // 탄환마다 부채꼴 안의 제 각도에 서고, 반지름은 고르게 흩는다.
                    float rad = _bulletAngles[i] * Mathf.Deg2Rad;
                    float r = _radius * ((i + 1) / (float)(_bullets.Length + 1));
                    // **위에서 떨어진다** — 한 바퀴 도는 동안 낙하분이 0으로 줄어든다.
                    float drop = fall * (1f - turn);
                    _bullets[i].localPosition = new Vector3(
                        Mathf.Cos(rad) * r, Mathf.Sin(rad) * r + drop, 0f);
                }
            }

            // 한 바퀴만 돈다 — 다 돌면 사라진다(반복 없음).
            if (turn >= 1f) Destroy(gameObject);
        }
    }
}
