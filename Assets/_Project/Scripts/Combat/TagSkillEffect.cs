using MBI.Data;
using UnityEngine;

namespace MBI.Combat
{
    /// <summary>
    /// 태그 스킬 연출 둘 — **화면을 덮는 탄환비**(A 태그 인)와 **화면을 덮는 섬광**(B 태그 인).
    ///
    /// ✅ **2026-09-18 사용자 확정 · 육안 뒤** — 「소용돌이처럼 하지 말고 **화면 전체에**」.
    /// 🗑️ 구 연출 셋을 차례로 폐기한 자리다 —
    ///   ① 09-08 「한 바퀴 도는 부채꼴·레이저」(09-16 사용자 육안에서 폐기 · 적 없는 쪽에도 깔렸다)
    ///   ② 09-16 「적 무리 방향 한 번」(09-18 폐기 · 판정은 화면 전부인데 그림만 한쪽이었다)
    ///   ③ 09-18 오전 「원점에서 360도로 뻗는 탄환·표적마다 빔」(같은 날 육안에서 폐기 —
    ///      **원점에서 뻗으면 무엇을 그리든 소용돌이로 읽힌다.** 화면 전체는 방사가 아니다)
    ///
    /// 📌 그래서 지금은 **원점을 안 쓴다.** 두 연출 다 **화면 사각형**을 받아 그 안을 덮는다 —
    /// 탄환비는 화면 전체에 흩뿌리고, 섬광은 화면 한 장을 덮었다 걷힌다.
    ///
    /// ⚠️ **한 번만 재생하고 사라진다**(연출 문서「공통 생성 규칙」) — 태그 인 순간 1회다.
    ///
    /// ⚠️ **이 연출은 판정에 손대지 않는다.** 피해는 <c>CombatSimulation.TagSkillStrike</c>가
    /// 이미 넣었고 여기는 그리기만 한다 — 전투 시스템 문서 10-1 「판정 무개입」.
    /// 그리는 범위(화면)는 **판정이 쓰는 범위와 같은 사각형**이다(러너가 카메라에서 재서 준다).
    /// </summary>
    public sealed class TagSkillEffect : MonoBehaviour
    {
        /// <summary>아트 픽셀 → 유닛. PPU 192는 로봇·이펙트가 함께 쓴다(연출 문서 6장).</summary>
        public const float PixelsPerUnit = 192f;

        private CombatTuning _tuning;
        private Color _color;

        private bool _isFlash;
        private bool _isSweep;
        private float _elapsed;
        private float _sweepSeconds;

        private Transform[] _bullets;
        private Vector2[] _bulletSpots;
        private int _waves = 1;
        private float _waveSeconds = 0.22f;
        private SpriteRenderer _flash;

        // ── 빔 + 파동 (2026-09-21 사용자 확정) ────────────────────────────
        private SpriteRenderer _beam;
        private float _beamSeconds;
        private SpriteRenderer[] _rings;
        private float _ringInterval;
        private float _ringSeconds;
        private float _ringMaxRadius;
        private float _ringSpritePixels = 256f;

        // ── 자리를 고르는 법 — 순수 계산이라 시험이 여기를 잰다 ──────────────

        /// <summary>
        /// 화면을 **고르게 덮는 자리들**. 격자로 나눈 뒤 칸 안에서 조금씩 흔든다.
        ///
        /// 📌 **격자만 쓰면 도열해 보이고, 난수만 쓰면 뭉친다.** 칸마다 하나씩 두되
        /// 칸 안에서 흔들면 「쏟아졌다」로 읽히면서도 빈 구석이 안 생긴다.
        ///
        /// ⚠️ **흔드는 값은 자리에서 만든다**(<see cref="Jitter"/>) — `Random` 을 쓰면
        /// 같은 입력이 판마다 다른 그림을 내서 **시험이 값을 못 잰다.**
        /// </summary>
        public static Vector2[] ScatterPositions(Rect screen, int count)
        {
            count = Mathf.Max(1, count);
            int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt(count / (float)cols);

            float cw = screen.width / cols;
            float ch = screen.height / rows;

            var spots = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                int cx = i % cols;
                int cy = i / cols;

                // 칸 한가운데에서 반 칸의 ±40% 안으로만 흔든다 — 칸 밖으로 나가면 다시 뭉친다.
                float jx = (Jitter(i * 2) - 0.5f) * 0.8f * cw;
                float jy = (Jitter(i * 2 + 1) - 0.5f) * 0.8f * ch;

                spots[i] = new Vector2(
                    screen.xMin + (cx + 0.5f) * cw + jx,
                    screen.yMin + (cy + 0.5f) * ch + jy);
            }
            return spots;
        }

        /// <summary>0~1 사이의 **되풀이되는** 흔들림 값. 같은 n 이면 늘 같은 수다.</summary>
        public static float Jitter(int n)
        {
            // 정수 하나를 섞어 소수부만 쓴다 — 씨앗도 상태도 없다.
            float x = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }

        // ── A 태그 인 — 화면을 덮는 탄환비 ───────────────────────────────────

        /// <param name="nativeSize">
        /// 참이면 스프라이트를 **제 크기 그대로** 둔다(PPU가 크기를 정한다).
        /// ⚠️ 자리표시(흰 사각)는 1유닛짜리라 크기를 줘야 하고, 실제 자산은 이미 크기를 갖는다.
        /// </param>
        public static TagSkillEffect PlayBulletRain(Transform parent, Rect screen,
                                                   CombatTuning tuning, Sprite bullet, Color color,
                                                   bool nativeSize = false)
        {
            TagSkillEffect fx = Create(parent, screen.center, tuning, color);
            fx._isFlash = false;
            fx._sweepSeconds = tuning != null ? tuning.tagBulletSweepSeconds : 0.40f;

            int count = tuning != null ? Mathf.Max(1, tuning.tagBulletFullCircleCountTbd) : 30;
            float sizePx = tuning != null ? tuning.tagBulletSizeArtPixels : 38f;
            // ⚠️ **크게**(2026-09-18 사용자 리허설 ⑦) — 38 아트 픽셀은 화면에서 점이었다.
            float sizeMult = tuning != null && tuning.tagBulletSizeMultTbd > 0f
                ? tuning.tagBulletSizeMultTbd : 1f;
            float size = sizePx * sizeMult / PixelsPerUnit;

            // **파도 여럿이 빠르게 내려온다**(사용자 ⑦). 탄환마다 제 파도에 속한다.
            fx._waves = tuning != null ? Mathf.Max(1, tuning.tagBulletWavesTbd) : 3;
            fx._waveSeconds = tuning != null && tuning.tagBulletWaveSecondsTbd > 0f
                ? tuning.tagBulletWaveSecondsTbd : 0.22f;
            fx._sweepSeconds = fx._waveSeconds * fx._waves;

            fx._bulletSpots = ScatterPositions(screen, count);
            fx._bullets = new Transform[count];
            for (int i = 0; i < count; i++)
            {
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

        // ── B 태그 인 — 화면을 덮는 섬광 ─────────────────────────────────────

        /// <summary>
        /// 화면 한 장을 덮었다가 걷히는 빛.
        ///
        /// 🗑️ 구 「레이저 빔」 폐기(2026-09-18 육안) — 원점에서 뻗는 그림은 줄기가 몇이든
        /// **소용돌이·폭죽으로 읽힌다.** B 의 태그 스킬도 치는 것은 화면 안 적 전부이므로
        /// 화면 한 장이 그 사실에 더 가깝다.
        ///
        /// ⚠️ **처음이 가장 밝고 뒤로 갈수록 걷힌다.** 반대로 두면 화면이 「덮여 가는」
        /// 것처럼 보여 다음 화면이 안 보인다.
        /// </summary>
        /// <summary>
        /// **적 무리의 무게중심** — 빔이 향할 곳 (2026-09-21).
        ///
        /// ⚠️ **화면 안에 있는 산 적만 센다** — 판정이 화면 안 전부에 나누므로 겨냥도
        /// 같은 무리를 봐야 한다. 화면 밖의 적에게 빔이 가면 「저기는 왜 쏘나」가 된다.
        ///
        /// ⚠️ **빈 경우에는 <paramref name="fallback"/> 이다** — 적이 없는데 스킬이 나가는
        /// 판(마지막 한 마리를 같은 프레임에 죽인 경우)에서 원점을 향한 빔은 길이가 0 이다.
        ///
        /// 📌 **회전 스윕이 아니라 한 방향이다** — 사용자 설명에 방향이 없어 **무리 쪽으로
        /// 가정**했다. 판정은 화면 전부라 방향이 판정을 안 바꾼다(되돌릴 수 있다).
        /// </summary>
        public static Vector2 CrowdCenter(System.Collections.Generic.IReadOnlyList<Vector2> spots,
                                          Rect screen, Vector2 fallback)
        {
            if (spots == null || spots.Count == 0) return fallback;

            var sum = Vector2.zero;
            int n = 0;
            for (int i = 0; i < spots.Count; i++)
            {
                if (!screen.Contains(spots[i])) continue;
                sum += spots[i];
                n++;
            }
            return n == 0 ? fallback : sum / n;
        }

        /// <summary>
        /// **빔 한 줄기 + 고리 파동 셋** (2026-09-21 사용자 확정 · B 태그 인).
        ///
        /// 🗑️ **구 연출 「화면 전체 섬광」 폐기** — 09-18 에 반투명·짧게로 고쳐 두었는데
        /// 09-21 육안에서 **여전히 「녹색 큰 화면」**으로 왔다. 알파를 더 낮추는 것은
        /// 같은 그림을 흐리게 하는 것일 뿐이라, 그림 자체를 바꾼다.
        ///
        /// ⚠️ **판정은 그대로다** — 화면 안 전부에 나눈다. 빔이 한 방향이라고 그쪽만
        /// 맞는 것이 아니다(연출 문서 10-1 「판정 무개입」).
        /// </summary>
        public static TagSkillEffect PlaySweep(Transform parent, Vector2 origin, Vector2 aim,
                                               Rect screen, CombatTuning tuning,
                                               Sprite ring, Sprite white, Color color)
        {
            TagSkillEffect fx = Create(parent, origin, tuning, color);
            fx._isSweep = true;

            float beamSeconds = tuning != null && tuning.tagBeamSecondsTbd > 0f
                ? tuning.tagBeamSecondsTbd : 0.3f;
            float waveSeconds = tuning != null && tuning.tagWaveSecondsTbd > 0f
                ? tuning.tagWaveSecondsTbd : 0.45f;
            int waves = tuning != null ? Mathf.Max(1, tuning.tagWaveCountTbd) : 3;
            float interval = tuning != null ? Mathf.Max(0f, tuning.tagWaveIntervalSecondsTbd) : 0.15f;
            float thickness = tuning != null && tuning.tagBeamCellsTbd > 0f
                ? tuning.tagBeamCellsTbd : 1.5f;

            // 화면 대각선 — 빔 길이이자 파동이 다다를 곳이다. 어디서 쏘든 화면을 벗어난다.
            float diagonal = new Vector2(screen.width, screen.height).magnitude;

            fx._beamSeconds = beamSeconds;
            fx._ringSeconds = waveSeconds;
            fx._ringInterval = interval;
            fx._ringMaxRadius = diagonal * 0.5f;
            fx._sweepSeconds = Mathf.Max(beamSeconds, interval * (waves - 1) + waveSeconds);

            // ── 빔 ──
            Vector2 dir = aim - origin;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
            dir.Normalize();

            var beamGo = new GameObject("TagBeam");
            beamGo.transform.SetParent(fx.transform, false);
            // ⚠️ **기준점이 왼쪽 한가운데**다(`SoftBeam`) — 원점에 그대로 놓고 길이만큼
            //    늘이면 뿌리가 로봇에 붙는다. 한가운데 기준이던 흰 사각과 다르다.
            beamGo.transform.localPosition = Vector3.zero;
            beamGo.transform.localRotation =
                Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            beamGo.transform.localScale = new Vector3(diagonal, thickness, 1f);
            fx._beam = beamGo.AddComponent<SpriteRenderer>();
            // ✅ **빔은 빔 그림으로**(2026-09-21 리허설 ①). 🗑️ 흰 사각 폴백 폐기 —
            //    늘이면 「녹색 직사각형」으로 보였다. 받은 `white` 는 안 쓴다.
            fx._beam.sprite = PlaceholderSprite.SoftBeam();
            fx._beam.color = color;
            fx._beam.sortingOrder = SortingLayers.EffectOver;

            // ── 파동 ──
            fx._rings = new SpriteRenderer[waves];
            for (int i = 0; i < waves; i++)
            {
                var go = new GameObject("TagWave" + i);
                go.transform.SetParent(fx.transform, false);
                go.transform.localScale = Vector3.zero;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ring;
                sr.color = new Color(color.r, color.g, color.b, 0f);
                sr.sortingOrder = SortingLayers.EffectOver - 1;   // 빔 아래
                fx._rings[i] = sr;
            }

            // 고리 자산의 한 변(유닛) — 스케일 1 일 때의 지름이다. 이것을 모르면
            // 「반경 n 칸」이 자산 크기에 따라 달라진다.
            fx._ringSpritePixels = ring != null && ring.rect.width > 0f ? ring.rect.width : 256f;
            return fx;
        }

        public static TagSkillEffect PlayFlash(Transform parent, Rect screen,
                                               CombatTuning tuning, Sprite white, Color color)
        {
            TagSkillEffect fx = Create(parent, screen.center, tuning, color);
            fx._isFlash = true;
            // ⚠️ **짧게 스친다**(2026-09-18 사용자 리허설 ⑥) — 0.40초를 화면 가득 덮으면
            //    「연출」이 아니라 「가림막」으로 읽힌다.
            fx._sweepSeconds = tuning != null ? tuning.tagFlashSecondsTbd : 0.18f;

            // ⚠️⚠️ **반투명이다.** 흰 사각에 색만 입히면 **불투명**이라 화면이 통째로 가려진다 —
            //    사용자가 「녹색 사각으로 가려진다」로 본 자리가 여기다.
            float peak = tuning != null ? tuning.tagFlashAlphaTbd : 0.35f;
            fx._color = new Color(color.r, color.g, color.b, Mathf.Clamp01(peak));

            var sheet = new GameObject("TagFlash");
            sheet.transform.SetParent(fx.transform, false);
            sheet.transform.localScale = new Vector3(screen.width, screen.height, 1f);
            var sr = sheet.AddComponent<SpriteRenderer>();
            sr.sprite = white;
            sr.color = fx._color;   // ⚠️ 첫 프레임부터 반투명이라야 한다(색을 그대로 넣으면 한 칸 번쩍인다)
            sr.sortingOrder = SortingLayers.EffectOver;
            fx._flash = sr;
            return fx;
        }

        private static TagSkillEffect Create(Transform parent, Vector2 origin,
                                             CombatTuning tuning, Color color)
        {
            var go = new GameObject("TagSkillEffect");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(origin.x, origin.y, 0f);
            TagSkillEffect fx = go.AddComponent<TagSkillEffect>();
            fx._tuning = tuning;
            fx._color = color;
            return fx;
        }

        /// <summary>
        /// 🗑️ **폐기 — 겨냥이 없다**(2026-09-18). 연출이 화면 전체를 덮으면서 「어느 쪽으로
        /// 나가는가」가 뜻을 잃었다. 남겨 둔 것은 부르는 쪽이 아직 값을 넣어도 **아무 일도
        /// 안 일어난다**는 것을 분명히 하기 위해서다.
        /// </summary>
        public float AimDegrees { get; set; }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float turn = _sweepSeconds <= 0f ? 1f : Mathf.Clamp01(_elapsed / _sweepSeconds);

            if (_isSweep)
            {
                TickSweep();
            }
            else if (_isFlash)
            {
                if (_flash != null)
                {
                    // 밝은 데서 시작해 걷힌다.
                    Color c = _color;
                    _flash.color = new Color(c.r, c.g, c.b, c.a * (1f - turn));
                }
            }
            else if (_bullets != null && _bulletSpots != null)
            {
                float fallPx = _tuning != null ? _tuning.tagBulletFallArtPixels : 192f;
                float fall = fallPx / PixelsPerUnit;
                Vector3 center = transform.position;

                for (int i = 0; i < _bullets.Length; i++)
                {
                    // **파도마다 따로 떨어진다**(2026-09-18 ⑦). 탄환은 제 파도의 차례가
                    // 올 때까지 화면 위에서 기다렸다가, 그 파도 안에서 빠르게 꽂힌다.
                    int wave = _waves <= 1 ? 0 : i % _waves;
                    float waveStart = wave * _waveSeconds;
                    float u = _waveSeconds <= 0f ? 1f
                        : Mathf.Clamp01((_elapsed - waveStart) / _waveSeconds);

                    float drop = fall * (1f - u);
                    bool waiting = _elapsed < waveStart;
                    _bullets[i].gameObject.SetActive(!waiting);   // 제 차례 전에는 안 보인다

                    Vector2 spot = _bulletSpots[i];
                    _bullets[i].localPosition = new Vector3(
                        spot.x - center.x, spot.y - center.y + drop, 0f);
                }
            }

            // 한 번만 나간다 — 시간이 다 되면 사라진다(반복 없음).
            if (turn >= 1f) Destroy(gameObject);
        }

        /// <summary>
        /// 빔은 **처음이 가장 밝고 걷힌다**, 파동은 **저마다 제 차례에 커진다**.
        ///
        /// ⚠️ 파동은 **고리 자산의 지름을 1 로 보고** 키운다 — 자산이 256 이든 512 든
        /// 화면에서 같은 크기가 되게, 픽셀을 유닛으로 옮겨 나눈다. 안 그러면
        /// 「반경 몇 칸」이 자산 크기에 따라 달라진다.
        /// </summary>
        private void TickSweep()
        {
            if (_beam != null)
            {
                float u = _beamSeconds <= 0f ? 1f : Mathf.Clamp01(_elapsed / _beamSeconds);
                Color c = _color;
                _beam.color = new Color(c.r, c.g, c.b, 1f - u);
            }

            if (_rings == null) return;

            float unitDiameter = _ringSpritePixels / PixelsPerUnit;
            if (unitDiameter <= 0f) unitDiameter = 1f;

            for (int i = 0; i < _rings.Length; i++)
            {
                SpriteRenderer sr = _rings[i];
                if (sr == null) continue;

                float start = i * _ringInterval;
                if (_elapsed < start) continue;                 // 아직 제 차례가 아니다

                float u = _ringSeconds <= 0f ? 1f
                    : Mathf.Clamp01((_elapsed - start) / _ringSeconds);

                // 반경 0 에서 화면 끝까지. 지름으로 환산해 자산 크기를 지운다.
                float diameter = _ringMaxRadius * 2f * u;
                float scale = diameter / unitDiameter;
                sr.transform.localScale = new Vector3(scale, scale, 1f);

                Color c = _color;
                sr.color = new Color(c.r, c.g, c.b, 1f - u);
            }
        }
    }
}
