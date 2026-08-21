using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 상주 파밍 층의 스폰 규칙(스테이지 기획서「파밍 규칙」, 2026-08-19 확정). 순수 함수.
    ///
    ///   N초마다 스폰 수 = 정원(M) − 현재 살아 있는 수
    ///
    /// **보충은 한 틱에 전량** 이루어진다. 그래서 보충 직후 맵은 항상 정원이고,
    /// 「한 바퀴」(정원까지 찬 순간 → 다음에 찬 순간)가 **정확히 N초로 고정**된다.
    /// 바퀴 경계를 따로 추적할 필요가 없어져 시급 계산이 "N초 창에 몇 마리 잡았나"로 단순해진다.
    ///
    /// 순차 보충을 쓰지 않는 이유: 화력이 보충 속도를 넘으면 살아 있는 수가 정원에 영영 닿지 못해
    /// 바퀴가 닫히지 않는다. 세질수록 기록이 안 남는 인과 역전이 생긴다.
    ///
    /// ⚠️ 돌파 도전 층에는 적용하지 않는다. 도전은 정해진 적을 전멸시키는 구조라 정원 개념이 없고,
    /// 수입 상한과 종료 조건도 다르다(스테이지 기획서「이층 구조」). 두 스포너는 따로 둔다.
    /// </summary>
    public static class FarmSpawnRule
    {
        /// <summary>이번 보충에서 내보낼 마릿수. 잡아서 생긴 빈 자리만큼만 채운다.</summary>
        public static int RefillCount(int cap, int alive)
        {
            if (cap <= 0) return 0;              // 정원 미확정(TBD) → 스폰하지 않는다
            int need = cap - alive;
            return need > 0 ? need : 0;          // 정원보다 많으면 줄이지는 않는다
        }

        /// <summary>
        /// 파밍 포화 전투력 = (정원 × 몬스터 체력) ÷ 스폰 간격.
        /// 이 화력에 닿으면 파밍 수입이 천장에 걸린다 — 시스템이 막는 게 아니라 잡을 몬스터가 더 안 나와서다.
        /// 표시용(HUD 후보). 0을 돌려주면 미확정이라는 뜻이다.
        /// </summary>
        public static float SaturationPower(int cap, float enemyHp, float intervalSeconds)
        {
            if (cap <= 0 || enemyHp <= 0f || intervalSeconds <= 0f) return 0f;
            return cap * enemyHp / intervalSeconds;
        }

        /// <summary>스폰 속도(마리/초) = 정원 ÷ 간격. 미확정이면 0.</summary>
        public static float SpawnPerSecond(int cap, float intervalSeconds)
        {
            if (cap <= 0 || intervalSeconds <= 0f) return 0f;
            return cap / intervalSeconds;
        }

        /// <summary>한 바퀴 시급 = (처치수 × 마리당고철) ÷ 바퀴 소요초 × 3600.</summary>
        public static double HourlyRate(int kills, double scrapPerKill, float lapSeconds)
        {
            if (kills <= 0 || scrapPerKill <= 0d || lapSeconds <= 0f) return 0d;
            // ×3600을 빠뜨리면 지급량이 1/60이 된다(초 → 시간 환산).
            return kills * scrapPerKill / lapSeconds * 3600d;
        }

        /// <summary>보충 배치를 아레나 경계에 균등 배치(결정론 — 난수 0).</summary>
        public static Vector2 RingPosition(int index, int count, float radius)
        {
            if (count <= 0) return Vector2.zero;
            float angle = 2f * Mathf.PI * index / count;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
    }
}
