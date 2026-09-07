using UnityEngine;

namespace MBI.Combat
{
    /// <summary>
    /// 스프라이트 하나를 투명해질 때까지 지우고 자기 게임오브젝트를 없앤다.
    ///
    /// 태그 전환에서 <b>물러나는 로봇을 페이드 아웃으로 제거</b>하는 데 쓴다
    /// (`260907_W01` 2-3 · 사용자 확정). 로봇 뷰는 하나뿐이라 새 로봇에 다시 묶는 순간
    /// 이전 그림이 그대로 사라지는데, 그러면 「사라졌다」가 아니라 「없던 일이 된다」로 보인다.
    ///
    /// ⚠️ <b>이 규정을 어느 문서가 갖는지는 아직 정해지지 않았다</b> — 15-1에 없는 새 규정이고
    /// 로봇 B도 같으므로 자식 둘에 나눠 쓰면 중복이 된다. UI 문서「연출 표현 규칙」이
    /// 소관으로 보이나 설계가 그 문서를 확인하기 전이다(W01 9장 3).
    /// </summary>
    public sealed class FadeOutAndDestroy : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private float _seconds;
        private float _elapsed;
        private Color _from;

        public void Begin(SpriteRenderer target, float seconds)
        {
            _renderer = target;
            _seconds = Mathf.Max(0.01f, seconds);
            _elapsed = 0f;
            if (_renderer != null) _from = _renderer.color;
        }

        private void Update()
        {
            if (_renderer == null) { Destroy(gameObject); return; }

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _seconds);
            Color c = _from;
            c.a = _from.a * (1f - t);
            _renderer.color = c;

            if (t >= 1f) Destroy(gameObject);
        }
    }
}
