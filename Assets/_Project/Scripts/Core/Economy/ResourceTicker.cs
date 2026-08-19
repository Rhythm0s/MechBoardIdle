namespace MBI.Core
{
    /// <summary>
    /// 고정 주기 타이머(§5-7, 순수). dt를 모아 주기마다 1틱씩 내보낸다.
    ///
    /// 쓰이는 곳이 둘이라 공용으로 둔다:
    ///   - 오토세이브 주기
    ///   - **상주 파밍의 N초 창** — 스폰 보충이 한 틱 전량이라 한 바퀴 = 스폰 간격 N으로 고정이고,
    ///     그래서 바퀴 경계를 따로 추적할 필요 없이 이 타이머 하나로 창을 자른다
    ///     (스테이지 기획서「파밍 규칙」, 2026-08-19 확정).
    ///
    /// 프레임이 크게 튀어 dt가 주기를 여러 번 넘겨도 넘긴 만큼 전부 돌려준다 — 삼켜서 수입이 새면 안 된다.
    /// </summary>
    public sealed class ResourceTicker
    {
        private readonly float _interval;
        private float _accum;

        public ResourceTicker(float intervalSeconds)
        {
            _interval = intervalSeconds > 0f ? intervalSeconds : 0f;
        }

        /// <summary>진행 중인 창의 경과(초). 창 길이 대비 진행도 표시용.</summary>
        public float Elapsed => _accum;

        public float Interval => _interval;

        /// <summary>dt 누적 후 완료된 틱 수를 낸다. 주기가 0이면 절대 틱하지 않는다(미설정 = TBD).</summary>
        public bool TryConsume(float dt, out int ticks)
        {
            ticks = 0;
            if (_interval <= 0f || dt <= 0f) return false;

            _accum += dt;
            while (_accum >= _interval)
            {
                _accum -= _interval;
                ticks++;
            }
            return ticks > 0;
        }

        public void Reset() => _accum = 0f;
    }
}
