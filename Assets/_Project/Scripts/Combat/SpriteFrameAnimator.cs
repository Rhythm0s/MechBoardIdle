using MBI.Core.Anim;
using MBI.Data;
using UnityEngine;

namespace MBI.Combat
{
    /// <summary>
    /// <see cref="SpriteRenderer"/>에 칸 목록을 돌린다. 프레임을 만들어도 이것이 없으면
    /// 화면에 안 나온다 — 2026-09-07까지 `Art/Anim/`을 읽는 코드가 아예 없었다.
    ///
    /// <b>한 칸은 1/16초다</b>(`260907_W01` 4장). 초당 프레임은 폐기했다 — 공통 속도를 두면
    /// 그림 수가 부드러움과 길이를 겸해 무거운 로봇이 가벼운 몬스터보다 빠르게 들썩인다.
    /// 칸 목록은 <see cref="AnimSchedule"/>가 만든다. <b>왕복(대기)은 목록이 이미 펴져 있으므로</b>
    /// 여기서 되감기를 따로 계산하지 않는다.
    ///
    /// 사망은 되감지 않는다. 마지막 칸에서 멈추고 그대로 남는다 —
    /// 주저앉은 기체가 다시 일어서면 안 된다.
    /// </summary>
    public sealed class SpriteFrameAnimator : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private UnitAnimClip _clip;
        private int[] _cells;
        private bool _loop;
        private bool _hasClip;
        private bool _flipX;
        private float _elapsed;
        private int _cell = -1;

        public UnitAnimState State { get; private set; }
        public UnitAnimDirection Direction { get; private set; }
        public bool HasClip => _hasClip;

        /// <summary>지금 걸린 벌의 실제 초. 목표 초와 다르면 반올림이 생긴 것이다.</summary>
        public float ActualSeconds { get; private set; }

        /// <summary>재생이 끝났는가. 반복 클립은 언제나 false다.</summary>
        public bool Finished => _hasClip && !_loop && _cells != null && _cell >= _cells.Length - 1;

        public void Attach(SpriteRenderer target) => _renderer = target;

        /// <summary>
        /// 한 벌을 건다. 같은 상태·방향을 다시 걸면 아무것도 하지 않는다 —
        /// 매 프레임 Play를 불러도 애니메이션이 첫 칸에 얼어붙지 않게 한다.
        /// </summary>
        public void Play(UnitAnimClip clip, bool loop, bool flipX = false)
        {
            if (!clip.IsValid || _renderer == null) return;
            if (_hasClip && _cell >= 0 && State == clip.state && Direction == clip.direction && _flipX == flipX)
                return;

            AnimSchedule schedule = AnimSchedule.Build(
                clip.frames.Length, clip.targetSeconds, clip.pingPong, clip.dwellCells, clip.cellOrder);
            if (schedule.Cells == null || schedule.Cells.Length == 0) return;

            _clip = clip;
            _cells = schedule.Cells;
            ActualSeconds = schedule.ActualSeconds;
            _loop = loop;
            _flipX = flipX;
            _hasClip = true;
            State = clip.state;
            Direction = clip.direction;
            _elapsed = 0f;
            _cell = 0;
            Apply();
        }

        public void Clear()
        {
            _hasClip = false;
            _cell = -1;
        }

        private void Update()
        {
            if (!_hasClip || _renderer == null || _cells == null) return;

            int last = _cells.Length - 1;
            if (!_loop && _cell >= last) return;

            _elapsed += Time.deltaTime;
            const float step = 1f / AnimSchedule.CellsPerSecond;
            while (_elapsed >= step)
            {
                _elapsed -= step;
                if (_cell >= last)
                {
                    if (!_loop) { _cell = last; break; }
                    _cell = 0;
                }
                else _cell++;
            }
            Apply();
        }

        private void Apply()
        {
            int frame = _cells[Mathf.Clamp(_cell, 0, _cells.Length - 1)];
            Sprite s = _clip.frames[Mathf.Clamp(frame, 0, _clip.frames.Length - 1)];
            if (s != null) _renderer.sprite = s;
            _renderer.flipX = _flipX;
        }
    }
}
