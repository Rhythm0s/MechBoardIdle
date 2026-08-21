namespace MBI.Core
{
    /// <summary>
    /// 상주 파밍 스포너(§5-7). N초마다 정원까지 전량 보충하고, 그 틱이 곧 「한 바퀴」의 경계다.
    ///
    /// 순수 클래스 — 씬도 시뮬도 모른다. 호출자가 dt와 현재 생존 수를 넣으면
    /// "이번에 몇 마리 내보낼지"와 "바퀴가 닫혔는지"를 돌려준다. 실제 스폰은 호출자가 한다.
    /// 덕분에 EditMode에서 전투 없이도 규칙을 검증할 수 있다.
    ///
    /// 정원(M)·간격(N)은 balance_v4.json stages[].spawnCap / spawnInterval에서 오며 **현재 전부 TBD(0)** 다.
    /// 0이면 이 스포너는 아무것도 하지 않는다 — 미확정 수치가 게임을 멈추게 하지 않되,
    /// 확정 전까지 파밍이 돌지 않는다는 사실도 감추지 않는다.
    /// </summary>
    public sealed class FarmSpawner
    {
        private readonly ResourceTicker _ticker;

        public FarmSpawner(int cap, float intervalSeconds)
        {
            Cap = cap;
            Interval = intervalSeconds;
            _ticker = new ResourceTicker(intervalSeconds);
        }

        public int Cap { get; }
        public float Interval { get; }

        /// <summary>지금까지 닫힌 바퀴 수. 시급 창의 개수와 같다.</summary>
        public int Laps { get; private set; }

        /// <summary>수치가 확정돼 실제로 돌 수 있는 상태인가.</summary>
        public bool IsConfigured => Cap > 0 && Interval > 0f;

        /// <summary>
        /// 시간을 진행시킨다. 보충 틱이 오면 true와 함께 내보낼 마릿수를 준다.
        ///
        /// 한 프레임에 창이 여러 번 지날 만큼 dt가 크면(간격이 15초 이상이라 실사용에선 없다)
        /// 바퀴는 지난 만큼 세되 보충은 **현재 생존 수 기준 1회**만 계산한다 —
        /// 중간 시점의 생존 수를 알 방법이 없기 때문이다.
        /// </summary>
        public bool Tick(float dt, int aliveCount, out int refill)
        {
            refill = 0;
            if (!IsConfigured) return false;
            if (!_ticker.TryConsume(dt, out int ticks)) return false;

            Laps += ticks;
            refill = FarmSpawnRule.RefillCount(Cap, aliveCount);
            return true;
        }

        public void Reset()
        {
            _ticker.Reset();
            Laps = 0;
        }
    }
}
