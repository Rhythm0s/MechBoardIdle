namespace MBI.Core
{
    /// <summary>한 틱의 결과. 보충량·적립 고철·바퀴 종료 여부와 그 바퀴의 시급.</summary>
    public struct FarmTickResult
    {
        public int refill;          // 이번에 내보낼 마릿수(0이면 보충 틱이 아니거나 정원이 꽉 참)
        public double scrapEarned;  // 이번 틱에 적립된 고철(킬 기반)
        public bool lapClosed;      // 바퀴가 닫혔는가
        public int lapKills;        // 닫힌 바퀴의 처치 수
        public double lapHourlyRate; // 닫힌 바퀴의 시급(고철/시간). lapClosed=false면 0
    }

    /// <summary>
    /// 상주 파밍 한 판(§5-7). 스폰 보충·고철 적립·바퀴 시급 산출을 한 흐름으로 묶는다.
    ///
    /// 묶는 이유는 순서 실수를 구조로 막기 위해서다. 처치를 바퀴에 넣는 시점과 바퀴를 닫는 시점이
    /// 어긋나면 시급이 조용히 틀어지는데, 호출자가 그 순서를 매번 맞추게 두면 언젠가 어긋난다.
    ///
    /// 규칙 원천 = 스테이지 기획서「파밍 규칙」·「오프라인 보상」:
    ///   - 보충은 N초마다 정원까지 한 번에 → 한 바퀴 = N초 고정
    ///   - 한 바퀴 시급 = (그 바퀴 처치수 × 마리당고철) ÷ 바퀴 소요초 × 3600
    ///   - **다 잡고 기다리는 빈 시간도 바퀴에 포함**된다. 바퀴 길이가 N으로 고정이라 자동으로 그렇게 된다 —
    ///     빼면 실제로는 벌 수 없는 속도가 기록되고, 화력이 오를수록 "꺼두는 편이 이득"인 지점이 생긴다
    ///
    /// ⚠️ 고철 적립은 **킬 기반 한 경로뿐**이다. 시급은 오프라인 정산용 기록이지 수입이 아니다 —
    /// 둘을 다 더하면 이중 적립이 된다.
    /// </summary>
    public sealed class FarmSession
    {
        private readonly FarmSpawner _spawner;
        private readonly double _scrapPerKill;

        public FarmSession(int cap, float intervalSeconds, double scrapPerKill)
        {
            _spawner = new FarmSpawner(cap, intervalSeconds);
            _scrapPerKill = scrapPerKill;
        }

        /// <summary>진행 중인 바퀴에서 지금까지 잡은 수.</summary>
        public int KillsThisLap { get; private set; }

        /// <summary>닫힌 바퀴 수.</summary>
        public int Laps => _spawner.Laps;

        /// <summary>정원·간격이 확정돼 실제로 돌 수 있는가.</summary>
        public bool IsConfigured => _spawner.IsConfigured;

        public float LapSeconds => _spawner.Interval;

        /// <summary>
        /// 한 프레임 진행. killsSinceLastTick은 <see cref="CombatSimulation.ConsumeKills"/>로 가져온 값을 넣는다
        /// (그 API가 가져가며 비우므로 같은 처치를 두 번 세지 않는다).
        /// </summary>
        public FarmTickResult Tick(float dt, int aliveCount, int killsSinceLastTick)
        {
            var r = new FarmTickResult();

            if (killsSinceLastTick > 0)
            {
                KillsThisLap += killsSinceLastTick;
                r.scrapEarned = KillRewardRule.Scrap(killsSinceLastTick, _scrapPerKill);
            }

            // 처치를 먼저 반영한 뒤에 바퀴를 닫는다. 순서가 뒤바뀌면 바퀴 끝에 잡은 적이 다음 바퀴로 밀린다.
            if (_spawner.Tick(dt, aliveCount, out int refill))
            {
                r.refill = refill;
                r.lapClosed = true;
                r.lapKills = KillsThisLap;
                r.lapHourlyRate = FarmSpawnRule.HourlyRate(KillsThisLap, _scrapPerKill, _spawner.Interval);
                KillsThisLap = 0;
            }

            return r;
        }

        public void Reset()
        {
            _spawner.Reset();
            KillsThisLap = 0;
        }
    }
}
