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

        public CombatEntity Entity => _entity;

        /// <summary>발사 반동 시작(UI 문서「연출 표현 규칙」). 표적 방향을 주면 반대로 밀린다.</summary>
        public void Recoil(Vector2 fireDirection)
        {
            _recoilDirection = fireDirection;
            _recoilElapsed = 0f;
        }

        /// <summary>피격 점멸 시작. 세기는 일정 — 맞았는지 아닌지만 알린다.</summary>
        public void FlashHit() => _flashElapsed = 0f;

        public void Bind(CombatEntity entity, Color color, float size, int sortingOrder, Sprite art = null)
        {
            _entity = entity;
            _size = size; // HP 바 치수는 실제 아트 여부와 무관하게 이 값을 쓴다

            // 본체
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            var body = bodyGo.AddComponent<SpriteRenderer>();

            if (art != null)
            {
                // 크기는 **캔버스가 결정한다**(ArtSpec, PPU 192). localScale로 다시 곱하면
                // 256px 스프라이트가 1.333칸 × 1.333배 = 1.78칸이 되어 두 번 커진다.
                body.sprite = art;
                bodyGo.transform.localScale = Vector3.one;
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

        public void Sync()
        {
            if (_entity == null) return;
            transform.position = new Vector3(_entity.position.x, _entity.position.y, 0f);

            float ratio = _entity.maxHp > 0f ? Mathf.Clamp01(_entity.hp / _entity.maxHp) : 0f;
            if (_hpFill != null)
                _hpFill.localScale = new Vector3(_size * ratio, _size * 0.14f, 1f);
        }

        // 반동과 점멸은 시뮬 틱이 아니라 실시간으로 흐른다 — 판정에 영향을 주지 않는 순수 연출이다.
        private void Update()
        {
            float dt = Time.deltaTime;

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
