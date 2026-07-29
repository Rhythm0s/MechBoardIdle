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

        public void Bind(CombatEntity entity, Color color, float size, int sortingOrder)
        {
            _entity = entity;
            _size = size;

            // 본체
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            bodyGo.transform.localScale = new Vector3(size, size, 1f);
            var body = bodyGo.AddComponent<SpriteRenderer>();
            body.sprite = PlaceholderSprite.White();
            body.color = color;
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
