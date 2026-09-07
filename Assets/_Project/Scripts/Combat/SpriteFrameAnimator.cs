using MBI.Data;
using UnityEngine;

namespace MBI.Combat
{
    /// <summary>
    /// <see cref="SpriteRenderer"/>에 프레임을 순환시킨다. 프레임을 만들어도 이것이 없으면
    /// 화면에 안 나온다 — 2026-09-07까지 `Art/Anim/`을 읽는 코드가 아예 없었다.
    ///
    /// 사망은 되감지 않는다. 마지막 프레임에서 멈추고 그대로 남는다 —
    /// 주저앉은 기체가 다시 일어서면 안 된다.
    /// </summary>
    public sealed class SpriteFrameAnimator : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private UnitAnimClip _clip;
        private bool _loop;
        private bool _hasClip;
        private bool _flipX;
        private float _elapsed;
        private int _frame = -1;

        public UnitAnimState State { get; private set; }
        public UnitAnimDirection Direction { get; private set; }
        public bool HasClip => _hasClip;

        /// <summary>재생이 끝났는가. 반복 클립은 언제나 false다.</summary>
        public bool Finished => _hasClip && !_loop && _frame >= _clip.frames.Length - 1;

        public void Attach(SpriteRenderer target) => _renderer = target;

        /// <summary>
        /// 한 벌을 건다. 같은 상태·방향을 다시 걸면 아무것도 하지 않는다 —
        /// 매 프레임 Play를 불러도 애니메이션이 0번 프레임에 얼어붙지 않게 한다.
        /// </summary>
        public void Play(UnitAnimClip clip, bool loop, bool flipX = false)
        {
            if (!clip.IsValid || _renderer == null) return;
            if (_hasClip && _frame >= 0 && State == clip.state && Direction == clip.direction && _flipX == flipX)
                return;

            _clip = clip;
            _loop = loop;
            _flipX = flipX;
            _hasClip = true;
            State = clip.state;
            Direction = clip.direction;
            _elapsed = 0f;
            _frame = 0;
            Apply();
        }

        public void Clear()
        {
            _hasClip = false;
            _frame = -1;
        }

        private void Update()
        {
            if (!_hasClip || _renderer == null) return;

            int last = _clip.frames.Length - 1;
            if (!_loop && _frame >= last) return;

            _elapsed += Time.deltaTime;
            float step = 1f / _clip.fps;
            while (_elapsed >= step)
            {
                _elapsed -= step;
                if (_frame >= last)
                {
                    if (!_loop) { _frame = last; break; }
                    _frame = 0;
                }
                else _frame++;
            }
            Apply();
        }

        private void Apply()
        {
            Sprite s = _clip.frames[Mathf.Clamp(_frame, 0, _clip.frames.Length - 1)];
            if (s != null) _renderer.sprite = s;
            _renderer.flipX = _flipX;
        }
    }
}
