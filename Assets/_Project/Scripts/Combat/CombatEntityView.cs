using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using UnityEngine;

namespace MBI.Combat
{
    /// <summary>
    /// 전투 엔티티의 최소 비주얼(플레이스홀더). 런타임 생성 — 프리팹/아트 자산 불필요.
    /// 본체 사각 스프라이트 + 상단 HP바(초록). StageRunner가 Bind→매 프레임 Sync.
    /// 아트 리소스가 준비되면 스프라이트/애니메이션으로 교체(현재는 색·크기만).
    /// </summary>
    public sealed class CombatEntityView : MonoBehaviour
    {
        private CombatEntity _entity;
        private float _size;

        // ── 막대 치수 — **한 곳에서만 적는다**(지침 §7) ──────────────────────
        //
        // ⚠️ 수치 글자가 이 막대 **안**에 앉으므로(2026-09-21 사용자 확정 ⑥)
        //    그리는 쪽과 적는 쪽이 같은 수를 봐야 한다. 따로 적으면 어긋나는 날이 온다.

        /// <summary>막대 높이 ÷ 몸 크기. ⚠️ 가정(2026-09-18 「얇게」 0.14 → 0.10).</summary>
        public const float BarHeightRatio = 0.10f;

        /// <summary>HP 막대 중심이 몸 중심에서 내려간 거리 ÷ 몸 크기. ⚠️ 가정.</summary>
        public const float BarCenterRatio = 0.62f;

        /// <summary>보호막 막대가 HP 막대에서 더 내려간 거리 ÷ 막대 높이. ⚠️ 가정.</summary>
        public const float ShieldGapRatio = 1.25f;

        /// <summary>
        /// **막대가 실제로 쓰는 폭**(월드) — 게이지 안 수치가 이 값으로 자리를 잡는다.
        /// ⚠️ `_size`(수치에서 온 수)가 아니다 — 2026-09-21 에 둘이 갈렸던 자리다.
        /// </summary>
        public float ViewSize => _barWidth > 0.0001f ? _barWidth : _size;

        /// <summary>HP 막대 한가운데(월드). 눕힌 자리를 그대로 읽는다.</summary>
        public Vector2 HpBarCenter => _hpBg != null
            ? (Vector2)_hpBg.position
            : (Vector2)transform.position + new Vector2(0f, -_size * BarCenterRatio);

        /// <summary>보호막 막대 한가운데(월드).</summary>
        public Vector2 ShieldBarCenter => _shieldBar != null
            ? (Vector2)_shieldBar.transform.position
            : HpBarCenter + new Vector2(0f, -_size * BarHeightRatio * ShieldGapRatio);
        private Transform _hpFill;

        // ── 쉴드 바 (2026-09-17 · `260917_W07` 4장 4번) ───────────────────────
        //
        // ⚠️⚠️ **규격이 문서에 없었다.** UI 문서 · UI 아트 요청 문서를 찾아보니
        //    쉴드 게이지 규격이 없고, 회피 스택만 「HP 바 인접」으로 정해져 있다.
        //    설계 지시대로 **HP 바 규칙을 그대로** 쓴다 — 같은 너비 · 같은 높이 ·
        //    같은 「왼쪽에 붙여 줄인다」 · **HP 바 바로 위** 한 칸.
        //    색만 다르다(청록) — 층이 다른 것을 색으로 가른다. **구현 가정 · 역기입 자리.**
        //
        // ⚠️ **최대치가 0 이면 아예 안 보인다** — 쉴드 줄이 없는 판에서 빈 막대가
        //    떠 있으면 「고장 났다」로 읽힌다.
        private Transform _shieldFill;
        private GameObject _shieldBar;
        private float _shieldRatio = -1f;   // 음수 = 쉴드 없음(그리지 않는다)

        // 본체 — 반동(위치)·피격 점멸(색)이 여기에 걸린다. 스프라이트를 갈아끼우지 않는다.
        private Transform _body;

        /// <summary>막대 배경 둘 — 매 틱 몸에 맞춰 다시 눕힌다(2026-09-21).</summary>
        private Transform _hpBg;
        private Transform _shieldBg;
        private SpriteRenderer _bodyRenderer;
        private Color _bodyBaseColor = Color.white;
        private Vector2 _recoilDirection;
        private float _recoilElapsed = float.MaxValue;
        private float _flashElapsed = float.MaxValue;

        // 애니메이션 — 걸린 벌이 없으면 _animator 가 null 이고 스틸 한 장이 그대로 남는다.
        private SpriteFrameAnimator _animator;
        private List<UnitAnimClip> _clips;
        private UnitAnimDirection _lastDirection = UnitAnimDirection.South;

        // 태그 진입 — 화면 우측 밖에서 자리로 들어온다(260907_W01 2-3 사용자 확정).
        // 이것은 코드가 위치를 옮기는 것이며 프레임 재생이 아니다.
        private float _entryElapsed = -1f;
        private float _entrySeconds;
        private float _entryOffsetX;
        private Vector2 _lastPosition;
        private bool _hasLastPosition;
        private bool _deathPlayed;

        public CombatEntity Entity => _entity;

        /// <summary>
        /// 지금 몸이 쓰는 그림과 좌우 뒤집힘 — **회피 잔상이 읽는다**(2026-09-18).
        ///
        /// ⚠️ 잔상은 **그때 그 그림**이어야 한다. 기본 스프라이트를 쓰면 걷는 중에 회피했을 때
        /// 잔상만 다른 자세로 서서 「누구의 자국인가」가 흐려진다.
        /// ⚠️ 뷰가 아직 안 묶였으면 <c>null</c> 이다 — 부르는 쪽이 그때 아무것도 안 그린다.
        /// </summary>
        public Sprite BodySprite => _bodyRenderer != null ? _bodyRenderer.sprite : null;

        /// <summary>몸 그림이 좌우로 뒤집혀 있는가(서면을 동면 미러로 그리는 경우).</summary>
        public bool BodyFlipX => _bodyRenderer != null && _bodyRenderer.flipX;

        /// <summary>몸 그림의 크기 — 잔상이 같은 크기로 서야 한다.</summary>
        public Vector3 BodyScale => _body != null ? _body.lossyScale : Vector3.one;

        /// <summary>발사 반동 시작(UI 문서「연출 표현 규칙」). 표적 방향을 주면 반대로 밀린다.</summary>
        public void Recoil(Vector2 fireDirection)
        {
            _recoilDirection = fireDirection;
            _recoilElapsed = 0f;
        }

        /// <summary>피격 점멸 시작. 세기는 일정 — 맞았는지 아닌지만 알린다.</summary>
        public void FlashHit() => _flashElapsed = 0f;

        public void Bind(CombatEntity entity, Color color, float size, int sortingOrder, Sprite art = null,
            List<UnitAnimClip> clips = null, int viewScale = 1)
        {
            _entity = entity;
            _size = size; // HP 바 치수는 실제 아트 여부와 무관하게 이 값을 쓴다
            _clips = clips;
            _deathPlayed = false;
            _hasLastPosition = false;

            // ⚠️ **다시 묶기 전에 옛 몸을 지운다**(2026-09-10 · 리허설 결함 ② 「태그 유령」).
            //
            // 태그 교대는 **같은 뷰를 다시 묶는다**(`StageRunner.BindRobotView`). 그런데 여기는
            // `Body`·`Shadow`·HP 바를 **새로 만들어 붙이기만** 했다 — 옛 것을 안 지우니
            // **나간 로봇이 그 자리에 그대로 남았다.** 그것이 사용자가 본 유령이다.
            //
            // ⚠️ **09-10 의 첫 수정(`e4215fe`)이 이것을 못 잡은 이유**가 여기 있다.
            // 그때 걷은 것은 **페이드 유령 복사본**이었고, 진짜로 남던 것은 **덧붙은 자식**이다.
            // 같은 이름의 증상에 원인이 둘이었다.
            //
            // `Destroy` 는 이 프레임 끝에 걸리므로 **먼저 꺼서** 한 프레임도 겹쳐 보이지 않게 한다.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject old = transform.GetChild(i).gameObject;
                old.SetActive(false);
                Destroy(old);
            }
            _animator = null;
            _body = null;
            _bodyRenderer = null;
            _hpFill = null;

            // 본체
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            var body = bodyGo.AddComponent<SpriteRenderer>();

            if (art != null)
            {
                // 크기는 **캔버스가 결정한다**(ArtSpec, PPU 192). 크기를 맞추려고 임의의 배를
                // 곱하면 안 된다 — 256px 스프라이트에 1.333을 곱해 1.78칸으로 만드는 식이면
                // **두 번 커지고** 도트도 뭉개진다.
                //
                // ⚠️ **예외는 정수배 하나뿐이다**(2026-09-10 · 보스). 벌 캔버스가 모자랄 때
                // **한 픽셀이 정확히 n×n 픽셀이 되는 배**만 곱한다. 기본은 1 이라
                // 아무 데도 영향이 없고, 값은 SO 에서 온다(<see cref="EnemyDefinition.viewScale"/>).
                body.sprite = art;
                bodyGo.transform.localScale = Vector3.one * Mathf.Max(1, viewScale);
                body.color = Color.white; // 도트에 색을 입히면 팔레트가 뭉개진다
            }
            else
            {
                // 아트 미투입 폴백: 1×1 흰 사각을 크기만큼 늘리고 색으로 구분한다.
                body.sprite = PlaceholderSprite.White();
                bodyGo.transform.localScale = new Vector3(size, size, 1f);
                body.color = color;
            }

            body.sortingOrder = sortingOrder;
            _body = bodyGo.transform;
            _bodyRenderer = body;
            _bodyBaseColor = body.color;

            // 애니메이션은 스틸 위에 얹는다. 벌이 없으면 얹지 않고 스틸을 그대로 둔다 —
            // 폴백 사각형에 프레임을 걸면 색 구분이 사라진다.
            if (art != null && _clips != null && _clips.Count > 0)
            {
                _animator = bodyGo.AddComponent<SpriteFrameAnimator>();
                _animator.Attach(body);
                PlayState(UnitAnimState.Idle, _lastDirection);
            }

            // 바닥 그림자 — 탑뷰에는 높이가 없어 크기와 그림자로 위조한다(V01 §3).
            // 본체보다 뒤에 깔고, 반동으로 본체가 밀려도 그림자는 제자리에 둔다(발이 붙어 있어야 한다).
            Vector2 shadow = EffectTiming.ShadowSize(size);
            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(transform, false);
            shadowGo.transform.localPosition = new Vector3(0f, EffectTiming.ShadowFootOffset(size), 0f);
            shadowGo.transform.localScale = new Vector3(shadow.x, shadow.y, 1f);
            var shadowSr = shadowGo.AddComponent<SpriteRenderer>();
            shadowSr.sprite = PlaceholderSprite.SoftDisc();
            shadowSr.color = new Color(0f, 0f, 0f, 0.45f);
            // 하단 이펙트 층 — 액터보다 아래. 그림자가 위로 올라가면 높이 위조가 뒤집힌다.
            shadowSr.sortingOrder = SortingLayers.EffectUnder;

            // HP 배경(어두움)
            //
            // ⚠️⚠️ **발밑으로 내렸다**(2026-09-18 사용자 확정 · 시안 3 — 「몹 체력 = 몹 발밑
            //    얇은 빨간 바」). 🗑️ 구 자리(머리 위 +0.72) 폐기.
            //    까닭은 **머리 위가 이제 붐비기 때문**이다 — 로봇 쪽은 몸 밑에 HP·보호막·회피
            //    셋이 서고, 적 쪽도 같은 규칙이어야 「밑에 있는 것이 그 몸의 상태」로 읽힌다.
            //
            // ⚠️ **얇게**(0.14 → 0.10) — 발밑은 그림자와 가까워 두꺼우면 몸을 가린다. ⚠️ 가정.
            float barW = size;
            float barH = size * BarHeightRatio;
            float barY = -size * BarCenterRatio;
            var bgGo = new GameObject("HpBg");
            _hpBg = bgGo.transform;
            bgGo.transform.SetParent(transform, false);
            bgGo.transform.localPosition = new Vector3(0f, barY, 0f);
            bgGo.transform.localScale = new Vector3(barW, barH, 1f);
            var bg = bgGo.AddComponent<SpriteRenderer>();
            bg.sprite = PlaceholderSprite.White();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            bg.sortingOrder = SortingLayers.Hud;      // 체력바는 HUD 층

            // HP 채움 — **적은 빨강 · 로봇은 초록**(2026-09-18 사용자 확정 · 「얇은 빨간 바」).
            //
            // ⚠️ 색으로 편을 가른다 — 같은 색이면 발밑 막대가 늘어선 화면에서 **누구의 것인지**가
            //    안 읽힌다. 빨강은 조립 층의 「못 쓴다」와 층이 달라 섞이지 않는다(월드 대 UI).
            var fillGo = new GameObject("HpFill");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.localPosition = new Vector3(0f, barY, 0f);
            fillGo.transform.localScale = new Vector3(barW, barH, 1f);
            var fill = fillGo.AddComponent<SpriteRenderer>();
            fill.sprite = PlaceholderSprite.White();
            fill.color = entity != null && entity.faction == Faction.Enemy
                ? new Color(0.90f, 0.22f, 0.20f, 1f)    // 적 — 빨강
                : new Color(0.2f, 0.85f, 0.3f, 1f);     // 로봇 — 초록
            fill.sortingOrder = SortingLayers.Hud + 1; // 배경 위 채움(같은 층 안 미세 조정)
            _hpFill = fillGo.transform;

            // 쉴드 바 — **HP 바 규칙 그대로 · 바로 아래 한 칸**(2026-09-18).
            // 🗑️ 구 「바로 위」 폐기 — HP 바가 발밑으로 내려오면서 위쪽은 몸이다.
            float shieldY = barY - barH * ShieldGapRatio;
            _shieldBar = new GameObject("ShieldBar");
            _shieldBar.transform.SetParent(transform, false);
            _shieldBar.transform.localPosition = new Vector3(0f, shieldY, 0f);

            var sBgGo = new GameObject("ShieldBg");
            _shieldBg = sBgGo.transform;
            sBgGo.transform.SetParent(_shieldBar.transform, false);
            sBgGo.transform.localScale = new Vector3(barW, barH, 1f);
            var sBg = sBgGo.AddComponent<SpriteRenderer>();
            sBg.sprite = PlaceholderSprite.White();
            sBg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            sBg.sortingOrder = SortingLayers.Hud;

            var sFillGo = new GameObject("ShieldFill");
            sFillGo.transform.SetParent(_shieldBar.transform, false);
            sFillGo.transform.localScale = new Vector3(barW, barH, 1f);
            var sFill = sFillGo.AddComponent<SpriteRenderer>();
            sFill.sprite = PlaceholderSprite.White();
            sFill.color = new Color(0.35f, 0.8f, 0.95f, 1f);   // 청록 — HP(초록)와 가른다
            sFill.sortingOrder = SortingLayers.Hud + 1;
            _shieldFill = sFillGo.transform;

            _shieldBar.SetActive(false);   // 쉴드가 없는 판에서는 안 뜬다

            Sync();
        }

        /// <summary>
        /// 태그 진입을 건다 — <b>화면 우측 밖에서 자리로, 빠르게 들어와 점점 느려진다</b>
        /// (`260907_W01` 2-3 · 사용자 확정). 길이를 태그 클립의 실제 초와 같게 두면
        /// 클립과 이동이 함께 끝나 <b>마지막 프레임으로 굳은 채 미끄러져 들어오는 것</b>을 피한다
        /// (W01 확인 1). <b>가정이며 되돌릴 수 있다</b> — 0.75초 자체가 잠정이다(W01 9장 2).
        /// </summary>
        public void PlayTagIn(float seconds, float offsetX)
        {
            _entrySeconds = Mathf.Max(0.01f, seconds);
            _entryOffsetX = offsetX;
            _entryElapsed = 0f;
            PlayState(UnitAnimState.TagIn, UnitAnimDirection.South);
        }

        /// <summary>진입이 아직 돌고 있는가.</summary>
        public bool Entering => _entryElapsed >= 0f && _entryElapsed < _entrySeconds;

        public void Sync()
        {
            if (_entity == null) return;
            transform.position = new Vector3(_entity.position.x, _entity.position.y, 0f);

            // 들어오는 동안만 자리에서 오른쪽으로 밀어 둔다. 감속은 1-(1-t)^2 — 빠르게 들어와
            // 점점 느려진다. 끝나면 offset 이 0이 되어 원래 자리에 정확히 선다.
            if (_entryElapsed >= 0f)
            {
                _entryElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_entryElapsed / _entrySeconds);
                float eased = 1f - (1f - t) * (1f - t);
                transform.position += new Vector3(_entryOffsetX * (1f - eased), 0f, 0f);
                if (t >= 1f) _entryElapsed = -1f;
            }

            // ⚠️⚠️ **막대는 그려진 몸을 따라간다**(2026-09-21 사용자 리허설 · 촬영 차단 —
            //    「적 HP 바가 몬스터와 떨어져 거대한 빨간 막대로 찍힌다」).
            //
            // 🗑️ 구 규칙 「막대 치수는 실제 아트 여부와 무관하게 `size` 를 쓴다」 폐기.
            //    `size` 는 `EnemySize(maxHp)` 가 낸 **수치에서 온 수**이고, 몸은 아트가
            //    있으면 **캔버스가 정한다**(PPU 192 × viewScale) — 둘이 만난 적이 없다.
            //    64px 스프라이트(0.33칸)에 `size` 0.9 막대가 붙으면 **몸의 세 배**이고,
            //    자리도 `-size × 0.62` 라 한참 아래로 떨어진다.
            //
            // 📌 **막대는 몸에 붙는 표시다** — 몸이 실제로 차지하는 사각을 따라야 한다.
            //    폴백(흰 사각)일 때는 몸이 곧 `size` 라 값이 종전과 같다.
            LayOutBars();

            float ratio = _entity.maxHp > 0f ? Mathf.Clamp01(_entity.hp / _entity.maxHp) : 0f;
            if (_hpFill != null)
            {
                // ⚠️ **왼쪽에 붙여 줄인다**(2026-09-11 사용자 확정 · 플랜 §71-16 ⑥).
                //
                // 스프라이트 피벗이 가운데라 크기만 줄이면 **양쪽에서 안으로** 오므라든다 —
                // 화면에서는 막대가 가운데로 모이는 것처럼 보여 **어느 쪽이 줄어드는지**가
                // 안 읽혔다. 체력은 **오른쪽에서 왼쪽으로** 준다.
                //
                // 피벗을 바꿀 수는 없으므로(`PlaceholderSprite.White()` 공용이다)
                // **왼변이 제자리에 있도록 중심을 민다** — 줄어든 만큼의 절반이다.
                // ⚠️ **0.14 → 0.10**(2026-09-21). 배경은 09-18 에 얇게 고쳤는데 **채움은
                //    안 고쳤다** — 채움이 배경보다 두꺼워 위아래로 삐져나와 있었다.
                _hpFill.localScale = new Vector3(_barWidth * ratio, _barWidth * BarHeightRatio, 1f);
                _hpFill.localPosition = new Vector3(
                    _barMid - _barWidth * (1f - ratio) * 0.5f,
                    _hpBg != null ? _hpBg.localPosition.y : _hpFill.localPosition.y,
                    _hpFill.localPosition.z);
            }

            if (_shieldBar != null) _shieldBar.SetActive(_shieldRatio >= 0f);
            if (_shieldFill != null && _shieldRatio >= 0f)
            {
                // HP 바와 **같은 규칙**이다 — 왼변을 제자리에 두고 오른쪽에서 줄인다.
                float r = Mathf.Clamp01(_shieldRatio);
                _shieldFill.localScale = new Vector3(_barWidth * r, _barWidth * BarHeightRatio, 1f);
                _shieldFill.localPosition = new Vector3(-_barWidth * (1f - r) * 0.5f, 0f, 0f);  // 쉴드 묶음이 이미 가운데로 옮겨져 있다
            }
        }

        /// <summary>지금 막대가 쓸 폭(월드). 그려진 몸을 따른다.</summary>
        private float _barWidth;

        /// <summary>막대의 가로 가운데(로컬) — 그림이 캔버스 한가운데가 아닐 수 있다.</summary>
        private float _barMid;

        /// <summary>
        /// 막대 둘을 **그려진 몸에 맞춰** 다시 눕힌다 (2026-09-21).
        ///
        /// ⚠️ **`bounds` 는 월드다** — 뷰 뿌리에는 배율이 없으므로(몸에만 있다)
        /// 월드에서 뿌리 자리를 빼면 그대로 로컬이다.
        ///
        /// ⚠️ **몸이 없거나 아직 안 그려졌으면 종전 값으로 떨어진다** — 수를 지어내지 않는다.
        /// </summary>
        private void LayOutBars()
        {
            float w = _size, bottom = -_size * BarCenterRatio, mid = 0f;

            if (_bodyRenderer != null && _bodyRenderer.sprite != null)
            {
                Bounds b = _bodyRenderer.bounds;
                if (b.size.x > 0.0001f)
                {
                    w = b.size.x;
                    // 발밑 — 몸 사각의 아랫변에서 막대 반 칸만큼 더 내려간 자리.
                    bottom = b.min.y - transform.position.y - w * BarHeightRatio;

                    // ⚠️⚠️ **가로 가운데도 몸에서 낸다**(2026-09-21 · 내가 띄워 보고 잡았다).
                    //    몸을 0 에 놓고 폭만 몸에서 가져왔더니 **막대가 왼쪽으로 치우쳤다** —
                    //    그림이 캔버스 한가운데에 있지 않은 자산이 있어서다.
                    //    막대는 몸에 붙는 표시이므로 **몸 사각의 한가운데**를 따라야 한다.
                    mid = b.center.x - transform.position.x;
                }
            }

            _barWidth = w;
            _barMid = mid;
            float h = w * BarHeightRatio;

            if (_hpBg != null)
            {
                _hpBg.localPosition = new Vector3(mid, bottom, 0f);
                _hpBg.localScale = new Vector3(w, h, 1f);
            }
            if (_shieldBar != null)
                _shieldBar.transform.localPosition =
                    new Vector3(mid, bottom - h * ShieldGapRatio, 0f);
            if (_shieldBg != null) _shieldBg.localScale = new Vector3(w, h, 1f);
        }

        /// <summary>
        /// 쉴드 비율을 건다(0~1). **음수를 주면 막대가 사라진다** — 쉴드가 없는 판이다.
        /// ⚠️ 이 뷰는 쉴드를 **모른다** — 값은 러너가 시뮬에서 읽어 넣는다(§7).
        /// </summary>
        public void SetShieldRatio(float ratio) => _shieldRatio = ratio;

        /// <summary>
        /// 상태 한 벌을 고른다. 요청한 방향이 없으면 남면으로 내린다.
        ///
        /// <b>서면이 없으면 동면을 좌우로 뒤집는다.</b> 합체 로봇이 그러하고(15-3 3-3),
        /// 2026-09-07부터 <b>로봇 B의 대기</b>도 그렇다 — 사용자 판정으로 서면 폴더를 지우고
        /// 동면 하나를 미러로 쓰기로 했다. 좌우 대칭인 기체에서만 성립하며 <b>로봇 A는 안 된다</b>
        /// (15-1 3-2 — 마운트가 붙은 팔이 한쪽만 두껍다). 그래서 방향을 로봇별로 가르지 않고
        /// <b>있는 벌이 무엇인가</b>로 고른다 — A는 서면이 있으니 이 자리에 오지 않는다.
        /// 규격에 「좌우 대칭 기체는 3방향 + 미러」를 넣을지는 설계 판정 대기다.
        ///
        /// 대기는 <b>되감기</b>로 돈다(2026-09-07 사용자 판정) — 이동은 걷는 순환이라 아니다.
        /// 되감기와 길이는 벌 자료가 갖고 <see cref="MBI.Core.Anim.AnimSchedule"/>가 칸으로 편다.
        /// </summary>
        private void PlayState(UnitAnimState state, UnitAnimDirection dir)
        {
            if (_animator == null || _clips == null) return;

            bool loop = state == UnitAnimState.Idle || state == UnitAnimState.Move;

            if (TryFind(state, dir, out UnitAnimClip clip)) { _animator.Play(clip, loop); return; }

            // 서면이 없으면 동면을 좌우로 뒤집는다.
            if (dir == UnitAnimDirection.West && TryFind(state, UnitAnimDirection.East, out clip))
            { _animator.Play(clip, loop, flipX: true); return; }

            if (TryFind(state, UnitAnimDirection.South, out clip)) _animator.Play(clip, loop);
        }

        private bool TryFind(UnitAnimState state, UnitAnimDirection dir, out UnitAnimClip found)
        {
            for (int i = 0; i < _clips.Count; i++)
            {
                UnitAnimClip c = _clips[i];
                if (c.state == state && c.direction == dir && c.IsValid) { found = c; return true; }
            }
            found = default;
            return false;
        }

        /// <summary>
        /// 움직인 방향을 넷 중 하나로 접는다 — **마지막 축을 붙든다**
        /// (2026-09-11 사용자 육안 · <see cref="DirectionHysteresis"/>).
        ///
        /// ⚠️ **구 규칙 「우세 축이 이긴다」는 폐기**다. 대각으로 갈 때 두 축이 거의 같아
        /// 프레임마다 승자가 뒤집혔고, 그때마다 벌이 갈려 **동면과 북면이 번갈아 깜빡였다.**
        /// 이제 새 축이 옛 축을 1.5배 넘게 이겨야 넘어간다.
        /// </summary>
        private static UnitAnimDirection ToDirection(Vector2 delta, UnitAnimDirection last) =>
            DirectionHysteresis.Resolve(delta, last);

        // 애니메이션 상태 선택. 위치 변화로 이동을 판정한다 — 시뮬은 속도를 내주지 않는다.
        private void DriveAnimation(float dt)
        {
            if (_animator == null || _entity == null) return;

            if (_entity.hp <= 0f)
            {
                if (!_deathPlayed) { PlayState(UnitAnimState.Death, UnitAnimDirection.South); _deathPlayed = true; }
                return;
            }

            var pos = new Vector2(_entity.position.x, _entity.position.y);
            if (!_hasLastPosition) { _lastPosition = pos; _hasLastPosition = true; }

            Vector2 delta = pos - _lastPosition;
            _lastPosition = pos;

            // ⚠️ **쏘는 동안에는 쏘는 쪽을 본다**(2026-09-15 사용자 확정 · 육안 4차 ①).
            //
            // 종전에는 얼굴이 **이동 축**만 따랐다. 자동 조종은 사거리 안에 적이 있으면
            // 제자리에서 쏘는데, 그러면 **마지막으로 걸었던 쪽**을 그대로 보고 선 채
            // 엉뚱한 방향으로 탄이 나갔다 — 「등지고 쏜다」로 보인다.
            //
            // ⚠️ **수동 입력이 우선이다**(구현 가정 · 되돌릴 수 있다 · 설계 사후).
            // 손으로 몰 때까지 표적을 보게 하면 **내가 가는 쪽을 못 보게** 된다.
            // 그래서 조종 중에는 이 덮어쓰기가 꺼진다 — 끄는 것은 `StageRunner` 다.
            //
            // 표적이 없으면 `null` 이라 아래가 종전대로 돈다.
            if (FacingOverride.HasValue && FacingOverride.Value.sqrMagnitude > 1e-6f)
            {
                // ⚠️⚠️ **조준도 걸음과 같은 120° 를 쓴다**(2026-09-16 사용자 확정 · 육안 10차 ①).
                //
                // 🗑️ **구 규칙 「조준에는 관성을 안 건다」(관성 1) 는 폐기**(09-15 ~ 09-16).
                // 그때의 근거는 「조준은 떨리지 않는다 — 고른 표적 하나를 가리키는 값이니
                // 프레임마다 뒤집힐 일이 없다」였다. **그 근거가 틀렸다.**
                //
                // 관성 1 은 네 방향을 **45° 로 딱 나눈다.** 표적이 대각선에 서 있으면 그 선이
                // 바로 경계선이고, 로봇도 적도 계속 움직이므로 **경계선을 몇 번이고 넘나든다** —
                // 사용자가 본 「얼굴이 좌/우로 매우 빠르게 뒤집힌다」가 그것이다.
                // 떠는 것은 조준값이 아니라 **조준값을 넷으로 접는 자리**였다(걸음과 같은 자리다).
                //
                // 📌 그러면 09-15 의 「세 번째 발사에야 돌아본다」가 돌아오지 않는가 —
                // **안 돌아온다.** 그때 문제는 관성 1.5(±56°)가 아니라 **조준의 근거가
                // 발사였다**는 것이었고, 그쪽은 `AimDirection` 이 표적으로 바뀌며 이미 닫혔다.
                // 120° 는 ±60° 라 1.5 보다 조금 더 붙들지만, 표적이 그 밖으로 나가면 **그 틱에**
                // 돈다 — 발사를 기다리지 않는다.
                _lastDirection = DirectionHysteresis.Resolve(FacingOverride.Value, _lastDirection);
                PlayState(!ForceIdlePose && IsMoving(delta, dt) ? UnitAnimState.Move : UnitAnimState.Idle,
                          _lastDirection);
                return;
            }

            // 문턱은 프레임 시간에 비례시킨다 — 프레임이 길어도 서 있는 것으로 오판하지 않게.
            float moved = delta.magnitude;
            if (moved > MoveEpsilonPerSecond * Mathf.Max(dt, 1e-4f))
            {
                // ⚠️ **자동 이동에도 같이 걸린다** — 떨림은 입력이 아니라 위치 변화를
                // 방향으로 접는 이 자리에서 나므로, 손으로 몰든 시뮬이 몰든 같은 곳이다.
                _lastDirection = ToDirection(delta, _lastDirection);
                PlayState(UnitAnimState.Move, _lastDirection);
            }
            else PlayState(UnitAnimState.Idle, _lastDirection);
        }

        /// <summary>
        /// 이동으로 볼 최소 속도(월드 단위/초).
        ///
        /// ⚠️ **0.01 은 너무 작았다**(2026-09-15 · 사용자 육안 8차 ③ — 「제자리 회전인데
        /// 걷는 벌이 돈다」). 한 프레임(1/60초)으로 치면 **0.00017 유닛**이라, 부동소수
        /// 떨림이나 표적을 바꾸며 생기는 **아주 작은 미끄러짐**까지 「걷는다」로 읽혔다.
        ///
        /// 📌 **눈에 안 보이는 움직임은 걸음이 아니다.** 로봇 걸음이 초당 몇 유닛이므로
        /// 그 **몇 %** 를 문턱으로 둔다 — 그보다 느리면 서 있는 것으로 보이고, 실제로
        /// 화면에서도 안 움직인다. ⚠️ **가정이다**(문서에 벌 전환 문턱 절이 없다).
        /// </summary>
        private const float MoveEpsilonPerSecond = 0.15f;

        /// <summary>
        /// **이 프레임에 바라볼 쪽**. `null` 이면 종전대로 **이동 축**을 본다.
        ///
        /// 로봇 뷰에만 걸린다 — `StageRunner` 가 매 프레임 넣고 뺀다(육안 4차 ①).
        /// 몬스터는 아무도 안 넣으므로 `null` 인 채로 돈다.
        /// </summary>
        public Vector2? FacingOverride;

        // 🗑️ **무적 깜빡임 폐기**(2026-09-18 사용자 리허설 ④) — 09-18 오전에 넣었다가
        //    같은 날 저녁에 걷었다. 사용자가 「버그처럼 보인다」로 뒤집었다.
        //    📌 회피가 났다는 것은 **밀려나는 것**과 **분사**가 이미 말하고 있었다 —
        //       몸이 사라졌다 나타나는 것은 그 위에 얹힌 **넷째 신호**였고, 셋이 겹치자
        //       가장 세게 읽히는 것(사라짐)이 「죽었나?」로 읽혔다.

        /// <summary>
        /// **선 자세를 강제한다** — 회피로 밀리는 동안 걷는 벌을 돌리지 않으려는 자리
        /// (2026-09-18 사용자 확정 — 「회피로 움직이는 방향으로 Idle 로 바라보도록」).
        ///
        /// 📌 회피는 **걸음이 아니라 튕김**이다. 0.167초 동안 반 칸을 가는데 그걸 걸음으로
        /// 그리면 벌 한 바퀴가 그 안에 다 돌아 **다리가 파르르 떠는 것처럼** 보인다.
        /// </summary>
        public bool ForceIdlePose;

        /// <summary>걷고 있는가 — 벌을 고르는 데만 쓴다(방향은 위에서 이미 정했다).</summary>
        private static bool IsMoving(Vector2 delta, float dt) =>
            delta.magnitude > MoveEpsilonPerSecond * Mathf.Max(dt, 1e-4f);

        // 반동과 점멸은 시뮬 틱이 아니라 실시간으로 흐른다 — 판정에 영향을 주지 않는 순수 연출이다.
        private void Update()
        {
            float dt = Time.deltaTime;

            DriveAnimation(dt);

            // ⚠️⚠️ **몸의 오프셋은 매 프레임 0 에서 다시 짓는다**(2026-09-15 오후 · 육안 ⑤).
            //
            // 종전엔 반동이 `localPosition` 을 **쓰기만 하고 끝날 때 안 되돌렸다.**
            // 지속이 지나면 그 블록을 통째로 건너뛰므로 **마지막 오프셋이 그대로 남고**,
            // 피격 흔들림은 거기에 `+=` 로 쌓였다. 로봇은 쏘고 맞기를 한 판 내내 하므로
            // **몸만 점점 걸어 나간다** — HP 막대·그림자는 형제라 localPosition 0 에 그대로 서
            // 있고, 탄약 소진 아이콘은 시뮬 좌표에 서 있다. 그래서 사용자 화면에서
            // **막대와 아이콘만 허공에 떠 보였다** — 월드→화면 환산이 아니라 이것이었다.
            //
            // 📌 적은 안 쏴서(반동 없음) 거의 안 보였고, 무엇보다 금방 죽는다 —
            // 「적 막대는 잘 붙어 있는데 로봇만」이 그 뜻이다.
            if (_body != null)
            {
                Vector3 bodyOffset = Vector3.zero;

                if (_recoilElapsed < EffectTiming.RecoilDuration)
                {
                    _recoilElapsed += dt;
                    Vector2 off = EffectTiming.RecoilOffset(_recoilDirection, _recoilElapsed);
                    bodyOffset += new Vector3(off.x, off.y, 0f);
                }

                // ⚠️ **작은 흔들림**(2026-09-15 사용자 확정 · 육안 ④). **넉백이 아니다** —
                // 몸은 제자리에 있고 **그림만** 떤다. 밀려나면 이동 규칙과 싸우고
                // 자동 조종이 그 자리를 다시 계산한다.
                if (_flashElapsed < EffectTiming.HitFlashDuration)
                {
                    Vector2 shake = EffectTiming.HitShakeOffset(_flashElapsed);
                    bodyOffset += new Vector3(shake.x, shake.y, 0f);
                }

                // 항상 쓴다 — 두 연출이 다 끝나면 저절로 0 이 된다.
                _body.localPosition = bodyOffset;
            }

            if (_bodyRenderer != null && _flashElapsed < EffectTiming.HitFlashDuration)
            {
                _flashElapsed += dt;
                _bodyRenderer.color = EffectTiming.HitFlashColor(_bodyBaseColor, _flashElapsed);

                // ⚠️ **끝나면 색을 되돌린다.** 종전에는 지속이 지나면 이 블록을 통째로
                // 건너뛰어 **마지막 프레임의 색이 그대로 남았다** — 빨강·하양 교차라
                // 하양으로 끝나면 티가 안 났을 뿐이다.
                if (_flashElapsed >= EffectTiming.HitFlashDuration)
                    _bodyRenderer.color = _bodyBaseColor;
            }

        }

    }
}
