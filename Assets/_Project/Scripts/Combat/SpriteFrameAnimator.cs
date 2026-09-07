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
    ///
    /// <b>대기는 되감기(핑퐁)로 돈다</b> — 0·1·2·3·4·3·2·1 (2026-09-07 사용자 판정).
    /// 프레임 파일은 다섯 장 그대로이고 순서만 바뀐다. 처음으로 뚝 끊어 돌아가면
    /// 숨쉬기가 한 박자마다 끊기는데, 되감으면 오르내림이 이어져 보인다.
    /// 이동은 걷는 순환이라 되감으면 뒷걸음질이 되므로 쓰지 않는다.
    /// </summary>
    public sealed class SpriteFrameAnimator : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private UnitAnimClip _clip;
        private bool _loop;
        private bool _hasClip;
        private bool _flipX;
        private bool _pingPong;
        private float _elapsed;
        private int _frame = -1;

        public UnitAnimState State { get; private set; }
        public UnitAnimDirection Direction { get; private set; }
        public bool HasClip => _hasClip;

        /// <summary>재생이 끝났는가. 반복 클립은 언제나 false다.</summary>
        public bool Finished => _hasClip && !_loop && _frame >= _clip.frames.Length - 1;

        private int CycleLength => PingPongCycle(_clip.frames.Length, _pingPong);

        /// <summary>
        /// 되감기 한 바퀴의 걸음 수. 다섯 장이면 여덟이다(0·1·2·3·4·3·2·1) —
        /// 양 끝을 두 번 세지 않는다. 세 장 미만이면 되감을 것이 없어 그냥 순환한다.
        /// <b>규격이라 테스트가 고정한다</b>(<c>UnitAnimWiringTests</c>).
        /// </summary>
        public static int PingPongCycle(int frameCount, bool pingPong) =>
            pingPong && frameCount >= 3 ? frameCount * 2 - 2 : frameCount;

        /// <summary>
        /// 걸음 번호를 프레임 번호로 옮긴다. 되감기 구간은 거꾸로 센다.
        /// <b>규격이라 테스트가 고정한다.</b>
        /// </summary>
        public static int PingPongIndex(int step, int frameCount, bool pingPong)
        {
            if (frameCount <= 0) return 0;
            if (!pingPong || frameCount < 3) return Mathf.Clamp(step, 0, frameCount - 1);
            step = Mathf.Clamp(step, 0, frameCount * 2 - 3);
            return step < frameCount ? step : frameCount * 2 - 2 - step;
        }

        public void Attach(SpriteRenderer target) => _renderer = target;

        /// <summary>
        /// 한 벌을 건다. 같은 상태·방향을 다시 걸면 아무것도 하지 않는다 —
        /// 매 프레임 Play를 불러도 애니메이션이 0번 프레임에 얼어붙지 않게 한다.
        /// </summary>
        public void Play(UnitAnimClip clip, bool loop, bool flipX = false, bool pingPong = false)
        {
            if (!clip.IsValid || _renderer == null) return;
            if (_hasClip && _frame >= 0 && State == clip.state && Direction == clip.direction
                && _flipX == flipX && _pingPong == pingPong)
                return;

            _clip = clip;
            _loop = loop;
            _flipX = flipX;
            _pingPong = pingPong;
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

            int last = CycleLength - 1;
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
            Sprite s = _clip.frames[PingPongIndex(_frame, _clip.frames.Length, _pingPong)];
            if (s != null) _renderer.sprite = s;
            _renderer.flipX = _flipX;
        }
    }
}
