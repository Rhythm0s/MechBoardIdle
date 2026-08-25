using MBI.Core;
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

        public CombatEntity Entity => _entity;

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
            bg.sortingOrder = sortingOrder + 1;

            // HP 채움(초록)
            var fillGo = new GameObject("HpFill");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.localPosition = new Vector3(0f, barY, 0f);
            fillGo.transform.localScale = new Vector3(barW, barH, 1f);
            var fill = fillGo.AddComponent<SpriteRenderer>();
            fill.sprite = PlaceholderSprite.White();
            fill.color = new Color(0.2f, 0.85f, 0.3f, 1f);
            fill.sortingOrder = sortingOrder + 2;
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
    }
}
