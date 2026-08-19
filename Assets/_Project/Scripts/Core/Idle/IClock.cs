using System;

namespace MBI.Core
{
    /// <summary>
    /// 현재 시각 공급자(§5-7). 이게 없으면 오프라인 보상을 **검증할 방법이 없다** —
    /// "8시간 뒤"를 실제 시각으로 확인하려면 8시간을 기다려야 하기 때문이다.
    /// 한 줄짜리 비용으로 검증 가능성을 산다.
    ///
    /// 치트 패널의 시간 주입도 이 지점을 그대로 쓴다(신규 경로를 만들지 않는다).
    /// </summary>
    public interface IClock
    {
        DateTimeOffset UtcNow { get; }
    }

    /// <summary>실제 시계. 프로덕션 전용 — 테스트는 가짜 시계를 넣는다.</summary>
    public sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
