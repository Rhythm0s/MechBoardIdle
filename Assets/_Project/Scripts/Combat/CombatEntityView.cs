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
        private Transform _hpFill;

        // 본체 — 반동(위치)·피격 점멸(색)이 여기에 걸린다. 스프라이트를 갈아끼우지 않는다.
        private Transform _body;
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
            float barW = size;
            float barH = size * 0.14f;
            float barY = size * 0.72f;
            var bgGo = new GameObject("HpBg");
            bgGo.transform.SetParent(transform, false);
            bgGo.transform.localPosition = new Vector3(0f, barY, 0f);
            bgGo.transform.localScale = new Vector3(barW, barH, 1f);
            var bg = bgGo.AddComponent<SpriteRenderer>();
            bg.sprite = PlaceholderSprite.White();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            bg.sortingOrder = SortingLayers.Hud;      // 체력바는 HUD 층

            // HP 채움(초록)
            var fillGo = new GameObject("HpFill");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.localPosition = new Vector3(0f, barY, 0f);
            fillGo.transform.localScale = new Vector3(barW, barH, 1f);
            var fill = fillGo.AddComponent<SpriteRenderer>();
            fill.sprite = PlaceholderSprite.White();
            fill.color = new Color(0.2f, 0.85f, 0.3f, 1f);
            fill.sortingOrder = SortingLayers.Hud + 1; // 배경 위 채움(같은 층 안 미세 조정)
            _hpFill = fillGo.transform;

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
                _hpFill.localScale = new Vector3(_size * ratio, _size * 0.14f, 1f);
                Vector3 lp = _hpFill.localPosition;
                _hpFill.localPosition = new Vector3(-_size * (1f - ratio) * 0.5f, lp.y, lp.z);
            }
        }

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

        /// <summary>이동으로 볼 최소 속도(월드 단위/초). 떨림을 이동으로 읽지 않을 만큼만 둔다.</summary>
        private const float MoveEpsilonPerSecond = 0.01f;

        // 반동과 점멸은 시뮬 틱이 아니라 실시간으로 흐른다 — 판정에 영향을 주지 않는 순수 연출이다.
        private void Update()
        {
            float dt = Time.deltaTime;

            DriveAnimation(dt);

            if (_body != null && _recoilElapsed < EffectTiming.RecoilDuration)
            {
                _recoilElapsed += dt;
                Vector2 off = EffectTiming.RecoilOffset(_recoilDirection, _recoilElapsed);
                _body.localPosition = new Vector3(off.x, off.y, 0f);
            }

            if (_bodyRenderer != null && _flashElapsed < EffectTiming.HitFlashDuration)
            {
                _flashElapsed += dt;
                _bodyRenderer.color = EffectTiming.HitFlashColor(_bodyBaseColor, _flashElapsed);
            }
        }
    }
}
